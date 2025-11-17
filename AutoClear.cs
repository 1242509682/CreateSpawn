using TShockAPI.DB;
using Microsoft.Xna.Framework;
using TShockAPI;
using Newtonsoft.Json;
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
    [JsonProperty("每次同时检查", Order = 6)]
    public int MaxPerCheck { get; set; } = 10; // 每次检查最多处理10个区域
    [JsonProperty("免清理玩家表", Order = 7)]
    public List<string> ExemptPlayers { get; set; } = new List<string>();
}

public class AutoClear
{
    private long LastCheckTimer = DateTime.Now.Ticks; // 上次检查时间戳
    private int CurrIndex = 0; // 当前检查的区域索引

    #region 在游戏更新中检查清理条件
    public void CheckAutoClear()
    {
        if (Config?.AutoClear?.Enabled != true) return;

        // 检查访客记录功能是否开启
        if (Config?.VisitRecord?.Enabled != true)
        {
            // 只在第一次检测到时记录一次警告，避免重复刷日志
            if (LastCheckTimer == DateTime.Now.Ticks)
            {
                TShock.Log.ConsoleWarn("[复制建筑] 自动清理已启用，但访客记录功能未开启，自动清理将无法正常工作！");
            }
            return;
        }

        long Now = DateTime.Now.Ticks;
        long Interval = Config.AutoClear.CheckSec * TimeSpan.TicksPerSecond;

        // 检查是否到达检查间隔
        if (Now - LastCheckTimer < Interval) return;

        LastCheckTimer = Now;

        // 分批处理区域，避免单次处理太多
        ProcessRegionsBatch();
    }
    #endregion

    #region 分批处理区域
    private void ProcessRegionsBatch()
    {
        try
        {
            // 获取所有可清理的区域
            var regions = GetClearableRegions();
            if (regions.Count == 0) return;

            int Handle = 0;
            int maxBatch = Math.Min(Config.AutoClear.MaxPerCheck, regions.Count - CurrIndex);

            // 处理当前批次
            for (int i = 0; i < maxBatch && CurrIndex < regions.Count; i++, CurrIndex++)
            {
                var region = regions[CurrIndex];

                // 快速检查最后访问时间
                long LastVisit = GetLastVisitTime(region.Name);
                if (LastVisit == 0) continue;

                var LastVisitTime = new DateTime(LastVisit);
                var minsSince = (DateTime.Now - LastVisitTime).TotalMinutes;

                // 检查是否满足清理条件
                if (minsSince >= Config.AutoClear.ClearMins)
                {
                    if (ClearRegion(region))
                    {
                        Handle++;
                    }
                }
            }

            // 如果已经处理完所有区域，重置索引
            if (CurrIndex >= regions.Count)
            {
                CurrIndex = 0;
            }

            // 记录处理结果
            if (Handle > 0)
            {
                TShock.Log.ConsoleInfo($"[复制建筑] 自动清理完成，处理了 {Handle} 个区域");
            }
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[复制建筑] 处理区域时出错: {ex}");
        }
    }
    #endregion

    #region 获取可清理的区域列表（过滤免清理区域）
    private static List<Region> GetClearableRegions()
    {
        var allRegions = RegionManager.GetPluginRegions();
        var clearable = new List<Region>();

        foreach (var region in allRegions)
        {
            if (!IsExempt(region))
            {
                // 确保每个区域都有有效的时间戳
                long lastActivity = GetLastVisitTime(region.Name);
                if (lastActivity > 0) // 现在所有区域都应该有有效时间戳
                {
                    clearable.Add(region);
                }
                else
                {
                    TShock.Log.ConsoleWarn($"[复制建筑] 区域 {region.Name} 没有有效的时间戳，跳过清理检查");
                }
            }
        }

        return clearable;
    }
    #endregion

    #region 检查区域是否在免清理名单中
    public static bool IsExempt(Region region)
    {
        try
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
        catch
        {
            return false; // 出错时默认不豁免
        }
    }
    #endregion

