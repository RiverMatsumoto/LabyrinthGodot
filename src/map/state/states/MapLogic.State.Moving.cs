namespace Labyrinth;

using Chickensoft.Introspection;

public partial class MapLogic
{
    public partial record State
    {
        [Meta]
        public partial record Moving : State
        {

        }
    }
}
