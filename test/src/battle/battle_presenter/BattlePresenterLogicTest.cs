namespace Labyrinth;

using System.Collections.Generic;
using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

public class BattlePresenterLogicTest(Node testScene) : TestClass(testScene)
{
    [Test]
    public void ActionMenuAttackSelectsTarget()
    {
        using var logic = StartedLogic();
        BattleActionOption? rendered = null;
        using var binding = logic.Bind()
            .OnOutput((
                in BattlePresenterLogicState.Output.RenderTargets output
            ) => rendered = output.Option);

        logic.ShowCommandPrompt(Screen(), Prompt());
        logic.SelectMenuAction(0);
        logic.Confirm();

        logic.State.ShouldBeOfType<
            BattlePresenterLogicState.AttackSelectingTarget>();
        rendered.ShouldNotBeNull();
        rendered.ActionId.ShouldBe(BattleContent.BasicAttackId);
    }

    [Test]
    public void AttackAfterUndoSelectsTargetAgain()
    {
        using var logic = StartedLogic();
        var commands = new List<BattleCommand>();
        var undoRequested = false;
        using var binding = logic.Bind()
            .OnOutput((
                in BattlePresenterLogicState.Output.CommandSubmitted output
            ) =>
            {
                commands.Add(output.Command);
                logic.ShowCommandPrompt(Screen(), Prompt());
            })
            .OnOutput((
                in BattlePresenterLogicState.Output.UndoRequested _
            ) =>
            {
                undoRequested = true;
                logic.ShowCommandPrompt(Screen(), Prompt());
            });

        logic.ShowCommandPrompt(Screen(), Prompt());
        logic.SelectMenuAction(0);
        logic.Confirm();
        logic.Confirm();

        logic.State.ShouldBeOfType<
            BattlePresenterLogicState.CommandMenu>();

        logic.RequestUndo();

        undoRequested.ShouldBeTrue();

        logic.SelectMenuAction(0);
        logic.Confirm();

        logic.State.ShouldBeOfType<
            BattlePresenterLogicState.AttackSelectingTarget>();
        commands.Count.ShouldBe(1);
    }

    [Test]
    public void SkillSelectsActionThenTargetThenSubmits()
    {
        using var logic = StartedLogic();
        var commands = new List<BattleCommand>();
        using var binding = logic.Bind()
            .OnOutput((
                in BattlePresenterLogicState.Output.CommandSubmitted output
            ) => commands.Add(output.Command));

        logic.ShowCommandPrompt(Screen(), Prompt());
        logic.SelectMenuAction(1);
        logic.Confirm();

        logic.State.ShouldBeOfType<
            BattlePresenterLogicState.SkillSelectingAction>();

        logic.SelectSkillAction(0);
        logic.Confirm();

        logic.State.ShouldBeOfType<
            BattlePresenterLogicState.SkillSelectingTarget>();

        logic.Confirm();

        commands.ShouldBe([
            new BattleCommand(
                new BattlerId("hero"),
                BattleContent.FireId,
                new BattlerId("enemy")
            ),
        ]);
    }

    [Test]
    public void BackNavigatesLocallyBeforeUndo()
    {
        using var logic = StartedLogic();
        var undoCount = 0;
        using var binding = logic.Bind()
            .OnOutput((
                in BattlePresenterLogicState.Output.UndoRequested _
            ) => undoCount++);

        logic.ShowCommandPrompt(Screen(), Prompt());
        logic.SelectMenuAction(1);
        logic.Confirm();
        logic.Confirm();
        logic.State.ShouldBeOfType<
            BattlePresenterLogicState.SkillSelectingTarget>();

        logic.RequestUndo();
        logic.State.ShouldBeOfType<
            BattlePresenterLogicState.SkillSelectingAction>();
        undoCount.ShouldBe(0);

        logic.RequestUndo();
        logic.State.ShouldBeOfType<BattlePresenterLogicState.CommandMenu>();
        undoCount.ShouldBe(0);

        logic.RequestUndo();
        undoCount.ShouldBe(1);
    }

