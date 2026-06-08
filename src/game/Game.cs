namespace Labyrinth;

using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;

public interface IGame : INode, IProvide<IGameRepo>, IProvide<IMapRepo>
{
    Node AreaRoot { get; }
    Node UiRoot { get; }
    void LoadGame(int id);
    event Game.SaveFileLoadedEventHandler? SaveFileLoaded;
}

[Meta(typeof(IAutoNode))]
public partial class Game : Node, IGame
{
    public override void _Notification(int what) => this.Notify(what);

    public IGameRepo GameRepo { get; set; } = default!;
    public IMapRepo MapRepo { get; set; } = default!;
    public IGameLogic GameLogic { get; set; } = default!;
    public Node AreaRoot { get; private set; } = default!;
    public Node UiRoot { get; private set; } = default!;

    #region Save
    [Signal]
    public delegate void SaveFileLoadedEventHandler(int id);
    #endregion


    public void Setup()
    {
        GameRepo = new GameRepo();
        MapRepo = new MapRepo();
        GameLogic = new GameLogic();
    }

    public void OnReady()
    {
        AreaRoot = GetNodeOrNull<Node>("AreaRoot") ?? this;
        UiRoot = GetNodeOrNull<Node>("UiRoot") ?? this;
    }

    public void OnResolved()
    {
        GameLogic.Start<GameLogicState.MainMenu>();
        this.Provide();
    }

    public void OnExitTree()
    {
        GameRepo.Dispose();
        MapRepo.Dispose();
    }

    public IGameRepo Value() => GameRepo;
    IMapRepo IProvide<IMapRepo>.Value() => MapRepo;

    public void LoadGame(int id) => GD.Print($"Load game {id}");
}
