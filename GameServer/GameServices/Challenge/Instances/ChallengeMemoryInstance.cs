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

public class ChallengeMemoryInstance(PlayerInstance player, ChallengeDataPb data)
    : BaseLegacyChallengeInstance(player, data)
{
    #region Properties

    public override ChallengeConfigExcel Config { get; } =
        GameData.ChallengeConfigData[(int)data.Memory.ChallengeMazeId];

    #endregion

    #region Serialization

    public override CurChallenge ToProto()
    {
        return new CurChallenge
        {
            ChallengeId = Data.Memory.ChallengeMazeId,
            DeadAvatarNum = Data.Memory.DeadAvatarNum,
            ExtraLineupType = (ExtraLineupType)Data.Memory.CurrentExtraLineup,
            Status = (ChallengeStatus)Data.Memory.CurStatus,
            StageInfo = new ChallengeCurBuffInfo(),
            RoundCount = (uint)(Config.ChallengeCountDown - Data.Memory.RoundsLeft)
        };
    }

    #endregion

    #region Getter & Setter

    public void SetCurrentExtraLineup(ExtraLineupType type)
    {
        Data.Memory.CurrentExtraLineup = (ChallengeLineupTypePb)type;
    }

    public override Dictionary<int, List<ChallengeConfigExcel.ChallengeMonsterInfo>> GetStageMonsters()
    {
        return Data.Memory.CurrentStage == 1 ? Config.ChallengeMonsters1 : Config.ChallengeMonsters2;
    }

    public override uint GetStars()
    {
        return Data.Memory.Stars;
    }

    public override int GetCurrentExtraLineupType()
    {
        return (int)Data.Memory.CurrentExtraLineup;
    }

    public override void SetStartPos(Position pos)
    {
        Data.Memory.StartPos = pos.ToVector();
    }

    public override void SetStartRot(Position rot)
    {
        Data.Memory.StartRot = rot.ToVector();
    }

    public override void SetSavedMp(int mp)
    {
        Data.Memory.SavedMp = (uint)mp;
    }

    #endregion

    #region Handlers

    public override void OnBattleStart(BattleInstance battle)
    {
        base.OnBattleStart(battle);

        battle.RoundLimit = (int)Data.Memory.RoundsLeft;

        battle.Buffs.Add(new MazeBuff(Config.MazeBuffID, 1, -1)
        {
            WaveFlag = -1
        });
    }

    public override async ValueTask OnBattleEnd(BattleInstance battle, PVEBattleResultCsReq req)
    {
        switch (req.EndStatus)
        {
            case BattleEndStatus.BattleEndWin:
                // Check if any avatar in the lineup has died
                foreach (var avatar in battle.Lineup.AvatarData!.FormalAvatars)
                    if (avatar.CurrentHp <= 0)
                        Data.Memory.DeadAvatarNum++;

                // Get monster count in stage
                long monsters = Player.SceneInstance!.Entities.Values.OfType<EntityMonster>().Count();

                if (monsters == 0) await AdvanceStage();

                // Calculate rounds left
                Data.Memory.RoundsLeft = Math.Min(Math.Max(Data.Memory.RoundsLeft - req.Stt.RoundCnt, 1),
                    Data.Memory.RoundsLeft);

                // Set saved technique points (This will be restored if the player resets the challenge)
                Data.Memory.SavedMp = (uint)Player.LineupManager!.GetCurLineup()!.Mp;
                break;
            case BattleEndStatus.BattleEndQuit:
                // Reset technique points and move back to start position
                var lineup = Player.LineupManager!.GetCurLineup()!;
                lineup.Mp = (int)Data.Memory.SavedMp;
                await Player.MoveTo(Data.Memory.StartPos.ToPosition(), Data.Memory.StartRot.ToPosition());
                await Player.SendPacket(new PacketSyncLineupNotify(lineup));
                break;
            default:
                // Determine challenge result
                // Fail challenge
                Data.Memory.CurStatus = (int)ChallengeStatus.ChallengeFailed;

                // Send challenge result data
                await Player.SendPacket(new PacketChallengeSettleNotify(this));

                break;
        }
    }

    public uint CalculateStars()
    {
        var targets = Config.ChallengeTargetID!;
        var stars = 0u;

        for (var i = 0; i < targets.Count; i++)
        {
            if (!GameData.ChallengeTargetData.ContainsKey(targets[i])) continue;

            var target = GameData.ChallengeTargetData[targets[i]];

            switch (target.ChallengeTargetType)
            {
                case ChallengeTargetExcel.ChallengeType.ROUNDS_LEFT:
                    if (Data.Memory.RoundsLeft >= target.ChallengeTargetParam1) stars += 1u << i;
                    break;
                case ChallengeTargetExcel.ChallengeType.DEAD_AVATAR:
                    if (Data.Memory.DeadAvatarNum == 0) stars += 1u << i;
                    break;
            }
        }

        return Math.Min(stars, 7);
    }

    private async ValueTask AdvanceStage()
    {
        if (Data.Memory.CurrentStage >= Config.StageNum)
        {
            // 1. 计算最终消耗轮�?
        uint consumedRounds = (uint)(Config.ChallengeCountDown - Data.Memory.RoundsLeft);
        
        Data.Memory.CurStatus = (int)ChallengeStatus.ChallengeFinish;
        Data.Memory.Stars = CalculateStars();

        // 2. 修正：将消耗轮数存入历史记�?(不要�?)
        Player.ChallengeManager!.AddHistory((int)Data.Memory.ChallengeMazeId, (int)Data.Memory.Stars, (int)consumedRounds);

        // 3. 构造战报并保存 (确保内部处理�?RoundCount)
        Player.ChallengeManager.SaveBattleRecord(this);

            // Send challenge result data
            await Player.SendPacket(new PacketChallengeSettleNotify(this));

            // Call MissionManager
            await Player.MissionManager!.HandleFinishType(MissionFinishTypeEnum.ChallengeFinish, this);

            // save
            Player.ChallengeManager.SaveBattleRecord(this);

            // add development
            Player.FriendRecordData!.AddAndRemoveOld(new FriendDevelopmentInfoPb
            {
                DevelopmentType = DevelopmentType.DevelopmentMemoryChallenge,
                Params = { { "ChallengeId", (uint)Config.ID } }
            });
        }
        else
        {
            // MOC behavior: silently switch to stage 2.
            await NextPhase();
        }
    }

    public async ValueTask<bool> NextPhase()
    {
        if (Config.StageNum < 2) return false;

        if (Data.Memory.CurrentStage < 2)
            Data.Memory.CurrentStage++;

        SetCurrentExtraLineup(ExtraLineupType.LineupChallenge2);
        await Player.LineupManager!.SetExtraLineup((ExtraLineupType)GetCurrentExtraLineupType(), notify: false);
        await Player.SendPacket(new PacketChallengeLineupNotify((ExtraLineupType)Data.Memory.CurrentExtraLineup));
        await Player.SceneInstance!.SyncLineup();

        Data.Memory.SavedMp = (uint)Player.LineupManager.GetCurLineup()!.Mp;

        var stage2EntryId = Config.MapEntranceID2 != 0 ? Config.MapEntranceID2 : Config.MapEntranceID;
        var sameEntry = stage2EntryId == Player.Data.EntryId;

        if (!sameEntry && stage2EntryId != 0)
        {
            await Player.EnterScene(stage2EntryId, 0, false);
            Data.Memory.StartPos = Player.Data.Pos!.ToVector();
            Data.Memory.StartRot = Player.Data.Rot!.ToVector();
        }
        else if (Config.MapEntranceID2 == 0)
        {
            await Player.MoveTo(Data.Memory.StartPos.ToPosition(), Data.Memory.StartRot.ToPosition());
        }

        var needClientRefresh = sameEntry;
        if (Config.MazeGroupID1 != 0 && Config.MazeGroupID1 != Config.MazeGroupID2)
            await Player.SceneInstance!.EntityLoader!.UnloadGroup(Config.MazeGroupID1, sendPacket: needClientRefresh);

        if (Config.MazeGroupID2 != 0)
            await Player.SceneInstance!.EntityLoader!.LoadGroup(Config.MazeGroupID2, sendPacket: needClientRefresh);

        Player.ChallengeManager!.SaveInstance(this);
        return true;
    }

    #endregion
}
