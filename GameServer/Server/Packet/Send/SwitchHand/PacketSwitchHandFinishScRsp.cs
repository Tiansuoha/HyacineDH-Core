using HyacineCore.Server.Database.Scene;
using HyacineCore.Server.Kcp;
using HyacineCore.Server.Proto;

namespace HyacineCore.Server.GameServer.Server.Packet.Send.SwitchHand;

public class PacketSwitchHandFinishScRsp : BasePacket
{
    public PacketSwitchHandFinishScRsp(SwitchHandInfo info) : base(CmdIds.SwitchHandFinishScRsp)
    {
        var proto = new SwitchHandFinishScRsp
        {
            HandInfo = info.ToProto()
        };

        SetData(proto);
    }

    public PacketSwitchHandFinishScRsp(Retcode ret) : base(CmdIds.SwitchHandFinishScRsp)
    {
        var proto = new SwitchHandFinishScRsp
        {
            Retcode = (uint)ret
        };

        SetData(proto);
    }
}
