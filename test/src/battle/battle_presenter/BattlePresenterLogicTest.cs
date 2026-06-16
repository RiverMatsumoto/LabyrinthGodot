namespace Labyrinth;

using System.Collections.Generic;
using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

public class BattlePresenterLogicTest(Node testScene) : TestClass(testScene)
{
    [Test]
    public void AttackSelectsTarget()
    {
        using var logic = StartedLogic();
        BattleActionOption? rendered = null;
        using var binding = logic.Bind()
            .OnOutput((
                in BattlePresenterLogicState.Output.RenderTargets output
            ) => rendered = output.Option);

        logic.ShowCommandPrompt(Screen(), Prompt());
        logic.SelectAttack();

        logic.State.ShouldBeOfType<
            BattlePresenterLogicState.AttackSelectingTarget>();
        rendered.ShouldNotBeNull();
        rendered.ActionId.ShouldBe(BattleContent.BasicAttackId);
    }

    [Test]
    public void SkillSelectsActionThenTarget()
    {
        using var logic = StartedLogic();
        using var binding = logic.Bind();

        logic.ShowCommandPrompt(Screen(), Prompt());
        logic.SelectSkill();

        logic.State.ShouldBeOfType<
            BattlePresenterLogicState.SkillSelectingAction>();

        logic.SelectSkillAction(0);
        logic.Confirm();

        logic.State.ShouldBeOfType<
            BattlePresenterLogicState.SkillSelectingTarget>();
    }

    [Test]
    public void PlaceholdersDoNotSubmitCommand()
    {
        using var logic = StartedLogic();
        var commands = new List<BattleCommand>();
        using var binding = logic.Bind()
            .OnOutput((
                in BattlePresenterLogicState.Output.CommandSubmitted output
            ) => commands.Add(output.Command));

        logic.ShowCommandPrompt(Screen(), Prompt());
        logic.SelectItem();
        logic.SelectDefence();
        logic.SelectMove();

        commands.ShouldBeEmpty();
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
        logic.RequestEscape();

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
            new BattleUnitViewModel(
                new BattlerId("hero"),
                "Hero",
                BattleTeam.Player,
                new PartyPosition(PartyRow.Front, 0),
                100,
                100,
                20,
                20,
                true
            ),
            new BattleUnitViewModel(
                new BattlerId("enemy"),
                "Enemy",
                BattleTeam.Enemy,
                new PartyPosition(PartyRow.Front, 0),
                100,
                100,
                20,
                20,
                true
            ),
        ]
    );

    private static BattleCommandPrompt Prompt() => new(
        new BattlerId("hero"),
        new BattleActionOption(
            BattleContent.BasicAttackId,
            "Attack",
            0,
            [new BattleTargetOption(new BattlerId("enemy"), "Enemy")]
        ),
        [
            new BattleActionOption(
                BattleContent.FireId,
                "Fire",
                4,
                [new BattleTargetOption(new BattlerId("enemy"), "Enemy")]
            ),
        ]
    );
}
