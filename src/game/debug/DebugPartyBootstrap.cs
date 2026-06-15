namespace Labyrinth;

#if DEBUG
using System.Linq;

public static class DebugPartyBootstrap {
  public static void SeedIfEmpty(
    IPartyRepo partyRepo,
    BattleCatalog catalog
  ) {
    if (partyRepo.Count > 0) {
      return;
    }

    Add(
      partyRepo,
      catalog,
      "bastion",
      "Bastion",
      new PartyPosition(PartyRow.Front, 0),
      BattleStats.Default with {
        MaxHp = 160,
        Strength = 12,
        Agility = 7,
        Vitality = 16,
        Attack = 6,
        Defense = 12,
      },
      BattleContent.BasicAttackId
    );
    Add(
      partyRepo,
      catalog,
      "duelist",
      "Duelist",
      new PartyPosition(PartyRow.Front, 1),
      BattleStats.Default with {
        MaxHp = 110,
        MaxTp = 28,
        Strength = 16,
        Agility = 13,
        Luck = 12,
        Attack = 10,
        Defense = 4,
      },
      BattleContent.BasicAttackId,
      BattleContent.PoisonStrikeId
    );
    Add(
      partyRepo,
      catalog,
      "arcanist",
      "Arcanist",
      new PartyPosition(PartyRow.Back, 0),
      BattleStats.Default with {
        MaxHp = 80,
        MaxTp = 45,
        Technique = 17,
        Agility = 11,
        Wisdom = 14,
        Attack = 7,
        Defense = 2,
      },
      BattleContent.BasicAttackId,
      BattleContent.FireId
    );
    Add(
      partyRepo,
      catalog,
      "medic",
      "Medic",
      new PartyPosition(PartyRow.Back, 1),
      BattleStats.Default with {
        MaxHp = 95,
        MaxTp = 42,
        Technique = 14,
        Agility = 9,
        Wisdom = 16,
        Defense = 4,
      },
      BattleContent.BasicAttackId,
      BattleContent.HealId
    );
    Add(
      partyRepo,
      catalog,
      "ranger",
      "Ranger",
      new PartyPosition(PartyRow.Back, 2),
      BattleStats.Default with {
        MaxHp = 100,
        MaxTp = 32,
        Strength = 11,
        Technique = 15,
        Agility = 16,
        Luck = 14,
        Attack = 8,
        Defense = 3,
      },
      BattleContent.BasicAttackId,
      BattleContent.HealId,
      BattleContent.PoisonStrikeId
    );
  }

  private static void Add(
    IPartyRepo partyRepo,
    BattleCatalog catalog,
    string id,
    string name,
    PartyPosition position,
    BattleStats stats,
    params ActionId[] actions
  ) {
    var learnedActions = actions
      .Where(actionId => catalog.TryGetAction(actionId, out _))
      .Distinct()
      .ToList();
    if (
      learnedActions.Count == 0
      && catalog.Actions.FirstOrDefault() is { } fallback
    ) {
      learnedActions.Add(fallback.Id);
    }

    var member = new PartyMember {
      Id = new BattlerId(id),
      Name = name,
      BaseStats = stats,
      Hp = stats.MaxHp,
      Tp = stats.MaxTp,
    };
    member.LearnedActions.AddRange(learnedActions);
    partyRepo.TryAdd(member, position);
  }
}
#endif