    [Test]
    public void RemembersLastActionSkillAndTargetBetweenTurns()
    {
        using var logic = StartedLogic();
        var selectedMenuIndex = -1;
        var selectedSkillIndex = -1;
        var selectedTargetIndex = -1;
        using var binding = logic.Bind()
            .OnOutput((
                in BattlePresenterLogicState.Output.RenderCommandMenu output
            ) => selectedMenuIndex = output.SelectedIndex)
            .OnOutput((
                in BattlePresenterLogicState.Output.RenderSkillActions output
            ) => selectedSkillIndex = output.SelectedIndex)
            .OnOutput((
                in BattlePresenterLogicState.Output.RenderTargets output
            ) => selectedTargetIndex = output.SelectedIndex);

        logic.ShowCommandPrompt(Screen(), PromptWithTwoSkillsAndTargets());
        logic.SelectMenuAction(1);
        logic.Confirm();
        logic.SelectSkillAction(1);
        logic.Confirm();
        logic.SelectTarget(1);
        logic.Confirm();
        logic.PlayCueBatch(Screen(), [new WaitCue(0)]);
        logic.CueVisualFinished();

        logic.State.ShouldBeOfType<BattlePresenterLogicState.Hidden>();

        logic.ShowCommandPrompt(Screen(), PromptWithTwoSkillsAndTargets());
        selectedMenuIndex.ShouldBe(1);

        logic.Confirm();
        selectedSkillIndex.ShouldBe(1);

        logic.Confirm();
        selectedTargetIndex.ShouldBe(1);
    }

    [Test]
    public void RemembersSubmittedCommandsByActor()
    {
        using var logic = StartedLogic();
        var selectedMenuIndex = -1;
        var selectedSkillIndex = -1;
        using var binding = logic.Bind()
            .OnOutput((
                in BattlePresenterLogicState.Output.RenderCommandMenu output
            ) => selectedMenuIndex = output.SelectedIndex)
            .OnOutput((
                in BattlePresenterLogicState.Output.RenderSkillActions output
            ) => selectedSkillIndex = output.SelectedIndex);

        logic.ShowCommandPrompt(
            Screen(),
            PromptWithTwoSkillsAndTargets("hero")
        );
        logic.SelectMenuAction(1);
        logic.Confirm();
        logic.SelectSkillAction(1);
        logic.Confirm();
        logic.Confirm();

        logic.ShowCommandPrompt(Screen(), Prompt("mage"));
        selectedMenuIndex.ShouldBe(0);
        logic.SelectMenuAction(0);
        logic.Confirm();
        logic.Confirm();

        logic.ShowCommandPrompt(
            Screen(),
            PromptWithTwoSkillsAndTargets("hero")
        );
        selectedMenuIndex.ShouldBe(1);
        logic.Confirm();
        selectedSkillIndex.ShouldBe(1);

        logic.ShowCommandPrompt(Screen(), Prompt("mage"));
        selectedMenuIndex.ShouldBe(0);
    }

    [Test]
    public void DoesNotReturnToRememberedActionWhenDisabled()
    {
        using var logic = StartedLogic();
        var selectedMenuIndex = -1;
        using var binding = logic.Bind()
            .OnOutput((
                in BattlePresenterLogicState.Output.RenderCommandMenu output
            ) => selectedMenuIndex = output.SelectedIndex);

        logic.ShowCommandPrompt(Screen(), PromptWithTwoSkillsAndTargets());
        logic.SelectMenuAction(1);
        logic.Confirm();
        logic.SelectSkillAction(1);
        logic.Confirm();
        logic.Confirm();

        logic.ShowCommandPrompt(Screen(), PromptWithDisabledIce());

        selectedMenuIndex.ShouldBe(0);
    }

    [Test]
    public void DisabledActionsDoNotSubmitCommand()
    {
        using var logic = StartedLogic();
        var commands = new List<BattleCommand>();
        using var binding = logic.Bind()
            .OnOutput((
                in BattlePresenterLogicState.Output.CommandSubmitted output
            ) => commands.Add(output.Command));

        logic.ShowCommandPrompt(Screen(), DisabledPrompt());
        logic.SelectMenuAction(0);
        logic.Confirm();
        logic.SelectMenuAction(1);
        logic.Confirm();
        logic.SelectMenuAction(2);
        logic.Confirm();
        logic.SelectMenuAction(3);
        logic.Confirm();
        logic.SelectMenuAction(4);
        logic.Confirm();

        commands.ShouldBeEmpty();
    }

