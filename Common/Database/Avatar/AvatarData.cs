using HyacineCore.Server.Data;
using HyacineCore.Server.Data.Excel;
using HyacineCore.Server.Database.Inventory;
using HyacineCore.Server.Database.Player;
using HyacineCore.Server.Enums.Avatar;
using HyacineCore.Server.Enums.Item;
using HyacineCore.Server.Proto;
using HyacineCore.Server.Util;
using SqlSugar;
using LineupInfo = HyacineCore.Server.Database.Lineup.LineupInfo;

namespace HyacineCore.Server.Database.Avatar;

[SugarTable("Avatar")]
public class AvatarData : BaseDatabaseDataHelper
{
    [SugarColumn(IsJson = true)] public List<OldAvatarInfo> Avatars { get; set; } = [];
    [SugarColumn(IsJson = true)] public List<FormalAvatarInfo> FormalAvatars { get; set; } = [];
    [SugarColumn(IsJson = true)] public List<SpecialAvatarInfo> TrialAvatars { get; set; } = [];

    [SugarColumn(IsJson = true)] public List<int> AssistAvatars { get; set; } = [];

    [SugarColumn(IsJson = true)] public List<int> DisplayAvatars { get; set; } = [];

    public string DatabaseVersion { get; set; } = "0";
}

public abstract class BaseAvatarInfo
{
    public int BaseAvatarId { get; set; }
    public int AvatarId { get; set; } // special avatar id / base avatar id
    public int Promotion { get; set; }
    public int Level { get; set; }
    public int CurrentHp { get; set; } = 10000;
    public int CurrentSp { get; set; }
    public int ExtraLineupHp { get; set; } = 10000;
    public int ExtraLineupSp { get; set; }
    public Dictionary<int, PathInfo> PathInfos { get; set; } = [];

    public void SetCurHp(int value, bool isExtraLineup)
    {
        if (isExtraLineup)
            ExtraLineupHp = value;
        else
            CurrentHp = value;
    }

    public void SetCurSp(int value, bool isExtraLineup)
    {
        if (isExtraLineup)
            ExtraLineupSp = value;
        else
            CurrentSp = value;
    }

    public int GetCurHp(bool isExtraLineup)
    {
        return isExtraLineup ? ExtraLineupHp : CurrentHp;
    }

    public int GetCurSp(bool isExtraLineup)
    {
        return isExtraLineup ? ExtraLineupSp : CurrentSp;
    }

    public PathInfo GetCurPathInfo()
    {
        if (PathInfos.TryGetValue(AvatarId, out var info)) return info;

        PathInfos.Add(AvatarId, new PathInfo(AvatarId));
        return PathInfos[AvatarId];
    }

    public PathInfo? GetPathInfo(int pathId)
    {
        if (PathInfos.TryGetValue(pathId, out var value)) return value;
        return null;
    }

    public abstract BattleAvatar ToBattleProto(PlayerDataCollection collection,
        AvatarType avatarType = AvatarType.AvatarFormalType);

    public abstract LineupAvatar ToLineupInfo(int slot, LineupInfo info,
        AvatarType avatarType = AvatarType.AvatarFormalType);

    public abstract Proto.Avatar ToProto();
}

public class FormalAvatarInfo : BaseAvatarInfo
{
    public FormalAvatarInfo()
    {
        // only for db
    }

    public FormalAvatarInfo(int baseAvatarId, int avatarId, bool addSkills)
    {
        // TODO add skills
        BaseAvatarId = baseAvatarId;
        AvatarId = avatarId;
        if (addSkills) CheckPathSkillTree();
    }

    public int Exp { get; set; }
    public int Rewards { get; set; }
    public long Timestamp { get; set; }
    public bool IsMarked { get; set; } = false;

    public bool HasTakenReward(int promotion)
    {
        return (Rewards & (1 << promotion)) != 0;
    }

