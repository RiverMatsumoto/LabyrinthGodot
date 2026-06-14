namespace Labyrinth;

using System;
using System.Collections.Generic;
using System.Linq;

public sealed class BattleRepo : IBattleRepo
{
    public const int MaxReactionDepth = 16;
    public const double BackRowMeleeMultiplier = 0.5;

    private readonly Dictionary<BattlerId, BattleUnit> _units = [];
    private readonly Dictionary<BattlerId, BattleIntent> _playerIntents = [];
    private readonly List<BattlerId> _intentOrder = [];
    private readonly LinkedList<Operation> _operations = [];
    private readonly List<ReactionInvocation> _afterActionReactions = [];
    private readonly List<ReactionInvocation> _endTurnReactions = [];
    private readonly List<RuntimeReaction> _reactions = [];
    private readonly HashSet<(long RegistrationId, long CauseId)>
        _reactionGuards = [];
    private readonly HashSet<BattlerId> _handledDeaths = [];

    private BattleSetup? _setup;
    private IRandomSource _random = new SeededRandomSource(1);
    private long _nextPresentationId;
    private long _awaitingPresentationId;
    private long _nextCauseId;
    private long _nextReactionRegistrationId;
    private BattleResult? _result;
    private bool _disposed;

    public BattleRepo(BattleCatalog catalog)
    {
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public BattleDomainPhase Phase { get; private set; } =
        BattleDomainPhase.Disabled;
    public BattleCatalog Catalog { get; }
    public int Turn { get; private set; }
    public BattlerId? RequestedPlayerId => FindRequestedPlayer();
    public BattleResult? Result => _result;

    public void Start(BattleSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ThrowIfDisposed();
        Reset();

        if (setup.Players.Count is < 1 or > PartyRepo.MaxMembers)
        {
            throw new ArgumentException(
                "Battle requires one to five player battlers.",
                nameof(setup)
            );
        }
        if (setup.Enemies.Count == 0)
        {
            throw new ArgumentException(
                "Battle requires at least one enemy.",
                nameof(setup)
            );
        }

        _setup = setup;
        _random = new SeededRandomSource(setup.Seed);
        foreach (var seed in setup.Players.Concat(setup.Enemies))
        {
            if (!_units.TryAdd(seed.Id, new BattleUnit(seed)))
            {
                throw new ArgumentException(
                    $"Duplicate battler id '{seed.Id}'.",
                    nameof(setup)
                );
            }

            foreach (var actionId in seed.ActionIds)
            {
                _ = Catalog.GetAction(actionId);
            }
        }

        Turn = 1;
        Phase = BattleDomainPhase.SelectingCommands;
    }

    public IntentValidationResult ValidateIntent(BattleIntent intent)
    {
        ThrowIfDisposed();
        if (Phase != BattleDomainPhase.SelectingCommands)
        {
            return IntentValidationResult.Invalid(
                "Battle is not selecting commands."
            );
        }
        if (
            !_units.TryGetValue(intent.SourceId, out var source)
            || source.Team != BattleTeam.Player
            || !source.IsAlive
        )
        {
            return IntentValidationResult.Invalid(
                "Intent source is not a living player battler."
            );
        }
        if (
            !Catalog.TryGetAction(intent.ActionId, out var action)
            || !source.ActionIds.Contains(intent.ActionId)
        )
        {
            return IntentValidationResult.Invalid(
                "Battler does not know the requested action."
            );
        }
        if (source.Tp < action.TpCost)
        {
            return IntentValidationResult.Invalid("Not enough TP.");
        }

        var targets = ResolveTargets(source, action, intent.SelectedTargetId);
        return targets.Count > 0
            ? IntentValidationResult.Valid
            : IntentValidationResult.Invalid(
                "The action has no valid targets."
            );
    }

    public bool SubmitIntent(BattleIntent intent)
    {
        var validation = ValidateIntent(intent);
        if (!validation.IsValid)
        {
            return false;
        }

        if (!_playerIntents.ContainsKey(intent.SourceId))
        {
            _intentOrder.Add(intent.SourceId);
        }
        _playerIntents[intent.SourceId] = intent;
        return true;
    }

    public bool UndoLastIntent()
    {
        ThrowIfDisposed();
        if (
            Phase != BattleDomainPhase.SelectingCommands
            || _intentOrder.Count == 0
        )
        {
            return false;
        }

        var id = _intentOrder[^1];
        _intentOrder.RemoveAt(_intentOrder.Count - 1);
        return _playerIntents.Remove(id);
    }

    public IReadOnlyList<BattlerId> GetValidTargets(
        BattlerId sourceId,
        ActionId actionId
    )
    {
        ThrowIfDisposed();
        if (
            !_units.TryGetValue(sourceId, out var source)
            || !Catalog.TryGetAction(actionId, out var action)
        )
        {
            return [];
        }

        return GetTargetCandidates(source, action)
            .Select(unit => unit.Id)
            .ToArray();
    }

    public void BeginResolution(IEnemyIntentProvider enemyIntentProvider)
    {
        ArgumentNullException.ThrowIfNull(enemyIntentProvider);
        ThrowIfDisposed();
        if (Phase != BattleDomainPhase.SelectingCommands)
        {
            throw new InvalidOperationException(
                "Battle is not selecting commands."
            );
        }
        if (FindRequestedPlayer() is not null)
        {
            throw new InvalidOperationException(
                "Every living player requires an intent."
            );
        }

        var intents = new List<BattleIntent>(_playerIntents.Values);
        var snapshot = Snapshot();
        foreach (var enemy in OrderedUnits(BattleTeam.Enemy))
        {
            if (!enemy.IsAlive)
            {
                continue;
            }
            var intent = enemyIntentProvider.Plan(
                snapshot,
                enemy.Id,
                Catalog,
                _random
            );
            if (intent is not null)
            {
                intents.Add(intent);
            }
        }

        var ordered = intents
            .Where(intent => _units.ContainsKey(intent.SourceId))
            .OrderByDescending(intent =>
                Catalog.GetAction(intent.ActionId).Priority)
            .ThenByDescending(intent =>
                _units[intent.SourceId].Stats.Agility)
            .ThenBy(intent => intent.SourceId.Value, StringComparer.Ordinal)
            .ToArray();

        _operations.Clear();
        _operations.AddLast(new WindowOperation(new ReactionEvent(
            NextCauseId(),
            ReactionWindow.TurnStart,
            default,
            null,
            0
        )));
        foreach (var intent in ordered)
        {
            _operations.AddLast(new ExecuteActionOperation(intent));
        }
        _operations.AddLast(new StatusTickOperation());
        _operations.AddLast(new WindowOperation(new ReactionEvent(
            NextCauseId(),
            ReactionWindow.TurnEnd,
            default,
            null,
            0
        )));
        _operations.AddLast(new FlushEndTurnReactionsOperation());
        _operations.AddLast(new ExpireStatusesOperation());
        _operations.AddLast(new FinishTurnOperation());
        Phase = BattleDomainPhase.ResolvingTurn;
    }

    public BattleStep Advance()
    {
        ThrowIfDisposed();
        if (Phase == BattleDomainPhase.AwaitingPresentation)
        {
            throw new InvalidOperationException(
                "Presentation must be acknowledged before advancing."
            );
        }
        if (Phase == BattleDomainPhase.Completed)
        {
            return BattleStep.Complete(
                _result ?? throw new InvalidOperationException(
                    "Completed battle has no result."
                )
            );
        }
        if (Phase == BattleDomainPhase.SelectingCommands)
        {
            return BattleStep.Select(
                FindRequestedPlayer()
                ?? throw new InvalidOperationException(
                    "All player commands are already selected."
                )
            );
        }
        if (Phase != BattleDomainPhase.ResolvingTurn)
        {
            throw new InvalidOperationException("Battle is not running.");
        }

        while (_operations.First is not null)
        {
            var operation = _operations.First.Value;
            _operations.RemoveFirst();

            if (operation is CueOperation cue)
            {
                _awaitingPresentationId = ++_nextPresentationId;
                Phase = BattleDomainPhase.AwaitingPresentation;
                return BattleStep.Present(
                    _awaitingPresentationId,
                    cue.Cues
                );
            }

            Execute(operation);
            if (Phase == BattleDomainPhase.Completed)
            {
                return BattleStep.Complete(_result!);
            }
            if (Phase == BattleDomainPhase.SelectingCommands)
            {
                return BattleStep.Select(FindRequestedPlayer()!.Value);
            }
        }

        throw new InvalidOperationException(
            "Resolution ended without a terminal operation."
        );
    }

    public void AcknowledgePresentation(long presentationId)
    {
        ThrowIfDisposed();
        if (
            Phase != BattleDomainPhase.AwaitingPresentation
            || presentationId != _awaitingPresentationId
        )
        {
            throw new InvalidOperationException(
                "Presentation acknowledgement does not match."
            );
        }

        _awaitingPresentationId = 0;
        Phase = BattleDomainPhase.ResolvingTurn;
    }

    public BattleStep Flee()
    {
        ThrowIfDisposed();
        if (
            Phase is BattleDomainPhase.Disabled
                or BattleDomainPhase.Completed
        )
        {
            throw new InvalidOperationException(
                "Cannot flee from the current battle phase."
            );
        }

        Complete(BattleOutcome.Fled);
        return BattleStep.Complete(_result!);
    }

    public BattleSnapshot Snapshot() => new(
        Turn,
        Phase,
        _units.Values
            .OrderBy(unit => unit.Team)
            .ThenBy(unit => unit.Position.Row)
            .ThenBy(unit => unit.Position.Index)
            .ThenBy(unit => unit.Id.Value, StringComparer.Ordinal)
            .Select(unit => unit.View())
            .ToArray()
    );

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        Reset();
        _disposed = true;
    }

