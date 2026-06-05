namespace Labyrinth;

using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;
using Godot;

[Meta]
[StateDiagram]
public abstract partial record MapMovementState : LogicBlockState
{
    public static class Input
    {
        // public readonly record struct <NameOfInput>(DataAttachedToTheInput);
        public readonly record struct Moved(Vector2 Direction);
    }

    public static class Output
    {
        // public readonly record struct <NameOfOutput>(DataAttachedToTheOutput);
    }
}
