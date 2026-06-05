namespace Labyrinth;

using Chickensoft.AutoInject;
using Chickensoft.Introspection;
using Godot;

[Meta(typeof(IAutoNode))]
public partial class Game : Node3D
{
    public IGameRepo GameRepo { get; set; } = default!;
    public IGameLogic GameLogic { get; set; } = default!;

    public void Setup()
    {
        GameRepo = new GameRepo();
        GameLogic = new GameLogic();
    }
}
