namespace Labyrinth;

using Godot;

public abstract partial class BattleEffectResource : Resource
{
    public abstract BattleEffectDefinition Compile();
}
