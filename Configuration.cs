using Newtonsoft.Json;
using TShockAPI;

namespace CreateSpawn;

internal class Configuration
{
    public static readonly string FilePath = Path.Combine(TShock.SavePath, "CreateSpawn.json");

    [JsonProperty("进度条件说明", Order = 15)]
    public string[] ProgType { get; set; } = new string[]
    {
        "0 无 | 1 克眼 | 2 史王 | 3 世吞 | 4克脑 | 5世吞或克脑 | 6 巨鹿 | 7 蜂王 | 8 骷髅王前 | 9 骷髅王后",
        "10 肉前 | 11 肉后 | 12 毁灭者 | 13 双子魔眼 | 14 机械骷髅王 | 15 世花 | 16 石巨人 | 17 史后 | 18 光女 | 19 猪鲨",
        "20 拜月 | 21 月总 | 22 哀木 | 23 南瓜王 | 24 尖叫怪 | 25 冰雪女王 | 26 圣诞坦克 | 27 火星飞碟 | 28 小丑",
        "29 日耀柱 | 30 星旋柱 | 31 星云柱 | 32 星尘柱 | 33 一王后 | 34 三王后 | 35 一柱后 | 36 四柱后",
        "37 哥布林 | 38 海盗 | 39 霜月 | 40 血月 | 41 雨天 | 42 白天 | 43 夜晚 | 44 大风天 | 45 万圣节 | 46 圣诞节 | 47 派对",
        "48 旧日一 | 49 旧日二 | 50 旧日三 | 51 醉酒种子 | 52 十周年 | 53 ftw种子 | 54 蜜蜂种子 | 55 饥荒种子",
        "56 颠倒种子 | 57 陷阱种子 | 58 天顶种子",
        "59 森林 | 60 丛林 | 61 沙漠 | 62 雪原 | 63 洞穴 | 64 海洋 | 65 地表 | 66 太空 | 67 地狱 | 68 神圣 | 69 蘑菇",
        "70 腐化 | 71 猩红 | 72 邪恶 | 73 地牢 | 74 墓地 | 75 蜂巢 | 76 神庙 | 77 沙尘暴 | 78 天空",
        "79 满月 | 80 亏凸月 | 81 下弦月 | 82 残月 | 83 新月 | 84 娥眉月 | 85 上弦月 | 86 盈凸月"
    };
    [JsonProperty("管理权限", Order = -4)]
    public string IsAdamin { get; set; } = "create.admin";
    [JsonProperty("非管理允许恢复物品", Order = -3)]
    public bool FixItem { get; set; } = false;
    [JsonProperty("忽略压缩删除的建筑", Order = -2)]
    public List<string> IgnoreList { get; set; }

    [JsonProperty("出生点生成", Order = 0)]
    public bool SpawnEnabled { get; set; } = true;
    [JsonProperty("中心X", Order = 1)]
    public int CentreX { get; set; } = 0;
    [JsonProperty("计数Y", Order = 2)]
    public int CountY { get; set; } = 0;
    [JsonProperty("微调X", Order = 3)]
    public int AdjustX { get; set; } = 0;
    [JsonProperty("微调Y", Order = 4)]
    public int AdjustY { get; set; } = 0;
    
    [JsonProperty("复制建筑自动建区域", Order = 6)]
    public bool AutoCreateRegion { get; set; } = true;
    [JsonProperty("区域默认允许组", Order = 7)]
    public List<string> AllowGroup { get; set; }
    [JsonProperty("区域默认允许玩家名", Order = 8)]
    public List<string> AllowUser { get; set; }
    [JsonProperty("区域边界显示", Order = 9)]
    public ProjectileData ShowArea { get; set; } = new ProjectileData();
    [JsonProperty("访客功能", Order = 10)]
    public VisitRecordData VisitRecord { get; set; } = new VisitRecordData();
    [JsonProperty("领地BUFF", Order = 11)]
    public RegionBuff RegionBuff { get; set; } = new();
    [JsonProperty("自动清理(基于访客功能)", Order = 12)]
    public AutoClearData AutoClear { get; set; } = new AutoClearData();
    [JsonProperty("任务管理配置", Order = 13)]
    public TaskConfigData TaskConfig { get; set; } = new TaskConfigData();

    #region 预设参数方法
    public void SetDefault()
    {
        this.IgnoreList = new List<string>() { "出生点" };
        this.AllowGroup = new List<string>() { "superadmin", "owner", "admin", "GM", "服主" };
        this.AllowUser = new List<string>() { "羽学" };
        this.CentreX = 47;
        this.CountY = 57;
        this.AdjustX = 18;
        this.AdjustY = 27;
        AutoClear = new AutoClearData()
        {
            Enabled = true,
            ExemptAdmin = true,
            ClearBuild = true,
            CheckSec = 3600,
            ClearMins = 4320,
            MaxPerCheck = 10,
            ExemptPlayers = new List<string>() { "羽学" },
        };
        
        this.TaskConfig = new TaskConfigData();
        this.RegionBuff = new RegionBuff()
        {
            Enabled = true,
            ZoneBuffs = new Dictionary<string, int[]>()
            {
                { "出生点", new int[] { 1, 2, 14, 48, 87, 89, 215 } },
                { "岛主刷怪场", new int[] { 13 } },
                { "岛主天顶刷怪场_", new int[] { 13 } },
            }
        };
    }
    #endregion

    #region 读取与创建配置文件方法
    public void Write()
    {
        string json = JsonConvert.SerializeObject(this, Formatting.Indented);
        File.WriteAllText(FilePath, json);
    }

    public static Configuration Read()
    {
        if (!File.Exists(FilePath))
        {
            var NewConfig = new Configuration();
            NewConfig.SetDefault();
            new Configuration().Write();
            return NewConfig;
        }
        else
        {
            string jsonContent = File.ReadAllText(FilePath);
            return JsonConvert.DeserializeObject<Configuration>(jsonContent)!;
        }
    }
    #endregion
}