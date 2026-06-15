namespace Labyrinth;

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class PartyContentResource : Resource
{
    [Export]
    public Array<CharacterClassResource> Classes { get; set; } = [];
    [Export]
    public Array<BattleCharacterResource> Characters { get; set; } = [];
    [Export] public DebugPartyResource? DebugParty { get; set; }

    public CompiledPartyContent Compile(BattleCatalog catalog)
    {
        var classes = Unique(
            Classes.Select(value =>
                value?.Compile(catalog)
                ?? throw new InvalidOperationException(
                    "Party content has a missing character class."
                )
            ),
            definition => definition.Id,
            "character class"
        );
        var characters = Unique(
            Characters.Select(value =>
                value?.Compile(classes)
                ?? throw new InvalidOperationException(
                    "Party content has a missing battle character."
                )
            ),
            definition => definition.Id,
            "battle character"
        );
        var debugParty = DebugParty?.Compile(characters)
            ?? throw new InvalidOperationException(
                "Party content requires a debug party."
            );
        return new CompiledPartyContent(classes, characters, debugParty);
    }

    private static System.Collections.Generic.Dictionary<TKey, TValue>
        Unique<TKey, TValue>(
        IEnumerable<TValue> values,
        Func<TValue, TKey> keySelector,
        string kind
    ) where TKey : notnull
    {
        var result =
            new System.Collections.Generic.Dictionary<TKey, TValue>();
        foreach (var value in values)
        {
            var key = keySelector(value);
            if (!result.TryAdd(key, value))
            {
                throw new InvalidOperationException(
                    $"Duplicate {kind} id '{key}'."
                );
            }
        }
        return result;
    }
}
