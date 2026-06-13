namespace Labyrinth;

using Godot;

public partial class MapMovementLogic
{
    public sealed class Data
    {
        public Vector3 Position { get; set; }
        public MapEntityId EntityId { get; set; }
        public double MoveDuration { get; set; } =
            MapMovementSettings.Default.MoveDuration;
        public double MoveCooldown { get; set; } =
            MapMovementSettings.Default.MoveCooldown;
        public bool DisableRequested { get; set; }
    }
}

public readonly record struct MapMovementSettings(
    double MoveDuration,
    double MoveCooldown
)
{
    public static MapMovementSettings Default => new(
        MoveDuration: 0.2,
        MoveCooldown: 0.04
    );
}
