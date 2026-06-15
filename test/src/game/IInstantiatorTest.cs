namespace Labyrinth;

using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

public class IInstantiatorTest(Node testScene) : TestClass(testScene) {
  [Test]
  public void LoadsAndInstantiatesScene() {
    var instantiator = new Instantiator(TestScene.GetTree());
    var movement = instantiator.LoadAndInstantiate<MapMovement>(
      Map.MapMovementScenePath
    );

    movement.ShouldNotBeNull();
    movement.Free();
  }
}
