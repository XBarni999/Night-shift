using Godot;

public partial class InteractiveDoor : AnimatableBody3D, IInteractable
{
    [Export] public float MinAngle = 0.0f;
    [Export] public float MaxAngle = 90.0f;
    [Export] public float HandleSensitivity = 0.18f;
    [Export] public float DoorWeight = 10.0f;
    [Export] public bool InvertDirection = false;
    [Export] public AudioStreamPlayer3D CreakAudio;

    public float CurrentAngle { get; private set; } = 0.0f;
    public bool IsGrabbed { get; private set; } = false;

    private float _targetAngle = 0.0f;
    private float _lastAngle = 0.0f;
    private float _lockedSideFactor = 1.0f;

    public string PromptText => IsGrabbed ? "Утримуй ЛКМ, щоб вести двері" : "[ЛКМ] Взятися за двері";

    public override void _Ready()
    {
        CreakAudio ??= GetNodeOrNull<AudioStreamPlayer3D>("DoorAudio");
        CurrentAngle = RotationDegrees.Y;
        _targetAngle = CurrentAngle;
        _lastAngle = CurrentAngle;
    }

    public override void _PhysicsProcess(double delta)
    {
        CurrentAngle = Mathf.Lerp(CurrentAngle, _targetAngle, (float)delta * DoorWeight);
        RotationDegrees = new Vector3(RotationDegrees.X, CurrentAngle, RotationDegrees.Z);

        float diff = Mathf.Abs(CurrentAngle - _lastAngle);
        if (diff > 0.15f)
        {
            PlayCreakSound();
        }
        _lastAngle = CurrentAngle;
    }

    // Тепер передаємо позицію гравця при захопленні
    public void Grab(Vector3 playerGlobalPos)
    {
        IsGrabbed = true;

        // Беремо орієнтацію батьківської ноди (дверної коробки), бо вона НЕ крутиться
        Node3D parent = GetParent<Node3D>();
        Vector3 referenceForward = parent != null ? parent.GlobalTransform.Basis.Z : Vector3.Back;

        Vector3 toPlayer = (playerGlobalPos - GlobalPosition).Normalized();
        
        // Фіксуємо знак раз і назавжди на весь час утримання ЛКМ
        _lockedSideFactor = toPlayer.Dot(referenceForward) >= 0f ? 1.0f : -1.0f;
        if (InvertDirection) _lockedSideFactor = -_lockedSideFactor;
    }

    public void Release()
    {
        IsGrabbed = false;
    }

    public void MoveWithInput(float mouseDeltaX)
    {
        // Використовуємо зафіксований напрямок, ніяких змін знаку на льоту
        _targetAngle += mouseDeltaX * HandleSensitivity * _lockedSideFactor;
        _targetAngle = Mathf.Clamp(_targetAngle, MinAngle, MaxAngle);
    }

    public void Interact()
    {
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
