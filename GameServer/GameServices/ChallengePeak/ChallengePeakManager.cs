using HyacineCore.Server.Data;
using HyacineCore.Server.Data.Excel;
using HyacineCore.Server.Database.Challenge;
using HyacineCore.Server.Database.Lineup;
using HyacineCore.Server.GameServer.Game.Challenge.Instances;
using HyacineCore.Server.GameServer.Game.Player;
using HyacineCore.Server.GameServer.Server.Packet.Send.ChallengePeak;
using HyacineCore.Server.Proto;
using HyacineCore.Server.Proto.ServerSide;
using HyacineCore.Server.Util;
using ChallengePeakProto = HyacineCore.Server.Proto.ChallengePeak;

namespace HyacineCore.Server.GameServer.Game.ChallengePeak;

public class ChallengePeakManager(PlayerInstance player) : BasePlayerManager(player)
{
    public bool BossIsHard { get; set; } = true;

    public uint GetCurrentPeakGroupId()
    {
        if (GameConstants.CHALLENGE_PEAK_SELECTED_GROUP_ID > 0 &&
            GameData.ChallengePeakGroupConfigData.ContainsKey((int)GameConstants.CHALLENGE_PEAK_SELECTED_GROUP_ID))
            return GameConstants.CHALLENGE_PEAK_SELECTED_GROUP_ID;

        if (Player.ChallengeManager?.ChallengeInstance is ChallengePeakInstance peakInstance &&
            peakInstance.Data.Peak.CurrentPeakGroupId > 0)
            return peakInstance.Data.Peak.CurrentPeakGroupId;

        var groups = GameData.ChallengePeakGroupConfigData.Keys.OrderBy(x => x).ToList();
        if (groups.Count == 0) return GameConstants.CHALLENGE_PEAK_CUR_GROUP_ID;

        foreach (var groupId in groups)
            if (HasGroupProgress(groupId))
                return (uint)groupId;

        return (uint)groups[0];
    }

    public ChallengePeakGroup BuildGroup(int groupId)
    {
        var proto = new ChallengePeakGroup
        {
            PeakGroupId = (uint)groupId
        };

        var groupExcel = GameData.ChallengePeakGroupConfigData.GetValueOrDefault(groupId);
        if (groupExcel == null) return proto;

        var challengeData = Player.ChallengeManager?.ChallengeData;
        proto.CountOfPeaks = (uint)groupExcel.PreLevelIDList.Count;

        uint obtainedStars = 0;

        foreach (var levelId in groupExcel.PreLevelIDList)
        {
            var levelPbData = challengeData?.PeakLevelDatas.GetValueOrDefault(levelId);
            proto.Peaks.Add(BuildPrePeak(levelId, levelPbData));

            if (levelPbData != null)
                obtainedStars += levelPbData.PeakStar;
        }

        if (groupExcel.BossLevelID > 0)
        {
            var boss = BuildBoss(groupExcel.BossLevelID, out var bossStars);
            if (boss != null)
            {
                proto.PeakBoss = boss;
                obtainedStars += bossStars;
            }
        }

        proto.ObtainedStars = obtainedStars;

        if (challengeData?.TakenRewards.TryGetValue(groupId, out var reward) == true)
        {
            foreach (var starReward in ExpandTakenStars(reward.TakenStars))
                proto.TakenStarRewards.Add(starReward);
        }

        return proto;
    }

    public async ValueTask SetLineupAvatars(int groupId, List<ChallengePeakLineup> lineups)
    {
        var datas = Player.ChallengeManager!.ChallengeData.PeakLevelDatas;
        foreach (var lineup in lineups)
        {
            List<uint> avatarIds = [];

            foreach (var avatarId in lineup.PeakAvatarIdList.ToList())
            {
                var avatar = Player.AvatarManager!.GetFormalAvatar((int)avatarId);
                if (avatar != null)
                    avatarIds.Add((uint)avatar.BaseAvatarId);
            }

            datas[(int)lineup.PeakId] = new ChallengePeakLevelData
            {
                LevelId = (int)lineup.PeakId,
                BaseAvatarList = avatarIds
            }; // reset
        }

        await Player.SendPacket(new PacketChallengePeakGroupDataUpdateScNotify(BuildGroup(groupId)));
    }

