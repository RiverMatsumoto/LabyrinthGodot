namespace Labyrinth;

using System;
using Godot;

[GlobalClass]
public partial class BattleStatusResource : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public StatusBehavior Behavior { get; set; }
    [Export] public int DefaultDuration { get; set; } = 1;
    [Export] public int MaxStacks { get; set; } = 1;
    [Export] public int PowerPerStack { get; set; }

    public StatusDefinition Compile()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new InvalidOperationException("Status id is required.");
        }
        return new StatusDefinition(
            new StatusId(Id),
            string.IsNullOrWhiteSpace(DisplayName) ? Id : DisplayName,
            Behavior,
            Math.Max(1, DefaultDuration),
            Math.Max(1, MaxStacks),
            Math.Max(0, PowerPerStack)
        );
    }
}
