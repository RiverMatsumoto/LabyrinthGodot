namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public partial record GameLogicState
{
    public record MainMenu
        : GameLogicState,
            IGet<Input.EnterTown>,
            IGet<Input.EnterLabyrinth>,
            IGet<Input.EnterBattle>
    {
        public MainMenu()
        {
            this.OnEnter(() =>
            {
                Output(new Output.EnteredMainMenu());
            });
        }

        public Type On(in Input.EnterTown input) => To<Town>();

        public Type On(in Input.EnterLabyrinth input) => To<Labyrinth>();

        public Type On(in Input.EnterBattle input)
        {
            Get<IGameRepo>().SetBattleRequest(new BattleRequest(
                input.EncounterId.IsEmpty
                    ? BattleContent.DefaultEncounterId
                    : input.EncounterId,
                input.Seed,
                input.ReturnMode
            ));
            return To<Battle>();
        }
    }
}
