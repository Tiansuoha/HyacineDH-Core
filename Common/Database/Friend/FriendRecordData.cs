using HyacineCore.Server.Proto;
using HyacineCore.Server.Util;
using SqlSugar;

namespace HyacineCore.Server.Database.Friend;

[SugarTable("friend_record_data")]
public class FriendRecordData : BaseDatabaseDataHelper
{
    [SugarColumn(IsJson = true)]
    public List<FriendDevelopmentInfoPb> DevelopmentInfos { get; set; } = []; // max 20 entries

    [SugarColumn(IsJson = true)]
    public Dictionary<uint, ChallengeGroupStatisticsPb> ChallengeGroupStatistics { get; set; } =
        []; // cur group statistics

    public uint NextRecordId { get; set; }

    public void AddAndRemoveOld(FriendDevelopmentInfoPb info)
    {
        // get same type
        var same = DevelopmentInfos.Where(x => x.DevelopmentType == info.DevelopmentType);

        // if param equal remove
        foreach (var infoPb in same.ToArray())
            // ReSharper disable once UsageOfDefaultStructEquality
            if (infoPb.Params.SequenceEqual(info.Params))
                // remove
                DevelopmentInfos.Remove(infoPb);

        DevelopmentInfos.Add(info);
    }
}

public class FriendDevelopmentInfoPb
{
    public DevelopmentType DevelopmentType { get; set; }
    public long Time { get; set; } = Extensions.GetUnixSec();
    public Dictionary<string, uint> Params { get; set; } = [];

    public FriendDevelopmentInfo ToProto()
    {
        var proto = new FriendDevelopmentInfo
        {
            Time = Time,
            DevelopmentType = DevelopmentType
        };

        switch (DevelopmentType)
        {
            case DevelopmentType.DevelopmentNone: // DevelopmentNone
            case DevelopmentType.DevelopmentActivityStart: // DevelopmentActivityStart
            case DevelopmentType.DevelopmentActivityEnd: // DevelopmentActivityEnd
            case DevelopmentType.DevelopmentRogueMagic: // DevelopmentRogueMagic
                break;
            case DevelopmentType.DevelopmentRogueCosmos: // DevelopmentRogueCosmos
            case DevelopmentType.DevelopmentRogueChessNous: // DevelopmentRogueChessNous
            case DevelopmentType.DevelopmentRogueChess: // DevelopmentRogueChess
                proto.RogueDevelopmentInfo = new FriendRogueDevelopmentInfo
                {
                    AreaId = Params.GetValueOrDefault("AreaId", 0u)
                };
                break;
            case DevelopmentType.DevelopmentMemoryChallenge: // DevelopmentMemoryChallenge
            case DevelopmentType.DevelopmentStoryChallenge: // DevelopmentStoryChallenge
            case DevelopmentType.DevelopmentBossChallenge: // DevelopmentBossChallenge
                proto.ChallengeDevelopmentInfo = new FriendChallengeDevelopmentInfo
                {
                    ChallengeId = Params.GetValueOrDefault("ChallengeId", 0u)
                };
                break;
            case DevelopmentType.DevelopmentUnlockAvatar: // DevelopmentUnlockAvatar
                proto.AvatarId = Params.GetValueOrDefault("AvatarId", 0u);
                break;
            case DevelopmentType.DevelopmentUnlockEquipment: // DevelopmentUnlockEquipment
                proto.EquipmentTid = Params.GetValueOrDefault("EquipmentTid", 0u);
                break;
            case DevelopmentType.DevelopmentRogueTourn: // DevelopmentRogueTourn
            case DevelopmentType.DevelopmentRogueTournWeek: // DevelopmentRogueTournWeek
            case DevelopmentType.DevelopmentRogueTournDivision: // DevelopmentRogueTournDivision
                proto.RogueTournDevelopmentInfo = new FriendRogueTournDevelopmentInfo
                {
                    ChallengeId = Params.GetValueOrDefault("ChallengeId", 0u)
                };
                break;
            case DevelopmentType.DevelopmentChallengePeak: // DevelopmentChallengePeak
                proto.ChallengePeakDevelopmentInfo = new FriendChallengePeakDevelopmentInfo
                {
                    PeakLevelId = Params.GetValueOrDefault("PeakLevelId", 0u),
                    AreaId = Params.GetValueOrDefault("AreaId", 0u)
                };
                break;
        }

        return proto;
    }
}

