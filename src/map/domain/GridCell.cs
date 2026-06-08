namespace Labyrinth;

using System;
using Godot;

public readonly record struct GridCell
{
    public GridCell(
        Vector2I position,
        GridCellTerrain terrain = GridCellTerrain.Floor,
        GridCellFlags flags = GridCellFlags.None
    )
    {
        Position = position;
        Terrain = terrain;
        Flags = flags;
    }

    public Vector2I Position { get; init; }
    public GridCellTerrain Terrain { get; init; }
    public GridCellFlags Flags { get; init; }

    public bool IsMapped => Terrain != GridCellTerrain.Unmapped;
    public bool IsWalkable =>
        Terrain.IsWalkable() && !Flags.Has(GridCellFlags.BlocksMovement);
    public bool BlocksSight =>
        Terrain.BlocksSight() || Flags.Has(GridCellFlags.BlocksSight);

    public static GridCell Floor(Vector2I position) => new(position);

    public static GridCell Wall(Vector2I position) =>
        new(position, GridCellTerrain.Wall);

    public Vector2I Neighbor(Vector2I offset) => Position + offset;

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

public static class GridCellExtensions
{
    public static bool Has(this GridCellFlags flags, GridCellFlags flag) =>
        (flags & flag) == flag;

    public static bool IsWalkable(this GridCellTerrain terrain) =>
        terrain is GridCellTerrain.Floor;

    public static bool BlocksSight(this GridCellTerrain terrain) =>
        terrain == GridCellTerrain.Wall;
}
