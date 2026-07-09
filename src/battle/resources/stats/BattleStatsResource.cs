namespace Labyrinth;

using System;
using Godot;

[GlobalClass]
public partial class BattleStatsResource : Resource
{
    [Export] public int MaxHp { get; set; } = 100;
    [Export] public int MaxTp { get; set; } = 20;
    [Export] public int Strength { get; set; } = 10;
    [Export] public int Technique { get; set; } = 10;
    [Export] public int Agility { get; set; } = 10;
    [Export] public int Vitality { get; set; } = 10;
    [Export] public int Wisdom { get; set; } = 10;
    [Export] public int Luck { get; set; } = 10;
    [Export] public int Attack { get; set; }
    [Export] public int Defense { get; set; }

    public BattleStats Compile() => new(
        Math.Max(1, MaxHp),
        Math.Max(0, MaxTp),
        Strength,
        Technique,
        Agility,
        Vitality,
        Wisdom,
        Luck,
        Attack,
        Defense
    );
}
