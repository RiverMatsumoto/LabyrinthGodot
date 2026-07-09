namespace Labyrinth;

using System;
using System.Collections.Generic;
using System.Linq;
using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;

[Meta, StateDiagram]
public abstract partial record BattlePresenterLogicState
    : LogicBlockState,
        IGet<BattlePresenterLogicState.Input.ScreenUpdated>,
        IGet<BattlePresenterLogicState.Input.CueBatchRequested>,
        IGet<BattlePresenterLogicState.Input.Cancelled>
{
    protected BattlePresenterLogic.Data Data =>
        Get<BattlePresenterLogic.Data>();

    public Type On(in Input.ScreenUpdated input)
    {
        Data.Screen = input.View;
        Output(new Output.RenderBattle(input.View));
        return ToSelf();
    }

    public Type On(in Input.CueBatchRequested input) =>
        StartCueBatch(input.View, input.Cues);

    public Type On(in Input.Cancelled input) => Hide();

    protected Type ShowPrompt(
        BattleScreenView view,
        BattleCommandPrompt prompt
    )
    {
        Data.Screen = view;
        Data.Prompt = prompt;
        Data.SelectedAction = null;
        Data.SelectedSkillIndex = 0;
        Data.SelectedTargetIndex = 0;
        Data.SelectedMenuIndex = PreferredMenuIndex(prompt);
        Output(new Output.RenderBattle(view));
        OutputCommandMenu();
        return To<CommandMenu>();
    }

    protected Type StartCueBatch(
        BattleScreenView view,
        IReadOnlyList<BattleCue> cues
    )
    {
        Data.Screen = view;
        Data.CueBatch = cues;
        Data.CueIndex = 0;
        Output(new Output.RenderBattle(view));
        Output(new Output.ClearCueVisual());
        if (cues.Count == 0)
        {
            Output(new Output.CueBatchFinished());
            return To<Hidden>();
        }
        Output(new Output.BeginCueVisual(cues[0]));
        return To<PlayingCueBatch>();
    }

    protected Type Hide()
    {
        Data.Prompt = null;
        Data.SelectedAction = null;
        Data.SelectedMenuIndex = 0;
        Data.SelectedSkillIndex = 0;
        Data.SelectedTargetIndex = 0;
        Data.CueBatch = [];
        Data.CueIndex = 0;
        Data.CommandMemories.Clear();
        Output(new Output.ClearCueVisual());
        Output(new Output.Hide());
        return To<Hidden>();
    }

    protected void OutputCommandMenu()
    {
        if (Data.Prompt is not { } prompt)
        {
            return;
        }

        var options = MenuOptions(prompt);
        Data.SelectedMenuIndex = Math.Clamp(
            Data.SelectedMenuIndex,
            0,
            Math.Max(0, options.Count - 1)
        );
        Output(new Output.RenderCommandMenu(
            prompt,
            options,
            Data.SelectedMenuIndex
        ));
    }

    protected void OutputSkillActions()
    {
        if (Data.Prompt is { } prompt)
        {
            Output(new Output.RenderSkillActions(
                prompt,
                Data.SelectedSkillIndex
            ));
        }
    }

    protected void OutputTargets(BattleActionOption option)
    {
        Data.SelectedAction = option;
        Data.SelectedTargetIndex = Math.Clamp(
            Data.SelectedTargetIndex,
            0,
            Math.Max(0, option.TargetOptions.Count - 1)
        );
        Output(new Output.RenderTargets(option, Data.SelectedTargetIndex));
    }

    protected Type OpenSkillActions()
    {
        if (Data.Prompt is not { } prompt || prompt.Skills.Count == 0)
        {
            return ToSelf();
        }

        Data.SelectedSkillIndex = PreferredSkillIndex(prompt);
        OutputSkillActions();
        return To<SkillSelectingAction>();
    }

    protected Type OpenAttackTargets()
    {
        if (Data.Prompt?.Attack is not { IsEnabled: true } attack)
        {
            return ToSelf();
        }

        Data.SelectedTargetIndex = PreferredTargetIndex(attack);
        OutputTargets(attack);
        return To<AttackSelectingTarget>();
    }

    protected Type OpenSkillTargets()
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
        if (!skill.IsEnabled)
        {
            return ToSelf();
        }

        Data.SelectedTargetIndex = PreferredTargetIndex(skill);
        OutputTargets(skill);
        return To<SkillSelectingTarget>();
    }

    protected void SelectTargetIndex(int index)
    {
        if (Data.SelectedAction is not { } action)
        {
            return;
        }

        Data.SelectedTargetIndex = Math.Clamp(
            index,
            0,
            Math.Max(0, action.TargetOptions.Count - 1)
        );
        OutputTargets(action);
    }

    protected void SelectTargetAnchor(BattlerId anchorId)
    {
        if (Data.SelectedAction is not { } action)
        {
            return;
        }

        for (var index = 0; index < action.TargetOptions.Count; index++)
        {
            var option = action.TargetOptions[index];
            if (
                option.AnchorIds.Contains(anchorId)
                || option.AffectedIds.Contains(anchorId)
            )
            {
                SelectTargetIndex(index);
                return;
            }
        }
    }

    protected void MoveTarget(int rowDelta, int slotDelta)
    {
        if (Data.SelectedAction is not { } action)
        {
            return;
        }

        var options = action.TargetOptions;
        if (options.Count <= 1)
        {
            return;
        }

        var current = options[
            Math.Clamp(Data.SelectedTargetIndex, 0, options.Count - 1)
        ];
        var currentVisualRow = VisualRow(current.GridPoint);
        var candidates = options
            .Select((option, index) => (option, index))
            .Where(candidate => candidate.index != Data.SelectedTargetIndex)
            .Select(candidate => (
                candidate.index,
                row: VisualRow(candidate.option.GridPoint),
                slot: candidate.option.GridPoint.Slot,
                score: MovementScore(
                    currentVisualRow,
                    current.GridPoint.Slot,
                    VisualRow(candidate.option.GridPoint),
                    candidate.option.GridPoint.Slot,
                    rowDelta,
                    slotDelta
                )
            ))
            .Where(candidate => candidate.score >= 0)
            .OrderBy(candidate => candidate.score)
            .ThenBy(candidate => candidate.row)
            .ThenBy(candidate => candidate.slot)
            .ToArray();

        if (candidates.Length > 0)
        {
            SelectTargetIndex(candidates[0].index);
        }
    }

    protected void SubmitSelectedAction()
    {
        if (
            Data.Prompt is null
            || Data.SelectedAction is not { IsEnabled: true } action
            || action.TargetOptions.Count == 0
        )
        {
            return;
        }

        var target = action.TargetOptions[
            Math.Clamp(
                Data.SelectedTargetIndex,
                0,
                action.TargetOptions.Count - 1
            )
        ];
        var targetAnchorId = target.AnchorIds.Count > 0
            ? target.AnchorIds[0]
            : target.CommandTargetId;
        Data.CommandMemories[Data.Prompt.ActorId] =
            new BattlePresenterCommandMemory(action.ActionId, targetAnchorId);
        Data.SelectedAction = null;
        Output(new Output.CommandSubmitted(new BattleCommand(
            Data.Prompt.ActorId,
            action.ActionId,
            target.CommandTargetId
        )));
    }

    protected IReadOnlyList<BattleCommandMenuOption> MenuOptions(
        BattleCommandPrompt prompt
    )
    {
        var attack = prompt.Attack;
        return
        [
            new(
                BattleCommandMenuAction.Attack,
                "ATTACK",
                attack?.IsEnabled == true,
                attack?.DisabledReason ?? "No attack."
            ),
            new(
                BattleCommandMenuAction.Skill,
                "SKILL",
                prompt.Skills.Any(skill => skill.IsEnabled),
                prompt.Skills.Count == 0
                    ? "No skills."
                    : "No usable skills."
            ),
            new(
                BattleCommandMenuAction.Item,
                "ITEM",
                prompt.CanUseItem,
                "Items unavailable."
            ),
            new(
                BattleCommandMenuAction.Defend,
                "DEFEND",
                prompt.CanDefend,
                "Defend unavailable."
            ),
            new(
                BattleCommandMenuAction.Move,
                "MOVE",
                prompt.CanMove,
                "Move unavailable."
            ),
            new(BattleCommandMenuAction.Escape, "ESCAPE", true),
        ];
    }

    protected BattleCommandMenuOption SelectedMenuOption()
    {
        if (Data.Prompt is not { } prompt)
        {
            return new BattleCommandMenuOption(
                BattleCommandMenuAction.Attack,
                "ATTACK",
                false
            );
        }

        var options = MenuOptions(prompt);
        return options[Math.Clamp(
            Data.SelectedMenuIndex,
            0,
            options.Count - 1
        )];
    }

    private int PreferredMenuIndex(BattleCommandPrompt prompt)
    {
        var options = MenuOptions(prompt);
        if (TryGetCommandMemory(prompt, out var memory))
        {
            if (
                prompt.Attack is { IsEnabled: true } attack
                && attack.ActionId == memory.ActionId
            )
            {
                return options.FindIndex(option =>
                    option.Action == BattleCommandMenuAction.Attack
                    && option.IsEnabled);
            }

            if (prompt.Skills.Any(skill =>
                skill.ActionId == memory.ActionId && skill.IsEnabled))
            {
                return options.FindIndex(option =>
                    option.Action == BattleCommandMenuAction.Skill
                    && option.IsEnabled);
            }
        }

        var firstEnabled = options.FindIndex(option => option.IsEnabled);
        return Math.Max(0, firstEnabled);
    }

    private int PreferredSkillIndex(BattleCommandPrompt prompt)
    {
        if (prompt.Skills.Count == 0)
        {
            return 0;
        }

        if (TryGetCommandMemory(prompt, out var memory))
        {
            var remembered = prompt.Skills.FindIndex(skill =>
                skill.ActionId == memory.ActionId && skill.IsEnabled);
            if (remembered >= 0)
            {
                return remembered;
            }
        }

        var firstEnabled = prompt.Skills.FindIndex(skill => skill.IsEnabled);
        return Math.Max(0, firstEnabled);
    }

    private int PreferredTargetIndex(BattleActionOption action)
    {
        if (action.TargetOptions.Count == 0)
        {
            return 0;
        }

        if (
            Data.Prompt is { } prompt
            && TryGetCommandMemory(prompt, out var memory)
            && memory.ActionId == action.ActionId
            && memory.TargetAnchorId is { } id
        )
        {
            var remembered = action.TargetOptions.FindIndex(option =>
                option.AnchorIds.Contains(id)
                || option.AffectedIds.Contains(id)
                || option.CommandTargetId == id
            );
            if (remembered >= 0)
            {
                return remembered;
            }
        }

        return 0;
    }

    private bool TryGetCommandMemory(
        BattleCommandPrompt prompt,
        out BattlePresenterCommandMemory memory
    ) => Data.CommandMemories.TryGetValue(prompt.ActorId, out memory);

    private static int VisualRow(BattleTargetGridPoint point) =>
        point.Team switch
        {
            BattleTeam.Player => point.Row == PartyRow.Front ? 0 : 1,
            BattleTeam.Enemy => point.Row == PartyRow.Back ? 0 : 1,
            _ => 0,
        };

    private static int MovementScore(
        int currentRow,
        int currentSlot,
        int row,
        int slot,
        int rowDelta,
        int slotDelta
    )
    {
        if (slotDelta < 0 && slot >= currentSlot)
        {
            return -1;
        }
        if (slotDelta > 0 && slot <= currentSlot)
        {
            return -1;
        }
        if (rowDelta < 0 && row >= currentRow)
        {
            return -1;
        }
        if (rowDelta > 0 && row <= currentRow)
        {
            return -1;
        }

        var rowDistance = Math.Abs(row - currentRow);
        var slotDistance = Math.Abs(slot - currentSlot);
        return rowDelta != 0
            ? rowDistance * 100 + slotDistance
            : slotDistance * 100 + rowDistance;
    }
}

internal static class BattlePresenterListExtensions
{
    public static int FindIndex<T>(
        this IReadOnlyList<T> items,
        Func<T, bool> predicate
    )
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (predicate(items[index]))
            {
                return index;
            }
        }

        return -1;
    }
}