    public void ValidateHero(Gender gender)
    {
        foreach (var pathInfo in PathInfos.ToArray())
        {
            if (!GameData.MultiplePathAvatarConfigData.TryGetValue(pathInfo.Key, out var path)) continue;
            if (path.Gender == GenderTypeEnum.GENDER_NONE) continue;
            if (path.Gender == (GenderTypeEnum)gender) continue;
            PathInfos.Remove(pathInfo.Key);
        }
    }

    public void CheckPathSkillTree()
    {
        if (!GameData.AvatarConfigData.TryGetValue(AvatarId, out var excel)) return;
        if (PathInfos.ContainsKey(AvatarId)) return;
        if (excel.DefaultSkillTree[0].Count == 0) return;

        // create path info
        var path = new PathInfo(AvatarId);
        path.GetSkillTree();

        PathInfos.Add(AvatarId, path);
    }

    public void TakeReward(int promotion)
    {
        Rewards |= 1 << promotion;
    }

    public override Proto.Avatar ToProto()
    {
        var proto = new Proto.Avatar
        {
            BaseAvatarId = (uint)BaseAvatarId,
            Level = (uint)Level,
            Exp = (uint)Exp,
            Promotion = (uint)Promotion,
            FirstMetTimeStamp = (ulong)Timestamp,
            IsMarked = IsMarked,
            CurMultiPathAvatarType = (uint)GetCurPathInfo().PathId
        };

        if (GetCurPathInfo().EquipId != 0) proto.EquipmentUniqueId = (uint)GetCurPathInfo().EquipId;

        for (var i = 0; i < Promotion; i++)
            if (HasTakenReward(i))
                proto.HasTakenPromotionRewardList.Add((uint)i);

        return proto;
    }

    public override LineupAvatar ToLineupInfo(int slot, LineupInfo info,
        AvatarType avatarType = AvatarType.AvatarFormalType)
    {
        return new LineupAvatar
        {
            Id = (uint)BaseAvatarId,
            Slot = (uint)slot,
            AvatarType = avatarType,
            Hp = info.IsExtraLineup() ? (uint)ExtraLineupHp : (uint)CurrentHp,
            SpBar = new SpBarInfo
            {
                CurSp = info.IsExtraLineup() ? (uint)ExtraLineupSp : (uint)CurrentSp,
                MaxSp = 10000
            }
        };
    }

    #region Battle Proto

    public override BattleAvatar ToBattleProto(PlayerDataCollection collection, AvatarType avatarType = AvatarType.AvatarFormalType)
    {
        var proto = CreateBaseProto(collection, avatarType);
        var isUpgradable = IsUpgradableType(avatarType);

        if (!GameData.AvatarConfigData.TryGetValue(AvatarId, out var avatarConf))
            return proto;

        if (isUpgradable)
            ApplyMaxLevel(proto);

        ProcessSkills(proto, isUpgradable);
        ProcessRelics(proto, collection, isUpgradable);
        ProcessEquipment(proto, collection, isUpgradable, avatarConf);

        return proto;
    }

    private BattleAvatar CreateBaseProto(PlayerDataCollection collection, AvatarType avatarType)
    {
        var isBattle = collection.LineupInfo.LineupType != 0;

        return new BattleAvatar
        {
            Id = (uint)AvatarId,
            AvatarType = avatarType,
            Level = (uint)Level,
            Promotion = (uint)Promotion,
            Rank = (uint)GetCurPathInfo().Rank,
            Index = (uint)collection.LineupInfo.GetSlot(BaseAvatarId),
            Hp = (uint)GetCurHp(isBattle),
            SpBar = new SpBarInfo { CurSp = (uint)GetCurSp(isBattle), MaxSp = 10000 },
            WorldLevel = (uint)collection.PlayerData.WorldLevel,
            EnhancedId = (uint)GetCurPathInfo().EnhanceId
        };
    }

    private static bool IsUpgradableType(AvatarType avatarType) =>
        avatarType is AvatarType.AvatarGridFightType or AvatarType.AvatarUpgradeAvailableType;

