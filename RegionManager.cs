using System.Text.RegularExpressions;
using Terraria;
using TShockAPI;
using TShockAPI.DB;
using static CreateSpawn.CreateSpawn;

namespace CreateSpawn;

internal class RegionManager
{
    #region 创建保护区域方法
    public static string CreateRegion(TSPlayer plr, int startX, int startY, int endX, int endY, string buildName, Building clip)
    {
        try
        {
            // 生成区域名称：建筑名_时间（格式：建筑名_yyyyMMddHHmmss）
            string regionName = string.IsNullOrEmpty(buildName)
                ? $"CB_{plr.Name}_{DateTime.Now:yyyyMMddHHmmss}"
                : $"{buildName}_{DateTime.Now:yyyyMMddHHmmss}";

            // 清理区域名称中的非法字符
            regionName = CleanRegionName(regionName);

            // 设置区域边界（稍微扩大一点范围确保完全覆盖建筑）
            int regionStartX = Math.Max(0, startX - 1);
            int regionStartY = Math.Max(0, startY - 1);
            int regionEndX = Math.Min(Main.maxTilesX - 1, endX + 1);
            int regionEndY = Math.Min(Main.maxTilesY - 1, endY + 1);

            // 计算区域的宽度和高度
            int regionWidth = regionEndX - regionStartX + 1;
            int regionHeight = regionEndY - regionStartY + 1;

            // 检查区域是否已存在
            var existingRegion = TShock.Regions.GetRegionByName(regionName);
            if (existingRegion != null)
            {
                // 如果区域已存在，添加时间戳确保唯一性
                regionName = $"{regionName}_{GetUnixTimestamp}";
                regionName = CleanRegionName(regionName);
            }

            // 使用TShock.Regions.AddRegion方法创建区域
            var region = TShock.Regions.AddRegion(
                regionStartX,
                regionStartY,
                regionWidth,
                regionHeight,
                regionName,
                plr.Account?.Name ?? plr.Name,
                Main.worldID.ToString()
            );

            if (region)
            {
                // 设置允许的组
                TShock.Regions.AllowGroup(regionName, "服主");
                TShock.Regions.AllowGroup(regionName, "GM");
                TShock.Regions.AllowGroup(regionName, "admin");
                TShock.Regions.AllowGroup(regionName, "owner");
                TShock.Regions.AllowGroup(regionName, "superadmin");

                // 如果玩家已登录，也允许玩家自己
                if (plr.IsLoggedIn && plr.Account != null)
                {
                    TShock.Regions.AddNewUser(regionName, plr.Account.Name);
                }

                // 禁止建筑
                TShock.Regions.SetRegionState(regionName, true);

                // 保存区域名称到建筑数据中
                clip.RegionName = regionName;


                // 记录区域信息到日志
                TShock.Log.ConsoleInfo($"[复制建筑] 为建筑 '{buildName}' 创建保护区域: {regionName}");

                return regionName;
            }
            else
            {
                plr.SendErrorMessage("创建保护区域失败！");
                return null;
            }
        }
        catch (Exception ex)
        {
            plr.SendErrorMessage($"创建保护区域时出错: {ex.Message}");
            TShock.Log.ConsoleError($"[复制建筑] 创建区域错误: {ex}");
            return null;
        }
    }
    #endregion

    #region 清理区域名称中的非法字符
    /// <summary>
    /// 清理区域名称中的非法字符
    /// </summary>
    private static string CleanRegionName(string name)
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
            // 查找所有包含下划线和时间戳格式的区域
            var regions = GetPluginRegions();

            int count = 0;
            foreach (var region in regions)
            {
                try
                {
                    if (TShock.Regions.DeleteRegion(region.Name))
                    {
                        count++;
                    }
                }
                catch (Exception ex)
                {
                    TShock.Log.ConsoleError($"[复制建筑] 删除区域 {region.Name} 时出错: {ex}");
                }
            }

