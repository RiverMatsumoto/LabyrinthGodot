namespace Labyrinth;

using Godot;

public partial record MapMovementLogicState
{
    public static class Output
    {
        public readonly record struct MoveStarted(
            GridMove Move,
            double Duration
        );
        public readonly record struct MoveBlocked(Vector2I Direction);
        public readonly record struct MoveFinished;
        public readonly record struct TurnStarted(
            Vector2I FacingDirection,
            double Duration
        );
        public readonly record struct TurnFinished;
        public readonly record struct CooldownStarted(double Duration);
    }
}
