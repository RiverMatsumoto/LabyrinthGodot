namespace Labyrinth;

using System;
using Chickensoft.LogicBlocks;

public abstract partial record BattlePresenterLogicState
{
    public record CommandMenu
        : ShowingCommandMenu,
            IGet<Input.MenuActionSelected>,
            IGet<Input.AttackSelected>,
            IGet<Input.SkillSelected>,
            IGet<Input.Confirmed>,
            IGet<Input.BackRequested>
    {
        public Type On(in Input.MenuActionSelected input)
        {
            if (Data.Prompt is null)
            {
                return ToSelf();
            }

            Data.SelectedMenuIndex = input.Index;
            OutputCommandMenu();
            return ToSelf();
        }

        public Type On(in Input.AttackSelected input) => OpenAttackTargets();

        public Type On(in Input.SkillSelected input) => OpenSkillActions();

        public Type On(in Input.Confirmed input)
        {
            var selected = SelectedMenuOption();
            if (!selected.IsEnabled)
            {
                return ToSelf();
            }

            return selected.Action switch
            {
                BattleCommandMenuAction.Attack => OpenAttackTargets(),
                BattleCommandMenuAction.Skill => OpenSkillActions(),
                BattleCommandMenuAction.Escape => RequestEscape(),
                _ => ToSelf(),
            };
        }

        public Type On(in Input.BackRequested input)
        {
            Output(new Output.UndoRequested());
            return ToSelf();
        }
    }
}
