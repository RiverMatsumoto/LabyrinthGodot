namespace Labyrinth;

using System.Collections.Generic;

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
    bool IsAlive
);

public sealed record BattleTargetOption(BattlerId Id, string Name);

public sealed record BattleActionOption(
    ActionId ActionId,
    string Name,
    int TpCost,
    IReadOnlyList<BattleTargetOption> TargetOptions
);

public sealed record BattleCommandPrompt(
    BattlerId ActorId,
    BattleActionOption? Attack,
    IReadOnlyList<BattleActionOption> Skills,
    bool CanUseItem = false,
    bool CanDefend = false,
    bool CanMove = false
);
