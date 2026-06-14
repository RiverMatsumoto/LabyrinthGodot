namespace Labyrinth;

using System;
using System.Collections.Generic;
using System.Linq;
using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

public class BattleRepoTest : TestClass
{
    public BattleRepoTest(Node testScene) : base(testScene) { }

    [Test]
    public void ResolvesAnimationThenMutationThenPopup()
    {
        var actionId = new ActionId("strike");
        var catalog = Catalog(
            new BattleActionDefinition(
                actionId,
                "Strike",
                BattleTargetRule.SingleEnemy,
                [
                    new DamageEffectDefinition(
                        new DamageSpec(
                            DamageType.True,
                            DamageMode.Fixed,
                            10
                        ),
                        "strike"
                    ),
                ],
                RetargetPolicy: RetargetPolicy.NearestValid
            )
        );
        using var repo = new BattleRepo(catalog);
        repo.Start(Setup(
            [Seed("hero", BattleTeam.Player, actionId, agility: 20)],
            [Seed("enemy", BattleTeam.Enemy, actionId, hp: 100)]
        ));
        repo.SubmitIntent(new BattleIntent(
            new BattlerId("hero"),
            actionId,
            new BattlerId("enemy")
        )).ShouldBeTrue();
        repo.BeginResolution(new NoEnemyIntentProvider());

        var animation = repo.Advance();
        animation.Cues.Single().ShouldBeOfType<AnimationCue>();
        Unit(repo, "enemy").Hp.ShouldBe(100);

        repo.AcknowledgePresentation(animation.PresentationId);
        var popup = repo.Advance();
        popup.Cues.Single().ShouldBeOfType<PopupBatchCue>()
            .Popups.Single().Amount.ShouldBe(10);
        Unit(repo, "enemy").Hp.ShouldBe(90);
    }

    [Test]
    public void SupportsRowsRangeAndMultiTargetPopups()
    {
        var meleeId = new ActionId("row_slash");
        var rangedId = new ActionId("row_shot");
        var catalog = Catalog(
            new BattleActionDefinition(
                meleeId,
                "Row Slash",
                BattleTargetRule.RowEnemies,
                [FixedDamage(20)],
                Range: BattleRange.Melee,
                RetargetPolicy: RetargetPolicy.NearestValid
            ),
            new BattleActionDefinition(
                rangedId,
                "Row Shot",
                BattleTargetRule.RowEnemies,
                [FixedDamage(20)],
                Range: BattleRange.Ranged,
                RetargetPolicy: RetargetPolicy.NearestValid
            )
        );
        using var repo = new BattleRepo(catalog);
        repo.Start(Setup(
            [
                Seed(
                    "hero",
                    BattleTeam.Player,
                    meleeId,
                    row: PartyRow.Back,
                    extraActions: [rangedId]
                ),
            ],
            [
                Seed("front_1", BattleTeam.Enemy, meleeId),
                Seed("front_2", BattleTeam.Enemy, meleeId, slot: 1),
                Seed(
                    "back",
                    BattleTeam.Enemy,
                    meleeId,
                    row: PartyRow.Back
                ),
            ]
        ));

        repo.GetValidTargets(new BattlerId("hero"), meleeId)
            .Count.ShouldBe(3);
        repo.SubmitIntent(new BattleIntent(
            new BattlerId("hero"),
            meleeId,
            new BattlerId("front_1")
        ));
        repo.BeginResolution(new NoEnemyIntentProvider());
        var cues = ResolveUntilSelection(repo);

        var batch = cues.OfType<PopupBatchCue>().First();
        batch.Popups.Count.ShouldBe(2);
        batch.Popups.All(popup => popup.Amount == 10).ShouldBeTrue();
        Unit(repo, "back").Hp.ShouldBe(100);
    }

