namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public abstract partial record BattlePresenterLogicState
{
    public record SkillSelectingTarget
        : ShowingCommandMenu,
            IGet<Input.TargetSelected>,
            IGet<Input.Confirmed>,
            IGet<Input.SkillSelected>,
            IGet<Input.AttackSelected>
    {
        public Type On(in Input.TargetSelected input)
        {
            Data.SelectedTargetIndex = input.Index;
            if (Data.SelectedAction is { } action)
            {
                OutputTargets(action);
            }
            return ToSelf();
        }

        public Type On(in Input.Confirmed input)
        {
            SubmitSelectedAction();
            return ToSelf();
        }

        public Type On(in Input.SkillSelected input)
        {
            if (Data.Prompt is null || Data.Prompt.Skills.Count == 0)
            {
                return ToSelf();
            }

            Output(new Output.RenderSkillActions(
                Data.Prompt,
                Data.SelectedSkillIndex
            ));
            return To<SkillSelectingAction>();
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
