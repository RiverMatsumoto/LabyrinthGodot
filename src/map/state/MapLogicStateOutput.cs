namespace Labyrinth;

public partial record MapLogicState
{
    public static class Output
    {
        public readonly record struct SpawnPlayer(
            MapEntityId Id,
            MapEntityPose Pose
        );
        public readonly record struct SpawnEnemy(
            MapEntityId Id,
            MapEntityPose Pose
        );
        public readonly record struct DespawnEntity(MapEntityId Id);
    }
}