    [Test]
    public void AppliesPoisonStunRegenAndResistance()
    {
        var poisonId = new ActionId("poison");
        var regenId = new ActionId("regen");
        var stunId = new ActionId("stun");
        var attackId = new ActionId("attack");
        var catalog = new BattleCatalog(
            [
                new BattleActionDefinition(
                    poisonId,
                    "Poison",
                    BattleTargetRule.SingleEnemy,
                    [new ApplyStatusEffectDefinition(
                        BattleContent.PoisonId,
                        Duration: 2
                    )],
                    RetargetPolicy: RetargetPolicy.NearestValid
                ),
                new BattleActionDefinition(
                    regenId,
                    "Regen",
                    BattleTargetRule.Self,
                    [new ApplyStatusEffectDefinition(
                        BattleContent.RegenId,
                        Duration: 2
                    )]
                ),
                new BattleActionDefinition(
                    stunId,
                    "Stun",
                    BattleTargetRule.SingleEnemy,
                    [new ApplyStatusEffectDefinition(
                        BattleContent.StunId
                    )],
                    Priority: 10,
                    RetargetPolicy: RetargetPolicy.NearestValid
                ),
                new BattleActionDefinition(
                    attackId,
                    "Attack",
                    BattleTargetRule.SingleEnemy,
                    [FixedDamage(50)],
                    RetargetPolicy: RetargetPolicy.NearestValid
                ),
            ],
            DefaultStatuses()
        );

        using var poisonRepo = new BattleRepo(catalog);
        poisonRepo.Start(Setup(
            [Seed("hero", BattleTeam.Player, poisonId)],
            [Seed("enemy", BattleTeam.Enemy, attackId, hp: 100)]
        ));
        SubmitAndResolve(poisonRepo, poisonId, "enemy");
        Unit(poisonRepo, "enemy").Hp.ShouldBe(95);

        using var immuneRepo = new BattleRepo(catalog);
        immuneRepo.Start(Setup(
            [Seed("hero", BattleTeam.Player, poisonId)],
            [
                Seed(
                    "enemy",
                    BattleTeam.Enemy,
                    attackId,
                    hp: 100,
                    resistances: new Dictionary<StatusId, double>
                    {
                        [BattleContent.PoisonId] = 1,
                    }
                ),
            ]
        ));
        SubmitAndResolve(immuneRepo, poisonId, "enemy");
        Unit(immuneRepo, "enemy").Hp.ShouldBe(100);

        using var regenRepo = new BattleRepo(catalog);
        regenRepo.Start(Setup(
            [Seed("hero", BattleTeam.Player, regenId, hp: 50)],
            [Seed("enemy", BattleTeam.Enemy, attackId)]
        ));
        SubmitAndResolve(regenRepo, regenId, "hero");
        Unit(regenRepo, "hero").Hp.ShouldBe(55);

        using var stunRepo = new BattleRepo(catalog);
        stunRepo.Start(Setup(
            [
                Seed(
                    "hero",
                    BattleTeam.Player,
                    stunId,
                    agility: 20
                ),
            ],
            [
                Seed(
                    "enemy",
                    BattleTeam.Enemy,
                    attackId,
                    agility: 1
                ),
            ]
        ));
        stunRepo.SubmitIntent(new BattleIntent(
            new BattlerId("hero"),
            stunId,
            new BattlerId("enemy")
        ));
        stunRepo.BeginResolution(new FixedEnemyIntentProvider(
            attackId,
            new BattlerId("hero")
        ));
        ResolveUntilSelection(stunRepo);
        Unit(stunRepo, "hero").Hp.ShouldBe(100);
    }

    [Test]
    public void BoundsRecursiveReactions()
    {
        var actionId = new ActionId("reactive");
        var reaction = new ReactionDefinition(
            "echo_damage",
            ReactionWindow.Damage,
            ReactionInsertionPolicy.BeforeNextEffect,
            ReactionTargetPolicy.EventTarget,
            Priority: 0,
            [FixedDamage(1)]
        );
        var catalog = Catalog(new BattleActionDefinition(
            actionId,
            "Reactive",
            BattleTargetRule.SingleEnemy,
            [
                new RegisterReactionEffectDefinition(reaction),
                FixedDamage(1),
            ],
            RetargetPolicy: RetargetPolicy.NearestValid
        ));
        using var repo = new BattleRepo(catalog);
        repo.Start(Setup(
            [Seed("hero", BattleTeam.Player, actionId)],
            [Seed("enemy", BattleTeam.Enemy, actionId, hp: 1000)]
        ));
        SubmitAndResolve(repo, actionId, "enemy");

        Unit(repo, "enemy").Hp.ShouldBe(983);
    }

