namespace Labyrinth;

using System;

public interface IMapRepo : IDisposable
{

    bool CanEnter(GridPosition gridPosition);
    bool IsInside(GridPosition gridPosition);
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

    public bool CanEnter(GridPosition gridPosition) => throw new NotImplementedException();

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

    public bool IsInside(GridPosition gridPosition) => throw new NotImplementedException();
}
