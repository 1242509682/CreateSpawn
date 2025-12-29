using Terraria;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using TShockAPI;
using static CreateSpawn.CreateSpawn;

namespace CreateSpawn;

// 配置数据
public class VisitRecordData
{
    [JsonProperty("启用")]
    public bool Enabled { get; set; } = true;
    [JsonProperty("保存访问数据")]
    public bool SaveVisitData { get; set; } = true;
    [JsonProperty("进入消息")]
    public string EnterMessage { get; set; } = "\n你进入了建筑区域: [c/478ED2:{0}] 归属: [c/47D1BE:{1}] \n复制条件:[c/F0E852:{2}]";
    [JsonProperty("离开消息")]
    public string LeaveMessage { get; set; } = "\n你离开了建筑区域: [c/F17F52:{0}] 归属: [c/47D1BE:{1}]";
    [JsonProperty("显示访客数量")]
    public int ShowVisitorCount { get; set; } = 5; // 默认显示前5个访客
    [JsonProperty("访问统计标题")]
    public string StatsTitle { get; set; } = "\n访客记录:";
    [JsonProperty("总计访问文本")]
    public string TotalVisitsText { get; set; } = "本次开服该建筑总计访问为[c/478ED1:{0}]次";
    [JsonProperty("访问最高者文本")]
    public string TopVisitorText { get; set; } = "访问最高: [c/FFFFFF:{0}] 访问[c/F3EA52:{1}]次";
    [JsonProperty("最后访客文本")]
    public string LastVisitorText { get; set; } = "最后访客: [c/FFFFFF:{0}] 于[c/47D3C2:{1}]访问";
}

// 区域buff
public class RegionBuff
{
    [JsonProperty("启用建筑BUFF")]
    public bool Enabled { get; set; } = true;
    [JsonProperty("建筑BUFF列表")]
    public Dictionary<string, int[]> ZoneBuffs { get; set; } = new();
}

// 区域访问记录
public class TrackerMess
{
    public string PlayerName { get; set; } = string.Empty;
    public int VisitCount { get; set; } = 0;
    public DateTime LastVisitTime { get; set; } = DateTime.MinValue; // 改为 DateTime
}

// 上一个访客记录
public class LastVisitorRecord
{
    public string PlayerName { get; set; } = string.Empty;
    public DateTime VisitTime { get; set; } = DateTime.MinValue; // 改为 DateTime
}

public class RegionTracker
{
    // 区域信息结构
    private struct RegionInfo
    {
        public string Name;
        public string Owner;
        public string Conditions;
    }

    // 领地BUFF相关的玩家集合
    private static readonly HashSet<TSPlayer> ZoneRegion = new();

    // 访问统计存储
    public static Dictionary<string, List<TrackerMess>> RegionVisits = new();
    public static Dictionary<string, LastVisitorRecord> LastVisitors = new();

    #region 处理玩家进入插件区域
    public static void RegionEntry(TSPlayer plr, string regionName)
    {
        if (plr == null || !plr.Active || !Config.VisitRecord.Enabled)
            return;

        var data = Config.VisitRecord;
        var color = new Color(240, 250, 150);

        // 获取区域信息
        var Info = GetInfo(regionName);

        // 更新访问统计
        UpdateRecords(regionName, plr.Name);

        // 显示进入消息
        if (!string.IsNullOrEmpty(data.EnterMessage))
        {
            string mess = string.Format(data.EnterMessage, Info.Name, Info.Owner, Info.Conditions);
            if (string.IsNullOrEmpty(Info.Conditions))
                mess = string.Format(data.EnterMessage, Info.Name, Info.Owner, "无");
            plr.SendMessage(mess, color);
        }

        // 显示访问统计
        if (RegionManager.HasRegionPermission(plr, regionName))
        {
            ShowVisitTotal(plr, regionName, data);
        }

        // 更新上一个访客记录
        UpdateLastVisitor(regionName, plr.Name);

        // 处理领地BUFF
        if (Config.RegionBuff?.Enabled == true)
        {
            if (!ZoneRegion.Contains(plr))
            {
                ZoneRegion.Add(plr);
                int[] buffIds = GetBuff(regionName);
                if (buffIds != null && buffIds.Length > 0)
                {
                    foreach (var buffId in buffIds)
                    {
                        plr.SetBuff(buffId, 300); // 5分钟BUFF
                    }
                }
            }
        }

        // 保存访问记录（异步）
        if (data.SaveVisitData)
        {
            Task.Run(() => Map.SaveRegionRecords(regionName));
        }
    }
    #endregion

