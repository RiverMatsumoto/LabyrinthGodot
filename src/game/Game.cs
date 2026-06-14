namespace Labyrinth;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Chickensoft.LogicBlocks.Auto;
using Chickensoft.SaveFileBuilder;
using Chickensoft.Serialization;
using Chickensoft.Serialization.Godot;
using Godot;

public interface IGame : INode,
    IProvide<IGameRepo>,
    IProvide<IMapRepo>,
    IProvide<IPartyRepo>,
    IProvide<IGameLogic>,
    IProvide<ISaveChunk<GameData>>
{
    bool IsSaveReady { get; }
    Task<bool> SaveGame(int id);
    Task<bool> LoadGame(int id);
    event Game.SaveFileLoadedEventHandler? SaveFileLoaded;
}

[Meta(typeof(IAutoNode))]
public partial class Game : Node, IGame
{
    public override void _Notification(int what) => this.Notify(what);

#if DEBUG
    private DebugConsole? _debugConsole;
#endif

    #region Save
    [Signal]
    public delegate void SaveFileLoadedEventHandler(int id);
    public JsonSerializerOptions JsonOptions { get; set; } = default!;
    public const string SAVE_FILE_PREFIX = "game_";
    public const string SAVE_FILE_EXTENSION = ".json";
    public IFileSystem FileSystem { get; set; } = default!;
    public string SaveDirectoryPath { get; set; } = default!;
    public SaveFile<GameData> SaveFile { get; set; } = default!;
    public ISaveChunk<GameData> GameChunk { get; set; } = default!;
    ISaveChunk<GameData> IProvide<ISaveChunk<GameData>>.Value() =>
        SaveFile.Root;

    private int _activeSaveSlot = -1;
    private int _saveOperationInProgress;
    private string SaveFilePath() =>
        $"{SAVE_FILE_PREFIX}{_activeSaveSlot}{SAVE_FILE_EXTENSION}";

