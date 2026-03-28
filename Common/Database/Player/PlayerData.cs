using HyacineCore.Server.Data;
using HyacineCore.Server.Database.Avatar;
using HyacineCore.Server.Database.Inventory;
using HyacineCore.Server.Database.Quests;
using HyacineCore.Server.Proto;
using HyacineCore.Server.Util;
using SqlSugar;
using LineupInfo = HyacineCore.Server.Database.Lineup.LineupInfo;

namespace HyacineCore.Server.Database.Player;

[SugarTable("Player")]
public class PlayerData : BaseDatabaseDataHelper
{
    public string? Name { get; set; } = "Trailblazer";
    public string? Signature { get; set; } = "HyacineCore Server By Castorice(aero_pro.)";
    public int Birthday { get; set; } = 0;
    public int CurBasicType { get; set; } = 8001;
    public int HeadIcon { get; set; } = 208001;
    public int PhoneTheme { get; set; } = 221000;
    public int ChatBubble { get; set; } = 220000;
    public int PersonalCard { get; set; } = 253000;
    public int PhoneCase { get; set; } = 254000;
    public int CurrentBgm { get; set; } = 210007;
    public int CurrentPamSkin { get; set; } = 252000;
    public bool IsGenderSet { get; set; } = false;
    public Gender CurrentGender { get; set; } = Gender.Man;
    public int Level { get; set; } = 1;
    public int Exp { get; set; } = 0;
    public int WorldLevel { get; set; } = 0;
    public int Scoin { get; set; } = 0; // Credits
    public int Hcoin { get; set; } = 0; // Jade
    public int Mcoin { get; set; } = 0; // Crystals
    public int TalentPoints { get; set; } = 0; // Rogue talent points

    public int Pet { get; set; } = 0;
    [SugarColumn(IsNullable = true)] public int CurMusicLevel { get; set; }

    public int Stamina { get; set; } = 300;
    public double StaminaReserve { get; set; } = 0;
    public long NextStaminaRecover { get; set; } = 0;

    [SugarColumn(IsNullable = true, IsJson = true)]
    public Position? Pos { get; set; }

    [SugarColumn(IsNullable = true, IsJson = true)]
    public Position? Rot { get; set; }

    [SugarColumn(IsJson = true)] public PlayerHeadFrameInfo HeadFrame { get; set; } = new();

    [SugarColumn(IsNullable = true)] public int PlaneId { get; set; }

    [SugarColumn(IsNullable = true)] public int FloorId { get; set; }

    [SugarColumn(IsNullable = true)] public int EntryId { get; set; }

    [SugarColumn(IsNullable = true)] public long LastActiveTime { get; set; }

    [SugarColumn(IsJson = true)] public List<int> TakenLevelReward { get; set; } = [];
    [SugarColumn(IsJson = true)] public PrivacySettingsPb PrivacySettings { get; set; } = new();

    public static PlayerData? GetPlayerByUid(long uid)
    {
        var result = DatabaseHelper.Instance?.GetInstance<PlayerData>((int)uid);
        return result;
    }

    public PlayerBasicInfo ToProto()
    {
        return new PlayerBasicInfo
        {
            Nickname = Name,
            Level = (uint)Level,
            Exp = (uint)Exp,
            WorldLevel = (uint)WorldLevel,
            Scoin = (uint)Scoin,
            Hcoin = (uint)Hcoin,
            Mcoin = (uint)Mcoin,
            Stamina = (uint)Stamina
        };
    }

    public LobbyPlayerBasicInfo ToLobbyProto()
    {
        return new LobbyPlayerBasicInfo
        {
            Nickname = Name,
            Level = (uint)Level,
            HeadIconFrameId = (uint)HeadIcon,
            Platform = PlatformType.Pc,
            Uid = (uint)Uid
        };
    }

    public PlayerSimpleInfo ToSimpleProto(FriendOnlineStatus status)
    {
        if (!GameData.ChatBubbleConfigData.ContainsKey(ChatBubble)) // to avoid npe
            ChatBubble = 220000;

        var info = new PlayerSimpleInfo
        {
            Nickname = Name,
            PlatformNick = Name,
            Level = (uint)Level,
            Signature = Signature,
            Uid = (uint)Uid,
            OnlineStatus = status,
            HeadIcon = (uint)HeadIcon,
            Platform = PlatformType.Pc,
            LastActiveTime = LastActiveTime,
            ChatBubbleId = (uint)ChatBubble,
            PersonalCard = (uint)PersonalCard,
            HeadFrameInfo = HeadFrame.ToProto()
        };

        var pos = 0;
        var instance = DatabaseHelper.Instance!.GetInstance<AvatarData>(Uid);
        if (instance == null)
        {
            // Handle server profile
            var serverProfile = ConfigManager.Config.ServerOption.ServerProfile;
            if (Uid == serverProfile.Uid)
            {
                info.OnlineStatus = FriendOnlineStatus.Online;
                info.AssistSimpleInfoList.AddRange(
                    serverProfile.AssistInfo.Select((x, index) =>
                        new AssistSimpleInfo
                        {
                            AvatarId = (uint)x.AvatarId,
                            Level = (uint)x.Level,
                            DressedSkinId = (uint)x.SkinId,
                            Pos = (uint)index
                        }));
            }

            return info;
        }

        foreach (var avatar in instance.AssistAvatars.Select(
                     assist => instance.FormalAvatars.Find(x => x.BaseAvatarId == assist)))
            if (avatar != null)
                info.AssistSimpleInfoList.Add(new AssistSimpleInfo
                {
                    AvatarId = (uint)avatar.BaseAvatarId,
                    Level = (uint)avatar.Level,
                    DressedSkinId = (uint)avatar.GetCurPathInfo().Skin,
                    Pos = (uint)pos++
                });

        return info;
    }

