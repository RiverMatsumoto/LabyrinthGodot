namespace Labyrinth;

using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;
using Godot;

public interface IMenuHubScreen : IControl;

[Meta(typeof(IAutoNode))]
public partial class MenuHubScreen :
    LogicControl<MenuHubLogicState.MenuHub>,
    IMenuHubScreen
{
    [Dependency]
    private IMenuHubLogic MenuHubLogic => this.DependOn<IMenuHubLogic>();

    [Node] protected Button ItemButton { get; set; } = default!;
    [Node] protected Button SkillButton { get; set; } = default!;
    [Node] protected Button StatusButton { get; set; } = default!;
    [Node] protected Button EquipButton { get; set; } = default!;
    [Node] protected Button CustomButton { get; set; } = default!;
    [Node] protected Button PartyButton { get; set; } = default!;
    [Node] protected Button QuestButton { get; set; } = default!;

    protected override ILogicBlock LogicBlock => MenuHubLogic;
    protected override Control DefaultFocusControl => ItemButton;

    public override void OnResolved()
    {
        ItemButton.Pressed += OpenItemMenu;
        SkillButton.Pressed += OpenSkillMenu;
        StatusButton.Pressed += OpenStatusMenu;
        EquipButton.Pressed += OpenEquipMenu;
        CustomButton.Pressed += OpenCustomMenu;
        PartyButton.Pressed += OpenPartyMenu;
        QuestButton.Pressed += OpenQuestMenu;
        base.OnResolved();
    }

    public override void OnExitTree()
    {
        ItemButton.Pressed -= OpenItemMenu;
        SkillButton.Pressed -= OpenSkillMenu;
        StatusButton.Pressed -= OpenStatusMenu;
        EquipButton.Pressed -= OpenEquipMenu;
        CustomButton.Pressed -= OpenCustomMenu;
        PartyButton.Pressed -= OpenPartyMenu;
        QuestButton.Pressed -= OpenQuestMenu;
        base.OnExitTree();
    }

    private void OpenItemMenu() =>
        MenuHubLogic.Input(new MenuHubLogicState.Input.OpenItemMenu());

    private void OpenSkillMenu() =>
        MenuHubLogic.Input(new MenuHubLogicState.Input.OpenSkillMenu());

    private void OpenStatusMenu() =>
        MenuHubLogic.Input(new MenuHubLogicState.Input.OpenStatusMenu());

    private void OpenEquipMenu() =>
        MenuHubLogic.Input(new MenuHubLogicState.Input.OpenEquipMenu());

    private void OpenCustomMenu() =>
        MenuHubLogic.Input(new MenuHubLogicState.Input.OpenCustomMenu());

    private void OpenPartyMenu() =>
        MenuHubLogic.Input(new MenuHubLogicState.Input.OpenPartyMenu());

    private void OpenQuestMenu() =>
        MenuHubLogic.Input(new MenuHubLogicState.Input.OpenQuestMenu());
}