    [Test]
    public void TargetRulesSubmitExpectedPayloads()
    {
        using var logic = StartedLogic();
        var commands = new List<BattleCommand>();
        using var binding = logic.Bind()
            .OnOutput((
                in BattlePresenterLogicState.Output.CommandSubmitted output
            ) => commands.Add(output.Command));

        logic.ShowCommandPrompt(Screen(), RulePrompt(SelfAction()));
        logic.SelectMenuAction(0);
        logic.Confirm();
        logic.Confirm();

        logic.ShowCommandPrompt(Screen(), RulePrompt(AllAction()));
        logic.SelectMenuAction(0);
        logic.Confirm();
        logic.Confirm();

        logic.ShowCommandPrompt(Screen(), RulePrompt(RowAction()));
        logic.SelectMenuAction(0);
        logic.Confirm();
        logic.Confirm();

        commands.ShouldBe([
            new BattleCommand(
                new BattlerId("hero"),
                new ActionId("self"),
                null
            ),
            new BattleCommand(
                new BattlerId("hero"),
                new ActionId("all"),
                null
            ),
            new BattleCommand(
                new BattlerId("hero"),
                new ActionId("row"),
                new BattlerId("enemy")
            ),
        ]);
    }

    [Test]
    public void EscapeEmitsRequest()
    {
        using var logic = StartedLogic();
        var requested = false;
        using var binding = logic.Bind()
            .OnOutput((
                in BattlePresenterLogicState.Output.EscapeRequested _
            ) => requested = true);

        logic.ShowCommandPrompt(Screen(), Prompt());
        logic.SelectMenuAction(5);
        logic.Confirm();

        requested.ShouldBeTrue();
    }

    [Test]
    public void PlaysCueBatchInOrder()
    {
        using var logic = StartedLogic();
        var cues = new List<BattleCue>();
        var finished = false;
        using var binding = logic.Bind()
            .OnOutput((
                in BattlePresenterLogicState.Output.BeginCueVisual output
            ) => cues.Add(output.Cue))
            .OnOutput((
                in BattlePresenterLogicState.Output.CueBatchFinished _
            ) => finished = true);

        logic.PlayCueBatch(Screen(), [
            new WaitCue(0),
            new DeathCue(new BattlerId("enemy")),
        ]);

        logic.State.ShouldBeOfType<BattlePresenterLogicState.PlayingCueBatch>();
        cues.ShouldBe([new WaitCue(0)]);

        logic.CueVisualFinished();
        cues.ShouldBe([
            new WaitCue(0),
            new DeathCue(new BattlerId("enemy")),
        ]);

        logic.CueVisualFinished();
        finished.ShouldBeTrue();
        logic.State.ShouldBeOfType<BattlePresenterLogicState.Hidden>();
    }

    [Test]
    public void CancelReturnsToHidden()
    {
        using var logic = StartedLogic();

        logic.ShowCommandPrompt(Screen(), Prompt());
        logic.Cancel();

        logic.State.ShouldBeOfType<BattlePresenterLogicState.Hidden>();
    }

    private static BattlePresenterLogic StartedLogic()
    {
        var logic = new BattlePresenterLogic();
        logic.Start<BattlePresenterLogicState.Hidden>();
        return logic;
    }

    private static BattleScreenView Screen() => new(
        1,
        new BattlerId("hero"),
        [
            Unit("hero", "Hero", BattleTeam.Player, PartyRow.Front, 0),
            Unit("enemy", "Enemy", BattleTeam.Enemy, PartyRow.Front, 0),
            Unit("enemy_2", "Enemy 2", BattleTeam.Enemy, PartyRow.Front, 1),
        ]
    );