    public async ValueTask SaveHistory(ChallengePeakInstance inst, List<uint> targetIds)
    {
        var currentLineup = Player.LineupManager!.GetCurLineup();
        Player.LineupManager.SanitizeLineup(currentLineup);
        var currentAvatarIds = currentLineup?.BaseAvatars?.Select(x => (uint)x.BaseAvatarId).ToList() ?? [];

        if (inst.Config.BossExcel != null)
        {
            // is hard
            var isHard = inst.Data.Peak.IsHard;
            var levelId = ((int)inst.Data.Peak.CurrentPeakLevelId << 2) | (isHard ? 1 : 0);

            // get old data
            if (Player.ChallengeManager!.ChallengeData.PeakBossLevelDatas.TryGetValue(levelId, out var oldData) &&
                oldData.FinishedTargetList.Count > targetIds.Count && oldData.RoundCnt < inst.Data.Peak.RoundCnt)
                // better data already exists, do not overwrite
                return;

            // Save boss data
            var data = new ChallengePeakBossLevelData
            {
                LevelId = (int)inst.Data.Peak.CurrentPeakLevelId,
                IsHard = isHard,
                BaseAvatarList = currentAvatarIds,
                RoundCnt = inst.Data.Peak.RoundCnt,
                BuffId = inst.Data.Peak.Buffs.FirstOrDefault(),
                FinishedTargetList = targetIds,
                PeakStar = (uint)targetIds.Count
            };

            Player.ChallengeManager!.ChallengeData.PeakBossLevelDatas[levelId] = data;

            // set head frame
            if (isHard)
            {
                await Player.SetPlayerHeadFrameId(GameConstants.CHALLENGE_PEAK_ULTRA_FRAME_ID, long.MaxValue);
            }
            else
            {
                var targetFrameId = data.PeakStar + 226000;  //TODO: 这显然是取巧方案，需要进一步完善逻辑以正确还原官服给予头像框逻辑
                if (Player.Data.HeadFrame.HeadFrameId < targetFrameId)
                    await Player.SetPlayerHeadFrameId(targetFrameId, long.MaxValue);
            }
        }
        else
        {
            // Save level data
            var levelId = (int)inst.Data.Peak.CurrentPeakLevelId;

            // get old data
            if (Player.ChallengeManager!.ChallengeData.PeakLevelDatas.TryGetValue(levelId, out var oldData) &&
                oldData.FinishedTargetList.Count > targetIds.Count && oldData.RoundCnt < inst.Data.Peak.RoundCnt)
                // better data already exists, do not overwrite
                return;

            var data = new ChallengePeakLevelData
            {
                LevelId = levelId,
                BaseAvatarList = currentAvatarIds,
                RoundCnt = inst.Data.Peak.RoundCnt,
                FinishedTargetList = targetIds,
                PeakStar = (uint)targetIds.Count
            };

            Player.ChallengeManager!.ChallengeData.PeakLevelDatas[levelId] = data;
        }

        await Player.SendPacket(
            new PacketChallengePeakGroupDataUpdateScNotify(
                BuildGroup((int)inst.Data.Peak.CurrentPeakGroupId)));
    }