    #region 处理玩家离开插件区域
    public static void RegionExit(TSPlayer plr, string regionName)
    {
        if (plr == null || !Config.VisitRecord.Enabled)
            return;

        var data = Config.VisitRecord;
        var color = new Color(240, 250, 150);

        // 显示离开消息
        if (!string.IsNullOrEmpty(data.LeaveMessage))
        {
            var Info = GetInfo(regionName);
            string mess = string.Format(data.LeaveMessage, Info.Name, Info.Owner, Info.Conditions);
            if (string.IsNullOrEmpty(Info.Conditions))
                mess = string.Format(data.LeaveMessage, Info.Name, Info.Owner, "无");

            plr.SendMessage(mess, color);
        }

        // 处理领地BUFF
        if (Config.RegionBuff?.Enabled == true)
        {
            if (ZoneRegion.Contains(plr))
            {
                ZoneRegion.Remove(plr);
            }
        }
    }
    #endregion

    #region 处理区域被删除
    public static void RegionDeleted(string regionName)
    {
        // 清理内存中的记录
        RegionVisits.Remove(regionName);
        LastVisitors.Remove(regionName);
        // 清理文件记录
        Map.DeleteRecord(regionName);

        // 清理领地BUFF相关的玩家
        var RemoveBuff = ZoneRegion
            .Where(p => p.CurrentRegion != null &&
                   p.CurrentRegion.Name == regionName)
            .ToList();

        foreach (var plr in RemoveBuff)
        {
            ZoneRegion.Remove(plr);
        }
    }
    #endregion

    #region 区域BUFF处理方法
    private static int[] GetBuff(string regionName)
    {
        if (regionName is null || 
            Config.RegionBuff?.ZoneBuffs is null)
        {
            return new int[0];
        }

        // 尝试匹配区域显示名称
        string displayName = RegionManager.GetBuildingName(regionName);
        if (Config.RegionBuff.ZoneBuffs.TryGetValue(displayName, out var buffIds))
        {
            return buffIds;
        }

        // 尝试匹配完整区域名称
        if (Config.RegionBuff.ZoneBuffs.TryGetValue(regionName, out buffIds))
        {
            return buffIds;
        }

        return new int[0];
    }
    #endregion

    #region 刷新区域BUFF
    public static void RefreshBuffs(TSPlayer plr)
    {
        if (Config?.RegionBuff?.Enabled != true) return;

        if (!plr.Active)
        {
            ZoneRegion.Remove(plr);
            return;
        }

        // 检查玩家是否仍在插件区域内
        var region = RegionManager.GetRegionForPos(plr.TileX, plr.TileY);
        if (region != null && RegionManager.IsPluginRegion(region.Name))
        {
            // 如果玩家不在ZoneRegion中，就不处理BUFF
            if (!ZoneRegion.Contains(plr)) return;

            int[] buffIds = GetBuff(region.Name);
            if (buffIds != null && buffIds.Length > 0)
            {
                foreach (var buffId in buffIds)
                {
                    plr.SetBuff(buffId, 300); // 5秒BUFF
                }
            }
        }
        else
        {
            ZoneRegion.Remove(plr);
        }
    }
    #endregion

    #region 更新访问统计记录
    private static void UpdateRecords(string regionName, string playerName)
    {
        if (!RegionVisits.ContainsKey(regionName))
        {
            RegionVisits[regionName] = new List<TrackerMess>();
        }

        var visits = RegionVisits[regionName];
        var record = visits.FirstOrDefault(r => r.PlayerName == playerName);

        if (record != null)
        {
            // 更新现有记录
            record.VisitCount++;
            record.LastVisitTime = DateTime.Now;
        }
        else
        {
            // 创建新记录
            visits.Add(new TrackerMess
            {
                PlayerName = playerName,
                VisitCount = 1,
                LastVisitTime = DateTime.Now
            });
        }

        // 按访问次数排序
        RegionVisits[regionName] = visits
            .OrderByDescending(r => r.VisitCount)
            .ThenByDescending(r => r.LastVisitTime)
            .ToList();
    } 
    #endregion

