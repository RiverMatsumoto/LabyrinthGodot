namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public abstract partial record BattlePresenterLogicState
{
    public record CommandMenu
        : ShowingCommandMenu,
            IGet<Input.AttackSelected>,
            IGet<Input.SkillSelected>
    {
        public Type On(in Input.AttackSelected input)
        {
            if (Data.Prompt?.Attack is not { } attack)
            {
                return ToSelf();
            }

            OutputTargets(attack);
            return To<AttackSelectingTarget>();
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
