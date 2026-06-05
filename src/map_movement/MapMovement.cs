namespace Labyrinth;

using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;

// Interfaces for visuals specific
public interface IMapMovement : INode3D
{
}

[Meta(typeof(IAutoNode))]
public partial class MapMovement : Node3D, IMapMovement
{
    public IMapMovementLogic MapMovementLogic { get; set; } = default!;

    [Dependency] public IGameRepo GameRepo => this.DependOn<IGameRepo>();
    [Dependency] public IMapRepo MapRepo => this.DependOn<IMapRepo>();

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
