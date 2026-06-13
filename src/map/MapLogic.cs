namespace Labyrinth;

using System;
using System.Collections.Generic;
using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;
using Chickensoft.LogicBlocks.Auto;

public interface IMapLogic : ILogicBlock
{
    bool PlayerIsRegistered { get; }
    bool TryRegisterEntity(
        MapEntityId id,
        MapEntityPose pose
    );
    bool TryUnregisterEntity(MapEntityId id);
    bool TryGetEntityPose(MapEntityId id, out MapEntityPose pose);
}

[Meta]
public partial class MapLogic : AutoBlock, IMapLogic
{
    public MapLogic()
    {
        Preallocate<MapLogicState>();
        Set(new Data());
    }

    public bool PlayerIsRegistered => Get<IMapRepo>().PlayerIsRegistered;

    public override IEnumerable<IDisposable> OnStartSubscriptions()
    {
        yield return Get<IMapRepo>().AutoChannel.Bind()
            .On((in IMapRepo.MapEntityWasRegistered message) =>
                Input(new MapLogicState.Input.EntityRegistered(
                    message.Id,
                    message.Pose
                )))
            .On((in IMapRepo.MapEntityWasUnregistered message) =>
                Input(new MapLogicState.Input.EntityUnregistered(message.Id)));
    }

    public bool TryRegisterEntity(MapEntityId id, MapEntityPose pose)
    {
        var data = Get<Data>();
        data.LastCommandSucceeded = false;
        Input(new MapLogicState.Input.RegisterEntity(id, pose));
        return data.LastCommandSucceeded;
    }

    public bool TryUnregisterEntity(MapEntityId id)
    {
        var data = Get<Data>();
        data.LastCommandSucceeded = false;
        Input(new MapLogicState.Input.UnregisterEntity(id));
        return data.LastCommandSucceeded;
    }

    public bool TryGetEntityPose(MapEntityId id, out MapEntityPose pose) =>
        Get<IMapRepo>().TryGetEntityPose(id, out pose);

    public sealed class Data
    {
        public bool LastCommandSucceeded { get; set; }
    }
}
