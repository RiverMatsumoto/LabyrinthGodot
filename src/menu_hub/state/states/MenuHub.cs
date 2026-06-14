namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public partial record MenuHubLogicState
{
    public record MenuHub : MenuHubLogicState,
        IGet<Input.OpenItemMenu>,
        IGet<Input.OpenSkillMenu>,
        IGet<Input.OpenStatusMenu>,
        IGet<Input.OpenEquipMenu>,
        IGet<Input.OpenCustomMenu>,
        IGet<Input.OpenPartyMenu>,
        IGet<Input.OpenQuestMenu>,
        IGet<Input.OpenSettings>,
        IGet<Input.Close>,
        IGet<Input.HandleMenuInput>
    {
        public MenuHub()
        {
            this.OnEnter(() =>
            {
                Output(new Output.OpenedMenuHub());
                Get<IGameRepo>().SetIsInMenu(true);
            });
        }

        public Type On(in Input.OpenItemMenu input) => To<ItemMenu>();

        public Type On(in Input.OpenSkillMenu input) => To<SkillMenu>();

        public Type On(in Input.OpenStatusMenu input) => To<StatusMenu>();

        public Type On(in Input.OpenEquipMenu input) => To<EquipMenu>();

        public Type On(in Input.OpenCustomMenu input) => To<CustomMenu>();

        public Type On(in Input.OpenPartyMenu input) => To<PartyMenu>();

        public Type On(in Input.OpenQuestMenu input) => To<QuestMenu>();

        public Type On(in Input.OpenSettings input) => To<Settings>();

        public Type On(in Input.Close input) => To<Disabled>();

        public Type On(in Input.HandleMenuInput input) => To<Disabled>();
    }
}
