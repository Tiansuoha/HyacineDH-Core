using HyacineCore.Server.Database.Scene;
using HyacineCore.Server.Kcp;
using HyacineCore.Server.Proto;

namespace HyacineCore.Server.GameServer.Server.Packet.Send.SwitchHand;

public class PacketSwitchHandResetHandPosScRsp : BasePacket
{
    public PacketSwitchHandResetHandPosScRsp(SwitchHandInfo info) : base(CmdIds.SwitchHandResetHandPosScRsp)
    {
        var proto = new SwitchHandResetHandPosScRsp
        {
            TargetHandInfo = info.ToProto()
        };

        SetData(proto);
    }

    public PacketSwitchHandResetHandPosScRsp(Retcode ret) : base(CmdIds.SwitchHandResetHandPosScRsp)
    {
        var proto = new SwitchHandResetHandPosScRsp
        {
            Retcode = (uint)ret
        };

        SetData(proto);
    }
}
