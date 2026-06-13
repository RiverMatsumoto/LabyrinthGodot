namespace Labyrinth;

using System;
using Chickensoft.AutoInject;
using Chickensoft.Sync.Primitives;

public interface IGameRepo : IDisposable
{
    IAutoValue<GameMode> CurrentGameState { get; }
    IAutoValue<MenuOverlay> GameMenuOverlay { get; }
    IAutoValue<MapMovementSettings> MapMovementSettings { get; }

    void EnterMainMenu();
    void EnterTown();
    void EnterLabyrinth();
    void EnterBattle();
    void EnterMap();
    void OpenMenuHub();
    void CloseMenuHub();
    void OpenSettings();
    void CloseSettings();
    void SetMapMovementSettings(MapMovementSettings settings);
}

public partial class GameRepo : IGameRepo
{
    public IAutoValue<GameMode> CurrentGameState => _currentGameMode;
    private readonly AutoValue<GameMode> _currentGameMode;

    public IAutoValue<MenuOverlay> GameMenuOverlay => _menuOverlay;
    private readonly AutoValue<MenuOverlay> _menuOverlay;

    public IAutoValue<MapMovementSettings> MapMovementSettings =>
        _mapMovementSettings;
    private readonly AutoValue<MapMovementSettings> _mapMovementSettings;

    private bool _disposedValue;

    public GameRepo()
    {
        _menuOverlay = new AutoValue<MenuOverlay>(MenuOverlay.None);
        _currentGameMode = new AutoValue<GameMode>(GameMode.MainMenu);
        _mapMovementSettings = new AutoValue<MapMovementSettings>(
            Labyrinth.MapMovementSettings.Default
        );
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

    public void SetMapMovementSettings(MapMovementSettings settings)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(settings.MoveDuration);
        ArgumentOutOfRangeException.ThrowIfNegative(settings.MoveCooldown);

        _mapMovementSettings.Value = settings;
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
                _mapMovementSettings.Dispose();
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
