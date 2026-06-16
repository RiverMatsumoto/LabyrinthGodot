namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public abstract partial record BattlePresenterLogicState
{
    public record AttackSelectingTarget
        : ShowingCommandMenu,
            IGet<Input.TargetSelected>,
            IGet<Input.Confirmed>,
            IGet<Input.SkillSelected>
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
            if (Data.Prompt is not { } prompt || prompt.Skills.Count == 0)
            {
                return ToSelf();
            }

            Data.SelectedSkillIndex = 0;
            Output(new Output.RenderSkillActions(prompt, 0));
            return To<SkillSelectingAction>();
        }
    }
}
