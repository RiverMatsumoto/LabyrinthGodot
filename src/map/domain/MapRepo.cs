namespace Labyrinth;

using System;
using System.Collections.Generic;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Sync.Primitives;
using Godot;

public interface IMapRepo : IDisposable
{
    IAutoChannel AutoChannel { get; }

    void LoadTerrainFromGridMap(IGridMap gridMap);
    bool IsInside(Vector2I gridPosition);
    bool CanEnter(Vector2I gridPosition);
    bool TryRegisterEntity(string id, Vector2I position);
    bool TryRegisterEntity(
        string id,
        Vector2I position,
        Vector2I facingDirection
    );
    bool TryRegisterEntity(MapEntityId id, Vector2I position);
    bool TryRegisterEntity(
        MapEntityId id,
        Vector2I position,
        Vector2I facingDirection
    );
    bool TryUnregisterEntity(MapEntityId id);
    bool TryGetEntityPosition(MapEntityId id, out Vector2I position);
    bool TryGetEntityPose(MapEntityId id, out MapEntityPose pose);
    bool ContainsEntity(MapEntityId id);
    bool IsOccupied(Vector2I position);
    bool CanEntityMove(MapEntityId id, Vector2I direction);
    bool TryMoveEntity(
        MapEntityId id,
        Vector2I direction,
        out GridMove move
    );
    bool TryMoveEntityPreservingFacing(
        MapEntityId id,
        Vector2I direction,
        out GridMove move
    );
    bool TryTurnEntity(
        MapEntityId id,
        TurnDirection turnDirection,
        out MapEntityPose pose
    );

    bool PlayerIsRegistered { get; }

    #region Events
    readonly record struct MapEntityWasRegistered(MapEntityId Id, Vector2I InitialPosition);
    readonly record struct MapEntityWasUnregistered(MapEntityId Id);
    #endregion

}

public class MapRepo : IMapRepo
{
    public const int TerrainWidth = 64;
    public const int TerrainHeight = 64;
    public static MapEntityId PlayerId { get; } = new("player");

    private readonly AutoChannel _autoChannel = new();
    public IAutoChannel AutoChannel => _autoChannel;

    private bool _disposedValue;
    public GridCellMap GridCellMap => _gridCellMap;

    public bool PlayerIsRegistered => _entityPoses.ContainsKey(PlayerId);

    private GridCellMap _gridCellMap;
    private readonly Dictionary<MapEntityId, MapEntityPose> _entityPoses;
    private readonly Dictionary<Vector2I, MapEntityId> _occupants;

    public MapRepo() : this(
        new GridCellMap(TerrainWidth, TerrainHeight)
    )
    {
    }

    public MapRepo(GridCellMap gridCellMap)
    {
        _gridCellMap = gridCellMap;
        _entityPoses = new Dictionary<MapEntityId, MapEntityPose>();
        _occupants = new Dictionary<Vector2I, MapEntityId>();
    }

