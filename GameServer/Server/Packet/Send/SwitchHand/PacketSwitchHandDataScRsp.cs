using HyacineCore.Server.Database.Scene;
using HyacineCore.Server.Kcp;
using HyacineCore.Server.Proto;

namespace HyacineCore.Server.GameServer.Server.Packet.Send.SwitchHand;

public class PacketSwitchHandDataScRsp : BasePacket
{
    public PacketSwitchHandDataScRsp(SwitchHandInfo info) : base(CmdIds.SwitchHandDataScRsp)
    {
        var proto = new SwitchHandDataScRsp
        {
            TargetHandInfo = { info.ToProto() }
        };

        SetData(proto);
    }

    public PacketSwitchHandDataScRsp(List<SwitchHandInfo> infos) : base(CmdIds.SwitchHandDataScRsp)
    {
        var proto = new SwitchHandDataScRsp
        {
            TargetHandInfo = { infos.Select(x => x.ToProto()) }
        };

        SetData(proto);
    }

    public PacketSwitchHandDataScRsp(Retcode code) : base(CmdIds.SwitchHandDataScRsp)
    {
        var proto = new SwitchHandDataScRsp
        {
            Retcode = (uint)code
        };

        SetData(proto);
    }
}
