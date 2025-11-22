using System.Collections.Concurrent;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using TShockAPI;
using TShockAPI.DB;
using static CreateSpawn.CreateSpawn;

namespace CreateSpawn;

public class AutoClearData
{
    [JsonProperty("自动清理开关", Order = 1)]
    public bool Enabled { get; set; } = true;
    [JsonProperty("不清理管理区", Order = 2)]
    public bool ExemptAdmin { get; set; } = true;
    [JsonProperty("顺便清理建筑", Order = 3)]
    public bool ClearBuild { get; set; } = true;
    [JsonProperty("检查间隔秒数", Order = 4)]
    public int CheckSec { get; set; } = 3600; // 默认1小时
    [JsonProperty("触发清理分钟", Order = 5)]
    public int ClearMins { get; set; } = 4320; // 3天 = 3 * 24 * 60 = 4320分钟
    [JsonProperty("每次批量处理", Order = 6)]
    public int MaxPerCheck { get; set; } = 10; // 每次检查最多处理10个区域
    [JsonProperty("每个处理秒数", Order = 7)]
    public TimeSpan MinHandleIntervalSpan { get; set; } = TimeSpan.FromSeconds(5);
    [JsonProperty("免清理玩家表", Order = 8)]
    public List<string> ExemptPlayers { get; set; } = new List<string>();
}

public class AutoClear
{
    private DateTime LastCheckTime = DateTime.Now;
    private DateTime LastHandleTime = DateTime.Now;
    private readonly ConcurrentQueue<Region> HandleQueue = new();
    private bool InHandle = false;

    #region 在游戏更新中检查清理条件
    public void CheckAutoClear()
    {
        if (Config?.AutoClear?.Enabled != true) return;

        // 检查访客记录功能是否开启
        if (Config?.VisitRecord?.Enabled != true)
        {
            // 只在第一次检测到时记录一次警告
            if (LastCheckTime == DateTime.Now)
            {
                TShock.Log.ConsoleWarn("[复制建筑] 自动清理已启用，但访客记录功能未开启，自动清理将无法正常工作！");
            }
            return;
        }

        DateTime now = DateTime.Now;
        TimeSpan LastCheck = now - LastCheckTime;

        // 检查是否到达检查间隔
        if (LastCheck.TotalSeconds < Config.AutoClear.CheckSec) return;

        LastCheckTime = now;

        // 如果队列为空，重新填充队列
        if (HandleQueue.IsEmpty && !InHandle)
        {
            RefillClearQueue();
        }

        // 检查是否可以处理下一个区域（至少5秒间隔）
        TimeSpan LastTime = now - LastHandleTime;
        if (LastTime >= Config?.AutoClear.MinHandleIntervalSpan &&
           !HandleQueue.IsEmpty && !InHandle)
        {
            ProcessNextRegion();
        }
    }
    #endregion

    #region 填充清理队列
    private void RefillClearQueue()
    {
        try
        {
            var regions = GetClearableRegions();
            if (regions.Count == 0) return;

            // 按最后访问时间排序，最早访问的优先清理
            var Regions = regions
                .OrderBy(r => GetLastVisitTime(r.Name))
                .Take(Config.AutoClear.MaxPerCheck)
                .ToList();

            foreach (var region in Regions)
            {
                HandleQueue.Enqueue(region);
            }
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[复制建筑] 填充清理队列时出错: {ex}");
        }
    }
    #endregion

    #region 处理下一个区域
    private void ProcessNextRegion()
    {
        if (HandleQueue.IsEmpty || InHandle) return;

        if (!HandleQueue.TryDequeue(out var region)) return;

        try
        {
            InHandle = true;

            DateTime LastVisitTime = GetLastVisitTime(region.Name);
            if (LastVisitTime == DateTime.MinValue)
            {
                InHandle = false;
                return;
            }

            var Since = (DateTime.Now - LastVisitTime).TotalMinutes;

            // 检查是否满足清理条件
            if (Since >= Config.AutoClear.ClearMins)
            {
                TShock.Log.ConsoleInfo($"[复制建筑] 区域 {region.Name} 满足清理条件，开始清理");
                RegionManager.RemoveRegion(TSPlayer.Server, region, true);
            }
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[复制建筑] 处理区域时出错: {ex}");
        }
        finally
        {
            InHandle = false;
            LastHandleTime = DateTime.Now;
        }
    }
    #endregion