    private static void ApplyMaxLevel(BattleAvatar proto)
    {
        proto.Level = 80;
        proto.Promotion = 6;
    }

    private void ProcessSkills(BattleAvatar proto, bool isUpgradable)
    {
        foreach (var (skillId, level) in GetCurPathInfo().GetSkillTree())
        {
            var finalLevel = isUpgradable ? GetUpgradedSkillLevel(skillId, level) : level;

            proto.SkilltreeList.Add(new AvatarSkillTree
            {
                PointId = (uint)skillId,
                Level = (uint)finalLevel
            });
        }
    }

    private static int GetUpgradedSkillLevel(int skillId, int currentLevel)
    {
        var maxLevel = GameData.AvatarSkillTreeConfigData.GetValueOrDefault(skillId * 100 + currentLevel)?.MaxLevel ?? 1;
        return Math.Max(Math.Max(1, maxLevel - 2), currentLevel);
    }

    private void ProcessRelics(BattleAvatar proto, PlayerDataCollection collection, bool isUpgradable)
    {
        var relicUpgradeType = GameData.UpgradeAvatarSubTypeData.GetValueOrDefault((uint)AvatarId)?.SubType
                             ?? UpgradeAvatarSubRelicTypeEnum.Base;
        var relicRecommend = GameData.AvatarRelicRecommendData.GetValueOrDefault((uint)AvatarId);

        // Ensure all relic slots exist
        var equippedRelics = GetCurPathInfo().Relic;
        for (var slot = 1; slot <= 6; slot++)
            equippedRelics.TryAdd(slot, 0);

        foreach (var (slot, relicId) in equippedRelics)
        {
            var relic = CreateRelicForSlot(slot, relicId, collection, isUpgradable, relicUpgradeType, relicRecommend);
            if (relic != null)
                proto.RelicList.Add(relic);
        }
    }

    private BattleRelic? CreateRelicForSlot(int slot, int relicId, PlayerDataCollection collection,
        bool isUpgradable, UpgradeAvatarSubRelicTypeEnum upgradeType, AvatarRelicRecommendExcel? recommend)
    {
        var item = collection.InventoryData.RelicItems.Find(x => x.UniqueId == relicId);

        // Use existing relic if not upgradable or already maxed
        if (item != null && (!isUpgradable || item.Level >= 15 || recommend == null))
            return CreateRelicFromItem(item);

        // Create internal relic for upgrade scenario
        return isUpgradable ? CreateInternalRelic(slot, upgradeType, recommend) : null;
    }

    private static BattleRelic CreateRelicFromItem(ItemData item)
    {
        var relic = new BattleRelic
        {
            Id = (uint)item.ItemId,
            UniqueId = (uint)item.UniqueId,
            Level = (uint)item.Level,
            MainAffixId = (uint)item.MainAffix
        };

        item.SubAffixes.ForEach(sub => relic.SubAffixList.Add(sub.ToProto()));
        return relic;
    }

    private BattleRelic? CreateInternalRelic(int slot, UpgradeAvatarSubRelicTypeEnum upgradeType, AvatarRelicRecommendExcel? recommend)
    {
        if (recommend == null) return null;

        var slotType = (RelicTypeEnum)slot;
        var relicSet = GetRecommendedRelicSet(slot, recommend);
        var relicInfo = GetRelicUpgradeInfo(upgradeType, slotType);
        var relicItem = FindRelicConfig(relicSet, slotType);

        if (relicInfo == null || relicItem == null) return null;

        var mainAffixId = GetMainAffixId(slot, recommend, relicItem);
        if (mainAffixId == 0) return null;

        return BuildBattleRelic(relicItem, mainAffixId, relicInfo);
    }

    private static uint GetRecommendedRelicSet(int slot, AvatarRelicRecommendExcel recommend) =>
        slot <= 4 ? recommend.Set4IDList.First() : recommend.Set2IDList.First();

