#if DEBUG
namespace Labyrinth;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Chickensoft.Sync.Primitives;
using Godot;

public partial class DebugConsole : CanvasLayer
{
    public const string MapMovementGroup = "debug_map_movement";

    private const int MaxOutputLines = 1000;
    private const int MaxCommandHistory = 20;

    private readonly List<string> _outputLines = [];
    private readonly List<string> _commandHistory = [];

    private IGameRepo _gameRepo = default!;
    private IMapRepo _mapRepo = default!;
    private PanelContainer _panel = default!;
    private RichTextLabel _output = default!;
    private LineEdit _commandInput = default!;

    private AutoChannel.Binding? _mapBinding;
    private AutoValue<GameMode>.Binding? _gameModeBinding;
    private AutoValue<MenuOverlay>.Binding? _overlayBinding;
    private AutoValue<MapMovementSettings>.Binding? _settingsBinding;

    private int _historyIndex;
    private bool _isInitialized;
    private bool _isOpen;
    private bool _wasTreePaused;
    private bool _isShutdown;
    private Input.MouseModeEnum _previousMouseMode;

    public void Initialize(IGameRepo gameRepo, IMapRepo mapRepo)
    {
        if (_isInitialized)
        {
            throw new InvalidOperationException(
                "DebugConsole has already been initialized."
            );
        }

        _gameRepo = gameRepo;
        _mapRepo = mapRepo;
        _isInitialized = true;
    }

