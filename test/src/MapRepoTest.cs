namespace Labyrinth;

using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

public class MapRepoTest : TestClass
{
    public MapRepoTest(Node testScene) : base(testScene) { }

    [Test]
    public void RegistersEntityOnValidCell()
    {
        using var repo = new MapRepo();
        var id = new MapEntityId("player");
        var position = new Vector2I(1, 1);

        repo.TryRegisterEntity(id, position).ShouldBeTrue();
        repo.TryGetEntityPosition(id, out var actual).ShouldBeTrue();
        actual.ShouldBe(position);
        repo.TryGetEntityPose(id, out var pose).ShouldBeTrue();
        pose.ShouldBe(new MapEntityPose(position, GridDirection.North));
        repo.IsOccupied(position).ShouldBeTrue();
    }

    [Test]
    public void RejectsDuplicateIdAndOccupiedCell()
    {
        using var repo = new MapRepo();
        var position = new Vector2I(1, 1);

        repo.TryRegisterEntity(new MapEntityId("a"), position).ShouldBeTrue();

        repo.TryRegisterEntity(new MapEntityId("a"), new Vector2I(2, 1))
            .ShouldBeFalse();
        repo.TryRegisterEntity(new MapEntityId("b"), position).ShouldBeFalse();
    }

    [Test]
    public void BlocksMoveOutsideMap()
    {
        using var repo = new MapRepo();
        var id = new MapEntityId("edge");

        repo.TryRegisterEntity(id, new Vector2I(55, 55)).ShouldBeTrue();

        repo.CanEntityMove(id, GridDirection.East).ShouldBeFalse();
        repo.TryMoveEntity(id, GridDirection.East, out _).ShouldBeFalse();
    }

    [Test]
    public void BlocksMoveIntoBlockedCell()
    {
        using var repo = new MapRepo();
        var id = new MapEntityId("blocked");
        var position = new Vector2I(1, 1);
        var target = new Vector2I(2, 1);

        repo.GridCellMap[target] =
            repo.GridCellMap[target].WithFlags(GridCellFlags.BlocksMovement);

        repo.TryRegisterEntity(id, position).ShouldBeTrue();

        repo.CanEntityMove(id, GridDirection.East).ShouldBeFalse();
        repo.TryMoveEntity(id, GridDirection.East, out _).ShouldBeFalse();
    }

    [Test]
    public void BlocksMoveIntoOccupiedCell()
    {
        using var repo = new MapRepo();

        repo.TryRegisterEntity(new MapEntityId("a"), new Vector2I(1, 1))
            .ShouldBeTrue();
        repo.TryRegisterEntity(new MapEntityId("b"), new Vector2I(2, 1))
            .ShouldBeTrue();

        repo.TryMoveEntity(
                new MapEntityId("a"),
                GridDirection.East,
                out _
            )
            .ShouldBeFalse();
    }

    [Test]
    public void CommitsAcceptedMoveAndFreesPreviousCell()
    {
        using var repo = new MapRepo();
        var id = new MapEntityId("mover");
        var from = new Vector2I(1, 1);
        var to = new Vector2I(2, 1);

        repo.TryRegisterEntity(id, from).ShouldBeTrue();

        var direction = GridDirection.East;

        repo.TryMoveEntity(id, direction, out var move).ShouldBeTrue();

        move.ShouldBe(new GridMove(id, from, direction));
        move.Offset.ShouldBe(direction);
        move.To.ShouldBe(to);
        repo.TryGetEntityPosition(id, out var actual).ShouldBeTrue();
        actual.ShouldBe(to);
        repo.TryGetEntityPose(id, out var pose).ShouldBeTrue();
        pose.ShouldBe(new MapEntityPose(to, GridDirection.East));
        repo.IsOccupied(from).ShouldBeFalse();
        repo.IsOccupied(to).ShouldBeTrue();
    }

    [Test]
    public void CommitsAcceptedMoveWithoutChangingFacing()
    {
        using var repo = new MapRepo();
        var id = new MapEntityId("mover");
        var from = new Vector2I(1, 1);
        var to = new Vector2I(1, 2);

        repo.TryRegisterEntity(id, from, GridDirection.North)
            .ShouldBeTrue();

        repo.TryMoveEntityPreservingFacing(
                id,
                GridDirection.South,
                out var move
            )
            .ShouldBeTrue();

        move.ShouldBe(new GridMove(id, from, GridDirection.South));
        move.To.ShouldBe(to);
        repo.TryGetEntityPose(id, out var pose).ShouldBeTrue();
        pose.ShouldBe(new MapEntityPose(to, GridDirection.North));
        repo.IsOccupied(from).ShouldBeFalse();
        repo.IsOccupied(to).ShouldBeTrue();
    }

    [Test]
    public void TurnsEntityWithoutMoving()
    {
        using var repo = new MapRepo();
        var id = new MapEntityId("turner");
        var position = new Vector2I(1, 1);

        repo.TryRegisterEntity(id, position, GridDirection.North)
            .ShouldBeTrue();

        repo.TryTurnEntity(id, TurnDirection.Right, out var pose)
            .ShouldBeTrue();

        pose.ShouldBe(new MapEntityPose(position, GridDirection.East));
        repo.TryGetEntityPose(id, out var actual).ShouldBeTrue();
        actual.ShouldBe(pose);
        repo.IsOccupied(position).ShouldBeTrue();
    }
}
