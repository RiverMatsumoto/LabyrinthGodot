namespace Labyrinth;

using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;
using Godot;

public interface IMenuHub : ICanvasLayer
{

}

[Meta(typeof(IAutoNode))]
public partial class MenuHub : CanvasLayer, IMenuHub
{
    public override void _Notification(int what) => this.Notify(what);

    [Dependency]
    public IGameLogic GameLogic => this.DependOn<IGameLogic>();

    public IMenuHubLogic MenuHubLogic { get; set; } = default!;
    private LogicBlock.Binding? _menuBinding;
    private LogicBlock.Binding? _gameBinding;

    [Node]
    protected IVBoxContainer Buttons { get; set; } = default!;

    public void Setup()
    {
        MenuHubLogic = new MenuHubLogic();
    }

    public void OnResolved()
    {
        MenuHubLogic.Set(GameLogic);

        _menuBinding = MenuHubLogic.Bind()
            .OnEnter<MenuHubLogicState.Disabled>(_ => ShowDisabled())
            .OnEnter<MenuHubLogicState.MenuHub>(_ => ShowMenuHub())
            .OnEnter<MenuHubLogicState.Settings>(_ => ShowSettings());

        _gameBinding = GameLogic.Bind()
            .OnEnter<GameLogicState.MainMenu>(_ =>
                MenuHubLogic.Input(new MenuHubLogicState.Input.Close()));

        MenuHubLogic.Start<MenuHubLogicState.Disabled>();
    }

    public override void _Input(InputEvent @event)
    {
        if (Input.IsActionJustPressed(GameInputs.Menu))
        {
            GD.Print("menu input");
            MenuHubLogic.Input(new MenuHubLogicState.Input.HandleMenuInput());
        }
    }

    public void OnExitTree()
    {
        _menuBinding?.Dispose();
        _gameBinding?.Dispose();
        MenuHubLogic.Dispose();
    }

    private void ShowDisabled()
    {
        Visible = false;
        Buttons.Visible = false;
    }

    private void ShowMenuHub()
    {
        Visible = true;
        Buttons.Visible = true;
    }

    private void ShowSettings()
    {
        Visible = true;
        Buttons.Visible = false;
    }
}
