namespace Labyrinth;

using System.Collections.Generic;
using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

public class BattleLogicTest : TestClass
{
    public BattleLogicTest(Node testScene) : base(testScene) { }

    [Test]
    public void CoordinatesSelectionCuePlaybackAndAcknowledgement()
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
                                new DamageValueDefinition(5),
                                new DamageValueDefinition(1)
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
        logic.Set<IEnemyCommandPlanner>(new NoEnemyCommandPlanner());
        logic.Set<IBattleSession>(new FakeBattleSession(new BattleSetup(
            new EncounterId("test"),
            1,
            [Seed("hero", BattleTeam.Player, actionId)],
            [Seed("enemy", BattleTeam.Enemy, actionId)],
            new BattleReward()
        )));

        var requested = new List<BattlerId>();
        long? cueBatchId = null;
        using var binding = logic.Bind()
            .OnOutput((
                in BattleLogicState.Output.CommandRequested output
            ) => requested.Add(output.ActorId))
            .OnOutput((
                in BattleLogicState.Output.CuePlaybackRequested output
            ) => cueBatchId = output.CueBatchId);

        logic.Start<BattleLogicState.Disabled>();
        logic.StartRequestedBattle();

        logic.State.ShouldBeOfType<BattleLogicState.SelectingCommands>();
        requested.ShouldContain(new BattlerId("hero"));

        logic.SubmitCommand(new BattleCommand(
            new BattlerId("hero"),
            actionId,
            new BattlerId("enemy")
        ));
        logic.State.ShouldBeOfType<BattleLogicState.ResolvingTurn>();

        logic.AdvanceResolution();
        logic.State.ShouldBeOfType<BattleLogicState.AwaitingCuePlayback>();
        cueBatchId.ShouldNotBeNull();

        logic.AcknowledgeCuePlayback(cueBatchId.Value);
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

    private sealed class NoEnemyCommandPlanner : IEnemyCommandPlanner
    {
        public BattleCommand? Plan(
            BattleSnapshot snapshot,
            BattlerId enemyActorId,
            BattleCatalog catalog,
            IRandomSource random
        ) => null;
    }

    private sealed class FakeBattleSession(BattleSetup setup)
        : IBattleSession
    {
        public BattleSetup CreateSetup() => setup;
        public GameMode Complete(BattleResult result) => GameMode.Labyrinth;
    }
}
