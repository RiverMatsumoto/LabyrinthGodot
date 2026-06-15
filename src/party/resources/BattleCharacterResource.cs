namespace Labyrinth;

using System;
using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class BattleCharacterResource : Resource
{
    [Export] public string BattlerId { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export(PropertyHint.Range, "1,99,1")]
    public int Level { get; set; } = 1;
    [Export] public int Experience { get; set; }
    [Export] public CharacterClassResource? Class { get; set; }

    public BattleCharacterDefinition Compile(
        IReadOnlyDictionary<CharacterClassId, CharacterClassDefinition> classes
    )
    {
        if (string.IsNullOrWhiteSpace(BattlerId))
        {
            throw new InvalidOperationException(
                "Battle character battler id is required."
            );
        }
        if (Class is null)
        {
            throw new InvalidOperationException(
                $"Battle character '{BattlerId}' requires a class."
            );
        }
        var classId = new CharacterClassId(Class.Id);
        if (!classes.TryGetValue(classId, out var definition))
        {
            throw new InvalidOperationException(
                $"Battle character '{BattlerId}' references unknown class "
                    + $"'{classId}'."
            );
        }
        return new BattleCharacterDefinition(
            new BattlerId(BattlerId),
            string.IsNullOrWhiteSpace(DisplayName)
                ? BattlerId
                : DisplayName,
            Math.Max(1, Level),
            Math.Max(0, Experience),
            definition
        );
    }
}
