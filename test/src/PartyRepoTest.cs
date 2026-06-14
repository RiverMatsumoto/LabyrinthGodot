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
    }

    [Test]
    public void EmptyPartyDataMigratesToEmptyRepo()
    {
        using var repo = new PartyRepo();
        repo.Load(PartyData.Empty);
        repo.Count.ShouldBe(0);
        PartyData.Empty.Version.ShouldBe(PartyData.CurrentVersion);
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
        return member;
    }
}
