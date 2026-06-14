namespace Labyrinth;

using System.Collections.Generic;
using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

public class BattleLogicTest : TestClass
{
    public BattleLogicTest(Node testScene) : base(testScene) { }

    [Test]
    public void CoordinatesSelectionPresentationAndAcknowledgement()
    {
        var actionId = new ActionId("attack");
        var catalog = new BattleCatalog(
            [
                new BattleActionDefinition(
                    actionId,
                    "Attack",
                    BattleTargetRule.SingleEnemy,
                    [
                        new DamageEffectDefinition(
                            new DamageSpec(
                                DamageType.True,
                                DamageMode.Fixed,
                                5
                            ),
                            "attack"
                        ),
                    ],
                    RetargetPolicy: RetargetPolicy.NearestValid
                ),
            ],
            []
        );
        using var repo = new BattleRepo(catalog);
        using var logic = new BattleLogic();
        logic.Set<IBattleRepo>(repo);
        logic.Set<IEnemyIntentProvider>(new NoEnemyIntentProvider());

        var requested = new List<BattlerId>();
        BattleStep? presentation = null;
        using var binding = logic.Bind()
            .OnOutput((
                in BattleLogicState.Output.CommandRequested output
            ) => requested.Add(output.BattlerId))
            .OnOutput((
                in BattleLogicState.Output.PresentationRequested output
            ) => presentation = output.Step);

        logic.Start<BattleLogicState.Disabled>();
        logic.StartBattle(new BattleSetup(
            new EncounterId("test"),
            1,
            GameMode.Labyrinth,
            [Seed("hero", BattleTeam.Player, actionId)],
            [Seed("enemy", BattleTeam.Enemy, actionId)],
            new BattleReward()
        ));

        logic.State.ShouldBeOfType<BattleLogicState.SelectingCommands>();
        requested.ShouldContain(new BattlerId("hero"));

        logic.SubmitIntent(new BattleIntent(
            new BattlerId("hero"),
            actionId,
            new BattlerId("enemy")
        ));
        logic.State.ShouldBeOfType<BattleLogicState.ResolvingTurn>();

        logic.AdvanceResolution();
        logic.State.ShouldBeOfType<BattleLogicState.AwaitingPresentation>();
        presentation.ShouldNotBeNull();

        logic.AcknowledgePresentation(presentation.PresentationId);
        logic.State.ShouldBeOfType<BattleLogicState.ResolvingTurn>();
    }

    private static BattleBattlerSeed Seed(
        string id,
        BattleTeam team,
        ActionId actionId
    ) => new(
        new BattlerId(id),
        id,
        team,
        new PartyPosition(PartyRow.Front, 0),
        BattleStats.Default,
        100,
        20,
        [actionId]
    );

    private sealed class NoEnemyIntentProvider : IEnemyIntentProvider
    {
        public BattleIntent? Plan(
            BattleSnapshot snapshot,
            BattlerId enemyId,
            BattleCatalog catalog,
            IRandomSource random
        ) => null;
    }
}
