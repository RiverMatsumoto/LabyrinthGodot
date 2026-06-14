namespace Labyrinth;

using System;
using System.Collections.Generic;

public enum BattleDomainPhase
{
    Disabled,
    SelectingCommands,
    ResolvingTurn,
    AwaitingPresentation,
    Completed,
}

public enum BattleOutcome
{
    Victory,
    Defeat,
    Fled,
}

public enum BattleStepKind
{
    CommandSelection,
    Presentation,
    Completed,
}

public enum BattlePopupKind
{
    Damage,
    Heal,
    Tp,
}

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
    GameMode ReturnMode,
    BattleReward Reward,
    IReadOnlyDictionary<BattlerId, (int Hp, int Tp)> PlayerVitals
);

public sealed record BattleStep(
    BattleStepKind Kind,
    long PresentationId = 0,
    IReadOnlyList<BattleCue>? CueList = null,
    BattlerId? RequestedBattlerId = null,
    BattleResult? Result = null
)
{
    public IReadOnlyList<BattleCue> Cues => CueList ?? [];

    public static BattleStep Select(BattlerId id) =>
        new(BattleStepKind.CommandSelection, RequestedBattlerId: id);

    public static BattleStep Present(
        long id,
        IReadOnlyList<BattleCue> cues
    ) => new(BattleStepKind.Presentation, id, cues);

    public static BattleStep Complete(BattleResult result) =>
        new(BattleStepKind.Completed, Result: result);
}

public sealed record BattleIntent(
    BattlerId SourceId,
    ActionId ActionId,
    BattlerId? SelectedTargetId
);

public readonly record struct IntentValidationResult(
    bool IsValid,
    string Error
)
{
    public static IntentValidationResult Valid => new(true, "");
    public static IntentValidationResult Invalid(string error) =>
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

public interface IEnemyIntentProvider
{
    BattleIntent? Plan(
        BattleSnapshot snapshot,
        BattlerId enemyId,
        BattleCatalog catalog,
        IRandomSource random
    );
}

public sealed class BasicEnemyIntentProvider : IEnemyIntentProvider
{
    public BattleIntent? Plan(
        BattleSnapshot snapshot,
        BattlerId enemyId,
        BattleCatalog catalog,
        IRandomSource random
    )
    {
        var source = Find(snapshot, enemyId);
        if (source is null || !source.IsAlive)
        {
            return null;
        }

        foreach (var actionId in source.ActionIds)
        {
            if (!catalog.TryGetAction(actionId, out var action))
            {
                continue;
            }

            var candidates = TargetCandidates(snapshot, source, action);
            if (candidates.Count == 0)
            {
                continue;
            }

            var selected = action.TargetRule is BattleTargetRule.Self
                or BattleTargetRule.AllAllies
                or BattleTargetRule.AllEnemies
                ? (BattlerId?)null
                : candidates[random.Next(candidates.Count)].Id;
            return new BattleIntent(enemyId, actionId, selected);
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
        BattleUnitView source,
        BattleActionDefinition action
    )
    {
        var targetTeam = action.TargetRule switch
        {
            BattleTargetRule.Self => source.Team,
            BattleTargetRule.SingleAlly => source.Team,
            BattleTargetRule.RowAllies => source.Team,
            BattleTargetRule.AllAllies => source.Team,
            _ => source.Team == BattleTeam.Player
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
                && unit.Id != source.Id
            )
            {
                continue;
            }
            result.Add(unit);
        }
        return result;
    }
}

public interface IBattleRepo : IDisposable
{
    BattleDomainPhase Phase { get; }
    BattleCatalog Catalog { get; }
    int Turn { get; }
    BattlerId? RequestedPlayerId { get; }
    BattleResult? Result { get; }

    void Start(BattleSetup setup);
    IntentValidationResult ValidateIntent(BattleIntent intent);
    bool SubmitIntent(BattleIntent intent);
    bool UndoLastIntent();
    IReadOnlyList<BattlerId> GetValidTargets(
        BattlerId sourceId,
        ActionId actionId
    );
    void BeginResolution(IEnemyIntentProvider enemyIntentProvider);
    BattleStep Advance();
    void AcknowledgePresentation(long presentationId);
    BattleStep Flee();
    BattleSnapshot Snapshot();
}
