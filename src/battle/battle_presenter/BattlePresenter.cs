namespace Labyrinth;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Chickensoft.AutoInject;
using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;
using Godot;

public interface IBattlePresenter
{
    event Action<BattleCommand>? CommandSubmitted;
    event Action? UndoRequested;
    event Action? EscapeRequested;

    bool IsPlaying { get; }
    double BaseSpeed { get; set; }
    double EffectiveSpeed { get; }
    void ShowCommandPrompt(
        BattleScreenView view,
        BattleCommandPrompt prompt
    );
    void Render(BattleScreenView view);
    void ShowError(string error);
    void PlayCueBatch(
        BattleScreenView view,
        IReadOnlyList<BattleCue> cues,
        Action finished
    );
    void Cancel();
}

[Meta(typeof(IAutoNode))]
public partial class BattlePresenter : Control, IBattlePresenter
{
    public event Action<BattleCommand>? CommandSubmitted;
    public event Action? UndoRequested;
    public event Action? EscapeRequested;

    public override void _Notification(int what) => this.Notify(what);

    [Export(PropertyHint.Range, "0.25,4,0.1")]
    public double BaseSpeed { get; set; } = 1;

    [Node] public Label Turn { get; set; } = default!;
    [Node] public VBoxContainer PartySlots { get; set; } = default!;
    [Node] public HBoxContainer FrontRow { get; set; } = default!;
    [Node] public HBoxContainer BackRow { get; set; } = default!;
    [Node] public Control EnemySlots { get; set; } = default!;
    [Node] public ItemList ActionMenu { get; set; } = default!;
    [Node] public ItemList SkillActions { get; set; } = default!;
    [Node] public Label Message { get; set; } = default!;

    public bool IsPlaying => _finished is not null;
    public double EffectiveSpeed => CalculateEffectiveSpeed(
        BaseSpeed,
        Input.IsActionPressed(GameInputs.UiAccept)
    );

    private readonly BattlePresenterLogic _logic = new();
    private readonly Dictionary<BattleTargetGridPoint, UnitSlot> _slots = [];
    private BattlePresenterLogic.Binding? _binding;
    private Action? _finished;
    private double _elapsed;
    private double _duration;
    private bool _ignoreTargetAcceptUntilRelease;

    public void OnResolved()
    {
        BuildSlotLookup();

        ActionMenu.ItemSelected += OnActionMenuSelected;
        ActionMenu.ItemActivated += OnActionMenuActivated;
        ActionMenu.GuiInput += OnActionMenuGuiInput;
        SkillActions.ItemSelected += OnSkillActionSelected;
        SkillActions.ItemActivated += OnSkillActionActivated;
        SkillActions.GuiInput += OnSkillActionsGuiInput;

        _binding = _logic.Bind()
            .OnOutput((
                in BattlePresenterLogicState.Output.RenderBattle output
            ) => RenderBattle(output.View))
            .OnOutput((
                in BattlePresenterLogicState.Output.RenderCommandMenu output
            ) => RenderCommandMenu(
                output.Prompt,
                output.Options,
                output.SelectedIndex
            ))
            .OnOutput((
                in BattlePresenterLogicState.Output.RenderSkillActions output
            ) => RenderSkillActions(output.Prompt, output.SelectedIndex))
            .OnOutput((
                in BattlePresenterLogicState.Output.RenderTargets output
            ) => RenderTargets(output.Option, output.SelectedIndex))
            .OnOutput((
                in BattlePresenterLogicState.Output.BeginCueVisual output
            ) => BeginCueVisual(output.Cue))
            .OnOutput((
                in BattlePresenterLogicState.Output.ClearCueVisual _
            ) => ClearCueVisual())
            .OnOutput((
                in BattlePresenterLogicState.Output.CommandSubmitted output
            ) => CommandSubmitted?.Invoke(output.Command))
            .OnOutput((
                in BattlePresenterLogicState.Output.UndoRequested _
            ) => UndoRequested?.Invoke())
            .OnOutput((
                in BattlePresenterLogicState.Output.EscapeRequested _
            ) => EscapeRequested?.Invoke())
            .OnOutput((
                in BattlePresenterLogicState.Output.CueBatchFinished _
            ) => FinishCueBatch())
            .OnOutput((
                in BattlePresenterLogicState.Output.Hide _
            ) => HideUi())
            .OnExit<BattlePresenterLogicState.Hidden>(_ => ShowUi())
            .OnEnter<BattlePresenterLogicState.Hidden>(_ => HideUi());

        _logic.Start<BattlePresenterLogicState.Hidden>();
        SetProcess(false);
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible || !IsSelectingTarget)
        {
            return;
        }

