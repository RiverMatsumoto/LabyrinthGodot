namespace Labyrinth;

using Godot;

public readonly record struct MapEntityPose(
    Vector2I Position,
    Vector2I FacingDirection
);
