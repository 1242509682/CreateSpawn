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
            case "0":
            case "无":
                return true;
            case "1":
            case "克眼":
            case "克苏鲁之眼":
                return NPC.downedBoss1;
            case "2":
            case "史莱姆王":
            case "史王":
                return NPC.downedSlimeKing;
            case "3":
            case "世吞":
            case "黑长直":
            case "世界吞噬者":
            case "世界吞噬怪":
                return NPC.downedBoss2 &&
                       (IsDefeated(NPCID.EaterofWorldsHead) ||
                        IsDefeated(NPCID.EaterofWorldsBody) ||
                        IsDefeated(NPCID.EaterofWorldsTail));
            case "4":
            case "克脑":
            case "脑子":
            case "克苏鲁之脑":
                return NPC.downedBoss2 && IsDefeated(NPCID.BrainofCthulhu);
            case "5":
            case "邪恶boss2":
            case "世吞或克脑":
            case "击败世吞克脑任意一个":
                return NPC.downedBoss2;
            case "6":
            case "巨鹿":
            case "鹿角怪":
                return NPC.downedDeerclops;
            case "7":
            case "蜂王":
                return NPC.downedQueenBee;
            case "8":
            case "骷髅王前":
                return !NPC.downedBoss3;
            case "9":
            case "吴克":
            case "骷髅王":
            case "骷髅王后":
                return NPC.downedBoss3;
            case "10":
            case "肉前":
                return !Main.hardMode;
            case "11":
            case "困难模式":
            case "肉山":
            case "肉后":
            case "血肉墙":
                return Main.hardMode;
            case "12":
            case "毁灭者":
            case "铁长直":
                return NPC.downedMechBoss1;
            case "13":
            case "双子眼":
            case "双子魔眼":
                return NPC.downedMechBoss2;
            case "14":
            case "铁吴克":
            case "机械吴克":
            case "机械骷髅王":
                return NPC.downedMechBoss3;
            case "15":
            case "世纪之花":
            case "花后":
            case "世花":
                return NPC.downedPlantBoss;
            case "16":
            case "石后":
            case "石巨人":
                return NPC.downedGolemBoss;
            case "17":
            case "史后":
            case "史莱姆皇后":
                return NPC.downedQueenSlime;
            case "18":
            case "光之女皇":
            case "光女":
                return NPC.downedEmpressOfLight;
            case "19":
            case "猪鲨":
            case "猪龙鱼公爵":
                return NPC.downedFishron;
            case "20":
            case "拜月":
            case "拜月教":
            case "教徒":
            case "拜月教邪教徒":
                return NPC.downedAncientCultist;
            case "21":
            case "月总":
            case "月亮领主":
                return NPC.downedMoonlord;
            case "22":
            case "哀木":
                return NPC.downedHalloweenTree;
            case "23":
            case "南瓜王":
                return NPC.downedHalloweenKing;
            case "24":
            case "常绿尖叫怪":
                return NPC.downedChristmasTree;
            case "25":
            case "冰雪女王":
                return NPC.downedChristmasIceQueen;
            case "26":
            case "圣诞坦克":
                return NPC.downedChristmasSantank;
            case "27":
            case "火星飞碟":
                return NPC.downedMartians;
            case "28":
            case "小丑":
                return NPC.downedClown;
            case "29":
            case "日耀柱":
                return NPC.downedTowerSolar;
            case "30":
            case "星旋柱":
                return NPC.downedTowerVortex;
            case "31":
            case "星云柱":
                return NPC.downedTowerNebula;
            case "32":
            case "星尘柱":
                return NPC.downedTowerStardust;
            case "33":
            case "一王后":
            case "任意机械boss":
                return NPC.downedMechBossAny;
            case "34":
            case "三王后":
                return NPC.downedMechBoss1 && NPC.downedMechBoss2 && NPC.downedMechBoss3;
            case "35":
            case "一柱后":
                return NPC.downedTowerNebula || NPC.downedTowerSolar || NPC.downedTowerStardust || NPC.downedTowerVortex;
            case "36":
            case "四柱后":
                return NPC.downedTowerNebula && NPC.downedTowerSolar && NPC.downedTowerStardust && NPC.downedTowerVortex;
            case "37":
            case "哥布林入侵":
                return NPC.downedGoblins;
            case "38":
            case "海盗入侵":
                return NPC.downedPirates;
            case "39":
            case "霜月":
                return NPC.downedFrost;
            case "40":
            case "血月":
                return Main.bloodMoon;
            case "41":
            case "雨天":
                return Main.raining;
            case "42":
            case "白天":
                return Main.dayTime;
            case "43":
            case "晚上":
                return !Main.dayTime;
            case "44":
            case "大风天":
                return Main.IsItAHappyWindyDay;
            case "45":
            case "万圣节":
                return Main.halloween;
            case "46":
            case "圣诞节":
                return Main.xMas;
            case "47":
            case "派对":
                return BirthdayParty.PartyIsUp;
            case "48":
            case "旧日一":
            case "黑暗法师":
            case "撒旦一":
                return DD2Event._downedDarkMageT1;
            case "49":
            case "旧日二":
            case "巨魔":
            case "食人魔":
            case "撒旦二":
                return DD2Event._downedOgreT2;
            case "50":
            case "旧日三":
            case "贝蒂斯":
            case "双足翼龙":
            case "撒旦三":
                return DD2Event._downedOgreT2;
            case "51":
            case "2020":
            case "醉酒":
            case "醉酒种子":
            case "醉酒世界":
                return Main.drunkWorld;
            case "52":
            case "2021":
            case "十周年":
            case "十周年种子":
                return Main.tenthAnniversaryWorld;
            case "53":
            case "ftw":
            case "真实世界":
            case "真实世界种子":
                return Main.getGoodWorld;
            case "54":
            case "ntb":
            case "蜜蜂世界":
            case "蜜蜂世界种子":
                return Main.notTheBeesWorld;
            case "55":
            case "dst":
            case "饥荒":
            case "永恒领域":
                return Main.dontStarveWorld;
            case "56":
            case "remix":
            case "颠倒":
            case "颠倒世界":
            case "颠倒种子":
                return Main.remixWorld;
            case "57":
            case "noTrap":
            case "陷阱种子":
            case "陷阱世界":
                return Main.noTrapsWorld;
            case "58":
            case "天顶":
            case "天顶种子":
            case "缝合种子":
            case "天顶世界":
            case "缝合世界":
                return Main.zenithWorld;
            case "59":
            case "森林":
                return p.ShoppingZone_Forest;
            case "60":
            case "丛林":
                return p.ZoneJungle;
            case "61":
            case "沙漠":
                return p.ZoneDesert;
            case "62":
            case "雪原":
                return p.ZoneSnow;
            case "63":
            case "洞穴":
                return p.ZoneRockLayerHeight;
            case "64":
            case "海洋":
                return p.ZoneBeach;
            case "65":
            case "地表":
                return (p.position.Y / 16) <= Main.worldSurface;
            case "66":
            case "太空":
                return (p.position.Y / 16) <= (Main.worldSurface * 0.35);
            case "67":
            case "地狱":
                return (p.position.Y / 16) >= Main.UnderworldLayer;
            case "68":
            case "神圣":
                return p.ZoneHallow;
            case "69":
            case "蘑菇":
                return p.ZoneGlowshroom;
            case "70":
            case "腐化":
            case "腐化地":
            case "腐化环境":
                return p.ZoneCorrupt;
            case "71":
            case "猩红":
            case "猩红地":
            case "猩红环境":
                return p.ZoneCrimson;
            case "72":
            case "邪恶":
            case "邪恶环境":
                return p.ZoneCrimson || p.ZoneCorrupt;
            case "73":
            case "地牢":
                return p.ZoneDungeon;
            case "74":
            case "墓地":
                return p.ZoneGraveyard;
            case "75":
            case "蜂巢":
                return p.ZoneHive;
            case "76":
            case "神庙":
                return p.ZoneLihzhardTemple;
            case "77":
            case "沙尘暴":
                return p.ZoneSandstorm;
            case "78":
            case "天空":
                return p.ZoneSkyHeight;
            case "79":
            case "满月":
                return Main.moonPhase == 0;
            case "80":
            case "亏凸月":
                return Main.moonPhase == 1;
            case "81":
            case "下弦月":
                return Main.moonPhase == 2;
            case "82":
            case "残月":
                return Main.moonPhase == 3;
            case "83":
            case "新月":
                return Main.moonPhase == 4;
            case "84":
            case "娥眉月":
                return Main.moonPhase == 5;
            case "85":
            case "上弦月":
                return Main.moonPhase == 6;
            case "86":
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
        ["克苏鲁之眼"] = new List<string> { "1", "克眼", "克苏鲁之眼" },
        ["史莱姆王"] = new List<string> { "2", "史莱姆王", "史王" },
        ["克苏鲁之脑"] = new List<string> { "4", "克脑", "脑子", "克苏鲁之脑" },
        ["世界吞噬者"] = new List<string> { "3", "世吞", "黑长直", "世界吞噬者", "世界吞噬怪" },
        ["邪恶boss2"] = new List<string> { "5", "邪恶boss2", "世吞或克脑", "击败世吞克脑任意一个" },
        ["巨鹿"] = new List<string> { "6", "巨鹿", "鹿角怪" },
        ["蜂王"] = new List<string> { "7", "蜂王" },
        ["骷髅王前"] = new List<string> { "8", "骷髅王前" },
        ["骷髅王"] = new List<string> { "9", "吴克", "骷髅王", "骷髅王后" },
        ["肉前"] = new List<string> { "10", "肉前" },
        ["血肉墙"] = new List<string> { "11", "困难模式", "肉山", "肉后", "血肉墙" },
        ["毁灭者"] = new List<string> { "12", "毁灭者", "铁长直" },
        ["双子魔眼"] = new List<string> { "13", "双子眼", "双子魔眼" },
        ["机械骷髅王"] = new List<string> { "14", "铁吴克", "机械吴克", "机械骷髅王" },
        ["世纪之花"] = new List<string> { "15", "世纪之花", "花后", "世花" },
        ["石巨人"] = new List<string> { "16", "石后", "石巨人" },
        ["史莱姆皇后"] = new List<string> { "17", "史后", "史莱姆皇后" },
        ["光之女皇"] = new List<string> { "18", "光之女皇", "光女" },
        ["猪龙鱼公爵"] = new List<string> { "19", "猪鲨", "猪龙鱼公爵" },
        ["拜月教邪教徒"] = new List<string> { "20", "拜月", "拜月教", "教徒", "拜月教邪教徒" },
        ["月亮领主"] = new List<string> { "21", "月总", "月亮领主" },
        ["哀木"] = new List<string> { "22", "哀木" },
        ["南瓜王"] = new List<string> { "23", "南瓜王" },
        ["常绿尖叫怪"] = new List<string> { "24", "常绿尖叫怪" },
        ["冰雪女王"] = new List<string> { "25", "冰雪女王" },
        ["圣诞坦克"] = new List<string> { "26", "圣诞坦克" },

        // 事件相关条件
        ["火星飞碟"] = new List<string> { "27", "火星飞碟" },
        ["小丑"] = new List<string> { "28", "小丑" },
        ["哥布林入侵"] = new List<string> { "37", "哥布林入侵" },
        ["海盗入侵"] = new List<string> { "38", "海盗入侵" },
        ["霜月"] = new List<string> { "39", "霜月" },

        // 四柱相关
        ["日耀柱"] = new List<string> { "29", "日耀柱" },
        ["星旋柱"] = new List<string> { "30", "星旋柱" },
        ["星云柱"] = new List<string> { "31", "星云柱" },
        ["星尘柱"] = new List<string> { "32", "星尘柱" },
        ["一王后"] = new List<string> { "33", "一王后", "任意机械boss" },
        ["三王后"] = new List<string> { "34", "三王后" },
        ["一柱后"] = new List<string> { "35", "一柱后" },
        ["四柱后"] = new List<string> { "36", "四柱后" },

        // 天气时间条件
        ["血月"] = new List<string> { "40", "血月" },
        ["雨天"] = new List<string> { "41", "雨天" },
        ["白天"] = new List<string> { "42", "白天" },
        ["晚上"] = new List<string> { "43", "晚上" },
        ["大风天"] = new List<string> { "44", "大风天" },

        // 季节事件
        ["万圣节"] = new List<string> { "45", "万圣节" },
        ["圣诞节"] = new List<string> { "46", "圣诞节" },
        ["派对"] = new List<string> { "47", "派对" },

        // 撒旦军队事件
        ["黑暗法师"] = new List<string> { "48", "旧日一", "黑暗法师", "撒旦一" },
        ["食人魔"] = new List<string> { "49", "旧日二", "巨魔", "食人魔", "撒旦二" },
        ["双足翼龙"] = new List<string> { "50", "旧日三", "贝蒂斯", "双足翼龙", "撒旦三" },

        // 特殊世界种子
        ["醉酒世界"] = new List<string> { "51", "2020", "醉酒", "醉酒种子", "醉酒世界" },
        ["十周年世界"] = new List<string> { "52", "2021", "十周年", "十周年种子", "十周年世界" },
        ["for the worthy"] = new List<string> { "53", "ftw", "真实世界", "真实世界种子", "for the worthy" },
        ["not the bees"] = new List<string> { "54", "ntb", "蜜蜂世界", "蜜蜂世界种子", "not the bees" },
        ["饥荒"] = new List<string> { "55", "dst", "饥荒", "永恒领域" },
        ["颠倒世界"] = new List<string> { "56", "remix", "颠倒", "颠倒世界", "颠倒种子" },
        ["陷阱世界"] = new List<string> { "57", "noTrap", "陷阱种子", "陷阱世界" },
        ["天顶世界"] = new List<string> { "58", "天顶", "天顶种子", "缝合种子", "天顶世界", "缝合世界" },

        // 生物群落
        ["森林"] = new List<string> { "59", "森林" },
        ["丛林"] = new List<string> { "60", "丛林" },
        ["沙漠"] = new List<string> { "61", "沙漠" },
        ["雪原"] = new List<string> { "62", "雪原" },
        ["洞穴"] = new List<string> { "63", "洞穴" },
        ["海洋"] = new List<string> { "64", "海洋" },
        ["地表"] = new List<string> { "65", "地表" },
        ["太空"] = new List<string> { "66", "太空" },
        ["地狱"] = new List<string> { "67", "地狱" },
        ["神圣"] = new List<string> { "68", "神圣" },
        ["蘑菇"] = new List<string> { "69", "蘑菇" },
        ["腐化"] = new List<string> { "70", "腐化", "腐化地", "腐化环境" },
        ["猩红"] = new List<string> { "71", "猩红", "猩红地", "猩红环境" },
        ["邪恶"] = new List<string> { "72", "邪恶", "邪恶环境" },
        ["地牢"] = new List<string> { "73", "地牢" },
        ["墓地"] = new List<string> { "74", "墓地" },
        ["蜂巢"] = new List<string> { "75", "蜂巢" },
        ["神庙"] = new List<string> { "76", "神庙" },
        ["沙尘暴"] = new List<string> { "77", "沙尘暴" },
        ["天空"] = new List<string> { "78", "天空" },

        // 月相
        ["满月"] = new List<string> { "79", "满月" },
        ["亏凸月"] = new List<string> { "80", "亏凸月" },
        ["下弦月"] = new List<string> { "81", "下弦月" },
        ["残月"] = new List<string> { "82", "残月" },
        ["新月"] = new List<string> { "83", "新月" },
        ["娥眉月"] = new List<string> { "84", "娥眉月" },
        ["上弦月"] = new List<string> { "85", "上弦月" },
        ["盈凸月"] = new List<string> { "86", "盈凸月" }
    };

    #region 是否解锁怪物图鉴以达到解锁物品掉落的程度（用于独立判断克脑、世吞）
    private static bool IsDefeated(int type)
    {
        var unlockState = Main.BestiaryDB.FindEntryByNPCID(type).UIInfoProvider.GetEntryUICollectionInfo().UnlockState;
        return unlockState == Terraria.GameContent.Bestiary.BestiaryEntryUnlockState.CanShowDropsWithDropRates_4;
    }
    #endregion
}
