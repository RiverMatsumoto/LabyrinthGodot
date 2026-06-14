namespace Labyrinth;

using System;
using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

public class MenuHubLogicTest : TestClass
{
    public MenuHubLogicTest(Node testScene) : base(testScene) { }

    [Test]
    public void NavigatesBetweenHubSubmenusAndDisabled()
    {
        using var gameRepo = new GameRepo();
        using var gameLogic = new GameLogic();
        using var menuLogic = new MenuHubLogic();

        gameLogic.Set<IGameRepo>(gameRepo);
        gameLogic.Start<GameLogicState.Town>();

        menuLogic.Set<IGameLogic>(gameLogic);
        menuLogic.Set<IGameRepo>(gameRepo);
        menuLogic.Start<MenuHubLogicState.Disabled>();

        menuLogic.Input(new MenuHubLogicState.Input.HandleMenuInput());
        menuLogic.State.ShouldBeOfType<MenuHubLogicState.MenuHub>();
        gameRepo.IsInMenu.Value.ShouldBeTrue();

        AssertSubmenu<MenuHubLogicState.ItemMenu>(
            menuLogic,
            () => menuLogic.Input(
                new MenuHubLogicState.Input.OpenItemMenu()
            )
        );
        AssertSubmenu<MenuHubLogicState.SkillMenu>(
            menuLogic,
            () => menuLogic.Input(
                new MenuHubLogicState.Input.OpenSkillMenu()
            )
        );
        AssertSubmenu<MenuHubLogicState.StatusMenu>(
            menuLogic,
            () => menuLogic.Input(
                new MenuHubLogicState.Input.OpenStatusMenu()
            )
        );
        AssertSubmenu<MenuHubLogicState.EquipMenu>(
            menuLogic,
            () => menuLogic.Input(
                new MenuHubLogicState.Input.OpenEquipMenu()
            )
        );
        AssertSubmenu<MenuHubLogicState.CustomMenu>(
            menuLogic,
            () => menuLogic.Input(
                new MenuHubLogicState.Input.OpenCustomMenu()
            )
        );
        AssertSubmenu<MenuHubLogicState.PartyMenu>(
            menuLogic,
            () => menuLogic.Input(
                new MenuHubLogicState.Input.OpenPartyMenu()
            )
        );
        AssertSubmenu<MenuHubLogicState.QuestMenu>(
            menuLogic,
            () => menuLogic.Input(
                new MenuHubLogicState.Input.OpenQuestMenu()
            )
        );

        menuLogic.Input(new MenuHubLogicState.Input.OpenItemMenu());
        menuLogic.Input(new MenuHubLogicState.Input.Close());
        menuLogic.State.ShouldBeOfType<MenuHubLogicState.Disabled>();
        gameRepo.IsInMenu.Value.ShouldBeFalse();
    }

    private static void AssertSubmenu<TState>(
        IMenuHubLogic menuLogic,
        Action open
    ) where TState : MenuHubLogicState
    {
        open();
        menuLogic.State.ShouldBeOfType<TState>();

        menuLogic.Input(new MenuHubLogicState.Input.Back());
        menuLogic.State.ShouldBeOfType<MenuHubLogicState.MenuHub>();
    }
}
