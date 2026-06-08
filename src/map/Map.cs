namespace Labyrinth;

using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
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

    public IMapLogic MapLogic { get; private set; } = default!;
    [Node] public IGridMap GridMap { get; private set; } = default!;
    public IGridMap Value() => GridMap;
    [Node] public INode3D Entities { get; private set; } = default!;

    public static PackedScene MapMovement =>
        GD.Load<PackedScene>("res://src/map_movement/MapMovement.tscn");

    public void Setup()
    {
        MapLogic = new MapLogic();
    }

    public void OnResolved()
    {
        MapRepo.AutoChannel.Bind()
            .On((in IMapRepo.MapEntityWasRegistered message)
            => OnMapEntityRegistered(message.Id, message.InitialPosition));

        // GD.Print(GridMap);
        // MapLogic.Start();
        this.Provide();
    }

    public void OnReady()
    {
    }

    public override void _Process(double delta)
    {
        if (Input.IsActionJustPressed(GameInputs.UiAccept))
        {
            MapRepo.TryRegisterEntity("player", new Vector2I(1, 1));
        }

        if (Input.IsActionJustPressed(GameInputs.UiCancel))
        {
            MapRepo.TryUnregisterEntity(new MapEntityId("player"));
        }
    }

    public void OnMapEntityRegistered(MapEntityId id, Vector2I initialPosition)
    {
        var mapMovement = MapMovement.Instantiate<MapMovement>();
        mapMovement.Initialize(id, initialPosition);
        Entities.AddChild(mapMovement);
    }
}
