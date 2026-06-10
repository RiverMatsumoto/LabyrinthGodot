namespace Labyrinth;

using System;
using Godot;

public static class GridDirection
{
    public static Vector2I North { get; } = new(0, -1);
    public static Vector2I South { get; } = new(0, 1);
    public static Vector2I West { get; } = new(-1, 0);
    public static Vector2I East { get; } = new(1, 0);

    public static bool IsValid(Vector2I direction) =>
        TryFrom(direction, out _);

    public static Vector2I Opposite(Vector2I direction) =>
        From(new Vector2I(-direction.X, -direction.Y));

    public static Vector2I Left(Vector2I direction) =>
        From(new Vector2I(direction.Y, -direction.X));

    public static Vector2I Right(Vector2I direction) =>
        From(new Vector2I(-direction.Y, direction.X));

    public static Vector2I Turn(
        Vector2I direction,
        TurnDirection turnDirection
    ) =>
        turnDirection switch
        {
            TurnDirection.Left => Left(direction),
            TurnDirection.Right => Right(direction),
            _ => From(direction),
        };

    public static Vector2I From(Vector2I direction) =>
        TryFrom(direction, out var cardinalDirection)
            ? cardinalDirection
            : throw new ArgumentOutOfRangeException(
                nameof(direction),
                direction,
                "Grid direction must be a cardinal unit vector."
            );

    public static bool TryFrom(
        Vector2I direction,
        out Vector2I cardinalDirection
    )
    {
        if (direction == North)
        {
            cardinalDirection = North;
            return true;
        }

        if (direction == South)
        {
            cardinalDirection = South;
            return true;
        }

        if (direction == West)
        {
            cardinalDirection = West;
            return true;
        }

        if (direction == East)
        {
            cardinalDirection = East;
            return true;
        }

        cardinalDirection = default;
        return false;
    }
}
