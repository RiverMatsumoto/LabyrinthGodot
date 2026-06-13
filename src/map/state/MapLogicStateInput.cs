namespace Labyrinth;

public partial record MapLogicState
{
    public static class Input
    {
        public readonly record struct RegisterEntity(
            MapEntityId Id,
            MapEntityPose Pose
        );
        public readonly record struct UnregisterEntity(MapEntityId Id);
        public readonly record struct EntityRegistered(
            MapEntityId Id,
            MapEntityPose Pose
        );
        public readonly record struct EntityUnregistered(MapEntityId Id);
    }
}
