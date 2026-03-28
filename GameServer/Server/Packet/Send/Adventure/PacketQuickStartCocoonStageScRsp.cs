using HyacineCore.Server.GameServer.Game.Battle;
using HyacineCore.Server.Kcp;
using HyacineCore.Server.Proto;

namespace HyacineCore.Server.GameServer.Server.Packet.Send.Adventure;

public class PacketQuickStartCocoonStageScRsp : BasePacket
{
    public PacketQuickStartCocoonStageScRsp() : base(CmdIds.QuickStartCocoonStageScRsp)
    {
        var rsp = new QuickStartCocoonStageScRsp
        {
            Retcode = 1
        };

        SetData(rsp);
    }

    public PacketQuickStartCocoonStageScRsp(BattleInstance battle, int cocoonId, int wave) : base(
        CmdIds.QuickStartCocoonStageScRsp)
    {
        var rsp = new QuickStartCocoonStageScRsp
        {
            CocoonId = (uint)cocoonId,
            Wave = (uint)wave,
            CocoonChallengeTimes = (uint)wave,
            BattleInfo = battle.ToProto()
        };

        SetData(rsp);
    }
}
