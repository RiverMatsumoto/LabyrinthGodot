namespace Labyrinth;

using System;
using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

public class BattleInvariantTest(Node testScene) : TestClass(testScene) {
  private static readonly ActionId AttackId = new("attack");

  [Test]
  public void RejectsAllDeadSetups() {
    using var repo = new BattleRepo(Catalog());

    Should.Throw<ArgumentException>(() => repo.Start(new BattleSetup(
      new EncounterId("test"),
      1,
      [Seed("hero", BattleTeam.Player, hp: 0)],
      [Seed("enemy", BattleTeam.Enemy)],
      new BattleReward()
    )));
  }

  [Test]
  public void BasicPlannerSkipsUnaffordableActions() {
    var costlyAction = new BattleActionDefinition(
      AttackId,
      "Attack",
      BattleTargetRule.SingleEnemy,
      [],
      TpCost: 5
    );
    var catalog = new BattleCatalog([costlyAction], []);
    var snapshot = new BattleSnapshot(
      1,
      BattleDomainPhase.SelectingCommands,
      [
        View("enemy", BattleTeam.Enemy, tp: 0),
        View("hero", BattleTeam.Player, tp: 20),
      ],
      []
    );

    new BasicEnemyCommandPlanner().Plan(
      snapshot,
      new BattlerId("enemy"),
      catalog,
      new SeededRandomSource(1)
    ).ShouldBeNull();
  }

  [Test]
  public void IgnoresInvalidEnemyPlannerCommands() {
    using var repo = new BattleRepo(Catalog());
    repo.Start(new BattleSetup(
      new EncounterId("test"),
      1,
      [Seed("hero", BattleTeam.Player)],
      [Seed("enemy", BattleTeam.Enemy)],
      new BattleReward()
    ));
    repo.SubmitCommand(new BattleCommand(
      new BattlerId("hero"),
      AttackId,
      new BattlerId("enemy")
    )).ShouldBeTrue();

    Should.NotThrow(() => repo.BeginResolution(new InvalidPlanner()));
    repo.AdvanceResolution().Kind.ShouldBe(
      BattleAdvanceKind.CuePlaybackRequired
    );
  }

  private static BattleCatalog Catalog() => new(
    [
      new BattleActionDefinition(
        AttackId,
        "Attack",
        BattleTargetRule.SingleEnemy,
        [new DamageEffectDefinition(
          new DamageSpec(
            DamageType.True,
            DamageMode.Fixed,
            new DamageValueDefinition(1),
            new DamageValueDefinition(1)
          )
        )]
      ),
    ],
    []
  );

  private static BattleBattlerSeed Seed(
    string id,
    BattleTeam team,
    int hp = 100
  ) => new(
    new BattlerId(id),
    id,
    team,
    new PartyPosition(PartyRow.Front, 0),
    BattleStats.Default,
    hp,
    20,
    [AttackId]
  );

  private static BattleUnitView View(
    string id,
    BattleTeam team,
    int tp
  ) => new(
    new BattlerId(id),
    id,
    team,
    new PartyPosition(PartyRow.Front, 0),
    BattleStats.Default,
    100,
    tp,
    [AttackId]
  );

  private sealed class InvalidPlanner : IEnemyCommandPlanner {
    public BattleCommand? Plan(
      BattleSnapshot snapshot,
      BattlerId enemyActorId,
      BattleCatalog catalog,
      IRandomSource random
    ) => new(
      enemyActorId,
      new ActionId("unknown"),
      new BattlerId("hero")
    );
  }
}
