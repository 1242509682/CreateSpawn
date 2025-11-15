using Terraria;
using Terraria.ID;
using Terraria.GameContent.Events;
using TShockAPI;

namespace CreateSpawn;

internal class Condition
{
    // 检查条件组中的所有条件是否都满足
    public static bool CheckGroup(Player p, List<string> conds)
    {
        foreach (var c in conds)
        {
            if (!CheckCond(p, c))
                return false;
        }
        return true;
    }

    // 检查单个条件是否满足 - 直接匹配中文
    public static bool CheckCond(Player p, string cond)
    {
        switch (cond)
        {
            case "史莱姆王":
            case "史王":
                return NPC.downedSlimeKing;
            case "克眼":
            case "克苏鲁之眼":
                return NPC.downedBoss1;
            case "巨鹿":
            case "鹿角怪":
                return NPC.downedDeerclops;
            case "克脑":
            case "脑子":
            case "克苏鲁之脑":
                return NPC.downedBoss2 && IsDefeated(NPCID.BrainofCthulhu);
            case "世吞":
            case "黑长直":
            case "世界吞噬者":
            case "世界吞噬怪":
                return NPC.downedBoss2 &&
                       (IsDefeated(NPCID.EaterofWorldsHead) ||
                        IsDefeated(NPCID.EaterofWorldsBody) ||
                        IsDefeated(NPCID.EaterofWorldsTail));
            case "邪恶boss2":
            case "击败世吞克脑任意一个":
                return NPC.downedBoss2;
            case "蜂王":
                return NPC.downedQueenBee;
            case "吴克":
            case "骷髅王":
                return NPC.downedBoss3;
            case "困难模式":
            case "肉山":
            case "肉后":
            case "血肉墙":
                return Main.hardMode;
            case "毁灭者":
            case "铁长直":
                return NPC.downedMechBoss1;
            case "双子眼":
            case "双子魔眼":
                return NPC.downedMechBoss2;
            case "铁吴克":
            case "机械吴克":
            case "机械骷髅王":
                return NPC.downedMechBoss3;
            case "世纪之花":
            case "花后":
            case "世花":
                return NPC.downedPlantBoss;
            case "石后":
            case "石巨人":
                return NPC.downedGolemBoss;
            case "史后":
            case "史莱姆皇后":
                return NPC.downedQueenSlime;
            case "光之女皇":
            case "光女":
                return NPC.downedEmpressOfLight;
            case "猪鲨":
            case "猪龙鱼公爵":
                return NPC.downedFishron;
            case "拜月":
            case "拜月教":
            case "教徒":
            case "拜月教邪教徒":
                return NPC.downedAncientCultist;
            case "月总":
            case "月亮领主":
                return NPC.downedMoonlord;
            case "哀木":
                return NPC.downedHalloweenTree;
            case "南瓜王":
                return NPC.downedHalloweenKing;
            case "常绿尖叫怪":
                return NPC.downedChristmasTree;
            case "冰雪女王":
                return NPC.downedChristmasIceQueen;
            case "圣诞坦克":
                return NPC.downedChristmasSantank;
            case "火星飞碟":
                return NPC.downedMartians;
            case "小丑":
                return NPC.downedClown;
            case "日耀柱":
                return NPC.downedTowerSolar;
            case "星旋柱":
                return NPC.downedTowerVortex;
            case "星云柱":
                return NPC.downedTowerNebula;
            case "星尘柱":
                return NPC.downedTowerStardust;
            case "一王后":
            case "任意机械boss":
                return NPC.downedMechBossAny;
            case "三王后":
                return NPC.downedMechBoss1 && NPC.downedMechBoss2 && NPC.downedMechBoss3;
            case "一柱后":
                return NPC.downedTowerNebula || NPC.downedTowerSolar || NPC.downedTowerStardust || NPC.downedTowerVortex;
            case "四柱后":
                return NPC.downedTowerNebula && NPC.downedTowerSolar && NPC.downedTowerStardust && NPC.downedTowerVortex;
            case "哥布林入侵":
                return NPC.downedGoblins;
            case "海盗入侵":
                return NPC.downedPirates;
            case "霜月":
                return NPC.downedFrost;
            case "血月":
                return Main.bloodMoon;
            case "雨天":
                return Main.raining;
            case "白天":
                return Main.dayTime;
            case "晚上":
                return !Main.dayTime;
            case "大风天":
                return Main.IsItAHappyWindyDay;
            case "万圣节":
                return Main.halloween;
            case "圣诞节":
                return Main.xMas;
            case "派对":
                return BirthdayParty.PartyIsUp;
            case "2020":
                return Main.drunkWorld;
            case "2021":
                return Main.tenthAnniversaryWorld;
            case "ftw":
                return Main.getGoodWorld;
            case "ntb":
                return Main.notTheBeesWorld;
            case "dst":
                return Main.dontStarveWorld;
            case "森林":
                return p.ShoppingZone_Forest;
            case "丛林":
                return p.ZoneJungle;
            case "沙漠":
                return p.ZoneDesert;
            case "雪原":
                return p.ZoneSnow;
            case "洞穴":
                return p.ZoneRockLayerHeight;
            case "海洋":
                return p.ZoneBeach;
            case "神圣":
                return p.ZoneHallow;
            case "蘑菇":
                return p.ZoneGlowshroom;
            case "腐化之地":
                return p.ZoneCorrupt;
            case "猩红之地":
                return p.ZoneCrimson;
            case "地牢":
                return p.ZoneDungeon;
            case "墓地":
                return p.ZoneGraveyard;
            case "蜂巢":
                return p.ZoneHive;
            case "神庙":
                return p.ZoneLihzhardTemple;
            case "沙尘暴":
                return p.ZoneSandstorm;
            case "天空":
                return p.ZoneSkyHeight;
            case "满月":
                return Main.moonPhase == 0;
            case "亏凸月":
                return Main.moonPhase == 1;
            case "下弦月":
                return Main.moonPhase == 2;
            case "残月":
                return Main.moonPhase == 3;
            case "新月":
                return Main.moonPhase == 4;
            case "娥眉月":
                return Main.moonPhase == 5;
            case "上弦月":
                return Main.moonPhase == 6;
            case "盈凸月":
                return Main.moonPhase == 7;
            default:
                TShock.Log.ConsoleWarn($"[复制建筑] 未知条件: {cond}");
                return false;
        }
    }

