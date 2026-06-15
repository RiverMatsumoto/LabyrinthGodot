namespace Labyrinth;

using System;
using System.Collections.Generic;
using System.Linq;
using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;
using Godot;

/// <summary>
/// Exposes the battle scene's domain repository and interaction logic.
/// </summary>
public interface IBattle :
    IControl,
    IProvide<IBattleRepo>,
    IProvide<IBattleLogic>
{
    IBattleRepo BattleRepo { get; }
    IBattleLogic BattleLogic { get; }
}

/// <summary>
/// Coordinates the battle UI, game and party state, battle logic, and cue
/// presenter. Combat rules and runtime state remain in <see cref="IBattleRepo"/>.
/// </summary>
[Meta(typeof(IAutoNode))]
public partial class Battle : Control, IBattle
{
    public override void _Notification(int what) => this.Notify(what);

    [Export] public BattleContentResource? Content { get; set; }

    [Dependency] public IGameLogic GameLogic => this.DependOn<IGameLogic>();
    [Dependency] public IGameRepo GameRepo => this.DependOn<IGameRepo>();
    [Dependency] public IPartyRepo PartyRepo => this.DependOn<IPartyRepo>();

    public IBattleRepo BattleRepo { get; private set; } = default!;
    public IBattleLogic BattleLogic { get; private set; } = default!;
    IBattleRepo IProvide<IBattleRepo>.Value() => BattleRepo;
    IBattleLogic IProvide<IBattleLogic>.Value() => BattleLogic;

    private CompiledBattleContent _compiled = default!;
    private BattlePresenter _presenter = default!;
    private Label _turnLabel = default!;
    private Label _partyLabel = default!;
    private Label _enemyLabel = default!;
    private OptionButton _actionOption = default!;
    private OptionButton _targetOption = default!;
    private Button _confirmButton = default!;
    private Button _undoButton = default!;
    private Button _fleeButton = default!;
    private readonly List<ActionId> _actionIds = [];
    private readonly List<BattlerId> _targetIds = [];
    private BattlerId? _currentBattlerId;
    private BattleLogic.Binding? _battleBinding;
    private LogicBlock.Binding? _gameBinding;

    public void Setup()
    {
        _compiled = Content?.Compile() ?? CreateFallbackContent();
        BattleRepo = new BattleRepo(_compiled.Catalog);
        BattleLogic = new BattleLogic();
        BattleLogic.Set(BattleRepo);
        BattleLogic.Set<IEnemyCommandPlanner>(
            new BasicEnemyCommandPlanner()
        );
    }

    public void OnResolved()
    {
        _presenter = GetNode<BattlePresenter>("Panel/Margin/Layout/Presenter");
        _turnLabel = GetNode<Label>("Panel/Margin/Layout/Turn");
        _partyLabel = GetNode<Label>("Panel/Margin/Layout/Teams/Party");
        _enemyLabel = GetNode<Label>("Panel/Margin/Layout/Teams/Enemies");
        _actionOption = GetNode<OptionButton>(
            "Panel/Margin/Layout/Commands/Action"
        );
        _targetOption = GetNode<OptionButton>(
            "Panel/Margin/Layout/Commands/Target"
        );
        _confirmButton = GetNode<Button>(
            "Panel/Margin/Layout/Commands/Confirm"
        );
        _undoButton = GetNode<Button>(
            "Panel/Margin/Layout/Commands/Undo"
        );
        _fleeButton = GetNode<Button>(
            "Panel/Margin/Layout/Commands/Flee"
        );

        _actionOption.ItemSelected += OnActionSelected;
        _confirmButton.Pressed += ConfirmCommand;
        _undoButton.Pressed += BattleLogic.UndoCommand;
        _fleeButton.Pressed += BattleLogic.Flee;

        _battleBinding = ((BattleLogic)BattleLogic).Bind()
            .OnOutput((
                in BattleLogicState.Output.CommandRequested output
            ) => ShowCommand(output.ActorId))
            .OnOutput((
                in BattleLogicState.Output.CommandRejected output
            ) => _turnLabel.Text = output.Error)
            .OnOutput((
                in BattleLogicState.Output.CuePlaybackRequested output
            ) => PlayCues(output.Advance))
            .OnOutput((
                in BattleLogicState.Output.BattleCompleted output
            ) => FinishBattle(output.Result))
            .OnEnter<BattleLogicState.ResolvingTurn>(_ =>
                CallDeferred(nameof(AdvanceBattleLogic)));

        _gameBinding = GameLogic.Bind()
            .OnEnter<GameLogicState.Battle>(_ => StartRequestedBattle())
            .OnExit<GameLogicState.Battle>(_ => HideBattle());

        this.Provide();
        BattleLogic.Start<BattleLogicState.Disabled>();
        HideBattle();

    }

