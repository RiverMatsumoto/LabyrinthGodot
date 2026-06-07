namespace Labyrinth;

using System;
using Godot;

public interface IMapRepo : IDisposable
{
    bool CanPlayerEnter(Vector2I gridPosition);
    bool IsInside(Vector2I gridPosition);
}

public class MapRepo : IMapRepo
{
    private bool _disposedValue;
    public GridCellMap GridCellMap => _gridCellMap;
    private readonly GridCellMap _gridCellMap;

    public MapRepo()
    {
        _gridCellMap = new GridCellMap(50, 50, GridCellTerrain.Floor);
    }

    public bool CanPlayerEnter(Vector2I gridPosition)
    {
        if (_gridCellMap.TryGetCell(gridPosition, out var cell))
            return cell.IsWalkable;
        return false;
    }

    public void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                // dispose of managed objects
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public bool IsInside(Vector2I gridPosition) => throw new NotImplementedException();
}
