namespace Labyrinth;

using System;
using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class DebugPartyEntryResource : Resource
{
    [Export] public BattleCharacterResource? Character { get; set; }
    [Export] public PartyRow Row { get; set; }
    [Export(PropertyHint.Range, "0,2,1")]
    public int Slot { get; set; }

    public DebugPartyEntryDefinition Compile(
        IReadOnlyDictionary<BattlerId, BattleCharacterDefinition> characters
    )
    {
        if (Character is null)
        {
            throw new InvalidOperationException(
                "Debug party entry requires a character."
            );
        }
        var id = new BattlerId(Character.BattlerId);
        if (!characters.TryGetValue(id, out var character))
        {
            throw new InvalidOperationException(
                $"Debug party references unknown character '{id}'."
            );
        }
        var position = new PartyPosition(Row, Slot);
        if (!position.IsValid)
        {
            throw new InvalidOperationException(
                $"Debug party character '{id}' has invalid slot '{Slot}'."
            );
        }
        return new DebugPartyEntryDefinition(character, position);
    }
}
