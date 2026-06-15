namespace Labyrinth;

using System;
using System.Collections.Generic;

public enum BattleDomainPhase
{
    Disabled,
    SelectingCommands,
    ResolvingTurn,
    AwaitingCuePlayback,
    Completed,
}

public enum BattleOutcome
{
    Victory,
    Defeat,
    Fled,
}

/// <summary>
/// Identifies the external boundary reached by battle resolution.
/// </summary>
public enum BattleAdvanceKind
{
    CommandRequired,
    CuePlaybackRequired,
    Completed,
}

public enum BattlePopupKind
{
    Damage,
    Heal,
    Tp,
}

/// <summary>
/// View-facing instruction emitted by domain resolution. Cues describe
/// presentation but never mutate battle state.
/// </summary>
public abstract record BattleCue;

public sealed record AnimationCue(
    string AnimationId,
    BattlerId SourceId,
    IReadOnlyList<BattlerId> TargetIds
) : BattleCue;

public sealed record BattlePopup(
    BattlerId TargetId,
    int Amount,
    BattlePopupKind Kind
);

public sealed record PopupBatchCue(IReadOnlyList<BattlePopup> Popups)
    : BattleCue;

public sealed record StatusCue(
    BattlerId TargetId,
    StatusId StatusId,
    bool Applied,
    int Stacks
) : BattleCue;

public sealed record WaitCue(double Seconds) : BattleCue;

public sealed record DeathCue(BattlerId TargetId) : BattleCue;

public sealed record BattleResult(
    BattleOutcome Outcome,
    EncounterId EncounterId,
    BattleReward Reward,
    IReadOnlyDictionary<BattlerId, (int Hp, int Tp)> PlayerVitals
);

/// <summary>
/// Result of advancing domain resolution to its next external boundary.
/// Cue batches must be played and acknowledged before resolution continues.
/// </summary>
public sealed record BattleAdvance
{
    public required BattleAdvanceKind Kind { get; init; }

    /// <summary>
    /// Identifier required to acknowledge a cue-playback request.
    /// </summary>
    public long CueBatchId { get; init; }

    /// <summary>
    /// Ordered view instructions for a cue-playback request.
    /// </summary>
    public IReadOnlyList<BattleCue> Cues { get; init; } = [];
    public BattlerId? RequestedActorId { get; init; }
    public BattleResult? Result { get; init; }

    public static BattleAdvance RequireCommand(BattlerId actorId) => new()
    {
        Kind = BattleAdvanceKind.CommandRequired,
        RequestedActorId = actorId,
    };

    public static BattleAdvance RequireCuePlayback(
        long cueBatchId,
        IReadOnlyList<BattleCue> cues
    ) => new()
    {
        Kind = BattleAdvanceKind.CuePlaybackRequired,
        CueBatchId = cueBatchId,
        Cues = cues,
    };

    public static BattleAdvance Complete(BattleResult result) => new()
    {
        Kind = BattleAdvanceKind.Completed,
        Result = result,
    };
}

/// <summary>
/// A player or enemy actor's selected action and optional target for a turn.
/// </summary>
public sealed record BattleCommand(
    BattlerId ActorId,
    ActionId ActionId,
    BattlerId? TargetId
);

public readonly record struct CommandValidationResult(
    bool IsValid,
    string Error
)
{
    public static CommandValidationResult Valid => new(true, "");
    public static CommandValidationResult Invalid(string error) =>
        new(false, error);
}

public sealed record BattleUnitView(
    BattlerId Id,
    string Name,
    BattleTeam Team,
    PartyPosition Position,
    BattleStats Stats,
    int Hp,
    int Tp,
    IReadOnlyList<ActionId> ActionIds
)
{
    public bool IsAlive => Hp > 0;
}

public sealed record BattleSnapshot(
    int Turn,
    BattleDomainPhase Phase,
    IReadOnlyList<BattleUnitView> Units
);

public interface IRandomSource
{
    double NextDouble();
    int Next(int maxExclusive);
}

public sealed class SeededRandomSource : IRandomSource
{
    private readonly Random _random;

