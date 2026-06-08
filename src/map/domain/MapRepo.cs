namespace Labyrinth;

using System;
using System.Collections.Generic;
using Chickensoft.Sync.Primitives;
using Godot;

public interface IMapRepo : IDisposable
{
    IAutoChannel AutoChannel { get; }

    bool IsInside(Vector2I gridPosition);
    bool CanEnter(Vector2I gridPosition);
    bool TryRegisterEntity(string id, Vector2I position);
    bool TryRegisterEntity(MapEntityId id, Vector2I position);
    bool TryUnregisterEntity(MapEntityId id);
    bool TryGetEntityPosition(MapEntityId id, out Vector2I position);
    bool ContainsEntity(MapEntityId id);
    bool IsOccupied(Vector2I position);
    bool CanEntityMove(MapEntityId id, Vector2I offset);
    bool TryMoveEntity(MapEntityId id, Vector2I offset, out GridMove move);

    #region Events
    readonly record struct MapEntityWasRegistered(MapEntityId Id, Vector2I InitialPosition);
    readonly record struct MapEntityWasUnregistered(MapEntityId Id);
    #endregion

}

public class MapRepo : IMapRepo
{
    private readonly AutoChannel _autoChannel = new();
    public IAutoChannel AutoChannel => _autoChannel;

    private bool _disposedValue;
    public GridCellMap GridCellMap => _gridCellMap;
    private readonly GridCellMap _gridCellMap;
    private readonly Dictionary<MapEntityId, Vector2I> _entityPositions;
    private readonly Dictionary<Vector2I, MapEntityId> _occupants;

    public MapRepo()
    {
        _gridCellMap = new GridCellMap(56, 56, GridCellTerrain.Floor);
        _entityPositions = new Dictionary<MapEntityId, Vector2I>();
        _occupants = new Dictionary<Vector2I, MapEntityId>();
    }

    public bool IsInside(Vector2I gridPosition) =>
        _gridCellMap.IsInside(gridPosition);

    public bool CanEnter(Vector2I gridPosition)
    {
        if (_gridCellMap.TryGetCell(gridPosition, out var cell))
        {
            return cell.IsWalkable;
        }

        return false;
    }

    public bool TryRegisterEntity(string id, Vector2I position)
        => TryRegisterEntity(new MapEntityId(id), position);

    public bool TryRegisterEntity(MapEntityId id, Vector2I position)
    {
        if (id.IsEmpty)
        {
            return false;
        }

        if (_entityPositions.ContainsKey(id))
        {
            return false;
        }

        if (!CanEnter(position) || IsOccupied(position))
        {
            return false;
        }

        _entityPositions[id] = position;
        _occupants[position] = id;
        _autoChannel.Send(new IMapRepo.MapEntityWasRegistered(id, position));
        return true;
    }

    public bool TryUnregisterEntity(MapEntityId id)
    {
        if (!_entityPositions.Remove(id, out var position))
        {
            return false;
        }

        _occupants.Remove(position);
        _autoChannel.Send(new IMapRepo.MapEntityWasUnregistered(id));
        return true;
    }

    public bool TryGetEntityPosition(MapEntityId id, out Vector2I position) =>
        _entityPositions.TryGetValue(id, out position);

    public bool IsOccupied(Vector2I position) => _occupants.ContainsKey(position);

    public bool CanEntityMove(MapEntityId id, Vector2I offset) =>
        TryGetMove(id, offset, out _);

    public bool TryMoveEntity(
        MapEntityId id,
        Vector2I offset,
        out GridMove move
    )
    {
        if (!TryGetMove(id, offset, out move))
        {
            return false;
        }

        _occupants.Remove(move.From);
        _occupants[move.To] = id;
        _entityPositions[id] = move.To;
        return true;
    }

    private bool TryGetMove(
        MapEntityId id,
        Vector2I offset,
        out GridMove move
    )
    {
        move = default;

        if (offset == Vector2I.Zero)
        {
            return false;
        }

        if (!_entityPositions.TryGetValue(id, out var from))
        {
            return false;
        }

        if (!_gridCellMap.TryGetCell(from, out var sourceCell))
        {
            return false;
        }

        if (!sourceCell.IsWalkable)
        {
            return false;
        }

        var to = sourceCell.Neighbor(offset);

        if (!_gridCellMap.TryGetCell(to, out var targetCell))
        {
            return false;
        }

        if (!targetCell.IsWalkable)
        {
            return false;
        }

        if (IsOccupied(to))
        {
            return false;
        }

        move = new GridMove(id, from, to, offset);
        return true;
    }

    public bool ContainsEntity(MapEntityId id) => _entityPositions.ContainsKey(id);

    #region Internals

    public void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _entityPositions.Clear();
                _occupants.Clear();
                _autoChannel.Dispose();
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion
}
