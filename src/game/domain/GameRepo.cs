namespace Labyrinth;

using System;
using Chickensoft.AutoInject;
using Chickensoft.Sync.Primitives;

public interface IGameRepo : IDisposable
{
    IAutoValue<MapMovementSettings> MapMovementSettings { get; }

    void SetMapMovementSettings(MapMovementSettings settings);
}

public partial class GameRepo : IGameRepo
{
    public IAutoValue<MapMovementSettings> MapMovementSettings =>
        _mapMovementSettings;
    private readonly AutoValue<MapMovementSettings> _mapMovementSettings;

    private bool _disposedValue;

    public GameRepo()
    {
        _mapMovementSettings = new AutoValue<MapMovementSettings>(
            Labyrinth.MapMovementSettings.Default
        );
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
