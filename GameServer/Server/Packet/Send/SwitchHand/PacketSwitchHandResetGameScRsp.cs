using HyacineCore.Server.Database.Scene;
using HyacineCore.Server.Kcp;
using HyacineCore.Server.Proto;

namespace HyacineCore.Server.GameServer.Server.Packet.Send.SwitchHand;

public class PacketSwitchHandResetGameScRsp : BasePacket
{
    public PacketSwitchHandResetGameScRsp(SwitchHandInfo info) : base(CmdIds.SwitchHandResetGameScRsp)
    {
        var proto = new SwitchHandResetGameScRsp
        {
            TargetHandInfo = info.ToProto()
        };

        SetData(proto);
    }

    public PacketSwitchHandResetGameScRsp(Retcode ret) : base(CmdIds.SwitchHandResetGameScRsp)
    {
        var proto = new SwitchHandResetGameScRsp
        {
            Retcode = (uint)ret
        };

        SetData(proto);
    }
}
