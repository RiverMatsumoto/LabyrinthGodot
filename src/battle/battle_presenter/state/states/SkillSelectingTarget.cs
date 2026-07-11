namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public abstract partial record BattlePresenterLogicState
{
    public record SkillSelectingTarget
        : ShowingCommandMenu,
            IGet<Input.TargetSelected>,
            IGet<Input.TargetAnchorSelected>,
            IGet<Input.TargetMoved>,
            IGet<Input.Confirmed>,
            IGet<Input.SkillSelected>,
            IGet<Input.AttackSelected>,
            IGet<Input.BackRequested>
    {
        public Type On(in Input.TargetSelected input)
        {
            SelectTargetIndex(input.Index);
            return ToSelf();
        }

        public Type On(in Input.TargetAnchorSelected input)
        {
            SelectTargetAnchor(input.AnchorId);
            return ToSelf();
        }

        public Type On(in Input.TargetMoved input)
        {
            MoveTarget(input.RowDelta, input.SlotDelta);
            return ToSelf();
        }

        public Type On(in Input.Confirmed input)
        {
            SubmitSelectedAction();
            return To<CommandMenu>();
        }

        public Type On(in Input.SkillSelected input)
        {
            OutputSkillActions();
            return To<SkillSelectingAction>();
        }

        public Type On(in Input.AttackSelected input) => OpenAttackTargets();

        public Type On(in Input.BackRequested input)
        {
            Data.SelectedAction = null;
            OutputSkillActions();
            return To<SkillSelectingAction>();
        }
    }
}
