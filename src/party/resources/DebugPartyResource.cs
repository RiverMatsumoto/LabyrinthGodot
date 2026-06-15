namespace Labyrinth;

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class DebugPartyResource : Resource
{
    [Export]
    public Array<DebugPartyEntryResource> Entries { get; set; } = [];

    public DebugPartyDefinition Compile(
        IReadOnlyDictionary<BattlerId, BattleCharacterDefinition> characters
    )
    {
        if (Entries.Count == 0)
        {
            throw new InvalidOperationException(
                "Debug party requires at least one character."
            );
        }
        if (Entries.Count > BattleLimits.MaxPlayerBattlers)
        {
            throw new InvalidOperationException(
                $"Debug party cannot exceed "
                    + $"{BattleLimits.MaxPlayerBattlers} characters."
            );
        }
        var entries = Entries.Select(entry =>
            entry?.Compile(characters)
            ?? throw new InvalidOperationException(
                "Debug party has a missing entry."
            )
        ).ToArray();
        if (entries.Select(entry => entry.Character.Id).Distinct().Count()
            != entries.Length)
        {
            throw new InvalidOperationException(
                "Debug party has duplicate characters."
            );
        }
        if (entries.Select(entry => entry.Position).Distinct().Count()
            != entries.Length)
        {
            throw new InvalidOperationException(
                "Debug party has duplicate positions."
            );
        }
        return new DebugPartyDefinition(entries);
    }
}
