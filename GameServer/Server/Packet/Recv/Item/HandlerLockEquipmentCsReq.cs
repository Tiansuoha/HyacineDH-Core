using HyacineCore.Server.Enums.Item;
using HyacineCore.Server.GameServer.Server.Packet.Send.Item;
using HyacineCore.Server.Kcp;
using HyacineCore.Server.Proto;

namespace HyacineCore.Server.GameServer.Server.Packet.Recv.Item;

[Opcode(CmdIds.LockEquipmentCsReq)]
public class HandlerLockEquipmentCsReq : Handler
{
    public override async Task OnHandle(Connection connection, byte[] header, byte[] data)
    {
        var req = LockEquipmentCsReq.Parser.ParseFrom(data);
        var result =
            await connection.Player!.InventoryManager!.LockItems(req.EquipmentIdList, req.IsLocked,
                ItemMainTypeEnum.Equipment);
        await connection.SendPacket(new PacketLockEquipmentScRsp(result));
    }
}
