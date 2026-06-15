namespace Labyrinth;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Chickensoft.AutoInject;
using Chickensoft.GoDotTest;
using Chickensoft.Introspection;
using Godot;
using Shouldly;

[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "GoDotTest Cleanup frees the provider after every test."
)]
public class MenuHubTest : TestClass
{
    private static readonly (
        string ButtonName,
        string ScreenName,
        Type StateType
    )[] Submenus = [
        ("ItemButton", "ItemMenu", typeof(MenuHubLogicState.ItemMenu)),
        ("SkillButton", "SkillMenu", typeof(MenuHubLogicState.SkillMenu)),
        ("StatusButton", "StatusMenu", typeof(MenuHubLogicState.StatusMenu)),
        ("EquipButton", "EquipMenu", typeof(MenuHubLogicState.EquipMenu)),
        ("CustomButton", "CustomMenu", typeof(MenuHubLogicState.CustomMenu)),
        ("PartyButton", "PartyMenu", typeof(MenuHubLogicState.PartyMenu)),
        ("QuestButton", "QuestMenu", typeof(MenuHubLogicState.QuestMenu)),
    ];

    private MenuHubTestProvider _provider = default!;
    private MenuHub _menuHub = default!;
    private Control _hubScreen = default!;

    public MenuHubTest(Node testScene) : base(testScene) { }

    [Setup]
    public async Task Setup()
    {
        _provider = new MenuHubTestProvider();
        _menuHub = GD.Load<PackedScene>(
            "res://src/menu_hub/MenuHub.tscn"
        ).Instantiate<MenuHub>();

        _provider.AddChild(_menuHub);
        TestScene.AddChild(_provider);
        await TestScene.ToSignal(
            TestScene.GetTree(),
            SceneTree.SignalName.ProcessFrame
        );

        _hubScreen = _menuHub.GetNode<Control>("HubScreen");
    }

    [Cleanup]
    public void Cleanup()
    {
        TestScene.RemoveChild(_provider);
        _provider.Free();
    }

    [Test]
    public void SwitchesEverySubmenuAndRestoresFocus()
    {
        _hubScreen.Visible.ShouldBeFalse();
        _hubScreen.ProcessMode.ShouldBe(Node.ProcessModeEnum.Disabled);
        foreach (var submenu in Submenus)
        {
            var screen = _menuHub.GetNode<Control>(submenu.ScreenName);
            screen.Visible.ShouldBeFalse();
            screen.ProcessMode.ShouldBe(Node.ProcessModeEnum.Disabled);
        }

        _menuHub.MenuHubLogic.Input(
            new MenuHubLogicState.Input.HandleMenuInput()
        );

        _hubScreen.Visible.ShouldBeTrue();
        _hubScreen.ProcessMode.ShouldBe(Node.ProcessModeEnum.Inherit);

        foreach (var submenu in Submenus)
        {
            var button = _hubScreen.GetNode<Button>(
                $"Buttons/{submenu.ButtonName}"
            );
            var screen = _menuHub.GetNode<Control>(submenu.ScreenName);
            var backButton = screen.GetNode<Button>(
                "CenterContainer/Content/BackButton"
            );

            button.GrabFocus();
            button.EmitSignal(BaseButton.SignalName.Pressed);

            _menuHub.MenuHubLogic.State!
                .GetType()
                .ShouldBe(submenu.StateType);
            _hubScreen.Visible.ShouldBeFalse();
            screen.Visible.ShouldBeTrue();
            screen.ProcessMode.ShouldBe(Node.ProcessModeEnum.Inherit);
            backButton.HasFocus().ShouldBeTrue();

            foreach (var otherSubmenu in Submenus)
            {
                _menuHub.GetNode<Control>(otherSubmenu.ScreenName)
                    .Visible
                    .ShouldBe(
                        otherSubmenu.ScreenName == submenu.ScreenName
                    );
            }

            backButton.EmitSignal(BaseButton.SignalName.Pressed);

            _menuHub.MenuHubLogic.State
                .ShouldBeOfType<MenuHubLogicState.MenuHub>();
            _hubScreen.Visible.ShouldBeTrue();
            screen.Visible.ShouldBeFalse();
            button.HasFocus().ShouldBeTrue();
        }

        _menuHub.MenuHubLogic.Input(new MenuHubLogicState.Input.Close());

        _hubScreen.Visible.ShouldBeFalse();
        foreach (var submenu in Submenus)
        {
            _menuHub.GetNode<Control>(submenu.ScreenName)
                .Visible
                .ShouldBeFalse();
        }
        _menuHub.GetViewport().GuiGetFocusOwner().ShouldBeNull();
    }
}

[Meta(typeof(IAutoNode))]
public partial class MenuHubTestProvider :
    Node,
    IProvide<IGameLogic>,
    IProvide<IGameRepo>
{
    public override void _Notification(int what) => this.Notify(what);

    public GameLogic GameLogic { get; private set; } = default!;
    public GameRepo GameRepo { get; private set; } = default!;

    public void Setup()
    {
        GameRepo = new GameRepo();
        GameLogic = new GameLogic();
        GameLogic.Set<IGameRepo>(GameRepo);
        GameLogic.Start<GameLogicState.Town>();
    }

    public void OnResolved() => this.Provide();

    public void OnExitTree()
    {
        GameLogic.Dispose();
        GameRepo.Dispose();
    }

    IGameLogic IProvide<IGameLogic>.Value() => GameLogic;
    IGameRepo IProvide<IGameRepo>.Value() => GameRepo;
}
