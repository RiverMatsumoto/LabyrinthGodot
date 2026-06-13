namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public partial record MapLogicState
{
    public record Idle : MapLogicState,
        IGet<Input.RegisterEntity>,
        IGet<Input.UnregisterEntity>,
        IGet<Input.EntityRegistered>,
        IGet<Input.EntityUnregistered>
    {
        public Type On(in Input.RegisterEntity input)
        {
            Get<MapLogic.Data>().LastCommandSucceeded =
                Get<IMapRepo>().TryRegisterEntity(
                    input.Id,
                    input.Pose.Position,
                    input.Pose.FacingDirection
                );
            return ToSelf();
        }

        public Type On(in Input.UnregisterEntity input)
        {
            Get<MapLogic.Data>().LastCommandSucceeded =
                Get<IMapRepo>().TryUnregisterEntity(input.Id);
            return ToSelf();
        }

        public Type On(in Input.EntityRegistered input)
        {
            if (input.Id == MapRepo.PlayerId)
            {
                Output(new Output.SpawnPlayer(input.Id, input.Pose));
                return ToSelf();
            }

            Output(new Output.SpawnEnemy(input.Id, input.Pose));
            return ToSelf();
        }

        public Type On(in Input.EntityUnregistered input)
        {
            Output(new Output.DespawnEntity(input.Id));
            return ToSelf();
        }
    }
}
