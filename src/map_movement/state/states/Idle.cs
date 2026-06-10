namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;
using Godot;

public partial record MapMovementLogicState
{
    public record Idle
        : MapMovementLogicState,
            IGet<Input.MoveRequested>,
            IGet<Input.RelativeMoveRequested>,
            IGet<Input.TurnRequested>,
            IGet<Input.Disable>
    {
        public Type On(in Input.MoveRequested input) =>
            TryStartMove(input.Direction);

        public Type On(in Input.RelativeMoveRequested input)
        {
            var data = Get<MapMovementLogic.Data>();
            var mapRepo = Get<IMapRepo>();

            if (!mapRepo.TryGetEntityPose(data.EntityId, out var pose))
            {
                return To<Idle>();
            }

            return TryStartMovePreservingFacing(
                ResolveRelativeDirection(pose.FacingDirection, input.Direction)
            );
        }

        public Type On(in Input.Disable input) => To<Disabled>();

        public Type On(in Input.TurnRequested input)
        {
            var data = Get<MapMovementLogic.Data>();

            if (Get<IMapRepo>().TryTurnEntity(
                    data.EntityId,
                    input.Direction,
                    out var pose
                ))
            {
                Output(new Output.TurnStarted(pose.FacingDirection));
                return To<Turning>();
            }

            return To<Idle>();
        }

        private Type TryStartMove(Vector2I direction)
        {
            var data = Get<MapMovementLogic.Data>();

            if (!Get<IMapRepo>().TryMoveEntity(
                    data.EntityId,
                    direction,
                    out var move
                ))
            {
                Output(new Output.MoveBlocked(direction));
                return To<Idle>();
            }

            Output(new Output.MoveStarted(move));
            return To<Moving>();
        }

        private Type TryStartMovePreservingFacing(Vector2I direction)
        {
            var data = Get<MapMovementLogic.Data>();

            if (!Get<IMapRepo>().TryMoveEntityPreservingFacing(
                    data.EntityId,
                    direction,
                    out var move
                ))
            {
                Output(new Output.MoveBlocked(direction));
                return To<Idle>();
            }

            Output(new Output.MoveStarted(move));
            return To<Moving>();
        }

        private static Vector2I ResolveRelativeDirection(
            Vector2I facingDirection,
            RelativeMoveDirection moveDirection
        ) =>
            moveDirection switch
            {
                RelativeMoveDirection.Forward => facingDirection,
                RelativeMoveDirection.Backward =>
                    GridDirection.Opposite(facingDirection),
                RelativeMoveDirection.Left =>
                    GridDirection.Left(facingDirection),
                RelativeMoveDirection.Right =>
                    GridDirection.Right(facingDirection),
                _ => facingDirection,
            };
    }
}
