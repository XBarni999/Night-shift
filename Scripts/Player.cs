using Godot;
using System.Collections.Generic;

public partial class Player : CharacterBody3D
{
    [ExportGroup("Movement")]
    [Export] public float WalkSpeed = 3.2f;
    [Export] public float SprintSpeed = 5.8f;
    [Export] public float CrouchSpeed = 1.6f;
    [Export] public float JumpVelocity = 4.5f;
    [Export] public float Gravity = 12.0f;
    [Export] public float MouseSensitivity = 0.0025f;

    [ExportGroup("Crouch & Collision")]
    [Export] public CollisionShape3D PlayerCollider;
    [Export] public float StandingHeight = 1.8f;
    [Export] public float CrouchingHeight = 1.0f;
    [Export] public float StandingHeadY = 1.6f;
    [Export] public float CrouchingHeadY = 0.85f;

    [ExportGroup("Stamina System")]
    [Export] public float MaxStamina = 100.0f;
    [Export] public float StaminaDrainSprint = 22.0f;
    [Export] public float StaminaDrainJump = 18.0f;
    [Export] public float StaminaRegenRate = 16.0f;
    [Export] public float StaminaRegenDelay = 1.2f;

    [ExportGroup("Flashlight & Battery")]
    [Export] public SpotLight3D Flashlight;
    [Export] public Vector3 FlashlightOffset = new Vector3(0.28f, -0.22f, -0.3f);
    [Export] public float FlashlightRotationSmoothness = 14.0f;
    [Export] public float FlashlightPosSmoothness = 18.0f;
    [Export] public float FlashlightBobMultiplier = 1.25f;
    [Export] public float BaseFlashlightEnergy = 2.5f;
    [Export] public float BatteryDrainRate = 1.2f;
    [Export] public float MaxBattery = 100.0f;

    [ExportGroup("HUD Elements")]
    [Export] public CanvasItem HUDGroup;
    [Export] public ProgressBar StaminaBar;
    [Export] public HBoxContainer BatterySegments;
    [Export] public Label BatteryCountLabel;
    [Export] public Label InteractPrompt;
    [Export] public DeathScreen PlayerDeathScreen;

    [ExportGroup("Audio")]
    [Export] public AudioStreamPlayer FlashlightAudio;
    [Export] public AudioStreamPlayer FootstepAudio;
    [Export] public AudioStreamPlayer JumpAudio;
    [Export] public AudioStreamPlayer BatteryReloadAudio;

    [ExportGroup("Interaction & Physics")]
    [Export] public Node3D Head;
    [Export] public Camera3D Camera;
    [Export] public RayCast3D InteractRay;
    [Export] public float HoldDistance = 1.8f;
    [Export] public float PullPower = 24.0f;
    [Export] public float ThrowForce = 12.0f;
    [Export] public float RotationPower = 12.0f;
    [Export] public float PushForce = 1.5f;

    public float CurrentStamina { get; private set; }
    private float _staminaRegenTimer = 0f;
    private bool _isExhausted = false;
    private readonly HashSet<string> _keys = new HashSet<string>();

    public float CurrentBattery { get; private set; }
    public int InventoryBatteries { get; private set; } = 0;
    private bool _isReloading = false;

    private float _stepCycle = 0f;
    private bool _stepTriggered = false;
    private bool _isLeftFoot = false;
    private Vector3 _baseCamPos;
    private float _lowBatteryBlinkTimer = 0f;

    private bool _isCrouching;
    private bool _isSprinting;
    private bool _wasOnFloor = true;

    private InteractiveDoor _grabbedDoor = null;
    private IHoldable _heldObject;

