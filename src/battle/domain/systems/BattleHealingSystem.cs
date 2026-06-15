namespace Labyrinth;

using System;
using System.Collections.Generic;

internal sealed class BattleHealingSystem(BattleRuntime runtime)
{
    public void Apply(HealOperation operation)
    {
        var popups = new List<BattlePopup>();
        var reactionEvents = new List<BattleOperation>();
        foreach (var targetId in operation.Context.TargetIds)
        {
            if (
                !runtime.Units.TryGetValue(targetId, out var target)
                || !target.IsAlive
            )
            {
                continue;
            }
            var amount = Math.Max(0, operation.Amount);
            var oldHp = target.Hp;
            target.Hp = Math.Min(
                target.Stats.MaxHp,
                target.Hp + amount
            );
            var healed = target.Hp - oldHp;
            popups.Add(new BattlePopup(
                target.Id,
                healed,
                BattlePopupKind.Heal
            ));
            reactionEvents.Add(new WindowOperation(new ReactionEvent(
                runtime.NextCauseId(),
                ReactionTrigger.Healing,
                operation.Context.SourceId,
                target.Id,
                operation.Context.ActionId,
                Depth: operation.Context.ReactionDepth
            )));
        }

        var followUps = new List<BattleOperation>();
        if (popups.Count > 0)
        {
            followUps.Add(new CueOperation([
                new PopupBatchCue(popups),
            ]));
        }
        followUps.AddRange(reactionEvents);
        runtime.InsertFront(followUps);
    }
}
