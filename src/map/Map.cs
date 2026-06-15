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
    [Dependency]
    public IInstantiator Instantiator => this.DependOn<IInstantiator>();

    public IMapLogic MapLogic { get; set; } = default!;
    IMapLogic IProvide<IMapLogic>.Value() => MapLogic;
    private MapLogic.Binding? _mapBinding;
    private readonly Dictionary<MapEntityId, Node> _entityNodes = [];
    [Node] public IGridMap GridMap { get; set; } = default!;
    public IGridMap Value() => GridMap;
    [Node] public INode3D Entities { get; set; } = default!;

    public const string MapMovementScenePath =
        "res://src/map_movement/MapMovement.tscn";
    public const string EnemyMapEntityScenePath =
        "res://src/enemy_map_entity/EnemyMapEntity.tscn";

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

        MapRepo.LoadTerrain(GridMapTerrainCompiler.Compile(GridMap));

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
        var player = Instantiator.LoadAndInstantiate<MapMovement>(
            MapMovementScenePath
        );
        player.Initialize(id, pose, isPlayer: true);
        _entityNodes[id] = player;
        Entities.AddChild(player);
    }

    private void SpawnEnemy(MapEntityId id, MapEntityPose pose)
    {
        var enemy = Instantiator.LoadAndInstantiate<EnemyMapEntity>(
            EnemyMapEntityScenePath
        );
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
