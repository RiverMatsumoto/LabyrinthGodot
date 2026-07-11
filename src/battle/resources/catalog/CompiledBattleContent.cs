namespace Labyrinth;

using System.Collections.Generic;

public sealed class CompiledBattleContent(
    BattleCatalog catalog,
    IReadOnlyDictionary<EncounterId, EncounterDefinition> encounters,
    IReadOnlyDictionary<EquipmentId, EquipmentDefinition> equipment
)
{
    public BattleCatalog Catalog { get; } = catalog;
    public IReadOnlyDictionary<EncounterId, EncounterDefinition> Encounters
    {
        get;
    } = encounters;
    public IReadOnlyDictionary<EquipmentId, EquipmentDefinition> Equipment
    {
        get;
    } = equipment;

    public EncounterDefinition GetEncounter(EncounterId id) =>
        Encounters.TryGetValue(id, out var encounter)
            ? encounter
            : throw new KeyNotFoundException($"Unknown encounter '{id}'.");
}
