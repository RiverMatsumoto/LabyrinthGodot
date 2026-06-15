namespace Labyrinth;

using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;
using Chickensoft.LogicBlocks.Auto;

/// <summary>
/// State-machine facade for battle interactions. Inputs are accepted by the
/// current <see cref="BattleLogicState"/> and observable outputs coordinate the
/// scene without owning domain state.
/// </summary>
public interface IBattleLogic : ILogicBlock
{
    void StartRequestedBattle();
    void SubmitCommand(BattleCommand command);
    void UndoCommand();

    /// <summary>
    /// Requests resolution until the repository reaches the next command,
    /// cue-playback, or completion boundary.
    /// </summary>
    void AdvanceResolution();

    /// <summary>
    /// Confirms that the currently requested cue batch finished playing.
    /// </summary>
    /// <param name="cueBatchId">
    /// The exact batch ID supplied by the corresponding
    /// <see cref="BattleAdvance"/>.
    /// </param>
    void AcknowledgeCuePlayback(long cueBatchId);
    void Flee();
}

/// <summary>
/// LogicBlocks implementation of <see cref="IBattleLogic"/>.
/// </summary>
[Meta]
public partial class BattleLogic : AutoBlock, IBattleLogic
{
    public BattleLogic()
    {
        Preallocate<BattleLogicState>();
    }

    public void StartRequestedBattle() =>
        Input(new BattleLogicState.Input.StartRequestedBattle());

    public void SubmitCommand(BattleCommand command) =>
        Input(new BattleLogicState.Input.SubmitCommand(command));

    public void UndoCommand() =>
        Input(new BattleLogicState.Input.UndoCommand());

    public void AdvanceResolution() =>
        Input(new BattleLogicState.Input.AdvanceResolution());

    public void AcknowledgeCuePlayback(long cueBatchId) =>
        Input(new BattleLogicState.Input.CuePlaybackFinished(
            cueBatchId
        ));

    public void Flee() =>
        Input(new BattleLogicState.Input.Flee());
}
