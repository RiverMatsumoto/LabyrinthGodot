namespace Labyrinth;

using Chickensoft.Introspection;
using Chickensoft.Serialization;

[Meta, Id("map_movement_data")]
public partial record MapMovementData
{
    [Save("move_duration")]
    public required double MoveDuration { get; init; }
    [Save("move_cooldown")]
    public required double MoveCooldown { get; init; }
}
