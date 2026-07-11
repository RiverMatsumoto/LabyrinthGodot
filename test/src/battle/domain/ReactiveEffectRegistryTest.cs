namespace Labyrinth;

using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

public class ReactiveEffectRegistryTest(Node testScene) : TestClass(testScene)
{
    [Test]
    public void IndexesSnapshotsByTriggerAndOrdersRegistrations()
    {
        var registry = new ReactiveEffectRegistry();
        var low = Effect(1, ReactiveEffectTrigger.Damage, priority: 0);
        var firstHigh = Effect(2, ReactiveEffectTrigger.Damage, priority: 10);
        var secondHigh = Effect(3, ReactiveEffectTrigger.Damage, priority: 10);
        var healing = Effect(4, ReactiveEffectTrigger.Healing, priority: 20);

        registry.Add(low);
        registry.Add(firstHigh);
        registry.Add(secondHigh);
        registry.Add(healing);

        registry.Snapshot(ReactiveEffectTrigger.Damage)
            .ShouldBe([firstHigh, secondHigh, low]);
        registry.Snapshot(ReactiveEffectTrigger.Healing)
            .ShouldBe([healing]);
        registry.Snapshot(ReactiveEffectTrigger.Defeat).ShouldBeEmpty();
    }

    [Test]
    public void SnapshotIsStableAcrossRegistryChanges()
    {
        var registry = new ReactiveEffectRegistry();
        var first = Effect(1, ReactiveEffectTrigger.Damage, priority: 0);
        var second = Effect(2, ReactiveEffectTrigger.Damage, priority: 0);
        registry.Add(first);
        var snapshot = registry.Snapshot(ReactiveEffectTrigger.Damage);

        registry.Add(second);
        registry.RemoveWhere(effect => effect == first);

        snapshot.ShouldBe([first]);
        registry.Snapshot(ReactiveEffectTrigger.Damage).ShouldBe([second]);
    }

    [Test]
    public void ClearRemovesEveryTriggerBucket()
    {
        var registry = new ReactiveEffectRegistry();
        registry.Add(Effect(1, ReactiveEffectTrigger.Damage, priority: 0));
        registry.Add(Effect(2, ReactiveEffectTrigger.Healing, priority: 0));

        registry.Clear();

        registry.Snapshot(ReactiveEffectTrigger.Damage).ShouldBeEmpty();
        registry.Snapshot(ReactiveEffectTrigger.Healing).ShouldBeEmpty();
    }

    private static RuntimeReactiveEffect Effect(
        long registrationId,
        ReactiveEffectTrigger trigger,
        int priority
    ) => new(
        registrationId,
        new BattlerId($"owner_{registrationId}"),
        new ReactiveEffectDefinition(
            new ReactiveEffectId($"effect_{registrationId}"),
            trigger,
            ReactiveEffectSchedule.Immediate,
            ReactiveEffectTargetPolicy.Owner,
            priority,
            [],
            []
        ),
        null
    );
}
