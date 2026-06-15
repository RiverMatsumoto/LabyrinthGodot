namespace Labyrinth;

using Chickensoft.GodotNodeInterfaces;
using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

public class GridMapTerrainCompilerTest(Node testScene)
  : TestClass(testScene) {
  [Test]
  public void CompilesAuthoredMeshNames() {
    var gridMap = new GridMap();
    var library = new MeshLibrary();
    library.CreateItem(0);
    library.SetItemName(0, "Floor");
    library.CreateItem(1);
    library.SetItemName(1, "Wall");
    gridMap.MeshLibrary = library;
    gridMap.SetCellItem(new Vector3I(0, 0, 0), 0);
    gridMap.SetCellItem(new Vector3I(1, 0, 0), 1);

    var terrain = GridMapTerrainCompiler.Compile(
      new GridMapAdapter(gridMap),
      2,
      1
    );

    terrain[0, 0].Terrain.ShouldBe(GridCellTerrain.Floor);
    terrain[1, 0].Terrain.ShouldBe(GridCellTerrain.Wall);
    gridMap.Free();
  }
}
