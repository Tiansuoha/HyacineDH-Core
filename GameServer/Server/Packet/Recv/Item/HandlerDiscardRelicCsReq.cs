using HyacineCore.Server.Enums.Item;
using HyacineCore.Server.GameServer.Server.Packet.Send.Item;
using HyacineCore.Server.Kcp;
using HyacineCore.Server.Proto;

namespace HyacineCore.Server.GameServer.Server.Packet.Recv.Item;

[Opcode(CmdIds.DiscardRelicCsReq)]
public class HandlerDiscardRelicCsReq : Handler
{
    public override async Task OnHandle(Connection connection, byte[] header, byte[] data)
    {
        var req = DiscardRelicCsReq.Parser.ParseFrom(data);
        var result =
            await connection.Player!.InventoryManager!.DiscardItems(req.RelicIds, req.Discarded,
                ItemMainTypeEnum.Relic);
        await connection.SendPacket(new PacketDiscardRelicScRsp(result, req.Discarded));
    }
}
