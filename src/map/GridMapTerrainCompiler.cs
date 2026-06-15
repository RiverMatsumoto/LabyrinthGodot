namespace Labyrinth;

using System;
using Chickensoft.GodotNodeInterfaces;
using Godot;

public static class GridMapTerrainCompiler {
  public static GridCellMap Compile(
    IGridMap gridMap,
    int width = MapRepo.TerrainWidth,
    int height = MapRepo.TerrainHeight
  ) {
    ArgumentNullException.ThrowIfNull(gridMap);
    var meshLibrary = gridMap.MeshLibrary
      ?? throw new InvalidOperationException(
        "GridMap terrain requires a MeshLibrary."
      );
    var terrain = new GridCellMap(
      width,
      height,
      GridCellTerrain.Unmapped
    );

    foreach (var cell in gridMap.GetUsedCells()) {
      var position = new Vector2I(cell.X, cell.Z);
      if (!terrain.IsInside(position)) {
        throw new InvalidOperationException(
          $"GridMap cell {cell} is outside {width}x{height} terrain bounds."
        );
      }

      var itemName = meshLibrary.GetItemName(gridMap.GetCellItem(cell));
      terrain[position] = itemName switch {
        "Floor" => GridCell.Floor(position),
        "Wall" => GridCell.Wall(position),
        _ => throw new InvalidOperationException(
          $"Unsupported GridMap mesh item '{itemName}'."
        ),
      };
    }

    return terrain;
  }
}
