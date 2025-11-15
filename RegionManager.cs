using System.Text.RegularExpressions;
using Terraria;
using TShockAPI;
using TShockAPI.DB;
using Microsoft.Xna.Framework;
using static CreateSpawn.CreateSpawn;

namespace CreateSpawn;

internal class RegionManager
{
    #region 创建保护区域方法
    public static string CreateRegion(TSPlayer plr, int startX, int startY, int endX, int endY, string BuildName, Building clip)
    {
        try
        {
            // 生成唯一区域名称
            string BaseName = string.IsNullOrEmpty(BuildName) ? plr.Name : BuildName;
            string RegionName = $"{ClearRegionName(BaseName)}_{DateTime.Now:yyyyMMddHHmmss}";

            // 确保区域名称唯一
            if (TShock.Regions.GetRegionByName(RegionName) != null)
            {
                RegionName = $"{RegionName}_{DateTime.Now.Ticks}";
            }

            // 设置边界（稍微扩大范围）
            int StartX = Math.Max(0, startX - 1);
            int StartY = Math.Max(0, startY - 1);
            int EndX = Math.Min(Main.maxTilesX - 1, endX + 1);
            int EndY = Math.Min(Main.maxTilesY - 1, endY + 1);

            // 创建区域
            if (TShock.Regions.AddRegion(StartX, StartY,
                EndX - StartX + 1, EndY - StartY + 1,
                RegionName, plr.Name, Main.worldID.ToString()))
            {
                SetNewRegionState(RegionName);
                clip.RegionName = RegionName;

                // 初始化访客记录 - 创建者作为第一个访客
                SetDefaultVisitRecord(RegionName, plr.Name);

                TShock.Log.ConsoleInfo($"[复制建筑] 为建筑 '{BuildName}' 创建保护区域: {RegionName}");
                return RegionName;
            }

            plr.SendErrorMessage("创建保护区域失败！");
            return "";
        }
        catch (Exception ex)
        {
            plr.SendErrorMessage($"创建保护区域时出错: {ex.Message}");
            TShock.Log.ConsoleError($"[复制建筑] 创建区域错误: {ex}");
            return "";
        }
    }

    // 设置新区域的权限和状态
    private static void SetNewRegionState(string RegionName)
    {
        // 配置组权限s
        Config?.AllowGroup?.ForEach(group => TShock.Regions.AllowGroup(RegionName, group));

        // 配置用户权限
        Config?.AllowUser?.ForEach(user => TShock.Regions.AddNewUser(RegionName, user));

        // 默认禁止建筑
        TShock.Regions.SetRegionState(RegionName, true);
    }
    #endregion

    #region 初始化访客记录
    private static void SetDefaultVisitRecord(string RegionName, string creatorName)
    {
        try
        {
            if (!RegionTracker.RegionVisits.ContainsKey(RegionName))
            {
                RegionTracker.RegionVisits[RegionName] = new List<RegionVisitRecord>();
            }

            // 添加创建者作为第一个访客
            var visits = RegionTracker.RegionVisits[RegionName];
            var existingRecord = visits.FirstOrDefault(r => r.PlayerName == creatorName);

            if (existingRecord == null)
            {
                visits.Add(new RegionVisitRecord
                {
                    PlayerName = creatorName,
                    VisitCount = 1,
                    LastVisitTime = DateTime.Now.Ticks
                });
            }

            // 设置最后访客
            RegionTracker.LastVisitors[RegionName] = new LastVisitorRecord
            {
                PlayerName = creatorName,
                VisitTime = DateTime.Now.Ticks
            };

            // 立即保存记录
            if (Config?.VisitRecord?.SaveVisitData == true)
            {
                Map.SaveAllRecords();
            }
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[复制建筑] 初始化访客记录失败: {ex}");
        }
    }
    #endregion

    #region 清理区域名称中的非法字符
    private static string ClearRegionName(string name)
    {
        // 只允许字母、数字、下划线
        // 替换空格和特殊字符为下划线
        return Regex.Replace(name, @"[^\w]", "_");
    }
    #endregion

