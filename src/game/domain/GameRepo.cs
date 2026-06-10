namespace Labyrinth;

using System;
using Chickensoft.AutoInject;
using Chickensoft.Sync.Primitives;

public interface IGameRepo : IDisposable
{
    IAutoValue<GameMode> CurrentGameState { get; }
    IAutoValue<MenuOverlay> GameMenuOverlay { get; }
    #region Settings
    IAutoValue<double> MapMoveDuration { get; }
    #endregion

    void EnterMainMenu();
    void EnterTown();
    void EnterLabyrinth();
    void EnterBattle();
    void EnterMap();
    void OpenMenuHub();
    void CloseMenuHub();
    void OpenSettings();
    void CloseSettings();
}

public partial class GameRepo : IGameRepo
{
    public IAutoValue<GameMode> CurrentGameState => _currentGameMode;
    private readonly AutoValue<GameMode> _currentGameMode;

    public IAutoValue<double> MapMoveDuration => _mapMoveSpeed;
    private readonly AutoValue<double> _mapMoveSpeed;

    public IAutoValue<MenuOverlay> GameMenuOverlay => _menuOverlay;
    private readonly AutoValue<MenuOverlay> _menuOverlay;

    private bool _disposedValue;

    public GameRepo()
    {
        _menuOverlay = new AutoValue<MenuOverlay>(MenuOverlay.None);
        _currentGameMode = new AutoValue<GameMode>(GameMode.MainMenu);
        _mapMoveSpeed = new AutoValue<double>(0.18);
    }

    public void EnterMainMenu()
    {
        _currentGameMode.Value = GameMode.MainMenu;
    }

    public void EnterTown()
    {
        _currentGameMode.Value = GameMode.Town;
    }

    public void EnterLabyrinth()
    {
        _currentGameMode.Value = GameMode.Labyrinth;
    }

    public void EnterBattle()
    {
        _currentGameMode.Value = GameMode.Battle;
    }

    public void EnterMap() => EnterLabyrinth();

    public void OpenMenuHub()
    {
        if (_menuOverlay.Value != MenuOverlay.None)
            return;
        _menuOverlay.Value = MenuOverlay.MenuHub;
    }

    public void CloseMenuHub()
    {
        _menuOverlay.Value = MenuOverlay.None;
    }

    public void OpenSettings()
    {
        if (_menuOverlay.Value == MenuOverlay.None)
        {
            _menuOverlay.Value = MenuOverlay.Settings;
        }
    }

    public void CloseSettings()
    {
        _menuOverlay.Value = MenuOverlay.None;
    }

    #region Internals

    public void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _menuOverlay.Dispose();
                _currentGameMode.Dispose();
            }
            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    #endregion
}
