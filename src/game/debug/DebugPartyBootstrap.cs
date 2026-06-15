namespace Labyrinth;

#if DEBUG
using System;

public static class DebugPartyBootstrap
{
    public static void SeedIfEmpty(
        IPartyRepo partyRepo,
        DebugPartyDefinition debugParty
    )
    {
        if (partyRepo.Count > 0)
        {
            return;
        }

        foreach (var entry in debugParty.Entries)
        {
            if (!partyRepo.TryAdd(
                entry.Character.CreatePartyMember(),
                entry.Position
            ))
            {
                throw new InvalidOperationException(
                    $"Unable to seed debug character "
                        + $"'{entry.Character.Id}'."
                );
            }
        }
    }
}
#endif