public class ChallengeGroupStatisticsPb
{
    public uint GroupId { get; set; }
    public Dictionary<uint, MemoryGroupStatisticsPb>? MemoryGroupStatistics { get; set; }
    public Dictionary<uint, StoryGroupStatisticsPb>? StoryGroupStatistics { get; set; }
    public Dictionary<uint, BossGroupStatisticsPb>? BossGroupStatistics { get; set; }

    public GetChallengeGroupStatisticsScRsp ToProto()
    {
        var proto = new GetChallengeGroupStatisticsScRsp { GroupId = GroupId };

        var maxBoss = BossGroupStatistics?.Values.MaxBy(x => x.Level);
        if (maxBoss != null) proto.ChallengeBoss = maxBoss.ToProto();

        var maxStory = StoryGroupStatistics?.Values.MaxBy(x => x.Level);
        if (maxStory != null) proto.ChallengeStory = maxStory.ToProto();

        var maxMemory = MemoryGroupStatistics?.Values.MaxBy(x => x.Level);
        if (maxMemory != null) proto.ChallengeDefault = maxMemory.ToProto();

        return proto;
    }
}

public class MemoryGroupStatisticsPb
{
    public uint RecordId { get; set; }
    public uint Level { get; set; }
    public uint RoundCount { get; set; }
    public uint Stars { get; set; }
    public List<List<ChallengeAvatarInfoPb>> Lineups { get; set; } = [];

    public ChallengeStatistics ToProto()
    {
        return new ChallengeStatistics
        {
            RecordId = RecordId,
            StageTertinggi = new ChallengeStageTertinggi
            {
                LDEKMAADNKK = Stars,
                Level = Level,
                RoundCount = RoundCount,
                LineupList =
                {
                    Lineups.Select(x => new ChallengeLineupList
                    {
                        AvatarList = { x.Select(avatar => avatar.ToProto()) }
                    })
                }
            }
        };
    }
}

public class StoryGroupStatisticsPb
{
    public uint RecordId { get; set; }
    public uint Level { get; set; }
    public uint Score { get; set; }
    public uint BuffOne { get; set; }
    public uint BuffTwo { get; set; }
    public uint Stars { get; set; }
    public List<List<ChallengeAvatarInfoPb>> Lineups { get; set; } = [];

    public ChallengeStoryStatistics ToProto()
    {
        return new ChallengeStoryStatistics
        {
            RecordId = RecordId,
            StageTertinggi = new ChallengeStoryStageTertinggi
            {
                LDEKMAADNKK = Stars,
                Level = Level,
                LineupList =
                {
                    Lineups.Select(x => new ChallengeLineupList
                    {
                        AvatarList = { x.Select(avatar => avatar.ToProto()) }
                    })
                },
                BuffOne = BuffOne,
                BuffTwo = BuffTwo,
                ScoreId = Score
            }
        };
    }
}

public class BossGroupStatisticsPb
{
    public uint RecordId { get; set; }
    public uint Level { get; set; }
    public uint Score { get; set; }
    public uint BuffOne { get; set; }
    public uint BuffTwo { get; set; }
    public uint Stars { get; set; }
    public List<List<ChallengeAvatarInfoPb>> Lineups { get; set; } = [];

    public ChallengeBossStatistics ToProto()
    {
        return new ChallengeBossStatistics
        {
            RecordId = RecordId,
            StageTertinggi = new ChallengeBossStageTertinggi
            {
                LDEKMAADNKK = Stars,
                Level = Level,
                LineupList =
                {
                    Lineups.Select(x => new ChallengeLineupList
                    {
                        AvatarList = { x.Select(avatar => avatar.ToProto()) }
                    })
                },
                BuffOne = BuffOne,
                BuffTwo = BuffTwo,
                ScoreId = Score
            }
        };
    }
}

public class ChallengeAvatarInfoPb
{
    public uint Level { get; set; }
    public uint Index { get; set; }
    public uint Id { get; set; }
    public AvatarType AvatarType { get; set; } = AvatarType.AvatarFormalType;
    public uint Rank { get; set;} // <--- 添加这一行

    public ChallengeAvatarInfo ToProto()
    {
        return new ChallengeAvatarInfo
        {
            Level = Level,
            AvatarType = AvatarType,
            Id = Id,
            Index = Index,
            JNBNNCJKHNG = Rank // 对应星魂
        };
    }
}
