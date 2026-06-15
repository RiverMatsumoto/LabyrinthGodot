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
        repo.SubmitCommand(new BattleCommand(
            new BattlerId("hero"),
            actionId,
            new BattlerId("enemy")
        )).ShouldBeTrue();
        repo.BeginResolution(new NoEnemyCommandPlanner());

        var animation = repo.AdvanceResolution();
        animation.Cues.Single().ShouldBeOfType<AnimationCue>();
        Unit(repo, "enemy").Hp.ShouldBe(100);

        repo.AcknowledgeCuePlayback(animation.CueBatchId);
        var popup = repo.AdvanceResolution();
        popup.Cues.Single().ShouldBeOfType<PopupBatchCue>()
            .Popups.Single().Amount.ShouldBe(10);
        Unit(repo, "enemy").Hp.ShouldBe(90);
    }

    [Test]
    public void ValidatesAndUndoesPlayerCommands()
    {
        var actionId = new ActionId("strike");
        using var repo = new BattleRepo(Catalog(
            new BattleActionDefinition(
                actionId,
                "Strike",
                BattleTargetRule.SingleEnemy,
                [FixedDamage(10)]
            )
        ));
        var heroId = new BattlerId("hero");
        var enemyId = new BattlerId("enemy");
        repo.Start(Setup(
            [Seed("hero", BattleTeam.Player, actionId)],
            [Seed("enemy", BattleTeam.Enemy, actionId)]
        ));

        var command = new BattleCommand(heroId, actionId, enemyId);
        repo.ValidateCommand(command).IsValid.ShouldBeTrue();
        repo.SubmitCommand(command).ShouldBeTrue();
        repo.RequestedPlayerId.ShouldBeNull();

        repo.UndoLastCommand().ShouldBeTrue();
        repo.RequestedPlayerId.ShouldBe(heroId);
        repo.ValidateCommand(new BattleCommand(
            enemyId,
            actionId,
            heroId
        )).IsValid.ShouldBeFalse();
    }

    [Test]
    public void BasicEnemyPlannerCreatesCommandForKnownAction()
    {
        var actionId = new ActionId("strike");
        var catalog = Catalog(new BattleActionDefinition(
            actionId,
            "Strike",
            BattleTargetRule.SingleEnemy,
            [FixedDamage(10)]
        ));
        using var repo = new BattleRepo(catalog);
        var heroId = new BattlerId("hero");
        var enemyId = new BattlerId("enemy");
        repo.Start(Setup(
            [Seed("hero", BattleTeam.Player, actionId)],
            [Seed("enemy", BattleTeam.Enemy, actionId)]
        ));

        var command = new BasicEnemyCommandPlanner().Plan(
            repo.Snapshot(),
            enemyId,
            catalog,
            new SeededRandomSource(1)
        );

        command.ShouldNotBeNull();
        command.ActorId.ShouldBe(enemyId);
        command.ActionId.ShouldBe(actionId);
        command.TargetId.ShouldBe(heroId);
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
        repo.SubmitCommand(new BattleCommand(
            new BattlerId("hero"),
            meleeId,
            new BattlerId("front_1")
        ));
        repo.BeginResolution(new NoEnemyCommandPlanner());
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
            DefaultStatuses(),
            DefaultReactions()
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
        stunRepo.SubmitCommand(new BattleCommand(
            new BattlerId("hero"),
            stunId,
            new BattlerId("enemy")
        ));
        stunRepo.BeginResolution(new FixedEnemyCommandPlanner(
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
        var reactionId = new ReactionId("echo_damage");
        var reaction = new ReactionDefinition(
            reactionId,
            ReactionTrigger.Damage,
            ReactionSchedule.Immediate,
            ReactionTargetPolicy.EventTarget,
            Priority: 0,
            Conditions: [],
            Effects: [FixedDamage(1)]
        );
        var catalog = new BattleCatalog(
            [
                new BattleActionDefinition(
                    actionId,
                    "Reactive",
                    BattleTargetRule.SingleEnemy,
                    [
                        new RegisterReactionEffectDefinition(reactionId),
                        FixedDamage(1),
                    ],
                    RetargetPolicy: RetargetPolicy.NearestValid
                ),
            ],
            DefaultStatuses(),
            DefaultReactions().Append(reaction)
        );
        using var repo = new BattleRepo(catalog);
        repo.Start(Setup(
            [Seed("hero", BattleTeam.Player, actionId)],
            [Seed("enemy", BattleTeam.Enemy, actionId, hp: 1000)]
        ));
        SubmitAndResolve(repo, actionId, "enemy");

        Unit(repo, "enemy").Hp.ShouldBe(983);
    }

    [Test]
    public void CompilesReusableEnemiesIntoDistinctPlacements()
    {
        var enemy = EnemyResource();
        var encounter = EncounterResource(
            Placement("squirrel_1", PartyRow.Front, 0, enemy),
            Placement("squirrel_2", PartyRow.Back, 2, enemy)
        );

        var definition = encounter.Compile(
            BattleContent.CreateDefaultCatalog()
        );

        definition.Enemies.Select(seed => seed.Id).ShouldBe([
            new BattlerId("squirrel_1"),
            new BattlerId("squirrel_2"),
        ]);
        definition.Enemies[0].Position.ShouldBe(
            new PartyPosition(PartyRow.Front, 0)
        );
        definition.Enemies[1].Position.ShouldBe(
            new PartyPosition(PartyRow.Back, 2)
        );
        definition.Enemies.All(seed => seed.Name == "Squirrel")
            .ShouldBeTrue();
    }

    [Test]
    public void CompilesAuthoredBattleContent()
    {
        var resource = ResourceLoader.Load<BattleContentResource>(
            "res://src/battle/resources/BattleContent.tres"
        );

        resource.ShouldNotBeNull();
        var content = resource.Compile();
        content.Catalog.GetStatus(BattleContent.PoisonId)
            .ReactionIds.ShouldContain(
                BattleContent.PoisonTickReactionId
            );
        content.Catalog.GetReaction(
            BattleContent.ToxicRecoveryReactionId
        ).Conditions.ShouldNotBeEmpty();
        content.GetEncounter(new EncounterId("floor_1_squirrel"))
            .Enemies.Single().Id.ShouldBe(new BattlerId("squirrel_1"));
    }

    [Test]
    public void RejectsInvalidEncounterPlacements()
    {
        var catalog = BattleContent.CreateDefaultCatalog();
        var enemy = EnemyResource();

        Should.Throw<InvalidOperationException>(() => EncounterResource(
            Placement("same", PartyRow.Front, 0, enemy),
            Placement("same", PartyRow.Front, 1, enemy)
        ).Compile(catalog));
        Should.Throw<InvalidOperationException>(() => EncounterResource(
            Placement("one", PartyRow.Front, 0, enemy),
            Placement("two", PartyRow.Front, 0, enemy)
        ).Compile(catalog));
        Should.Throw<InvalidOperationException>(() => EncounterResource(
            Placement("invalid", PartyRow.Front, 3, enemy)
        ).Compile(catalog));
        Should.Throw<InvalidOperationException>(() => EncounterResource(
            Placement("missing", PartyRow.Front, 0, null)
        ).Compile(catalog));
        Should.Throw<InvalidOperationException>(() => EncounterResource(
            Enumerable.Range(0, 7)
                .Select(index => Placement(
                    $"enemy_{index}",
                    index < 3 ? PartyRow.Front : PartyRow.Back,
                    index % 3,
                    enemy
                ))
                .ToArray()
        ).Compile(catalog));
    }

    [Test]
    public void FiltersInnateReactionsByActionAndOwnerRelation()
    {
        var markedId = new ActionId("marked");
        var otherId = new ActionId("other");
        var reactionId = new ReactionId("marked_target_recovery");
        var reaction = new ReactionDefinition(
            reactionId,
            ReactionTrigger.Damage,
            ReactionSchedule.Immediate,
            ReactionTargetPolicy.Owner,
            Priority: 0,
            Conditions:
            [
                new TriggerActionConditionDefinition(markedId),
                new OwnerRelationConditionDefinition(
                    ReactionOwnerRelation.EventTarget
                ),
            ],
            Effects: [new HealEffectDefinition(5)]
        );
        var catalog = new BattleCatalog(
            [
                DamageAction(markedId, 10),
                DamageAction(otherId, 10),
            ],
            DefaultStatuses(),
            DefaultReactions().Append(reaction)
        );
        using var repo = new BattleRepo(catalog);
        repo.Start(Setup(
            [
                Seed(
                    "hero",
                    BattleTeam.Player,
                    markedId,
                    extraActions: [otherId]
                ),
            ],
            [
                Seed(
                    "enemy",
                    BattleTeam.Enemy,
                    markedId,
                    reactions: [reactionId]
                ),
            ]
        ));

        SubmitAndResolve(repo, markedId, "enemy");
        Unit(repo, "enemy").Hp.ShouldBe(95);
        SubmitAndResolve(repo, otherId, "enemy");
        Unit(repo, "enemy").Hp.ShouldBe(85);
    }

    [Test]
    public void FiltersReactionsByStatusAndEventSource()
    {
        var poisonActionId = new ActionId("self_poison");
        var reactionId = new ReactionId("status_apply_recovery");
        var reaction = new ReactionDefinition(
            reactionId,
            ReactionTrigger.StatusApplied,
            ReactionSchedule.Immediate,
            ReactionTargetPolicy.Owner,
            Priority: 0,
            Conditions:
            [
                new OwnerHasStatusConditionDefinition(
                    BattleContent.PoisonId
                ),
                new TriggerStatusConditionDefinition(
                    BattleContent.PoisonId
                ),
                new OwnerRelationConditionDefinition(
                    ReactionOwnerRelation.EventSource
                ),
            ],
            Effects: [new HealEffectDefinition(7)]
        );
        var catalog = new BattleCatalog(
            [
                new BattleActionDefinition(
                    poisonActionId,
                    "Self Poison",
                    BattleTargetRule.Self,
                    [new ApplyStatusEffectDefinition(
                        BattleContent.PoisonId,
                        Duration: 1
                    )]
                ),
            ],
            DefaultStatuses(),
            DefaultReactions().Append(reaction)
        );
        using var repo = new BattleRepo(catalog);
        repo.Start(Setup(
            [
                Seed(
                    "hero",
                    BattleTeam.Player,
                    poisonActionId,
                    hp: 50,
                    reactions: [reactionId]
                ),
            ],
            [Seed("enemy", BattleTeam.Enemy, poisonActionId)]
        ));

        SubmitAndResolve(repo, poisonActionId, "hero");

        Unit(repo, "hero").Hp.ShouldBe(52);
    }

    [Test]
    public void HonorsReactionPriorityUsesAndScheduling()
    {
        ScheduledReactionResult(ReactionSchedule.Immediate)
            .ShouldBe(90);
        ScheduledReactionResult(ReactionSchedule.AfterCurrentAction)
            .ShouldBe(95);
        ScheduledReactionResult(ReactionSchedule.EndOfTurn)
            .ShouldBe(95);

        var actionId = new ActionId("priority_hit");
        var healId = new ReactionId("priority_heal");
        var damageId = new ReactionId("priority_damage");
        var catalog = new BattleCatalog(
            [DamageAction(actionId, 10)],
            DefaultStatuses(),
            DefaultReactions().Concat([
                new ReactionDefinition(
                    healId,
                    ReactionTrigger.Damage,
                    ReactionSchedule.Immediate,
                    ReactionTargetPolicy.Owner,
                    Priority: 10,
                    Conditions: [],
                    Effects: [new HealEffectDefinition(20)],
                    Uses: 1
                ),
                new ReactionDefinition(
                    damageId,
                    ReactionTrigger.Damage,
                    ReactionSchedule.Immediate,
                    ReactionTargetPolicy.Owner,
                    Priority: 0,
                    Conditions: [],
                    Effects:
                    [
                        new ModifyResourceEffectDefinition(
                            BattleResource.Hp,
                            -10
                        ),
                    ],
                    Uses: 1
                ),
            ])
        );
        using var repo = new BattleRepo(catalog);
        repo.Start(Setup(
            [Seed("hero", BattleTeam.Player, actionId)],
            [
                Seed(
                    "enemy",
                    BattleTeam.Enemy,
                    actionId,
                    reactions: [damageId, healId]
                ),
            ]
        ));

        SubmitAndResolve(repo, actionId, "enemy");

        Unit(repo, "enemy").Hp.ShouldBe(90);
    }

    [Test]
    public void DrainsDeferredReactionsWithinCurrentBoundary()
    {
        var actionId = new ActionId("after_chain");
        var healId = new ReactionId("after_heal");
        var costId = new ReactionId("after_heal_cost");
        var catalog = new BattleCatalog(
            [DamageAction(actionId, 10)],
            DefaultStatuses(),
            DefaultReactions().Concat([
                new ReactionDefinition(
                    healId,
                    ReactionTrigger.Damage,
                    ReactionSchedule.AfterCurrentAction,
                    ReactionTargetPolicy.Owner,
                    Priority: 0,
                    Conditions:
                    [
                        new OwnerRelationConditionDefinition(
                            ReactionOwnerRelation.EventTarget
                        ),
                    ],
                    Effects: [new HealEffectDefinition(20)]
                ),
                new ReactionDefinition(
                    costId,
                    ReactionTrigger.Healing,
                    ReactionSchedule.AfterCurrentAction,
                    ReactionTargetPolicy.Owner,
                    Priority: 0,
                    Conditions:
                    [
                        new OwnerRelationConditionDefinition(
                            ReactionOwnerRelation.EventTarget
                        ),
                    ],
                    Effects:
                    [
                        new ModifyResourceEffectDefinition(
                            BattleResource.Hp,
                            -5
                        ),
                    ]
                ),
            ])
        );
        using var repo = new BattleRepo(catalog);
        repo.Start(Setup(
            [Seed("hero", BattleTeam.Player, actionId)],
            [
                Seed(
                    "enemy",
                    BattleTeam.Enemy,
                    actionId,
                    reactions: [healId, costId]
                ),
            ]
        ));

        SubmitAndResolve(repo, actionId, "enemy");

        Unit(repo, "enemy").Hp.ShouldBe(95);

        var statusId = new StatusId("brief");
        var applyId = new ActionId("apply_brief");
        var removalId = new ReactionId("brief_removed");
        var expirationCatalog = new BattleCatalog(
            [
                new BattleActionDefinition(
                    applyId,
                    "Apply Brief",
                    BattleTargetRule.Self,
                    [new ApplyStatusEffectDefinition(statusId)]
                ),
            ],
            DefaultStatuses().Append(new StatusDefinition(
                statusId,
                "Brief",
                PreventsAction: false,
                DefaultDuration: 1
            )),
            DefaultReactions().Append(new ReactionDefinition(
                removalId,
                ReactionTrigger.StatusRemoved,
                ReactionSchedule.EndOfTurn,
                ReactionTargetPolicy.Owner,
                Priority: 0,
                Conditions:
                [
                    new TriggerStatusConditionDefinition(statusId),
                    new OwnerRelationConditionDefinition(
                        ReactionOwnerRelation.EventTarget
                    ),
                ],
                Effects:
                [
                    new ModifyResourceEffectDefinition(
                        BattleResource.Hp,
                        -10
                    ),
                ]
            ))
        );
        using var expirationRepo = new BattleRepo(expirationCatalog);
        expirationRepo.Start(Setup(
            [
                Seed(
                    "hero",
                    BattleTeam.Player,
                    applyId,
                    reactions: [removalId]
                ),
            ],
            [Seed("enemy", BattleTeam.Enemy, applyId)]
        ));

        SubmitAndResolve(expirationRepo, applyId, "hero");

        Unit(expirationRepo, "hero").Hp.ShouldBe(90);
    }

    [Test]
    public void ScalesStatusReactionsAndRemovesThemOnExpiration()
    {
        var poisonId = new ActionId("double_poison");
        var waitId = new ActionId("wait");
        var catalog = new BattleCatalog(
            [
                new BattleActionDefinition(
                    poisonId,
                    "Double Poison",
                    BattleTargetRule.SingleEnemy,
                    [new ApplyStatusEffectDefinition(
                        BattleContent.PoisonId,
                        Stacks: 2,
                        Duration: 1
                    )],
                    RetargetPolicy: RetargetPolicy.NearestValid
                ),
                new BattleActionDefinition(
                    waitId,
                    "Wait",
                    BattleTargetRule.SingleEnemy,
                    [],
                    RetargetPolicy: RetargetPolicy.NearestValid
                ),
            ],
            DefaultStatuses(),
            DefaultReactions()
        );
        using var repo = new BattleRepo(catalog);
        repo.Start(Setup(
            [
                Seed(
                    "hero",
                    BattleTeam.Player,
                    poisonId,
                    extraActions: [waitId]
                ),
            ],
            [Seed("enemy", BattleTeam.Enemy, waitId)]
        ));

        SubmitAndResolve(repo, poisonId, "enemy");
        Unit(repo, "enemy").Hp.ShouldBe(90);
        SubmitAndResolve(repo, waitId, "enemy");
        Unit(repo, "enemy").Hp.ShouldBe(90);
    }

    [Test]
    public void AppliesDamageAndStatusAffinitiesButBypassesTrueDamage()
    {
        var fireId = new ActionId("fire_fixed");
        var trueId = new ActionId("true_fixed");
        var poisonId = new ActionId("affinity_poison");
        var catalog = new BattleCatalog(
            [
                DamageAction(fireId, 100, DamageType.Fire),
                DamageAction(trueId, 100, DamageType.True),
                new BattleActionDefinition(
                    poisonId,
                    "Affinity Poison",
                    BattleTargetRule.SingleEnemy,
                    [new ApplyStatusEffectDefinition(
                        BattleContent.PoisonId,
                        Duration: 1,
                        BaseChance: 1
                    )],
                    RetargetPolicy: RetargetPolicy.NearestValid
                ),
            ],
            DefaultStatuses(),
            DefaultReactions()
        );

        using var fireRepo = AffinityRepo(catalog, fireId);
        SubmitAndResolve(fireRepo, fireId, "enemy");
        Unit(fireRepo, "enemy").Hp.ShouldBe(388);

        using var trueRepo = AffinityRepo(catalog, trueId);
        SubmitAndResolve(trueRepo, trueId, "enemy");
        Unit(trueRepo, "enemy").Hp.ShouldBe(400);

        using var statusRepo = AffinityRepo(catalog, poisonId);
        SubmitAndResolve(statusRepo, poisonId, "enemy");
        Unit(statusRepo, "enemy").Hp.ShouldBe(495);
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
        repo.SubmitCommand(new BattleCommand(
            new BattlerId("hero"),
            actionId,
            new BattlerId(targetId)
        )).ShouldBeTrue();
        repo.BeginResolution(new NoEnemyCommandPlanner());
        ResolveUntilSelection(repo);
    }

    private static List<BattleCue> ResolveUntilSelection(BattleRepo repo)
    {
        var cues = new List<BattleCue>();
        var guard = 0;
        while (
            repo.Phase is BattleDomainPhase.ResolvingTurn
                or BattleDomainPhase.AwaitingCuePlayback
        )
        {
            guard++;
            guard.ShouldBeLessThan(256);
            if (repo.Phase == BattleDomainPhase.AwaitingCuePlayback)
            {
                throw new InvalidOperationException(
                    "Test failed to acknowledge cue playback."
                );
            }
            var advance = repo.AdvanceResolution();
            if (advance.Kind == BattleAdvanceKind.CuePlaybackRequired)
            {
                cues.AddRange(advance.Cues);
                repo.AcknowledgeCuePlayback(advance.CueBatchId);
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

    private static BattleActionDefinition DamageAction(
        ActionId id,
        int amount,
        DamageType type = DamageType.True
    ) => new(
        id,
        id.Value,
        BattleTargetRule.SingleEnemy,
        [new DamageEffectDefinition(new DamageSpec(
            type,
            DamageMode.Fixed,
            amount
        ))],
        RetargetPolicy: RetargetPolicy.NearestValid
    );

    private static int ScheduledReactionResult(
        ReactionSchedule schedule
    )
    {
        var actionId = new ActionId($"schedule_{schedule}");
        var reactionId = new ReactionId($"schedule_{schedule}");
        var reaction = new ReactionDefinition(
            reactionId,
            ReactionTrigger.Damage,
            schedule,
            ReactionTargetPolicy.Owner,
            Priority: 0,
            Conditions: [],
            Effects: [new HealEffectDefinition(15)],
            Uses: 1
        );
        var catalog = new BattleCatalog(
            [
                new BattleActionDefinition(
                    actionId,
                    actionId.Value,
                    BattleTargetRule.SingleEnemy,
                    [FixedDamage(10), FixedDamage(10)],
                    RetargetPolicy: RetargetPolicy.NearestValid
                ),
            ],
            DefaultStatuses(),
            DefaultReactions().Append(reaction)
        );
        using var repo = new BattleRepo(catalog);
        repo.Start(Setup(
            [Seed("hero", BattleTeam.Player, actionId)],
            [
                Seed(
                    "enemy",
                    BattleTeam.Enemy,
                    actionId,
                    reactions: [reactionId]
                ),
            ]
        ));
        SubmitAndResolve(repo, actionId, "enemy");
        return Unit(repo, "enemy").Hp;
    }

    private static BattleRepo AffinityRepo(
        BattleCatalog catalog,
        ActionId actionId
    )
    {
        var repo = new BattleRepo(catalog);
        repo.Start(Setup(
            [Seed("hero", BattleTeam.Player, actionId)],
            [
                Seed(
                    "enemy",
                    BattleTeam.Enemy,
                    actionId,
                    hp: 500,
                    statusResistances:
                        new Dictionary<StatusId, double>
                        {
                            [BattleContent.PoisonId] = 0.5,
                        },
                    statusWeaknesses:
                        new Dictionary<StatusId, double>
                        {
                            [BattleContent.PoisonId] = 1,
                        },
                    damageResistances:
                        new Dictionary<DamageType, double>
                        {
                            [DamageType.Fire] = 0.25,
                        },
                    damageWeaknesses:
                        new Dictionary<DamageType, double>
                        {
                            [DamageType.Fire] = 0.5,
                        }
                ),
            ]
        ));
        return repo;
    }

    private static BattleEnemyResource EnemyResource() => new()
    {
        Id = "squirrel",
        DisplayName = "Squirrel",
        Stats = new BattleStatsResource(),
        Hp = 25,
        ActionIds = [BattleContent.BasicAttackId.Value],
    };

    private static BattleEnemyPlacementResource Placement(
        string battlerId,
        PartyRow row,
        int slot,
        BattleEnemyResource? enemy
    ) => new()
    {
        BattlerId = battlerId,
        Row = row,
        Slot = slot,
        Enemy = enemy,
    };

    private static BattleEncounterResource EncounterResource(
        params BattleEnemyPlacementResource[] placements
    )
    {
        var encounter = new BattleEncounterResource { Id = "test" };
        foreach (var placement in placements)
        {
            encounter.Enemies.Add(placement);
        }
        return encounter;
    }

    private static BattleCatalog Catalog(
        params BattleActionDefinition[] actions
    ) => new(actions, DefaultStatuses(), DefaultReactions());

    private static StatusDefinition[] DefaultStatuses() =>
    [
        new(
            BattleContent.PoisonId,
            "Poison",
            PreventsAction: false,
            DefaultDuration: 3,
            MaxStacks: 3,
            ReactionIdList: [BattleContent.PoisonTickReactionId]
        ),
        new(
            BattleContent.StunId,
            "Stun",
            PreventsAction: true,
            DefaultDuration: 1
        ),
        new(
            BattleContent.RegenId,
            "Regen",
            PreventsAction: false,
            DefaultDuration: 3,
            MaxStacks: 3,
            ReactionIdList: [BattleContent.RegenTickReactionId]
        ),
    ];

    private static ReactionDefinition[] DefaultReactions() =>
    [
        new(
            BattleContent.PoisonTickReactionId,
            ReactionTrigger.TurnEnded,
            ReactionSchedule.EndOfTurn,
            ReactionTargetPolicy.Owner,
            Priority: 10,
            Conditions: [],
            Effects:
            [
                new DamageEffectDefinition(
                    new DamageSpec(
                        DamageType.True,
                        DamageMode.Fixed,
                        5
                    ),
                    Scale: new StatusStackScaleDefinition(
                        BattleContent.PoisonId
                    )
                ),
            ]
        ),
        new(
            BattleContent.RegenTickReactionId,
            ReactionTrigger.TurnEnded,
            ReactionSchedule.EndOfTurn,
            ReactionTargetPolicy.Owner,
            Priority: 0,
            Conditions: [],
            Effects:
            [
                new HealEffectDefinition(
                    5,
                    Scale: new StatusStackScaleDefinition(
                        BattleContent.RegenId
                    )
                ),
            ]
        ),
        new(
            BattleContent.ToxicRecoveryReactionId,
            ReactionTrigger.TurnEnded,
            ReactionSchedule.EndOfTurn,
            ReactionTargetPolicy.Owner,
            Priority: 0,
            Conditions:
            [
                new OwnerHasStatusConditionDefinition(
                    BattleContent.PoisonId
                ),
            ],
            Effects:
            [
                new HealEffectDefinition(
                    5,
                    Scale: new StatusStackScaleDefinition(
                        BattleContent.PoisonId
                    )
                ),
            ]
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
        IReadOnlyDictionary<StatusId, double>? resistances = null,
        IReadOnlyList<ReactionId>? reactions = null,
        IReadOnlyDictionary<StatusId, double>?
            statusResistances = null,
        IReadOnlyDictionary<StatusId, double>?
            statusWeaknesses = null,
        IReadOnlyDictionary<DamageType, double>?
            damageResistances = null,
        IReadOnlyDictionary<DamageType, double>?
            damageWeaknesses = null
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
        ReactionIdList: reactions,
        StatusResistances: statusResistances ?? resistances,
        StatusWeaknesses: statusWeaknesses,
        DamageTypeResistances: damageResistances,
        DamageTypeWeaknesses: damageWeaknesses
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

    private sealed class FixedEnemyCommandPlanner(
        ActionId actionId,
        BattlerId targetId
    ) : IEnemyCommandPlanner
    {
        public BattleCommand? Plan(
            BattleSnapshot snapshot,
            BattlerId enemyActorId,
            BattleCatalog catalog,
            IRandomSource random
        ) => new(enemyActorId, actionId, targetId);
    }
}