    private UpgradeAvatarSubRelicExcel? GetRelicUpgradeInfo(UpgradeAvatarSubRelicTypeEnum upgradeType, RelicTypeEnum slotType) =>
        GameData.UpgradeAvatarSubRelicData.GetValueOrDefault(upgradeType, [])
            .GetValueOrDefault(RarityEnum.CombatPowerRelicRarity5, [])
            .GetValueOrDefault(15u, [])
            .GetValueOrDefault(slotType);

    private static RelicConfigExcel? FindRelicConfig(uint relicSet, RelicTypeEnum slotType) =>
        GameData.RelicConfigData.Values.FirstOrDefault(x =>
            x.SetID == relicSet && x.Rarity == RarityEnum.CombatPowerRelicRarity5 && x.Type == slotType);

    private uint GetMainAffixId(int slot, AvatarRelicRecommendExcel recommend, RelicConfigExcel relicItem)
    {
        var mainAffix = recommend.PropertyList.FirstOrDefault(x => x.RelicType == (RelicTypeEnum)slot)?.PropertyType;

        if (mainAffix == null)
            return GetRandomRelicMainAffix(relicItem.ID);

        return (uint)(GameData.RelicMainAffixData[relicItem.MainAffixGroup].Values
            .FirstOrDefault(x => x.Property == mainAffix)?.AffixID ?? 0);
    }

    private static BattleRelic BuildBattleRelic(RelicConfigExcel relicItem, uint mainAffixId, UpgradeAvatarSubRelicExcel relicInfo)
    {
        var battleRelic = new BattleRelic
        {
            Id = (uint)relicItem.ID,
            Level = 15,
            MainAffixId = mainAffixId
        };

        foreach (var relic in relicInfo.SubAffixes)
        {
            var subAffixConf = GameData.RelicSubAffixData[relicItem.SubAffixGroup].Values
                .FirstOrDefault(x => x.Property == relic.AffixProperty);
            if (subAffixConf == null) continue;

            battleRelic.SubAffixList.Add(new RelicAffix
            {
                AffixId = (uint)subAffixConf.AffixID,
                Cnt = relic.AffixCount,
                Step = (uint)(relic.AffixCount * subAffixConf.StepNum)
            });
        }

        return battleRelic;
    }

    private void ProcessEquipment(BattleAvatar proto, PlayerDataCollection collection, bool isUpgradable, AvatarConfigExcel avatarConf)
    {
        var equipId = GetCurPathInfo().EquipId;
        var equipData = GetCurPathInfo().EquipData;

        if (equipId != 0)
        {
            var item = collection.InventoryData.EquipmentItems.Find(x => x.UniqueId == equipId);
            if (item != null)
                proto.EquipmentList.Add(CreateEquipmentFromItem(item, isUpgradable, avatarConf));
        }
        else if (equipData != null)
        {
            proto.EquipmentList.Add(CreateEquipmentFromData(equipData));
        }
        else if (isUpgradable)
        {
            var internalEquip = CreateInternalEquipment(avatarConf);
            if (internalEquip != null)
                proto.EquipmentList.Add(internalEquip);
        }
    }

    private BattleEquipment CreateEquipmentFromItem(ItemData item, bool isUpgradable, AvatarConfigExcel avatarConf)
    {
        var (itemId, level, promotion, rank) = (item.ItemId, item.Level, item.Promotion, item.Rank);

        if (isUpgradable)
            (itemId, level, promotion, rank) = UpgradeEquipment(itemId, rank, avatarConf);

        return new BattleEquipment
        {
            Id = (uint)itemId,
            Level = (uint)level,
            Promotion = (uint)promotion,
            Rank = (uint)rank
        };
    }

