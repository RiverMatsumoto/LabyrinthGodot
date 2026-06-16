namespace Labyrinth;

using System;
using System.Collections.Generic;

/// <summary>
/// Public battle-domain facade and composition root for the runtime resolver.
/// </summary>
public sealed class BattleRepo : IBattleRepo
{
    public const int MaxReactiveEffectDepth =
        BattleRules.MaxReactiveEffectDepth;
    public const double BackRowMeleeMultiplier =
        BattleRules.BackRowMeleeMultiplier;

    private readonly BattleRuntime _runtime;
    private readonly BattleCommandService _commands;
    private readonly BattleTargetResolver _targeting;
    private readonly BattleResolutionEngine _resolution;
    private readonly BattleOutcomeResolver _outcome;
    private bool _disposed;

    public BattleRepo(BattleCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        _runtime = new BattleRuntime(catalog);
        _targeting = new BattleTargetResolver(_runtime);
        _commands = new BattleCommandService(_runtime, _targeting);

        var effects = new BattleEffectOperationBuilder(_runtime);
        var reactiveEffects = new BattleReactiveEffectResolver(
            _runtime,
            effects,
            _targeting
        );
        _outcome = new BattleOutcomeResolver(_runtime);

        var damage = new BattleDamageSystem(_runtime);
        var healing = new BattleHealingSystem(_runtime);
        var resources = new BattleResourceSystem(_runtime);
        var statuses = new BattleStatusSystem(_runtime, reactiveEffects);
        var deaths = new BattleDeathSystem(_runtime, _outcome);
        var executor = new BattleOperationExecutor(
            _runtime,
            _targeting,
            effects,
            reactiveEffects,
            _outcome,
            damage,
            healing,
            resources,
            statuses,
            deaths
        );

        _resolution = new BattleResolutionEngine(
            _runtime,
            _commands,
            reactiveEffects,
            executor
        );
    }

    public BattleDomainPhase Phase => _runtime.Phase;
    public BattleCatalog Catalog => _runtime.Catalog;
    public int Turn => _runtime.Turn;
    public BattlerId? RequestedPlayerId =>
        _commands.RequestedPlayerId;
    public BattleResult? Result => _runtime.Result;

    public void Start(BattleSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ThrowIfDisposed();
        _resolution.Start(setup);
    }

    public CommandValidationResult ValidateCommand(
        BattleCommand command
    )
    {
        ThrowIfDisposed();
        return _commands.Validate(command);
    }

    public bool SubmitCommand(BattleCommand command)
    {
        ThrowIfDisposed();
        return _commands.Submit(command);
    }

    public bool UndoLastCommand()
    {
        ThrowIfDisposed();
        return _commands.UndoLast();
    }

    public IReadOnlyList<BattlerId> GetValidTargets(
        BattlerId actorId,
        ActionId actionId
    )
    {
        ThrowIfDisposed();
        return _targeting.GetValidTargets(actorId, actionId);
    }

    public void BeginResolution(
        IEnemyCommandPlanner enemyCommandPlanner
    )
    {
        ArgumentNullException.ThrowIfNull(enemyCommandPlanner);
        ThrowIfDisposed();
        _resolution.BeginResolution(enemyCommandPlanner);
    }

    public BattleAdvance AdvanceResolution()
    {
        ThrowIfDisposed();
        return _resolution.AdvanceResolution();
    }

    public void AcknowledgeCuePlayback(long cueBatchId)
    {
        ThrowIfDisposed();
        _resolution.AcknowledgeCuePlayback(cueBatchId);
    }

    public BattleAdvance Flee()
    {
        ThrowIfDisposed();
        return _outcome.Flee();
    }

    public BattleSnapshot Snapshot() => _runtime.Snapshot();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _runtime.Reset();
        _disposed = true;
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);
}
