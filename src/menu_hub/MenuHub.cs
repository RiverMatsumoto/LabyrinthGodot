namespace Labyrinth;

using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;
using Godot;

public interface IMenuHub : IControl, IProvide<IMenuHubLogic>
{
    IMenuHubLogic MenuHubLogic { get; }
}

[Meta(typeof(IAutoNode))]
public partial class MenuHub : Control, IMenuHub
{
    public override void _Notification(int what) => this.Notify(what);

    [Dependency]
    public IGameLogic GameLogic => this.DependOn<IGameLogic>();
    [Dependency]
    public IGameRepo GameRepo => this.DependOn<IGameRepo>();

    public IMenuHubLogic MenuHubLogic { get; set; } = default!;
    public IMenuHubLogic Value() => MenuHubLogic;

    private LogicBlock.Binding? _gameBinding;

    public void Setup()
    {
        MenuHubLogic = new MenuHubLogic();
    }

    public void OnResolved()
    {
        MenuHubLogic.Set(GameLogic);
        MenuHubLogic.Set(GameRepo);

        _gameBinding = GameLogic.Bind()
            .OnEnter<GameLogicState.MainMenu>(_ =>
                MenuHubLogic.Input(new MenuHubLogicState.Input.Close()));

        this.Provide();
        MenuHubLogic.Start<MenuHubLogicState.Disabled>();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(GameInputs.Menu))
        {
            if (MenuHubLogic.State is MenuHubLogicState.Disabled)
            {
                MenuHubLogic.Input(
                    new MenuHubLogicState.Input.HandleMenuInput()
                );
            }
            else
            {
                MenuHubLogic.Input(new MenuHubLogicState.Input.Close());
            }

            GetViewport().SetInputAsHandled();
            return;
        }

        if (!@event.IsActionPressed(GameInputs.UiCancel))
        {
            return;
        }

        if (MenuHubLogic.State is MenuHubLogicState.MenuHub)
        {
            MenuHubLogic.Input(new MenuHubLogicState.Input.Close());
        }
        else if (MenuHubLogic.State is not MenuHubLogicState.Disabled)
        {
            MenuHubLogic.Input(new MenuHubLogicState.Input.Back());
        }
        else
        {
            return;
        }

        GetViewport().SetInputAsHandled();
    }

    public void OnExitTree()
    {
        _gameBinding?.Dispose();
        MenuHubLogic.Dispose();
    }
}
