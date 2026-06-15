namespace Labyrinth;

using System;
using System.Collections.Generic;
using System.Linq;

public interface IBattleSession
{
    BattleSetup CreateSetup();
    GameMode Complete(BattleResult result);
}

public sealed class BattleSession(
    CompiledBattleContent content,
    IGameRepo gameRepo,
    IPartyRepo partyRepo
) : IBattleSession
{
    private GameMode? _returnMode;

    public BattleSetup CreateSetup()
    {
        var request = gameRepo.CurrentBattleRequest;
        var encounter = content.GetEncounter(request.EncounterId);
        _returnMode = request.ReturnMode;
#if DEBUG
        DebugPartyBootstrap.SeedIfEmpty(partyRepo, content.Catalog);
#endif

        return new BattleSetup(
            request.EncounterId,
            request.Seed,
            partyRepo.Members.Select(FromParty).ToArray(),
            encounter.Enemies,
            encounter.Reward
        );
    }

    public GameMode Complete(BattleResult result)
    {
        partyRepo.ApplyBattleVitals(result.PlayerVitals);
        return result.Outcome == BattleOutcome.Defeat
          ? GameMode.MainMenu
          : _returnMode ?? throw new InvalidOperationException(
            "Battle session has not been started."
          );
    }

    private static BattleBattlerSeed FromParty(PartyMemberEntry entry) => new(
      Id: entry.Member.Id,
      Name: entry.Member.Name,
      Team: BattleTeam.Player,
      Position: entry.Position,
      Stats: entry.Member.EffectiveStats,
      Hp: entry.Member.Hp,
      Tp: entry.Member.Tp,
      ActionIds: entry.Member.LearnedActions.ToArray(),
      ReactionIdList: entry.Member.PassiveReactionIds.ToArray(),
      StatusResistances: new Dictionary<StatusId, double>(
        entry.Member.StatusResistances
      ),
      StatusWeaknesses: new Dictionary<StatusId, double>(
        entry.Member.StatusWeakness
      ),
      DamageTypeResistances: new Dictionary<DamageType, double>(
        entry.Member.DamageTypeResistances
      ),
      DamageTypeWeaknesses: new Dictionary<DamageType, double>(
        entry.Member.DamageTypeWeaknesses
      )
    );
}