            TShock.Log.ConsoleInfo($"[复制建筑] 已清理 {count} 个保护区域");
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
        return TShock.Regions.Regions
            .Where(r => r.Name.Contains("_") &&
                       (char.IsDigit(r.Name[r.Name.Length - 1]) ||
                        r.Name.StartsWith("CB_"))).ToList();
    }
    #endregion

    #region 更新建筑保护区域方法（支持索引）
    public static void UpdateRegion(TSPlayer plr, string regionInput, string flag)
    {
        // 尝试解析输入是否为索引号
        Region region = ParseRegionInput(plr, regionInput);
        if (region == null) return;

        // 处理保护状态设置 (0=允许建筑, 1=禁止建筑)
        if (flag == "0" || flag == "1")
        {
            bool disableBuild = flag == "1";
            if (TShock.Regions.SetRegionState(region.Name, disableBuild))
            {
                plr.SendSuccessMessage($"区域 '{region.Name}' 保护状态已设置为: {(disableBuild ? "禁止建筑" : "允许建筑")}");
            }
            else
            {
                plr.SendErrorMessage($"更新区域保护状态失败: {region.Name}");
            }
            return;
        }

        // 处理组权限 (+组名 添加, -组名 移除)
        if (flag.StartsWith("+") || flag.StartsWith("-"))
        {
            string groupName = flag.Substring(1);
            bool isAdd = flag.StartsWith("+");

            if (string.IsNullOrWhiteSpace(groupName))
            {
                plr.SendErrorMessage("组名不能为空!");
                return;
            }

            if (isAdd)
            {
                if (TShock.Regions.AllowGroup(region.Name, groupName))
                {
                    plr.SendSuccessMessage($"已为区域 '{region.Name}' 添加组权限: {groupName}");
                }
                else
                {
                    plr.SendErrorMessage($"添加组权限失败: {groupName}");
                }
            }
            else
            {
                if (TShock.Regions.RemoveGroup(region.Name, groupName))
                {
                    plr.SendSuccessMessage($"已从区域 '{region.Name}' 移除组权限: {groupName}");
                }
                else
                {
                    plr.SendErrorMessage($"移除组权限失败: {groupName}");
                }
            }
            return;
        }

        // 处理玩家权限 (添加或移除玩家)
        string playerName = flag;

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
            else
            {
                plr.SendErrorMessage($"移除玩家权限失败: {playerName}");
            }
        }
        else
        {
            // 添加玩家权限
            if (TShock.Regions.AddNewUser(region.Name, playerName))
            {
                plr.SendSuccessMessage($"已为区域 '{region.Name}' 添加玩家: {playerName}");
            }
            else
            {
                plr.SendErrorMessage($"添加玩家权限失败: {playerName}");
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

        if (TShock.Regions.DeleteRegion(region.Name))
        {
            plr.SendSuccessMessage($"已强制移除区域: {region.Name}");
        }
        else
        {
            plr.SendErrorMessage($"移除区域失败: {region.Name}");
        }
    }
    #endregion

    #region 解析区域输入（支持索引和名称）
    private static Region ParseRegionInput(TSPlayer plr, string input)
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
            plr.SendInfoMessage($"使用索引 {index} 对应的区域: {region.Name}");
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

    #region 更新备份栈中的区域名称
    public static void UpdateBackupRegionName(TSPlayer plr, string regionName)
    {
        try
        {
            var stack = Map.LoadBack(plr.Name);
            if (stack.Count > 0)
            {
                // 获取栈顶的建筑数据（最近的一次备份）
                var building = stack.Pop();
                // 更新区域名称
                building.RegionName = regionName;
                // 重新压入栈中
                stack.Push(building);
                // 保存更新后的栈
                Map.SaveBack(plr.Name, stack);
            }
        }
        catch (Exception ex)
        {
            plr.SendErrorMessage($"更新备份区域名称时出错: {ex.Message}");
            TShock.Log.ConsoleError($"[复制建筑] 更新备份区域名称错误: {ex}");
        }
    }
    #endregion

}