    public override void _Ready()
    {
        AddToGroup("Player");
        Input.MouseMode = Input.MouseModeEnum.Captured;

        Head ??= GetNodeOrNull<Node3D>("Head");
        Camera ??= GetNodeOrNull<Camera3D>("Head/Camera3D");
        Flashlight ??= GetNodeOrNull<SpotLight3D>("SpotLight3D");
        InteractRay ??= GetNodeOrNull<RayCast3D>("Head/Camera3D/RayCast3D") ?? GetNodeOrNull<RayCast3D>("Head/RayCast3D");
        InteractPrompt ??= GetNodeOrNull<Label>("UI/InteractPrompt");
        PlayerCollider ??= GetNodeOrNull<CollisionShape3D>("CollisionShape3D");

        HUDGroup ??= GetNodeOrNull<CanvasItem>("UI/HUDContainer");
        StaminaBar ??= GetNodeOrNull<ProgressBar>("UI/HUDContainer/VBoxContainer/StaminaBar");
        BatterySegments ??= GetNodeOrNull<HBoxContainer>("UI/HUDContainer/VBoxContainer/BatteryContainer/BatterySegments");
        BatteryCountLabel ??= GetNodeOrNull<Label>("UI/HUDContainer/VBoxContainer/BatteryContainer/BatteryCountLabe")
                           ?? GetNodeOrNull<Label>("UI/HUDContainer/VBoxContainer/BatteryContainer/BatteryCountLabel");

        FlashlightAudio ??= GetNodeOrNull<AudioStreamPlayer>("FlashlightAudio");
        FootstepAudio ??= GetNodeOrNull<AudioStreamPlayer>("FootstepAudio");
        JumpAudio ??= GetNodeOrNull<AudioStreamPlayer>("JumpAudio");
        BatteryReloadAudio ??= GetNodeOrNull<AudioStreamPlayer>("BatteryReloadAudio");

        CurrentStamina = MaxStamina;
        CurrentBattery = MaxBattery;

        if (StaminaBar != null)
        {
            StaminaBar.MinValue = 0;
            StaminaBar.MaxValue = MaxStamina;
            StaminaBar.Value = CurrentStamina;
            Color c = StaminaBar.Modulate;
            c.A = 0.0f;
            StaminaBar.Modulate = c;
        }

        if (Camera != null)
        {
            _baseCamPos = Camera.Position;
        }

        if (PlayerCollider?.Shape != null)
        {
            PlayerCollider.Shape = (Shape3D)PlayerCollider.Shape.Duplicate();
        }

        if (Flashlight != null)
        {
            Flashlight.LightEnergy = BaseFlashlightEnergy;
        }
    }

    public void AddKey(string keyId)
    {
        if (!_keys.Contains(keyId))
        {
            _keys.Add(keyId);
        }
    }

    public bool HasKey(string keyId)
    {
        return _keys.Contains(keyId);
    }

    private InteractiveDoor FindDoor(GodotObject obj)
    {
        if (obj is InteractiveDoor door) return door;

        if (obj is Node node)
        {
            Node current = node.GetParent();
            while (current != null)
            {
                if (current is InteractiveDoor parentDoor) return parentDoor;
                current = current.GetParent();
            }

            if (node.Owner is InteractiveDoor ownerDoor) return ownerDoor;
        }

        return null;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            if (_grabbedDoor != null)
            {
                _grabbedDoor.MoveWithInput(mouseMotion.Relative.X);
            }

            float sens = _grabbedDoor != null ? MouseSensitivity * 0.45f : MouseSensitivity;
            RotateY(-mouseMotion.Relative.X * sens);

            if (Head != null)
            {
                Head.RotateX(-mouseMotion.Relative.Y * sens);
                Vector3 rot = Head.Rotation;
                rot.X = Mathf.Clamp(rot.X, Mathf.DegToRad(-85f), Mathf.DegToRad(85f));
                Head.Rotation = rot;
            }
        }

        if (@event is InputEventMouseButton mouseBtn && mouseBtn.ButtonIndex == MouseButton.Left)
        {
            if (mouseBtn.Pressed)
            {
                if (_heldObject != null)
                {
                    ThrowObject();
                }
                else if (InteractRay != null && InteractRay.IsColliding())
                {
                    var collider = InteractRay.GetCollider();
                    var door = FindDoor(collider);

                    if (door != null)
                    {
                        _grabbedDoor = door;
                        _grabbedDoor.Grab(GlobalPosition);
                    }
                }
            }
            else
            {
                if (_grabbedDoor != null)
                {
                    _grabbedDoor.Release();
                    _grabbedDoor = null;
                }
            }
        }

