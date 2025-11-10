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
    public static string CreateRegion(TSPlayer plr, int startX, int startY, int endX, int endY, string buildName, Building clip)
    {
        try
        {
            // 生成区域名称：建筑名_时间（格式：建筑名_yyyyMMddHHmmss）
            string RegionName = string.IsNullOrEmpty(buildName)
                ? $"{plr.Name}_{DateTime.Now:yyyyMMddHHmmss}"
                : $"{buildName}_{DateTime.Now:yyyyMMddHHmmss}";

            // 清理区域名称中的非法字符
            RegionName = ClearRegionName(RegionName);

            // 设置区域边界（稍微扩大一点范围确保完全覆盖建筑）
            int StartX = Math.Max(0, startX - 1);
            int StartY = Math.Max(0, startY - 1);
            int EndX = Math.Min(Main.maxTilesX - 1, endX + 1);
            int EndY = Math.Min(Main.maxTilesY - 1, endY + 1);

            // 计算区域的宽度和高度
            int Width = EndX - StartX + 1;
            int Height = EndY - StartY + 1;

            // 检查区域是否已存在
            var ExistingRegion = TShock.Regions.GetRegionByName(RegionName);
            if (ExistingRegion != null)
            {
                // 如果区域已存在，添加时间戳确保唯一性
                RegionName = $"{RegionName}_{GetUnixTimestamp}";
                RegionName = ClearRegionName(RegionName);
            }

            // 使用TShock.Regions.AddRegion方法创建区域
            var region = TShock.Regions.AddRegion(StartX,StartY,Width,Height,RegionName,plr.Name,Main.worldID.ToString());

            if (region)
            {
                if (Config is not null)
                {
                    if (Config.AllowGroup is not null && Config.AllowGroup.Count > 0)
                    {
                        foreach (var group in Config.AllowGroup)
                        {
                            TShock.Regions.AllowGroup(RegionName, group);
                        }
                    }

                    if (Config.AllowUser is not null && Config.AllowUser.Count > 0)
                    {
                        foreach (var user in Config.AllowUser)
                        {
                            TShock.Regions.AddNewUser(RegionName, user);
                        }
                    }
                }

                // 如果玩家已登录，也允许玩家自己
                if (plr.IsLoggedIn)
                {
                    TShock.Regions.AddNewUser(RegionName, plr.Name);
                }

                // 禁止建筑
                TShock.Regions.SetRegionState(RegionName, true);

                // 保存区域名称到建筑数据中
                clip.RegionName = RegionName;


                // 记录区域信息到日志
                TShock.Log.ConsoleInfo($"[复制建筑] 为建筑 '{buildName}'\n" +
                                       $"创建保护区域: {RegionName}");

                return RegionName;
            }
            else
            {
                plr.SendErrorMessage("创建保护区域失败！");
                return "";
            }
        }
        catch (Exception ex)
        {
            plr.SendErrorMessage($"创建保护区域时出错: {ex.Message}");
            TShock.Log.ConsoleError($"[复制建筑] 创建区域错误: {ex}");
            return "";
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
            char.IsDigit(r.Name[r.Name.Length - 1])).ToList();
    }
    #endregion

    #region 更新建筑保护区域方法（支持索引）
    public static void UpdateRegion(TSPlayer plr, string regionInput, string flag)
    {
        // 尝试解析输入是否为索引号
        Region region = ParseRegionInput(plr, regionInput);
        if (region == null) return;

        // 检查权限：玩家必须是管理员或者是该区域的拥有者
        bool hasPermission = plr.HasPermission(Config.IsAdamin) || plr.Name == region.Owner;
        if (!hasPermission)
        {
            plr.SendErrorMessage($"你没有权限修改区域 '{region.Name}'");
            plr.SendInfoMessage($"该区域的所有者是: {region.Owner}");
            return;
        }

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
            string GroupName = flag.Substring(1);
            bool isAdd = flag.StartsWith("+");

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
                else
                {
                    plr.SendErrorMessage($"添加组权限失败: {GroupName}");
                }
            }
            else
            {
                if (TShock.Regions.RemoveGroup(region.Name, GroupName))
                {
                    plr.SendSuccessMessage($"已从区域 '{region.Name}' 移除组权限: {GroupName}");
                }
                else
                {
                    plr.SendErrorMessage($"移除组权限失败: {GroupName}");
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

        // 检查权限：玩家必须是管理员或者是该区域的拥有者
        bool hasPermission = plr.HasPermission(Config.IsAdamin) || plr.Name == region.Owner;
        if (!hasPermission)
        {
            plr.SendErrorMessage($"你没有权限删除区域 '{region.Name}'");
            plr.SendInfoMessage($"该区域的所有者是: {region.Owner}");
            return;
        }

        if (TShock.Regions.DeleteRegion(region.Name))
        {
            plr.SendSuccessMessage($"已移除区域: {region.Name}");
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

    #region 检查区域是否已被保护（避免覆盖粘贴）
    public static bool IsAreaProtected(TSPlayer plr, int startX, int startY, int width, int height)
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

            foreach (var point in checkPoints)
            {
                var regions = TShock.Regions.InAreaRegion(point.X, point.Y);
                if (regions != null && regions.Any())
                {
                    // 检查是否是由本插件创建的保护区域
                    foreach (var region in regions)
                    {
                        if (region.Name.Contains("_") && char.IsDigit(region.Name[region.Name.Length - 1]))
                        {
                            plr.SendErrorMessage($"检测到保护区域: {region.Name}");
                            return true;
                        }
                    }
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[复制建筑] 检查区域保护状态时出错: {ex}");
            return false; // 出错时默认允许粘贴
        }
    }
    #endregion
}