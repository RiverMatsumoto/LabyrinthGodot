namespace Labyrinth;

using System.Collections.Generic;
using System.Linq;

internal sealed class BattleRuntime
{
    private long _nextCueBatchId;
    private long _nextCauseId;
    private long _nextReactiveEffectRegistrationId;

    public BattleRuntime(BattleCatalog catalog)
    {
        Catalog = catalog;
    }

    public BattleCatalog Catalog { get; }
    public Dictionary<BattlerId, BattleUnit> Units { get; } = [];
    public Dictionary<BattlerId, BattleCommand> PlayerCommands { get; } = [];
    public List<BattlerId> CommandOrder { get; } = [];
    public LinkedList<BattleOperation> Operations { get; } = [];
    public List<ReactiveEffectInvocation> AfterActionReactiveEffects { get; } = [];
    public List<ReactiveEffectInvocation> EndTurnReactiveEffects { get; } = [];
    public List<RuntimeReactiveEffect> ReactiveEffects { get; } = [];
    public HashSet<(long RegistrationId, long CauseId)> ReactiveEffectGuards
        { get; } = [];
    public HashSet<BattlerId> HandledDeaths { get; } = [];

    public BattleSetup? Setup { get; set; }
    public IRandomSource Random { get; set; } = new SeededRandomSource(1);
    public BattleDomainPhase Phase { get; set; } =
        BattleDomainPhase.Disabled;
    public BattleResult? Result { get; set; }
    public int Turn { get; set; }
    public long AwaitingCueBatchId { get; set; }
    public bool AfterActionFlushStarted { get; set; }
    public bool EndTurnFlushStarted { get; set; }

    public long NextCueBatchId() => ++_nextCueBatchId;
    public long NextCauseId() => ++_nextCauseId;
    public long NextReactiveEffectRegistrationId() =>
        ++_nextReactiveEffectRegistrationId;

    public void InsertFront(IEnumerable<BattleOperation> operations)
    {
        foreach (var operation in operations.Reverse())
        {
            Operations.AddFirst(operation);
        }
    }

    public BattleSnapshot Snapshot() => new(
        Turn,
        Phase,
        Units.Values
            .OrderBy(unit => unit.Team)
            .ThenBy(unit => unit.Position.Row)
            .ThenBy(unit => unit.Position.Index)
            .ThenBy(unit => unit.Id.Value, System.StringComparer.Ordinal)
            .Select(unit => unit.View())
            .ToArray(),
        CommandOrder
            .Where(PlayerCommands.ContainsKey)
            .Select(id => PlayerCommands[id])
            .ToArray()
    );

    public void Reset()
    {
        Units.Clear();
        PlayerCommands.Clear();
        CommandOrder.Clear();
        Operations.Clear();
        AfterActionReactiveEffects.Clear();
        EndTurnReactiveEffects.Clear();
        ReactiveEffects.Clear();
        ReactiveEffectGuards.Clear();
        HandledDeaths.Clear();

        Setup = null;
        Result = null;
        Random = new SeededRandomSource(1);
        Phase = BattleDomainPhase.Disabled;
        Turn = 0;
        AwaitingCueBatchId = 0;
        AfterActionFlushStarted = false;
        EndTurnFlushStarted = false;

        _nextCueBatchId = 0;
        _nextCauseId = 0;
        _nextReactiveEffectRegistrationId = 0;
    }
}