        if (@event.IsActionPressed("flashlight") && Flashlight != null)
        {
            if (CurrentBattery > 0f && !_isReloading)
            {
                Flashlight.Visible = !Flashlight.Visible;
                PlayFlashlightClick();
            }
        }

        if (@event.IsActionPressed("reload"))
        {
            ReloadBattery();
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
    }

    public override void _Process(double delta)
    {
        if (_grabbedDoor != null)
        {
            if (!IsInstanceValid(_grabbedDoor) || GlobalPosition.DistanceTo(_grabbedDoor.GlobalPosition) > 2.6f)
            {
                _grabbedDoor.Release();
                _grabbedDoor = null;
            }
        }

        UpdateInteractionUI();
        UpdateViewAndFlashlightBob((float)delta);
        UpdateBatteryLogic((float)delta);
        UpdateHUD((float)delta);
    }

    public override void _PhysicsProcess(double delta)
    {
        Vector3 vel = Velocity;

        if (!IsOnFloor())
        {
            vel.Y -= Gravity * (float)delta;
        }

        _isCrouching = Input.IsActionPressed("crouch");
        UpdateCrouchCollision((float)delta);

        Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        bool isMoving = inputDir != Vector2.Zero;

        bool wantsSprint = Input.IsActionPressed("sprint") && isMoving && !_isCrouching;

        if (_isExhausted && CurrentStamina >= MaxStamina * 0.25f)
        {
            _isExhausted = false;
        }

        _isSprinting = wantsSprint && !_isExhausted && CurrentStamina > 0f;

        if (_isSprinting)
        {
            CurrentStamina = Mathf.Max(0f, CurrentStamina - StaminaDrainSprint * (float)delta);
            _staminaRegenTimer = StaminaRegenDelay;
            if (CurrentStamina <= 0f)
            {
                _isExhausted = true;
            }
        }
        else
        {
            if (_staminaRegenTimer > 0f)
            {
                _staminaRegenTimer -= (float)delta;
            }
            else if (CurrentStamina < MaxStamina)
            {
                CurrentStamina = Mathf.Min(MaxStamina, CurrentStamina + StaminaRegenRate * (float)delta);
            }
        }

        if (IsOnFloor() && Input.IsActionJustPressed("jump") && !_isCrouching)
        {
            if (CurrentStamina >= StaminaDrainJump)
            {
                vel.Y = JumpVelocity;
                CurrentStamina -= StaminaDrainJump;
                _staminaRegenTimer = StaminaRegenDelay;
                PlayJumpSound();
            }
        }

        if (!_wasOnFloor && IsOnFloor())
        {
            PlayFootstepSound();
        }
        _wasOnFloor = IsOnFloor();

        float currentSpeed = WalkSpeed;
        if (_isCrouching) currentSpeed = CrouchSpeed;
        else if (_isSprinting) currentSpeed = SprintSpeed;

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

        for (int i = 0; i < GetSlideCollisionCount(); i++)
        {
            var collision = GetSlideCollision(i);
            if (collision.GetCollider() is RigidBody3D rigidBody)
            {
                if (_heldObject != null && _heldObject.Body == rigidBody) continue;

                rigidBody.Sleeping = false;
                Vector3 pushDir = -collision.GetNormal();
                pushDir.Y = 0;

                float moveSpeed = Velocity.Length();
                if (moveSpeed > 0.1f)
                {
                    rigidBody.ApplyCentralImpulse(pushDir * moveSpeed * PushForce);
                }
            }
        }

        HoldPhysicsProcess((float)delta);
    }