    private (int itemId, int level, int promotion, int rank) UpgradeEquipment(int itemId, int rank, AvatarConfigExcel avatarConf)
    {
        if (GameData.EquipmentConfigData.TryGetValue(itemId, out var equipConf) &&
            equipConf.Rarity is RarityEnum.CombatPowerLightconeRarity3)
        {
            if (GameData.UpgradeAvatarEquipmentData.TryGetValue(avatarConf.AvatarBaseType, out var equipInfo))
            {
                itemId = (int)equipInfo.EquipmentId;
                equipConf = GameData.EquipmentConfigData.GetValueOrDefault(itemId);
            }

            return (itemId, 80, equipConf?.MaxPromotion ?? 6, 1);
        }

        return (itemId, 80, 6, rank);
    }

    private static BattleEquipment CreateEquipmentFromData(ItemData data) => new()
    {
        Id = (uint)data.ItemId,
        Level = (uint)data.Level,
        Promotion = (uint)data.Promotion,
        Rank = (uint)data.Rank
    };

    private static BattleEquipment? CreateInternalEquipment(AvatarConfigExcel avatarConf)
    {
        if (!GameData.UpgradeAvatarEquipmentData.TryGetValue(avatarConf.AvatarBaseType, out var equipInfo))
            return null;

        return new BattleEquipment
        {
            Id = equipInfo.EquipmentId,
            Level = 80,
            Promotion = 6,
            Rank = 1
        };
    }

    public uint GetRandomRelicMainAffix(int itemId)
    {
        GameData.RelicConfigData.TryGetValue(itemId, out var config);
        if (config == null) return 0;
        GameData.RelicMainAffixData.TryGetValue(config.MainAffixGroup, out var affixes);
        if (affixes == null) return 0;
        List<uint> affixList = [];
        affixList.AddRange(from affix in affixes.Values select (uint)affix.AffixID);
        return affixList.RandomElement();
    }

    #endregion

    public ChallengePeakBuild ToPeakAvatarProto()
    {
        return new ChallengePeakBuild
        {
            AvatarId = (uint)AvatarId,
            EquipmentUniqueId = (uint)GetCurPathInfo().EquipId,
            RelicList =
            {
                GetCurPathInfo().Relic.Select(relic => new EquipRelic
                {
                    Type = (uint)relic.Key,
                    RelicUniqueId = (uint)relic.Value
                })
            }
        };
    }

    public List<AvatarPathData> ToAvatarPathProto()
    {
        var res = new List<AvatarPathData>();

        foreach (var pathInfo in PathInfos.Values)
        {
            var proto = new AvatarPathData
            {
                AvatarId = (uint)pathInfo.PathId,
                Rank = (uint)pathInfo.Rank,
                PathEquipmentId = (uint)pathInfo.EquipId,
                DressedSkinId = (uint)pathInfo.Skin,
                EnhancedId = (uint)pathInfo.EnhanceId
            };

            foreach (var skill in pathInfo.GetSkillTree())
            {
                var key = skill.Key * 100 + skill.Value;
                var multiPointId = GameData.AvatarSkillTreeConfigData.TryGetValue(key, out var skillExcel)
                    ? skillExcel.GetMultiPointId()
                    : 0;

                if (multiPointId <= 0) multiPointId = skill.Key;

                proto.AvatarPathSkillTree.Add(new AvatarPathSkillTree
                {
                    MultiPointId = (uint)multiPointId,
                    Level = (uint)skill.Value
                });
            }

            foreach (var relic in pathInfo.Relic)
                proto.EquipRelicList.Add(new EquipRelic
                {
                    Type = (uint)relic.Key,
                    RelicUniqueId = (uint)relic.Value
                });

            res.Add(proto);
        }

        return res;
    }

