namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public partial record BattleLogicState
{
    public record SelectingCommands :
        BattleLogicState,
        IGet<Input.SubmitCommand>,
        IGet<Input.UndoCommand>,
        IGet<Input.Flee>
    {
        public Type On(in Input.SubmitCommand input)
        {
            var validation = BattleRepo.ValidateCommand(input.Command);
            if (!validation.IsValid)
            {
                Output(new Output.CommandRejected(validation.Error));
                return ToSelf();
            }

            BattleRepo.SubmitCommand(input.Command);
            if (BattleRepo.RequestedPlayerId is { })
            {
                OutputCommandRequest();
                return ToSelf();
            }

            BattleRepo.BeginResolution(EnemyCommandPlanner);
            return To<ResolvingTurn>();
        }

        public Type On(in Input.UndoCommand input)
        {
            if (BattleRepo.UndoLastCommand())
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
