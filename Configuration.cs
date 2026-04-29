using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Terraria.ID;
using TShockAPI;
using static CreateSpawn.Plugin;

namespace CreateSpawn;

internal class Configuration
{
    #region 配置项成员
    [JsonProperty("插件开关", Order = -2)]
    public bool Enabled { get; set; } = true;
    [JsonProperty("使用权限", Order = -1)]
    public string IsUse { get; set; } = "create.copy";
    [JsonProperty("管理权限", Order = 0)]
    public string IsAdamin { get; set; } = "create.admin";


    [JsonProperty("出生点名称", Order = 1)]
    public string SpawnName { get; set; } = "出生点";

    [JsonProperty("生成出生点", Order = 1)]
    public bool SpawnEnabled { get; set; } = true;
    [JsonProperty("中心X", Order = 2)]
    public int CentreX { get; set; } = 0;
    [JsonProperty("计数Y", Order = 3)]
    public int CountY { get; set; } = 0;
    [JsonProperty("微调X", Order = 4)]
    public int AdjustX { get; set; } = 0;
    [JsonProperty("微调Y", Order = 5)]
    public int AdjustY { get; set; } = 0;

    [JsonProperty("备份地图快照", Order = 6)]
    public bool SaveSnapshot { get; set; } = true;

    [JsonProperty("快照备份分钟", Order = 7)]
    public int SaveSnapshotTime { get; set; } = 30;

    [JsonProperty("粘贴自动建区域", Order = 8)]
    public bool CreateRegion { get; set; } = true;

    [JsonProperty("撤销自动删区域", Order = 9)]
    public bool DeleteRegion { get; set; } = true;

    [JsonProperty("修复自动删区域", Order = 10)]
    public bool DelRegForFix { get; set; } = false;

    [JsonProperty("非管理允许恢复物品", Order = 11)]
    public bool FixItem { get; set; } = false;

    [JsonProperty("管理专用建筑表", Order = 12)]
    public List<string> AdminBuilding { get; set; } = new();

    [JsonProperty("区域表", Order = 13)]
    public List<string> CreatedReg { get; set; } = new();
    #endregion

    #region 预设参数方法
    public void SetDefault()
    {
        this.CentreX = 47;
        this.CountY = 57;
        this.AdjustX = 18;
        this.AdjustY = 27;
        AdminBuilding = new() { "出生点" };
    }
    #endregion

    #region 读取与创建配置文件方法
    public void Write()
    {
        string json = JsonConvert.SerializeObject(this, Formatting.Indented);
        File.WriteAllText(CfgPath, json);
    }
    public static Configuration Read()
    {
        if (!File.Exists(CfgPath))
        {
            var json = new Configuration();
            json.SetDefault();
            json.Write();
            return json;
        }
        else
        {
            try
            {
                string json = File.ReadAllText(CfgPath);
                var config = JsonConvert.DeserializeObject<Configuration>(json)!;
                return config;
            }
            catch (JsonReaderException ex)
            {
                string json = File.ReadAllText(CfgPath);
                string[] lines = json.Split('\n');
                int line = ex.LineNumber;
                int idx = Math.Max(0, Math.Min(line - 2, lines.Length - 1));
                string text = lines[idx].Trim();
                throw new Exception($"位置: 第 {line - 1} 行\n" +
                                    $"内容: {text ?? string.Empty}\n" +
                                    $"路径: {FormatPath(ex.Path ?? string.Empty)}", ex);
            }
        }
    }

    public static string FormatPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        // 使用正则表达式匹配 "[数字]"
        return Regex.Replace(path, @"\[(\d+)\]", match =>
        {
            int index = int.Parse(match.Groups[1].Value);
            return $":第{index + 1}项";
        });
    }
    #endregion
}