    public void AdvanceBattleLogic()
    {
        if (BattleLogic.State is BattleLogicState.ResolvingTurn)
        {
            BattleLogic.AdvanceResolution();
        }
    }

    public void OnExitTree()
    {
        _presenter?.Cancel();
        _battleBinding?.Dispose();
        _gameBinding?.Dispose();
        BattleLogic?.Dispose();
        BattleRepo?.Dispose();
    }

    private void StartRequestedBattle()
    {
        EnsureDebugParty();
        var request = GameRepo.CurrentBattleRequest;
        var encounter = _compiled.Encounters.TryGetValue(
            request.EncounterId,
            out var requested
        )
            ? requested
            : _compiled.Encounters.Values.FirstOrDefault()
                ?? throw new InvalidOperationException(
                    "Battle content has no encounters."
                );
        var setup = new BattleSetup(
            encounter.Id,
            request.Seed,
            request.ReturnMode,
            PartyRepo.Members
                .Select(BattleBattlerSeed.FromParty)
                .ToArray(),
            encounter.Enemies,
            encounter.Reward
        );

        Visible = true;
        MouseFilter = MouseFilterEnum.Stop;
        BattleLogic.StartBattle(setup);
        RefreshBattleState();
    }

    private void ShowCommand(BattlerId battlerId)
    {
        _currentBattlerId = battlerId;
        _actionIds.Clear();
        _actionOption.Clear();
        var battler = BattleRepo.Snapshot().Units.First(unit =>
            unit.Id == battlerId);
        foreach (var actionId in battler.ActionIds)
        {
            var action = BattleRepo.Catalog.GetAction(actionId);
            _actionIds.Add(actionId);
            _actionOption.AddItem($"{action.Name} ({action.TpCost} TP)");
        }

        _actionOption.Disabled = _actionIds.Count == 0;
        _confirmButton.Disabled = _actionIds.Count == 0;
        if (_actionIds.Count > 0)
        {
            _actionOption.Select(0);
            PopulateTargets(_actionIds[0]);
        }
        _turnLabel.Text = $"Turn {BattleRepo.Turn}: {battler.Name}";
        SetCommandControlsEnabled(true);
        RefreshBattleState();
    }

    private void OnActionSelected(long index)
    {
        if (index >= 0 && index < _actionIds.Count)
        {
            PopulateTargets(_actionIds[(int)index]);
        }
    }

    private void PopulateTargets(ActionId actionId)
    {
        _targetIds.Clear();
        _targetOption.Clear();
        if (_currentBattlerId is not { } actorId)
        {
            return;
        }
        var snapshot = BattleRepo.Snapshot();
        foreach (var id in BattleRepo.GetValidTargets(actorId, actionId))
        {
            var unit = snapshot.Units.First(candidate => candidate.Id == id);
            _targetIds.Add(id);
            _targetOption.AddItem(unit.Name);
        }
        _targetOption.Disabled = _targetIds.Count <= 1;
        _confirmButton.Disabled = _targetIds.Count == 0;
    }

