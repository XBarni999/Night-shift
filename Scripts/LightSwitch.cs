using Godot;

public partial class LightSwitch : StaticBody3D, IInteractable
{
    // Масив ламп: можна додати одну, можна десяток
    [Export] public Godot.Collections.Array<Light3D> TargetLights = new();

    // Початковий стан при старті гри
    [Export] public bool IsOn = true;

    [Export] public AudioStreamPlayer3D ClickSound;

    public string PromptText => IsOn ? "[E] Вимкнути світло" : "[E] Увімкнути світло";

    public override void _Ready()
    {
        ApplyLightsState();
    }

    public void Interact()
    {
        IsOn = !IsOn;
        ApplyLightsState();

        if (ClickSound != null)
        {
            ClickSound.PitchScale = (float)GD.RandRange(0.9f, 1.1f);
            ClickSound.Play();
        }
    }

    private void ApplyLightsState()
    {
        foreach (var light in TargetLights)
        {
            if (IsInstanceValid(light))
            {
                light.Visible = IsOn;
            }
        }
    }
}
