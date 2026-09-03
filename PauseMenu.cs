using Godot;

public partial class PauseMenu : Control
{
    [Export] public WorldEnvironment WorldEnv;
    [Export] public CheckButton FogToggle;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;

        // Початковий стан: меню сховане, гра активна
        Hide();
        GetTree().Paused = false;
        Input.MouseMode = Input.MouseModeEnum.Captured;

        GetNode<Button>("CenterContainer/VBoxContainer/ResumeButton").Pressed += ResumeGame;
        GetNode<Button>("CenterContainer/VBoxContainer/QuitButton").Pressed += QuitGame;

        WorldEnv ??= GetTree().Root.FindChild("WorldEnvironment", true, false) as WorldEnvironment;
        FogToggle ??= GetNodeOrNull<CheckButton>("CenterContainer/VBoxContainer/FogToggle");

        if (WorldEnv?.Environment != null && FogToggle != null)
        {
            FogToggle.FocusMode = FocusModeEnum.None;
            FogToggle.SetPressedNoSignal(WorldEnv.Environment.VolumetricFogEnabled);
            FogToggle.Toggled += OnFogToggled;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();

            if (GetTree().Paused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    public void PauseGame()
    {
        Show();
        GetTree().Paused = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public void ResumeGame()
    {
        Hide();
        GetTree().Paused = false;
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    private void QuitGame()
    {
        GetTree().Quit();
    }

    private void OnFogToggled(bool isVolumetric)
    {
        if (WorldEnv?.Environment == null) return;

        var env = WorldEnv.Environment;

        if (isVolumetric)
        {
            env.VolumetricFogEnabled = true;
            env.FogEnabled = false;
        }
        else
        {
            env.VolumetricFogEnabled = false;
            env.FogEnabled = true;
            env.FogLightColor = new Color("#1a1c1e");
            env.FogDensity = 0.04f;
        }
    }
}
