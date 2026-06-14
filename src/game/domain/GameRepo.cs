namespace Labyrinth;

using System;
using Chickensoft.Sync.Primitives;

public interface IGameRepo : IDisposable
{
    IAutoValue<MapMovementSettings> MapMovementSettings { get; }
    IAutoValue<bool> IsInMenu { get; }

    void SetMapMovementSettings(MapMovementSettings settings);
    void SetIsInMenu(bool isInMenu);
}

public partial class GameRepo : IGameRepo
{
    public IAutoValue<MapMovementSettings> MapMovementSettings =>
        _mapMovementSettings;

    public IAutoValue<bool> IsInMenu => _isInMenu;
    private readonly AutoValue<bool> _isInMenu;

    private readonly AutoValue<MapMovementSettings> _mapMovementSettings;

    private bool _disposedValue;

    public GameRepo()
    {
        _mapMovementSettings = new AutoValue<MapMovementSettings>(
            Labyrinth.MapMovementSettings.Default
        );
        _isInMenu = new AutoValue<bool>(false);
    }

    public void SetMapMovementSettings(MapMovementSettings settings)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(settings.MoveDuration);
        ArgumentOutOfRangeException.ThrowIfNegative(settings.MoveCooldown);

        _mapMovementSettings.Value = settings;
    }

    public void SetIsInMenu(bool isInMenu) => _isInMenu.Value = isInMenu;

    #region Internals

    public void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _mapMovementSettings.Dispose();
            }
            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion
}
