namespace Labyrinth;

using System;
using Godot;

public readonly record struct GridCell
{
    public GridCell(
        Vector2I position,
        GridCellTerrain terrain = GridCellTerrain.Floor,
        GridCellSides walls = GridCellSides.None,
        GridCellFlags flags = GridCellFlags.None
    )
    {
        Position = position;
        Terrain = terrain;
        Walls = walls;
        Flags = flags;
    }

    public Vector2I Position { get; init; }
    public GridCellTerrain Terrain { get; init; }
    public GridCellSides Walls { get; init; }
    public GridCellFlags Flags { get; init; }

    public bool IsMapped => Terrain != GridCellTerrain.Unmapped;
    public bool IsWalkable =>
        Terrain.IsWalkable() && !Flags.Has(GridCellFlags.BlocksMovement);
    public bool BlocksSight =>
        Terrain.BlocksSight() || Flags.Has(GridCellFlags.BlocksSight);

    public static GridCell Floor(Vector2I position) => new(position);

    public static GridCell Wall(Vector2I position) =>
        new(position, GridCellTerrain.Wall);

    public bool HasWall(GridDirection side) => Walls.Has(side);

    public bool CanExit(GridDirection direction) =>
        IsWalkable && !HasWall(direction);

    public bool CanEnterFrom(GridDirection side) =>
        IsWalkable && !HasWall(side);

    public Vector2I Neighbor(GridDirection direction) =>
        Position + direction.ToGridOffset();

    public GridCell WithWall(GridDirection side) =>
        this with { Walls = Walls | side.ToCellSide() };

    public GridCell WithoutWall(GridDirection side) =>
        this with { Walls = Walls & ~side.ToCellSide() };

    public GridCell WithFlags(GridCellFlags flags) =>
        this with { Flags = Flags | flags };

    public GridCell WithoutFlags(GridCellFlags flags) =>
        this with { Flags = Flags & ~flags };
}

public enum GridCellTerrain
{
    Unmapped,
    Floor,
    Wall,
    Pit,
    Water,
    StairsUp,
    StairsDown,
}

[Flags]
public enum GridCellFlags
{
    None = 0,
    BlocksMovement = 1 << 0,
    BlocksSight = 1 << 1,
    Encounter = 1 << 2,
    Event = 1 << 3,
    Hidden = 1 << 4,
    SavePoint = 1 << 5,
}

[Flags]
public enum GridCellSides
{
    None = 0,
    North = 1 << 0,
    East = 1 << 1,
    South = 1 << 2,
    West = 1 << 3,
    All = North | East | South | West,
}

public enum GridDirection
{
    North,
    East,
    South,
    West,
}

public static class GridCellExtensions
{
    public static bool Has(this GridCellFlags flags, GridCellFlags flag) =>
        (flags & flag) == flag;

    public static bool Has(this GridCellSides sides, GridDirection side) =>
        (sides & side.ToCellSide()) != GridCellSides.None;

    public static bool IsWalkable(this GridCellTerrain terrain) =>
        terrain is GridCellTerrain.Floor
            or GridCellTerrain.StairsUp
            or GridCellTerrain.StairsDown;

    public static bool BlocksSight(this GridCellTerrain terrain) =>
        terrain == GridCellTerrain.Wall;

    public static GridCellSides ToCellSide(this GridDirection direction) =>
        direction switch
        {
            GridDirection.North => GridCellSides.North,
            GridDirection.East => GridCellSides.East,
            GridDirection.South => GridCellSides.South,
            GridDirection.West => GridCellSides.West,
            _ => GridCellSides.None,
        };

    public static Vector2I ToGridOffset(this GridDirection direction) =>
        direction switch
        {
            GridDirection.North => new Vector2I(0, -1),
            GridDirection.East => new Vector2I(1, 0),
            GridDirection.South => new Vector2I(0, 1),
            GridDirection.West => new Vector2I(-1, 0),
            _ => new Vector2I(0, 0),
        };

    public static GridDirection Opposite(this GridDirection direction) =>
        direction switch
        {
            GridDirection.North => GridDirection.South,
            GridDirection.East => GridDirection.West,
            GridDirection.South => GridDirection.North,
            GridDirection.West => GridDirection.East,
            _ => direction,
        };
}
