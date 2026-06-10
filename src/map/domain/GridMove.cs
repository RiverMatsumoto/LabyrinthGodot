namespace Labyrinth;

using Godot;

public readonly record struct GridMove(
    MapEntityId EntityId,
    Vector2I From,
    Vector2I Direction
)
{
    public Vector2I Offset => Direction;
    public Vector2I To => From + Direction;
}
