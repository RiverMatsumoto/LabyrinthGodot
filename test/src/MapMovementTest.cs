namespace Labyrinth;

using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

public class MapMovementTest : TestClass
{
    public MapMovementTest(Node testScene) : base(testScene) { }

    [Test]
    public void MapsGridPositionToGridMapCell()
    {
        MapMovement.GridToMapCell(new Vector2I(2, 3))
            .ShouldBe(new Vector3I(2, 0, 3));
    }

    [Test]
    public void MapsGridPositionToWorldPosition()
    {
        MapMovement.GridToWorldPosition(new Vector2I(2, 3))
            .ShouldBe(new Vector3(2, 0, 3));
    }
}
