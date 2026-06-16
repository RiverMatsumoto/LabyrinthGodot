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

    public IBattleRepo BattleRepo { get; set; } = default!;
    public IBattleLogic BattleLogic { get; set; } = default!;
    public IBattleSession BattleSession { get; set; } = default!;

    IBattleRepo IProvide<IBattleRepo>.Value() => BattleRepo;
    IBattleLogic IProvide<IBattleLogic>.Value() => BattleLogic;

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
        Presenter.CommandSubmitted += BattleLogic.SubmitCommand;
        Presenter.UndoRequested += BattleLogic.UndoCommand;
        Presenter.EscapeRequested += BattleLogic.Flee;

        _battleBinding = ((BattleLogic)BattleLogic).Bind()
            .OnOutput((
                in BattleLogicState.Output.CommandRequested output
            ) => ShowCommand(output.ActorId))
            .OnOutput((
                in BattleLogicState.Output.CommandRejected output
            ) => Presenter.ShowError(output.Error))
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
        Presenter.CommandSubmitted -= BattleLogic.SubmitCommand;
        Presenter.UndoRequested -= BattleLogic.UndoCommand;
        Presenter.EscapeRequested -= BattleLogic.Flee;
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
        Presenter.Render(BuildScreen(null));
    }

    private void ShowCommand(BattlerId battlerId)
    {
        Presenter.ShowCommandPrompt(
            BuildScreen(battlerId),
            BuildPrompt(battlerId)
        );
    }

    private void PlayCues(
        long cueBatchId,
        IReadOnlyList<BattleCue> cues
    )
    {
        Presenter.PlayCueBatch(
            BuildScreen(null),
            cues,
            () => BattleLogic.AcknowledgeCuePlayback(cueBatchId)
        );
    }

    private void CompleteBattle()
    {
        Presenter.Render(BuildScreen(null));
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

    private BattleScreenView BuildScreen(BattlerId? activeActorId)
    {
        var snapshot = BattleRepo.Snapshot();
        return new BattleScreenView(
            snapshot.Turn,
            activeActorId,
            snapshot.Units
                .Select(unit => new BattleUnitViewModel(
                    unit.Id,
                    unit.Name,
                    unit.Team,
                    unit.Position,
                    unit.Hp,
                    unit.Stats.MaxHp,
                    unit.Tp,
                    unit.Stats.MaxTp,
                    unit.IsAlive
                ))
                .ToArray()
        );
    }

    private BattleCommandPrompt BuildPrompt(BattlerId actorId)
    {
        var snapshot = BattleRepo.Snapshot();
        var actor = snapshot.Units.First(unit => unit.Id == actorId);
        var options = actor.ActionIds
            .Select(actionId => BuildActionOption(actorId, snapshot, actionId))
            .Where(option => option is not null)
            .Cast<BattleActionOption>()
            .ToArray();
        return new BattleCommandPrompt(
            actorId,
            options.FirstOrDefault(option =>
                option.ActionId == BattleContent.BasicAttackId),
            options
                .Where(option =>
                    option.ActionId != BattleContent.BasicAttackId)
                .ToArray()
        );
    }

    private BattleActionOption? BuildActionOption(
        BattlerId actorId,
        BattleSnapshot snapshot,
        ActionId actionId
    )
    {
        if (!BattleRepo.Catalog.TryGetAction(actionId, out var action))
        {
            return null;
        }

        var targets = BattleRepo.GetValidTargets(actorId, actionId)
            .Select(id =>
            {
                var unit = snapshot.Units.First(candidate =>
                    candidate.Id == id);
                return new BattleTargetOption(id, unit.Name);
            })
            .ToArray();
        if (targets.Length == 0)
        {
            return null;
        }

        return new BattleActionOption(
            action.Id,
            action.Name,
            action.TpCost,
            targets
        );
    }

    private void HideBattle()
    {
        Presenter.Cancel();
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
    }
}