    public SeededRandomSource(int seed) => _random = new Random(seed);

    public double NextDouble() => _random.NextDouble();
    public int Next(int maxExclusive) => _random.Next(maxExclusive);
}

/// <summary>
/// Selects a command for an enemy from a read-only battle snapshot.
/// </summary>
public interface IEnemyCommandPlanner
{
    BattleCommand? Plan(
        BattleSnapshot snapshot,
        BattlerId enemyActorId,
        BattleCatalog catalog,
        IRandomSource random
    );
}

public sealed class BasicEnemyCommandPlanner : IEnemyCommandPlanner
{
    public BattleCommand? Plan(
        BattleSnapshot snapshot,
        BattlerId enemyActorId,
        BattleCatalog catalog,
        IRandomSource random
    )
    {
        var actor = Find(snapshot, enemyActorId);
        if (actor is null || !actor.IsAlive)
        {
            return null;
        }

        foreach (var actionId in actor.ActionIds)
        {
            if (!catalog.TryGetAction(actionId, out var action))
            {
                continue;
            }
            if (actor.Tp < action.TpCost)
            {
                continue;
            }

            var candidates = TargetCandidates(snapshot, actor, action);
            if (candidates.Count == 0)
            {
                continue;
            }

            var selected = action.TargetRule is BattleTargetRule.Self
                or BattleTargetRule.AllAllies
                or BattleTargetRule.AllEnemies
                ? (BattlerId?)null
                : candidates[random.Next(candidates.Count)].Id;
            return new BattleCommand(enemyActorId, actionId, selected);
        }

        return null;
    }

    private static BattleUnitView? Find(
        BattleSnapshot snapshot,
        BattlerId id
    )
    {
        foreach (var unit in snapshot.Units)
        {
            if (unit.Id == id)
            {
                return unit;
            }
        }
        return null;
    }

    private static List<BattleUnitView> TargetCandidates(
        BattleSnapshot snapshot,
        BattleUnitView actor,
        BattleActionDefinition action
    )
    {
        var targetTeam = action.TargetRule switch
        {
            BattleTargetRule.Self => actor.Team,
            BattleTargetRule.SingleAlly => actor.Team,
            BattleTargetRule.RowAllies => actor.Team,
            BattleTargetRule.AllAllies => actor.Team,
            _ => actor.Team == BattleTeam.Player
                ? BattleTeam.Enemy
                : BattleTeam.Player,
        };

        var result = new List<BattleUnitView>();
        foreach (var unit in snapshot.Units)
        {
            if (!unit.IsAlive || unit.Team != targetTeam)
            {
                continue;
            }
            if (
                action.TargetRule == BattleTargetRule.Self
                && unit.Id != actor.Id
            )
            {
                continue;
            }
            result.Add(unit);
        }
        return result;
    }
}

/// <summary>
/// Owns authoritative runtime battle state and advances its operation queue.
/// The repository is independent of Godot controls and frame timing.
/// </summary>
public interface IBattleRepo : IDisposable
{
    BattleDomainPhase Phase { get; }
    BattleCatalog Catalog { get; }
    int Turn { get; }
    BattlerId? RequestedPlayerId { get; }
    BattleResult? Result { get; }

    void Start(BattleSetup setup);
    CommandValidationResult ValidateCommand(BattleCommand command);
    bool SubmitCommand(BattleCommand command);
    bool UndoLastCommand();
    IReadOnlyList<BattlerId> GetValidTargets(
        BattlerId actorId,
        ActionId actionId
    );
    void BeginResolution(IEnemyCommandPlanner enemyCommandPlanner);

    /// <summary>
    /// Executes queued operations until command input, cue playback, or battle
    /// completion requires the caller to act.
    /// </summary>
    BattleAdvance AdvanceResolution();

    /// <summary>
    /// Resumes resolution after the current cue batch has finished playing.
    /// </summary>
    /// <param name="cueBatchId">
    /// The exact ID returned by the pending cue-playback advance.
    /// </param>
    void AcknowledgeCuePlayback(long cueBatchId);
    BattleAdvance Flee();
    BattleSnapshot Snapshot();
}
