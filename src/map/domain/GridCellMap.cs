namespace Labyrinth;

using System;

public sealed class GridCellMap
{
    private readonly GridCell[] _cells;

    public GridCellMap(
        int width,
        int height,
        GridCellTerrain defaultTerrain = GridCellTerrain.Unmapped
    )
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                "Grid width must be greater than zero."
            );
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(height),
                "Grid height must be greater than zero."
            );
        }

        Width = width;
        Height = height;
        _cells = new GridCell[width * height];

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var position = new GridPosition(x, y);
                _cells[GetIndexUnchecked(position)] =
                    new GridCell(position, defaultTerrain);
            }
        }
    }

    public int Width { get; }
    public int Height { get; }
    public int Count => _cells.Length;

    public GridCell this[GridPosition position]
    {
        get => _cells[GetIndex(position)];
        set => _cells[GetIndex(position)] = value;
    }

    public GridCell this[int x, int y]
    {
        get => this[new GridPosition(x, y)];
        set => this[new GridPosition(x, y)] = value;
    }

    public ReadOnlySpan<GridCell> Cells => _cells;

    public bool IsInside(GridPosition position) =>
        position.X >= 0
            && position.Y >= 0
            && position.X < Width
            && position.Y < Height;

    public bool TryGetCell(GridPosition position, out GridCell cell)
    {
        if (!IsInside(position))
        {
            cell = default;
            return false;
        }

        cell = this[position];
        return true;
    }

    public int GetIndex(GridPosition position)
    {
        if (!IsInside(position))
        {
            throw new ArgumentOutOfRangeException(
                nameof(position),
                $"Grid position {position} is outside {Width}x{Height}."
            );
        }

        return GetIndexUnchecked(position);
    }

    private int GetIndexUnchecked(GridPosition position) =>
        position.X + (position.Y * Width);
}
