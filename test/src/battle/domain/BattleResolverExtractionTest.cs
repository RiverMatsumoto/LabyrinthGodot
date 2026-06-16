namespace Labyrinth;

using System.Linq;
using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

public class BattleResolverExtractionTest(Node testScene)
    : TestClass(testScene)
{
    private static readonly ActionId AttackId = new("attack");

    [Test]
    public void RuntimeResetClearsMutableBattleState()
    {
        var runtime = new BattleRuntime(Catalog());
        runtime.Units.Add(
            new BattlerId("hero"),
            Unit("hero", BattleTeam.Player)
        );
        runtime.PlayerCommands.Add(
            new BattlerId("hero"),
            new BattleCommand(
                new BattlerId("hero"),
                AttackId,
                null
            )
        );
        runtime.CommandOrder.Add(new BattlerId("hero"));
        runtime.Operations.AddLast(new FinishTurnOperation());
        runtime.NextCauseId();
        runtime.NextCueBatchId();
        runtime.NextReactiveEffectRegistrationId();
        runtime.Turn = 3;
        runtime.Phase = BattleDomainPhase.ResolvingTurn;

        runtime.Reset();

        runtime.Units.ShouldBeEmpty();
        runtime.PlayerCommands.ShouldBeEmpty();
        runtime.CommandOrder.ShouldBeEmpty();
        runtime.Operations.ShouldBeEmpty();
        runtime.Turn.ShouldBe(0);
        runtime.Phase.ShouldBe(BattleDomainPhase.Disabled);
        runtime.NextCauseId().ShouldBe(1);
        runtime.NextCueBatchId().ShouldBe(1);
        runtime.NextReactiveEffectRegistrationId().ShouldBe(1);
    }

    [Test]
    public void CommandServiceValidatesAndOrdersCommands()
    {
        var catalog = Catalog(
            Action(priority: 0),
            new BattleActionDefinition(
                new ActionId("fast"),
                "Fast",
                BattleTargetRule.SingleEnemy,
                [],
                Priority: 2
            )
        );
        var runtime = new BattleRuntime(catalog)
        {
            Phase = BattleDomainPhase.SelectingCommands,
        };
        runtime.Units.Add(
            new BattlerId("hero"),
            Unit("hero", BattleTeam.Player)
        );
        runtime.Units.Add(
            new BattlerId("enemy"),
            Unit(
                "enemy",
                BattleTeam.Enemy,
                [new ActionId("fast")]
            )
        );
        var targeting = new BattleTargetResolver(runtime);
        var commands = new BattleCommandService(runtime, targeting);

        commands.Submit(new BattleCommand(
            new BattlerId("hero"),
            AttackId,
            new BattlerId("enemy")
        )).ShouldBeTrue();

        var ordered = commands.BuildOrderedCommands(
            new FixedPlanner(new BattleCommand(
                new BattlerId("enemy"),
                new ActionId("fast"),
                new BattlerId("hero")
            ))
        );

        ordered.Select(command => command.ActorId).ShouldBe([
            new BattlerId("enemy"),
            new BattlerId("hero"),
        ]);
    }

    [Test]
    public void TargetResolverReturnsSelectedRow()
    {
        var rowAction = new BattleActionDefinition(
            AttackId,
            "Row",
            BattleTargetRule.RowEnemies,
            []
        );
        var runtime = new BattleRuntime(Catalog(rowAction));
        var hero = Unit("hero", BattleTeam.Player);
        runtime.Units.Add(hero.Id, hero);
        runtime.Units.Add(
            new BattlerId("front"),
            Unit("front", BattleTeam.Enemy)
        );
        runtime.Units.Add(
            new BattlerId("back-a"),
            Unit(
                "back-a",
                BattleTeam.Enemy,
                position: new PartyPosition(PartyRow.Back, 0)
            )
        );
        runtime.Units.Add(
            new BattlerId("back-b"),
            Unit(
                "back-b",
                BattleTeam.Enemy,
                position: new PartyPosition(PartyRow.Back, 1)
            )
        );

        var targets = new BattleTargetResolver(runtime)
            .ResolveTargets(
                hero,
                rowAction,
                new BattlerId("back-b")
            );

        targets.Select(unit => unit.Id).ShouldBe([
            new BattlerId("back-a"),
            new BattlerId("back-b"),
        ]);
    }

    [Test]
    public void EffectBuilderPreservesOperationOrder()
    {
        var runtime = new BattleRuntime(Catalog());
        var operations = new BattleEffectOperationBuilder(runtime).Build(
            new DamageEffectDefinition(
                new DamageSpec(
                    DamageType.True,
                    DamageMode.Fixed,
                    5
                ),
                AnimationId: "attack"
            ),
            new EffectContext(
                new BattlerId("hero"),
                [new BattlerId("enemy")],
                BattleRange.Melee,
                AttackId,
                0,
                null,
                0,
                0
            )
        );

        operations[0].ShouldBeOfType<TriggerReactiveEffectsOperation>();
        operations[1].ShouldBeOfType<VisualCueOperation>();
        operations[2].ShouldBeOfType<DamageOperation>();
        operations[3].ShouldBeOfType<TriggerReactiveEffectsOperation>();
    }

    [Test]
    public void ReactiveEffectResolverDefersAfterActionReactiveEffects()
    {
        var reactiveEffectId = new ReactiveEffectId("counter");
        var reactiveEffect = new ReactiveEffectDefinition(
            reactiveEffectId,
            ReactiveEffectTrigger.Damage,
            ReactiveEffectSchedule.AfterCurrentAction,
            ReactiveEffectTargetPolicy.Owner,
            Priority: 0,
            Conditions: [],
            Effects: [new HealEffectDefinition(1)]
        );
        var runtime = new BattleRuntime(new BattleCatalog(
            [Action()],
            [],
            [reactiveEffect]
        ));
        var owner = Unit("enemy", BattleTeam.Enemy);
        runtime.Units.Add(owner.Id, owner);
        var targeting = new BattleTargetResolver(runtime);
        var effects = new BattleEffectOperationBuilder(runtime);
        var reactiveEffects = new BattleReactiveEffectResolver(
            runtime,
            effects,
            targeting
        );
        reactiveEffects.Register(owner.Id, reactiveEffectId, null);

        reactiveEffects.Trigger(new ReactiveEffectEvent(
            runtime.NextCauseId(),
            ReactiveEffectTrigger.Damage,
            new BattlerId("hero"),
            owner.Id,
            AttackId
        ));

        runtime.AfterActionReactiveEffects.Count.ShouldBe(1);
        runtime.Operations.ShouldBeEmpty();
    }

    [Test]
    public void OutcomeResolverFinishesTurnsAndCompletesVictory()
    {
        var runtime = new BattleRuntime(Catalog())
        {
            Setup = Setup(),
            Phase = BattleDomainPhase.ResolvingTurn,
            Turn = 1,
        };
        runtime.Units.Add(
            new BattlerId("hero"),
            Unit("hero", BattleTeam.Player)
        );
        var enemy = Unit("enemy", BattleTeam.Enemy);
        runtime.Units.Add(enemy.Id, enemy);
        var outcome = new BattleOutcomeResolver(runtime);

        outcome.FinishTurn();

        runtime.Turn.ShouldBe(2);
        runtime.Phase.ShouldBe(BattleDomainPhase.SelectingCommands);

        runtime.Phase = BattleDomainPhase.ResolvingTurn;
        enemy.Hp = 0;
        outcome.FinishTurn();

        runtime.Phase.ShouldBe(BattleDomainPhase.Completed);
        runtime.Result!.Outcome.ShouldBe(BattleOutcome.Victory);
    }

    private static BattleCatalog Catalog(
        params BattleActionDefinition[] actions
    ) => new(actions.Length == 0 ? [Action()] : actions, []);

    private static BattleActionDefinition Action(int priority = 0) =>
        new(
            AttackId,
            "Attack",
            BattleTargetRule.SingleEnemy,
            [],
            Priority: priority
        );

    private static BattleUnit Unit(
        string id,
        BattleTeam team,
        ActionId[]? actions = null,
        PartyPosition? position = null
    ) => new(new BattleBattlerSeed(
        new BattlerId(id),
        id,
        team,
        position ?? new PartyPosition(PartyRow.Front, 0),
        BattleStats.Default,
        100,
        20,
        actions ?? [AttackId]
    ));

    private static BattleSetup Setup() => new(
        new EncounterId("test"),
        1,
        [],
        [],
        new BattleReward()
    );

    private sealed class FixedPlanner(BattleCommand command)
        : IEnemyCommandPlanner
    {
        public BattleCommand? Plan(
            BattleSnapshot snapshot,
            BattlerId enemyActorId,
            BattleCatalog catalog,
            IRandomSource random
        ) => command;
    }
}
