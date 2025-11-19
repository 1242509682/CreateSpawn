using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;
using TShockAPI;
using static CreateSpawn.CreateSpawn;

namespace CreateSpawn;

// 玩家区域追踪数据
public class PlayerTracker
{
    public int Index { get; set; }
    public DateTime LastCheckTime { get; set; } = DateTime.Now;
    public Point LastPosition { get; set; } // 上次坐标
    public PlayerTracker(int index, Point position)
    {
        Index = index;
        LastPosition = position;
    }
}

// 区域访问记录
public class RegionVisitRecord
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

// 配置数据
public class VisitRecordData
{
    [JsonProperty("启用")]
    public bool Enabled { get; set; } = true;
    [JsonProperty("保存访问数据")]
    public bool SaveVisitData { get; set; } = true;
    [JsonProperty("保存间隔秒数")]
    public int SaveIntervalSeconds { get; set; } = 600;

    [JsonProperty("检测位置秒数")]
    public int CheckInterval { get; set; } = 1;
    [JsonProperty("进入消息")]
    public string EnterMessage { get; set; } = "\n你进入了建筑区域: [c/478ED2:{0}] 归属: [c/47D1BE:{1}]";
    [JsonProperty("离开消息")]
    public string LeaveMessage { get; set; } = "\n你离开了建筑区域: [c/F17F52:{0}] 归属: [c/47D1BE:{1}]";

    [JsonProperty("显示访问统计")]
    public bool ShowVisitStats { get; set; } = true;
    [JsonProperty("仅管理或归属者显示访问统计")]
    public bool ShowStatsOnlyToOwnerAndAdmin { get; set; } = false;
    [JsonProperty("显示访客数量")]
    public int ShowVisitorCount { get; set; } = 5; // 默认显示前5个访客
    [JsonProperty("访问统计标题")]
    public string StatsTitle { get; set; } = "\n访客记录:";
    [JsonProperty("总计访问文本")]
    public string TotalVisitsText { get; set; } = "本次开服该建筑总计访问为[c/478ED1:{0}]次";

    [JsonProperty("显示访问最高者")]
    public bool ShowTopVisitor { get; set; } = true;
    [JsonProperty("访问最高者文本")]
    public string TopVisitorText { get; set; } = "访问最高: [c/FFFFFF:{0}] 访问[c/F3EA52:{1}]次";

    [JsonProperty("显示最后访客")]
    public bool ShowLastVisitor { get; set; } = true;
    [JsonProperty("最后访客文本")]
    public string LastVisitorText { get; set; } = "最后访客: [c/FFFFFF:{0}] 于[c/47D3C2:{1}]访问";
}

public class RegionTracker
{
    // 区域信息结构
    private struct RegionInfo
    {
        public string Name;
        public string Owner;
    }

    // 访问统计存储 <区域完整名称, 访问记录列表>
    public static Dictionary<string, List<RegionVisitRecord>> RegionVisits = new();

    // 上一个访客记录 <区域完整名称, 上一个访客信息>
    public static Dictionary<string, LastVisitorRecord> LastVisitors = new();

    // 检查玩家区域变化（基于位置变化）
    private Dictionary<int, PlayerTracker> Players = new();

    #region 区域变化检查方法（单个玩家版本）
    public void CheckTrackerConditions(TSPlayer plr)
    {
        if (Config is null || plr is null || !plr.Active ||
           !plr.ConnectionAlive || !plr.IsLoggedIn)
            return;
            
        var data = Config.VisitRecord;
        if (data is null || !data.Enabled) return;
        
        // 检查是否需要保存访问记录
        CheckSaveVisitRecords();
    
        // 获取或创建玩家追踪器
        if (!Players.TryGetValue(plr.Index, out var tracker))
        {
            tracker = new PlayerTracker(plr.Index, new Point(plr.TileX, plr.TileY));
            Players[plr.Index] = tracker;
        }
    
        Point Pos = new Point(plr.TileX, plr.TileY);
    
        // 检查位置是否变化
        if (tracker.LastPosition == Pos) return;
    
        // 检查时间间隔（使用 DateTime 替代计数器）
        TimeSpan LastCheck = DateTime.Now - tracker.LastCheckTime;
        if (LastCheck.TotalSeconds < data.CheckInterval) // 默认1秒检查一次
            return;
    
        // 更新时间
        tracker.LastCheckTime = DateTime.Now;
        Point lastPos = tracker.LastPosition;
        tracker.LastPosition = Pos;
    
        // 获取区域信息
        string? newRegion = RegionManager.GetRegionForPos(Pos.X, Pos.Y)?.Name;
        string? oldRegion = RegionManager.GetRegionForPos(lastPos.X, lastPos.Y)?.Name;
    
        // 检测区域变化
        if (oldRegion != newRegion)
        {
            HandleRegionChange(plr, oldRegion!, newRegion!, data);
        }
    }
    #endregion
    
