namespace Labyrinth;

using System;
using System.Collections.Generic;
using Chickensoft.AutoInject;
using Chickensoft.Introspection;
using Godot;

public interface IBattlePresenter
{
    bool IsPlaying { get; }
    double BaseSpeed { get; set; }
    double EffectiveSpeed { get; }
    void Play(IReadOnlyList<BattleCue> cues, Action finished);
    void Cancel();
}

[Meta(typeof(IAutoNode))]
public partial class BattlePresenter : Control, IBattlePresenter
{
    public override void _Notification(int what) => this.Notify(what);

    [Export(PropertyHint.Range, "0.25,4,0.25")]
    public double BaseSpeed { get; set; } = 1;

    [Node] public Label Message { get; set; } = default!;
    [Node] public Control Popups { get; set; } = default!;

    public bool IsPlaying => _cues is not null;
    public double EffectiveSpeed => CalculateEffectiveSpeed(
      BaseSpeed,
      Input.IsActionPressed(GameInputs.UiAccept)
    );

    private IReadOnlyList<BattleCue>? _cues;
    private Action? _finished;
    private int _cueIndex;
    private double _elapsed;
    private double _duration;

    public void OnResolved() => SetProcess(false);

    public override void _Process(double delta)
    {
        if (_cues is null)
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

    public void Play(IReadOnlyList<BattleCue> cues, Action finished)
    {
        ArgumentNullException.ThrowIfNull(cues);
        ArgumentNullException.ThrowIfNull(finished);
        Cancel();
        _cues = cues;
        _finished = finished;
        _cueIndex = 0;
        SetProcess(true);
        BeginCue();
    }

    public void Cancel()
    {
        _cues = null;
        _finished = null;
        _cueIndex = 0;
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

    public void OnExitTree() => Cancel();

    public static double CalculateEffectiveSpeed(
      double baseSpeed,
      bool fastForward
    ) => Math.Max(0.01, baseSpeed) * (fastForward ? 2.0 : 1.0);

    private void BeginCue()
    {
        if (_cues is null)
        {
            return;
        }
        if (_cueIndex >= _cues.Count)
        {
            var callback = _finished;
            Cancel();
            callback?.Invoke();
            return;
        }

        _elapsed = 0;
        switch (_cues[_cueIndex])
        {
            case AnimationCue animation:
                Message.Text = animation.AnimationId;
                _duration = 0.35;
                break;
            case PopupBatchCue batch:
                Message.Text = "";
                foreach (var popup in batch.Popups)
                {
                    Popups.AddChild(new Label
                    {
                        Text = PopupText(popup),
                        Position = new Vector2(16, 32 * Popups.GetChildCount()),
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
                Message.Text = "";
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
        foreach (var child in Popups.GetChildren())
        {
            child.QueueFree();
        }
    }
}
