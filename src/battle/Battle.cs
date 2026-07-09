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
        var queuedCommands = snapshot.SubmittedPlayerCommands
            .ToDictionary(
                command => command.ActorId,
                command => BattleRepo.Catalog.TryGetAction(
                    command.ActionId,
                    out var action
                )
                    ? action.Name
                    : command.ActionId.Value
            );
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
                    unit.IsAlive,
                    queuedCommands.GetValueOrDefault(unit.Id)
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

        var actor = snapshot.Units.First(unit => unit.Id == actorId);
        var targets = BuildTargetOptions(actor, snapshot, action);
        var disabledReason = "";
        if (actor.Tp < action.TpCost)
        {
            disabledReason = "Not enough TP.";
        }
        else if (targets.Length == 0)
        {
            disabledReason = "No valid targets.";
        }

        return new BattleActionOption(
            action.Id,
            action.Name,
            action.TargetRule,
            action.TpCost,
            targets,
            string.IsNullOrEmpty(disabledReason),
            disabledReason
        );
    }

    private BattleTargetOption[] BuildTargetOptions(
        BattleUnitView actor,
        BattleSnapshot snapshot,
        BattleActionDefinition action
    )
    {
        var validIds = BattleRepo
            .GetValidTargets(actor.Id, action.Id)
            .ToHashSet();
        var candidates = snapshot.Units
            .Where(unit => validIds.Contains(unit.Id))
            .OrderBy(unit => unit.Position.Row)
            .ThenBy(unit => unit.Position.Index)
            .ThenBy(unit => unit.Id.Value, StringComparer.Ordinal)
            .ToArray();

        if (candidates.Length == 0)
        {
            return [];
        }

        if (action.TargetRule == BattleTargetRule.Self)
        {
            return
            [
                new BattleTargetOption(
                    "Self",
                    null,
                    [actor.Id],
                    [actor.Id],
                    actor.Team,
                    GridPoint(actor)
                ),
            ];
        }

        if (
            action.TargetRule is BattleTargetRule.AllAllies
                or BattleTargetRule.AllEnemies
        )
        {
            return
            [
                new BattleTargetOption(
                    action.TargetRule == BattleTargetRule.AllAllies
                        ? "All Allies"
                        : "All Enemies",
                    null,
                    candidates.Select(unit => unit.Id).ToArray(),
                    candidates.Select(unit => unit.Id).ToArray(),
                    candidates[0].Team,
                    GridPoint(candidates[0])
                ),
            ];
        }

        if (
            action.TargetRule is BattleTargetRule.RowAllies
                or BattleTargetRule.RowEnemies
        )
        {
            return candidates
                .GroupBy(unit => unit.Position.Row)
                .Select(group =>
                {
                    var affected = group
                        .OrderBy(unit => unit.Position.Index)
                        .ThenBy(unit => unit.Id.Value, StringComparer.Ordinal)
                        .ToArray();
                    var anchor = affected[0];
                    return new BattleTargetOption(
                        $"{anchor.Position.Row} Row",
                        anchor.Id,
                        [anchor.Id],
                        affected.Select(unit => unit.Id).ToArray(),
                        anchor.Team,
                        GridPoint(anchor)
                    );
                })
                .ToArray();
        }

        return candidates
            .Select(unit => new BattleTargetOption(
                unit.Name,
                unit.Id,
                [unit.Id],
                [unit.Id],
                unit.Team,
                GridPoint(unit)
            ))
            .ToArray();
    }

    private static BattleTargetGridPoint GridPoint(BattleUnitView unit) =>
        new(unit.Team, unit.Position.Row, unit.Position.Index);

    private void HideBattle()
    {
        Presenter.Cancel();
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
    }
}
