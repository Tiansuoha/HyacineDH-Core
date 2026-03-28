using HyacineCore.Server.Kcp;
using HyacineCore.Server.Proto;

namespace HyacineCore.Server.GameServer.Server.Packet.Send.Avatar;

public class PacketSetAvatarEnhancedIdScRsp : BasePacket
{
    public PacketSetAvatarEnhancedIdScRsp(Retcode retcode) : base(CmdIds.SetAvatarEnhancedIdScRsp)
    {
        var proto = new SetAvatarEnhancedIdScRsp
        {
            Retcode = (uint)retcode
        };

        SetData(proto);
    }

    public PacketSetAvatarEnhancedIdScRsp(uint avatarId, int enhanceId) : base(CmdIds.SetAvatarEnhancedIdScRsp)
    {
        var proto = new SetAvatarEnhancedIdScRsp
        {
            CurEnhanceId = (uint)enhanceId,
            SetTargetAvatarId = avatarId
        };

        SetData(proto);
    }
}
