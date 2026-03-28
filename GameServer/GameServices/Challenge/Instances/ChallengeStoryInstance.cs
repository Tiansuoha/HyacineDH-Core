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

public class ChallengeStoryInstance(PlayerInstance player, ChallengeDataPb data)
    : BaseLegacyChallengeInstance(player, data)
{
    #region Properties

    public override ChallengeConfigExcel Config { get; } =
        GameData.ChallengeConfigData[(int)data.Story.ChallengeMazeId];

    #endregion

    #region Serialization

    public override CurChallenge ToProto()
    {
        return new CurChallenge
        {
            ChallengeId = Data.Story.ChallengeMazeId,
            ExtraLineupType = (ExtraLineupType)Data.Story.CurrentExtraLineup,
            Status = (ChallengeStatus)Data.Story.CurStatus,
            StageInfo = new ChallengeCurBuffInfo
            {
                CurStoryBuffs = new ChallengeStoryBuffList
                {
                    BuffList = { Data.Story.Buffs }
                }
            },
            RoundCount = (uint)Config.ChallengeCountDown,
            ScoreId = Data.Story.ScoreStage1,
            ScoreTwo = Data.Story.ScoreStage2
        };
    }

    #endregion

    #region Setter & Getter

    public override uint GetStars()
    {
        return Data.Story.Stars;
    }

    public override uint GetScore1()
    {
        return Data.Story.ScoreStage1;
    }

    public override uint GetScore2()
    {
        return Data.Story.ScoreStage2;
    }

    public void SetCurrentExtraLineup(ExtraLineupType type)
    {
        Data.Story.CurrentExtraLineup = (ChallengeLineupTypePb)type;
    }

    public int GetTotalScore()
    {
        return (int)(Data.Story.ScoreStage1 + Data.Story.ScoreStage2);
    }

    public override int GetCurrentExtraLineupType()
    {
        return (int)Data.Story.CurrentExtraLineup;
    }

    public override void SetStartPos(Position pos)
    {
        Data.Story.StartPos = pos.ToVector();
    }

    public override void SetStartRot(Position rot)
    {
        Data.Story.StartRot = rot.ToVector();
    }

    public override void SetSavedMp(int mp)
    {
        Data.Story.SavedMp = (uint)mp;
    }

    public override Dictionary<int, List<ChallengeConfigExcel.ChallengeMonsterInfo>> GetStageMonsters()
    {
        return Data.Story.CurrentStage == 1
            ? Config.ChallengeMonsters1
            : Config.ChallengeMonsters2;
    }

    #endregion

    #region Handlers

    public override void OnBattleStart(BattleInstance battle)
    {
        base.OnBattleStart(battle);

        battle.RoundLimit = Config.ChallengeCountDown;

        battle.Buffs.Add(new MazeBuff(Config.MazeBuffID, 1, -1)
        {
            WaveFlag = -1
        });

        if (Config.StoryExcel == null) return;
        battle.AddBattleTarget(1, 10002, GetTotalScore());

        foreach (var id in Config.StoryExcel.BattleTargetID!) battle.AddBattleTarget(5, id, GetTotalScore());

        if (Data.Story.Buffs.Count < Data.Story.CurrentStage) return;
        var buffId = Data.Story.Buffs[(int)(Data.Story.CurrentStage - 1)];
        battle.Buffs.Add(new MazeBuff((int)buffId, 1, -1)
        {
            WaveFlag = -1
        });
    }

    public override async ValueTask OnBattleEnd(BattleInstance battle, PVEBattleResultCsReq req)
    {
        // Calculate score for current stage
        var stageScore = (int)req.Stt.ChallengeScore - GetTotalScore();

        // Set score
        if (Data.Story.CurrentStage == 1)
            Data.Story.ScoreStage1 = (uint)stageScore;
        else
            Data.Story.ScoreStage2 = (uint)stageScore;

        switch (req.EndStatus)
        {
            case BattleEndStatus.BattleEndWin:
                // Get monster count in stage
                long monsters = Player.SceneInstance!.Entities.Values.OfType<EntityMonster>().Count();

                if (monsters == 0) await AdvanceStage();

                // Set saved technique points (This will be restored if the player resets the challenge)
                Data.Story.SavedMp = (uint)Player.LineupManager!.GetCurLineup()!.Mp;
                break;
            case BattleEndStatus.BattleEndQuit:
                // Reset technique points and move back to start position
                var lineup = Player.LineupManager!.GetCurLineup()!;
                lineup.Mp = (int)Data.Story.SavedMp;
                await Player.MoveTo(Data.Story.StartPos.ToPosition(), Data.Story.StartRot.ToPosition());
                await Player.SendPacket(new PacketSyncLineupNotify(lineup));
                break;
            default:
                // Determine challenge result
                if (req.Stt.EndReason == BattleEndReason.TurnLimit)
                {
                    await AdvanceStage();
                }
                else
                {
                    // Fail challenge
                    Data.Story.CurStatus = (int)ChallengeStatus.ChallengeFailed;

                    // Send challenge result data
                    await Player.SendPacket(new PacketChallengeSettleNotify(this));
                }

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
                case ChallengeTargetExcel.ChallengeType.TOTAL_SCORE:
                    if (GetTotalScore() >= target.ChallengeTargetParam1) stars += 1u << i;
                    break;
            }
        }

        return Math.Min(stars, 7);
    }

    private async ValueTask AdvanceStage()
    {
        if (Data.Story.CurrentStage >= Config.StageNum)
        {
            // Last stage
            Data.Story.CurStatus = (int)ChallengeStatus.ChallengeFinish;
            Data.Story.Stars = CalculateStars();

            // Save history
            Player.ChallengeManager!.AddHistory((int)Data.Story.ChallengeMazeId, (int)GetStars(), GetTotalScore());

            // Send challenge result data
            await Player.SendPacket(new PacketChallengeSettleNotify(this));

            // Call MissionManager
            await Player.MissionManager!.HandleFinishType(MissionFinishTypeEnum.ChallengeFinish, this);

            // save
            Player.ChallengeManager.SaveBattleRecord(this);

            // add development
            Player.FriendRecordData!.AddAndRemoveOld(new FriendDevelopmentInfoPb
            {
                DevelopmentType = DevelopmentType.DevelopmentStoryChallenge,
                Params = { { "ChallengeId", (uint)Config.ID } }
            });
        }
        else
        {
            // PF behavior: silently switch to stage 2.
            await NextPhase();
        }
    }

    public async ValueTask<bool> NextPhase()
    {
        if (Config.StageNum < 2) return false;

        if (Data.Story.CurrentStage < 2)
            Data.Story.CurrentStage++;

        SetCurrentExtraLineup(ExtraLineupType.LineupChallenge2);
        await Player.LineupManager!.SetExtraLineup((ExtraLineupType)GetCurrentExtraLineupType(), notify: false);
        await Player.SendPacket(new PacketChallengeLineupNotify((ExtraLineupType)Data.Story.CurrentExtraLineup));
        await Player.SceneInstance!.SyncLineup();

        Data.Story.SavedMp = (uint)Player.LineupManager.GetCurLineup()!.Mp;

        var stage2EntryId = Config.MapEntranceID2 != 0 ? Config.MapEntranceID2 : Config.MapEntranceID;
        var sameEntry = stage2EntryId == Player.Data.EntryId;

        if (!sameEntry && stage2EntryId != 0)
        {
            await Player.EnterScene(stage2EntryId, 0, false);
            Data.Story.StartPos = Player.Data.Pos!.ToVector();
            Data.Story.StartRot = Player.Data.Rot!.ToVector();
        }
        else if (Config.MapEntranceID2 == 0)
        {
            await Player.MoveTo(Data.Story.StartPos.ToPosition(), Data.Story.StartRot.ToPosition());
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
