namespace Labyrinth;

using System;
using System.Collections.Generic;

internal sealed class BattleEffectOperationBuilder(BattleRuntime runtime)
{
    public IReadOnlyList<BattleOperation> Build(
        BattleEffectDefinition effect,
        EffectContext context
    )
    {
        var operations = new List<BattleOperation>
        {
            new TriggerReactiveEffectsOperation(new ReactiveEffectEvent(
                runtime.NextCauseId(),
                ReactiveEffectTrigger.BeforeEffect,
                context.SourceId,
                FirstTarget(context),
                context.ActionId,
                Depth: context.ReactiveEffectDepth
            )),
        };

        switch (effect)
        {
            case DamageEffectDefinition damage:
                if (!string.IsNullOrWhiteSpace(damage.AnimationId))
                {
                    operations.Add(new VisualCueOperation([
                        new AnimationCue(
                            damage.AnimationId,
                            context.SourceId,
                            context.TargetIds
                        ),
                    ]));
                }
                operations.Add(new DamageOperation(
                    context,
                    damage.Spec
                ));
                break;
            case HealEffectDefinition heal:
                if (!string.IsNullOrWhiteSpace(heal.AnimationId))
                {
                    operations.Add(new VisualCueOperation([
                        new AnimationCue(
                            heal.AnimationId,
                            context.SourceId,
                            context.TargetIds
                        ),
                    ]));
                }
                operations.Add(new HealOperation(
                    context,
                    ScaleAmount(context, heal.Amount, heal.Scale)
                ));
                break;
            case ModifyResourceEffectDefinition modify:
                operations.Add(new ModifyResourceOperation(
                    context,
                    modify.Resource,
                    ScaleAmount(
                        context,
                        modify.Amount,
                        modify.Scale
                    )
                ));
                break;
            case ApplyStatusEffectDefinition apply:
                operations.Add(new ApplyStatusOperation(context, apply));
                break;
            case RemoveStatusEffectDefinition remove:
                operations.Add(new RemoveStatusOperation(
                    context,
                    remove.StatusId
                ));
                break;
            case PlayAnimationEffectDefinition animation:
                operations.Add(new VisualCueOperation([
                    new AnimationCue(
                        animation.AnimationId,
                        context.SourceId,
                        context.TargetIds
                    ),
                ]));
                break;
            case WaitEffectDefinition wait:
                operations.Add(new VisualCueOperation([
                    new WaitCue(Math.Max(0, wait.Seconds)),
                ]));
                break;
            case RegisterReactiveEffectEffectDefinition register:
                operations.Add(new RegisterReactiveEffectOperation(
                    context,
                    register.ReactiveEffectId
                ));
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported effect '{effect.GetType().Name}'."
                );
        }

        operations.Add(new TriggerReactiveEffectsOperation(new ReactiveEffectEvent(
            runtime.NextCauseId(),
            ReactiveEffectTrigger.AfterEffect,
            context.SourceId,
            FirstTarget(context),
            context.ActionId,
            Depth: context.ReactiveEffectDepth
        )));
        return operations;
    }

    private static BattlerId FirstTarget(EffectContext context)
    {
        if (context.TargetIds.Count > 0)
        {
            return context.TargetIds[0];
        }
        return default;
    }

    private int ScaleAmount(
        EffectContext context,
        int amount,
        StatusStackScaleDefinition? scale)
    {
        return (int)Math.Round(ScaleAmount(
            context,
            (double)amount,
            scale
        ));
    }

    private double ScaleAmount(
        EffectContext context,
        double amount,
        StatusStackScaleDefinition? scale
    )
    {
        if (scale is null)
        {
            return amount;
        }
        if (
            runtime.Units.TryGetValue(context.SourceId, out var source)
            && source.Statuses.TryGetValue(
                scale.StatusId,
                out var status
            )
        )
        {
            return amount * status.Stacks;
        }
        if (context.StatusId == scale.StatusId)
        {
            return amount * context.StatusStacks;
        }
        return 0;
    }
}
