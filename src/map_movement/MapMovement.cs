namespace Labyrinth;

using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;

public interface IMapMovement : INode3D
{
    IMapMovementLogic MapMovementLogic { get; }
}

[Meta(typeof(IAutoNode))]
public partial class MapMovement : Node3D, IMapMovement
{
    public IMapMovementLogic MapMovementLogic { get; set; } = default!;

    public void Setup()
    {
        // Create logicblocks instances
        MapMovementLogic = new MapMovementLogic();

    }

    public void OnReady()
    {

    }

    public void OnResolved()
    {

    }

    public void OnExitTree()
    {

    }
}