        if (@event is InputEventMouseMotion)
        {
            TrySelectTargetAtMouse();
            return;
        }

        if (
            @event is InputEventMouseButton
            {
                ButtonIndex: MouseButton.Left,
                Pressed: true,
            }
            && TrySelectTargetAtMouse()
        )
        {
            _logic.Confirm();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible)
        {
            return;
        }

        if (@event.IsActionPressed(GameInputs.UiCancel))
        {
            _logic.RequestUndo();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_ignoreTargetAcceptUntilRelease)
        {
            if (@event.IsActionReleased(GameInputs.UiAccept))
            {
                _ignoreTargetAcceptUntilRelease = false;
                GetViewport().SetInputAsHandled();
                return;
            }

            if (@event.IsActionPressed(GameInputs.UiAccept))
            {
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        if (!IsSelectingTarget)
        {
            return;
        }

        if (@event.IsActionPressed(GameInputs.UiAccept))
        {
            _logic.Confirm();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (@event.IsActionPressed(GameInputs.MoveForward))
        {
            _logic.MoveTarget(-1, 0);
            GetViewport().SetInputAsHandled();
            return;
        }
        if (@event.IsActionPressed(GameInputs.MoveBackward))
        {
            _logic.MoveTarget(1, 0);
            GetViewport().SetInputAsHandled();
            return;
        }
        if (
            @event.IsActionPressed(GameInputs.TurnLeft)
            || @event.IsActionPressed(GameInputs.MoveLeft)
        )
        {
            _logic.MoveTarget(0, -1);
            GetViewport().SetInputAsHandled();
            return;
        }
        if (
            @event.IsActionPressed(GameInputs.TurnRight)
            || @event.IsActionPressed(GameInputs.MoveRight)
        )
        {
            _logic.MoveTarget(0, 1);
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        _elapsed += delta * EffectiveSpeed;
        if (_elapsed < _duration)
        {
            return;
        }

        SetProcess(false);
        _logic.CueVisualFinished();
    }

    public void ShowCommandPrompt(
        BattleScreenView view,
        BattleCommandPrompt prompt
    )
    {
        _finished = null;
        ShowUi();
        _logic.ShowCommandPrompt(view, prompt);
    }

    public void Render(BattleScreenView view)
    {
        ShowUi();
        _logic.UpdateScreen(view);
    }

    public void ShowError(string error)
    {
        Message.Text = error;
    }

    public void PlayCueBatch(
        BattleScreenView view,
        IReadOnlyList<BattleCue> cues,
        Action finished
    )
    {
        ArgumentNullException.ThrowIfNull(cues);
        ArgumentNullException.ThrowIfNull(finished);
        _finished = finished;
        ShowUi();
        _logic.PlayCueBatch(view, cues);
    }

    public void Cancel()
    {
        _finished = null;
        ClearCueVisual();
        HideUi();
        _logic.Cancel();
    }

    public void OnExitTree()
    {
        ActionMenu.ItemSelected -= OnActionMenuSelected;
        ActionMenu.ItemActivated -= OnActionMenuActivated;
        ActionMenu.GuiInput -= OnActionMenuGuiInput;
        SkillActions.ItemSelected -= OnSkillActionSelected;
        SkillActions.ItemActivated -= OnSkillActionActivated;
        SkillActions.GuiInput -= OnSkillActionsGuiInput;
        Cancel();
        _binding?.Dispose();
        _logic.Dispose();
    }

    public static double CalculateEffectiveSpeed(
        double baseSpeed,
        bool fastForward
    ) => Math.Max(0.01, baseSpeed) * (fastForward ? 2.0 : 1.0);

    private bool IsSelectingTarget =>
        _logic.State is BattlePresenterLogicState.AttackSelectingTarget
            or BattlePresenterLogicState.SkillSelectingTarget;

    private void OnActionMenuSelected(long index) =>
        _logic.SelectMenuAction((int)index);

    private void OnActionMenuGuiInput(InputEvent @event) =>
        HandleMenuNavigation(
            @event,
            ActionMenu,
            _logic.SelectMenuAction
        );

    private void OnActionMenuActivated(long index)
    {
        _logic.SelectMenuAction((int)index);
        IgnoreTargetAcceptIfPressed();
        _logic.Confirm();
    }

    private void OnSkillActionSelected(long index) =>
        _logic.SelectSkillAction((int)index);

    private void OnSkillActionsGuiInput(InputEvent @event) =>
        HandleMenuNavigation(
            @event,
            SkillActions,
            _logic.SelectSkillAction
        );

    private void OnSkillActionActivated(long index)
    {
        _logic.SelectSkillAction((int)index);
        IgnoreTargetAcceptIfPressed();
        _logic.Confirm();
    }

    private void HandleMenuNavigation(
        InputEvent @event,
        ItemList list,
        Action<int> select
    )
    {
        var direction = MenuNavigationDirection(@event);
        if (direction == 0 || !list.Visible)
        {
            return;
        }

        var index = NextEnabledItemIndex(list, direction);
        if (index < 0)
        {
            return;
        }

        SelectMenuItem(list, index, list.GetItemCount());
        select(index);
        AcceptEvent();
        GetViewport().SetInputAsHandled();
    }

    private static int MenuNavigationDirection(InputEvent @event)
    {
        if (
            @event.IsActionPressed(GameInputs.MoveForward)
            || @event.IsActionPressed("ui_up")
        )
        {
            return -1;
        }

        if (
            @event.IsActionPressed(GameInputs.MoveBackward)
            || @event.IsActionPressed("ui_down")
        )
        {
            return 1;
        }

        return 0;
    }

    private static int NextEnabledItemIndex(ItemList list, int direction)
    {
        var count = list.GetItemCount();
        if (count == 0)
        {
            return -1;
        }

        var selected = list.GetSelectedItems();
        var current = selected.Length > 0
            ? selected[0]
            : direction > 0 ? -1 : count;

        for (var step = 1; step <= count; step++)
        {
            var index = (current + direction * step) % count;
            if (index < 0)
            {
                index += count;
            }

            if (!list.IsItemDisabled(index))
            {
                return index;
            }
        }

        return -1;
    }

    private void IgnoreTargetAcceptIfPressed()
    {
        if (Input.IsActionPressed(GameInputs.UiAccept))
        {
            _ignoreTargetAcceptUntilRelease = true;
        }
        AcceptEvent();
        GetViewport().SetInputAsHandled();
    }

    private void RenderBattle(BattleScreenView view)
    {
        var active = view.ActiveActorId is { } id
            ? view.Units.FirstOrDefault(unit => unit.Id == id)?.Name
            : null;
        Turn.Text = active is null
            ? $"Turn {view.Turn}"
            : $"Turn {view.Turn}: {active}";

        ClearSlots();
        foreach (var unit in view.Units)
        {
            var gridPoint = new BattleTargetGridPoint(
                unit.Team,
                unit.Position.Row,
                unit.Position.Index
            );
            if (!_slots.TryGetValue(gridPoint, out var slot))
            {
                continue;
            }

            slot.UnitId = unit.Id;
            slot.Root.Visible = true;
            slot.Root.Modulate = unit.IsAlive
                ? Colors.White
                : new Color(0.5f, 0.5f, 0.5f, 0.7f);
            slot.ActiveHighlight.Visible = unit.Id == view.ActiveActorId;
            slot.QueuedCommand.Text = unit.QueuedCommandText ?? "";
            slot.QueuedCommand.Visible =
                !string.IsNullOrWhiteSpace(unit.QueuedCommandText);

            if (slot.PartyUi is not null)
            {
                slot.PartyUi.SetNameLabel(unit.Name);
                slot.PartyUi.SetHp(unit.Hp, unit.MaxHp);
                slot.PartyUi.SetTp(unit.Tp, unit.MaxTp);
            }
            else
            {
                slot.EnemyName!.Text = unit.Name;
                slot.EnemyVitals!.Text =
                    $"HP {unit.Hp}/{unit.MaxHp}  TP {unit.Tp}/{unit.MaxTp}";
            }
        }
    }

    private void RenderCommandMenu(
        BattleCommandPrompt prompt,
        IReadOnlyList<BattleCommandMenuOption> options,
        int selectedIndex
    )
    {
        Message.Text = "Choose command";
        ClearTargetVisuals();
        SkillActions.Clear();
        SkillActions.Visible = false;
        ActionMenu.Clear();
        ActionMenu.Visible = true;

        for (var index = 0; index < options.Count; index++)
        {
            ActionMenu.AddItem(options[index].Name);
            ActionMenu.SetItemDisabled(index, !options[index].IsEnabled);
        }

        SelectMenuItem(ActionMenu, selectedIndex, options.Count);
        ActionMenu.GrabFocus();
    }

    private void RenderSkillActions(
        BattleCommandPrompt prompt,
        int selectedIndex
    )
    {
        Message.Text = "Choose skill";
        ClearTargetVisuals();
        SkillActions.Clear();
        SkillActions.Visible = true;

        for (var index = 0; index < prompt.Skills.Count; index++)
        {
            var skill = prompt.Skills[index];
            SkillActions.AddItem(ActionText(skill));
            SkillActions.SetItemDisabled(index, !skill.IsEnabled);
        }

        SelectMenuItem(SkillActions, selectedIndex, prompt.Skills.Count);
        SkillActions.GrabFocus();
    }

    private void RenderTargets(BattleActionOption option, int selectedIndex)
    {
        ClearTargetVisuals();
        Message.Text = $"{option.Name}: choose target";
        if (option.TargetOptions.Count == 0)
        {
            return;
        }

        var target = option.TargetOptions[Math.Clamp(
            selectedIndex,
            0,
            option.TargetOptions.Count - 1
        )];
        var affected = target.AffectedIds.ToHashSet();
        var anchors = target.AnchorIds.ToHashSet();

        foreach (var slot in _slots.Values)
        {
            if (slot.UnitId is not { } id)
            {
                continue;
            }

            slot.AffectedHighlight.Visible = affected.Contains(id);
            slot.Caret.Visible = anchors.Contains(id);
            if (slot.Caret.Visible)
            {
                slot.Caret.SetMode(
                    slot.Team == BattleTeam.Player
                        ? TargetSelectionCaretMode.Party
                        : TargetSelectionCaretMode.Enemy
                );
            }
        }

        ActionMenu.ReleaseFocus();
        SkillActions.ReleaseFocus();
        GrabFocus();
    }

    private void BeginCueVisual(BattleCue cue)
    {
        ClearCueVisual();
        _elapsed = 0;
        switch (cue)
        {
            case AnimationCue animation:
                Message.Text = animation.AnimationId;
                _duration = 0.35;
                break;
            case PopupBatchCue batch:
                Message.Text = string.Join(
                    "  ",
                    batch.Popups.Select(PopupText)
                );
                _duration = 0.5;
                break;
            case StatusCue status:
                Message.Text = status.Applied
                    ? $"{status.TargetId}: {status.StatusId}"
                    : $"{status.TargetId}: {status.StatusId} ended";
                _duration = 0.3;
                break;
            case WaitCue wait:
                _duration = Math.Max(0, wait.Seconds);
                break;
            case DeathCue death:
                Message.Text = $"{death.TargetId} defeated";
                _duration = 0.5;
                break;
            default:
                _duration = 0;
                break;
        }
        SetProcess(true);
    }

    private void FinishCueBatch()
    {
        var callback = _finished;
        _finished = null;
        ClearCueVisual();
        callback?.Invoke();
    }

    private void HideUi()
    {
        Visible = false;
        Message.Text = "";
        ActionMenu.Clear();
        ActionMenu.Visible = false;
        SkillActions.Clear();
        SkillActions.Visible = false;
        ClearTargetVisuals();
    }

    private void ShowUi()
    {
        Visible = true;
        ProcessMode = ProcessModeEnum.Inherit;
    }

    private void ClearCueVisual()
    {
        _elapsed = 0;
        _duration = 0;
        if (IsInstanceValid(Message))
        {
            Message.Text = "";
        }
        SetProcess(false);
    }

    private void BuildSlotLookup()
    {
        _slots.Clear();
        AddSlot(BattleTeam.Player, PartyRow.Front, 0);
        AddSlot(BattleTeam.Player, PartyRow.Front, 1);
        AddSlot(BattleTeam.Player, PartyRow.Front, 2);
        AddSlot(BattleTeam.Player, PartyRow.Back, 0);
        AddSlot(BattleTeam.Player, PartyRow.Back, 1);
        AddSlot(BattleTeam.Player, PartyRow.Back, 2);
        AddSlot(BattleTeam.Enemy, PartyRow.Back, 0);
        AddSlot(BattleTeam.Enemy, PartyRow.Back, 1);
        AddSlot(BattleTeam.Enemy, PartyRow.Back, 2);
        AddSlot(BattleTeam.Enemy, PartyRow.Front, 0);
        AddSlot(BattleTeam.Enemy, PartyRow.Front, 1);
        AddSlot(BattleTeam.Enemy, PartyRow.Front, 2);
    }

    private void AddSlot(BattleTeam team, PartyRow row, int index)
    {
        var parent = team == BattleTeam.Player ? PartySlots : EnemySlots;
        var name = team == BattleTeam.Player
            ? $"Player{row}{index}"
            : $"Enemy{row}{index}";
        if (row == PartyRow.Front && team == BattleTeam.Player)
        {
            parent = FrontRow;
        }
        if (row == PartyRow.Back && team == BattleTeam.Player)
        {
            parent = BackRow;
        }
        var root = parent.GetNode<Control>(name);
        var slot = new UnitSlot(team, row, index, root);
        _slots[new BattleTargetGridPoint(team, row, index)] = slot;
    }

    private void ClearSlots()
    {
        foreach (var slot in _slots.Values)
        {
            slot.UnitId = null;
            slot.Root.Visible = false;
            slot.Root.Modulate = Colors.White;
            slot.ActiveHighlight.Visible = false;
            slot.AffectedHighlight.Visible = false;
            slot.Caret.Visible = false;
            slot.QueuedCommand.Text = "";
            slot.QueuedCommand.Visible = false;
            if (slot.EnemyName is not null)
            {
                slot.EnemyName.Text = "";
            }
            if (slot.EnemyVitals is not null)
            {
                slot.EnemyVitals.Text = "";
            }
        }
    }

    private void ClearTargetVisuals()
    {
        foreach (var slot in _slots.Values)
        {
            slot.AffectedHighlight.Visible = false;
            slot.Caret.Visible = false;
        }
    }

    private bool TrySelectTargetAtMouse()
    {
        var point = GetGlobalMousePosition();
        foreach (var slot in _slots.Values)
        {
            if (
                slot.UnitId is { } id
                && slot.Root.Visible
                && slot.Root.GetGlobalRect().HasPoint(point)
            )
            {
                _logic.SelectTargetAnchor(id);
                return true;
            }
        }

        return false;
    }

    private static void SelectMenuItem(
        ItemList list,
        int selectedIndex,
        int count
    )
    {
        if (count == 0)
        {
            return;
        }

        list.Select(Math.Clamp(selectedIndex, 0, count - 1));
        list.EnsureCurrentIsVisible();
    }

    private static string ActionText(BattleActionOption option) =>
        $"{option.Name} ({option.TpCost} TP)";

    private static string PopupText(BattlePopup popup) =>
        popup.Kind switch
        {
            BattlePopupKind.Damage =>
                $"{popup.TargetId}: -{popup.Amount} HP",
            BattlePopupKind.Heal =>
                $"{popup.TargetId}: +{popup.Amount} HP",
            BattlePopupKind.Tp =>
                $"{popup.TargetId}: {popup.Amount:+#;-#;0} TP",
            _ => popup.Amount.ToString(CultureInfo.InvariantCulture),
        };

    private static void Fill(Control control)
    {
        control.AnchorLeft = 0;
        control.AnchorTop = 0;
        control.AnchorRight = 1;
        control.AnchorBottom = 1;
        control.OffsetLeft = 0;
        control.OffsetTop = 0;
        control.OffsetRight = 0;
        control.OffsetBottom = 0;
    }

    private sealed class UnitSlot
    {
        public UnitSlot(
            BattleTeam team,
            PartyRow row,
            int index,
            Control root
        )
        {
            Team = team;
            Row = row;
            Index = index;
            Root = root;
            Root.MouseFilter = Control.MouseFilterEnum.Stop;
            PartyUi = root.GetNodeOrNull<PartyMemberUi>("PartyMemberUi");
            if (PartyUi is not null)
            {
                Fill(PartyUi);
            }
            EnemyName = root.GetNodeOrNull<Label>("EnemyName");
            EnemyVitals = root.GetNodeOrNull<Label>("EnemyVitals");
            QueuedCommand = root.GetNode<Label>("QueuedCommand");
            ActiveHighlight = root.GetNode<ColorRect>("ActiveHighlight");
            AffectedHighlight = root.GetNode<ColorRect>("AffectedHighlight");
            Caret = root.GetNode<TargetSelectionCaret>("TargetCaret");
            Caret.Visible = false;
        }

        public BattleTeam Team { get; }
        public PartyRow Row { get; }
        public int Index { get; }
        public Control Root { get; }
        public PartyMemberUi? PartyUi { get; }
        public Label? EnemyName { get; }
        public Label? EnemyVitals { get; }
        public Label QueuedCommand { get; }
        public ColorRect ActiveHighlight { get; }
        public ColorRect AffectedHighlight { get; }
        public TargetSelectionCaret Caret { get; }
        public BattlerId? UnitId { get; set; }
    }
}
