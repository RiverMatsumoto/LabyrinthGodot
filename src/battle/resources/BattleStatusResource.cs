namespace Labyrinth;

using System;
using System.Linq;
using Godot;
using Godot.Collections;

/// <summary>
/// Authors action prevention, duration, stacking, and status-owned reactive effects.
/// </summary>
[GlobalClass]
public partial class BattleStatusResource : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public bool PreventsAction { get; set; }
    [Export] public int DefaultDuration { get; set; } = 1;
    [Export] public int MaxStacks { get; set; } = 1;
    [Export] public Array<string> ReactiveEffectIds { get; set; } = [];

    public StatusDefinition Compile()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new InvalidOperationException("Status id is required.");
        }
        return new StatusDefinition(
            new StatusId(Id),
            string.IsNullOrWhiteSpace(DisplayName) ? Id : DisplayName,
            PreventsAction,
            Math.Max(1, DefaultDuration),
            Math.Max(1, MaxStacks),
            ReactiveEffectIds.Select(id => new ReactiveEffectId(id)).ToArray()
        );
    }
}
