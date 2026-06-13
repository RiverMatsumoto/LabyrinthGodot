namespace Labyrinth;

using System;
using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;
using Chickensoft.LogicBlocks.Auto;

public interface IGameLogic : ILogicBlock
{
    GameMode CurrentMode { get; }
    MenuOverlay CurrentOverlay { get; }
    MapMovementSettings MovementSettings { get; }

    void RequestMode(GameMode mode);
    void RequestOverlay(MenuOverlay overlay);
    void RequestMovementSettings(MapMovementSettings settings);
}

[Meta]
public partial class GameLogic : AutoBlock, IGameLogic
{
    public GameLogic()
    {
        Preallocate<GameLogicState>();
    }

    public GameMode CurrentMode => Get<IGameRepo>().CurrentGameState.Value;
    public MenuOverlay CurrentOverlay => Get<IGameRepo>().GameMenuOverlay.Value;
    public MapMovementSettings MovementSettings =>
        Get<IGameRepo>().MapMovementSettings.Value;

    public void RequestMode(GameMode mode)
    {
        if (CurrentMode == mode)
        {
            return;
        }

        switch (mode)
        {
            case GameMode.MainMenu:
                Input(new GameLogicState.Input.EnterMainMenu());
                break;
            case GameMode.Town:
                Input(new GameLogicState.Input.EnterTown());
                break;
            case GameMode.Labyrinth:
                Input(new GameLogicState.Input.EnterLabyrinth());
                break;
            case GameMode.Battle:
                Input(new GameLogicState.Input.EnterBattle());
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(mode),
                    mode,
                    "The requested game mode has no corresponding state."
                );
        }
    }

    public void RequestOverlay(MenuOverlay overlay) =>
        Input(new GameLogicState.Input.SetOverlay(overlay));

    public void RequestMovementSettings(MapMovementSettings settings) =>
        Input(new GameLogicState.Input.SetMovementSettings(settings));
}
