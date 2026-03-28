using HyacineCore.Server.GameServer.Server.Packet.Send.Item;
using HyacineCore.Server.Kcp;
using HyacineCore.Server.Proto;

namespace HyacineCore.Server.GameServer.Server.Packet.Recv.Item;

[Opcode(CmdIds.SellItemCsReq)]
public class HandlerSellItemCsReq : Handler
{
    public override async Task OnHandle(Connection connection, byte[] header, byte[] data)
    {
        var req = SellItemCsReq.Parser.ParseFrom(data);
        var items = await connection.Player!.InventoryManager!.SellItem(req.CostData, req.ToMaterial);
        await connection.SendPacket(new PacketSellItemScRsp(items));
    }
}