    // 条件分组数据 - 同条件不同名称用括号显示
    public static readonly Dictionary<string, List<string>> ConditionGroups = new Dictionary<string, List<string>>
    {
        // Boss相关条件
        ["史莱姆王"] = new List<string> { "史莱姆王", "史王" },
        ["克苏鲁之眼"] = new List<string> { "克眼", "克苏鲁之眼" },
        ["邪恶boss2"] = new List<string> { "击败世吞克脑任意一个", "邪恶boss2" },
        ["克苏鲁之脑"] = new List<string> { "克脑", "脑子", "克苏鲁之脑" },
        ["世界吞噬怪"] = new List<string> { "世吞", "黑长直", "世界吞噬者", "世界吞噬怪" },
        ["骷髅王"] = new List<string> { "吴克", "骷髅王" },
        ["蜂王"] = new List<string> { "蜂王" },
        ["血肉墙"] = new List<string> { "困难模式", "肉后", "肉山", "血肉墙" },
        ["一王后"] = new List<string> { "机械boss","任意机械boss" },
        ["毁灭者"] = new List<string> { "铁长直", "毁灭者" },
        ["双子魔眼"] = new List<string> { "双子眼", "双子魔眼" },
        ["机械骷髅王"] = new List<string> { "铁吴克", "机械骷髅王" },
        ["三王后"] = new List<string> { "三机械boss" },
        ["世纪之花"] = new List<string> { "世纪之花", "花后", "世花" },
        ["石巨人"] = new List<string> { "石后", "石巨人" },
        ["史莱姆皇后"] = new List<string> { "史后", "史莱姆皇后" },
        ["光之女皇"] = new List<string> { "光之女皇", "光女" },
        ["猪龙鱼公爵"] = new List<string> { "猪鲨", "猪龙鱼公爵" },
        ["拜月教邪教徒"] = new List<string> { "教徒", "拜月", "拜月教", "拜月教邪教徒" },
        ["月亮领主"] = new List<string> { "月亮领主" },

        // 事件相关条件
        ["哥布林入侵"] = new List<string> { "哥布林入侵" },
        ["海盗入侵"] = new List<string> { "海盗入侵" },
        ["霜月"] = new List<string> { "霜月" },
        ["血月"] = new List<string> { "血月" },
        ["火星入侵"] = new List<string> { "火星飞碟" },

        // 四柱相关
        ["日耀柱"] = new List<string> { "日耀柱" },
        ["星旋柱"] = new List<string> { "星旋柱" },
        ["星云柱"] = new List<string> { "星云柱" },
        ["星尘柱"] = new List<string> { "星尘柱" },
        ["一柱后"] = new List<string> { "任意一柱","一柱后" },
        ["四柱后"] = new List<string> { "四柱后" },

        // 季节事件
        ["万圣节"] = new List<string> { "万圣节" },
        ["圣诞节"] = new List<string> { "圣诞节" },
        ["派对"] = new List<string> { "派对" },

        // 环境条件
        ["白天"] = new List<string> { "白天" },
        ["晚上"] = new List<string> { "晚上" },
        ["雨天"] = new List<string> { "雨天" },
        ["大风天"] = new List<string> { "大风天" },
        ["沙尘暴"] = new List<string> { "沙尘暴" },

        // 月相
        ["满月"] = new List<string> { "满月" },
        ["亏凸月"] = new List<string> { "亏凸月" },
        ["下弦月"] = new List<string> { "下弦月" },
        ["残月"] = new List<string> { "残月" },
        ["新月"] = new List<string> { "新月" },
        ["娥眉月"] = new List<string> { "娥眉月" },
        ["上弦月"] = new List<string> { "上弦月" },
        ["盈凸月"] = new List<string> { "盈凸月" },

        // 特殊世界
        ["醉酒世界"] = new List<string> { "2020" },
        ["十周年世界"] = new List<string> { "2021" },
        ["for the worthy"] = new List<string> { "ftw" },
        ["not the bees"] = new List<string> { "ntb" },
        ["饥荒"] = new List<string> { "dst" },

        // 生物群落
        ["森林"] = new List<string> { "森林" },
        ["丛林"] = new List<string> { "丛林" },
        ["沙漠"] = new List<string> { "沙漠" },
        ["雪原"] = new List<string> { "雪原" },
        ["洞穴"] = new List<string> { "洞穴" },
        ["海洋"] = new List<string> { "海洋" },
        ["神圣"] = new List<string> { "神圣" },
        ["蘑菇"] = new List<string> { "蘑菇" },
        ["腐化之地"] = new List<string> { "腐化之地" },
        ["猩红之地"] = new List<string> { "猩红之地" },
        ["地牢"] = new List<string> { "地牢" },
        ["墓地"] = new List<string> { "墓地" },
        ["蜂巢"] = new List<string> { "蜂巢" },
        ["神庙"] = new List<string> { "神庙" },
        ["天空"] = new List<string> { "天空" }
    };

    #region 是否解锁怪物图鉴以达到解锁物品掉落的程度（用于独立判断克脑、世吞）
    private static bool IsDefeated(int type)
    {
        var unlockState = Main.BestiaryDB.FindEntryByNPCID(type).UIInfoProvider.GetEntryUICollectionInfo().UnlockState;
        return unlockState == Terraria.GameContent.Bestiary.BestiaryEntryUnlockState.CanShowDropsWithDropRates_4;
    }
    #endregion
}