    #region 立即执行完整自动清理的检查（用于手动命令）
    public static void CheckAllRegions(TSPlayer plr)
    {
        try
        {
            var regions = GetClearableRegions();
            if (regions.Count == 0)
            {
                plr?.SendInfoMessage("没有需要清理的区域");
                return;
            }

            plr?.SendInfoMessage($"开始检查 {regions.Count} 个区域的访问情况...");

            int Cleared = 0;
            int total = regions.Count;

            // 按最后访问时间排序，最早访问的优先清理
            var sortedRegions = regions
                .OrderBy(r => GetLastVisitTime(r.Name))
                .ToList();

            int Count = 0; // 添加计数器
            foreach (var region in sortedRegions)
            {
                Count++; // 递增计数器

                DateTime LastVisitTime = GetLastVisitTime(region.Name);
                if (LastVisitTime == DateTime.MinValue) continue;

                var Since = (DateTime.Now - LastVisitTime).TotalMinutes;

                // 检查是否满足清理条件
                if (Since >= Config.AutoClear.ClearMins)
                {
                    RegionManager.RemoveRegion(TSPlayer.Server, region, true);
                }
                else
                {
                    // 不满足条件的区域也记录一下
                    var minsLeft = Config.AutoClear.ClearMins - Since;
                    if (plr != null)
                    {
                        plr.SendMessage($"[未满足] {region.Name} - {LastVisitTime:MM-dd HH:mm} ({minsLeft:F0}分钟后)", Color.Gray);
                    }
                }

                // 进度反馈
                if (plr != null)
                {
                    int progress = (Count * 100) / total;
                    plr.SendMessage($"清理进度: {progress}% ({Count}/{total})", Color.Yellow);
                }
            }

            // 最终结果统计
            if (Cleared > 0)
            {
                plr?.SendSuccessMessage($"手动清理完成: 成功清理 {Cleared} 个区域");
            }
            else
            {
                plr?.SendInfoMessage("没有需要立即清理的区域");
            }
        }
        catch (Exception ex)
        {
            plr?.SendErrorMessage($"手动清理过程出错: {ex.Message}");
        }
    }
    #endregion

    #region 获取可清理的区域列表（过滤免清理区域）
    private static List<Region> GetClearableRegions()
    {
        var AllRegions = RegionManager.GetPluginRegions();
        var ClearTable = new List<Region>();

        foreach (var region in AllRegions)
        {
            if (!IsExempt(region))
            {
                // 确保每个区域都有有效的时间戳
                DateTime LastActivity = GetLastVisitTime(region.Name);
                if (LastActivity != DateTime.MinValue) // 检查是否有有效时间
                {
                    ClearTable.Add(region);
                }
                else
                {
                    TShock.Log.ConsoleWarn($"[复制建筑] 区域 {region.Name} 没有有效的时间戳，跳过清理检查");
                }
            }
        }

        return ClearTable;
    }
    #endregion

    #region 检查区域是否在免清理名单中
    public static bool IsExempt(Region region)
    {
        string owner = region.Owner;

        // 检查是否是服务器后台的区域“也就是出生点”
        if (region.Owner.Equals("Server", StringComparison.OrdinalIgnoreCase))
            return true;

        // 检查是否不清理管理员区域且区域所有者是管理员
        if (Config.AutoClear.ExemptAdmin &&
            RegionManager.IsAdminRegion(owner))
            return true;

        // 检查是否在免清理玩家名单中
        if (Config.AutoClear.ExemptPlayers.Contains(owner))
            return true;

        return false;
    }
    #endregion

    #region 获取区域最后访问时间
    public static DateTime GetLastVisitTime(string RegionName)
    {
        // 优先检查最后访客记录（通常是最新的）
        if (RegionTracker.LastVisitors.TryGetValue(RegionName, out var visitor))
            return visitor.VisitTime;

        // 如果没有最后访客记录，从访问统计中查找
        if (RegionTracker.RegionVisits.TryGetValue(RegionName, out var visits) && visits.Count > 0)
            return visits.Max(r => r.LastVisitTime);

        // 如果没有访客记录，使用区域创建时间作为备用
        return new DateTime(RegionManager.GetRegionCreationTime(RegionName));
    }
    #endregion

