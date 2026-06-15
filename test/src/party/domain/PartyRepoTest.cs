namespace Labyrinth;

using System.Linq;
using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

public class PartyRepoTest : TestClass
{
    public PartyRepoTest(Node testScene) : base(testScene) { }

    [Test]
    public void EnforcesFiveMemberFormationAndRoundTripsData()
    {
        using var repo = new PartyRepo();
        for (var index = 0; index < PartyRepo.MaxMembers; index++)
        {
            var row = index < 3 ? PartyRow.Front : PartyRow.Back;
            var slot = index < 3 ? index : index - 3;
            repo.TryAdd(
                CreateMember(index),
                new PartyPosition(row, slot)
            ).ShouldBeTrue();
        }

        repo.TryAdd(
            CreateMember(99),
            new PartyPosition(PartyRow.Back, 2)
        ).ShouldBeFalse();
        repo.TryMove(
            new BattlerId("member_0"),
            new PartyPosition(PartyRow.Back, 0)
        ).ShouldBeFalse();

        var data = repo.ToData();
        using var loaded = new PartyRepo();
        loaded.Load(data);

        loaded.Count.ShouldBe(5);
        loaded.Members[0].Position.ShouldBe(
            new PartyPosition(PartyRow.Front, 0)
        );
        loaded.TryGet(new BattlerId("member_0"), out var member)
            .ShouldBeTrue();
        member.EffectiveStats.Attack.ShouldBe(7);
        member.LearnedActions.Single().ShouldBe(
            BattleContent.BasicAttackId
        );
        member.StatusResistances[BattleContent.PoisonId]
            .ShouldBe(0.25);
        member.StatusWeakness[BattleContent.StunId].ShouldBe(0.5);
        member.DamageTypeResistances[DamageType.Fire].ShouldBe(0.2);
        member.DamageTypeWeaknesses[DamageType.Ice].ShouldBe(0.4);
        member.PassiveReactionIds.Single().ShouldBe(
            BattleContent.ToxicRecoveryReactionId
        );
    }

    [Test]
    public void EmptyPartyDataMigratesToEmptyRepo()
    {
        using var repo = new PartyRepo();
        repo.Load(PartyData.Empty);
        repo.Count.ShouldBe(0);
        PartyData.Empty.Version.ShouldBe(PartyData.CurrentVersion);
    }

    [Test]
    public void VersionOnePartyDataDefaultsNewCollectionsToEmpty()
    {
        var legacy = new PartyData
        {
            Version = 1,
            Members =
            [
                new PartyMemberData
                {
                    Id = "legacy",
                    Name = "Legacy",
                    Hp = 100,
                    Tp = 20,
                    Stats = BattleStatsData.From(BattleStats.Default),
                },
            ],
        };
        using var repo = new PartyRepo();

        repo.Load(legacy);

        repo.TryGet(new BattlerId("legacy"), out var member)
            .ShouldBeTrue();
        member.PassiveReactionIds.ShouldBeEmpty();
        member.StatusWeakness.ShouldBeEmpty();
        member.DamageTypeResistances.ShouldBeEmpty();
        member.DamageTypeWeaknesses.ShouldBeEmpty();
    }

    private static PartyMember CreateMember(int index)
    {
        var member = new PartyMember
        {
            Id = new BattlerId($"member_{index}"),
            Name = $"Member {index}",
            BaseStats = BattleStats.Default with { Attack = 5 },
            Hp = 100,
            Tp = 20,
        };
        member.LearnedActions.Add(BattleContent.BasicAttackId);
        member.Equipment.Add(new EquipmentId("sword"));
        member.EquipmentModifiers.Add(
            new StatModifier(BattleStat.Attack, 2)
        );
        member.StatusResistances[BattleContent.PoisonId] = 0.25;
        member.StatusWeakness[BattleContent.StunId] = 0.5;
        member.DamageTypeResistances[DamageType.Fire] = 0.2;
        member.DamageTypeWeaknesses[DamageType.Ice] = 0.4;
        member.PassiveReactionIds.Add(
            BattleContent.ToxicRecoveryReactionId
        );
        return member;
    }
}
