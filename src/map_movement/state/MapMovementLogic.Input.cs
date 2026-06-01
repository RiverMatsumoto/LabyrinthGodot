namespace Labyrinth;

using System.Numerics;

public partial class MapMovementLogic
{
    public static class Input
    {
        public readonly record struct Moved(Vector2 Direction);
    }
}