    private void Execute(Operation operation)
    {
        switch (operation)
        {
            case ExecuteActionOperation execute:
                ExecuteAction(execute.Intent);
                break;
            case WindowOperation window:
                TriggerReactions(window.Event);
                break;
            case DamageOperation damage:
                ApplyDamage(damage);
                break;
            case HealOperation heal:
                ApplyHealing(heal);
                break;
            case ModifyResourceOperation modify:
                ModifyResource(modify);
                break;
            case ApplyStatusOperation apply:
                ApplyStatus(apply);
                break;
            case RemoveStatusOperation remove:
                RemoveStatus(remove);
                break;
            case RegisterReactionOperation register:
                RegisterReaction(register);
                break;
            case ActionEndOperation actionEnd:
                EndAction(actionEnd);
                break;
            case FlushAfterActionReactionsOperation:
                FlushReactions(_afterActionReactions);
                break;
            case StatusTickOperation:
                TickStatuses();
                break;
            case FlushEndTurnReactionsOperation:
                FlushReactions(_endTurnReactions);
                break;
            case ExpireStatusesOperation:
                ExpireStatuses();
                break;
            case DeathCheckOperation deathCheck:
                CheckDeaths(deathCheck);
                break;
            case FinishTurnOperation:
                FinishTurn();
                break;
            case CompleteOperation complete:
                Complete(complete.Outcome);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown battle operation '{operation.GetType().Name}'."
                );
        }
    }

    private void ExecuteAction(BattleIntent intent)
    {
        if (
            !_units.TryGetValue(intent.SourceId, out var source)
            || !source.IsAlive
            || !Catalog.TryGetAction(intent.ActionId, out var action)
            || source.Tp < action.TpCost
        )
        {
            return;
        }

        if (source.HasStatusBehavior(Catalog, StatusBehavior.Stun))
        {
            return;
        }

        var targets = ResolveTargets(source, action, intent.SelectedTargetId);
        if (targets.Count == 0)
        {
            return;
        }

        source.Tp -= action.TpCost;
        var context = new EffectContext(
            source.Id,
            targets.Select(target => target.Id).ToArray(),
            action.Range,
            0
        );
        var operations = new List<Operation>
        {
            new WindowOperation(new ReactionEvent(
                NextCauseId(),
                ReactionWindow.ActionStart,
                source.Id,
                targets[0].Id,
                0
            )),
        };
        foreach (var effect in action.Effects)
        {
            operations.AddRange(BuildEffectOperations(effect, context));
        }
        operations.Add(new ActionEndOperation(context));
        InsertFront(operations);
    }

    private IReadOnlyList<Operation> BuildEffectOperations(
        BattleEffectDefinition effect,
        EffectContext context
    )
    {
        var operations = new List<Operation>
        {
            new WindowOperation(new ReactionEvent(
                NextCauseId(),
                ReactionWindow.BeforeEffect,
                context.SourceId,
                context.TargetIds.FirstOrDefault(),
                context.ReactionDepth
            )),
        };

        switch (effect)
        {
            case DamageEffectDefinition damage:
                if (!string.IsNullOrWhiteSpace(damage.AnimationId))
                {
                    operations.Add(new CueOperation([
                        new AnimationCue(
                            damage.AnimationId,
                            context.SourceId,
                            context.TargetIds
                        ),
                    ]));
                }
                operations.Add(new DamageOperation(context, damage.Spec));
                break;
            case HealEffectDefinition heal:
                if (!string.IsNullOrWhiteSpace(heal.AnimationId))
                {
                    operations.Add(new CueOperation([
                        new AnimationCue(
                            heal.AnimationId,
                            context.SourceId,
                            context.TargetIds
                        ),
                    ]));
                }
                operations.Add(new HealOperation(context, heal.Amount));
                break;
            case ModifyResourceEffectDefinition modify:
                operations.Add(new ModifyResourceOperation(
                    context,
                    modify.Resource,
                    modify.Amount
                ));
                break;
            case ApplyStatusEffectDefinition apply:
                operations.Add(new ApplyStatusOperation(context, apply));
                break;
            case RemoveStatusEffectDefinition remove:
                operations.Add(new RemoveStatusOperation(
                    context,
                    remove.StatusId
                ));
                break;
            case PlayAnimationEffectDefinition animation:
                operations.Add(new CueOperation([
                    new AnimationCue(
                        animation.AnimationId,
                        context.SourceId,
                        context.TargetIds
                    ),
                ]));
                break;
            case WaitEffectDefinition wait:
                operations.Add(new CueOperation([
                    new WaitCue(Math.Max(0, wait.Seconds)),
                ]));
                break;
            case RegisterReactionEffectDefinition register:
                operations.Add(new RegisterReactionOperation(
                    context,
                    register.Reaction
                ));
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported effect '{effect.GetType().Name}'."
                );
        }

        operations.Add(new WindowOperation(new ReactionEvent(
            NextCauseId(),
            ReactionWindow.AfterEffect,
            context.SourceId,
            context.TargetIds.FirstOrDefault(),
            context.ReactionDepth
        )));
        return operations;
    }

    private void ApplyDamage(DamageOperation operation)
    {
        if (!_units.TryGetValue(operation.Context.SourceId, out var source))
        {
            return;
        }

        var popups = new List<BattlePopup>();
        var reactionEvents = new List<Operation>();
        foreach (var targetId in operation.Context.TargetIds)
        {
            if (!_units.TryGetValue(targetId, out var target) || !target.IsAlive)
            {
                continue;
            }
            var damage = ComputeDamage(
                source,
                target,
                operation.Spec,
                operation.Context.Range
            );
            target.Hp = Math.Max(0, target.Hp - damage);
            popups.Add(new BattlePopup(
                target.Id,
                damage,
                BattlePopupKind.Damage
            ));
            reactionEvents.Add(new WindowOperation(new ReactionEvent(
                NextCauseId(),
                ReactionWindow.Damage,
                source.Id,
                target.Id,
                operation.Context.ReactionDepth
            )));
        }

        var followUps = new List<Operation>();
        if (popups.Count > 0)
        {
            followUps.Add(new CueOperation([new PopupBatchCue(popups)]));
        }
        followUps.AddRange(reactionEvents);
        followUps.Add(new DeathCheckOperation(
            operation.Context.ReactionDepth
        ));
        InsertFront(followUps);
    }

    private void ApplyHealing(HealOperation operation)
    {
        var popups = new List<BattlePopup>();
        var reactionEvents = new List<Operation>();
        foreach (var targetId in operation.Context.TargetIds)
        {
            if (!_units.TryGetValue(targetId, out var target) || !target.IsAlive)
            {
                continue;
            }
            var amount = Math.Max(0, operation.Amount);
            var oldHp = target.Hp;
            target.Hp = Math.Min(target.Stats.MaxHp, target.Hp + amount);
            var healed = target.Hp - oldHp;
            popups.Add(new BattlePopup(
                target.Id,
                healed,
                BattlePopupKind.Heal
            ));
            reactionEvents.Add(new WindowOperation(new ReactionEvent(
                NextCauseId(),
                ReactionWindow.Healing,
                operation.Context.SourceId,
                target.Id,
                operation.Context.ReactionDepth
            )));
        }

        var followUps = new List<Operation>();
        if (popups.Count > 0)
        {
            followUps.Add(new CueOperation([new PopupBatchCue(popups)]));
        }
        followUps.AddRange(reactionEvents);
        InsertFront(followUps);
    }

    private void ModifyResource(ModifyResourceOperation operation)
    {
        var popups = new List<BattlePopup>();
        foreach (var targetId in operation.Context.TargetIds)
        {
            if (!_units.TryGetValue(targetId, out var target))
            {
                continue;
            }
            if (operation.Resource == BattleResource.Hp)
            {
                var old = target.Hp;
                target.Hp = Math.Clamp(
                    target.Hp + operation.Amount,
                    0,
                    target.Stats.MaxHp
                );
                var delta = target.Hp - old;
                popups.Add(new BattlePopup(
                    target.Id,
                    Math.Abs(delta),
                    delta >= 0
                        ? BattlePopupKind.Heal
                        : BattlePopupKind.Damage
                ));
            }
            else
            {
                var old = target.Tp;
                target.Tp = Math.Clamp(
                    target.Tp + operation.Amount,
                    0,
                    target.Stats.MaxTp
                );
                popups.Add(new BattlePopup(
                    target.Id,
                    target.Tp - old,
                    BattlePopupKind.Tp
                ));
            }
        }
        if (popups.Count > 0)
        {
            InsertFront([
                new CueOperation([new PopupBatchCue(popups)]),
                new DeathCheckOperation(operation.Context.ReactionDepth),
            ]);
        }
    }

    private void ApplyStatus(ApplyStatusOperation operation)
    {
        var definition = Catalog.GetStatus(operation.Effect.StatusId);
        var followUps = new List<Operation>();
        foreach (var targetId in operation.Context.TargetIds)
        {
            if (!_units.TryGetValue(targetId, out var target) || !target.IsAlive)
            {
                continue;
            }
            target.Resistances.TryGetValue(definition.Id, out var resistance);
            var chance = Math.Clamp(
                operation.Effect.BaseChance * (1.0 - resistance),
                0.0,
                1.0
            );
            if (_random.NextDouble() >= chance)
            {
                continue;
            }

            var duration = operation.Effect.Duration > 0
                ? operation.Effect.Duration
                : definition.DefaultDuration;
            if (target.Statuses.TryGetValue(definition.Id, out var status))
            {
                status.Stacks = Math.Min(
                    definition.MaxStacks,
                    status.Stacks + Math.Max(1, operation.Effect.Stacks)
                );
                status.RemainingTurns = Math.Max(
                    status.RemainingTurns,
                    duration
                );
            }
            else
            {
                status = new RuntimeStatus(
                    definition.Id,
                    Math.Min(
                        definition.MaxStacks,
                        Math.Max(1, operation.Effect.Stacks)
                    ),
                    duration
                );
                target.Statuses.Add(definition.Id, status);
            }

            followUps.Add(new CueOperation([
                new StatusCue(
                    target.Id,
                    definition.Id,
                    Applied: true,
                    status.Stacks
                ),
            ]));
            followUps.Add(new WindowOperation(new ReactionEvent(
                NextCauseId(),
                ReactionWindow.StatusApplied,
                operation.Context.SourceId,
                target.Id,
                operation.Context.ReactionDepth
            )));
        }
        InsertFront(followUps);
    }

    private void RemoveStatus(RemoveStatusOperation operation)
    {
        var cues = new List<BattleCue>();
        foreach (var targetId in operation.Context.TargetIds)
        {
            if (
                _units.TryGetValue(targetId, out var target)
                && target.Statuses.Remove(operation.StatusId)
            )
            {
                cues.Add(new StatusCue(
                    target.Id,
                    operation.StatusId,
                    Applied: false,
                    0
                ));
            }
        }
        if (cues.Count > 0)
        {
            InsertFront([new CueOperation(cues)]);
        }
    }

    private void RegisterReaction(RegisterReactionOperation operation)
    {
        if (
            operation.Context.ReactionDepth >= MaxReactionDepth
            || string.IsNullOrWhiteSpace(operation.Definition.Id)
        )
        {
            return;
        }
        _reactions.Add(new RuntimeReaction(
            ++_nextReactionRegistrationId,
            operation.Context.SourceId,
            operation.Definition
        ));
    }

    private void TriggerReactions(ReactionEvent reactionEvent)
    {
        var matching = _reactions
            .Where(reaction =>
                reaction.Definition.Window == reactionEvent.Window
                && reaction.HasUses
                && _units.TryGetValue(reaction.OwnerId, out var owner)
                && owner.IsAlive)
            .OrderByDescending(reaction => reaction.Definition.Priority)
            .ThenBy(reaction => reaction.RegistrationId)
            .ToArray();

        var immediate = new List<ReactionInvocation>();
        foreach (var reaction in matching)
        {
            var guard = (reaction.RegistrationId, reactionEvent.CauseId);
            if (
                reactionEvent.Depth >= MaxReactionDepth
                || !_reactionGuards.Add(guard)
            )
            {
                continue;
            }

            reaction.Consume();
            var invocation = new ReactionInvocation(
                reaction,
                reactionEvent
            );
            switch (reaction.Definition.InsertionPolicy)
            {
                case ReactionInsertionPolicy.BeforeNextEffect:
                    immediate.Add(invocation);
                    break;
                case ReactionInsertionPolicy.AfterCurrentAction:
                    _afterActionReactions.Add(invocation);
                    break;
                case ReactionInsertionPolicy.EndOfTurn:
                    _endTurnReactions.Add(invocation);
                    break;
            }
        }

        FlushReactions(immediate);
        _reactions.RemoveAll(reaction => !reaction.HasUses);
    }

    private void FlushReactions(List<ReactionInvocation> reactions)
    {
        if (reactions.Count == 0)
        {
            return;
        }

        var operations = new List<Operation>();
        foreach (var invocation in reactions)
        {
            var targets = ResolveReactionTargets(invocation);
            if (targets.Count == 0)
            {
                continue;
            }
            var context = new EffectContext(
                invocation.Reaction.OwnerId,
                targets,
                BattleRange.None,
                invocation.Event.Depth + 1
            );
            foreach (var effect in invocation.Reaction.Definition.Effects)
            {
                operations.AddRange(BuildEffectOperations(effect, context));
            }
        }
        reactions.Clear();
        InsertFront(operations);
    }

    private IReadOnlyList<BattlerId> ResolveReactionTargets(
        ReactionInvocation invocation
    )
    {
        var target = invocation.Reaction.Definition.TargetPolicy switch
        {
            ReactionTargetPolicy.Owner => invocation.Reaction.OwnerId,
            ReactionTargetPolicy.EventSource =>
                invocation.Event.SourceId,
            ReactionTargetPolicy.EventTarget =>
                invocation.Event.TargetId,
            _ => null,
        };
        return target is { } id && _units.ContainsKey(id)
            ? [id]
            : [];
    }

    private void EndAction(ActionEndOperation operation)
    {
        InsertFront([
            new WindowOperation(new ReactionEvent(
                NextCauseId(),
                ReactionWindow.ActionEnd,
                operation.Context.SourceId,
                operation.Context.TargetIds.FirstOrDefault(),
                operation.Context.ReactionDepth
            )),
            new FlushAfterActionReactionsOperation(),
        ]);
    }

    private void TickStatuses()
    {
        var operations = new List<Operation>();
        foreach (var unit in _units.Values.Where(unit => unit.IsAlive))
        {
            foreach (var status in unit.Statuses.Values)
            {
                var definition = Catalog.GetStatus(status.Id);
                var amount = definition.PowerPerStack * status.Stacks;
                var context = new EffectContext(
                    unit.Id,
                    [unit.Id],
                    BattleRange.None,
                    0
                );
                if (
                    definition.Behavior == StatusBehavior.Poison
                    && amount > 0
                )
                {
                    operations.Add(new ModifyResourceOperation(
                        context,
                        BattleResource.Hp,
                        -amount
                    ));
                }
                else if (
                    definition.Behavior == StatusBehavior.Regen
                    && amount > 0
                )
                {
                    operations.Add(new HealOperation(context, amount));
                }
            }
        }
        InsertFront(operations);
    }

    private void ExpireStatuses()
    {
        var cues = new List<BattleCue>();
        foreach (var unit in _units.Values)
        {
            foreach (var status in unit.Statuses.Values.ToArray())
            {
                status.RemainingTurns--;
                if (status.RemainingTurns > 0)
                {
                    continue;
                }
                unit.Statuses.Remove(status.Id);
                cues.Add(new StatusCue(
                    unit.Id,
                    status.Id,
                    Applied: false,
                    0
                ));
            }
        }
        if (cues.Count > 0)
        {
            InsertFront([new CueOperation(cues)]);
        }
    }

    private void CheckDeaths(DeathCheckOperation operation)
    {
        var newlyDead = _units.Values
            .Where(unit => !unit.IsAlive && _handledDeaths.Add(unit.Id))
            .OrderBy(unit => unit.Team)
            .ThenBy(unit => unit.Id.Value, StringComparer.Ordinal)
            .ToArray();
        if (newlyDead.Length == 0)
        {
            return;
        }

        var operations = new List<Operation>
        {
            new CueOperation(
                newlyDead.Select(unit => (BattleCue)new DeathCue(unit.Id))
                    .ToArray()
            ),
        };
        operations.AddRange(newlyDead.Select(unit =>
            (Operation)new WindowOperation(new ReactionEvent(
                NextCauseId(),
                ReactionWindow.Defeat,
                unit.Id,
                unit.Id,
                operation.ReactionDepth
            ))
        ));

        var outcome = DetermineOutcome();
        if (outcome is not null)
        {
            _operations.Clear();
            operations.Add(new CompleteOperation(outcome.Value));
        }
        InsertFront(operations);
    }

    private void FinishTurn()
    {
        var outcome = DetermineOutcome();
        if (outcome is not null)
        {
            Complete(outcome.Value);
            return;
        }

        _playerIntents.Clear();
        _intentOrder.Clear();
        _reactionGuards.Clear();
        Turn++;
        Phase = BattleDomainPhase.SelectingCommands;
    }

    private void Complete(BattleOutcome outcome)
    {
        var setup = _setup ?? throw new InvalidOperationException(
            "Battle has not been started."
        );
        _operations.Clear();
        _result = new BattleResult(
            outcome,
            setup.EncounterId,
            setup.ReturnMode,
            setup.Reward,
            _units.Values
                .Where(unit => unit.Team == BattleTeam.Player)
                .ToDictionary(unit => unit.Id, unit => (unit.Hp, unit.Tp))
        );
        Phase = BattleDomainPhase.Completed;
    }

    private BattleOutcome? DetermineOutcome()
    {
        var playersAlive = _units.Values.Any(unit =>
            unit.Team == BattleTeam.Player && unit.IsAlive);
        var enemiesAlive = _units.Values.Any(unit =>
            unit.Team == BattleTeam.Enemy && unit.IsAlive);
        if (!enemiesAlive)
        {
            return BattleOutcome.Victory;
        }
        if (!playersAlive)
        {
            return BattleOutcome.Defeat;
        }
        return null;
    }

    private int ComputeDamage(
        BattleUnit source,
        BattleUnit target,
        DamageSpec spec,
        BattleRange range
    )
    {
        double damage;
        if (spec.Mode == DamageMode.Fixed)
        {
            damage = spec.Power;
        }
        else if (spec.Type == DamageType.True)
        {
            damage = spec.Power
                + source.Stats.Strength
                + source.Stats.Technique
                + source.Stats.Attack;
        }
        else if (
            spec.Type is DamageType.Fire
                or DamageType.Ice
                or DamageType.Lightning
        )
        {
            var offense = spec.Power
                + source.Stats.Technique
                + (source.Stats.Attack * 0.5);
            var defense = (target.Stats.Wisdom * 1.35)
                + (target.Stats.Technique * 0.65)
                + (target.Stats.Defense * 0.35);
            damage = offense * (offense / (offense + defense + 1.0));
        }
        else
        {
            var offense = spec.Power
                + source.Stats.Strength
                + source.Stats.Attack;
            var defense = target.Stats.Defense
                + (target.Stats.Vitality * 0.75);
            damage = offense * (offense / (offense + defense + 1.0));
        }

        if (
            range == BattleRange.Melee
            && (
                source.Position.Row == PartyRow.Back
                || target.Position.Row == PartyRow.Back
            )
        )
        {
            damage *= BackRowMeleeMultiplier;
        }

        if (spec.CanCrit)
        {
            var chance = Math.Clamp(
                0.05
                    + ((source.Stats.Luck - target.Stats.Luck) * 0.0025),
                0.01,
                0.5
            );
            if (_random.NextDouble() < chance)
            {
                damage *= spec.CritMultiplier;
            }
        }

        return Math.Max(0, (int)Math.Round(damage));
    }

    private List<BattleUnit> ResolveTargets(
        BattleUnit source,
        BattleActionDefinition action,
        BattlerId? selectedId
    )
    {
        var candidates = GetTargetCandidates(source, action);
        if (candidates.Count == 0)
        {
            return [];
        }

        if (action.TargetRule == BattleTargetRule.Self)
        {
            return [source];
        }
        if (
            action.TargetRule is BattleTargetRule.AllAllies
                or BattleTargetRule.AllEnemies
        )
        {
            return candidates;
        }

        var selected = selectedId is { } id
            ? candidates.FirstOrDefault(unit => unit.Id == id)
            : null;
        if (selected is null)
        {
            if (action.RetargetPolicy == RetargetPolicy.Fail)
            {
                return [];
            }
            selected = candidates[0];
        }

        if (
            action.TargetRule is BattleTargetRule.RowAllies
                or BattleTargetRule.RowEnemies
        )
        {
            return candidates
                .Where(unit => unit.Position.Row == selected.Position.Row)
                .ToList();
        }
        return [selected];
    }

    private List<BattleUnit> GetTargetCandidates(
        BattleUnit source,
        BattleActionDefinition action
    )
    {
        var team = action.TargetRule switch
        {
            BattleTargetRule.Self => source.Team,
            BattleTargetRule.SingleAlly => source.Team,
            BattleTargetRule.RowAllies => source.Team,
            BattleTargetRule.AllAllies => source.Team,
            _ => source.Team == BattleTeam.Player
                ? BattleTeam.Enemy
                : BattleTeam.Player,
        };

        return _units.Values
            .Where(unit =>
                unit.IsAlive
                && unit.Team == team
                && (
                    action.TargetRule != BattleTargetRule.Self
                    || unit.Id == source.Id
                ))
            .OrderBy(unit => unit.Position.Row)
            .ThenBy(unit => unit.Position.Index)
            .ThenBy(unit => unit.Id.Value, StringComparer.Ordinal)
            .ToList();
    }

    private BattlerId? FindRequestedPlayer()
    {
        foreach (var unit in OrderedUnits(BattleTeam.Player))
        {
            if (unit.IsAlive && !_playerIntents.ContainsKey(unit.Id))
            {
                return unit.Id;
            }
        }
        return null;
    }

    private IEnumerable<BattleUnit> OrderedUnits(BattleTeam team) =>
        _units.Values
            .Where(unit => unit.Team == team)
            .OrderBy(unit => unit.Position.Row)
            .ThenBy(unit => unit.Position.Index)
            .ThenBy(unit => unit.Id.Value, StringComparer.Ordinal);

    private void InsertFront(IEnumerable<Operation> operations)
    {
        foreach (var operation in operations.Reverse())
        {
            _operations.AddFirst(operation);
        }
    }

    private long NextCauseId() => ++_nextCauseId;

    private void Reset()
    {
        _units.Clear();
        _playerIntents.Clear();
        _intentOrder.Clear();
        _operations.Clear();
        _afterActionReactions.Clear();
        _endTurnReactions.Clear();
        _reactions.Clear();
        _reactionGuards.Clear();
        _handledDeaths.Clear();
        _setup = null;
        _result = null;
        _nextPresentationId = 0;
        _awaitingPresentationId = 0;
        _nextCauseId = 0;
        _nextReactionRegistrationId = 0;
        Turn = 0;
        Phase = BattleDomainPhase.Disabled;
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);

    private sealed class BattleUnit
    {
        public BattleUnit(BattleBattlerSeed seed)
        {
            Id = seed.Id;
            Name = seed.Name;
            Team = seed.Team;
            Position = seed.Position;
            Stats = seed.Stats;
            Hp = Math.Clamp(seed.Hp, 0, seed.Stats.MaxHp);
            Tp = Math.Clamp(seed.Tp, 0, seed.Stats.MaxTp);
            ActionIds = seed.ActionIds.ToArray();
            Resistances = new Dictionary<StatusId, double>(
                seed.Resistances
            );
        }

        public BattlerId Id { get; }
        public string Name { get; }
        public BattleTeam Team { get; }
        public PartyPosition Position { get; }
        public BattleStats Stats { get; }
        public int Hp { get; set; }
        public int Tp { get; set; }
        public IReadOnlyList<ActionId> ActionIds { get; }
        public Dictionary<StatusId, double> Resistances { get; }
        public Dictionary<StatusId, RuntimeStatus> Statuses { get; } = [];
        public bool IsAlive => Hp > 0;

        public bool HasStatusBehavior(
            BattleCatalog catalog,
            StatusBehavior behavior
        ) => Statuses.Keys.Any(id =>
            catalog.GetStatus(id).Behavior == behavior);

        public BattleUnitView View() => new(
            Id,
            Name,
            Team,
            Position,
            Stats,
            Hp,
            Tp,
            ActionIds
        );
    }

    private sealed class RuntimeStatus(
        StatusId id,
        int stacks,
        int remainingTurns
    )
    {
        public StatusId Id { get; } = id;
        public int Stacks { get; set; } = stacks;
        public int RemainingTurns { get; set; } = remainingTurns;
    }

    private sealed class RuntimeReaction(
        long registrationId,
        BattlerId ownerId,
        ReactionDefinition definition
    )
    {
        public long RegistrationId { get; } = registrationId;
        public BattlerId OwnerId { get; } = ownerId;
        public ReactionDefinition Definition { get; } = definition;
        public int RemainingUses { get; private set; } = definition.Uses;
        public bool HasUses => RemainingUses != 0;

        public void Consume()
        {
            if (RemainingUses > 0)
            {
                RemainingUses--;
            }
        }
    }

    private sealed record EffectContext(
        BattlerId SourceId,
        IReadOnlyList<BattlerId> TargetIds,
        BattleRange Range,
        int ReactionDepth
    );

    private sealed record ReactionEvent(
        long CauseId,
        ReactionWindow Window,
        BattlerId? SourceId,
        BattlerId? TargetId,
        int Depth
    );

    private sealed record ReactionInvocation(
        RuntimeReaction Reaction,
        ReactionEvent Event
    );

    private abstract record Operation;
    private sealed record CueOperation(IReadOnlyList<BattleCue> Cues)
        : Operation;
    private sealed record ExecuteActionOperation(BattleIntent Intent)
        : Operation;
    private sealed record WindowOperation(ReactionEvent Event) : Operation;
    private sealed record DamageOperation(
        EffectContext Context,
        DamageSpec Spec
    ) : Operation;
    private sealed record HealOperation(
        EffectContext Context,
        int Amount
    ) : Operation;
    private sealed record ModifyResourceOperation(
        EffectContext Context,
        BattleResource Resource,
        int Amount
    ) : Operation;
    private sealed record ApplyStatusOperation(
        EffectContext Context,
        ApplyStatusEffectDefinition Effect
    ) : Operation;
    private sealed record RemoveStatusOperation(
        EffectContext Context,
        StatusId StatusId
    ) : Operation;
    private sealed record RegisterReactionOperation(
        EffectContext Context,
        ReactionDefinition Definition
    ) : Operation;
    private sealed record ActionEndOperation(EffectContext Context)
        : Operation;
    private sealed record FlushAfterActionReactionsOperation : Operation;
    private sealed record StatusTickOperation : Operation;
    private sealed record FlushEndTurnReactionsOperation : Operation;
    private sealed record ExpireStatusesOperation : Operation;
    private sealed record DeathCheckOperation(int ReactionDepth) : Operation;
    private sealed record FinishTurnOperation : Operation;
    private sealed record CompleteOperation(BattleOutcome Outcome)
        : Operation;
}