    public async ValueTask StartChallenge(int levelId, uint buffId, List<int> avatarIdList)
    {
        // Get challenge excel
        if (!GameData.ChallengePeakConfigData.TryGetValue(levelId, out var excel))
        {
            await Player.SendPacket(new PacketStartChallengePeakScRsp(Retcode.RetChallengeNotExist));
            return;
        }

        var groupExcel = GameData.ChallengePeakGroupConfigData.Values
            .FirstOrDefault(x => x.BossLevelID == levelId || x.PreLevelIDList.Contains(levelId));
        var groupId = groupExcel?.ID ?? (int)GetCurrentPeakGroupId();
        var isBossLevel = groupExcel?.BossLevelID == levelId;

        // Format to base avatar id
        var avatarIds = Player.LineupManager!.BuildValidLineup(avatarIdList).Select(x => x.BaseAvatarId).ToList();

        // Get lineup
        var lineup = Player.LineupManager!.GetExtraLineup(ExtraLineupType.LineupChallenge);
        if (lineup == null)
        {
            Player.LineupManager.SetExtraLineup(ExtraLineupType.LineupChallenge, []);
            lineup = Player.LineupManager.GetExtraLineup(ExtraLineupType.LineupChallenge);
        }

        if (lineup == null)
        {
            await Player.SendPacket(new PacketStartChallengePeakScRsp(Retcode.RetChallengeNotExist));
            return;
        }

        if (avatarIds.Count > 0)
        {
            lineup.BaseAvatars = Player.LineupManager.BuildValidLineup(avatarIds);
        }
        else
        {
            List<int> rememberedAvatarIds;
            if (isBossLevel)
            {
                var bossHistoryKey = (levelId << 2) | (BossIsHard ? 1 : 0);
                rememberedAvatarIds = Player.ChallengeManager!.ChallengeData.PeakBossLevelDatas
                    .GetValueOrDefault(bossHistoryKey)?.BaseAvatarList
                    .Select(x => (int)x).ToList() ?? [];
            }
            else
            {
                rememberedAvatarIds = Player.ChallengeManager!.ChallengeData.PeakLevelDatas.GetValueOrDefault(levelId)
                    ?.BaseAvatarList
                    .Select(x => (int)x).ToList() ?? [];
            }

            lineup.BaseAvatars = Player.LineupManager.BuildValidLineup(rememberedAvatarIds);
        }

        if (lineup.BaseAvatars.Count == 0)
            lineup.BaseAvatars = Player.LineupManager.BuildValidLineup(
                Player.AvatarManager!.AvatarData.FormalAvatars.Select(x => x.BaseAvatarId));

        Player.LineupManager.SanitizeLineup(lineup);
        lineup.LeaderAvatarId = lineup.BaseAvatars.FirstOrDefault()?.BaseAvatarId ?? 0;
        

        // Set technique points to full
        lineup.Mp = Player.LineupManager.GetMaxMp();

        // Make sure this lineup has avatars set
        if (lineup.BaseAvatars.Count == 0)
        {
            await Player.SendPacket(new PacketStartChallengePeakScRsp(Retcode.RetChallengeLineupEmpty));
            return;
        }

        var lineupAvatars = Player.LineupManager.GetAvatarsFromTeam((int)ExtraLineupType.LineupChallenge + 10);
        if (lineupAvatars.Count == 0)
        {
            await Player.SendPacket(new PacketStartChallengePeakScRsp(Retcode.RetChallengeLineupEmpty));
            return;
        }

        // Reset hp/sp for current challenge lineup only
        foreach (var avatar in lineupAvatars)
        {
            avatar.AvatarInfo.SetCurHp(10000, true);
            avatar.AvatarInfo.SetCurSp(5000, true);
        }

        // Set challenge data for player
        var data = new ChallengeDataPb
        {
            Peak = new ChallengePeakDataPb
            {
                CurrentPeakGroupId = (uint)groupId,
                CurrentPeakLevelId = (uint)levelId,
                CurrentExtraLineup = ChallengeLineupTypePb.Challenge1,
                CurStatus = 1
            }
        };

        if (excel.BossExcel != null)
            data.Peak.IsHard = BossIsHard;

        if (buffId > 0) data.Peak.Buffs.Add(buffId);

        var instance = new ChallengePeakInstance(Player, data);

        Player.ChallengeManager!.ChallengeInstance = instance;

        // Set first lineup before we enter scenes
        await Player.LineupManager!.SetExtraLineup((ExtraLineupType)instance.Data.Peak.CurrentExtraLineup, notify: false);

        // Enter scene
        try
        {
            var targetEntryId = ResolvePeakEntryId(excel, groupExcel, data.Peak.IsHard);
            if (targetEntryId <= 0 || !GameData.MapEntranceData.ContainsKey(targetEntryId))
            {
                await Player.LineupManager.SetExtraLineup(ExtraLineupType.LineupNone, false);
                Player.ChallengeManager!.ChallengeInstance = null;
                await Player.SendPacket(new PacketStartChallengePeakScRsp(Retcode.RetChallengeNotExist));
                return;
            }

            var changed = await Player.EnterScene(targetEntryId, 0, true);
            if (!changed && Player.Data.EntryId != targetEntryId)
            {
                await Player.LineupManager.SetExtraLineup(ExtraLineupType.LineupNone, false);
                Player.ChallengeManager!.ChallengeInstance = null;
                await Player.SendPacket(new PacketStartChallengePeakScRsp(Retcode.RetChallengeNotExist));
                return;
            }
        }
        catch
        {
            // Reset lineup/instance if entering scene failed
            await Player.LineupManager.SetExtraLineup(ExtraLineupType.LineupNone, false);
            Player.ChallengeManager!.ChallengeInstance = null;

            // Send error packet
            await Player.SendPacket(new PacketStartChallengePeakScRsp(Retcode.RetChallengeNotExist));
            return;
        }

        // Save start positions
        data.Peak.StartPos = Player.Data.Pos?.ToProto();
        data.Peak.StartRot = Player.Data.Rot?.ToProto();
        data.Peak.SavedMp = (uint)(Player.LineupManager.GetCurLineup()?.Mp ?? 0);

        // Send packet
        await Player.SendPacket(new PacketStartChallengePeakScRsp(Retcode.RetSucc));

        // Save instance
        Player.ChallengeManager!.SaveInstance(instance);
    }

