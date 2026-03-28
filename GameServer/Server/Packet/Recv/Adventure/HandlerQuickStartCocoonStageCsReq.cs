using HyacineCore.Server.GameServer.Server.Packet.Send.Adventure;
using HyacineCore.Server.Kcp;
using HyacineCore.Server.Proto;

namespace HyacineCore.Server.GameServer.Server.Packet.Recv.Adventure;

[Opcode(CmdIds.QuickStartCocoonStageCsReq)]
public class HandlerQuickStartCocoonStageCsReq : Handler
{
    public override async Task OnHandle(Connection connection, byte[] header, byte[] data)
    {
        var req = QuickStartCocoonStageCsReq.Parser.ParseFrom(data);
        // Different proto sets may use either Wave or IHIAFPLIPEK for challenge times.
        var wave = (int)(req.Wave > 0 ? req.Wave : 1);
        if (wave <= 0) wave = 1;

        var battle =
            await connection.Player!.BattleManager!.StartCocoonStage((int)req.CocoonId, wave,
                (int)req.WorldLevel);

        if (battle != null)
        {
            connection.Player!.SceneInstance?.OnEnterStage();
            await connection.SendPacket(new PacketQuickStartCocoonStageScRsp(battle, (int)req.CocoonId, wave));
        }
        else
            await connection.SendPacket(new PacketQuickStartCocoonStageScRsp());
    }
}
