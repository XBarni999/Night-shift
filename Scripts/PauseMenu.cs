using Godot;

public partial class PauseMenu : Control
{
    [Export] public WorldEnvironment WorldEnv;
    [Export] public CheckButton FogToggle;
    [Export] public CheckButton PotatoToggle;
    [Export] public CheckButton FpsToggle;
    [Export] public OptionButton ResolutionOption;
    [Export] public Label FpsLabel;
    [Export] public Control SettingsPanel;
    [Export] public Control SettingsDivider;
    [Export] public Button ApplySettingsButton;

    private const string ConfigPath = "user://settings.cfg";
    private ConfigFile _config = new();

    private readonly Vector2I[] _resolutions = new Vector2I[]
    {
        new Vector2I(1280, 720),
        new Vector2I(1366, 768),
        new Vector2I(1600, 900),
        new Vector2I(1920, 1080),
        new Vector2I(2560, 1440)
    };

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;

        var resumeBtn = FindChild("ResumeButt", true, false) as Button 
                     ?? FindChild("ResumeButton", true, false) as Button;

        var settingsBtn = FindChild("SettingsButt", true, false) as Button 
                       ?? FindChild("SettingsButton", true, false) as Button;

        var quitBtn = FindChild("QuitButton", true, false) as Button;

        SettingsPanel ??= FindChild("SettingsPanel", true, false) as Control;
        SettingsDivider ??= FindChild("SettingsDivider", true, false) as Control;
        FogToggle ??= FindChild("FogToggle", true, false) as CheckButton;
        PotatoToggle ??= FindChild("PotatoToggle", true, false) as CheckButton;
        FpsToggle ??= FindChild("FpsToggle", true, false) as CheckButton;
        ResolutionOption ??= FindChild("ResolutionOption", true, false) as OptionButton 
                          ?? FindChild("ResolutionOptio", true, false) as OptionButton;
        ApplySettingsButton ??= FindChild("ApplySettingsButton", true, false) as Button;

        WorldEnv ??= GetTree().Root.FindChild("WorldEnvironment", true, false) as WorldEnvironment;
        FpsLabel ??= GetTree().Root.FindChild("FpsLabel", true, false) as Label;

        if (resumeBtn != null) resumeBtn.Pressed += ResumeGame;
        if (quitBtn != null) quitBtn.Pressed += QuitGame;
        if (settingsBtn != null) settingsBtn.Pressed += ToggleSettingsPanel;
        if (ApplySettingsButton != null) ApplySettingsButton.Pressed += OnApplySettingsPressed;

        SetupResolutionDropdown();

        if (FogToggle != null) FogToggle.FocusMode = FocusModeEnum.None;
        if (PotatoToggle != null) PotatoToggle.FocusMode = FocusModeEnum.None;
        if (FpsToggle != null) FpsToggle.FocusMode = FocusModeEnum.None;
        if (ResolutionOption != null) ResolutionOption.FocusMode = FocusModeEnum.None;

        LoadAndApplySettings();