    private static BattleUnitViewModel Unit(
        string id,
        string name,
        BattleTeam team,
        PartyRow row,
        int slot
    ) => new(
        new BattlerId(id),
        name,
        team,
        new PartyPosition(row, slot),
        100,
        100,
        20,
        20,
        true,
        null
    );

    private static BattleCommandPrompt Prompt(string actorId = "hero") => new(
        new BattlerId(actorId),
        AttackAction(),
        [FireAction()]
    );

    private static BattleCommandPrompt PromptWithTwoSkillsAndTargets(
        string actorId = "hero"
    ) => new(
        new BattlerId(actorId),
        AttackAction(),
        [
            FireAction(),
            FireAction() with
            {
                ActionId = new ActionId("ice"),
                Name = "Ice",
                TargetOptions =
                [
                    EnemyTarget("enemy"),
                    EnemyTarget("enemy_2", slot: 1),
                ],
            },
        ]
    );

    private static BattleCommandPrompt PromptWithDisabledIce() => new(
        new BattlerId("hero"),
        AttackAction(),
        [
            FireAction(),
            FireAction() with
            {
                ActionId = new ActionId("ice"),
                Name = "Ice",
                IsEnabled = false,
                DisabledReason = "Not enough TP.",
            },
        ]
    );

    private static BattleCommandPrompt DisabledPrompt() => new(
        new BattlerId("hero"),
        AttackAction() with
        {
            IsEnabled = false,
            DisabledReason = "No target.",
        },
        [
            FireAction() with
            {
                IsEnabled = false,
                DisabledReason = "Not enough TP.",
            },
        ]
    );

    private static BattleCommandPrompt RulePrompt(
        BattleActionOption action
    ) => new(new BattlerId("hero"), action, []);

    private static BattleActionOption AttackAction() => new(
        BattleContent.BasicAttackId,
        "Attack",
        BattleTargetRule.SingleEnemy,
        0,
        [EnemyTarget("enemy")],
        true,
        ""
    );

    private static BattleActionOption FireAction() => new(
        BattleContent.FireId,
        "Fire",
        BattleTargetRule.SingleEnemy,
        4,
        [EnemyTarget("enemy")],
        true,
        ""
    );

    private static BattleActionOption SelfAction() => new(
        new ActionId("self"),
        "Self",
        BattleTargetRule.Self,
        0,
        [
            new BattleTargetOption(
                "Self",
                null,
                [new BattlerId("hero")],
                [new BattlerId("hero")],
                BattleTeam.Player,
                new BattleTargetGridPoint(
                    BattleTeam.Player,
                    PartyRow.Front,
                    0
                )
            ),
        ],
        true,
        ""
    );

    private static BattleActionOption AllAction() => new(
        new ActionId("all"),
        "All",
        BattleTargetRule.AllEnemies,
        0,
        [
            new BattleTargetOption(
                "All Enemies",
                null,
                [new BattlerId("enemy"), new BattlerId("enemy_2")],
                [new BattlerId("enemy"), new BattlerId("enemy_2")],
                BattleTeam.Enemy,
                new BattleTargetGridPoint(
                    BattleTeam.Enemy,
                    PartyRow.Front,
                    0
                )
            ),
        ],
        true,
        ""
    );

    private static BattleActionOption RowAction() => new(
        new ActionId("row"),
        "Row",
        BattleTargetRule.RowEnemies,
        0,
        [
            new BattleTargetOption(
                "Front Row",
                new BattlerId("enemy"),
                [new BattlerId("enemy")],
                [new BattlerId("enemy"), new BattlerId("enemy_2")],
                BattleTeam.Enemy,
                new BattleTargetGridPoint(
                    BattleTeam.Enemy,
                    PartyRow.Front,
                    0
                )
            ),
        ],
        true,
        ""
    );

    private static BattleTargetOption EnemyTarget(
        string id,
        int slot = 0
    ) => new(
        id,
        new BattlerId(id),
        [new BattlerId(id)],
        [new BattlerId(id)],
        BattleTeam.Enemy,
        new BattleTargetGridPoint(BattleTeam.Enemy, PartyRow.Front, slot)
    );
}
