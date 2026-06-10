namespace Labyrinth;

public readonly record struct MapEntityId(string Value)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}
