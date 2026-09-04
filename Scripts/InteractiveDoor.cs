using Godot;

public partial class InteractiveDoor : Node3D, IInteractable
{
    [ExportGroup("Door Structure")]
    [Export] public Node3D HingeNode;

    [ExportGroup("Door Limits & Physics")]
    [Export] public float MinAngle = 0.0f;
    [Export] public float MaxAngle = 90.0f;
    [Export] public float HandleSensitivity = 0.18f;
    [Export] public float DoorWeight = 10.0f;
    [Export] public bool InvertDirection = false;

    [ExportGroup("Lock & Key System")]
    [Export] public bool IsLocked = false;
    [Export] public string RequiredKeyId = "storage_room";

    [ExportGroup("Spooky Knocking")]
    [Export] public bool EnableKnocking = true;
    [Export] public float MinKnockInterval = 35.0f; // Мінімум секунд до наступного стуку
    [Export] public float MaxKnockInterval = 85.0f; // Максимум секунд
    [Export] public float KnockPlayerDistance = 10.0f; // Стукає лише якщо гравець у радіусі чутності
    [Export] public AudioStreamPlayer3D KnockAudio;

    [ExportGroup("Audio")]
    [Export] public AudioStreamPlayer3D CreakAudio;
    [Export] public AudioStreamPlayer3D LockedAudio;
    [Export] public AudioStreamPlayer3D UnlockAudio;

    public float CurrentAngle { get; private set; } = 0.0f;
    public bool IsGrabbed { get; private set; } = false;

    private float _targetAngle = 0.0f;
    private float _lastAngle = 0.0f;
    private float _lockedSideFactor = 1.0f;
    private float _knockTimer = 0.0f;
    private float _knockShakeOffset = 0.0f;

    public string PromptText
    {
        get
        {
            if (IsLocked) return "[ЛКМ / E] Зачинено на замок";
            if (IsGrabbed) return "Утримуй ЛКМ, щоб вести двері";
            return "[ЛКМ] Взятися за двері";
        }
    }

    public override void _Ready()
    {
        HingeNode ??= GetNodeOrNull<Node3D>("Hinge") ?? this;

        CreakAudio ??= GetNodeOrNull<AudioStreamPlayer3D>("DoorAudio");
        LockedAudio ??= GetNodeOrNull<AudioStreamPlayer3D>("LockedAudio");
        UnlockAudio ??= GetNodeOrNull<AudioStreamPlayer3D>("UnlockAudio");
        KnockAudio ??= GetNodeOrNull<AudioStreamPlayer3D>("KnockAudio");

        CurrentAngle = HingeNode.RotationDegrees.Y;
        _targetAngle = CurrentAngle;
        _lastAngle = CurrentAngle;

        ResetKnockTimer();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (HingeNode == null) return;

        // Плавне повернення здригання від удару
        _knockShakeOffset = Mathf.MoveToward(_knockShakeOffset, 0.0f, (float)delta * 15.0f);

        CurrentAngle = Mathf.Lerp(CurrentAngle, _targetAngle, (float)delta * DoorWeight);
        HingeNode.RotationDegrees = new Vector3(HingeNode.RotationDegrees.X, CurrentAngle + _knockShakeOffset, HingeNode.RotationDegrees.Z);

        float diff = Mathf.Abs(CurrentAngle - _lastAngle);
        if (diff > 0.15f)
        {
            PlayCreakSound();
        }
        _lastAngle = CurrentAngle;

        ProcessKnocking((float)delta);
    }

    private void ProcessKnocking(float delta)
    {
        if (!EnableKnocking || KnockAudio == null || IsGrabbed) return;

        // Стукає лише коли двері зачинені або майже зачинені
        if (Mathf.Abs(CurrentAngle - MinAngle) > 2.0f) return;

        _knockTimer -= delta;
        if (_knockTimer <= 0.0f)
        {
            ResetKnockTimer();

            Player player = GetTree().GetFirstNodeInGroup("Player") as Player;
            if (player != null && GlobalPosition.DistanceTo(player.GlobalPosition) <= KnockPlayerDistance)
            {
                TriggerKnock();
            }
        }
    }

    private void TriggerKnock()
    {
        KnockAudio.PitchScale = (float)GD.RandRange(0.92f, 1.05f);
        KnockAudio.Play();

        // Невеликий імпульс здригання дверей на мілісекунди
        _knockShakeOffset = (float)GD.RandRange(0.8f, 1.6f);
    }

    private void ResetKnockTimer()
    {
        _knockTimer = (float)GD.RandRange(MinKnockInterval, MaxKnockInterval);
    }

    public void Grab(Vector3 playerGlobalPos)
    {
        if (IsLocked)
        {
            TryUnlockOrJiggle();
            return;
        }

        IsGrabbed = true;

        Vector3 referenceForward = GlobalTransform.Basis.Z;
        Vector3 toPlayer = (playerGlobalPos - GlobalPosition).Normalized();

        _lockedSideFactor = toPlayer.Dot(referenceForward) >= 0f ? 1.0f : -1.0f;
        if (InvertDirection) _lockedSideFactor = -_lockedSideFactor;
    }

    public void Release()
    {
        IsGrabbed = false;
    }

    public void MoveWithInput(float mouseDeltaX)
    {
        if (IsLocked) return;

        _targetAngle += mouseDeltaX * HandleSensitivity * _lockedSideFactor;
        _targetAngle = Mathf.Clamp(_targetAngle, MinAngle, MaxAngle);
    }

    public void Interact()
    {
        if (IsLocked)
        {
            TryUnlockOrJiggle();
        }
    }

    private void TryUnlockOrJiggle()
    {
        Player player = GetTree().GetFirstNodeInGroup("Player") as Player;

        if (player != null && player.HasKey(RequiredKeyId))
        {
            IsLocked = false;

            if (UnlockAudio != null)
            {
                UnlockAudio.PitchScale = (float)GD.RandRange(0.95f, 1.05f);
                UnlockAudio.Play();
            }
        }
        else
        {
            if (LockedAudio != null && !LockedAudio.Playing)
            {
                LockedAudio.PitchScale = (float)GD.RandRange(0.92f, 1.08f);
                LockedAudio.Play();
            }
        }
    }

    private void PlayCreakSound()
    {
        if (CreakAudio != null && !CreakAudio.Playing)
        {
            CreakAudio.PitchScale = (float)GD.RandRange(0.85f, 1.15f);
            CreakAudio.Play();
        }
    }
}
