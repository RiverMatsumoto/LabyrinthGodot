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
                operation.Context.Range
            );
            target.Hp = Math.Max(0, target.Hp - damage);
            popups.Add(new BattlePopup(
                target.Id,
                damage,
                BattlePopupKind.Damage
            ));
            reactiveEffectEvents.Add(new WindowOperation(new ReactiveEffectEvent(
                runtime.NextCauseId(),
                ReactiveEffectTrigger.Damage,
                source.Id,
                target.Id,
                operation.Context.ActionId,
                Depth: operation.Context.ReactiveEffectDepth
            )));
        }

        var followUps = new List<BattleOperation>();
        if (popups.Count > 0)
        {
            followUps.Add(new CueOperation([
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
        BattleRange range
    )
    {
        double damage;
        if (spec.Mode == DamageMode.Fixed)
        {
            damage = spec.Power;
        }
        else if (spec.Type == DamageType.True)
        {
            damage = spec.Power
                + source.Stats.Strength
                + source.Stats.Technique
                + source.Stats.Attack;
        }
        else if (
            spec.Type is DamageType.Fire
                or DamageType.Ice
                or DamageType.Lightning
        )
        {
            var offense = spec.Power
                + source.Stats.Technique
                + (source.Stats.Attack * 0.5);
            var defense = (target.Stats.Wisdom * 1.35)
                + (target.Stats.Technique * 0.65)
                + (target.Stats.Defense * 0.35);
            damage = offense * (offense / (offense + defense + 1.0));
        }
        else
        {
            var offense = spec.Power
                + source.Stats.Strength
                + source.Stats.Attack;
            var defense = target.Stats.Defense
                + (target.Stats.Vitality * 0.75);
            damage = offense * (offense / (offense + defense + 1.0));
        }

        if (
            range == BattleRange.Melee
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
}
