namespace Labyrinth;

using System;
using System.Collections.Generic;

internal sealed class BattleResourceSystem(BattleRuntime runtime)
{
    public void Modify(ModifyResourceOperation operation)
    {
        var popups = new List<BattlePopup>();
        foreach (var targetId in operation.Context.TargetIds)
        {
            if (!runtime.Units.TryGetValue(targetId, out var target))
            {
                continue;
            }
            if (operation.Resource == BattleResource.Hp)
            {
                var old = target.Hp;
                target.Hp = Math.Clamp(
                    target.Hp + operation.Amount,
                    0,
                    target.Stats.MaxHp
                );
                var delta = target.Hp - old;
                popups.Add(new BattlePopup(
                    target.Id,
                    Math.Abs(delta),
                    delta >= 0
                        ? BattlePopupKind.Heal
                        : BattlePopupKind.Damage
                ));
            }
            else
            {
                var old = target.Tp;
                target.Tp = Math.Clamp(
                    target.Tp + operation.Amount,
                    0,
                    target.Stats.MaxTp
                );
                popups.Add(new BattlePopup(
                    target.Id,
                    target.Tp - old,
                    BattlePopupKind.Tp
                ));
            }
        }
        if (popups.Count > 0)
        {
            runtime.InsertFront([
                new CueOperation([new PopupBatchCue(popups)]),
                new DeathCheckOperation(
                    operation.Context.SourceId,
                    operation.Context.ActionId,
                    operation.Context.ReactionDepth
                ),
            ]);
        }
    }
}