    #region 获取区域最后访问时间
    public static long GetLastVisitTime(string RegionName)
    {
        // 优先检查最后访客记录（通常是最新的）
        if (RegionTracker.LastVisitors.TryGetValue(RegionName, out var visitor))
            return visitor.VisitTime;

        // 如果没有最后访客记录，从访问统计中查找
        if (RegionTracker.RegionVisits.TryGetValue(RegionName, out var visits) && visits.Count > 0)
            return visits.Max(r => r.LastVisitTime);

        // 如果没有访客记录，使用区域创建时间作为备用
        return GetRegionCreationTime(RegionName);
    }
    #endregion

    #region 获取区域创建时间（避免无访客的建筑没记录）
    private static long GetRegionCreationTime(string RegionName)
    {
        try
        {
            // 从区域名称中提取时间戳
            // 格式: BuildName_yyyyMMddHHmmss 或 BuildName_yyyyMMddHHmmss_ticks
            string[] parts = RegionName.Split('_');

            if (parts.Length >= 2)
            {
                string timestampStr = parts[^1]; // 取最后一部分

                // 如果是 ticks 格式
                if (parts.Length >= 3 && long.TryParse(parts[^1], out long ticks))
                {
                    return ticks;
                }

                // 尝试解析标准时间戳格式 yyyyMMddHHmmss
                if (DateTime.TryParseExact(timestampStr, "yyyyMMddHHmmss",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out DateTime createTime))
                {
                    return createTime.Ticks;
                }
            }

            // 如果无法从名称解析，使用文件创建时间
            return GetRegionFileCreationTime(RegionName);
        }
        catch
        {
            // 如果所有方法都失败，使用当前时间（避免立即清理）
            return DateTime.Now.Ticks;
        }
    }
    #endregion

    #region 从区域文件获取创建时间（避免无访客的建筑没记录）
    private static long GetRegionFileCreationTime(string RegionName)
    {
        try
        {
            // 查找对应的操作记录文件
            var region = TShock.Regions.GetRegionByName(RegionName);
            if (region != null)
            {
                string owner = region.Owner;
                string operationFile = Path.Combine(Map.Paths, $"{owner}_bk.map");

                if (File.Exists(operationFile))
                {
                    var creationTime = File.GetCreationTime(operationFile);
                    return creationTime.Ticks;
                }
            }

            return DateTime.Now.Ticks;
        }
        catch
        {
            return DateTime.Now.Ticks;
        }
    }
    #endregion

    #region 立即执行完整自动清理的检查（用于手动命令）
    public static void CheckAllRegions()
    {
        try
        {
            var regions = GetClearableRegions();
            int cleared = 0;

            TShock.Log.ConsoleInfo($"[复制建筑] 开始立即检查 {regions.Count} 个区域的访问情况");

            foreach (var region in regions)
            {
                long lastVisit = GetLastVisitTime(region.Name);
                if (lastVisit == 0) continue;

                var lastVisitTime = new DateTime(lastVisit);
                var minsSince = (DateTime.Now - lastVisitTime).TotalMinutes;

                // 检查是否满足清理条件
                if (minsSince >= Config.AutoClear.ClearMins)
                {
                    TShock.Log.ConsoleInfo($"[复制建筑] 区域 {region.Name} 满足清理条件，开始清理");

                    if (ClearRegion(region))
                    {
                        cleared++;
                        TShock.Log.ConsoleInfo($"[复制建筑] 成功清理区域: {region.Name} (最后访问: {lastVisitTime:yyyy-MM-dd HH:mm})");
                    }
                }
            }

            if (cleared > 0)
            {
                TShock.Utils.Broadcast($"[复制建筑] 自动清理完成，共清理 {cleared} 个区域", 250, 240, 150);
            }
            else
            {
                TShock.Log.ConsoleInfo($"[复制建筑] 自动清理检查完成，没有需要清理的区域");
            }
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[复制建筑] 自动清理时出错: {ex}");
        }
    }
    #endregion

    #region 清理单个区域（包括还原建筑和删除区域）
    public static bool ClearRegion(Region region)
    {
        try
        {
            string regionName = region.Name;
            string ownerName = region.Owner;

            // 使用服务器玩家执行清理
            var server = TSPlayer.Server;

            if (TaskManager.IsPlayerTaskRunning(server))
            {
                TShock.Log.ConsoleWarn($"[复制建筑] 服务器正忙，跳过清理区域 {regionName}");
                return false;
            }

            // 1. 先还原建筑
            bool restored = RestoreBuilding(regionName, ownerName);

            // 2. 再删除区域
            bool deleted = TShock.Regions.DeleteRegion(regionName);

            // 3. 清理内存记录
            RegionTracker.RegionVisits.Remove(regionName);
            RegionTracker.LastVisitors.Remove(regionName);
            Map.DeleteTargetRecord(regionName); // 新增：删除文件记录

            return restored && deleted;
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[复制建筑] 清理区域 {region.Name} 时出错: {ex}");
            return false;
        }
    }
    #endregion

