namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public partial record BattleLogicState
{
    public record Disabled :
        BattleLogicState,
        IGet<Input.StartBattle>
    {
        public Type On(in Input.StartBattle input) => Start(input.Setup);
    }
}
