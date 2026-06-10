namespace Labyrinth;

public partial class MapMovementLogic
{
    public sealed class Data
    {
        public MapEntityId EntityId { get; set; }
        public double MoveDuration { get; set; }
        public double MoveCooldown { get; set; }
    }
}
