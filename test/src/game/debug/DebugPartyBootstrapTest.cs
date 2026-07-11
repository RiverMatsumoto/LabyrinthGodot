namespace Labyrinth;

#if DEBUG
using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

public class DebugPartyBootstrapTest(Node testScene) : TestClass(testScene)
{
    [Test]
    public void SeedsOnlyAnEmptyParty()
    {
        using var partyRepo = new PartyRepo();
        var battleContent = GD.Load<BattleContentResource>(
            "res://src/battle/resources/BattleContent.tres"
        ).Compile();
        var partyContent = GD.Load<PartyContentResource>(
            "res://src/party/resources/PartyContent.tres"
        ).Compile(battleContent.Catalog);

        DebugPartyBootstrap.SeedIfEmpty(
            partyRepo,
            partyContent.DebugParty
        );
        DebugPartyBootstrap.SeedIfEmpty(
            partyRepo,
            partyContent.DebugParty
        );

        partyRepo.Count.ShouldBe(BattleLimits.MaxPlayerBattlers);
        partyRepo.Members.ShouldAllBe(entry =>
            entry.Member.LearnedActions.Contains(BattleContent.BasicAttackId)
        );
    }

    [Test]
    public void StartsAuthoredBattleWithSeededParty()
    {
        var content = GD.Load<BattleContentResource>(
            "res://src/battle/resources/BattleContent.tres"
        ).Compile();
        var partyContent = GD.Load<PartyContentResource>(
            "res://src/party/resources/PartyContent.tres"
        ).Compile(content.Catalog);
        using var gameRepo = new GameRepo();
        using var partyRepo = new PartyRepo();
        gameRepo.SetBattleRequest(new BattleRequest(
            BattleContent.DefaultEncounterId,
            1,
            GameMode.Labyrinth
        ));
        var setup = new BattleSession(
            content,
            partyContent,
            gameRepo,
            partyRepo
        ).CreateSetup();
        using var battleRepo = new BattleRepo(content.Catalog);

        Should.NotThrow(() => battleRepo.Start(setup));
        battleRepo.Snapshot().Units.Count.ShouldBe(7);
    }
}
#endif
