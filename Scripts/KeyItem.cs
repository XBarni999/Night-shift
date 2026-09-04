using Godot;

public partial class KeyItem : StaticBody3D, IInteractable
{
    [Export] public string KeyId = "storage_room"; // Унікальний ID цього ключа
    [Export] public string KeyName = "Ключ від складу";
    [Export] public AudioStream PickupSound;

    [ExportGroup("Visuals")]
    [Export] public Node3D VisualsNode;
    [Export] public OmniLight3D GlowLight;
    [Export] public float RotateSpeed = 60.0f;
    [Export] public float BobSpeed = 2.8f;
    [Export] public float BobHeight = 0.05f;

    private float _bobTimer = 0.0f;
    private float _baseVisualY = 0.0f;

    public string PromptText => $"[E] Підібрати {KeyName}";

    public override void _Ready()
    {
        VisualsNode ??= GetNodeOrNull<Node3D>("Visuals");
        GlowLight ??= GetNodeOrNull<OmniLight3D>("Visuals/OmniLight3D");

        if (VisualsNode != null)
        {
            _baseVisualY = VisualsNode.Position.Y;
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _bobTimer += dt;

        if (VisualsNode != null)
        {
            Vector3 rot = VisualsNode.RotationDegrees;
            rot.Y += RotateSpeed * dt;
            VisualsNode.RotationDegrees = rot;

            Vector3 pos = VisualsNode.Position;
            pos.Y = _baseVisualY + Mathf.Sin(_bobTimer * BobSpeed) * BobHeight;
            VisualsNode.Position = pos;
        }
    }

    public void Interact()
    {
        Player player = GetTree().GetFirstNodeInGroup("Player") as Player;
        if (player != null)
        {
            player.AddKey(KeyId);

            if (PickupSound != null)
            {
                AudioStreamPlayer tempAudio = new AudioStreamPlayer();
                tempAudio.Stream = PickupSound;
                tempAudio.PitchScale = (float)GD.RandRange(0.95f, 1.05f);
                GetTree().Root.AddChild(tempAudio);
                tempAudio.Play();
                tempAudio.Finished += () => tempAudio.QueueFree();
            }

            QueueFree();
        }
    }
}
