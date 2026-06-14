namespace Labyrinth;

using System;
using System.Collections.Generic;
using Godot;

public interface IBattlePresenter
{
    bool IsPresenting { get; }
    double BaseSpeed { get; set; }
    double EffectiveSpeed { get; }

    void Present(BattleStep step, Action finished);
    void Cancel();
}

public partial class BattlePresenter : Control, IBattlePresenter
{
    [Export(PropertyHint.Range, "0.25,4,0.25")]
    public double BaseSpeed { get; set; } = 1;

    public bool IsPresenting => _step is not null;
    public double EffectiveSpeed =>
        CalculateEffectiveSpeed(
            BaseSpeed,
            Input.IsActionPressed(GameInputs.UiAccept)
        );

    private Label _message = default!;
    private Control _popupRoot = default!;
    private BattleStep? _step;
    private Action? _finished;
    private int _cueIndex;
    private double _elapsed;
    private double _duration;

    public override void _Ready()
    {
        _message = GetNode<Label>("Message");
        _popupRoot = GetNode<Control>("Popups");
        SetProcess(false);
    }

    public override void _Process(double delta)
    {
        if (_step is null)
        {
            return;
        }
        _elapsed += delta * EffectiveSpeed;
        if (_elapsed < _duration)
        {
            return;
        }
        ClearPopups();
        _cueIndex++;
        BeginCue();
    }

    public void Present(BattleStep step, Action finished)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(finished);
        if (step.Kind != BattleStepKind.Presentation)
        {
            throw new ArgumentException(
                "Presenter requires a presentation step.",
                nameof(step)
            );
        }
        Cancel();
        _step = step;
        _finished = finished;
        _cueIndex = 0;
        SetProcess(true);
        BeginCue();
    }

    public void Cancel()
    {
        _step = null;
        _finished = null;
        _cueIndex = 0;
        _elapsed = 0;
        _duration = 0;
        if (IsInstanceValid(_message))
        {
            _message.Text = "";
        }
        if (IsInstanceValid(_popupRoot))
        {
            ClearPopups();
        }
        SetProcess(false);
    }

    public static double CalculateEffectiveSpeed(
        double baseSpeed,
        bool fastForward
    ) => Math.Max(0.01, baseSpeed) * (fastForward ? 2.0 : 1.0);

    private void BeginCue()
    {
        if (_step is null)
        {
            return;
        }
        if (_cueIndex >= _step.Cues.Count)
        {
            var callback = _finished;
            Cancel();
            callback?.Invoke();
            return;
        }

        _elapsed = 0;
        switch (_step.Cues[_cueIndex])
        {
            case AnimationCue animation:
                _message.Text = animation.AnimationId;
                _duration = 0.35;
                break;
            case PopupBatchCue batch:
                _message.Text = "";
                foreach (var popup in batch.Popups)
                {
                    var label = new Label
                    {
                        Text = PopupText(popup),
                        Position = new Vector2(
                            16,
                            32 * _popupRoot.GetChildCount()
                        ),
                    };
                    _popupRoot.AddChild(label);
                }
                _duration = 0.5;
                break;
            case StatusCue status:
                _message.Text = status.Applied
                    ? $"{status.TargetId}: {status.StatusId}"
                    : $"{status.TargetId}: {status.StatusId} ended";
                _duration = 0.3;
                break;
            case WaitCue wait:
                _message.Text = "";
                _duration = Math.Max(0, wait.Seconds);
                break;
            case DeathCue death:
                _message.Text = $"{death.TargetId} defeated";
                _duration = 0.5;
                break;
            default:
                _duration = 0;
                break;
        }
    }

    private static string PopupText(BattlePopup popup) =>
        popup.Kind switch
        {
            BattlePopupKind.Damage =>
                $"{popup.TargetId}: -{popup.Amount} HP",
            BattlePopupKind.Heal =>
                $"{popup.TargetId}: +{popup.Amount} HP",
            BattlePopupKind.Tp =>
                $"{popup.TargetId}: {popup.Amount:+#;-#;0} TP",
            _ => popup.Amount.ToString(),
        };

    private void ClearPopups()
    {
        foreach (var child in _popupRoot.GetChildren())
        {
            child.QueueFree();
        }
    }
}