    public bool IsSaveReady
    {
        get
        {
            try
            {
                _ = GameChunk.GetChunk<MapMovementData>();
                return true;
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }
    }
    #endregion Save

    #region State
    public IGameRepo GameRepo { get; set; } = default!;
    public IMapRepo MapRepo { get; set; } = default!;
    public IPartyRepo PartyRepo { get; set; } = default!;
    #endregion State

    public IGameLogic GameLogic { get; set; } = default!;
    IGameLogic IProvide<IGameLogic>.Value() => GameLogic;
    [Node] public INode AreaRoot { get; private set; } = default!;
    [Node] public INode UiRoot { get; private set; } = default!;

    public void Setup()
    {
        FileSystem = new FileSystem();
        SaveDirectoryPath = OS.GetUserDataDir();

        GameRepo = new GameRepo();
        MapRepo = new MapRepo();
        PartyRepo = new PartyRepo();
        GameLogic = new GameLogic();
        GameLogic.Set(GameRepo);

        GodotSerialization.Setup();
        LogicBlockSerialization.Setup();

        JsonOptions = new JsonSerializerOptions
        {
            Converters = {
                new SerializableTypeConverter()
            },
            TypeInfoResolver = new SerializableTypeResolver(),
            WriteIndented = true
        };

        GameChunk = new SaveChunk<GameData>(
            (chunk) =>
            {
                var gameData = new GameData()
                {
                    MapMovementData = chunk.GetChunkSaveData<MapMovementData>(),
                    PartyData = PartyRepo.ToData(),
                };
                return gameData;
            },
            onLoad: (chunk, data) =>
            {
                chunk.LoadChunkSaveData(data.MapMovementData);
                PartyRepo.Load(data.PartyData ?? PartyData.Empty);
            }
        );

        SaveFile = new SaveFile<GameData>(
            root: GameChunk,
            onSave: async data =>
            {
                FileSystem.Directory.CreateDirectory(SaveDirectoryPath);
                var json = JsonSerializer.Serialize(data, JsonOptions);
                await FileSystem.File.WriteAllTextAsync(
                    GetSaveFilePath(_activeSaveSlot),
                    json
                );
            },
            onLoad: async () =>
            {
                var path = GetSaveFilePath(_activeSaveSlot);
                var json = await FileSystem.File.ReadAllTextAsync(path);
                return JsonSerializer.Deserialize<GameData>(json, JsonOptions)
                    ?? throw new InvalidDataException(
                        $"Save slot {_activeSaveSlot} contained no game data."
                    );
            }
        );
    }

    public void OnResolved()
    {
        SaveFile = new SaveFile<GameData>(
            root: GameChunk,
            onSave: async data =>
            {
                var json = JsonSerializer.Serialize(data, JsonOptions);
                await FileSystem.File.WriteAllTextAsync(SaveFilePath(), json);
            },
            onLoad: async () =>
            {
                // Load the game data from disk.
                if (!FileSystem.File.Exists(SaveFilePath()))
                {
                    GD.Print("No save file to load :'(");
                    return null;
                }

                var json = await FileSystem.File.ReadAllTextAsync(SaveFilePath());
                return JsonSerializer.Deserialize<GameData>(json, JsonOptions);
            }
        );
        this.Provide();
        GameLogic.Start<GameLogicState.MainMenu>();

        GameLogic.Input(new GameLogicState.Input.EnterLabyrinth());

#if DEBUG
        var map = GetNode<Map>("AreaRoot/Map");
        var menuHub = GetNode<MenuHub>("UiRoot/MenuHub");
        _debugConsole = new DebugConsole();
        _debugConsole.Initialize(GameLogic, menuHub.MenuHubLogic, map.MapLogic);
        UiRoot.AddChild(_debugConsole);
#endif
    }

    public void OnExitTree()
    {
#if DEBUG
        _debugConsole?.Shutdown();
#endif

        GameLogic.Dispose();
        GameRepo.Dispose();
        MapRepo.Dispose();
        PartyRepo.Dispose();
    }

    public IGameRepo Value() => GameRepo;
    IMapRepo IProvide<IMapRepo>.Value() => MapRepo;
    IPartyRepo IProvide<IPartyRepo>.Value() => PartyRepo;

    public string GetSaveFilePath(int id) => FileSystem.Path.Join(
        SaveDirectoryPath,
        $"{SAVE_FILE_PREFIX}{id}{SAVE_FILE_EXTENSION}"
    );

    public async Task<bool> SaveGame(int id)
    {
        if (!CanStartSaveOperation(id, "save"))
        {
            return false;
        }

        try
        {
            _activeSaveSlot = id;
            await SaveFile.Save();
            return true;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"Game: failed to save slot {id}: {exception.Message}");
            return false;
        }
        finally
        {
            FinishSaveOperation();
        }
    }

    public async Task<bool> LoadGame(int id)
    {
        if (!CanStartSaveOperation(id, "load"))
        {
            return false;
        }

        try
        {
            _activeSaveSlot = id;
            var path = GetSaveFilePath(id);
            if (!FileSystem.File.Exists(path))
            {
                GD.PrintErr($"Game: save slot {id} does not exist.");
                return false;
            }

            await SaveFile.Load();
            EmitSignal(SignalName.SaveFileLoaded, id);
            return true;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"Game: failed to load slot {id}: {exception.Message}");
            return false;
        }
        finally
        {
            FinishSaveOperation();
        }
    }

    private bool CanStartSaveOperation(int id, string operation)
    {
        if (id < 0)
        {
            GD.PrintErr($"Game: cannot {operation} negative slot {id}.");
            return false;
        }

        if (!IsSaveReady)
        {
            GD.PrintErr(
                $"Game: cannot {operation} before the player save chunk "
                    + "is registered."
            );
            return false;
        }

        if (Interlocked.CompareExchange(
            ref _saveOperationInProgress,
            1,
            0
        ) != 0)
        {
            GD.PrintErr(
                $"Game: cannot {operation} while another save operation "
                    + "is running."
            );
            return false;
        }

        return true;
    }

    private void FinishSaveOperation()
    {
        _activeSaveSlot = -1;
        Interlocked.Exchange(ref _saveOperationInProgress, 0);
    }
}
