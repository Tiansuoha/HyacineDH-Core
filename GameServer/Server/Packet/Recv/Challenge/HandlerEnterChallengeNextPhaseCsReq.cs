using HyacineCore.Server.GameServer.Game.Challenge.Instances;
using HyacineCore.Server.GameServer.Server.Packet.Send.Challenge;
using HyacineCore.Server.Kcp;
using HyacineCore.Server.Proto;

namespace HyacineCore.Server.GameServer.Server.Packet.Recv.Challenge;

[Opcode(CmdIds.EnterChallengeNextPhaseCsReq)]
public class HandlerEnterChallengeNextPhaseCsReq : Handler
{
    public override async Task OnHandle(Connection connection, byte[] header, byte[] data)
    {
        if (connection.Player!.ChallengeManager?.ChallengeInstance is not ChallengeBossInstance boss)
        {
            await connection.SendPacket(new PacketEnterChallengeNextPhaseScRsp(Retcode.RetChallengeNotDoing));
            return;
        }

        await boss.NextPhase();
        await connection.SendPacket(new PacketEnterChallengeNextPhaseScRsp(connection.Player));
    }
}