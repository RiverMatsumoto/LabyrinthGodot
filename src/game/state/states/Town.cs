namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public partial record GameLogicState
{
    public record Town
        : GameLogicState,
            IGet<Input.EnterMainMenu>,
            IGet<Input.EnterLabyrinth>,
            IGet<Input.EnterBattle>
    {
        public Town()
        {
            this.OnEnter(() =>
            {
                Output(new Output.EnteredTown());
            });
        }

        public Type On(in Input.EnterMainMenu input) => To<MainMenu>();

        public Type On(in Input.EnterLabyrinth input) => To<Labyrinth>();

        public Type On(in Input.EnterBattle input)
        {
            Get<IGameRepo>().SetBattleRequest(new BattleRequest(
                input.EncounterId.IsEmpty
                    ? new EncounterId("debug")
                    : input.EncounterId,
                input.Seed,
                input.ReturnMode
            ));
            return To<Battle>();
        }
    }
}