    public override void _Ready()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException(
                "DebugConsole.Initialize must be called before adding it "
                    + "to the scene tree."
            );
        }

        Name = "DebugConsole";
        Layer = 1000;
        ProcessMode = ProcessModeEnum.Always;

        BuildUi();
        Append("Labyrinth debug console. Type 'help' for commands.");
        BindRepos();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey key
            || !key.Pressed
            || key.Echo)
        {
            return;
        }

        if (key.Keycode == Key.Quoteleft
            || key.PhysicalKeycode == Key.Quoteleft)
        {
            SetOpen(!_isOpen);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!_isOpen)
        {
            return;
        }

        if (key.Keycode == Key.Escape)
        {
            SetOpen(false);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_commandInput.HasFocus() && key.Keycode == Key.Up)
        {
            NavigateHistory(-1);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_commandInput.HasFocus() && key.Keycode == Key.Down)
        {
            NavigateHistory(1);
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _ExitTree() => Shutdown();

    public void Shutdown()
    {
        if (_isShutdown)
        {
            return;
        }

        _isShutdown = true;

        if (_isOpen && IsInsideTree())
        {
            RestoreGameInputState();
        }

        _mapBinding?.Dispose();
        _gameModeBinding?.Dispose();
        _overlayBinding?.Dispose();
        _settingsBinding?.Dispose();
    }

    private void BuildUi()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        margin.OffsetLeft = 12;
        margin.OffsetTop = 12;
        margin.OffsetRight = -12;
        margin.OffsetBottom = 430;
        margin.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(margin);

        _panel = new PanelContainer
        {
            Visible = false,
        };
        margin.AddChild(_panel);

        var layout = new VBoxContainer();
        _panel.AddChild(layout);

        var title = new Label
        {
            Text = "DEBUG CONSOLE  [` to close, Up/Down for history]",
        };
        title.AddThemeFontSizeOverride("font_size", 14);
        layout.AddChild(title);

        _output = new RichTextLabel
        {
            BbcodeEnabled = false,
            FitContent = false,
            ScrollActive = true,
            ScrollFollowing = true,
            SelectionEnabled = true,
            CustomMinimumSize = new Vector2(0, 310),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        _output.AddThemeFontSizeOverride("normal_font_size", 12);
        layout.AddChild(_output);

        var hint = new Label
        {
            Text = "Examples: map pose player | game mode labyrinth "
                + "| settings movement 0.2 0.04",
        };
        hint.AddThemeFontSizeOverride("font_size", 11);
        layout.AddChild(hint);

        var inputRow = new HBoxContainer();
        layout.AddChild(inputRow);
        var prompt = new Label { Text = ">" };
        prompt.AddThemeFontSizeOverride("font_size", 12);
        inputRow.AddChild(prompt);

        _commandInput = new LineEdit
        {
            PlaceholderText = "Enter command",
            KeepEditingOnTextSubmit = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _commandInput.AddThemeFontSizeOverride("font_size", 12);
        _commandInput.TextSubmitted += OnCommandSubmitted;
        inputRow.AddChild(_commandInput);
    }

    private void BindRepos()
    {
        _mapBinding = _mapRepo.AutoChannel.Bind()
            .On((in IMapRepo.MapEntityWasRegistered message) =>
                Append(
                    $"event map.registered id={message.Id} "
                        + $"position={message.InitialPosition}"
                ))
            .On((in IMapRepo.MapEntityWasUnregistered message) =>
                Append($"event map.unregistered id={message.Id}"));

        _gameModeBinding = _gameRepo.CurrentGameState.Bind()
            .OnValue(mode => Append($"state game.mode={mode}"));

        _overlayBinding = _gameRepo.GameMenuOverlay.Bind()
            .OnValue(overlay => Append($"state game.overlay={overlay}"));

        _settingsBinding = _gameRepo.MapMovementSettings.Bind()
            .OnValue(settings => Append(
                "state settings.movement "
                    + $"duration={Format(settings.MoveDuration)} "
                    + $"cooldown={Format(settings.MoveCooldown)}"
            ));
    }

    private void SetOpen(bool isOpen)
    {
        if (_isOpen == isOpen)
        {
            return;
        }

        _isOpen = isOpen;
        _panel.Visible = isOpen;

        if (isOpen)
        {
            var tree = GetTree();
            _wasTreePaused = tree.Paused;
            _previousMouseMode = Input.MouseMode;
            tree.Paused = true;
            Input.MouseMode = Input.MouseModeEnum.Visible;
            _historyIndex = _commandHistory.Count;
            _commandInput.GrabFocus();
            _commandInput.Edit();
            return;
        }

        _commandInput.ReleaseFocus();
        RestoreGameInputState();
    }

    private void RestoreGameInputState()
    {
        GetTree().Paused = _wasTreePaused;
        Input.MouseMode = _previousMouseMode;
    }

    private void OnCommandSubmitted(string command)
    {
        command = command.Trim();
        _commandInput.Clear();

        if (command.Length == 0)
        {
            return;
        }

        AddCommandHistory(command);
        Append($"> {command}");

        if (!TryTokenize(command, out var tokens, out var error))
        {
            Append($"error: {error}");
            return;
        }

        try
        {
            var response = Execute(tokens);
            if (!string.IsNullOrEmpty(response))
            {
                Append(response);
            }
        }
        catch (Exception exception)
        {
            Append($"error: {exception.Message}");
        }
    }

    private string? Execute(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            return null;
        }

        return args[0].ToLowerInvariant() switch
        {
            "help" => Help(args),
            "clear" => ClearOutput(),
            "status" => Status(),
            "map" => MapCommand(args),
            "game" => GameCommand(args),
            "settings" => SettingsCommand(args),
            _ => $"error: unknown command '{args[0]}'. Type 'help'.",
        };
    }

    private static string Help(IReadOnlyList<string> args)
    {
        if (args.Count > 2)
        {
            return "usage: help [map|game|settings]";
        }

        if (args.Count == 2)
        {
            return args[1].ToLowerInvariant() switch
            {
                "map" => MapHelp,
                "game" => GameHelp,
                "settings" => SettingsHelp,
                _ => $"error: no help topic '{args[1]}'.",
            };
        }

        return "Commands:\n"
            + "  help [map|game|settings]\n"
            + "  clear\n"
            + "  status\n"
            + "  map ...\n"
            + "  game ...\n"
            + "  settings ...";
    }

    private string? ClearOutput()
    {
        _outputLines.Clear();
        RenderOutput();
        return null;
    }

    private string Status()
    {
        var settings = _gameRepo.MapMovementSettings.Value;
        var lines = new List<string>
        {
            $"game.mode={_gameRepo.CurrentGameState.Value}",
            $"game.overlay={_gameRepo.GameMenuOverlay.Value}",
            "settings.movement="
                + $"{Format(settings.MoveDuration)},"
                + $"{Format(settings.MoveCooldown)}",
            $"map.player_registered={_mapRepo.PlayerIsRegistered}",
        };

        var movements = GetTree().GetNodesInGroup(MapMovementGroup);
        lines.Add($"map.movement_nodes={movements.Count}");

        foreach (var node in movements)
        {
            if (node is not MapMovement movement)
            {
                continue;
            }

            lines.Add($"  {DescribeMovement(movement)}");
        }

        return string.Join('\n', lines);
    }

    private string MapCommand(IReadOnlyList<string> args)
    {
        if (args.Count < 2)
        {
            return MapHelp;
        }

        return args[1].ToLowerInvariant() switch
        {
            "register" => RegisterEntity(args),
            "unregister" => UnregisterEntity(args),
            "pose" => EntityPose(args),
            "move" => MoveEntity(args),
            "turn" => TurnEntity(args),
            _ => $"error: unknown map command '{args[1]}'.\n{MapHelp}",
        };
    }

    private string RegisterEntity(IReadOnlyList<string> args)
    {
        if (args.Count is < 5 or > 6)
        {
            return "usage: map register <id> <x> <y> "
                + "[north|east|south|west]";
        }

        if (!TryParseInt(args[3], out var x)
            || !TryParseInt(args[4], out var y))
        {
            return "error: x and y must be integers.";
        }

        var direction = GridDirection.North;
        if (args.Count == 6
            && !TryParseDirection(args[5], out direction))
        {
            return "error: direction must be north, east, south, or west.";
        }

        var id = new MapEntityId(args[2]);
        var position = new Vector2I(x, y);
        var registered = _mapRepo.TryRegisterEntity(
            id,
            position,
            direction
        );

        return registered
            ? $"ok: registered {id} at {position}."
            : $"rejected: could not register {id} at {position}.";
    }

    private string UnregisterEntity(IReadOnlyList<string> args)
    {
        if (args.Count != 3)
        {
            return "usage: map unregister <id>";
        }

        var id = new MapEntityId(args[2]);
        return _mapRepo.TryUnregisterEntity(id)
            ? $"ok: unregistered {id}."
            : $"rejected: could not unregister {id}.";
    }

    private string EntityPose(IReadOnlyList<string> args)
    {
        if (args.Count != 3)
        {
            return "usage: map pose <id>";
        }

        var id = new MapEntityId(args[2]);
        return _mapRepo.TryGetEntityPose(id, out var pose)
            ? $"pose {id}: position={pose.Position} "
                + $"facing={DirectionName(pose.FacingDirection)}"
            : $"error: entity '{id}' was not found.";
    }

    private string MoveEntity(IReadOnlyList<string> args)
    {
        if (args.Count != 4)
        {
            return "usage: map move <id> "
                + "<north|east|south|west>";
        }

        if (!TryParseDirection(args[3], out var direction))
        {
            return "error: direction must be north, east, south, or west.";
        }

        var id = new MapEntityId(args[2]);
        if (!TryGetIdleMovement(id, out var movement, out var error))
        {
            return error;
        }

        movement.MapMovementLogic.RequestMove(direction);
        return $"ok: requested {id} move {DirectionName(direction)}.";
    }

    private string TurnEntity(IReadOnlyList<string> args)
    {
        if (args.Count != 4)
        {
            return "usage: map turn <id> <left|right>";
        }

        if (!TryParseTurnDirection(args[3], out var direction))
        {
            return "error: turn direction must be left or right.";
        }

        var id = new MapEntityId(args[2]);
        if (!TryGetIdleMovement(id, out var movement, out var error))
        {
            return error;
        }

        movement.MapMovementLogic.RequestTurn(direction);
        return $"ok: requested {id} turn "
            + $"{direction.ToString().ToLowerInvariant()}.";
    }

    private string GameCommand(IReadOnlyList<string> args)
    {
        if (args.Count != 3)
        {
            return GameHelp;
        }

        return args[1].ToLowerInvariant() switch
        {
            "mode" => SetGameMode(args[2]),
            "overlay" => SetGameOverlay(args[2]),
            _ => $"error: unknown game command '{args[1]}'.\n{GameHelp}",
        };
    }

    private string SetGameMode(string value)
    {
        switch (value.ToLowerInvariant())
        {
            case "main-menu":
            case "mainmenu":
                _gameRepo.EnterMainMenu();
                break;
            case "town":
                _gameRepo.EnterTown();
                break;
            case "labyrinth":
                _gameRepo.EnterLabyrinth();
                break;
            case "battle":
                _gameRepo.EnterBattle();
                break;
            default:
                return "error: mode must be main-menu, town, labyrinth, "
                    + "or battle.";
        }

        return $"ok: game mode is {_gameRepo.CurrentGameState.Value}.";
    }

    private string SetGameOverlay(string value)
    {
        switch (value.ToLowerInvariant())
        {
            case "none":
                _gameRepo.CloseMenuHub();
                break;
            case "menu":
            case "menu-hub":
                _gameRepo.CloseMenuHub();
                _gameRepo.OpenMenuHub();
                break;
            case "settings":
                _gameRepo.CloseMenuHub();
                _gameRepo.OpenSettings();
                break;
            default:
                return "error: overlay must be none, menu, or settings.";
        }

        return $"ok: game overlay is {_gameRepo.GameMenuOverlay.Value}.";
    }

    private string SettingsCommand(IReadOnlyList<string> args)
    {
        if (args.Count != 4
            || !args[1].Equals(
                "movement",
                StringComparison.OrdinalIgnoreCase
            ))
        {
            return SettingsHelp;
        }

        if (!TryParseDouble(args[2], out var duration)
            || !TryParseDouble(args[3], out var cooldown))
        {
            return "error: duration and cooldown must be numbers.";
        }

        if (duration < 0 || cooldown < 0)
        {
            return "error: duration and cooldown cannot be negative.";
        }

        _gameRepo.SetMapMovementSettings(new MapMovementSettings(
            MoveDuration: duration,
            MoveCooldown: cooldown
        ));

        return "ok: movement settings updated.";
    }

    private bool TryGetIdleMovement(
        MapEntityId id,
        out MapMovement movement,
        out string error
    )
    {
        movement = FindMovement(id)!;
        if (movement is null)
        {
            error = $"error: movement node for '{id}' was not found.";
            return false;
        }

        var state = movement.MapMovementLogic.State;
        if (state is not MapMovementLogicState.Idle)
        {
            error = $"rejected: '{id}' movement state is "
                + $"{state?.GetType().Name ?? "NotStarted"}.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private MapMovement? FindMovement(MapEntityId id)
    {
        foreach (var node in GetTree().GetNodesInGroup(MapMovementGroup))
        {
            if (node is MapMovement movement && movement.EntityId == id)
            {
                return movement;
            }
        }

        return null;
    }

    private string DescribeMovement(MapMovement movement)
    {
        var state = movement.MapMovementLogic.State?.GetType().Name
            ?? "NotStarted";
        if (!_mapRepo.TryGetEntityPose(movement.EntityId, out var pose))
        {
            return $"{movement.EntityId}: state={state}, pose=missing";
        }

        return $"{movement.EntityId}: state={state}, "
            + $"position={pose.Position}, "
            + $"facing={DirectionName(pose.FacingDirection)}";
    }

    private void AddCommandHistory(string command)
    {
        if (_commandHistory.Count == 0
            || _commandHistory[^1] != command)
        {
            _commandHistory.Add(command);
            if (_commandHistory.Count > MaxCommandHistory)
            {
                _commandHistory.RemoveAt(0);
            }
        }

        _historyIndex = _commandHistory.Count;
    }

    private void NavigateHistory(int offset)
    {
        if (_commandHistory.Count == 0)
        {
            return;
        }

        _historyIndex = Math.Clamp(
            _historyIndex + offset,
            0,
            _commandHistory.Count
        );

        _commandInput.Text = _historyIndex == _commandHistory.Count
            ? string.Empty
            : _commandHistory[_historyIndex];
        _commandInput.CaretColumn = _commandInput.Text.Length;
    }

    private void Append(string message)
    {
        var normalized = message.Replace("\r\n", "\n");
        foreach (var line in normalized.Split('\n'))
        {
            _outputLines.Add(line);
        }

        while (_outputLines.Count > MaxOutputLines)
        {
            _outputLines.RemoveAt(0);
        }

        RenderOutput();
    }

    private void RenderOutput()
    {
        if (_output is null)
        {
            return;
        }

        _output.Text = string.Join('\n', _outputLines);
    }

    private static bool TryTokenize(
        string command,
        out List<string> tokens,
        out string error
    )
    {
        tokens = [];
        error = string.Empty;
        var token = new StringBuilder();
        var inQuotes = false;
        var escaping = false;
        var tokenStarted = false;

        foreach (var character in command)
        {
            if (escaping)
            {
                token.Append(character);
                tokenStarted = true;
                escaping = false;
                continue;
            }

            if (character == '\\')
            {
                escaping = true;
                tokenStarted = true;
                continue;
            }

            if (character == '"')
            {
                inQuotes = !inQuotes;
                tokenStarted = true;
                continue;
            }

            if (char.IsWhiteSpace(character) && !inQuotes)
            {
                if (tokenStarted)
                {
                    tokens.Add(token.ToString());
                    token.Clear();
                    tokenStarted = false;
                }

                continue;
            }

            token.Append(character);
            tokenStarted = true;
        }

        if (escaping)
        {
            token.Append('\\');
        }

        if (inQuotes)
        {
            error = "unterminated quoted argument.";
            return false;
        }

        if (tokenStarted)
        {
            tokens.Add(token.ToString());
        }

        return true;
    }

    private static bool TryParseInt(string value, out int result) =>
        int.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out result
        );

    private static bool TryParseDouble(string value, out double result) =>
        double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out result
        );

    private static bool TryParseDirection(
        string value,
        out Vector2I direction
    )
    {
        direction = value.ToLowerInvariant() switch
        {
            "north" => GridDirection.North,
            "east" => GridDirection.East,
            "south" => GridDirection.South,
            "west" => GridDirection.West,
            _ => default,
        };

        return GridDirection.IsValid(direction);
    }

    private static bool TryParseTurnDirection(
        string value,
        out TurnDirection direction
    )
    {
        switch (value.ToLowerInvariant())
        {
            case "left":
                direction = TurnDirection.Left;
                return true;
            case "right":
                direction = TurnDirection.Right;
                return true;
            default:
                direction = default;
                return false;
        }
    }

    private static string DirectionName(Vector2I direction)
    {
        if (direction == GridDirection.North)
        {
            return "north";
        }

        if (direction == GridDirection.East)
        {
            return "east";
        }

        if (direction == GridDirection.South)
        {
            return "south";
        }

        if (direction == GridDirection.West)
        {
            return "west";
        }

        return direction.ToString();
    }

    private static string Format(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    private const string MapHelp =
        "Map commands:\n"
        + "  map register <id> <x> <y> "
        + "[north|east|south|west]\n"
        + "  map unregister <id>\n"
        + "  map pose <id>\n"
        + "  map move <id> <north|east|south|west>\n"
        + "  map turn <id> <left|right>";

    private const string GameHelp =
        "Game commands:\n"
        + "  game mode <main-menu|town|labyrinth|battle>\n"
        + "  game overlay <none|menu|settings>";

    private const string SettingsHelp =
        "Settings commands:\n"
        + "  settings movement <duration> <cooldown>";
}
#endif