    private void ConfirmCommand()
    {
        if (
            _currentBattlerId is not { } actorId
            || _actionOption.Selected < 0
            || _actionOption.Selected >= _actionIds.Count
        )
        {
            return;
        }
        var target = _targetOption.Selected >= 0
            && _targetOption.Selected < _targetIds.Count
            ? _targetIds[_targetOption.Selected]
            : (BattlerId?)null;
        BattleLogic.SubmitCommand(new BattleCommand(
            actorId,
            _actionIds[_actionOption.Selected],
            target
        ));
    }

    private void PlayCues(BattleAdvance advance)
    {
        SetCommandControlsEnabled(false);
        RefreshBattleState();
        _presenter.Play(
            advance,
            () => BattleLogic.AcknowledgeCuePlayback(advance.CueBatchId)
        );
    }

    private void FinishBattle(BattleResult result)
    {
        PartyRepo.ApplyBattleVitals(result.PlayerVitals);
        RefreshBattleState();
        HideBattle();

        var nextMode = result.Outcome == BattleOutcome.Defeat
            ? GameMode.MainMenu
            : result.ReturnMode;
        switch (nextMode)
        {
            case GameMode.MainMenu:
                GameLogic.Input(new GameLogicState.Input.EnterMainMenu());
                break;
            case GameMode.Town:
                GameLogic.Input(new GameLogicState.Input.EnterTown());
                break;
            case GameMode.Labyrinth:
                GameLogic.Input(new GameLogicState.Input.EnterLabyrinth());
                break;
            default:
                GameLogic.Input(new GameLogicState.Input.EnterLabyrinth());
                break;
        }
    }

    private void RefreshBattleState()
    {
        var snapshot = BattleRepo.Snapshot();
        _partyLabel.Text = FormatTeam(snapshot, BattleTeam.Player);
        _enemyLabel.Text = FormatTeam(snapshot, BattleTeam.Enemy);
    }

    private static string FormatTeam(
        BattleSnapshot snapshot,
        BattleTeam team
    ) => string.Join(
        "\n",
        snapshot.Units
            .Where(unit => unit.Team == team)
            .Select(unit =>
                $"{unit.Name}  HP {unit.Hp}/{unit.Stats.MaxHp}  "
                + $"TP {unit.Tp}/{unit.Stats.MaxTp}")
    );

    private void SetCommandControlsEnabled(bool enabled)
    {
        _actionOption.Disabled = !enabled || _actionIds.Count == 0;
        _targetOption.Disabled = !enabled || _targetIds.Count <= 1;
        _confirmButton.Disabled = !enabled || _targetIds.Count == 0;
        _undoButton.Disabled = !enabled;
        _fleeButton.Disabled = !enabled;
    }

    private void HideBattle()
    {
        _presenter?.Cancel();
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        _currentBattlerId = null;
    }

    private void EnsureDebugParty()
    {
        if (PartyRepo.Count > 0)
        {
            return;
        }

        AddDebugPartyMember(
            "bastion",
            "Bastion",
            new PartyPosition(PartyRow.Front, 0),
            BattleStats.Default with
            {
                MaxHp = 160,
                MaxTp = 20,
                Strength = 12,
                Agility = 7,
                Vitality = 16,
                Attack = 6,
                Defense = 12,
            },
            BattleContent.BasicAttackId
        );
        AddDebugPartyMember(
            "duelist",
            "Duelist",
            new PartyPosition(PartyRow.Front, 1),
            BattleStats.Default with
            {
                MaxHp = 110,
                MaxTp = 28,
                Strength = 16,
                Agility = 13,
                Luck = 12,
                Attack = 10,
                Defense = 4,
            },
            BattleContent.BasicAttackId,
            BattleContent.PoisonStrikeId
        );
        AddDebugPartyMember(
            "arcanist",
            "Arcanist",
            new PartyPosition(PartyRow.Back, 0),
            BattleStats.Default with
            {
                MaxHp = 80,
                MaxTp = 45,
                Technique = 17,
                Agility = 11,
                Wisdom = 14,
                Attack = 7,
                Defense = 2,
            },
            BattleContent.BasicAttackId,
            BattleContent.FireId
        );
        AddDebugPartyMember(
            "medic",
            "Medic",
            new PartyPosition(PartyRow.Back, 1),
            BattleStats.Default with
            {
                MaxHp = 95,
                MaxTp = 42,
                Technique = 14,
                Agility = 9,
                Wisdom = 16,
                Defense = 4,
            },
            BattleContent.BasicAttackId,
            BattleContent.HealId
        );
        AddDebugPartyMember(
            "ranger",
            "Ranger",
            new PartyPosition(PartyRow.Back, 2),
            BattleStats.Default with
            {
                MaxHp = 100,
                MaxTp = 32,
                Strength = 11,
                Technique = 15,
                Agility = 16,
                Luck = 14,
                Attack = 8,
                Defense = 3,
            },
            BattleContent.BasicAttackId,
            BattleContent.HealId,
            BattleContent.PoisonStrikeId
        );
    }

