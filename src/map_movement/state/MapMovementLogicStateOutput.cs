namespace Labyrinth;

using Godot;

public partial record MapMovementLogicState
{
    public static class Output
    {
        public readonly record struct MoveStarted(GridMove Move);
        public readonly record struct MoveBlocked(Vector2I Offset);
        public readonly record struct MoveFinished;
    }
}
