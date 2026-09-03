using Godot;

public partial class PropSpawner : Marker3D
{
    [Export] public PackedScene PropScene;
    [Export] public int MinBoxes = 1;
    [Export] public int MaxBoxes = 4;
    [Export] public float SpreadRadius = 0.8f;
    [Export] public bool StackOnTop = true;

    public override void _Ready()
    {
        SpawnProps();
    }

    public void SpawnProps()
    {
        if (PropScene == null) return;

        int count = GD.RandRange(MinBoxes, MaxBoxes);

        for (int i = 0; i < count; i++)
        {
            var prop = PropScene.Instantiate<RigidBody3D>();

            // Додаємо коробку як дочірній об'єкт спавнера
            AddChild(prop);

            Vector3 offset = Vector3.Zero;

            if (StackOnTop && i > 0)
            {
                offset = new Vector3(
                    (float)GD.RandRange(-0.15f, 0.15f),
                    i * 0.65f,
                    (float)GD.RandRange(-0.15f, 0.15f)
                );
            }
            else
            {
                offset = new Vector3(
                    (float)GD.RandRange(-SpreadRadius, SpreadRadius),
                    0.1f,
                    (float)GD.RandRange(-SpreadRadius, SpreadRadius)
                );
            }

            // Працюємо через локальну Position відносно спавнера
            prop.Position = offset;
            prop.Rotation = new Vector3(0, (float)GD.RandRange(0, Mathf.Tau), 0);
        }
    }
}
