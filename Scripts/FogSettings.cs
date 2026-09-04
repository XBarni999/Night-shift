using Godot;

public partial class FogSettings : HSlider
{
    [Export] public WorldEnvironment TargetWorldEnvironment;
    [Export] public Label ValueDisplay;

    public override void _Ready()
    {
        // Шукаємо WorldEnvironment на сцені, якщо не призначено вручну
        TargetWorldEnvironment ??= GetTree().Root.FindChild("WorldEnvironment", true, false) as WorldEnvironment;
        ValueDisplay ??= GetNodeOrNull<Label>("../FogValueLabel");

        // Зчитуємо початкове значення з сцени, якщо воно є
        if (TargetWorldEnvironment?.Environment != null)
        {
            var env = TargetWorldEnvironment.Environment;
            if (env.FogEnabled)
            {
                Value = env.FogDensity;
            }
            else if (env.VolumetricFogEnabled)
            {
                Value = env.VolumetricFogDensity;
            }
        }

        UpdateValueLabel((float)Value);
        ValueChanged += OnSliderValueChanged;
    }

    private void OnSliderValueChanged(double value)
    {
        float density = (float)value;
        UpdateFog(density);
        UpdateValueLabel(density);
    }

    private void UpdateFog(float density)
    {
        if (TargetWorldEnvironment?.Environment == null) return;

        var env = TargetWorldEnvironment.Environment;

        // Для звичайного туману
        if (density <= 0.0001f)
        {
            env.FogEnabled = false;
            env.VolumetricFogEnabled = false;
        }
        else
        {
            // Звичайний туман
            env.FogEnabled = true;
            env.FogDensity = density;

            // Якщо використовуєш об'ємний (Volumetric) туман:
            if (env.VolumetricFogEnabled)
            {
                env.VolumetricFogDensity = density;
            }
        }
    }

    private void UpdateValueLabel(float density)
    {
        if (ValueDisplay != null)
        {
            ValueDisplay.Text = density.ToString("0.000");
        }
    }
}
