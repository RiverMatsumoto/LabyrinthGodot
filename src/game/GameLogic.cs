namespace Labyrinth;

using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;
using Chickensoft.LogicBlocks.Auto;

public interface IGameLogic : ILogicBlock
{
    MapMovementSettings MovementSettings { get; }

    void RequestMovementSettings(MapMovementSettings settings);
}

[Meta]
public partial class GameLogic : AutoBlock, IGameLogic
{
    public GameLogic()
    {
        Preallocate<GameLogicState>();
    }

    public MapMovementSettings MovementSettings =>
        Get<IGameRepo>().MapMovementSettings.Value;

    public void RequestMovementSettings(MapMovementSettings settings) =>
        Input(new GameLogicState.Input.SetMovementSettings(settings));
}
