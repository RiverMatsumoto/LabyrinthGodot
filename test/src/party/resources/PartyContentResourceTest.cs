namespace Labyrinth;

using System;
using System.Linq;
using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

public class PartyContentResourceTest(Node testScene) : TestClass(testScene)
{
    [Test]
    public void CompilesAuthoredClassesCharactersAndDebugLineup()
    {
        var battleContent = LoadBattleContent();
        var resource = GD.Load<PartyContentResource>(
            "res://src/party/resources/PartyContent.tres"
        );

        var content = resource.Compile(battleContent.Catalog);
        var bastion = content.Characters[new BattlerId("bastion")]
            .CreatePartyMember();

        content.Classes.Count.ShouldBe(5);
        content.Characters.Count.ShouldBe(5);
        content.DebugParty.Entries.Count.ShouldBe(5);
        bastion.Hp.ShouldBe(bastion.BaseStats.MaxHp);
        bastion.Tp.ShouldBe(bastion.BaseStats.MaxTp);
        bastion.LearnedActions.ShouldContain(BattleContent.BasicAttackId);
        content.DebugParty.Entries.Select(entry => entry.Position)
            .Distinct()
            .Count()
            .ShouldBe(5);
    }

    [Test]
    public void RejectsMissingAndDuplicateClassReferences()
    {
        var catalog = LoadBattleContent().Catalog;
        var action = GD.Load<BattleActionResource>(
            "res://src/battle/resources/actions/BasicAttack.tres"
        );
        var classResource = Class("same", action);
        var content = new PartyContentResource
        {
            Classes = [classResource, Class("same", action)],
            DebugParty = new DebugPartyResource(),
        };

        Should.Throw<InvalidOperationException>(() =>
            content.Compile(catalog));

        var character = new BattleCharacterResource
        {
            BattlerId = "hero",
            Class = Class("missing", action),
        };
        Should.Throw<InvalidOperationException>(() =>
            character.Compile(
                new System.Collections.Generic.Dictionary<
                    CharacterClassId,
                    CharacterClassDefinition
                >()
            ));
    }

    [Test]
    public void RejectsMissingCatalogActions()
    {
        var classResource = Class(
            "invalid",
            new BattleActionResource { Id = "missing" }
        );

        Should.Throw<System.Collections.Generic.KeyNotFoundException>(() =>
            classResource.Compile(LoadBattleContent().Catalog));
    }

    [Test]
    public void RejectsDuplicatePositionsAndOversizedLineups()
    {
        var character = CharacterDefinition("hero");
        var secondCharacter = CharacterDefinition("second");
        var characters =
            new System.Collections.Generic.Dictionary<
                BattlerId,
                BattleCharacterDefinition
            >
            {
                [character.Id] = character,
                [secondCharacter.Id] = secondCharacter,
            };
        var characterResource = new BattleCharacterResource
        {
            BattlerId = character.Id.Value,
        };
        var secondCharacterResource = new BattleCharacterResource
        {
            BattlerId = secondCharacter.Id.Value,
        };
        var duplicate = new DebugPartyResource
        {
            Entries =
            [
                new DebugPartyEntryResource
                {
                    Character = secondCharacterResource,
                },
                new DebugPartyEntryResource
                {
                    Character = characterResource,
                },
            ],
        };

        Should.Throw<InvalidOperationException>(() =>
            duplicate.Compile(characters));

        var oversized = new DebugPartyResource();
        for (var index = 0; index <= BattleLimits.MaxPlayerBattlers; index++)
        {
            oversized.Entries.Add(new DebugPartyEntryResource());
        }
        Should.Throw<InvalidOperationException>(() =>
            oversized.Compile(characters));
    }

    [Test]
    public void EnemyAndClassesShareExternalBasicAttack()
    {
        var basicAttack = GD.Load<BattleActionResource>(
            "res://src/battle/resources/actions/BasicAttack.tres"
        );
        var battleContent = GD.Load<BattleContentResource>(
            "res://src/battle/resources/BattleContent.tres"
        );
        var enemy = GD.Load<BattleEnemyResource>(
            "res://src/battle/resources/enemies/Squirrel.tres"
        );
        var partyContent = GD.Load<PartyContentResource>(
            "res://src/party/resources/PartyContent.tres"
        );

        battleContent.Actions.ShouldContain(basicAttack);
        enemy.Actions.ShouldContain(basicAttack);
        partyContent.Classes.ShouldAllBe(characterClass =>
            characterClass.Actions.Contains(basicAttack)
        );

        var action = basicAttack.Compile();
        action.TargetRule.ShouldBe(BattleTargetRule.SingleEnemy);
        action.Range.ShouldBe(BattleRange.Melee);
        action.RetargetPolicy.ShouldBe(RetargetPolicy.NearestValid);
        action.Effects.ShouldNotBeEmpty();
    }

    private static CompiledBattleContent LoadBattleContent() =>
        GD.Load<BattleContentResource>(
            "res://src/battle/resources/BattleContent.tres"
        ).Compile();

    private static CharacterClassResource Class(
        string id,
        BattleActionResource action
    ) => new()
    {
        Id = id,
        Actions = [action],
    };

    private static BattleCharacterDefinition CharacterDefinition(string id)
    {
        var characterClass = new CharacterClassDefinition(
            new CharacterClassId("class"),
            "Class",
            BattleStats.Default,
            [BattleContent.BasicAttackId],
            [],
            new System.Collections.Generic.Dictionary<StatusId, double>(),
            new System.Collections.Generic.Dictionary<StatusId, double>(),
            new System.Collections.Generic.Dictionary<DamageType, double>(),
            new System.Collections.Generic.Dictionary<DamageType, double>()
        );
        return new BattleCharacterDefinition(
            new BattlerId(id),
            id,
            1,
            0,
            characterClass
        );
    }
}