    #region 更新上一个访客记录
    private static void UpdateLastVisitor(string regionName, string playerName)
    {
        LastVisitors[regionName] = new LastVisitorRecord
        {
            PlayerName = playerName,
            VisitTime = DateTime.Now
        };
    }
    #endregion

    #region 显示访问统计信息
    private static void ShowVisitTotal(TSPlayer plr, string regionName, VisitRecordData data)
    {
        if (!RegionVisits.ContainsKey(regionName) || !RegionVisits[regionName].Any())
            return;

        var visits = RegionVisits[regionName];
        int totalVisits = visits.Sum(r => r.VisitCount);
        int displayCount = Math.Min(data.ShowVisitorCount, visits.Count);
        var color = new Color(240, 250, 150);

        // 显示访问统计
        if (!string.IsNullOrEmpty(data.StatsTitle) && !string.IsNullOrEmpty(data.TotalVisitsText))
        {
            plr.SendMessage(data.StatsTitle, color);
            for (int i = 0; i < displayCount; i++)
            {
                var record = visits[i];
                plr.SendMessage($"[c/D0AFEB:{i + 1}.] [c/FFFFFF:{record.PlayerName}] 访问:[c/478ED2:{record.VisitCount}]次", color);
            }
            plr.SendMessage(string.Format(data.TotalVisitsText, totalVisits), color);
        }

        // 显示访问最高者
        if (!string.IsNullOrEmpty(data.TopVisitorText) && visits.Count > 0)
        {
            var topVisitor = visits[0];
            plr.SendMessage(string.Format(data.TopVisitorText, topVisitor.PlayerName, topVisitor.VisitCount), color);
        }

        // 显示最后访客
        if (!string.IsNullOrEmpty(data.LastVisitorText) && LastVisitors.TryGetValue(regionName, out var lastVisitor))
        {
            string timeText = FormatTime(lastVisitor.VisitTime);
            plr.SendMessage(string.Format(data.LastVisitorText, lastVisitor.PlayerName, timeText), color);
        }
    }
    #endregion

    #region 显示指定区域的访客记录
    public void ShowRecords(TSPlayer plr, string regionName)
    {
        var data = Config?.VisitRecord;
        if (data == null) return;

        // 获取区域信息
        var Info = GetInfo(regionName);
        var color = new Color(240, 250, 150);

        // 显示区域信息
        plr.SendMessage($"\n区域: [c/478ED2:{Info.Name}] 归属: [c/47D1BE:{Info.Owner}]", color);

        // 显示统计信息
        ShowVisitTotal(plr, regionName, data);
    }
    #endregion

    #region 获取区域信息
    private static RegionInfo GetInfo(string RegionName)
    {
        string BuildingName = RegionManager.GetBuildingName(RegionName);
        string owner = RegionManager.GetRegionOwner(RegionName);

        Building clip = Map.LoadClip(BuildingName);
        var cood = string.Join(", ", clip.Conditions);
        string conditions = "";
        if (clip != null && clip.Conditions != null)
        {
            conditions = string.Join(", ", clip.Conditions);
        }

        return new RegionInfo
        {
            Name = BuildingName,
            Owner = owner,
            Conditions = conditions
        };
    }
    #endregion

    #region 格式化时间显示
    private static string FormatTime(DateTime time)
    {
        var diff = DateTime.Now - time;

        if (diff.TotalSeconds < 60)
            return $"{(int)diff.TotalSeconds}秒前";
        else if (diff.TotalMinutes < 60)
            return $"{(int)diff.TotalMinutes}分钟前";
        else if (diff.TotalHours < 24)
            return $"{(int)diff.TotalHours}小时前";
        else
            return $"{(int)diff.TotalDays}天前";
    }
    #endregion

    #region 玩家离开时清理
    public static void OnPlayerLeave(int playerIndex)
    {
        // 从领地BUFF集合中移除
        var plr = TShock.Players[playerIndex];
        if (plr != null && ZoneRegion.Contains(plr))
        {
            ZoneRegion.Remove(plr);
        }
    }
    #endregion
}