    private void UpdateBatteryLogic(float delta)
    {
        if (Flashlight == null) return;

        if (Flashlight.Visible && CurrentBattery > 0f)
        {
            CurrentBattery = Mathf.Max(0f, CurrentBattery - BatteryDrainRate * delta);

            if (CurrentBattery < 20.0f)
            {
                float dimFactor = Mathf.Clamp(CurrentBattery / 20.0f, 0.2f, 1.0f);
                if (GD.Randf() > 0.85f)
                {
                    Flashlight.LightEnergy = (float)GD.RandRange(0.1f, BaseFlashlightEnergy * 0.4f);
                }
                else
                {
                    Flashlight.LightEnergy = BaseFlashlightEnergy * dimFactor;
                }
            }
            else
            {
                Flashlight.LightEnergy = BaseFlashlightEnergy;
            }

            if (CurrentBattery <= 0f)
            {
                Flashlight.Visible = false;
                PlayFlashlightClick();
            }
        }
    }

    public void AddBattery(int count)
    {
        InventoryBatteries += count;
    }

    public void ReloadBattery()
    {
        if (_isReloading || InventoryBatteries <= 0 || CurrentBattery >= MaxBattery) return;

        _isReloading = true;
        InventoryBatteries--;

        if (Flashlight != null)
        {
            Flashlight.Visible = false;
        }

        if (BatteryReloadAudio != null && BatteryReloadAudio.Stream != null)
        {
            BatteryReloadAudio.PitchScale = (float)GD.RandRange(0.95f, 1.05f);
            BatteryReloadAudio.Play();

            void OnReloadFinished()
            {
                BatteryReloadAudio.Finished -= OnReloadFinished;
                FinishReload();
            }

            BatteryReloadAudio.Finished += OnReloadFinished;
        }
        else
        {
            GetTree().CreateTimer(1.0).Timeout += FinishReload;
        }
    }

    private void FinishReload()
    {
        CurrentBattery = MaxBattery;
        _isReloading = false;

        if (Flashlight != null)
        {
            Flashlight.Visible = true;
            PlayFlashlightClick();
        }
    }

    private void UpdateHUD(float delta)
    {
        if (StaminaBar != null)
        {
            StaminaBar.Value = CurrentStamina;
            float targetAlpha = CurrentStamina < MaxStamina - 1.5f ? 1.0f : 0.0f;
            Color c = StaminaBar.Modulate;
            c.A = Mathf.Lerp(c.A, targetAlpha, delta * 5.0f);
            StaminaBar.Modulate = c;
        }

        if (BatterySegments != null)
        {
            var segments = BatterySegments.GetChildren();
            int totalSegments = segments.Count;

            if (totalSegments > 0)
            {
                float percent = CurrentBattery / MaxBattery;
                int activeCount = Mathf.CeilToInt(percent * totalSegments);

                _lowBatteryBlinkTimer += delta * 5.0f;
                bool blinkOn = Mathf.Sin(_lowBatteryBlinkTimer) > 0;

                for (int i = 0; i < totalSegments; i++)
                {
                    if (segments[i] is Control seg)
                    {
                        bool isSegmentLit = i < activeCount;

                        if (activeCount == 1 && i == 0 && Flashlight != null && Flashlight.Visible)
                        {
                            seg.Visible = blinkOn;
                        }
                        else
                        {
                            seg.Visible = isSegmentLit;
                        }
                    }
                }

                float targetAlpha = (Flashlight != null && Flashlight.Visible) ? 1.0f : 0.35f;
                Color bc = BatterySegments.Modulate;
                bc.A = Mathf.Lerp(bc.A, targetAlpha, delta * 4.0f);
                BatterySegments.Modulate = bc;
            }
        }

        if (BatteryCountLabel != null)
        {
            BatteryCountLabel.Text = $"x{InventoryBatteries}";
        }
    }

    private void UpdateCrouchCollision(float delta)
    {
        float targetHeight = _isCrouching ? CrouchingHeight : StandingHeight;
        float targetHeadY = _isCrouching ? CrouchingHeadY : StandingHeadY;

        if (Head != null)
        {
            Vector3 headPos = Head.Position;
            headPos.Y = Mathf.Lerp(headPos.Y, targetHeadY, delta * 12.0f);
            Head.Position = headPos;
        }

        if (PlayerCollider?.Shape is CapsuleShape3D capsule)
        {
            capsule.Height = Mathf.Lerp(capsule.Height, targetHeight, delta * 12.0f);
            Vector3 colPos = PlayerCollider.Position;
            colPos.Y = capsule.Height * 0.5f;
            PlayerCollider.Position = colPos;
        }
        else if (PlayerCollider?.Shape is CylinderShape3D cylinder)
        {
            cylinder.Height = Mathf.Lerp(cylinder.Height, targetHeight, delta * 12.0f);
            Vector3 colPos = PlayerCollider.Position;
            colPos.Y = cylinder.Height * 0.5f;
            PlayerCollider.Position = colPos;
        }
    }

