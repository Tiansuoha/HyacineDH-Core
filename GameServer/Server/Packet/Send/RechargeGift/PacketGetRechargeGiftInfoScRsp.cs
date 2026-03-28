using HyacineCore.Server.Data;
using HyacineCore.Server.Kcp;
using HyacineCore.Server.Proto;

namespace HyacineCore.Server.GameServer.Server.Packet.Send.RechargeGift;

public class PacketGetRechargeGiftInfoScRsp : BasePacket
{
    public PacketGetRechargeGiftInfoScRsp() : base(CmdIds.GetRechargeGiftInfoScRsp)
    {
        var proto = new GetRechargeGiftInfoScRsp
        {
            JHAJHMJBMPE =
            {
                GameData.RechargeGiftConfigData.Values.Select(x => new PDDCEJIPAHG
                {
                    GiftType = (uint)x.GiftType,
                    BeginTime = 0,
                    EndTime = long.MaxValue,
                    KHLMFEEHELN =
                    {
                        x.GiftIDList.Select(h => new BAFNEIDCECF
                        {
                            Status = ICINEONCGFO.Mffndnhcgdo,
                            Index = (uint)x.GiftIDList.IndexOf(h)
                        })
                    }
                })
            }
        };

        SetData(proto);
    }
}
