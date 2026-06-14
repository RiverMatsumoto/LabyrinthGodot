namespace Labyrinth;

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class BattleContentResource : Resource
{
    [Export]
    public Array<BattleActionResource> Actions { get; set; } = [];
    [Export]
    public Array<BattleStatusResource> Statuses { get; set; } = [];
    [Export]
    public Array<BattleEncounterResource> Encounters { get; set; } = [];
    [Export]
    public Array<BattleEquipmentResource> Equipment { get; set; } = [];

    public CompiledBattleContent Compile()
    {
        var catalog = new BattleCatalog(
            Actions.Select(action => action.Compile()),
            Statuses.Select(status => status.Compile())
        );
        var encounters = Unique(
            Encounters.Select(encounter => encounter.Compile(catalog)),
            encounter => encounter.Id,
            "encounter"
        );
        var equipment = Unique(
            Equipment.Select(item => item.Compile()),
            item => item.Id,
            "equipment"
        );
        return new CompiledBattleContent(catalog, encounters, equipment);
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
