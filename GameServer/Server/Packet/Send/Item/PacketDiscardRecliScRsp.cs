using HyacineCore.Server.Kcp;
using HyacineCore.Server.Proto;

namespace HyacineCore.Server.GameServer.Server.Packet.Send.Item;

public class PacketDiscardRelicScRsp : BasePacket
{
    public PacketDiscardRelicScRsp(bool success, bool isDiscard) : base(CmdIds.DiscardRelicScRsp)
    {
        DiscardRelicScRsp proto = new();

        if (success) proto.Discarded = isDiscard;
        else proto.Retcode = (uint)Retcode.RetFail;

        SetData(proto);
    }
}
