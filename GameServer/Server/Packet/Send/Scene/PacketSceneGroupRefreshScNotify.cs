using HyacineCore.Server.GameServer.Game.Mission;
using HyacineCore.Server.GameServer.Game.Player;
using HyacineCore.Server.GameServer.Game.Scene;
using HyacineCore.Server.GameServer.Game.Scene.Entity;
using HyacineCore.Server.Kcp;
using HyacineCore.Server.Proto;

namespace HyacineCore.Server.GameServer.Server.Packet.Send.Scene;

public class PacketSceneGroupRefreshScNotify : BasePacket
{
    public PacketSceneGroupRefreshScNotify(PlayerInstance player, List<BaseGameEntity>? addEntity = null,
        List<BaseGameEntity>? removeEntity = null)
        : base(CmdIds.SceneGroupRefreshScNotify)
    {
        var proto = new SceneGroupRefreshScNotify
        {
            FloorId = (uint)player.Data.FloorId,
            DimensionId = (uint)((player.SceneInstance!.EntityLoader as StoryLineEntityLoader)?.DimensionId ?? 0)
        };
        Dictionary<int, GroupRefreshInfo> refreshInfo = [];

        foreach (var e in removeEntity ?? [])
        {
            var group = new GroupRefreshInfo
            {
                GroupId = (uint)e.GroupId,
                RefreshType = SceneGroupRefreshType.Loaded
            };
            group.RefreshEntity.Add(new SceneEntityRefreshInfo
            {
                DeleteEntity = (uint)e.EntityId
            });

            if (refreshInfo.TryGetValue(e.GroupId, out var value))
                value.RefreshEntity.AddRange(group.RefreshEntity);
            else
                refreshInfo[e.GroupId] = group;
        }

        foreach (var e in addEntity ?? [])
        {
            var group = new GroupRefreshInfo
            {
                GroupId = (uint)e.GroupId,
                RefreshType = SceneGroupRefreshType.Loaded
            };
            group.RefreshEntity.Add(new SceneEntityRefreshInfo
            {
                AddEntity = e.ToProto()
            });

            if (refreshInfo.TryGetValue(e.GroupId, out var value))
                value.RefreshEntity.AddRange(group.RefreshEntity);
            else
                refreshInfo[e.GroupId] = group;
        }

        proto.GroupRefreshList.AddRange(refreshInfo.Values);

        SetData(proto);
    }

    public PacketSceneGroupRefreshScNotify(PlayerInstance player, BaseGameEntity? addEntity = null,
        BaseGameEntity? removeEntity = null) :
        this(player, addEntity == null ? [] : [addEntity], removeEntity == null ? [] : [removeEntity])
    {
    }

    public PacketSceneGroupRefreshScNotify(SceneInstance scene, List<GroupPropertyRefreshData> refreshDataList) : base(
        CmdIds.SceneGroupRefreshScNotify)
    {
        var proto = new SceneGroupRefreshScNotify
        {
            FloorId = (uint)scene.FloorId,
            DimensionId = (uint)((scene.EntityLoader as StoryLineEntityLoader)?.DimensionId ?? 0)
        };

        Dictionary<int, List<GroupPropertyRefreshData>> refreshDataDict = [];
        foreach (var data in refreshDataList)
        {
            if (!refreshDataDict.TryGetValue(data.GroupId, out var list))
            {
                list = [];
                refreshDataDict[data.GroupId] = list;
            }

            list.Add(data);
        }

        foreach (var (groupId, dataList) in refreshDataDict)
        {
            var group = new GroupRefreshInfo
            {
                GroupId = (uint)groupId,
                RefreshType = SceneGroupRefreshType.Loaded
            };

            foreach (var data in dataList)
                group.MPDEGPAMIEI.Add(new MDBADLOEJMP
                {
                    JIPGMBMBDFK = data.NewValue,
                    BNINLHOKICO = data.OldValue,
                    GCJKIDIBJHJ = data.PropertyName
                });

            proto.GroupRefreshList.Add(group);
        }

        SetData(proto);
    }
}

public record GroupPropertyRefreshData(int GroupId, string PropertyName, int OldValue, int NewValue);
