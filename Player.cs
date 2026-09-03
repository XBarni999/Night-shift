using Godot;

public partial class Player : CharacterBody3D
{
    [Export] public float WalkSpeed = 3.2f;
    [Export] public float SprintSpeed = 5.8f;
    [Export] public float CrouchSpeed = 1.6f;
    [Export] public float MouseSensitivity = 0.0025f;
    [Export] public float Gravity = 9.8f;
    [Export] public Vector3 FlashlightOffset = new Vector3(0.28f, -0.22f, -0.3f);
    [Export] public Node3D FlashlightHolder;

    [Export] public float FlashlightRotationSmoothness = 15.0f;
    [Export] public float FlashlightPosSmoothness = 20.0f;
    [Export] public float FlashlightBobAmount = 0.015f;
    [Export] public float FlashlightBobSpeed = 8.0f;

    [Export] public Node3D Head;
    [Export] public SpotLight3D Flashlight;
    [Export] public RayCast3D InteractRay;
    [Export] public Label InteractPrompt;
    [Export] public float BaseFlashlightEnergy = 2.5f;
    [Export] public float MinFlickerInterval = 10.0f;
    [Export] public float MaxFlickerInterval = 25.0f;

    // Налаштування утримання фізичних об'єктів
    [Export] public float HoldDistance = 1.8f;
    [Export] public float PullPower = 24.0f;
    [Export] public float ThrowForce = 12.0f;
    [Export] public float RotationPower = 12.0f;

    private float _flickerTimer;
    private float _flickerDuration;
    private bool _isFlickering;
    private float _bobTimer = 0f;

    private bool _isCrouching;
    private bool _isSprinting;

    private IHoldable _heldObject;

    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Captured;

        Head ??= GetNodeOrNull<Node3D>("Head");
        Flashlight ??= GetNodeOrNull<SpotLight3D>("SpotLight3D");
        InteractRay ??= GetNodeOrNull<RayCast3D>("Head/Camera3D/RayCast3D") ?? GetNodeOrNull<RayCast3D>("Head/RayCast3D");
        InteractPrompt ??= GetNodeOrNull<Label>("UI/InteractPrompt");

        _flickerTimer = (float)GD.RandRange(MinFlickerInterval, MaxFlickerInterval);

        if (Flashlight != null)
        {
            Flashlight.LightEnergy = BaseFlashlightEnergy;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            RotateY(-mouseMotion.Relative.X * MouseSensitivity);

            if (Head != null)
            {
                Head.RotateX(-mouseMotion.Relative.Y * MouseSensitivity);
                Vector3 rot = Head.Rotation;
                rot.X = Mathf.Clamp(rot.X, Mathf.DegToRad(-85f), Mathf.DegToRad(85f));
                Head.Rotation = rot;
            }
        }

        if (@event.IsActionPressed("flashlight") && Flashlight != null)
        {
            Flashlight.Visible = !Flashlight.Visible;
        }

        if (@event.IsActionPressed("interact"))
        {
            if (_heldObject != null)
            {
                DropObject();
            }
            else
            {
                TryInteract();
            }
        }

