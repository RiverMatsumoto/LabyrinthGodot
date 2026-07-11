namespace Labyrinth;

using System;
using System.Linq;
using Godot;
using Godot.Collections;

[Tool]
[GlobalClass]
public partial class DamageBattleEffectResource : BattleEffectResource
{
    private DamageMode _mode;
    private DamageValueSource _fixedAmountSource;
    private DamageValueSource _powerMultiplierSource;
    private bool _canCrit;

    [Export] public DamageType DamageType { get; set; }

    [Export]
    public DamageMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value)
            {
                return;
            }
            _mode = value;
            NotifyPropertyListChanged();
        }
    }

    [Export]
    public DamageValueSource FixedAmountSource
    {
        get => _fixedAmountSource;
        set
        {
            if (_fixedAmountSource == value)
            {
                return;
            }
            _fixedAmountSource = value;
            NotifyPropertyListChanged();
        }
    }

    [Export] public double FixedAmount { get; set; } = 1;
    [Export] public bool FixedAmountMultiplyByReactiveStatusPower { get; set; }
    [Export] public bool FixedAmountMultiplyByReactiveStatusStacks { get; set; }
    [Export] public string FixedAmountMultiplyBySourceStatusStacksId { get; set; } = "";

    [Export]
    public DamageValueSource PowerMultiplierSource
    {
        get => _powerMultiplierSource;
        set
        {
            if (_powerMultiplierSource == value)
            {
                return;
            }
            _powerMultiplierSource = value;
            NotifyPropertyListChanged();
        }
    }

    [Export] public double PowerMultiplier { get; set; } = 1;
    [Export] public bool PowerMultiplierMultiplyByReactiveStatusPower { get; set; }
    [Export] public bool PowerMultiplierMultiplyByReactiveStatusStacks { get; set; }
    [Export] public string PowerMultiplierMultiplyBySourceStatusStacksId { get; set; } = "";
    [Export] public Array<DamageStatScaleResource> StatScales { get; set; } = [];

    [Export]
    public bool CanCrit
    {
        get => _canCrit;
        set
        {
            if (_canCrit == value)
            {
                return;
            }
            _canCrit = value;
            NotifyPropertyListChanged();
        }
    }

    [Export] public double CritMultiplier { get; set; } = 2.5;
    [Export] public string AnimationId { get; set; } = "";

    public override BattleEffectDefinition Compile() =>
        new DamageEffectDefinition(
            new DamageSpec(
                DamageType,
                Mode,
                BuildValue(
                    FixedAmount,
                    FixedAmountSource,
                    FixedAmountMultiplyByReactiveStatusPower,
                    FixedAmountMultiplyByReactiveStatusStacks,
                    FixedAmountMultiplyBySourceStatusStacksId
                ),
                BuildValue(
                    PowerMultiplier,
                    PowerMultiplierSource,
                    PowerMultiplierMultiplyByReactiveStatusPower,
                    PowerMultiplierMultiplyByReactiveStatusStacks,
                    PowerMultiplierMultiplyBySourceStatusStacksId
                ),
                StatScales
                    .Where(scale => scale is not null)
                    .Select(scale => scale.Compile())
                    .ToArray(),
                CanCrit,
                Math.Max(1, CritMultiplier)
            ),
            AnimationId
        );

    public override void _ValidateProperty(Dictionary property)
    {
        var name = property["name"].AsStringName();
        if (
            HideFixedProperty(name)
            || HidePowerProperty(name)
            || HideCritProperty(name)
        )
        {
            Hide(property);
        }
    }

    private bool HideFixedProperty(StringName name) =>
        IsFixedProperty(name)
        && (
            Mode != DamageMode.Fixed
            || (
                name == "FixedAmount"
                && FixedAmountSource
                    == DamageValueSource.ReactiveStatusPower
            )
        );

    private bool HidePowerProperty(StringName name) =>
        IsPowerProperty(name)
        && (
            Mode != DamageMode.FromStats
            || (
                name == "PowerMultiplier"
                && PowerMultiplierSource
                    == DamageValueSource.ReactiveStatusPower
            )
        );

    private bool HideCritProperty(StringName name) =>
        name == "CritMultiplier" && !CanCrit;

    private static bool IsFixedProperty(StringName name) =>
        name == "FixedAmountSource"
        || name == "FixedAmount"
        || name == "FixedAmountMultiplyByReactiveStatusPower"
        || name == "FixedAmountMultiplyByReactiveStatusStacks"
        || name == "FixedAmountMultiplyBySourceStatusStacksId";

    private static bool IsPowerProperty(StringName name) =>
        name == "PowerMultiplierSource"
        || name == "PowerMultiplier"
        || name == "PowerMultiplierMultiplyByReactiveStatusPower"
        || name == "PowerMultiplierMultiplyByReactiveStatusStacks"
        || name == "PowerMultiplierMultiplyBySourceStatusStacksId"
        || name == "StatScales";

    private static void Hide(Dictionary property)
    {
        var usage = property["usage"].As<PropertyUsageFlags>();
        usage &= ~PropertyUsageFlags.Editor;
        usage |= PropertyUsageFlags.Storage;
        property["usage"] = (int)usage;
    }

    private static DamageValueDefinition BuildValue(
        double authoredValue,
        DamageValueSource source,
        bool multiplyByReactiveStatusPower,
        bool multiplyByReactiveStatusStacks,
        string sourceStatusStackScaleId
    ) => new(
        Math.Max(0, authoredValue),
        source,
        multiplyByReactiveStatusPower,
        multiplyByReactiveStatusStacks,
        string.IsNullOrWhiteSpace(sourceStatusStackScaleId)
            ? null
            : new StatusStackScaleDefinition(
                new StatusId(sourceStatusStackScaleId)
            )
    );
}
