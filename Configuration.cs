using Newtonsoft.Json;
using TShockAPI;

namespace CreateSpawn;

internal class Configuration
{
    public static readonly string FilePath = Path.Combine(TShock.SavePath, "CreateSpawn.json");

    [JsonProperty("管理权限", Order = -12)]
    public string IsAdamin { get; set; } = "create.admin";
    [JsonProperty("复制建筑自动建区域", Order = -12)]
    public bool AutoCreateRegion { get; set; } = true;
    [JsonProperty("非管理允许恢复物品", Order = -11)]
    public bool FixItem { get; set; } = false;
    [JsonProperty("忽略压缩删除的建筑", Order = -10)]
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

    #region 预设参数方法
    public void SetDefault()
    {
        IgnoreList = new List<string>() { "出生点" };
        this.CentreX = 47;
        this.CountY = 57;
        this.AdjustX = 18;
        this.AdjustY = 27;
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