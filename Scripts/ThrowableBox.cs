using Godot;

public partial class ThrowableBox : RigidBody3D, IHoldable
{
    public RigidBody3D Body => this;

    [Export] public AudioStreamPlayer3D ImpactAudio;
    [Export] public float MinImpactVelocity = 1.2f;

    private float _defaultGravityScale;
    private float _defaultLinearDamp;
    private float _defaultAngularDamp;

    private Vector3 _previousVelocity;
    private double _hitCooldown = 0.0;

    public override void _Ready()
    {
        ImpactAudio ??= GetNodeOrNull<AudioStreamPlayer3D>("ImpactAudio");

        _defaultGravityScale = GravityScale;
        _defaultLinearDamp = LinearDamp;
        _defaultAngularDamp = AngularDamp;

        ContactMonitor = true;
        MaxContactsReported = 6;
        BodyEntered += OnBodyEntered;

        CanSleep = false;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_hitCooldown > 0.0)
        {
            _hitCooldown -= delta;
        }

        // Зберігаємо швидкість до фізичного контакту
        _previousVelocity = LinearVelocity;
    }

    public void Pickup()
    {
        Sleeping = false;
        Freeze = false;
        GravityScale = 0.0f;
        LinearDamp = 8.0f;
        AngularDamp = 8.0f;
    }

    public void Drop()
    {
        GravityScale = _defaultGravityScale;
        LinearDamp = _defaultLinearDamp;
        AngularDamp = _defaultAngularDamp;
        Sleeping = false;
    }

    private void OnBodyEntered(Node node)
    {
        if (ImpactAudio == null || _hitCooldown > 0.0) return;

        // Беремо максимальну швидкість: або поточну, або ту, що була кадр тому
        float impactSpeed = Mathf.Max(LinearVelocity.Length(), _previousVelocity.Length());

        if (impactSpeed >= MinImpactVelocity)
        {
            // Перезапускаємо аудіо, навіть якщо старий звук ще дограє
            if (ImpactAudio.Playing)
            {
                ImpactAudio.Stop();
            }

            float volume = Mathf.Clamp(Mathf.LinearToDb(impactSpeed / 6.5f), -18.0f, 2.0f);
            ImpactAudio.VolumeDb = volume;
            ImpactAudio.PitchScale = (float)GD.RandRange(0.85f, 1.15f);
            ImpactAudio.Play();

            // Короткий кулдаун, щоб звук не тріщав сотню разів при коченні
            _hitCooldown = 0.08;
        }
    }
}