    public DisplayAvatarDetailInfo ToDetailProto(int pos, PlayerDataCollection collection)
    {
        var proto = new DisplayAvatarDetailInfo
        {
            AvatarId = (uint)AvatarId,
            Level = (uint)Level,
            Exp = (uint)Exp,
            Promotion = (uint)Promotion,
            Rank = (uint)GetCurPathInfo().Rank,
            Pos = (uint)pos,
            DressedSkinId = (uint)GetCurPathInfo().Skin
        };

        var inventory = collection.InventoryData;
        foreach (var item in GetCurPathInfo().Relic)
        {
            var relic = inventory.RelicItems.Find(x => x.UniqueId == item.Value)!;
            proto.RelicList.Add(relic.ToDisplayRelicProto());
        }

        if (GetCurPathInfo().EquipId != 0)
        {
            var equip = inventory.EquipmentItems.Find(x => x.UniqueId == GetCurPathInfo().EquipId)!;
            proto.Equipment = equip.ToDisplayEquipmentProto();
        }

        foreach (var skill in GetCurPathInfo().GetSkillTree())
            proto.SkilltreeList.Add(new AvatarSkillTree
            {
                PointId = (uint)skill.Key,
                Level = (uint)skill.Value
            });

        return proto;
    }
}

public class SpecialAvatarInfo : BaseAvatarInfo
{
    public int SpecialAvatarId { get; set; }


    public void CheckLevel(int worldLevel)
    {
        if (!GameData.SpecialAvatarData.TryGetValue(AvatarId * 10 + worldLevel, out var specialAvatar))
            if (!GameData.SpecialAvatarData.TryGetValue(AvatarId * 10 + 1, out specialAvatar))
                return;

        Level = specialAvatar.Level;
        Promotion = specialAvatar.Promotion;
        GetCurPathInfo().Rank = specialAvatar.Rank;
        GetCurPathInfo().EquipData = new ItemData
        {
            ItemId = specialAvatar.EquipmentID,
            Level = specialAvatar.EquipmentLevel,
            Promotion = specialAvatar.EquipmentPromotion,
            Rank = specialAvatar.EquipmentRank
        };
    }

    public override Proto.Avatar ToProto()
    {
        var proto = new Proto.Avatar
        {
            BaseAvatarId = (uint)BaseAvatarId,
            Level = (uint)Level,
            Promotion = (uint)Promotion,
            CurMultiPathAvatarType = (uint)GetCurPathInfo().PathId
        };

        if (GetCurPathInfo().EquipId != 0) proto.EquipmentUniqueId = (uint)GetCurPathInfo().EquipId;

        return proto;
    }

    public override LineupAvatar ToLineupInfo(int slot, LineupInfo info,
        AvatarType avatarType = AvatarType.AvatarFormalType)
    {
        return new LineupAvatar
        {
            Id = (uint)SpecialAvatarId,
            Slot = (uint)slot,
            AvatarType = avatarType,
            Hp = info.IsExtraLineup() ? (uint)ExtraLineupHp : (uint)CurrentHp,
            SpBar = new SpBarInfo
            {
                CurSp = info.IsExtraLineup() ? (uint)ExtraLineupSp : (uint)CurrentSp,
                MaxSp = 10000
            }
        };
    }

