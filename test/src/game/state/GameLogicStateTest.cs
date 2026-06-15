namespace Labyrinth;

using System.Collections.Generic;
using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

public class GameLogicStateTest(Node testScene) : TestClass(testScene) {
  [Test]
  public void EmitsEnvironmentOutputAndUsesAuthoredDefaultEncounter() {
    using var repo = new GameRepo();
    using var logic = new GameLogic();
    logic.Set<IGameRepo>(repo);
    var maxFps = new List<int>();
    using var binding = logic.Bind()
      .OnOutput((
        in GameLogicState.Output.MaxFpsRequested output
      ) => maxFps.Add(output.MaxFps));

    logic.Start<GameLogicState.MainMenu>();
    logic.Input(new GameLogicState.Input.EnterLabyrinth());
    maxFps.ShouldBe([0]);

    logic.Input(new GameLogicState.Input.EnterBattle());
    repo.CurrentBattleRequest.EncounterId.ShouldBe(
      BattleContent.DefaultEncounterId
    );
  }
}
