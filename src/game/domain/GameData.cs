namespace Labyrinth;

using System;
using Chickensoft.Introspection;
using Chickensoft.Serialization;
using Chickensoft.Sync.Primitives;

[Meta, Id("game_data")]
public partial class GameData
{
    [Save("map_movement_data")]
    public required MapMovementData MapMovementData { get; init; }

    [Save("party_data")]
    public PartyData PartyData { get; init; } = PartyData.Empty;
}
