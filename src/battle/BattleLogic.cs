namespace Labyrinth;

using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;
using Chickensoft.LogicBlocks.Auto;

public interface IBattleLogic : ILogicBlock
{
    void StartBattle(BattleSetup setup);
    void SubmitIntent(BattleIntent intent);
    void UndoIntent();
    void AdvanceResolution();
    void AcknowledgePresentation(long presentationId);
    void Flee();
}

[Meta]
public partial class BattleLogic : AutoBlock, IBattleLogic
{
    public BattleLogic()
    {
        Preallocate<BattleLogicState>();
    }

    public void StartBattle(BattleSetup setup) =>
        Input(new BattleLogicState.Input.StartBattle(setup));

    public void SubmitIntent(BattleIntent intent) =>
        Input(new BattleLogicState.Input.SubmitIntent(intent));

    public void UndoIntent() =>
        Input(new BattleLogicState.Input.UndoIntent());

    public void AdvanceResolution() =>
        Input(new BattleLogicState.Input.AdvanceResolution());

    public void AcknowledgePresentation(long presentationId) =>
        Input(new BattleLogicState.Input.PresentationFinished(
            presentationId
        ));

    public void Flee() =>
        Input(new BattleLogicState.Input.Flee());
}
