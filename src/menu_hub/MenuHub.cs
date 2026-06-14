namespace Labyrinth;

using System.Linq;
using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;
using Godot;

public interface IMenuHub : IControl,
    IProvide<IMenuHubLogic>
{

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

    private LogicBlock.Binding? _menuBinding;
    private LogicBlock.Binding? _gameBinding;

    [Node]
    protected IVBoxContainer Buttons { get; set; } = default!;
    [Node] protected IButton ItemButton { get; set; } = default!;
    [Node] protected IButton SkillButton { get; set; } = default!;
    [Node] protected IButton StatusButton { get; set; } = default!;
    [Node] protected IButton EquipButton { get; set; } = default!;
    [Node] protected IButton CustomButton { get; set; } = default!;
    [Node] protected IButton PartyButton { get; set; } = default!;
    [Node] protected IButton QuestButton { get; set; } = default!;

    private IButton _lastFocused { get; set; } = default!;

    public void Setup()
    {
        MenuHubLogic = new MenuHubLogic();
    }

    public void OnResolved()
    {
        MenuHubLogic.Set(GameLogic);
        MenuHubLogic.Set(GameRepo);

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
        ItemButton.GrabFocus();
    }

    private void ShowSettings()
    {
        Visible = true;
        Buttons.Visible = false;
    }
}
