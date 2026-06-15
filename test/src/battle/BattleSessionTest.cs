namespace Labyrinth;

using System;
using System.Collections.Generic;
using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

public class BattleSessionTest(Node testScene) : TestClass(testScene) {
  [Test]
  public void ResolvesExactEncounterPersistsVitalsAndKeepsReturnMode() {
    var encounterId = new EncounterId("authored");
    var content = Content(encounterId);
    using var gameRepo = new GameRepo();
    using var partyRepo = new PartyRepo();
    var member = Member();
    partyRepo.TryAdd(
      member,
      new PartyPosition(PartyRow.Front, 0)
    ).ShouldBeTrue();
    gameRepo.SetBattleRequest(new BattleRequest(
      encounterId,
      7,
      GameMode.Town
    ));
    var session = new BattleSession(content, gameRepo, partyRepo);

    var setup = session.CreateSetup();
    setup.EncounterId.ShouldBe(encounterId);
    setup.Seed.ShouldBe(7);
    setup.Players[0].Id.ShouldBe(member.Id);

    gameRepo.SetBattleRequest(new BattleRequest(
      encounterId,
      9,
      GameMode.Labyrinth
    ));
    session.Complete(new BattleResult(
      BattleOutcome.Victory,
      encounterId,
      new BattleReward(),
      new Dictionary<BattlerId, (int Hp, int Tp)> {
        [member.Id] = (4, 3),
      }
    )).ShouldBe(GameMode.Town);
    member.Hp.ShouldBe(4);
    member.Tp.ShouldBe(3);
  }

  [Test]
  public void RejectsUnknownEncounterWithoutFallback() {
    using var gameRepo = new GameRepo();
    using var partyRepo = new PartyRepo();
    gameRepo.SetBattleRequest(new BattleRequest(
      new EncounterId("missing"),
      1,
      GameMode.Labyrinth
    ));
    var session = new BattleSession(
      Content(new EncounterId("authored")),
      gameRepo,
      partyRepo
    );

    Should.Throw<KeyNotFoundException>(() => session.CreateSetup());
  }

  private static CompiledBattleContent Content(EncounterId encounterId) {
    var encounter = new EncounterDefinition(
      encounterId,
      [
        new BattleBattlerSeed(
          new BattlerId("enemy"),
          "Enemy",
          BattleTeam.Enemy,
          new PartyPosition(PartyRow.Front, 0),
          BattleStats.Default,
          100,
          0,
          [BattleContent.BasicAttackId]
        ),
      ],
      new BattleReward()
    );
    return new CompiledBattleContent(
      BattleContent.CreateDefaultCatalog(),
      new Dictionary<EncounterId, EncounterDefinition> {
        [encounterId] = encounter,
      },
      new Dictionary<EquipmentId, EquipmentDefinition>()
    );
  }

  private static PartyMember Member() {
    var member = new PartyMember {
      Id = new BattlerId("hero"),
      Name = "Hero",
      BaseStats = BattleStats.Default,
      Hp = 100,
      Tp = 20,
    };
    member.LearnedActions.Add(BattleContent.BasicAttackId);
    return member;
  }
}
