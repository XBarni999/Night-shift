using Godot;

public partial class Enemy : CharacterBody3D
{
    public enum EnemyState
    {
        Idle,
        Patrol,
        InvestigateSound,
        Hunt
    }

    [ExportGroup("Movement")]
    [Export] public float PatrolSpeed = 1.8f;
    [Export] public float HuntSpeed = 6.2f;
    [Export] public float Acceleration = 12.0f;
    [Export] public float Gravity = 12.0f;

    [ExportGroup("Hearing / Detection")]
    [Export] public float HearingMultiplier = 1.0f;
    [Export] public float StopInvestigateDistance = 1.2f;

    [ExportGroup("Audio")]
    [Export] public AudioStreamPlayer3D ScreamAudio;
    [Export] public AudioStreamPlayer3D FootstepsAudio;

    public EnemyState CurrentState { get; private set; } = EnemyState.Idle;

    private NavigationAgent3D _navAgent;
    private Player _player;
    private Vector3 _targetNoisePos;
    private float _idleWaitTimer = 0f;
    private float _footstepTimer = 0f;

    public override void _Ready()
    {
        AddToGroup("Enemy");

        _navAgent = GetNodeOrNull<NavigationAgent3D>("NavigationAgent3D");
        _player = GetTree().GetFirstNodeInGroup("Player") as Player;

        ScreamAudio ??= GetNodeOrNull<AudioStreamPlayer3D>("ScreamAudio");
        FootstepsAudio ??= GetNodeOrNull<AudioStreamPlayer3D>("FootstepsAudio");

        var killArea = GetNodeOrNull<Area3D>("KillArea");
        if (killArea != null)
        {
            killArea.BodyEntered += OnKillAreaBodyEntered;
        }

        // Початкова пауза
        _idleWaitTimer = 2.0f;
    }

    public override void _PhysicsProcess(double delta)
    {
        Vector3 vel = Velocity;

        if (!IsOnFloor())
        {
            vel.Y -= Gravity * (float)delta;
        }

        float dt = (float)delta;
        ProcessListening();
        ProcessState(dt, ref vel);

        Velocity = vel;
        MoveAndSlide();

        HandleFootstepSounds(dt);
    }

    // Головна фішка: слухаємо шум гравця залежно від його дій
    private void ProcessListening()
    {
        if (_player == null) return;

        float dist = GlobalPosition.DistanceTo(_player.GlobalPosition);

        // Радіуси шуму гравця
        float noiseRadius = 0.0f;

        Vector2 horizontalVel = new Vector2(_player.Velocity.X, _player.Velocity.Z);
        float playerSpeed = horizontalVel.Length();

        if (playerSpeed > 0.2f && _player.IsOnFloor())
        {
            // Крадеться: майже безшумний
            if (Input.IsActionPressed("crouch"))
            {
                noiseRadius = 2.5f;
            }
            // Спринт: чути на пів коридору
            else if (Input.IsActionPressed("sprint"))
            {
                noiseRadius = 18.0f;
            }
            // Звичайна хода
            else
            {
                noiseRadius = 8.5f;
            }
        }

        // Стрибок видає сильний звук при приземленні чи поштовху
        if (Input.IsActionJustPressed("jump"))
        {
            noiseRadius = 14.0f;
        }

        noiseRadius *= HearingMultiplier;

        // Якщо звук у радіусі слуху монстра
        if (noiseRadius > 0f && dist <= noiseRadius)
        {
            HearSound(_player.GlobalPosition, isPlayerRunning: playerSpeed > 4.0f);
        }
    }

    public void HearSound(Vector3 soundPos, bool isPlayerRunning = false)
    {
        _targetNoisePos = soundPos;

        if (CurrentState != EnemyState.Hunt)
        {
            if (ScreamAudio != null && !ScreamAudio.Playing)
            {
                ScreamAudio.PitchScale = (float)GD.RandRange(0.9f, 1.1f);
                ScreamAudio.Play();
            }
        }

        CurrentState = isPlayerRunning ? EnemyState.Hunt : EnemyState.InvestigateSound;
        _navAgent.TargetPosition = _targetNoisePos;
    }

    private void ProcessState(float delta, ref Vector3 vel)
    {
        float currentSpeed = (CurrentState == EnemyState.Hunt) ? HuntSpeed : PatrolSpeed;

        switch (CurrentState)
        {
            case EnemyState.Idle:
                vel.X = Mathf.MoveToward(vel.X, 0, Acceleration * delta);
                vel.Z = Mathf.MoveToward(vel.Z, 0, Acceleration * delta);

                _idleWaitTimer -= delta;
                if (_idleWaitTimer <= 0f)
                {
                    PickRandomWanderPoint();
                }
                break;

            case EnemyState.Patrol:
            case EnemyState.InvestigateSound:
            case EnemyState.Hunt:
                if (_navAgent.IsNavigationFinished())
                {
                    CurrentState = EnemyState.Idle;
                    _idleWaitTimer = (float)GD.RandRange(2.0f, 5.0f);
                    break;
                }

                Vector3 nextPathPos = _navAgent.GetNextPathPosition();
                Vector3 moveDir = (nextPathPos - GlobalPosition).Normalized();
                moveDir.Y = 0;

                vel.X = Mathf.Lerp(vel.X, moveDir.X * currentSpeed, delta * Acceleration);
                vel.Z = Mathf.Lerp(vel.Z, moveDir.Z * currentSpeed, delta * Acceleration);
                break;
        }
    }

    private void PickRandomWanderPoint()
    {
        Vector3 randomOffset = new Vector3(
            (float)GD.RandRange(-8.0f, 8.0f),
            0,
            (float)GD.RandRange(-8.0f, 8.0f)
        );

        _targetNoisePos = GlobalPosition + randomOffset;
        _navAgent.TargetPosition = _targetNoisePos;
        CurrentState = EnemyState.Patrol;
    }

    private void HandleFootstepSounds(float delta)
    {
        if (FootstepsAudio == null) return;

        Vector2 horizontalVel = new Vector2(Velocity.X, Velocity.Z);
        float speed = horizontalVel.Length();

        if (speed > 0.5f && IsOnFloor())
        {
            _footstepTimer += delta * (speed * 1.6f);
            if (_footstepTimer >= 1.0f)
            {
                _footstepTimer = 0f;
                FootstepsAudio.PitchScale = (float)GD.RandRange(0.85f, 1.15f);
                FootstepsAudio.Play();
            }
        }
    }

    private void OnKillAreaBodyEntered(Node3D body)
{
    // Перевіряємо чи це гравець за типом або за групою
    if (body is Player player || body.IsInGroup("Player"))
    {
        Player targetPlayer = body as Player ?? GetTree().GetFirstNodeInGroup("Player") as Player;
        targetPlayer?.Die();
    }
}
}
