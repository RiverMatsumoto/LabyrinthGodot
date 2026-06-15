namespace Labyrinth;

using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

public class MapRepoTest(Node testScene) : TestClass(testScene) {
  [Test]
  public void LoadsCompiledTerrainAndMovesEntities() {
    var terrain = new GridCellMap(3, 3);
    terrain[1, 1] = GridCell.Floor(new Vector2I(1, 1));
    terrain[2, 1] = GridCell.Floor(new Vector2I(2, 1));
    using var repo = new MapRepo();
    repo.LoadTerrain(terrain);

    repo.TryRegisterEntity(
      MapRepo.PlayerId,
      new Vector2I(1, 1),
      GridDirection.North
    ).ShouldBeTrue();
    repo.TryMoveEntity(
      MapRepo.PlayerId,
      GridDirection.East,
      out var move
    ).ShouldBeTrue();
    move.To.ShouldBe(new Vector2I(2, 1));
  }
}