    #region 清理所有由本插件创建的区域（用于zip指令）
    public static void ClearAllRegions()
    {
        try
        {
            var regions = GetPluginRegions();
            int count = regions.Count(region => TShock.Regions.DeleteRegion(region.Name));

            // 清理访问记录
            if (Config.ClearAllVisit)
            {
                Map.ClearAllRecords();
            }

            TShock.Utils.Broadcast($"[复制建筑] 已清理 {count} 个保护区域", 250, 240, 150);
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[复制建筑] 清理区域时出错: {ex}");
        }
    }
    #endregion

    #region 获取由本插件创建的所有区域
    public static List<Region> GetPluginRegions()
    {
        return TShock.Regions.Regions.Where(r => IsPluginRegion(r.Name)).ToList();
    }
    #endregion

    #region 更新建筑保护区域方法（支持索引）
    public static void UpdateRegion(TSPlayer plr, string Input, string action)
    {
        // 尝试解析输入是否为索引号
        Region region = ParseRegionInput(plr, Input);
        if (region == null) return;

        // 检查权限：玩家必须是管理员或者是该区域的拥有者
        if (!HasRegionPermission(plr, region.Name))
        {
            plr.SendErrorMessage($"你没有权限修改区域 '{region.Name}'");
            plr.SendInfoMessage($"该区域的所有者是: {region.Owner}");
            return;
        }

        // 处理保护状态设置 (0=允许建筑, 1=禁止建筑)
        if (action == "0" || action == "1")
        {
            bool disableBuild = action == "1";
            if (TShock.Regions.SetRegionState(region.Name, disableBuild))
            {
                plr.SendSuccessMessage($"区域 '{region.Name}' 保护状态已设置为: {(disableBuild ? "禁止建筑" : "允许建筑")}");
            }
            return;
        }

        // 处理组权限 (+组名 添加, -组名 移除)
        if (action.StartsWith("+") || action.StartsWith("-"))
        {
            string GroupName = action.Substring(1);
            bool isAdd = action.StartsWith("+");

            if (string.IsNullOrWhiteSpace(GroupName))
            {
                plr.SendErrorMessage("组名不能为空!");
                return;
            }

            if (isAdd)
            {
                if (TShock.Regions.AllowGroup(region.Name, GroupName))
                {
                    plr.SendSuccessMessage($"已为区域 '{region.Name}' 添加组权限: {GroupName}");
                }
            }
            else
            {
                if (TShock.Regions.RemoveGroup(region.Name, GroupName))
                {
                    plr.SendSuccessMessage($"已从区域 '{region.Name}' 移除组权限: {GroupName}");
                }
            }
            return;
        }

        // 处理玩家权限 (添加或移除玩家)
        string playerName = action;

        // 检查玩家是否存在
        var userAccount = TShock.UserAccounts.GetUserAccountByName(playerName);
        if (userAccount == null)
        {
            plr.SendErrorMessage($"玩家 '{playerName}' 不存在!");
            return;
        }

        // 检查玩家是否已经在允许列表中
        bool isAllowed = region.AllowedIDs.Contains(userAccount.ID);

        if (isAllowed)
        {
            // 移除玩家权限
            if (TShock.Regions.RemoveUser(region.Name, playerName))
            {
                plr.SendSuccessMessage($"已从区域 '{region.Name}' 移除玩家: {playerName}");
            }
        }
        else
        {
            // 添加玩家权限
            if (TShock.Regions.AddNewUser(region.Name, playerName))
            {
                plr.SendSuccessMessage($"已为区域 '{region.Name}' 添加玩家: {playerName}");
            }
        }
    }
    #endregion

    #region 删除区域方法（支持索引）
    public static void DeleteRegion(TSPlayer plr, string regionInput)
    {
        // 尝试解析输入是否为索引号
        Region region = ParseRegionInput(plr, regionInput);
        if (region == null) return;

        // 检查权限：玩家必须是管理员或者是该区域的拥有者
        if (!HasRegionPermission(plr, region.Name))
        {
            plr.SendErrorMessage($"你没有权限删除区域 '{region.Name}'");
            plr.SendInfoMessage($"该区域的所有者是: {region.Owner}");
            return;
        }

        if (TShock.Regions.DeleteRegion(region.Name))
        {
            plr.SendSuccessMessage($"已移除区域: {region.Name}");
        }
    }
    #endregion

