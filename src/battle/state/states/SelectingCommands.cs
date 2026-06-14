namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public partial record BattleLogicState
{
    public record SelectingCommands :
        BattleLogicState,
        IGet<Input.SubmitIntent>,
        IGet<Input.UndoIntent>,
        IGet<Input.Flee>
    {
        public Type On(in Input.SubmitIntent input)
        {
            var validation = BattleRepo.ValidateIntent(input.Intent);
            if (!validation.IsValid)
            {
                Output(new Output.CommandRejected(validation.Error));
                return ToSelf();
            }

            BattleRepo.SubmitIntent(input.Intent);
            if (BattleRepo.RequestedPlayerId is { })
            {
                OutputCommandRequest();
                return ToSelf();
            }

            BattleRepo.BeginResolution(EnemyIntentProvider);
            return To<ResolvingTurn>();
        }

        public Type On(in Input.UndoIntent input)
        {
            if (BattleRepo.UndoLastIntent())
            {
                OutputCommandRequest();
                if (BattleRepo.RequestedPlayerId is { } id)
                {
                    Output(new Output.CommandUndone(id));
                }
            }
            return ToSelf();
        }

        public Type On(in Input.Flee input) =>
            Complete(BattleRepo.Flee().Result!);
    }
}
