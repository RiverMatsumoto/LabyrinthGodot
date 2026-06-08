namespace Labyrinth;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Godot;

public readonly record struct MapEntityId(string Value)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}

public readonly record struct GridMove(
    MapEntityId EntityId,
    Vector2I From,
    Vector2I To,
    Vector2I Offset
);
