namespace Labyrinth;

using System.Collections.Generic;
using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;

public interface IMap : INode3D, IProvide<IGridMap>, IProvide<IMapLogic>
{
    IMapLogic MapLogic { get; }
    IGridMap GridMap { get; }
    INode3D Entities { get; }
}

[Meta(typeof(IAutoNode))]
public partial class Map : Node3D, IMap
{
    public override void _Notification(int what) => this.Notify(what);

    [Dependency] public IMapRepo MapRepo => this.DependOn<IMapRepo>();

    public IMapLogic MapLogic { get; private set; } = default!;
    IMapLogic IProvide<IMapLogic>.Value() => MapLogic;
    private MapLogic.Binding? _mapBinding;
    private readonly Dictionary<MapEntityId, Node> _entityNodes = [];
    [Node] public IGridMap GridMap { get; private set; } = default!;
    public IGridMap Value() => GridMap;
    [Node] public INode3D Entities { get; private set; } = default!;

    public static PackedScene MapMovementScene =>
        GD.Load<PackedScene>(
            "res://src/map_movement/MapMovement.tscn"
        );

    public static PackedScene EnemyMapEntityScene =>
        GD.Load<PackedScene>("res://src/enemy_map_entity/EnemyMapEntity.tscn");

    public void Setup()
    {
        MapLogic = new MapLogic();
    }

    public void OnResolved()
    {
        MapLogic.Set(MapRepo);

        _mapBinding = MapLogic.Bind()
            .OnOutput((in MapLogicState.Output.SpawnPlayer output) =>
                SpawnPlayer(output.Id, output.Pose))
            .OnOutput((in MapLogicState.Output.SpawnEnemy output) =>
                SpawnEnemy(output.Id, output.Pose))
            .OnOutput((in MapLogicState.Output.DespawnEntity output) =>
                DespawnEntity(output.Id));

        // Intentional boundary exception: maps are authored with Godot GridMap.
        MapRepo.LoadTerrainFromGridMap(GridMap);

        MapLogic.Start<MapLogicState.Idle>();

        this.Provide();

        MapLogic.TryRegisterEntity(
            global::Labyrinth.MapRepo.PlayerId,
            new MapEntityPose(new Vector2I(1, 1), GridDirection.North)
        );
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Input.IsActionJustPressed(GameInputs.UiAccept))
        {
            _ = MapLogic.TryRegisterEntity(
                global::Labyrinth.MapRepo.PlayerId,
                new MapEntityPose(new Vector2I(1, 1), GridDirection.North)
            );
        }
    }

    private void SpawnPlayer(MapEntityId id, MapEntityPose pose)
    {
        var player = MapMovementScene.Instantiate<MapMovement>();
        player.Initialize(id, pose, isPlayer: true);
        _entityNodes[id] = player;
        Entities.AddChild(player);
    }

    private void SpawnEnemy(MapEntityId id, MapEntityPose pose)
    {
        var enemy = EnemyMapEntityScene.Instantiate<EnemyMapEntity>();
        enemy.Initialize(id, pose);
        _entityNodes[id] = enemy;
        Entities.AddChild(enemy);
    }

    private void DespawnEntity(MapEntityId id)
    {
        if (!_entityNodes.Remove(id, out var entity))
        {
            return;
        }

        entity.QueueFree();
    }

    public void OnExitTree()
    {
        _mapBinding?.Dispose();
        MapLogic.Dispose();
    }
}
