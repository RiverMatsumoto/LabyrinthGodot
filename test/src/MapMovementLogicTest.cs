namespace Labyrinth;

using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

public class MapMovementLogicTest : TestClass
{
    public MapMovementLogicTest(Node testScene) : base(testScene) { }

    [Test]
    public void AcceptedMoveStartsAndArrivalFinishesMove()
    {
        var logic = new MapMovementLogic();
        logic.Start<MapMovementLogicState.Idle>();

        var move = new GridMove(
            new MapEntityId("player"),
            new Vector2I(1, 1),
            new Vector2I(2, 1),
            new Vector2I(1, 0)
        );

        logic.Input(new MapMovementLogicState.Input.MoveAccepted(move));
        logic.State.ShouldBeOfType<MapMovementLogicState.Moving>();

        logic.Input(new MapMovementLogicState.Input.Arrived());
        logic.State.ShouldBeOfType<MapMovementLogicState.Idle>();
    }

    [Test]
    public void BlockedMoveStaysIdle()
    {
        var logic = new MapMovementLogic();
        logic.Start<MapMovementLogicState.Idle>();

        logic.Input(
            new MapMovementLogicState.Input.MoveBlocked(new Vector2I(0, -1))
        );
        logic.State.ShouldBeOfType<MapMovementLogicState.Idle>();

        logic.Input(
            new MapMovementLogicState.Input.MoveAccepted(
                new GridMove(
                    new MapEntityId("player"),
                    new Vector2I(1, 1),
                    new Vector2I(1, 0),
                    new Vector2I(0, -1)
                )
            )
        );
        logic.State.ShouldBeOfType<MapMovementLogicState.Moving>();
    }

    [Test]
    public void MoveInputWhileMovingIsIgnored()
    {
        var logic = new MapMovementLogic();
        logic.Start<MapMovementLogicState.Idle>();

        var firstMove = new GridMove(
            new MapEntityId("player"),
            new Vector2I(1, 1),
            new Vector2I(2, 1),
            new Vector2I(1, 0)
        );
        var secondMove = new GridMove(
            new MapEntityId("player"),
            new Vector2I(2, 1),
            new Vector2I(3, 1),
            new Vector2I(1, 0)
        );

        logic.Input(new MapMovementLogicState.Input.MoveAccepted(firstMove));
        logic.State.ShouldBeOfType<MapMovementLogicState.Moving>();

        logic.Input(new MapMovementLogicState.Input.MoveAccepted(secondMove));
        logic.Input(
            new MapMovementLogicState.Input.MoveBlocked(new Vector2I(-1, 0))
        );
        logic.State.ShouldBeOfType<MapMovementLogicState.Moving>();
    }
}
