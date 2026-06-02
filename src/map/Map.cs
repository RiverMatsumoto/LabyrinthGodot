namespace Labyrinth;

using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;

public interface IMap : INode3D, IProvide<IMapRepo>
{
    IMapLogic MapLogic { get; }
}

[Meta(typeof(IAutoNode))]
public partial class Map : Node3D, IMap
{
    [Dependency] public IMapRepo MapRepo => this.DependOn<IMapRepo>();

    public IMapLogic MapLogic => throw new System.NotImplementedException();

    public IMapRepo Value() => throw new System.NotImplementedException();
}