    private void UpdateViewAndFlashlightBob(float delta)
    {
        Vector2 horizontalVel = new Vector2(Velocity.X, Velocity.Z);
        float speed = horizontalVel.Length();

        float bobX = 0f;
        float bobY = 0f;
        float bobTilt = 0f;

        if (IsOnFloor() && speed > 0.35f)
        {
            float speedRatio = speed / WalkSpeed;
            _stepCycle += delta * 9.0f * speedRatio;

            bobY = Mathf.Sin(_stepCycle) * 0.04f;
            bobX = Mathf.Cos(_stepCycle * 0.5f) * 0.025f;
            bobTilt = Mathf.Cos(_stepCycle * 0.5f) * 0.012f;

            float sinVal = Mathf.Sin(_stepCycle);
            if (sinVal < -0.85f && !_stepTriggered)
            {
                PlayFootstepSound();
                _stepTriggered = true;
            }
            else if (sinVal > 0f)
            {
                _stepTriggered = false;
            }
        }
        else
        {
            _stepCycle = Mathf.MoveToward(_stepCycle, 0f, delta * 4.0f);
            _stepTriggered = false;
        }

        if (Camera != null)
        {
            Vector3 targetCamPos = _baseCamPos + new Vector3(bobX, bobY, 0);
            Camera.Position = Camera.Position.Lerp(targetCamPos, delta * 14.0f);

            Vector3 camRot = Camera.Rotation;
            camRot.Z = Mathf.Lerp(camRot.Z, -bobTilt, delta * 10.0f);
            Camera.Rotation = camRot;
        }

        if (Head != null && Flashlight != null)
        {
            float targetPitch = Head.Rotation.X;
            float newPitch = Mathf.LerpAngle(Flashlight.Rotation.X, targetPitch, delta * FlashlightRotationSmoothness);
            float newRoll = Mathf.LerpAngle(Flashlight.Rotation.Z, bobTilt * 1.5f, delta * 8.0f);
            Flashlight.Rotation = new Vector3(newPitch, 0, newRoll);

            Vector3 targetFlashlightPos = Head.Position + FlashlightOffset + new Vector3(bobX * FlashlightBobMultiplier, bobY * FlashlightBobMultiplier, 0);
            Flashlight.Position = Flashlight.Position.Lerp(targetFlashlightPos, delta * FlashlightPosSmoothness);
        }
    }

    private void PlayFootstepSound()
    {
        if (FootstepAudio == null || FootstepAudio.Stream == null) return;

        _isLeftFoot = !_isLeftFoot;
        float footPitchOffset = _isLeftFoot ? -0.04f : 0.04f;

        FootstepAudio.PitchScale = (float)GD.RandRange(0.86f, 1.14f) + footPitchOffset;

        float baseVolume = -9.0f;
        if (_isCrouching) baseVolume = -16.0f;
        else if (_isSprinting) baseVolume = -5.0f;

        float volumeJitter = (float)GD.RandRange(-1.8f, 1.2f);
        FootstepAudio.VolumeDb = baseVolume + volumeJitter;

        FootstepAudio.Play();
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

        body.Sleeping = false;

        Camera3D camera = Camera ?? Head.GetNodeOrNull<Camera3D>("Camera3D");
        Transform3D camTransform = camera != null ? camera.GlobalTransform : Head.GlobalTransform;

        Vector3 targetPoint = camTransform.Origin + (-camTransform.Basis.Z * HoldDistance);
        Vector3 directionToTarget = targetPoint - body.GlobalPosition;
        float distance = directionToTarget.Length();

        if (distance > HoldDistance * 2.8f)
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

        var door = FindDoor(collider);
        if (door != null)
        {
            door.Interact();
            return;
        }

        IHoldable holdable = FindHoldable(collider);
        if (holdable != null)
        {
            PickupObject(holdable);
            return;
        }

        if (collider is IInteractable target)
        {
            target.Interact();
        }
    }

