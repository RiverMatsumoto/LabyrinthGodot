namespace Labyrinth;

using System;
using System.Collections.Generic;
using System.Linq;
using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;
using Godot;

public interface IBattle :
  IControl,
  IProvide<IBattleRepo>,
  IProvide<IBattleLogic>
{
    IBattleRepo BattleRepo { get; }
    IBattleLogic BattleLogic { get; }
}

[Meta(typeof(IAutoNode))]
public partial class Battle : Control, IBattle
{
    public override void _Notification(int what) => this.Notify(what);

    [Export] public BattleContentResource Content { get; set; } = default!;
    [Export] public PartyContentResource PartyContent { get; set; } = default!;

    [Dependency] public IGameLogic GameLogic => this.DependOn<IGameLogic>();
    [Dependency] public IGameRepo GameRepo => this.DependOn<IGameRepo>();
    [Dependency] public IPartyRepo PartyRepo => this.DependOn<IPartyRepo>();

    [Node] public BattlePresenter Presenter { get; set; } = default!;
    [Node] public Label Turn { get; set; } = default!;
    [Node] public Label Party { get; set; } = default!;
    [Node] public Label Enemies { get; set; } = default!;
    [Node] public OptionButton Action { get; set; } = default!;
    [Node] public OptionButton Target { get; set; } = default!;
    [Node] public Button Confirm { get; set; } = default!;
    [Node] public Button Undo { get; set; } = default!;
    [Node] public Button Flee { get; set; } = default!;

    public IBattleRepo BattleRepo { get; set; } = default!;
    public IBattleLogic BattleLogic { get; set; } = default!;
    public IBattleSession BattleSession { get; set; } = default!;

    IBattleRepo IProvide<IBattleRepo>.Value() => BattleRepo;
    IBattleLogic IProvide<IBattleLogic>.Value() => BattleLogic;

    private readonly List<ActionId> _actionIds = [];
    private readonly List<BattlerId> _targetIds = [];
    private BattlerId? _currentBattlerId;
    private BattleLogic.Binding? _battleBinding;
    private LogicBlock.Binding? _gameBinding;

    public void Setup()
    {
        var compiled = (
          Content
          ?? throw new InvalidOperationException(
            "Battle requires authored battle content."
          )
        ).Compile();
        var compiledParty = (
          PartyContent
          ?? throw new InvalidOperationException(
            "Battle requires authored party content."
          )
        ).Compile(compiled.Catalog);

        BattleRepo = new BattleRepo(compiled.Catalog);
        BattleSession = new BattleSession(
          compiled,
          compiledParty,
          GameRepo,
          PartyRepo
        );
        BattleLogic = new BattleLogic();
        BattleLogic.Set(BattleRepo);
        BattleLogic.Set<IEnemyCommandPlanner>(new BasicEnemyCommandPlanner());
        BattleLogic.Set(BattleSession);
    }

    public void OnResolved()
    {
        Action.ItemSelected += OnActionSelected;
        Confirm.Pressed += ConfirmCommand;
        Undo.Pressed += BattleLogic.UndoCommand;
        Flee.Pressed += BattleLogic.Flee;

        _battleBinding = ((BattleLogic)BattleLogic).Bind()
          .OnOutput((
            in BattleLogicState.Output.CommandRequested output
          ) => ShowCommand(output.ActorId))
          .OnOutput((
            in BattleLogicState.Output.CommandRejected output
          ) => Turn.Text = output.Error)
          .OnOutput((
            in BattleLogicState.Output.CuePlaybackRequested output
          ) => PlayCues(output.CueBatchId, output.Cues))
          .OnOutput((
            in BattleLogicState.Output.BattleCompleted output
          ) => CompleteBattle())
          .OnOutput((
            in BattleLogicState.Output.ReturnModeRequested output
          ) => RequestReturnMode(output.Mode))
          .OnEnter<BattleLogicState.ResolvingTurn>(_ =>
            CallDeferred(nameof(AdvanceBattleLogic)));

        _gameBinding = GameLogic.Bind()
          .OnEnter<GameLogicState.Battle>(_ => StartRequestedBattle())
          .OnExit<GameLogicState.Battle>(_ => HideBattle());

        this.Provide();
        BattleLogic.Start<BattleLogicState.Disabled>();
        HideBattle();
    }

    public void AdvanceBattleLogic()
    {
        if (BattleLogic.State is BattleLogicState.ResolvingTurn)
        {
            BattleLogic.AdvanceResolution();
        }
    }

    public void OnExitTree()
    {
        Action.ItemSelected -= OnActionSelected;
        Confirm.Pressed -= ConfirmCommand;
        Undo.Pressed -= BattleLogic.UndoCommand;
        Flee.Pressed -= BattleLogic.Flee;
        Presenter.Cancel();
        _battleBinding?.Dispose();
        _gameBinding?.Dispose();
        BattleLogic.Dispose();
        BattleRepo.Dispose();
    }

