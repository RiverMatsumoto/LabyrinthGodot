namespace Labyrinth;

using Godot;

[GlobalClass]
public abstract partial class BattleEffectResource : Resource
{
    public abstract BattleEffectDefinition Compile();
}
