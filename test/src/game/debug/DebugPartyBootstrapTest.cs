namespace Labyrinth;

#if DEBUG
using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

public class DebugPartyBootstrapTest(Node testScene) : TestClass(testScene) {
  [Test]
  public void SeedsOnlyAnEmptyParty() {
    using var partyRepo = new PartyRepo();
    var catalog = new BattleCatalog(
      [
        new BattleActionDefinition(
          BattleContent.BasicAttackId,
          "Attack",
          BattleTargetRule.SingleEnemy,
          []
        ),
      ],
      []
    );

    DebugPartyBootstrap.SeedIfEmpty(partyRepo, catalog);
    DebugPartyBootstrap.SeedIfEmpty(partyRepo, catalog);

    partyRepo.Count.ShouldBe(BattleLimits.MaxPlayerBattlers);
    partyRepo.Members.ShouldAllBe(entry =>
      entry.Member.LearnedActions.Count == 1
      && entry.Member.LearnedActions[0] == BattleContent.BasicAttackId
    );
  }

  [Test]
  public void StartsAuthoredBattleWithSeededParty() {
    var content = GD.Load<BattleContentResource>(
      "res://src/battle/resources/BattleContent.tres"
    ).Compile();
    using var gameRepo = new GameRepo();
    using var partyRepo = new PartyRepo();
    gameRepo.SetBattleRequest(new BattleRequest(
      BattleContent.DefaultEncounterId,
      1,
      GameMode.Labyrinth
    ));
    var setup = new BattleSession(
      content,
      gameRepo,
      partyRepo
    ).CreateSetup();
    using var battleRepo = new BattleRepo(content.Catalog);

    Should.NotThrow(() => battleRepo.Start(setup));
    battleRepo.Snapshot().Units.Count.ShouldBe(6);
  }
}
#endif
