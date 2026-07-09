namespace Labyrinth;

using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

public class SceneContractsTest(Node testScene) : TestClass(testScene) {
  [Test]
  public void BattleUsesAuthoredContentAndUniqueNodes() {
    var battle = Instantiate<Battle>("res://src/battle/Battle.tscn");

    battle.Content.ShouldNotBeNull();
    battle.PartyContent.ShouldNotBeNull();
    battle.Content.Compile().GetEncounter(
      BattleContent.DefaultEncounterId
    ).ShouldNotBeNull();
    battle.GetNode("%Presenter").ShouldNotBeNull();
    battle.GetNode("Presenter/ActionMenu").ShouldNotBeNull();
    battle.GetNode("Presenter/SkillActions").ShouldNotBeNull();
    battle.GetNode("Presenter/PartySlots").ShouldNotBeNull();
    battle.GetNode("Presenter/EnemySlots").ShouldNotBeNull();
    battle.GetNode("Presenter/PartySlots/PlayerFront0/PartyMemberUi")
      .ShouldNotBeNull();
    battle.GetNode("Presenter/EnemySlots/EnemyFront0/TargetCaret")
      .ShouldNotBeNull();
    battle.GetNode("Presenter/MessagePanel/Message").ShouldNotBeNull();
    battle.Free();
  }

  [Test]
  public void FeatureScenesExposeInjectedNodes() {
    var game = Instantiate<Game>("res://src/game/Game.tscn");
    game.GetNode("%Map").ShouldNotBeNull();
    game.GetNode("%MenuHub").ShouldNotBeNull();
    game.GetNode("%Battle").ShouldNotBeNull();
    game.Free();

    var map = Instantiate<Map>("res://src/map/Map.tscn");
    map.GetNode("%GridMap").ShouldNotBeNull();
    map.GetNode("%Entities").ShouldNotBeNull();
    map.Free();

    var movement = Instantiate<MapMovement>(
      "res://src/map_movement/MapMovement.tscn"
    );
    movement.GetNode("%CooldownTimer").ShouldNotBeNull();
    movement.Free();

    var enemy = Instantiate<EnemyMapEntity>(
      "res://src/enemy_map_entity/EnemyMapEntity.tscn"
    );
    enemy.GetNode("%MapMovement").ShouldNotBeNull();
    enemy.GetNode("%MovementController").ShouldNotBeNull();
    enemy.Free();
  }

  [Test]
  public void PresenterSpeedIsPresentationOnly() {
    BattlePresenter.CalculateEffectiveSpeed(1, false).ShouldBe(1);
    BattlePresenter.CalculateEffectiveSpeed(1, true).ShouldBe(2);
  }

  private static T Instantiate<T>(string path) where T : Node =>
    GD.Load<PackedScene>(path).Instantiate<T>();
}
