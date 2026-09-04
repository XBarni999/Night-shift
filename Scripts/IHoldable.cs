using Godot;

public interface IHoldable
{
    RigidBody3D Body { get; }
    void Pickup();
    void Drop();
}
