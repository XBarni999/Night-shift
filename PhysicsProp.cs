using Godot;

public partial class PhysicsProp : RigidBody3D, IInteractable, IHoldable
{
    [Export] public string PropName = "Коробка";

    public RigidBody3D Body => this;
    public string PromptText => $"[E] Підняти: {PropName}";

    public override void _Ready()
    {
        ContinuousCd = true;
    }

    public void Interact()
    {
    }

    public void Pickup()
    {
        // Легке гасіння, щоб не дрижало в руках, але не блокувало обертання
        LinearDamp = 4.0f;
        AngularDamp = 3.0f;
    }

    public void Drop()
    {
        LinearDamp = 0.0f;
        AngularDamp = 0.0f;
    }
}
