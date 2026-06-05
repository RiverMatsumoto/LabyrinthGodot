namespace Labyrinth;

using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;

[Meta]
[StateDiagram]
public abstract partial record GameState : LogicBlockState
{
    public static class Input
    {
        public readonly record struct EnterMainMenu;
        public readonly record struct EnterGame;
    }

    public static class Output
    {
        public readonly record struct EnteredMainMenu;
        public readonly record struct EnteredGame;
    }
}
