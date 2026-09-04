using Godot;

public partial class BatteryItem : StaticBody3D, IInteractable
{
    [ExportGroup("Pickup Settings")]
    [Export] public int BatteryAmount = 1;
    [Export] public AudioStream PickupSound;

    [ExportGroup("Floating & Glow")]
    [Export] public Node3D VisualsNode;
    [Export] public OmniLight3D GlowLight;
    [Export] public float RotateSpeed = 75.0f;
    [Export] public float BobSpeed = 3.2f;
    [Export] public float BobHeight = 0.08f;
    [Export] public float LightPulseSpeed = 4.0f;

    private float _bobTimer = 0.0f;
    private float _baseVisualY = 0.0f;
    private float _baseLightEnergy = 1.2f;

    public string PromptText => "[E] Підібрати батарейку";

    public override void _Ready()
    {
        VisualsNode ??= GetNodeOrNull<Node3D>("Visuals");
        GlowLight ??= GetNodeOrNull<OmniLight3D>("Visuals/OmniLight3D");

        if (VisualsNode != null)
        {
            _baseVisualY = VisualsNode.Position.Y;
        }

        if (GlowLight != null)
        {
            _baseLightEnergy = GlowLight.LightEnergy;
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _bobTimer += dt;

        if (VisualsNode != null)
        {
            // Обертання навколо вертикальної осі як у GTA
            Vector3 rot = VisualsNode.RotationDegrees;
            rot.Y += RotateSpeed * dt;
            VisualsNode.RotationDegrees = rot;

            // Плавне левітування вгору і вниз по синусоїді
            Vector3 pos = VisualsNode.Position;
            pos.Y = _baseVisualY + Mathf.Sin(_bobTimer * BobSpeed) * BobHeight;
            VisualsNode.Position = pos;
        }

        // М'яка пульсація світла
        if (GlowLight != null)
        {
            float pulse = 0.8f + Mathf.Sin(_bobTimer * LightPulseSpeed) * 0.25f;
            GlowLight.LightEnergy = _baseLightEnergy * pulse;
        }
    }

    public void Interact()
    {
        Player player = GetTree().GetFirstNodeInGroup("Player") as Player;
        if (player != null)
        {
            player.AddBattery(BatteryAmount);

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
