namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public partial record BattleLogicState
{
    public record Completed :
        BattleLogicState,
        IGet<Input.StartRequestedBattle>
    {
        public Type On(in Input.StartRequestedBattle input) => Start();
    }
}
