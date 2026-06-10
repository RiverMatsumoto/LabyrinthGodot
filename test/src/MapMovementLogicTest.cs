namespace Labyrinth;

using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

public class MapMovementLogicTest : TestClass
{
    public MapMovementLogicTest(Node testScene) : base(testScene) { }

    [Test]
    public void RequestedMoveStartsAndArrivalFinishesMove()
    {
        using var repo = new MapRepo();
        var id = new MapEntityId("player");
        var from = new Vector2I(1, 1);
        var direction = GridDirection.East;
        var to = new Vector2I(2, 1);

        repo.TryRegisterEntity(id, from).ShouldBeTrue();

        using var logic = CreateStartedLogic(repo, id, from);
        var moveStarted = false;
        var startedMove = default(GridMove);

        using var binding = logic.Bind()
            .OnOutput(
                (in MapMovementLogicState.Output.MoveStarted output) =>
                {
                    moveStarted = true;
                    startedMove = output.Move;
                }
            );

        logic.Input(new MapMovementLogicState.Input.MoveRequested(direction));

        moveStarted.ShouldBeTrue();
        startedMove.ShouldBe(new GridMove(id, from, direction));
        startedMove.To.ShouldBe(to);
        logic.State.ShouldBeOfType<MapMovementLogicState.Moving>();
        repo.TryGetEntityPosition(id, out var actual).ShouldBeTrue();
        actual.ShouldBe(to);
        repo.TryGetEntityPose(id, out var pose).ShouldBeTrue();
        pose.FacingDirection.ShouldBe(GridDirection.East);

        logic.Input(new MapMovementLogicState.Input.MoveFinished());
        logic.State.ShouldBeOfType<MapMovementLogicState.Idle>();
    }

    [Test]
    public void BlockedMoveStaysIdle()
    {
        using var repo = new MapRepo();
        var id = new MapEntityId("player");
        var from = new Vector2I(1, 1);
        var direction = GridDirection.East;

        repo.TryRegisterEntity(id, from).ShouldBeTrue();
        repo.GridCellMap[from + direction] =
            repo.GridCellMap[from + direction]
                .WithFlags(GridCellFlags.BlocksMovement);

        using var logic = CreateStartedLogic(repo, id, from);
        var moveBlocked = false;
        var blockedDirection = GridDirection.North;

        using var binding = logic.Bind()
            .OnOutput(
                (in MapMovementLogicState.Output.MoveBlocked output) =>
                {
                    moveBlocked = true;
                    blockedDirection = output.Direction;
                }
            );

        logic.Input(new MapMovementLogicState.Input.MoveRequested(direction));

        moveBlocked.ShouldBeTrue();
        blockedDirection.ShouldBe(direction);
        logic.State.ShouldBeOfType<MapMovementLogicState.Idle>();
        repo.TryGetEntityPosition(id, out var actual).ShouldBeTrue();
        actual.ShouldBe(from);
    }

    [Test]
    public void MoveRequestWhileMovingIsIgnored()
    {
        using var repo = new MapRepo();
        var id = new MapEntityId("player");
        var from = new Vector2I(1, 1);
        var firstDirection = GridDirection.East;
        var secondDirection = GridDirection.East;
        var firstTo = new Vector2I(2, 1);

        repo.TryRegisterEntity(id, from).ShouldBeTrue();

        using var logic = CreateStartedLogic(repo, id, from);
        var moveStartedCount = 0;

        using var binding = logic.Bind()
            .OnOutput(
                (in MapMovementLogicState.Output.MoveStarted output) =>
                    moveStartedCount++
            );

        logic.Input(new MapMovementLogicState.Input.MoveRequested(firstDirection));
        logic.State.ShouldBeOfType<MapMovementLogicState.Moving>();

        logic.Input(new MapMovementLogicState.Input.MoveRequested(secondDirection));

        moveStartedCount.ShouldBe(1);
        logic.State.ShouldBeOfType<MapMovementLogicState.Moving>();
        repo.TryGetEntityPosition(id, out var actual).ShouldBeTrue();
        actual.ShouldBe(firstTo);
    }