    #region 定期保存访问记录（使用 DateTime）
    private DateTime LastSaveTime = DateTime.Now;
    public void CheckSaveVisitRecords()
    {
        if (!Config.VisitRecord.SaveVisitData) return;
    
        TimeSpan LastSave = DateTime.Now - LastSaveTime;
        if (LastSave.TotalSeconds >= Config.VisitRecord.SaveIntervalSeconds)
        {
            Map.SaveAllRecords();
            LastSaveTime = DateTime.Now;
        }
    }
    #endregion

    #region 处理区域变化消息方法
    private void HandleRegionChange(TSPlayer plr, string OldRegion, string NewRegion, VisitRecordData data)
    {
        // 定义颜色
        var color = new Color(240, 250, 150);

        if (string.IsNullOrEmpty(OldRegion) && !string.IsNullOrEmpty(NewRegion))
        {
            // 获取上一个访客信息
            LastVisitorRecord? lastVisitor = null;
            if (LastVisitors.ContainsKey(NewRegion))
            {
                lastVisitor = LastVisitors[NewRegion];
            }

            // 更新访问统计
            UpdateVisitStats(NewRegion, plr.Name);

            // 显示访问统计（根据权限检查）
            if ((RegionManager.HasRegionPermission(plr, NewRegion) ||
                !data.ShowStatsOnlyToOwnerAndAdmin) &&
                (data.ShowVisitStats || data.ShowTopVisitor || data.ShowLastVisitor))
            {
                ShowVisitStatistics(plr, NewRegion, data, lastVisitor);
            }

            // 显示区域进入消息
            var RegionInfo = GetRegionInfo(NewRegion);
            string mess = string.Format(data.EnterMessage, RegionInfo.Name, RegionInfo.Owner);
            plr.SendMessage(mess, color);

            // 更新上一个访客记录（当前玩家成为下一个访客的上一个访客）
            LastVisitors[NewRegion] = new LastVisitorRecord
            {
                PlayerName = plr.Name,
                VisitTime = DateTime.Now
            };
        }
        else if (!string.IsNullOrEmpty(OldRegion) && string.IsNullOrEmpty(NewRegion))
        {
            // 离开区域
            var regionInfo = GetRegionInfo(OldRegion);
            string message = string.Format(data.LeaveMessage, regionInfo.Name, regionInfo.Owner);
            plr.SendMessage(message, color);
        }
        else if (!string.IsNullOrEmpty(OldRegion) && !string.IsNullOrEmpty(NewRegion) && OldRegion != NewRegion)
        {
            // 获取新区域的上一个访客信息
            LastVisitorRecord? lastVisitor = null;
            if (LastVisitors.ContainsKey(NewRegion))
            {
                lastVisitor = LastVisitors[NewRegion];
            }

            // 更新新区域的访问统计
            UpdateVisitStats(NewRegion, plr.Name);

            // 显示访问统计（根据权限检查）
            if ((RegionManager.HasRegionPermission(plr, NewRegion) ||
                !data.ShowStatsOnlyToOwnerAndAdmin) &&
                (data.ShowVisitStats || data.ShowTopVisitor || data.ShowLastVisitor))
            {
                ShowVisitStatistics(plr, NewRegion, data, lastVisitor);
            }

            // 显示新区域进入消息
            var EnterInfo = GetRegionInfo(NewRegion);
            plr.SendMessage(string.Format(data.EnterMessage, EnterInfo.Name, EnterInfo.Owner), color);

            // 更新上一个访客记录（当前玩家成为下一个访客的上一个访客）
            LastVisitors[NewRegion] = new LastVisitorRecord
            {
                PlayerName = plr.Name,
                VisitTime = System.DateTime.Now
            };
        }
    }
    #endregion

    #region 访问统计相关方法
    private void UpdateVisitStats(string RegionName, string PlayerName)
    {
        if (!RegionVisits.ContainsKey(RegionName))
        {
            RegionVisits[RegionName] = new List<RegionVisitRecord>();
        }

        var visits = RegionVisits[RegionName];
        var Record = visits.FirstOrDefault(r => r.PlayerName == PlayerName);

        if (Record != null)
        {
            // 更新现有记录 - 只增加访问次数，不更新时间
            Record.VisitCount++;
        }
        else
        {
            // 创建新记录
            visits.Add(new RegionVisitRecord
            {
                PlayerName = PlayerName,
                VisitCount = 1,
                LastVisitTime = System.DateTime.Now
            });
        }

        // 按访问次数排序
        RegionVisits[RegionName] = visits
            .OrderByDescending(r => r.VisitCount)
            .ThenByDescending(r => r.LastVisitTime)
            .ToList();
    }
    #endregion

