using HyacineCore.Server.Kcp;
using HyacineCore.Server.Proto;

namespace HyacineCore.Server.GameServer.Server.Packet.Send.ChallengePeak;

public class PacketChallengePeakGroupDataUpdateScNotify : BasePacket
{
    public PacketChallengePeakGroupDataUpdateScNotify(ChallengePeakGroup group) : base(
        CmdIds.ChallengePeakGroupDataUpdateScNotify)
    {
        var proto = new ChallengePeakGroupDataUpdateScNotify
        {
            ChallengePeakGroupId = group.PeakGroupId
        };

        SetData(proto);
    }
}
