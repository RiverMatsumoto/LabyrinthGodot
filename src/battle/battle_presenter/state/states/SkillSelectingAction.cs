namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public abstract partial record BattlePresenterLogicState
{
    public record SkillSelectingAction
        : ShowingCommandMenu,
            IGet<Input.SkillActionSelected>,
            IGet<Input.Confirmed>,
            IGet<Input.AttackSelected>
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
            Output(new Output.RenderSkillActions(
                Data.Prompt,
                Data.SelectedSkillIndex
            ));
            return ToSelf();
        }

        public Type On(in Input.Confirmed input)
        {
            if (Data.Prompt is null || Data.Prompt.Skills.Count == 0)
            {
                return ToSelf();
            }

            var skill = Data.Prompt.Skills[
                Math.Clamp(
                    Data.SelectedSkillIndex,
                    0,
                    Data.Prompt.Skills.Count - 1
                )
            ];
            Data.SelectedTargetIndex = 0;
            OutputTargets(skill);
            return To<SkillSelectingTarget>();
        }

        public Type On(in Input.AttackSelected input)
        {
            if (Data.Prompt?.Attack is not { } attack)
            {
                return ToSelf();
            }

            Data.SelectedTargetIndex = 0;
            OutputTargets(attack);
            return To<AttackSelectingTarget>();
        }
    }
}