    private IHoldable FindHoldable(GodotObject obj)
    {
        if (obj is IHoldable h) return h;

        if (obj is Node node)
        {
            if (node.GetParent() is IHoldable parentH) return parentH;

            foreach (var child in node.GetChildren())
            {
                if (child is IHoldable childH) return childH;
            }
        }

        return null;
    }
    public void Die()
{
    // Забороняємо подвійне спрацьовування
    if (GetTree().Paused) return;

    if (PlayerDeathScreen != null)
    {
        PlayerDeathScreen.TriggerDeath();
    }
    else
    {
        // Шукаємо по сцені, якщо не прикріплено в інспекторі
        var screen = GetTree().Root.FindChild("DeathScreen", true, false) as DeathScreen;
        screen?.TriggerDeath();
    }
}

    private void PickupObject(IHoldable holdable)
    {
        _heldObject = holdable;

        if (_heldObject.Body != null)
        {
            _heldObject.Body.Sleeping = false;
            _heldObject.Body.AddCollisionExceptionWith(this);
        }

        _heldObject.Pickup();
    }

    private void DropObject()
    {
        if (_heldObject == null) return;

        if (_heldObject.Body != null)
        {
            _heldObject.Body.RemoveCollisionExceptionWith(this);
        }

        _heldObject.Drop();
        _heldObject = null;
    }

    private void ThrowObject()
    {
        if (_heldObject == null || Head == null) return;

        RigidBody3D body = _heldObject.Body;
        if (body != null)
        {
            body.RemoveCollisionExceptionWith(this);
        }

        _heldObject.Drop();

        Camera3D camera = Camera ?? Head.GetNodeOrNull<Camera3D>("Camera3D");
        Vector3 shootDir = camera != null ? -camera.GlobalTransform.Basis.Z : -Head.GlobalTransform.Basis.Z;

        if (body != null)
        {
            body.Sleeping = false;
            body.LinearVelocity = shootDir * ThrowForce;
            body.AngularVelocity = new Vector3(
                (float)GD.RandRange(-8.0f, 8.0f),
                (float)GD.RandRange(-8.0f, 8.0f),
                (float)GD.RandRange(-8.0f, 8.0f)
            );
        }

        _heldObject = null;
    }

    private void UpdateInteractionUI()
    {
        if (InteractPrompt == null) return;

        if (_grabbedDoor != null)
        {
            InteractPrompt.Text = _grabbedDoor.PromptText;
            return;
        }

        if (_heldObject != null)
        {
            InteractPrompt.Text = "[E] Кинути під ноги | [ЛКМ] Жбурнути";
            return;
        }

        if (InteractRay != null && InteractRay.IsColliding())
        {
            var collider = InteractRay.GetCollider();

            var door = FindDoor(collider);
            if (door != null)
            {
                InteractPrompt.Text = door.PromptText;
                return;
            }

            if (FindHoldable(collider) != null)
            {
                InteractPrompt.Text = "[E] Взяти предмет";
                return;
            }

            if (collider is IInteractable target)
            {
                InteractPrompt.Text = target.PromptText;
                return;
            }
        }

        InteractPrompt.Text = string.Empty;
    }

    private void PlayFlashlightClick()
    {
        if (FlashlightAudio != null)
        {
            FlashlightAudio.PitchScale = (float)GD.RandRange(0.92f, 1.08f);
            FlashlightAudio.Play();
        }
    }

    private void PlayJumpSound()
    {
        if (JumpAudio != null)
        {
            JumpAudio.PitchScale = (float)GD.RandRange(0.9f, 1.1f);
            JumpAudio.Play();
        }
    }
}
