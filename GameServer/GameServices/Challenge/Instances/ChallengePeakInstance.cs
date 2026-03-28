using HyacineCore.Server.Data;
using HyacineCore.Server.Data.Excel;
using HyacineCore.Server.Database.Friend;
using HyacineCore.Server.Enums.Mission;
using HyacineCore.Server.GameServer.Game.Battle;
using HyacineCore.Server.GameServer.Game.Challenge.Definitions;
using HyacineCore.Server.GameServer.Game.Player;
using HyacineCore.Server.GameServer.Game.Scene.Entity;
using HyacineCore.Server.GameServer.Server.Packet.Send.Challenge;
using HyacineCore.Server.GameServer.Server.Packet.Send.Lineup;
using HyacineCore.Server.Proto;
using HyacineCore.Server.Proto.ServerSide;
using HyacineCore.Server.Util;

namespace HyacineCore.Server.GameServer.Game.Challenge.Instances;

public class ChallengePeakInstance(PlayerInstance player, ChallengeDataPb data) : BaseChallengeInstance(player, data)
{
    #region Setter & Getter

    public override Dictionary<int, List<ChallengeConfigExcel.ChallengeMonsterInfo>> GetStageMonsters()
    {
        if (!Data.Peak.IsHard || Config.BossExcel == null) return Config.ChallengeMonsters;

        Dictionary<int, List<ChallengeConfigExcel.ChallengeMonsterInfo>> monsters = [];

        var mazeGroupId = Config.MazeGroupID;
        if (mazeGroupId <= 0)
            mazeGroupId = GameConstants.ResolveChallengePeakStartGroupId(
                (int)Data.Peak.CurrentPeakGroupId,
                Data.Peak.IsHard);

        if (mazeGroupId <= 0) return Config.ChallengeMonsters;
        monsters.Add(mazeGroupId, []);


        var curConfId = 200000;
        foreach (var eventId in Config.BossExcel.HardEventIDList)
        {
            if (eventId <= 0) continue;

            // get from stage id
            if (!GameData.StageConfigData.TryGetValue(eventId, out var stage)) continue;

            var monsterId = stage.MonsterList.LastOrDefault()?.Monster0 ?? 0;
            if (!GameData.MonsterConfigData.TryGetValue(monsterId, out var monsterConf)) continue;
            if (!GameData.MonsterTemplateConfigData.TryGetValue(monsterConf.MonsterTemplateID, out var template)) continue;

            var npcMonsterId = template.NPCMonsterList.Take(2).LastOrDefault(0);
            if (!GameData.NpcMonsterDataData.ContainsKey(npcMonsterId)) continue;

            monsters[mazeGroupId].Add(new ChallengeConfigExcel.ChallengeMonsterInfo(++curConfId, npcMonsterId,
                    eventId));
        }

        return monsters;
    }

    #endregion

    #region Properties

    public ChallengePeakConfigExcel Config { get; } =
        GameData.ChallengePeakConfigData[(int)data.Peak.CurrentPeakLevelId];

    public List<int> AllBattleTargets { get; } = [];
    public bool IsWin { get; private set; }

    #endregion

    //#region Serialization

    //#endregion

    #region Handlers

    public override void OnBattleStart(BattleInstance battle)
    {
        base.OnBattleStart(battle);

        foreach (var peakBuff in Data.Peak.Buffs)
            battle.Buffs.Add(new MazeBuff((int)peakBuff, 1, -1)
            {
                WaveFlag = -1
            });

        if (Data.Peak.IsHard && Config.BossExcel != null)
        {
            var excel = GameData.BattleTargetConfigData.GetValueOrDefault(Config.BossExcel.HardTarget);
            if (excel != null)
            {
                battle.AddBattleTarget(5, excel.ID, 0, excel.TargetParam);
                AllBattleTargets.Add(excel.ID);
            }
        }
        else
        {
            foreach (var targetId in Config.NormalTargetList)
            {
                var excel = GameData.BattleTargetConfigData.GetValueOrDefault(targetId);
                if (excel != null)
                {
                    battle.AddBattleTarget(5, excel.ID, 0, excel.TargetParam);
                    AllBattleTargets.Add(excel.ID);
                }
            }
        }
    }

    public override async ValueTask OnBattleEnd(BattleInstance battle, PVEBattleResultCsReq req)
    {
        switch (req.EndStatus)
        {
            case BattleEndStatus.BattleEndWin:
                // Get monster count in stage
                long monsters = Player.SceneInstance!.Entities.Values.OfType<EntityMonster>().Count();

                if (monsters == 0)
                {
                    Data.Peak.CurStatus = (int)ChallengeStatus.ChallengeFinish;
                    var res = CalculateStars(req);
                    Data.Peak.Stars = res.Item1;
                    Data.Peak.RoundCnt = req.Stt.RoundCnt;
                    IsWin = true;

                    await Player.ChallengePeakManager!.SaveHistory(this, res.Item2);

                    await Player.SendPacket(new PacketChallengePeakSettleScNotify(this, res.Item2));

                    // Call MissionManager
                    await Player.MissionManager!.HandleFinishType(MissionFinishTypeEnum.ChallengePeakBattleFinish,
                        this);

                    // add development
                    Player.FriendRecordData!.AddAndRemoveOld(new FriendDevelopmentInfoPb
                    {
                        DevelopmentType = DevelopmentType.DevelopmentChallengePeak,
                        Params = { { "PeakLevelId", (uint)Config.ID } }
                    });
                }

                // Set saved technique points (This will be restored if the player resets the challenge)
                Data.Peak.SavedMp = (uint)Player.LineupManager!.GetCurLineup()!.Mp;
                break;
            case BattleEndStatus.BattleEndQuit:
                // Reset technique points and move back to start position
                var lineup = Player.LineupManager!.GetCurLineup()!;
                lineup.Mp = (int)Data.Peak.SavedMp;
                if (Data.Peak.StartPos != null && Data.Peak.StartRot != null)
                    await Player.MoveTo(Data.Peak.StartPos.ToPosition(), Data.Peak.StartRot.ToPosition());
                await Player.SendPacket(new PacketSyncLineupNotify(lineup));
                break;
            default:
                // Determine challenge result
                // Fail challenge
                IsWin = false;
                Data.Peak.CurStatus = (int)ChallengeStatus.ChallengeFailed;
                Data.Peak.Stars = 0;
                Data.Peak.RoundCnt = req.Stt.RoundCnt;

                // Send challenge result data
                await Player.SendPacket(new PacketChallengePeakSettleScNotify(this, []));

                break;
        }
    }

    public (uint, List<uint>) CalculateStars(PVEBattleResultCsReq req)
    {
        var targets = AllBattleTargets;
        var stars = 0u;

        List<uint> finishedIds = [];
        foreach (var targetId in targets)
        {
            var target = req.Stt.BattleTargetInfo[5].BattleTargetList_.FirstOrDefault(x => x.Id == targetId);
            if (target == null) continue;
            var excel = GameData.BattleTargetConfigData.GetValueOrDefault(targetId);
            if (excel == null) continue;

            if (target.Progress <= excel.TargetParam)
            {
                stars += 1u;
                finishedIds.Add((uint)targetId);
            }
        }

        if (Data.Peak.IsHard && Config.BossExcel != null) stars = 3;

        return (Math.Min(stars, 3), finishedIds);
    }

    #endregion
}