    #region 解析区域输入（支持索引和名称）
    public static Region ParseRegionInput(TSPlayer plr, string input)
    {
        // 如果是数字，按索引处理
        if (int.TryParse(input, out int index))
        {
            var regions = GetPluginRegions();

            if (index < 1 || index > regions.Count)
            {
                plr.SendErrorMessage($"索引 {index} 无效，可用范围: 1-{regions.Count}");
                return null;
            }

            var region = regions[index - 1];
            return region;
        }

        // 否则按区域名称处理
        var regionByName = TShock.Regions.GetRegionByName(input);
        if (regionByName == null)
        {
            plr.SendErrorMessage($"未找到区域: {input}");
            return null;
        }

        return regionByName;
    }
    #endregion

    #region 根据区域名称获取索引号
    public static int GetRegionIndex(string regionName)
    {
        try
        {
            var regions = GetPluginRegions();

            for (int i = 0; i < regions.Count; i++)
            {
                if (regions[i].Name.Equals(regionName, StringComparison.OrdinalIgnoreCase))
                {
                    return i + 1; // 返回从1开始的索引
                }
            }

            return -1; // 未找到
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[复制建筑] 获取区域索引时出错: {ex}");
            return -1;
        }
    }
    #endregion

    #region 根据位置点获取当前由插件创建的区域
    public static string? GetCurrRegion(int tileX, int tileY)
    {
        var regions = TShock.Regions.InAreaRegion(tileX, tileY);
        if (regions == null || regions.Count() == 0) return null;

        // 使用LINQ查找插件区域
        return regions.FirstOrDefault(r => IsPluginRegion(r.Name))?.Name ?? null;
    }
    #endregion

    #region 判断是否为插件区域
    public static bool IsPluginRegion(string RegionName)
    {
        return RegionName.Contains("_") &&
               RegionName.Length > 1 &&
               char.IsDigit(RegionName[^1]);
    }
    #endregion

    #region 根据区域原名 获取移除时间戳的名称
    public static string GetDisplayName(string RegionName)
    {
        int LastUnderscore = RegionName.LastIndexOf('_');
        return LastUnderscore > 0 ? RegionName[..LastUnderscore] : RegionName;
    }
    #endregion

    #region 检查区域是否已被保护（避免覆盖粘贴）
    public static bool IsAreaProtected(int startX, int startY, int width, int height, ref string Name)
    {
        try
        {
            int endX = startX + width - 1;
            int endY = startY + height - 1;

            // 检查四个角和中点是否在保护区域内
            var checkPoints = new[]
            {
                new Point(startX, startY),           // 左上角
                new Point(endX, startY),             // 右上角
                new Point(startX, endY),             // 左下角
                new Point(endX, endY),               // 右下角
                new Point(startX + width / 2, startY + height / 2) // 中心点
            };

            Name = checkPoints
            .SelectMany(point => TShock.Regions.InAreaRegion(point.X, point.Y) ?? Array.Empty<Region>())
            .FirstOrDefault(region => IsPluginRegion(region.Name))?.Name!;

            return Name != null;
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[复制建筑] 检查区域保护状态时出错: {ex}");
            return false; // 出错时默认允许粘贴
        }
    }
    #endregion

    #region 判断玩家是否在区域
    public static bool InRegion(TSPlayer plr, string RegionName)
    {
        return plr != null &&
               plr.Active &&
               plr.CurrentRegion != null &&
               plr.CurrentRegion.Name == RegionName;
    }
    #endregion

    #region 权限检查方法
    public static bool HasRegionPermission(TSPlayer plr, string RegionName)
    {
        return plr.HasPermission(Config.IsAdamin) ||
               plr.Name == GetRegionOwner(RegionName);
    }
    #endregion

    #region 根据区域名称获取区域所有者
    public static string GetRegionOwner(string regionName)
    {
        var region = TShock.Regions.GetRegionByName(regionName);
        return region?.Owner ?? "未知";
    }
    #endregion

    #region 检查是否为管理员区域
    public static bool IsAdminRegion(string owner)
    {
        var account = TShock.UserAccounts.GetUserAccountByName(owner);
        if (account == null) return false;

        var group = TShock.Groups.GetGroupByName(account.Group);
        return group != null && group.HasPermission(Config.IsAdamin);
    }
    #endregion
}