namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public partial record GameLogicState
{
    public record Labyrinth
        : GameLogicState,
            IGet<Input.EnterMainMenu>,
            IGet<Input.EnterTown>,
            IGet<Input.EnterBattle>
    {
        public Labyrinth()
        {
            this.OnEnter(() =>
            {
                Output(new Output.EnteredLabyrinth());
                Output(new Output.MaxFpsRequested(0));
            });
        }

        public Type On(in Input.EnterMainMenu input) => To<MainMenu>();

        public Type On(in Input.EnterTown input) => To<Town>();

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