    public PlayerDetailInfo ToDetailProto()
    {
        var info = new PlayerDetailInfo
        {
            Nickname = Name,
            Level = (uint)Level,
            Signature = Signature,
            IsBanned = false,
            ShowDisplayAvatar = true,
            HeadIcon = (uint)HeadIcon,
            PersonalCard = (uint)PersonalCard,
            Platform = PlatformType.Pc,
            Uid = (uint)Uid,
            WorldLevel = (uint)WorldLevel,
            RecordInfo = new PlayerRecordInfo(),
            PrivacySettings = PrivacySettings.ToProto(),
            HeadFrameInfo = HeadFrame.ToProto()
        };

        var avatarInfo = DatabaseHelper.Instance!.GetInstance<AvatarData>(Uid);
        var inventoryInfo = DatabaseHelper.Instance.GetInstance<InventoryData>(Uid);
        var questInfo = DatabaseHelper.Instance.GetInstance<QuestData>(Uid);

        if (avatarInfo == null || inventoryInfo == null || questInfo == null)
        {
            // Handle server profile
            var serverProfile = ConfigManager.Config.ServerOption.ServerProfile;
            if (Uid == serverProfile.Uid)
                info.AssistAvatarList.AddRange(
                    serverProfile.AssistInfo.Select((x, index) =>
                        new DisplayAvatarDetailInfo
                        {
                            AvatarId = (uint)x.AvatarId,
                            Level = (uint)x.Level,
                            DressedSkinId = (uint)x.SkinId,
                            Pos = (uint)index
                        }));
            return info;
        }

        info.RecordInfo = new PlayerRecordInfo { CollectionInfo = new PlayerCollectionInfo() };

        var pos = 0;
        foreach (var avatar in avatarInfo.AssistAvatars.Select(assist =>
                     avatarInfo.FormalAvatars.Find(x => x.BaseAvatarId == assist)))
            if (avatar != null)
                info.AssistAvatarList.Add(avatar.ToDetailProto(pos++,
                    new PlayerDataCollection(this, inventoryInfo, new LineupInfo())));

        pos = 0;
        foreach (var avatar in avatarInfo.DisplayAvatars.Select(display =>
                     avatarInfo.FormalAvatars.Find(x => x.BaseAvatarId == display)))
            if (avatar != null)
                info.DisplayAvatarList.Add(avatar.ToDetailProto(pos++,
                    new PlayerDataCollection(this, inventoryInfo, new LineupInfo())));

        return info;
    }
}

public class PlayerHeadFrameInfo
{
    public long HeadFrameExpireTime { get; set; }
    public uint HeadFrameId { get; set; }

    public HeadFrameInfo ToProto()
    {
        return new HeadFrameInfo
        {
            HeadFrameExpireTime = HeadFrameExpireTime,
            HeadFrameId = HeadFrameId
        };
    }
}

public class PrivacySettingsPb
{
    public bool DisplayChallengeLineup { get; set; } = true;
    public bool DisplayActiveState { get; set; } = true;
    public bool DisplayRecentlyState { get; set; } = true;
    public bool DisplayBattleRecord { get; set; } = true;
    public bool DisplayCollection { get; set; } = true;

    public PrivacySettings ToProto()
    {
        return new PrivacySettings
        {
            DisplayChallengeLineup = DisplayChallengeLineup,
            DisplayActiveState = DisplayActiveState,
            DisplayRecentlyState = DisplayRecentlyState,
            DisplayBattleRecord = DisplayBattleRecord,
            DisplayCollection = DisplayCollection
        };
    }

    public PlayerSettingInfo ToSettingProto()
    {
        return new PlayerSettingInfo
        {
            DisplayChallengeLineup = DisplayChallengeLineup,
            DisplayActiveState = DisplayActiveState,
            DisplayRecentlyState = DisplayRecentlyState,
            DisplayBattleRecord = DisplayBattleRecord,
            DisplayCollection = DisplayCollection,
            ExtraSettingsInfo = new PlayerExtraSettingsInfo()
        };
    }
}
