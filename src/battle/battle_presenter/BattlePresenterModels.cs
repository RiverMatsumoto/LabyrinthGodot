namespace Labyrinth;

using System.Collections.Generic;

public enum BattleCommandMenuAction
{
    Attack,
    Skill,
    Item,
    Defend,
    Move,
    Escape,
}

public readonly record struct BattleTargetGridPoint(
    BattleTeam Team,
    PartyRow Row,
    int Slot
);

public sealed record BattleCommandMenuOption(
    BattleCommandMenuAction Action,
    string Name,
    bool IsEnabled,
    string DisabledReason = ""
);

public sealed record BattleScreenView(
    int Turn,
    BattlerId? ActiveActorId,
    IReadOnlyList<BattleUnitViewModel> Units
);

public sealed record BattleUnitViewModel(
    BattlerId Id,
    string Name,
    BattleTeam Team,
    PartyPosition Position,
    int Hp,
    int MaxHp,
    int Tp,
    int MaxTp,
    bool IsAlive,
    string? QueuedCommandText
);

public sealed record BattleTargetOption(
    string Name,
    BattlerId? CommandTargetId,
    IReadOnlyList<BattlerId> AnchorIds,
    IReadOnlyList<BattlerId> AffectedIds,
    BattleTeam Team,
    BattleTargetGridPoint GridPoint
);

public sealed record BattleActionOption(
    ActionId ActionId,
    string Name,
    BattleTargetRule TargetRule,
    int TpCost,
    IReadOnlyList<BattleTargetOption> TargetOptions,
    bool IsEnabled,
    string DisabledReason
);

public sealed record BattleCommandPrompt(
    BattlerId ActorId,
    BattleActionOption? Attack,
    IReadOnlyList<BattleActionOption> Skills,
    bool CanUseItem = false,
    bool CanDefend = false,
    bool CanMove = false
);
