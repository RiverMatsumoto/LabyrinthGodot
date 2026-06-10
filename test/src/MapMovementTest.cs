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

    [Test]
    public void MapsFacingDirectionToYaw()
    {
        MapMovement.FacingDirectionToYaw(GridDirection.North)
            .ShouldBe(0.0f);
        MapMovement.FacingDirectionToYaw(GridDirection.East)
            .ShouldBe(-Mathf.Pi / 2.0f);
        MapMovement.FacingDirectionToYaw(GridDirection.South)
            .ShouldBe(Mathf.Pi);
        MapMovement.FacingDirectionToYaw(GridDirection.West)
            .ShouldBe(Mathf.Pi / 2.0f);
    }

    [Test]
    public void MapsCameraAimInputToLocalOffset()
    {
        PlayerMovementController.CameraAimInputToLocalOffset(
                new Vector2(1.0f, -1.0f)
            )
            .ShouldBe(new Vector3(
                Mathf.Sqrt(2.0f),
                0.0f,
                -Mathf.Sqrt(2.0f)
            ));
    }
}
