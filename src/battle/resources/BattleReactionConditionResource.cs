namespace Labyrinth;

using Godot;

/// <summary>Godot authoring base for a compiled reaction condition.</summary>
public abstract partial class BattleReactionConditionResource : Resource
{
    public abstract ReactionConditionDefinition Compile();
}