    #region 还原建筑到粘贴前的状态
    public static bool RestoreBuilding(string regionName, string ownerName)
    {
        try
        {
            // 检查配置是否要求还原建筑
            if (!Config.AutoClear.ClearBuild)
            {
                TShock.Log.ConsoleInfo($"[复制建筑] 配置为不还原建筑，跳过区域 {regionName}");
                return true;
            }

            // 查找该区域对应的操作记录
            var operation = FindOperation(regionName, ownerName);
            if (operation == null)
            {
                TShock.Log.ConsoleError($"[复制建筑] 未找到区域 {regionName} 的操作记录");
                return false;
            }

            // 使用服务器玩家身份，但传递操作记录来还原建筑
            var area = operation.Area;
            Back(TSPlayer.Server, area.X, area.Y,
                          area.X + area.Width - 1, area.Y + area.Height - 1, operation);

            TShock.Log.ConsoleInfo($"[复制建筑] 已还原区域 {regionName} 的建筑");
            return true;
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[复制建筑] 还原区域 {regionName} 的建筑时出错: {ex}");
            return false;
        }
    }
    #endregion

    #region 查找区域对应的操作记录
    public static BuildOperation FindOperation(string regionName, string ownerName)
    {
        try
        {
            var operations = Map.LoadOperations(ownerName);
            if (operations == null || operations.Count == 0)
            {
                TShock.Log.ConsoleError($"[复制建筑] 玩家 {ownerName} 没有操作记录");
                return null;
            }

            // 由于操作记录是栈，我们需要转换为列表来查找特定区域
            var tempList = new List<BuildOperation>();
            BuildOperation found = null;

            // 临时弹出所有操作记录来查找
            while (operations.Count > 0)
            {
                var op = operations.Pop();
                tempList.Add(op);
                if (op.CreatedRegion == regionName)
                {
                    found = op;
                    break;
                }
            }

            // 重新压回所有操作记录（除了找到的那个）
            foreach (var op in tempList)
            {
                if (op != found)
                    operations.Push(op);
            }

            if (found != null)
            {
                TShock.Log.ConsoleInfo($"[复制建筑] 找到区域 {regionName} 的操作记录");
                return found;
            }
            else
            {
                TShock.Log.ConsoleError($"[复制建筑] 在玩家 {ownerName} 的操作记录中未找到区域 {regionName}");
                return null;
            }
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[复制建筑] 查找区域 {regionName} 的操作记录时出错: {ex}");
            return null;
        }
    }
    #endregion

    #region 显示待清理区域统计信息
    public static void ShowStats(TSPlayer plr)
    {
        var regions = RegionManager.GetPluginRegions();
        var now = DateTime.Now;
        int willClear = 0;
        int exempt = 0;
        int noRecord = 0;

        plr.SendMessage("待清理区域统计:", 70, 140, 210);

        foreach (var region in regions)
        {
            // 检查是否在免清理名单中
            if (IsExempt(region))
            {
                exempt++;
                continue;
            }

            long lastVisit = GetLastVisitTime(region.Name);
            if (lastVisit == 0)
            {
                noRecord++;
                plr.SendMessage($"[无记录] {region.Name} - 没有访客记录", Color.Gray);
                continue;
            }

            var lastVisitTime = new DateTime(lastVisit);
            var minsSince = (now - lastVisitTime).TotalMinutes;
            var minsLeft = Config.AutoClear.ClearMins - minsSince;

            // 显示即将清理的区域
            if (minsLeft <= 0)
            {
                plr.SendMessage($"[清理] {region.Name} - {lastVisitTime:MM-dd HH:mm} (超期 {Math.Abs(minsLeft):F0}分)", Color.OrangeRed);
                willClear++;
            }
            else if (minsLeft <= 1440) // 24小时内的
            {
                plr.SendMessage($"[清理] {region.Name} - {lastVisitTime:MM-dd HH:mm} ({minsLeft / 60:F1}时后)", 240, 250, 150);
            }
        }

        // 显示统计摘要
        plr.SendMessage($"【统计】 总:[c/D68ACA:{regions.Count}] 免:[c/DF909A:{exempt}] 无记录:[c/AAAAAA:{noRecord}] 清:[c/E5A894:{willClear}]", 70, 210, 195);

        if (willClear > 0)
        {
            plr.SendMessage($"[c/AD89D5:{willClear}] 个区域待清理",240,150,150);
        }
        else
        {
            plr.SendSuccessMessage("当前没有需要立即清理的区域");
        }
    }
    #endregion

