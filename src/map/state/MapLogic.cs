namespace Labyrinth;

using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;

public interface IMapLogic : ILogicBlock;

// [Meta, LogicBlock(typeof(State), Diagram = true)]
public partial class MapLogic : LogicBlock, IMapLogic
{

}
