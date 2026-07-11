namespace Labyrinth;

using System;
using System.Collections.Generic;

internal sealed class BattleDamageSystem(BattleRuntime runtime)
{
    public void Apply(DamageOperation operation)
    {
        if (
            !runtime.Units.TryGetValue(
                operation.Context.SourceId,
                out var source
            )
        )
        {
            return;
        }

        var popups = new List<BattlePopup>();
        var reactiveEffectEvents = new List<BattleOperation>();
        foreach (var targetId in operation.Context.TargetIds)
        {
            if (
                !runtime.Units.TryGetValue(targetId, out var target)
                || !target.IsAlive
            )
            {
                continue;
            }
            var damage = ComputeDamage(
                source,
                target,
                operation.Spec,
                operation.Context
            );
            target.Hp = Math.Max(0, target.Hp - damage);
            popups.Add(new BattlePopup(
                target.Id,
                damage,
                BattlePopupKind.Damage
            ));
            reactiveEffectEvents.Add(new TriggerReactiveEffectsOperation(new ReactiveEffectEvent(
                runtime.NextCauseId(),
                ReactiveEffectTrigger.Damage,
                source.Id,
                target.Id,
                operation.Context.ActionId,
                Depth: operation.Context.ReactiveEffectDepth,
                DamageType: operation.Spec.Type
            )));
        }

        var followUps = new List<BattleOperation>();
        if (popups.Count > 0)
        {
            followUps.Add(new VisualCueOperation([
                new PopupBatchCue(popups),
            ]));
        }
        followUps.AddRange(reactiveEffectEvents);
        followUps.Add(new DeathCheckOperation(
            operation.Context.SourceId,
            operation.Context.ActionId,
            operation.Context.ReactiveEffectDepth
        ));
        runtime.InsertFront(followUps);
    }

    private int ComputeDamage(
        BattleUnit source,
        BattleUnit target,
        DamageSpec spec,
        EffectContext context
    )
    {
        double damage;
        if (spec.Mode == DamageMode.Fixed)
        {
            damage = ResolveDamageValue(context, spec.FixedAmount);
        }
        else
        {
            var offense = Math.Max(
                0,
                ResolveStatOffense(source, spec)
                    * ResolveDamageValue(context, spec.PowerMultiplier)
            );
            if (spec.Type == DamageType.True)
            {
                damage = offense;
            }
            else
            {
                var defense = IsElemental(spec.Type)
                    ? (target.Stats.Wisdom * 1.35)
                        + (target.Stats.Technique * 0.65)
                        + (target.Stats.Defense * 0.35)
                    : target.Stats.Defense
                        + (target.Stats.Vitality * 0.75);
                damage = offense * (offense / (offense + defense + 1.0));
            }
        }

        if (
            context.Range == BattleRange.Melee
            && (
                source.Position.Row == PartyRow.Back
                || target.Position.Row == PartyRow.Back
            )
        )
        {
            damage *= BattleRules.BackRowMeleeMultiplier;
        }

        if (spec.CanCrit)
        {
            var chance = Math.Clamp(
                0.05
                    + (
                        (source.Stats.Luck - target.Stats.Luck)
                        * 0.0025
                    ),
                0.01,
                0.5
            );
            if (runtime.Random.NextDouble() < chance)
            {
                damage *= spec.CritMultiplier;
            }
        }

        if (spec.Type != DamageType.True)
        {
            target.DamageTypeResistances.TryGetValue(
                spec.Type,
                out var resistance
            );
            target.DamageTypeWeaknesses.TryGetValue(
                spec.Type,
                out var weakness
            );
            damage *= (1.0 - resistance) * (1.0 + weakness);
        }

        return Math.Max(0, (int)Math.Round(damage));
    }

    private double ResolveDamageValue(
        EffectContext context,
        DamageValueDefinition value
    )
    {
        var resolved = value.Source switch
        {
            DamageValueSource.ReactiveStatusPower => context.StatusPower,
            _ => value.AuthoredValue,
        };
        if (value.MultiplyByReactiveStatusPower)
        {
            resolved *= context.StatusPower;
        }
        if (value.MultiplyByReactiveStatusStacks)
        {
            resolved *= context.StatusStacks;
        }
        if (value.SourceStatusStackScale is { } scale)
        {
            resolved *= ResolveStatusStacks(context, scale.StatusId);
        }
        return resolved;
    }

    private double ResolveStatusStacks(
        EffectContext context,
        StatusId statusId
    )
    {
        if (
            runtime.Units.TryGetValue(context.SourceId, out var source)
            && source.Statuses.TryGetValue(statusId, out var status)
        )
        {
            return status.Stacks;
        }
        return context.StatusId == statusId ? context.StatusStacks : 0;
    }

    private static double ResolveStatOffense(
        BattleUnit source,
        DamageSpec spec
    )
    {
        double offense = 0;
        foreach (var scale in spec.StatScales)
        {
            offense += StatValue(source.Stats, scale.Stat) * scale.Weight;
        }
        return offense;
    }

    private static double StatValue(BattleStats stats, BattleStat stat) =>
        stat switch
        {
            BattleStat.MaxHp => stats.MaxHp,
            BattleStat.MaxTp => stats.MaxTp,
            BattleStat.Strength => stats.Strength,
            BattleStat.Technique => stats.Technique,
            BattleStat.Agility => stats.Agility,
            BattleStat.Vitality => stats.Vitality,
            BattleStat.Wisdom => stats.Wisdom,
            BattleStat.Luck => stats.Luck,
            BattleStat.Attack => stats.Attack,
            BattleStat.Defense => stats.Defense,
            _ => 0,
        };

    private static bool IsElemental(DamageType type) =>
        type is DamageType.Fire
            or DamageType.Ice
            or DamageType.Lightning;
}
