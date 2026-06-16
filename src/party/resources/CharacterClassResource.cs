namespace Labyrinth;

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.Collections;
using SystemDictionary = System.Collections.Generic.Dictionary
    <Labyrinth.StatusId, double>;
using DamageDictionary = System.Collections.Generic.Dictionary
    <Labyrinth.DamageType, double>;

[GlobalClass]
public partial class CharacterClassResource : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public BattleStatsResource Stats { get; set; } = new();
    [Export] public Array<BattleActionResource> Actions { get; set; } = [];
    [Export]
    public Array<BattleReactiveEffectResource> PassiveReactiveEffects { get; set; } = [];
    [Export]
    public Array<StatusResistanceResource> StatusResistances { get; set; } = [];
    [Export]
    public Array<StatusWeaknessResource> StatusWeaknesses { get; set; } = [];
    [Export]
    public Array<DamageTypeResistanceResource> DamageTypeResistances
    {
        get;
        set;
    } = [];
    [Export]
    public Array<DamageTypeWeaknessResource> DamageTypeWeaknesses
    {
        get;
        set;
    } = [];

    public CharacterClassDefinition Compile(BattleCatalog catalog)
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new InvalidOperationException("Character class id is required.");
        }
        ArgumentNullException.ThrowIfNull(Stats);

        var actionIds = CompileActions(catalog);
        var reactiveEffectIds = CompileReactiveEffects(catalog);
        return new CharacterClassDefinition(
            new CharacterClassId(Id),
            string.IsNullOrWhiteSpace(DisplayName) ? Id : DisplayName,
            Stats.Compile(),
            actionIds,
            reactiveEffectIds,
            CompileStatusAffinities(
                catalog,
                StatusResistances,
                affinity => affinity.StatusId,
                affinity => affinity.Multiplier,
                "status resistance"
            ),
            CompileStatusAffinities(
                catalog,
                StatusWeaknesses,
                affinity => affinity.StatusId,
                affinity => affinity.Multiplier,
                "status weakness"
            ),
            CompileDamageAffinities(
                DamageTypeResistances,
                affinity => affinity.DamageType,
                affinity => affinity.Multiplier,
                "damage resistance"
            ),
            CompileDamageAffinities(
                DamageTypeWeaknesses,
                affinity => affinity.DamageType,
                affinity => affinity.Multiplier,
                "damage weakness"
            )
        );
    }

    private ActionId[] CompileActions()
    {
        var ids = new List<ActionId>();
        foreach (var action in Actions)
        {
            if (action is null)
            {
                throw new InvalidOperationException(
                    $"Character class '{Id}' has a missing action."
                );
            }
            var id = new ActionId(action.Id);
            ids.Add(id);
        }
        return RequireUnique(ids, "action");
    }

    private ActionId[] CompileActions(BattleCatalog catalog)
    {
        var ids = CompileActions();
        foreach (var id in ids)
        {
            _ = catalog.GetAction(id);
        }
        return ids;
    }

    private ReactiveEffectId[] CompileReactiveEffects(BattleCatalog catalog)
    {
        var ids = new List<ReactiveEffectId>();
        foreach (var reactiveEffect in PassiveReactiveEffects)
        {
            if (reactiveEffect is null)
            {
                throw new InvalidOperationException(
                    $"Character class '{Id}' has a missing passive reactive effect."
                );
            }
            var id = new ReactiveEffectId(reactiveEffect.Id);
            _ = catalog.GetReactiveEffect(id);
            ids.Add(id);
        }
        return RequireUnique(ids, "passive reactive effect");
    }

    private SystemDictionary CompileStatusAffinities<T>(
        BattleCatalog catalog,
        IEnumerable<T> affinities,
        Func<T, string> idSelector,
        Func<T, double> valueSelector,
        string kind
    )
    {
        var result = new SystemDictionary();
        foreach (var affinity in affinities)
        {
            if (affinity is null)
            {
                throw new InvalidOperationException(
                    $"Character class '{Id}' has a missing {kind}."
                );
            }
            var id = new StatusId(idSelector(affinity));
            _ = catalog.GetStatus(id);
            if (!result.TryAdd(id, Math.Clamp(valueSelector(affinity), 0, 1)))
            {
                throw new InvalidOperationException(
                    $"Character class '{Id}' has duplicate {kind} '{id}'."
                );
            }
        }
        return result;
    }

    private DamageDictionary CompileDamageAffinities<T>(
        IEnumerable<T> affinities,
        Func<T, DamageType> typeSelector,
        Func<T, double> valueSelector,
        string kind
    )
    {
        var result = new DamageDictionary();
        foreach (var affinity in affinities)
        {
            if (affinity is null)
            {
                throw new InvalidOperationException(
                    $"Character class '{Id}' has a missing {kind}."
                );
            }
            var type = typeSelector(affinity);
            if (!result.TryAdd(
                type,
                Math.Clamp(valueSelector(affinity), 0, 1)
            ))
            {
                throw new InvalidOperationException(
                    $"Character class '{Id}' has duplicate {kind} '{type}'."
                );
            }
        }
        return result;
    }

    private T[] RequireUnique<T>(IEnumerable<T> values, string kind)
        where T : notnull
    {
        var result = values.ToArray();
        if (result.Distinct().Count() != result.Length)
        {
            throw new InvalidOperationException(
                $"Character class '{Id}' has duplicate {kind} references."
            );
        }
        return result;
    }
}