    public void LoadTerrainFromGridMap(IGridMap gridMap)
    {
        ArgumentNullException.ThrowIfNull(gridMap);

        var meshLibrary = gridMap.MeshLibrary
            ?? throw new InvalidOperationException(
                "MapRepo: GridMap has no MeshLibrary."
            );

        var terrainMap = new GridCellMap(
            TerrainWidth,
            TerrainHeight,
            GridCellTerrain.Unmapped
        );

        foreach (var cell in gridMap.GetUsedCells())
        {
            var position = new Vector2I(cell.X, cell.Z);

            if (!terrainMap.IsInside(position))
            {
                throw new InvalidOperationException(
                    $"MapRepo: GridMap cell {cell} is outside "
                        + $"{TerrainWidth}x{TerrainHeight} terrain bounds."
                );
            }

            var item = gridMap.GetCellItem(cell);
            var itemName = meshLibrary.GetItemName(item);

            terrainMap[position] = itemName switch
            {
                "Floor" => GridCell.Floor(position),
                "Wall" => GridCell.Wall(position),
                _ => throw new InvalidOperationException(
                    $"MapRepo: Unsupported GridMap mesh item '{itemName}'."
                ),
            };
        }

        _gridCellMap = terrainMap;
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
        => TryRegisterEntity(id, position, GridDirection.North);

    public bool TryRegisterEntity(
        string id,
        Vector2I position,
        Vector2I facingDirection
    ) => TryRegisterEntity(new MapEntityId(id), position, facingDirection);

    public bool TryRegisterEntity(
        MapEntityId id,
        Vector2I position,
        Vector2I facingDirection
    )
    {
        if (id.IsEmpty || !GridDirection.IsValid(facingDirection))
        {
            return false;
        }

        if (_entityPoses.ContainsKey(id))
        {
            return false;
        }

        if (!CanEnter(position) || IsOccupied(position))
        {
            return false;
        }

        _entityPoses[id] = new MapEntityPose(position, facingDirection);
        _occupants[position] = id;
        _autoChannel.Send(new IMapRepo.MapEntityWasRegistered(id, position));
        return true;
    }

    public bool TryUnregisterEntity(MapEntityId id)
    {
        if (id == PlayerId)
        {
            return false;
        }

        if (!_entityPoses.Remove(id, out var pose))
        {
            return false;
        }

        _occupants.Remove(pose.Position);
        _autoChannel.Send(new IMapRepo.MapEntityWasUnregistered(id));
        return true;
    }

    public bool TryGetEntityPosition(MapEntityId id, out Vector2I position)
    {
        if (!TryGetEntityPose(id, out var pose))
        {
            position = default;
            return false;
        }

        position = pose.Position;
        return true;
    }

    public bool TryGetEntityPose(MapEntityId id, out MapEntityPose pose) =>
        _entityPoses.TryGetValue(id, out pose);

    public bool IsOccupied(Vector2I position) => _occupants.ContainsKey(position);

    public bool CanEntityMove(MapEntityId id, Vector2I direction) =>
        TryGetMove(id, direction, out _);

    public bool TryMoveEntity(
        MapEntityId id,
        Vector2I direction,
        out GridMove move
    )
        => TryMoveEntity(id, direction, updateFacing: true, out move);

    public bool TryMoveEntityPreservingFacing(
        MapEntityId id,
        Vector2I direction,
        out GridMove move
    )
        => TryMoveEntity(id, direction, updateFacing: false, out move);

    private bool TryMoveEntity(
        MapEntityId id,
        Vector2I direction,
        bool updateFacing,
        out GridMove move
    )
    {
        if (!TryGetMove(id, direction, out move))
        {
            return false;
        }

        _occupants.Remove(move.From);
        _occupants[move.To] = id;
        _entityPoses[id] = new MapEntityPose(
            move.To,
            updateFacing ? direction : _entityPoses[id].FacingDirection
        );
        return true;
    }

    public bool TryTurnEntity(
        MapEntityId id,
        TurnDirection turnDirection,
        out MapEntityPose pose
    )
    {
        if (!_entityPoses.TryGetValue(id, out pose))
        {
            return false;
        }

        pose = pose with
        {
            FacingDirection = GridDirection.Turn(
                pose.FacingDirection,
                turnDirection
            ),
        };
        _entityPoses[id] = pose;
        return true;
    }

    private bool TryGetMove(
        MapEntityId id,
        Vector2I direction,
        out GridMove move
    )
    {
        move = default;

        if (!GridDirection.IsValid(direction))
        {
            return false;
        }

        if (!_entityPoses.TryGetValue(id, out var pose))
        {
            return false;
        }

        var from = pose.Position;

        if (!_gridCellMap.TryGetCell(from, out var sourceCell))
        {
            return false;
        }

        if (!sourceCell.IsWalkable)
        {
            return false;
        }

        var to = sourceCell.Neighbor(direction);

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

        move = new GridMove(id, from, direction);
        return true;
    }

    public bool ContainsEntity(MapEntityId id) => _entityPoses.ContainsKey(id);

    #region Internals

    public void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _entityPoses.Clear();
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