        // Початковий стан: приховані налаштування і захоплена миша
        SetSettingsVisible(false);
        ResumeGame();
    }

    public override void _Process(double delta)
    {
        if (FpsLabel != null && FpsLabel.Visible)
        {
            FpsLabel.Text = $"FPS: {Engine.GetFramesPerSecond()}";
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();

            if (Visible)
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
        SetSettingsVisible(false);
        Visible = true;
        GetTree().Paused = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public void ResumeGame()
    {
        Visible = false;
        GetTree().Paused = false;
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    private void ToggleSettingsPanel()
    {
        if (SettingsPanel == null) return;
        bool newState = !SettingsPanel.Visible;
        SetSettingsVisible(newState);
    }

    private void SetSettingsVisible(bool visible)
    {
        if (SettingsPanel != null) SettingsPanel.Visible = visible;
        if (SettingsDivider != null) SettingsDivider.Visible = visible;
    }

    private void SetupResolutionDropdown()
    {
        if (ResolutionOption == null) return;

        ResolutionOption.Clear();
        for (int i = 0; i < _resolutions.Length; i++)
        {
            ResolutionOption.AddItem($"{_resolutions[i].X} x {_resolutions[i].Y}", i);
        }
    }

    private void OnApplySettingsPressed()
{
    // 1. Справжнє внутрішнє масштабування рендеру (3D Resolution)
    if (ResolutionOption != null && ResolutionOption.Selected >= 0 && ResolutionOption.Selected < _resolutions.Length)
    {
        ApplyInternalResolution(_resolutions[ResolutionOption.Selected]);
    }

    // 2. Лічильник FPS
    if (FpsLabel != null && FpsToggle != null)
    {
        FpsLabel.Visible = FpsToggle.ButtonPressed;
    }

    // 3. Графіка
    bool isPotato = PotatoToggle != null && PotatoToggle.ButtonPressed;
    bool isVolumetric = FogToggle != null && FogToggle.ButtonPressed;

    if (isPotato)
    {
        ApplyPotatoMode(true);
    }
    else
    {
        ApplyPotatoMode(false);
        ApplyFog(isVolumetric);
    }

    SaveSettings();
    SetSettingsVisible(false);
}
private void ApplyInternalResolution(Vector2I targetRes)
{
    var viewport = GetViewport();
    Vector2I windowSize = DisplayServer.WindowGetSize();

    // Якщо раптом розмір вікна 0, беремо розмір екрана
    if (windowSize.Y <= 0) windowSize = DisplayServer.ScreenGetSize();

    // Рахуємо відсоток масштабування відносно поточної висоти вікна
    float scale = (float)targetRes.Y / (float)windowSize.Y;
    scale = Mathf.Clamp(scale, 0.25f, 1.0f);

    viewport.Scaling3DMode = Viewport.Scaling3DModeEnum.Bilinear;
    viewport.Scaling3DScale = scale;
}

    private void ApplyFog(bool isVolumetric)
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

    private void ApplyPotatoMode(bool enabled)
    {
        var viewport = GetViewport();

        if (enabled)
        {
            viewport.Scaling3DScale = 0.6f;
            viewport.Scaling3DMode = Viewport.Scaling3DModeEnum.Bilinear;
            ToggleAllShadows(false);

            if (WorldEnv?.Environment != null)
            {
                var env = WorldEnv.Environment;
                env.VolumetricFogEnabled = false;
                env.FogEnabled = true;
                env.FogLightColor = new Color("#1a1c1e");
                env.FogDensity = 0.04f;
                env.GlowEnabled = false;
                env.SsaoEnabled = false;
            }

            if (FogToggle != null) FogToggle.SetPressedNoSignal(false);
        }
        else
        {
            viewport.Scaling3DScale = 1.0f;
            ToggleAllShadows(true);

            if (WorldEnv?.Environment != null)
            {
                var env = WorldEnv.Environment;
                env.VolumetricFogEnabled = true;
                env.FogEnabled = false;
                env.GlowEnabled = true;
            }
        }
    }

    private void ToggleAllShadows(bool enableShadows)
    {
        foreach (var child in GetTree().Root.FindChildren("*", "Light3D", true, false))
        {
            if (child is Light3D l)
            {
                l.ShadowEnabled = enableShadows;
            }
        }
    }

    private void QuitGame()
    {
        GetTree().Quit();
    }

    private void SaveSettings()
    {
        bool volumetric = FogToggle != null && FogToggle.ButtonPressed;
        bool potato = PotatoToggle != null && PotatoToggle.ButtonPressed;
        bool showFps = FpsToggle != null && FpsToggle.ButtonPressed;
        int resIndex = ResolutionOption != null ? ResolutionOption.Selected : 3;

        _config.SetValue("Graphics", "VolumetricFog", volumetric);
        _config.SetValue("Graphics", "PotatoMode", potato);
        _config.SetValue("UI", "ShowFPS", showFps);
        _config.SetValue("Display", "ResolutionIndex", resIndex);
        _config.Save(ConfigPath);
    }

    private void LoadAndApplySettings()
    {
        _config.Load(ConfigPath);

        bool volumetric = (bool)_config.GetValue("Graphics", "VolumetricFog", true);
        bool potato = (bool)_config.GetValue("Graphics", "PotatoMode", false);
        bool showFps = (bool)_config.GetValue("UI", "ShowFPS", true);
        int resIndex = (int)_config.GetValue("Display", "ResolutionIndex", 3);

        if (PotatoToggle != null) PotatoToggle.SetPressedNoSignal(potato);
        if (FogToggle != null) FogToggle.SetPressedNoSignal(volumetric);
        if (FpsToggle != null) FpsToggle.SetPressedNoSignal(showFps);

        if (ResolutionOption != null && resIndex >= 0 && resIndex < _resolutions.Length)
{
    ResolutionOption.Select(resIndex);
    ApplyInternalResolution(_resolutions[resIndex]);
}

        if (FpsLabel != null)
        {
            FpsLabel.Visible = showFps;
        }

        if (potato)
        {
            ApplyPotatoMode(true);
        }
        else
        {
            ApplyFog(volumetric);
        }
    }
}
