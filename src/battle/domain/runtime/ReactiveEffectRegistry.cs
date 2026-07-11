namespace Labyrinth;

using System;
using System.Collections.Generic;

internal sealed class ReactiveEffectRegistry
{
    private readonly Dictionary<
        ReactiveEffectTrigger,
        List<RuntimeReactiveEffect>
    > _effectsByTrigger = [];

    public void Add(RuntimeReactiveEffect reactiveEffect)
    {
        var trigger = reactiveEffect.Definition.Trigger;
        if (!_effectsByTrigger.TryGetValue(trigger, out var effects))
        {
            effects = [];
            _effectsByTrigger.Add(trigger, effects);
        }

        var index = effects.BinarySearch(
            reactiveEffect,
            RuntimeReactiveEffectComparer.Instance
        );
        effects.Insert(index < 0 ? ~index : index, reactiveEffect);
    }

    public RuntimeReactiveEffect[] Snapshot(ReactiveEffectTrigger trigger) =>
        _effectsByTrigger.TryGetValue(trigger, out var effects)
            ? effects.ToArray()
            : [];

    public void RemoveWhere(Predicate<RuntimeReactiveEffect> predicate)
    {
        foreach (var effects in _effectsByTrigger.Values)
        {
            effects.RemoveAll(predicate);
        }
    }

    public void Clear() => _effectsByTrigger.Clear();

    private sealed class RuntimeReactiveEffectComparer :
        IComparer<RuntimeReactiveEffect>
    {
        public static RuntimeReactiveEffectComparer Instance { get; } = new();

        public int Compare(
            RuntimeReactiveEffect? left,
            RuntimeReactiveEffect? right
        )
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }
            if (left is null)
            {
                return 1;
            }
            if (right is null)
            {
                return -1;
            }

            var priority = right.Definition.Priority.CompareTo(
                left.Definition.Priority
            );
            return priority != 0
                ? priority
                : left.RegistrationId.CompareTo(right.RegistrationId);
        }
    }
}
