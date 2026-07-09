namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public abstract partial record BattlePresenterLogicState
{
    public record SkillSelectingAction
        : ShowingCommandMenu,
            IGet<Input.SkillActionSelected>,
            IGet<Input.Confirmed>,
            IGet<Input.AttackSelected>,
            IGet<Input.BackRequested>
    {
        public Type On(in Input.SkillActionSelected input)
        {
            if (Data.Prompt is null || Data.Prompt.Skills.Count == 0)
            {
                return ToSelf();
            }

            Data.SelectedSkillIndex = Math.Clamp(
                input.Index,
                0,
                Data.Prompt.Skills.Count - 1
            );
            OutputSkillActions();
            return ToSelf();
        }

        public Type On(in Input.Confirmed input) => OpenSkillTargets();

        public Type On(in Input.AttackSelected input) => OpenAttackTargets();

        public Type On(in Input.BackRequested input)
        {
            OutputCommandMenu();
            return To<CommandMenu>();
        }
    }
}
