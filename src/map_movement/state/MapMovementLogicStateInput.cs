namespace Labyrinth;

using Godot;

public partial record MapMovementLogicState
{
    public static class Input
    {
        public readonly record struct Disable;
        public readonly record struct Enable;
        public readonly record struct MoveRequested(Vector2I Direction);
        public readonly record struct RelativeMoveRequested(
            RelativeMoveDirection Direction
        );
        public readonly record struct MoveFinished;
        public readonly record struct TurnRequested(TurnDirection Direction);
        public readonly record struct TurnFinished;
        public readonly record struct CooldownFinished;
    }
}