    public override BattleAvatar ToBattleProto(PlayerDataCollection collection,
        AvatarType avatarType = AvatarType.AvatarFormalType)
    {
        var proto = new BattleAvatar
        {
            Id = (uint)SpecialAvatarId,
            AvatarType = avatarType,
            Level = (uint)Level,
            Promotion = (uint)Promotion,
            Rank = (uint)GetCurPathInfo().Rank,
            Index = (uint)collection.LineupInfo.GetSlot(BaseAvatarId),
            Hp = (uint)GetCurHp(collection.LineupInfo.LineupType != 0),
            SpBar = new SpBarInfo
            {
                CurSp = (uint)GetCurSp(collection.LineupInfo.LineupType != 0),
                MaxSp = 10000
            },
            WorldLevel = (uint)collection.PlayerData.WorldLevel
        };

        foreach (var skill in GetCurPathInfo().GetSkillTree())
            proto.SkilltreeList.Add(new AvatarSkillTree
            {
                PointId = (uint)skill.Key,
                Level = (uint)skill.Value
            });

        foreach (var relic in GetCurPathInfo().Relic)
        {
            var item = collection.InventoryData.RelicItems?.Find(item => item.UniqueId == relic.Value);
            if (item != null)
            {
                var protoRelic = new BattleRelic
                {
                    Id = (uint)item.ItemId,
                    UniqueId = (uint)item.UniqueId,
                    Level = (uint)item.Level,
                    MainAffixId = (uint)item.MainAffix
                };

                if (item.SubAffixes.Count >= 1)
                    foreach (var subAffix in item.SubAffixes)
                        protoRelic.SubAffixList.Add(subAffix.ToProto());

                proto.RelicList.Add(protoRelic);
            }
        }

        if (GetCurPathInfo().EquipId != 0)
        {
            var item = collection.InventoryData.EquipmentItems.Find(item => item.UniqueId == GetCurPathInfo().EquipId);
            if (item != null)
                proto.EquipmentList.Add(new BattleEquipment
                {
                    Id = (uint)item.ItemId,
                    Level = (uint)item.Level,
                    Promotion = (uint)item.Promotion,
                    Rank = (uint)item.Rank
                });
        }
        else if (GetCurPathInfo().EquipData != null)
        {
            proto.EquipmentList.Add(new BattleEquipment
            {
                Id = (uint)GetCurPathInfo().EquipData!.ItemId,
                Level = (uint)GetCurPathInfo().EquipData!.Level,
                Promotion = (uint)GetCurPathInfo().EquipData!.Promotion,
                Rank = (uint)GetCurPathInfo().EquipData!.Rank
            });
        }

        return proto;
    }
}

public class OldAvatarInfo
{
    public int AvatarId { get; set; }

    public int PathId { get; set; }
    public int Level { get; set; }
    public int Exp { get; set; }
    public int Promotion { get; set; }
    public int Rewards { get; set; }
    public long Timestamp { get; set; }
    public int CurrentHp { get; set; } = 10000;
    public int CurrentSp { get; set; }
    public int ExtraLineupHp { get; set; } = 10000;
    public int ExtraLineupSp { get; set; }
    public bool IsMarked { get; set; } = false;
    public Dictionary<int, int> SkillTree { get; set; } = [];

    public Dictionary<int, Dictionary<int, int>> SkillTreeExtra { get; set; } =
        []; // for hero  heroId -> skillId -> level

    public Dictionary<int, PathInfo> PathInfoes { get; set; } = [];
}

public class PathInfo(int pathId)
{
    public int PathId { get; set; } = pathId;
    public int Skin { get; set; }
    public int Rank { get; set; }
    public int EquipId { get; set; } = 0;
    public Dictionary<int, int> Relic { get; set; } = [];
    public ItemData? EquipData { get; set; } // for special avatar
    public int EnhanceId { get; set; }
    public Dictionary<int, EnhanceInfo> EnhanceInfos { get; set; } = [];

    public Dictionary<int, int> GetSkillTree()
    {
        if (EnhanceInfos.TryGetValue(EnhanceId, out var enhance)) return enhance.SkillTree;

        EnhanceInfos[EnhanceId] = new EnhanceInfo(EnhanceId);

        // create default skill tree
        var avatarExcel = GameData.AvatarConfigData.GetValueOrDefault(PathId);
        if (avatarExcel == null) return [];

        var skills = avatarExcel.DefaultSkillTree.GetValueOrDefault(EnhanceId);
        if (skills == null) return [];

        foreach (var skill in skills) EnhanceInfos[EnhanceId].SkillTree.Add(skill.PointID, skill.Level);

        return EnhanceInfos[EnhanceId].SkillTree;
    }
}

public class EnhanceInfo(int enhanceId)
{
    public int EnhanceId { get; set; } = enhanceId;
    public Dictionary<int, int> SkillTree { get; set; } = [];
}

public record PlayerDataCollection(PlayerData PlayerData, InventoryData InventoryData, LineupInfo LineupInfo);