    #region 显示待清理区域统计信息
    public static void ShowStats(TSPlayer plr)
    {
        var regions = RegionManager.GetPluginRegions();
        var now = DateTime.Now;

        int WillClear = 0; // 待清理数量
        int exempt = 0;    // 免清理数量
        int noRecord = 0;  // 无记录数量

        plr.SendMessage("待清理区域统计:", 70, 140, 210);

        foreach (var region in regions)
        {
            // 检查是否在免清理名单中
            if (IsExempt(region))
            {
                exempt++;
                continue;
            }

            DateTime LastVisitTime = GetLastVisitTime(region.Name);
            if (LastVisitTime == DateTime.MinValue)
            {
                noRecord++;
                plr.SendMessage($"[无记录] {region.Name} - 没有访客记录", Color.Gray);
                continue;
            }

            var Since = (now - LastVisitTime).TotalMinutes;
            var MinsLeft = Config.AutoClear.ClearMins - Since;

            // 显示即将清理的区域
            if (MinsLeft <= 0)
            {
                plr.SendMessage($"[清理] {region.Name} - {LastVisitTime:MM-dd HH:mm} (超期 {Math.Abs(MinsLeft):F0}分)", Color.OrangeRed);
                WillClear++;
            }
            else if (MinsLeft <= 1440) // 24小时内的
            {
                plr.SendMessage($"[清理] {region.Name} - {LastVisitTime:MM-dd HH:mm} ({MinsLeft / 60:F1}时后)", 240, 250, 150);
            }
        }

        // 显示统计摘要
        plr.SendMessage($"【统计】 总:[c/D68ACA:{regions.Count}] 免清:[c/DF909A:{exempt}] 无记录:[c/AAAAAA:{noRecord}] 待清:[c/E5A894:{WillClear}]", 70, 210, 195);

        if (WillClear > 0)
        {
            plr.SendMessage($"[c/AD89D5:{WillClear}] 个区域待清理", 240, 150, 150);
        }
        else
        {
            plr.SendSuccessMessage("当前没有需要立即清理的区域");
        }
    }
    #endregion

    #region 添加玩家到免清理名单
    public static void AddExempt(TSPlayer plr, string PlayerName)
    {
        if (Config.AutoClear.ExemptPlayers.Contains(PlayerName))
        {
            plr.SendMessage($"玩家 [c/478ED3:{PlayerName}] 已在免清理名单中", 240, 250, 150);
            return;
        }

        Config.AutoClear.ExemptPlayers.Add(PlayerName);
        Config.Write();
        plr.SendMessage($"已添加玩家 [c/478ED3:{PlayerName}] 到免清理名单", 240, 250, 150);
    }
    #endregion

    #region 从免清理名单中移除玩家
    public static void RemoveExempt(TSPlayer plr, string PlayerName)
    {
        if (!Config.AutoClear.ExemptPlayers.Contains(PlayerName))
        {
            plr.SendMessage($"玩家 [c/478ED3:{PlayerName}] 不在免清理名单中", 240, 250, 150);
            return;
        }

        Config.AutoClear.ExemptPlayers.Remove(PlayerName);
        Config.Write();
        plr.SendMessage($"已从免清理名单中移除玩家 [c/478ED3:{PlayerName}]", 240, 250, 150);
    }
    #endregion

    #region 显示免清理玩家列表
    public static void ShowExemptList(TSPlayer plr)
    {
        if (Config.AutoClear.ExemptPlayers.Count == 0)
        {
            plr.SendInfoMessage("免清理玩家列表为空");
            return;
        }

        plr.SendInfoMessage($"免清理玩家列表 ({Config.AutoClear.ExemptPlayers.Count} 个):");
        for (int i = 0; i < Config.AutoClear.ExemptPlayers.Count; i++)
        {
            plr.SendMessage($"{i + 1}. {Config.AutoClear.ExemptPlayers[i]}", Color.LightBlue);
        }
    }
    #endregion

}