    [Test]
    public void TurnRequestStartsTurnAndFinishReturnsToIdle()
    {
        using var repo = new MapRepo();
        var id = new MapEntityId("player");
        var position = new Vector2I(1, 1);

        repo.TryRegisterEntity(id, position, GridDirection.North)
            .ShouldBeTrue();

        using var logic = CreateStartedLogic(repo, id, position);
        var turnStarted = false;
        var turnFinished = false;
        var facingDirection = GridDirection.North;

        using var binding = logic.Bind()
            .OnOutput(
                (in MapMovementLogicState.Output.TurnStarted output) =>
                {
                    turnStarted = true;
                    facingDirection = output.FacingDirection;
                }
            )
            .OnOutput(
                (in MapMovementLogicState.Output.TurnFinished output) =>
                    turnFinished = true
            );

        logic.Input(new MapMovementLogicState.Input.TurnRequested(
            TurnDirection.Right
        ));

        turnStarted.ShouldBeTrue();
        facingDirection.ShouldBe(GridDirection.East);
        logic.State.ShouldBeOfType<MapMovementLogicState.Turning>();
        repo.TryGetEntityPose(id, out var pose).ShouldBeTrue();
        pose.ShouldBe(new MapEntityPose(position, GridDirection.East));

        logic.Input(new MapMovementLogicState.Input.TurnFinished());

        turnFinished.ShouldBeTrue();
        logic.State.ShouldBeOfType<MapMovementLogicState.Idle>();
    }

    [Test]
    public void RelativeMoveUsesCurrentFacingDirection()
    {
        using var repo = new MapRepo();
        var id = new MapEntityId("player");
        var from = new Vector2I(1, 1);
        var to = new Vector2I(2, 1);

        repo.TryRegisterEntity(id, from, GridDirection.North)
            .ShouldBeTrue();

        using var logic = CreateStartedLogic(repo, id, from);

        logic.Input(new MapMovementLogicState.Input.TurnRequested(
            TurnDirection.Right
        ));
        logic.Input(new MapMovementLogicState.Input.TurnFinished());
        logic.Input(new MapMovementLogicState.Input.RelativeMoveRequested(
            RelativeMoveDirection.Forward
        ));

        logic.State.ShouldBeOfType<MapMovementLogicState.Moving>();
        repo.TryGetEntityPose(id, out var pose).ShouldBeTrue();
        pose.ShouldBe(new MapEntityPose(to, GridDirection.East));
    }

    [Test]
    public void RelativeBackwardMovePreservesFacing()
    {
        using var repo = new MapRepo();
        var id = new MapEntityId("player");
        var from = new Vector2I(1, 1);
        var firstTo = new Vector2I(1, 2);
        var secondTo = new Vector2I(1, 3);

        repo.TryRegisterEntity(id, from, GridDirection.North)
            .ShouldBeTrue();

        using var logic = CreateStartedLogic(repo, id, from);

        logic.Input(new MapMovementLogicState.Input.RelativeMoveRequested(
            RelativeMoveDirection.Backward
        ));
        logic.State.ShouldBeOfType<MapMovementLogicState.Moving>();
        repo.TryGetEntityPose(id, out var firstPose).ShouldBeTrue();
        firstPose.ShouldBe(new MapEntityPose(firstTo, GridDirection.North));

        logic.Input(new MapMovementLogicState.Input.MoveFinished());
        logic.Input(new MapMovementLogicState.Input.RelativeMoveRequested(
            RelativeMoveDirection.Backward
        ));

        logic.State.ShouldBeOfType<MapMovementLogicState.Moving>();
        repo.TryGetEntityPose(id, out var secondPose).ShouldBeTrue();
        secondPose.ShouldBe(new MapEntityPose(secondTo, GridDirection.North));
    }

    [Test]
    public void MoveRequestWhileTurningIsIgnored()
    {
        using var repo = new MapRepo();
        var id = new MapEntityId("player");
        var position = new Vector2I(1, 1);

        repo.TryRegisterEntity(id, position, GridDirection.North)
            .ShouldBeTrue();

        using var logic = CreateStartedLogic(repo, id, position);
        var moveStartedCount = 0;

        using var binding = logic.Bind()
            .OnOutput(
                (in MapMovementLogicState.Output.MoveStarted output) =>
                    moveStartedCount++
            );

        logic.Input(new MapMovementLogicState.Input.TurnRequested(
            TurnDirection.Right
        ));
        logic.State.ShouldBeOfType<MapMovementLogicState.Turning>();

        logic.Input(new MapMovementLogicState.Input.MoveRequested(
            GridDirection.East
        ));

        moveStartedCount.ShouldBe(0);
        logic.State.ShouldBeOfType<MapMovementLogicState.Turning>();
        repo.TryGetEntityPose(id, out var pose).ShouldBeTrue();
        pose.ShouldBe(new MapEntityPose(position, GridDirection.East));
    }

    private static MapMovementLogic CreateStartedLogic(
        IMapRepo repo,
        MapEntityId id,
        Vector2I position
    )
    {
        var logic = new MapMovementLogic();
        logic.Set<IMapRepo>(repo);

        var data = logic.Get<MapMovementLogic.Data>();
        data.EntityId = id;

        logic.Start<MapMovementLogicState.Idle>();
        return logic;
    }
}
