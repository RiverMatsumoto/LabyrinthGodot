namespace Labyrinth;

using Godot;

/// <summary>Godot authoring base for a compiled reactive effect condition.</summary>
public abstract partial class BattleReactiveEffectConditionResource : Resource
{
    public abstract ReactiveEffectConditionDefinition Compile();
}
