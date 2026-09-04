using Godot;

public partial class DeathScreen : CanvasLayer
{
    [Export] public Label DeathTitle;
    [Export] public Button RestartButton;
    [Export] public ColorRect Background;
    [Export] public AudioStreamPlayer DeathAudio;

    public override void _Ready()
    {
        Visible = false;
        ProcessMode = ProcessModeEnum.Always;

        DeathTitle ??= GetNodeOrNull<Label>("Content/DeathTitle");
        RestartButton ??= GetNodeOrNull<Button>("Content/RestartButton");
        Background ??= GetNodeOrNull<ColorRect>("Background");
        DeathAudio ??= GetNodeOrNull<AudioStreamPlayer>("DeathAudio");

        if (DeathAudio != null)
        {
            DeathAudio.ProcessMode = ProcessModeEnum.Always;
        }

        if (RestartButton != null)
        {
            RestartButton.Pressed += OnRestartPressed;
        }
    }

    public void TriggerDeath()
    {
        Visible = true;
        GetTree().Paused = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;

        // Запуск звуку смерті
        if (DeathAudio != null && DeathAudio.Stream != null)
        {
            DeathAudio.Play();
        }

        if (Background != null) Background.Modulate = new Color(1, 1, 1, 0);
        if (DeathTitle != null) DeathTitle.Modulate = new Color(1, 1, 1, 0);
        if (RestartButton != null)
        {
            RestartButton.Modulate = new Color(1, 1, 1, 0);
            RestartButton.Disabled = true;
        }

        Tween tween = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);

        // Затемнення
        if (Background != null)
        {
            tween.TweenProperty(Background, "modulate:a", 1.0f, 0.6f);
        }

        // Поява напису "СМЕРТЬ"
        if (DeathTitle != null)
        {
            tween.TweenProperty(DeathTitle, "modulate:a", 1.0f, 1.2f).SetTrans(Tween.TransitionType.Cubic);
        }

        // Кнопка рестарту
        if (RestartButton != null)
        {
            tween.TweenProperty(RestartButton, "modulate:a", 1.0f, 0.5f);
            tween.TweenCallback(Callable.From(() => RestartButton.Disabled = false));
        }
    }

    private void OnRestartPressed()
    {
        GetTree().Paused = false;
        GetTree().ReloadCurrentScene();
    }
}