    [Test]
    public void RejectsDuplicateAndMissingResourceReferences()
    {
        var duplicate = new ActionId("duplicate");
        Should.Throw<InvalidOperationException>(() => new BattleCatalog(
            [
                new BattleActionDefinition(
                    duplicate,
                    "One",
                    BattleTargetRule.Self,
                    []
                ),
                new BattleActionDefinition(
                    duplicate,
                    "Two",
                    BattleTargetRule.Self,
                    []
                ),
            ],
            []
        ));

        Should.Throw<InvalidOperationException>(() => new BattleCatalog(
            [
                new BattleActionDefinition(
                    new ActionId("bad"),
                    "Bad",
                    BattleTargetRule.Self,
                    [new ApplyStatusEffectDefinition(
                        new StatusId("missing")
                    )]
                ),
            ],
            []
        ));
    }

    [Test]
    public void CalculatesHeldAcceptAsTwoTimesBaseSpeed()
    {
        BattlePresenter.CalculateEffectiveSpeed(1.5, false)
            .ShouldBe(1.5);
        BattlePresenter.CalculateEffectiveSpeed(1.5, true)
            .ShouldBe(3.0);
    }

    private static void SubmitAndResolve(
        BattleRepo repo,
        ActionId actionId,
        string targetId
    )
    {
        repo.SubmitIntent(new BattleIntent(
            new BattlerId("hero"),
            actionId,
            new BattlerId(targetId)
        )).ShouldBeTrue();
        repo.BeginResolution(new NoEnemyIntentProvider());
        ResolveUntilSelection(repo);
    }

    private static List<BattleCue> ResolveUntilSelection(BattleRepo repo)
    {
        var cues = new List<BattleCue>();
        var guard = 0;
        while (
            repo.Phase is BattleDomainPhase.ResolvingTurn
                or BattleDomainPhase.AwaitingPresentation
        )
        {
            guard++;
            guard.ShouldBeLessThan(256);
            if (repo.Phase == BattleDomainPhase.AwaitingPresentation)
            {
                throw new InvalidOperationException(
                    "Test failed to acknowledge a presentation."
                );
            }
            var step = repo.Advance();
            if (step.Kind == BattleStepKind.Presentation)
            {
                cues.AddRange(step.Cues);
                repo.AcknowledgePresentation(step.PresentationId);
            }
        }
        return cues;
    }

    private static BattleUnitView Unit(BattleRepo repo, string id) =>
        repo.Snapshot().Units.Single(unit =>
            unit.Id == new BattlerId(id));

    private static DamageEffectDefinition FixedDamage(int amount) =>
        new(new DamageSpec(
            DamageType.True,
            DamageMode.Fixed,
            amount
        ));

    private static BattleCatalog Catalog(
        params BattleActionDefinition[] actions
    ) => new(actions, DefaultStatuses());

    private static StatusDefinition[] DefaultStatuses() =>
    [
        new(
            BattleContent.PoisonId,
            "Poison",
            StatusBehavior.Poison,
            3,
            3,
            5
        ),
        new(
            BattleContent.StunId,
            "Stun",
            StatusBehavior.Stun,
            1
        ),
        new(
            BattleContent.RegenId,
            "Regen",
            StatusBehavior.Regen,
            3,
            3,
            5
        ),
    ];

    private static BattleSetup Setup(
        IReadOnlyList<BattleBattlerSeed> players,
        IReadOnlyList<BattleBattlerSeed> enemies
    ) => new(
        new EncounterId("test"),
        7,
        GameMode.Labyrinth,
        players,
        enemies,
        new BattleReward()
    );

    private static BattleBattlerSeed Seed(
        string id,
        BattleTeam team,
        ActionId actionId,
        int hp = 100,
        int agility = 10,
        PartyRow row = PartyRow.Front,
        int slot = 0,
        IReadOnlyList<ActionId>? extraActions = null,
        IReadOnlyDictionary<StatusId, double>? resistances = null
    ) => new(
        new BattlerId(id),
        id,
        team,
        new PartyPosition(row, slot),
        BattleStats.Default with
        {
            MaxHp = Math.Max(100, hp),
            Agility = agility,
        },
        hp,
        100,
        new[] { actionId }.Concat(extraActions ?? []).ToArray(),
        resistances
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

    private sealed class FixedEnemyIntentProvider(
        ActionId actionId,
        BattlerId targetId
    ) : IEnemyIntentProvider
    {
        public BattleIntent? Plan(
            BattleSnapshot snapshot,
            BattlerId enemyId,
            BattleCatalog catalog,
            IRandomSource random
        ) => new(enemyId, actionId, targetId);
    }
}
