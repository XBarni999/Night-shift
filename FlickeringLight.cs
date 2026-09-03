using Godot;

public partial class FlickeringLight : OmniLight3D
{
    [Export] public float BaseEnergy = 1.6f;
    [Export] public float SmoothSpeed = 8.0f;
    [Export] public bool IsBroken = false;

    private FastNoiseLite _noise = new();
    private float _time;
    private float _targetEnergy;
    private float _flickerTimer;
    private float _flickerDuration;
    private bool _isFlickering;

    public override void _Ready()
    {
        if (BaseEnergy <= 0f)
        {
            BaseEnergy = LightEnergy;
        }

        _targetEnergy = BaseEnergy;

        _noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        _noise.Frequency = 1.2f;

        _flickerTimer = (float)GD.RandRange(1.5f, 4.0f);
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;

        float dt = (float)delta;
        _time += dt * 40.0f;

        if (IsBroken)
        {
            float brokenNoise = (_noise.GetNoise1D(_time) + 1f) * 0.5f;
            _targetEnergy = Mathf.Lerp(BaseEnergy * 0.1f, BaseEnergy * 0.8f, brokenNoise);
        }
        else
        {
            if (_isFlickering)
            {
                _flickerDuration -= dt;
                if (_flickerDuration <= 0f)
                {
                    _isFlickering = false;
                    _flickerTimer = (float)GD.RandRange(3.0f, 7.0f);
                }
                else
                {
                    float drop = Mathf.Clamp((_noise.GetNoise1D(_time * 2f) + 1f) * 0.5f, 0.15f, 0.7f);
                    _targetEnergy = BaseEnergy * drop;
                }
            }
            else
            {
                _flickerTimer -= dt;
                if (_flickerTimer <= 0f)
                {
                    _isFlickering = true;
                    _flickerDuration = (float)GD.RandRange(0.2f, 0.6f);
                }
                else
                {
                    float wobble = _noise.GetNoise1D(_time) * (BaseEnergy * 0.18f);
                    _targetEnergy = Mathf.Clamp(BaseEnergy + wobble, BaseEnergy * 0.75f, BaseEnergy * 1.1f);
                }
            }
        }

        LightEnergy = Mathf.Lerp(LightEnergy, _targetEnergy, dt * SmoothSpeed);
    }
}
