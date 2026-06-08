namespace Labyrinth;

using Chickensoft.Introspection;

[Meta, Id("map_movement_data")]
public partial record MapMovementData
{
    public required IMapMovementLogic StateMachine { get; init; }
}
