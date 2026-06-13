namespace Labyrinth;

using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Chickensoft.Sync.Primitives;
using Godot;

public interface IMap : INode3D, IProvide<IGridMap>
{
    IMapLogic MapLogic { get; }
    IGridMap GridMap { get; }
    INode3D Entities { get; }
}

[Meta(typeof(IAutoNode))]
public partial class Map : Node3D, IMap
{
    public override void _Notification(int what) => this.Notify(what);

    [Dependency] public IGameRepo GameRepo => this.DependOn<IGameRepo>();
    [Dependency] public IMapRepo MapRepo => this.DependOn<IMapRepo>();
    private AutoChannel.Binding _mapBinding { get; set; } = default!;

    public IMapLogic MapLogic { get; private set; } = default!;
    [Node] public IGridMap GridMap { get; private set; } = default!;
    public IGridMap Value() => GridMap;
    [Node] public INode3D Entities { get; private set; } = default!;

    private bool _isPlayerRegistered =>
        MapRepo.ContainsEntity(MapRepo.PlayerId);

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
        MapRepo.LoadTerrainFromGridMap(GridMap);
        // later other data to determine floor start and floorend

        _mapBinding = MapRepo.AutoChannel.Bind()
            .On((in IMapRepo.MapEntityWasRegistered message)
            => OnMapEntityRegistered(message.Id, message.InitialPosition));
        MapRepo.TryRegisterEntity(MapRepo.PlayerId, new Vector2I(1, 1));

        // GD.Print(GridMap);
        // MapLogic.Start();
        this.Provide();

        MapLogic.Start<MapLogicState.Idle>();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Input.IsActionJustPressed(GameInputs.UiAccept))
        {
            var _ = MapRepo.TryRegisterEntity("player", new Vector2I(1, 1));
        }

    }

    public void OnMapEntityRegistered(MapEntityId id, Vector2I initialPosition)
    {
        if (id == MapRepo.PlayerId)
        {
            var player =
                MapMovementScene.Instantiate<MapMovement>();
            player.Initialize(id, initialPosition);
            Entities.AddChild(player);
            return;
        }

        var enemy = EnemyMapEntityScene.Instantiate<EnemyMapEntity>();
        enemy.Initialize(id, initialPosition);
        Entities.AddChild(enemy);
    }

    public void OnExitTree()
    {
        _mapBinding.Dispose();
        MapLogic.Dispose();
    }
}