        // Кидок на ліву кнопку миші
        if (@event is InputEventMouseButton mouseBtn && mouseBtn.Pressed && mouseBtn.ButtonIndex == MouseButton.Left)
        {
            if (_heldObject != null)
            {
                ThrowObject();
            }
        }
    }

    public override void _Process(double delta)
    {
        UpdateInteractionUI();
        UpdateFlashlightSway((float)delta);
        UpdateFlashlightFlicker((float)delta);
    }

    public override void _PhysicsProcess(double delta)
    {
        Vector3 vel = Velocity;

        if (!IsOnFloor())
        {
            vel.Y -= Gravity * (float)delta;
        }

        _isCrouching = Input.IsActionPressed("crouch");
        float targetHeadY = _isCrouching ? 0.9f : 1.6f;

        if (Head != null)
        {
            Vector3 headPos = Head.Position;
            headPos.Y = Mathf.Lerp(headPos.Y, targetHeadY, (float)delta * 12.0f);
            Head.Position = headPos;
        }

        _isSprinting = Input.IsActionPressed("sprint") && !_isCrouching;

        float currentSpeed = WalkSpeed;
        if (_isCrouching) currentSpeed = CrouchSpeed;
        else if (_isSprinting) currentSpeed = SprintSpeed;

        Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        Vector3 direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();

        if (direction != Vector3.Zero)
        {
            vel.X = direction.X * currentSpeed;
            vel.Z = direction.Z * currentSpeed;
        }
        else
        {
            vel.X = Mathf.MoveToward(Velocity.X, 0, currentSpeed);
            vel.Z = Mathf.MoveToward(Velocity.Z, 0, currentSpeed);
        }

        Velocity = vel;
        MoveAndSlide();

        HoldPhysicsProcess((float)delta);
    }

    private void HoldPhysicsProcess(float delta)
    {
        if (_heldObject == null || Head == null) return;

        RigidBody3D body = _heldObject.Body;
        if (!IsInstanceValid(body))
        {
            _heldObject = null;
            return;
        }

        Camera3D camera = Head.GetNodeOrNull<Camera3D>("Camera3D");
        Transform3D camTransform = camera != null ? camera.GlobalTransform : Head.GlobalTransform;

        Vector3 targetPoint = camTransform.Origin + (-camTransform.Basis.Z * HoldDistance);
        Vector3 directionToTarget = targetPoint - body.GlobalPosition;
        float distance = directionToTarget.Length();

        if (distance > HoldDistance * 2.5f)
        {
            DropObject();
            return;
        }

        body.LinearVelocity = directionToTarget * PullPower;

        Basis currentBasis = body.GlobalBasis;
        Basis targetBasis = camTransform.Basis;

        Quaternion currentQuat = currentBasis.GetRotationQuaternion();
        Quaternion targetQuat = targetBasis.GetRotationQuaternion();

        Quaternion diffQuat = targetQuat * currentQuat.Inverse();

        Vector3 rotAxis = new Vector3(diffQuat.X, diffQuat.Y, diffQuat.Z);
        float rotAngle = 2.0f * Mathf.Acos(Mathf.Clamp(diffQuat.W, -1.0f, 1.0f));

        if (rotAngle > Mathf.Pi)
        {
            rotAngle -= 2.0f * Mathf.Pi;
        }

        if (rotAxis.LengthSquared() > 0.0001f)
        {
            rotAxis = rotAxis.Normalized();
            body.AngularVelocity = rotAxis * (rotAngle * RotationPower);
        }
    }

    private void TryInteract()
    {
        if (InteractRay == null || !InteractRay.IsColliding()) return;

        var collider = InteractRay.GetCollider();

        if (collider is IHoldable holdable)
        {
            PickupObject(holdable);
            return;
        }

        if (collider is IInteractable target)
        {
            target.Interact();
        }
    }

    private void PickupObject(IHoldable holdable)
    {
        _heldObject = holdable;
        _heldObject.Pickup();
    }

    private void DropObject()
    {
        if (_heldObject == null) return;

        _heldObject.Drop();
        _heldObject = null;
    }

    private void ThrowObject()
    {
        if (_heldObject == null || Head == null) return;

        RigidBody3D body = _heldObject.Body;
        _heldObject.Drop();

        Camera3D camera = Head.GetNodeOrNull<Camera3D>("Camera3D");
        Vector3 shootDir = camera != null ? -camera.GlobalTransform.Basis.Z : -Head.GlobalTransform.Basis.Z;

        body.LinearVelocity = shootDir * ThrowForce;

        body.AngularVelocity = new Vector3(
            (float)GD.RandRange(-8.0f, 8.0f),
            (float)GD.RandRange(-8.0f, 8.0f),
            (float)GD.RandRange(-8.0f, 8.0f)
        );

        _heldObject = null;
    }

    private void UpdateInteractionUI()
    {
        if (InteractPrompt == null) return;

        if (_heldObject != null)
        {
            InteractPrompt.Text = "[E] Кинути під ноги  |  [ЛКМ] Жбурнути";
            return;
        }

        if (InteractRay != null && InteractRay.IsColliding() && InteractRay.GetCollider() is IInteractable target)
        {
            InteractPrompt.Text = target.PromptText;
        }
        else
        {
            InteractPrompt.Text = string.Empty;
        }
    }

    private void UpdateFlashlightSway(float delta)
    {
        if (Head == null || Flashlight == null) return;

        // Поворот за головою
        float currentPitch = Flashlight.Rotation.X;
        float targetPitch = Head.Rotation.X;
        float newPitch = Mathf.LerpAngle(currentPitch, targetPitch, delta * FlashlightRotationSmoothness);
        Flashlight.Rotation = new Vector3(newPitch, 0, 0);

        // Рахуємо горизонтальну швидкість руху
        Vector2 horizontalVel = new Vector2(Velocity.X, Velocity.Z);
        float speed = horizontalVel.Length();

        float bobOffsetPos = 0f;
        float bobOffsetPosSide = 0f;

        // Хитання ліхтарика лише коли гравець іде по землі
        if (IsOnFloor() && speed > 0.3f)
        {
            float speedFactor = Mathf.Clamp(speed / SprintSpeed, 0.4f, 1.2f);
            _bobTimer += delta * FlashlightBobSpeed * speedFactor;

            bobOffsetPos = Mathf.Sin(_bobTimer) * FlashlightBobAmount;
            bobOffsetPosSide = Mathf.Cos(_bobTimer * 0.5f) * (FlashlightBobAmount * 0.6f);
        }
        else
        {
            // Плавно скидаємо таймер і крок, коли стоїмо
            _bobTimer = Mathf.MoveToward(_bobTimer, 0f, delta * 4f);
        }

        Vector3 targetPos = Head.Position + FlashlightOffset + new Vector3(bobOffsetPosSide, bobOffsetPos, 0);
        Flashlight.Position = Flashlight.Position.Lerp(targetPos, delta * FlashlightPosSmoothness);
    }

    private void UpdateFlashlightFlicker(float delta)
    {
        if (Flashlight == null || !Flashlight.Visible) return;

        if (_isFlickering)
        {
            _flickerDuration -= delta;
            if (_flickerDuration <= 0f)
            {
                _isFlickering = false;
                Flashlight.LightEnergy = BaseFlashlightEnergy;
                _flickerTimer = (float)GD.RandRange(MinFlickerInterval, MaxFlickerInterval);
            }
            else
            {
                if (GD.Randf() > 0.35f)
                {
                    Flashlight.LightEnergy = (float)GD.RandRange(0.2f, BaseFlashlightEnergy * 0.6f);
                }
                else
                {
                    Flashlight.LightEnergy = 0.0f;
                }
            }
        }
        else
        {
            _flickerTimer -= delta;
            if (_flickerTimer <= 0f)
            {
                _isFlickering = true;
                _flickerDuration = (float)GD.RandRange(0.3f, 0.9f);
            }
        }
    }
}