    #region 添加玩家到免清理名单
    public static void AddExempt(TSPlayer plr, string playerName)
    {
        if (Config.AutoClear.ExemptPlayers.Contains(playerName))
        {
            plr.SendMessage($"玩家 [c/:478ED3{playerName}] 已在免清理名单中", 240, 250, 150);
            return;
        }

        Config.AutoClear.ExemptPlayers.Add(playerName);
        Config.Write();
        plr.SendMessage($"已添加玩家 [c/:478ED3{playerName}] 到免清理名单", 240, 250, 150);
    }
    #endregion

    #region 从免清理名单中移除玩家
    public static void RemoveExempt(TSPlayer plr, string playerName)
    {
        if (!Config.AutoClear.ExemptPlayers.Contains(playerName))
        {
            plr.SendMessage($"玩家 [c/:478ED3{playerName}] 不在免清理名单中",240,250,150);
            return;
        }

        Config.AutoClear.ExemptPlayers.Remove(playerName);
        Config.Write();
        plr.SendMessage($"已从免清理名单中移除玩家 [c/:478ED3{playerName}]", 240, 250, 150);
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

    #region 删除区域并同时还原建筑
    public static void DeleteWithBuilding(TSPlayer plr, string Input)
    {
        var region = RegionManager.ParseRegionInput(plr, Input);
        if (region == null) return;

        // 检查权限：玩家必须是管理员或者是该区域的拥有者
        if (!RegionManager.HasRegionPermission(plr, region.Name))
        {
            plr.SendErrorMessage($"你没有权限删除区域 '{region.Name}'");
            plr.SendInfoMessage($"该区域的所有者是: {region.Owner}");
            return;
        }

        string regionName = region.Name;
        string ownerName = region.Owner;

        // 1. 先还原建筑
        bool restored = RestoreForDelete(plr, regionName, ownerName);

        // 2. 再删除区域
        bool deleted = TShock.Regions.DeleteRegion(regionName);

        if (deleted)
        {
            // 3. 清理内存记录
            RegionTracker.RegionVisits.Remove(regionName);
            RegionTracker.LastVisitors.Remove(regionName);
            Map.DeleteTargetRecord(regionName); // 删除访问记录文件

            plr.SendSuccessMessage($"已移除区域: {region.Name}" + (restored ? " 并还原了建筑" : " (但建筑还原失败)"));
        }
        else
        {
            plr.SendErrorMessage($"移除区域失败: {region.Name}");
        }
    }
    #endregion

    #region 为删除命令单独写的建筑还原方法
    public static bool RestoreForDelete(TSPlayer plr, string RegionName, string owner)
    {
        try
        {
            // 查找操作记录
            var operation = FindOperation(RegionName, owner);
            if (operation == null)
            {
                plr.SendErrorMessage($"未找到区域 {RegionName} 的操作记录，无法还原建筑");
                return false;
            }

            // 使用服务器玩家身份，但传递操作记录
            var area = operation.Area;
            Back(TSPlayer.Server, area.X, area.Y,
                            area.X + area.Width - 1, area.Y + area.Height - 1, operation);

            plr.SendSuccessMessage($"已还原区域 {RegionName} 的建筑");
            return true;
        }
        catch (Exception ex)
        {
            plr.SendErrorMessage($"还原区域 {RegionName} 的建筑时出错: {ex.Message}");
            TShock.Log.ConsoleError($"[复制建筑] 还原区域 {RegionName} 的建筑时出错: {ex}");
            return false;
        }
    }
    #endregion 
}