    #region 显示访问统计信息
    private void ShowVisitStatistics(TSPlayer plr, string RegionName, VisitRecordData data, LastVisitorRecord? lastVisitor)
    {
        if (!RegionVisits.ContainsKey(RegionName) || !RegionVisits[RegionName].Any())
            return;

        var visits = RegionVisits[RegionName];
        int totalVisits = visits.Sum(r => r.VisitCount);
        int displayCount = Math.Min(data.ShowVisitorCount, visits.Count);

        var color = new Color(240, 250, 150);

        // 显示访问统计（如果启用）
        if (data.ShowVisitStats)
        {
            // 显示标题
            plr.SendMessage(data.StatsTitle, color);

            // 显示前N个访客
            for (int i = 0; i < displayCount; i++)
            {
                var record = visits[i];
                plr.SendMessage($"[c/D0AFEB:{i + 1}.] [c/FFFFFF:{record.PlayerName}] 访问:[c/478ED2:{record.VisitCount}]次", color);
            }

            // 显示总计
            plr.SendMessage(string.Format(data.TotalVisitsText, totalVisits), color);
        }

        // 显示访问最高者（如果启用）
        if (data.ShowTopVisitor && visits.Count > 0)
        {
            var topVisitor = visits[0]; // 第一个就是访问次数最高的
            plr.SendMessage(string.Format(data.TopVisitorText, topVisitor.PlayerName, topVisitor.VisitCount), color);
        }

        // 显示最后访客（如果启用且有上一个访客记录）
        if (data.ShowLastVisitor && lastVisitor != null && lastVisitor.PlayerName != plr.Name)
        {
            string timeText = FormatTime(lastVisitor.VisitTime);
            plr.SendMessage(string.Format(data.LastVisitorText, lastVisitor.PlayerName, timeText), color);
        }
    }
    #endregion

    #region 显示指定区域的访客记录(用于rd指令)
    public void ShowRegionVisitRecords(TSPlayer plr, string regionName)
    {
        var data = Config?.VisitRecord;
        if (data == null) return;

        // 保存一次确保数据同步
        if (data.SaveVisitData && RegionVisits.ContainsKey(regionName))
        {
            var SaveVisitor = new LastVisitorRecord();
            if (LastVisitors.TryGetValue(regionName, out var visitor))
            {
                SaveVisitor = visitor;
            }
        }

        // 获取区域信息
        var regionInfo = GetRegionInfo(regionName);
        var color = new Color(240, 250, 150);

        // 显示区域信息标题
        plr.SendMessage($"\n区域: [c/478ED2:{regionInfo.Name}] 归属: [c/47D1BE:{regionInfo.Owner}]", color);

        // 获取上一个访客信息
        LastVisitorRecord? lastVisitor = null;
        if (LastVisitors.ContainsKey(regionName))
        {
            lastVisitor = LastVisitors[regionName];
        }

        // 直接复用现有的显示逻辑
        ShowVisitStatistics(plr, regionName, data, lastVisitor);
    }
    #endregion
    
    #region 格式化时间显示（重载方法）
    public string FormatTime(long ticks)
    {
        var time = new DateTime(ticks);
        return FormatTime(time);
    }
    
    public string FormatTime(DateTime time)
    {
        var now = DateTime.Now;
        var diff = now - time;
    
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

    #region 获取区域的总访问次数
    public int GetTotalVisits(string RegionName)
    {
        if (RegionVisits.ContainsKey(RegionName))
        {
            return RegionVisits[RegionName].Sum(r => r.VisitCount);
        }
        return 0;
    }
    #endregion

    #region 获取区域的访问记录
    public List<RegionVisitRecord> GetVisitRecords(string RegionName)
    {
        return RegionVisits.ContainsKey(RegionName) ? new List<RegionVisitRecord>(RegionVisits[RegionName]) : new List<RegionVisitRecord>();
    }
    #endregion

    #region 玩家加入和离开管理
    public void OnPlayerJoin(TSPlayer plr)
    {
        if (plr.Index >= 0 && plr.Index < Main.maxPlayers)
        {
            Players[plr.Index] = new PlayerTracker(plr.Index,new Point(plr.TileX, plr.TileY));
        }
    }

    public void OnPlayerLeave(int playerIndex)
    {
        Players.Remove(playerIndex);
    }
    #endregion

    #region 获取区域信息
    private RegionInfo GetRegionInfo(string RegionName)
    {
        string displayName = RegionManager.GetDisplayName(RegionName);
        string owner = RegionManager.GetRegionOwner(RegionName);
        return new RegionInfo { Name = displayName, Owner = owner };
    }
    #endregion
}