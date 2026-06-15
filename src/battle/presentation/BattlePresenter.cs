namespace Labyrinth;

using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Plays an ordered batch of battle cues and reports when the entire batch has
/// finished.
/// </summary>
public interface IBattlePresenter
{
    bool IsPlaying { get; }
    double BaseSpeed { get; set; }
    double EffectiveSpeed { get; }

    /// <summary>
    /// Plays a cue-playback advance, then invokes <paramref name="finished"/>
    /// once all cues have completed.
    /// </summary>
    void Play(BattleAdvance advance, Action finished);
    void Cancel();
}

/// <summary>
/// Godot UI implementation of <see cref="IBattlePresenter"/>. It controls cue
/// timing and temporary visuals but does not mutate battle state.
/// </summary>
public partial class BattlePresenter : Control, IBattlePresenter
{
    [Export(PropertyHint.Range, "0.25,4,0.25")]
    public double BaseSpeed { get; set; } = 1;

    public bool IsPlaying => _advance is not null;
    public double EffectiveSpeed =>
        CalculateEffectiveSpeed(
            BaseSpeed,
            Input.IsActionPressed(GameInputs.UiAccept)
        );

    private Label _message = default!;
    private Control _popupRoot = default!;
    private BattleAdvance? _advance;
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
        if (_advance is null)
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

    /// <inheritdoc />
    public void Play(BattleAdvance advance, Action finished)
    {
        ArgumentNullException.ThrowIfNull(advance);
        ArgumentNullException.ThrowIfNull(finished);
        if (advance.Kind != BattleAdvanceKind.CuePlaybackRequired)
        {
            throw new ArgumentException(
                "Presenter requires a cue playback advance.",
                nameof(advance)
            );
        }
        Cancel();
        _advance = advance;
        _finished = finished;
        _cueIndex = 0;
        SetProcess(true);
        BeginCue();
    }

    public void Cancel()
    {
        _advance = null;
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
        if (_advance is null)
        {
            return;
        }
        if (_cueIndex >= _advance.Cues.Count)
        {
            var callback = _finished;
            Cancel();
            callback?.Invoke();
            return;
        }

        _elapsed = 0;
        switch (_advance.Cues[_cueIndex])
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