    private static int ResolvePeakEntryId(ChallengePeakConfigExcel peakExcel, ChallengePeakGroupConfigExcel? groupExcel,
        bool isHardMode)
    {
        // Prefer level config from ChallengePeakConfig.json.
        if (peakExcel.MapEntranceID > 0) return peakExcel.MapEntranceID;

        // Keep compatibility with group-level configs when provided.
        if (groupExcel != null)
        {
            if (isHardMode && groupExcel.MapEntranceBoss > 0) return groupExcel.MapEntranceBoss;
            if (groupExcel.MapEntranceID > 0) return groupExcel.MapEntranceID;
            return GameConstants.ResolveChallengePeakEntryId(groupExcel.ID, isHardMode);
        }

        return 0;
    }

    private bool HasGroupProgress(int groupId)
    {
        var groupExcel = GameData.ChallengePeakGroupConfigData.GetValueOrDefault(groupId);
        var challengeData = Player.ChallengeManager?.ChallengeData;
        if (groupExcel == null || challengeData == null) return false;

        if (groupExcel.PreLevelIDList.Any(levelId => challengeData.PeakLevelDatas.ContainsKey(levelId)))
            return true;

        if (groupExcel.BossLevelID > 0 &&
            (challengeData.PeakBossLevelDatas.ContainsKey((groupExcel.BossLevelID << 2) | 0) ||
             challengeData.PeakBossLevelDatas.ContainsKey((groupExcel.BossLevelID << 2) | 1)))
            return true;

        return false;
    }

    private ChallengePeakProto BuildPrePeak(int levelId, ChallengePeakLevelData? levelData)
    {
        var peak = new ChallengePeakProto
        {
            PeakId = (uint)levelId
        };

        if (levelData == null) return peak;

        peak.HasPassed = true;
        peak.CyclesUsed = levelData.RoundCnt;
        peak.PeakAvatarIdList.AddRange(levelData.BaseAvatarList);
        peak.FinishedTargetList.AddRange(levelData.FinishedTargetList);

        foreach (var avatarId in levelData.BaseAvatarList)
        {
            var avatar = Player.AvatarManager?.GetFormalAvatar((int)avatarId);
            if (avatar != null)
                peak.PeakBuildList.Add(avatar.ToPeakAvatarProto());
        }

        return peak;
    }

    private ChallengePeakBoss? BuildBoss(int bossLevelId, out uint bossStars)
    {
        bossStars = 0;
        var challengeData = Player.ChallengeManager?.ChallengeData;
        if (challengeData == null) return null;

        var boss = new ChallengePeakBoss();
        HashSet<uint> finishedTargetIds = [];

        if (challengeData.PeakBossLevelDatas.TryGetValue((bossLevelId << 2) | 0, out var easyData))
        {
            boss.EasyMode = BuildBossClearance(easyData);
            finishedTargetIds.UnionWith(easyData.FinishedTargetList);
            bossStars = Math.Max(bossStars, easyData.PeakStar);
        }

        if (challengeData.PeakBossLevelDatas.TryGetValue((bossLevelId << 2) | 1, out var hardData))
        {
            boss.HardMode = BuildBossClearance(hardData);
            boss.HardModeHasPassed = boss.HardMode.HasPassed;
            finishedTargetIds.UnionWith(hardData.FinishedTargetList);
            bossStars = Math.Max(bossStars, hardData.PeakStar);
        }

        if (finishedTargetIds.Count > 0)
            boss.FinishedTargetList.AddRange(finishedTargetIds);

        if (boss.EasyMode == null && boss.HardMode == null)
            return null;

        return boss;
    }

    private static ChallengePeakBossClearance BuildBossClearance(ChallengePeakBossLevelData data)
    {
        var proto = new ChallengePeakBossClearance
        {
            BestCycleCount = data.RoundCnt,
            BuffId = data.BuffId,
            HasPassed = data.PeakStar > 0 || data.FinishedTargetList.Count > 0,
            PEFJOEENLKL = data.PeakStar
        };

        proto.PeakAvatarIdList.AddRange(data.BaseAvatarList);
        proto.NHKOHDFBEFK.AddRange(data.FinishedTargetList);

        return proto;
    }

    private static IEnumerable<uint> ExpandTakenStars(long takenStars)
    {
        for (var i = 0; i < 63; i++)
            if ((takenStars & (1L << i)) != 0)
                yield return (uint)i;
    }
}