    private void AddDebugPartyMember(
        string id,
        string name,
        PartyPosition position,
        BattleStats stats,
        params ActionId[] preferredActions
    )
    {
        var learnedActions = preferredActions
            .Where(actionId =>
                _compiled.Catalog.TryGetAction(actionId, out _))
            .Distinct()
            .ToList();
        if (
            learnedActions.Count == 0
            && _compiled.Catalog.Actions.FirstOrDefault() is { } fallback
        )
        {
            learnedActions.Add(fallback.Id);
        }

        var member = new PartyMember
        {
            Id = new BattlerId(id),
            Name = name,
            BaseStats = stats,
            Hp = stats.MaxHp,
            Tp = stats.MaxTp,
        };
        member.LearnedActions.AddRange(learnedActions);
        PartyRepo.TryAdd(member, position);
    }

    private static CompiledBattleContent CreateFallbackContent()
    {
        var catalog = BattleContent.CreateDefaultCatalog();
        var enemies = new BattleBattlerSeed[]
        {
            new(
                new BattlerId("training_slime"),
                "Training Slime",
                BattleTeam.Enemy,
                new PartyPosition(PartyRow.Front, 0),
                BattleStats.Default with
                {
                    MaxHp = 90,
                    MaxTp = 0,
                    Agility = 6,
                    Attack = 5,
                    Defense = 4,
                },
                Hp: 90,
                Tp: 0,
                [BattleContent.BasicAttackId]
            ),
            new(
                new BattlerId("ember_slime"),
                "Ember Slime",
                BattleTeam.Enemy,
                new PartyPosition(PartyRow.Front, 1),
                BattleStats.Default with
                {
                    MaxHp = 70,
                    MaxTp = 20,
                    Technique = 13,
                    Agility = 9,
                    Attack = 5,
                    Defense = 3,
                },
                Hp: 70,
                Tp: 20,
                [BattleContent.BasicAttackId, BattleContent.FireId]
            ),
            new(
                new BattlerId("cave_moth"),
                "Cave Moth",
                BattleTeam.Enemy,
                new PartyPosition(PartyRow.Back, 0),
                BattleStats.Default with
                {
                    MaxHp = 55,
                    MaxTp = 18,
                    Technique = 14,
                    Agility = 15,
                    Luck = 12,
                    Attack = 4,
                    Defense = 2,
                },
                Hp: 55,
                Tp: 18,
                [BattleContent.BasicAttackId, BattleContent.PoisonStrikeId]
            ),
        };
        var encounter = new EncounterDefinition(
            new EncounterId("debug"),
            enemies,
            new BattleReward(Experience: 30, Currency: 15)
        );
        return new CompiledBattleContent(
            catalog,
            new Dictionary<EncounterId, EncounterDefinition>
            {
                [encounter.Id] = encounter,
            },
            new Dictionary<EquipmentId, EquipmentDefinition>()
        );
    }
}
