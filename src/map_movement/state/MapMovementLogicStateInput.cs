namespace Labyrinth;

using Godot;

public partial record MapMovementLogicState
{
    public static class Input
    {
        public readonly record struct MoveAccepted(GridMove Move);
        public readonly record struct MoveBlocked(Vector2I Offset);
        public readonly record struct Arrived;
    }
}
