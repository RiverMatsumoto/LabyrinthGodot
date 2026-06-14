namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;
using Godot;

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
                Engine.MaxFps = 0;
            });
        }

        public Type On(in Input.EnterMainMenu input) => To<MainMenu>();

        public Type On(in Input.EnterTown input) => To<Town>();

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