    private void StartRequestedBattle()
    {
        Visible = true;
        MouseFilter = MouseFilterEnum.Stop;
        BattleLogic.StartRequestedBattle();
        RefreshBattleState();
    }

    private void ShowCommand(BattlerId battlerId)
    {
        _currentBattlerId = battlerId;
        _actionIds.Clear();
        Action.Clear();
        var battler = BattleRepo.Snapshot().Units.First(unit =>
          unit.Id == battlerId);

        foreach (var actionId in battler.ActionIds)
        {
            var action = BattleRepo.Catalog.GetAction(actionId);
            _actionIds.Add(actionId);
            Action.AddItem($"{action.Name} ({action.TpCost} TP)");
        }

        Action.Disabled = _actionIds.Count == 0;
        Confirm.Disabled = _actionIds.Count == 0;
        if (_actionIds.Count > 0)
        {
            Action.Select(0);
            PopulateTargets(_actionIds[0]);
        }

        Turn.Text = $"Turn {BattleRepo.Turn}: {battler.Name}";
        SetCommandControlsEnabled(true);
        RefreshBattleState();
    }

    private void OnActionSelected(long index)
    {
        if (index >= 0 && index < _actionIds.Count)
        {
            PopulateTargets(_actionIds[(int)index]);
        }
    }

    private void PopulateTargets(ActionId actionId)
    {
        _targetIds.Clear();
        Target.Clear();
        if (_currentBattlerId is not { } actorId)
        {
            return;
        }

        var snapshot = BattleRepo.Snapshot();
        foreach (var id in BattleRepo.GetValidTargets(actorId, actionId))
        {
            var unit = snapshot.Units.First(candidate => candidate.Id == id);
            _targetIds.Add(id);
            Target.AddItem(unit.Name);
        }

        Target.Disabled = _targetIds.Count <= 1;
        Confirm.Disabled = _targetIds.Count == 0;
    }

    private void ConfirmCommand()
    {
        if (
          _currentBattlerId is not { } actorId
          || Action.Selected < 0
          || Action.Selected >= _actionIds.Count
        )
        {
            return;
        }

        var target = Target.Selected >= 0
          && Target.Selected < _targetIds.Count
          ? _targetIds[Target.Selected]
          : (BattlerId?)null;
        BattleLogic.SubmitCommand(new BattleCommand(
          actorId,
          _actionIds[Action.Selected],
          target
        ));
    }

    private void PlayCues(
      long cueBatchId,
      IReadOnlyList<BattleCue> cues
    )
    {
        SetCommandControlsEnabled(false);
        RefreshBattleState();
        Presenter.Play(
          cues,
          () => BattleLogic.AcknowledgeCuePlayback(cueBatchId)
        );
    }

    private void CompleteBattle()
    {
        RefreshBattleState();
        HideBattle();
    }

    private void RequestReturnMode(GameMode mode)
    {
        switch (mode)
        {
            case GameMode.MainMenu:
                GameLogic.Input(new GameLogicState.Input.EnterMainMenu());
                break;
            case GameMode.Town:
                GameLogic.Input(new GameLogicState.Input.EnterTown());
                break;
            case GameMode.Labyrinth:
                GameLogic.Input(new GameLogicState.Input.EnterLabyrinth());
                break;
            default:
                throw new InvalidOperationException(
                  $"Battle cannot return to '{mode}'."
                );
        }
    }

    private void RefreshBattleState()
    {
        var snapshot = BattleRepo.Snapshot();
        Party.Text = FormatTeam(snapshot, BattleTeam.Player);
        Enemies.Text = FormatTeam(snapshot, BattleTeam.Enemy);
    }

    private static string FormatTeam(
      BattleSnapshot snapshot,
      BattleTeam team
    ) => string.Join(
      "\n",
      snapshot.Units
        .Where(unit => unit.Team == team)
        .Select(unit =>
          $"{unit.Name}  HP {unit.Hp}/{unit.Stats.MaxHp}  "
          + $"TP {unit.Tp}/{unit.Stats.MaxTp}")
    );

    private void SetCommandControlsEnabled(bool enabled)
    {
        Action.Disabled = !enabled || _actionIds.Count == 0;
        Target.Disabled = !enabled || _targetIds.Count <= 1;
        Confirm.Disabled = !enabled || _targetIds.Count == 0;
        Undo.Disabled = !enabled;
        Flee.Disabled = !enabled;
    }

    private void HideBattle()
    {
        Presenter.Cancel();
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        _currentBattlerId = null;
    }
}
