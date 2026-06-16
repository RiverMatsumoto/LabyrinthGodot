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
    [Node] public Label Party { get; set; } = default!;
    [Node] public Label Enemies { get; set; } = default!;
    [Node] public Label Message { get; set; } = default!;
    [Node] public Control Popups { get; set; } = default!;
    [Node] public Button Attack { get; set; } = default!;
    [Node] public Button Skill { get; set; } = default!;
    [Node] public Button Item { get; set; } = default!;
    [Node] public Button Defence { get; set; } = default!;
    [Node] public Button Move { get; set; } = default!;
    [Node] public Button Escape { get; set; } = default!;
    [Node] public Button Undo { get; set; } = default!;
    [Node] public Button Confirm { get; set; } = default!;
    [Node] public OptionButton SkillActions { get; set; } = default!;
    [Node] public OptionButton Target { get; set; } = default!;

    public bool IsPlaying => _finished is not null;
    public double EffectiveSpeed => CalculateEffectiveSpeed(
        BaseSpeed,
        Input.IsActionPressed(GameInputs.UiAccept)
    );

    private readonly BattlePresenterLogic _logic = new();
    private BattlePresenterLogic.Binding? _binding;
    private Action? _finished;
    private double _elapsed;
    private double _duration;

    public void OnResolved()
    {
        Attack.Pressed += _logic.SelectAttack;
        Skill.Pressed += _logic.SelectSkill;
        Item.Pressed += _logic.SelectItem;
        Defence.Pressed += _logic.SelectDefence;
        Move.Pressed += _logic.SelectMove;
        Escape.Pressed += _logic.RequestEscape;
        Undo.Pressed += _logic.RequestUndo;
        Confirm.Pressed += _logic.Confirm;
        SkillActions.ItemSelected += OnSkillActionSelected;
        Target.ItemSelected += OnTargetSelected;

        _binding = _logic.Bind()
            .OnOutput((
                in BattlePresenterLogicState.Output.RenderBattle output
            ) => RenderBattle(output.View))
            .OnOutput((
                in BattlePresenterLogicState.Output.RenderCommandMenu output
            ) => RenderCommandMenu(output.Prompt))
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
            ) => HideUi());

        _logic.Start<BattlePresenterLogicState.Hidden>();
        SetProcess(false);
        HideUi();
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
        Visible = true;
        _logic.ShowCommandPrompt(view, prompt);
    }

    public void Render(BattleScreenView view)
    {
        Visible = true;
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
        Visible = true;
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
        Attack.Pressed -= _logic.SelectAttack;
        Skill.Pressed -= _logic.SelectSkill;
        Item.Pressed -= _logic.SelectItem;
        Defence.Pressed -= _logic.SelectDefence;
        Move.Pressed -= _logic.SelectMove;
        Escape.Pressed -= _logic.RequestEscape;
        Undo.Pressed -= _logic.RequestUndo;
        Confirm.Pressed -= _logic.Confirm;
        SkillActions.ItemSelected -= OnSkillActionSelected;
        Target.ItemSelected -= OnTargetSelected;
        Cancel();
        _binding?.Dispose();
        _logic.Dispose();
    }

    public static double CalculateEffectiveSpeed(
        double baseSpeed,
        bool fastForward
    ) => Math.Max(0.01, baseSpeed) * (fastForward ? 2.0 : 1.0);

    private void OnSkillActionSelected(long index) =>
        _logic.SelectSkillAction((int)index);

    private void OnTargetSelected(long index) =>
        _logic.SelectTarget((int)index);

    private void RenderBattle(BattleScreenView view)
    {
        var active = view.ActiveActorId is { } id
            ? view.Units.FirstOrDefault(unit => unit.Id == id)?.Name
            : null;
        Turn.Text = active is null
            ? $"Turn {view.Turn}"
            : $"Turn {view.Turn}: {active}";
        Party.Text = FormatTeam(view, BattleTeam.Player);
        Enemies.Text = FormatTeam(view, BattleTeam.Enemy);
    }

    private void RenderCommandMenu(BattleCommandPrompt prompt)
    {
        Message.Text = "Choose command";
        SkillActions.Clear();
        SkillActions.Visible = false;
        Target.Clear();
        Target.Visible = false;
        Attack.Disabled = prompt.Attack is null;
        Skill.Disabled = prompt.Skills.Count == 0;
        Item.Disabled = !prompt.CanUseItem;
        Defence.Disabled = !prompt.CanDefend;
        Move.Disabled = !prompt.CanMove;
        Escape.Disabled = false;
        Undo.Disabled = false;
        Confirm.Disabled = true;
    }

    private void RenderSkillActions(
        BattleCommandPrompt prompt,
        int selectedIndex
    )
    {
        Message.Text = "Choose skill";
        SkillActions.Clear();
        foreach (var skill in prompt.Skills)
        {
            SkillActions.AddItem(ActionText(skill));
        }
        SkillActions.Visible = true;
        if (prompt.Skills.Count > 0)
        {
            SkillActions.Select(Math.Clamp(
                selectedIndex,
                0,
                prompt.Skills.Count - 1
            ));
        }
        Target.Clear();
        Target.Visible = false;
        Confirm.Disabled = prompt.Skills.Count == 0;
    }

    private void RenderTargets(BattleActionOption option, int selectedIndex)
    {
        Message.Text = $"{option.Name}: choose target";
        Target.Clear();
        foreach (var target in option.TargetOptions)
        {
            Target.AddItem(target.Name);
        }
        Target.Visible = true;
        if (option.TargetOptions.Count > 0)
        {
            Target.Select(Math.Clamp(
                selectedIndex,
                0,
                option.TargetOptions.Count - 1
            ));
        }
        Confirm.Disabled = option.TargetOptions.Count == 0;
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
                foreach (var popup in batch.Popups)
                {
                    Popups.AddChild(new Label
                    {
                        Text = PopupText(popup),
                        Position = new Vector2(
                            16,
                            32 * Popups.GetChildCount()
                        ),
                    });
                }
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
        SkillActions.Clear();
        SkillActions.Visible = false;
        Target.Clear();
        Target.Visible = false;
        Attack.Disabled = true;
        Skill.Disabled = true;
        Item.Disabled = true;
        Defence.Disabled = true;
        Move.Disabled = true;
        Escape.Disabled = true;
        Undo.Disabled = true;
        Confirm.Disabled = true;
    }

    private void ClearCueVisual()
    {
        _elapsed = 0;
        _duration = 0;
        if (IsInstanceValid(Message))
        {
            Message.Text = "";
        }
        if (IsInstanceValid(Popups))
        {
            ClearPopups();
        }
        SetProcess(false);
    }

    private static string FormatTeam(
        BattleScreenView view,
        BattleTeam team
    ) => string.Join(
        "\n",
        view.Units
            .Where(unit => unit.Team == team)
            .Select(unit =>
                $"{unit.Name}  HP {unit.Hp}/{unit.MaxHp}  "
                + $"TP {unit.Tp}/{unit.MaxTp}")
    );

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

    private void ClearPopups()
    {
        foreach (var child in Popups.GetChildren())
        {
            child.QueueFree();
        }
    }
}
