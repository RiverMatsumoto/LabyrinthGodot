using Godot;

namespace Labyrinth;

public interface IInstantiator
{
    SceneTree SceneTree { get; }

    T LoadAndInstantiate<T>(string path) where T : Node;
}

public sealed class Instantiator(SceneTree sceneTree) : IInstantiator
{
    public SceneTree SceneTree { get; } = sceneTree;

    public T LoadAndInstantiate<T>(string path) where T : Node =>
      GD.Load<PackedScene>(path).Instantiate<T>();
}
