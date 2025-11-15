using System.Text;
using Microsoft.Xna.Framework;
using TShockAPI;
using static CreateSpawn.CreateSpawn;
using static CreateSpawn.Utils;
using static CreateSpawn.Condition;

namespace CreateSpawn;

internal class Commands
{
    #region 主指令方法
    internal static async void CMDAsync(CommandArgs args)
    {
        var plr = args.Player;
        var random = new Random();
        Color color = RandomColors(random);

        //子命令数量为0时
        if (args.Parameters.Count == 0)
        {
            HelpCmd(plr);
        }

        //子命令数量超过1个时
        if (args.Parameters.Count >= 1)
        {
            switch (args.Parameters[0].ToLower())
            {
                case "on":
                case "开启":
                    {
                        if (!plr.HasPermission(Config.IsAdamin)) return;

                        Config.SpawnEnabled = true;
                        Config.Write();
                        plr.SendMessage($"已开启出生点生成功能,请重启服务器", 240, 250, 150);
                        plr.SendInfoMessage($"或在控制台使用:/cb sp 出生点", 240, 250, 150);
                    }
                    break;

                case "off":
                case "关闭":
                    {
                        if (!plr.HasPermission(Config.IsAdamin)) return;

                        Config.SpawnEnabled = false;
                        Config.Write();
                        plr.SendMessage($"已关闭出生点生成功能", 240, 250, 150);
                    }
                    break;

                case "s":
                case "set":
                case "设置":
                    if (args.Parameters.Count < 2)
                    {
                        plr.SendInfoMessage($"正确指令：/cb set <1/2> --选择复制的区域");
                        break;
                    }

                    switch (args.Parameters[1].ToLower())
                    {
                        case "1":
                            plr.AwaitingTempPoint = 1;
                            plr.SendInfoMessage("请选择复制区域的左上角");
                            break;
                        case "2":
                            plr.AwaitingTempPoint = 2;
                            plr.SendInfoMessage("请选择复制区域的右下角");
                            break;
                        default:
                            plr.SendInfoMessage($"正确指令：/cb set <1/2> --选择复制的区域");
                            plr.SendInfoMessage("/cb save");
                            break;
                    }
                    break;

                case "l":
                case "ls":
                case "list":
                case "列表":
                    {
                        var clipNames = Map.GetAllClipNames();

                        if (clipNames.Count == 0)
                        {
                            plr.SendErrorMessage("当前[c/64E0DA:没有可用]的复制建筑。");
                            plr.SendInfoMessage($"如何复制自己的建筑:");
                            plr.SendInfoMessage($"使用选区指令:[c/64A1E0:/cb s]");
                            plr.SendInfoMessage($"使用复制指令:[c/F56C77:/cb add] 或 [c/63E0D8:/cb sv 建筑名]");
                        }
                        else
                        {
                            plr.SendMessage($"\n当前可用的复制建筑 [c/64E0DA:{clipNames.Count}个]:", color);

                            // 加上索引编号（从 1 开始）
                            for (int i = 0; i < clipNames.Count; i++)
                            {
                                string buildingName = clipNames[i];
                                var building = Map.LoadClip(buildingName);
                                string msg = $"[c/D0AFEB:{i + 1}.] [c/FFFFFF:{buildingName}]";

                                // 显示进度条件
                                if (building.Conditions != null && building.Conditions.Count > 0)
                                {
                                    msg += $"\n[c/FFA500:条件: {string.Join(", ", building.Conditions)}]";
                                }

                                plr.SendMessage(msg, Color.AntiqueWhite);
                            }

                            plr.SendMessage($"可使用指定粘贴指令:[c/D0AFEB:/cb pt 名字]", color);
                            plr.SendMessage($"或使用索引号:[c/D0AFEB:/cb pt 索引号]", color);
                        }
                    }
                    break;

                case "cd":
                case "cond":
                case "condition":
                case "条件":
                case "进度":
                    {
                        if (args.Parameters.Count >= 2 && args.Parameters[1].ToLower() == "help")
                        {
                            ShowConditionHelp(plr);
                            break;
                        }

                        int page = 1;
                        if (args.Parameters.Count >= 2)
                        {
                            if (!int.TryParse(args.Parameters[1], out page) || page < 1)
                            {
                                plr.SendErrorMessage("页码必须是正整数！");
                                return;
                            }
                        }

                        ShowConditions(plr, page);
                    }
                    break;

                case "add":
                case "sv":
                case "save":
                case "copy":
                case "复制":
                    {
                        if (NeedInGame()) return;
                        if (plr.TempPoints[0].X == 0 || plr.TempPoints[1].X == 0)
                        {
                            plr.SendInfoMessage("您还没有选择区域！");
                            plr.SendMessage("使用方法: /cb s 1 选择左上角", color);
                            plr.SendMessage("使用方法: /cb s 2 选择右下角", color);
                            return;
                        }

                        string name = plr.Name; // 默认使用玩家自己的名字
                        List<string> conditions = new List<string>();

                        if (args.Parameters.Count >= 2 && !string.IsNullOrWhiteSpace(args.Parameters[1]))
                        {
                            name = args.Parameters[1]; // 使用指定的名字

                            // 检查是否包含条件参数
                            if (args.Parameters.Count > 2)
                            {
                                // 合并第2个参数之后的所有内容作为条件
                                string conditionStr = string.Join(" ", args.Parameters.Skip(2));

                                // 支持逗号分隔的多个条件
                                var conditionList = conditionStr.Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries);
                                foreach (var cond in conditionList)
                                {
                                    string trimmedCond = cond.Trim();
                                    if (!string.IsNullOrEmpty(trimmedCond))
                                    {
                                        conditions.Add(trimmedCond);
                                    }
                                }
                            }
                        }

                        // 新增：检查复制区域内是否有受保护建筑，并验证权限
                        if (!RegionManager.CanCopyFromArea(plr, plr.TempPoints[0].X, plr.TempPoints[0].Y,
                                            plr.TempPoints[1].X, plr.TempPoints[1].Y))
                        {
                            return; // 无法复制，方法内已发送错误信息
                        }

                        // 保存到剪贴板
                        var clip = CopyBuilding(
                            plr.TempPoints[0].X, plr.TempPoints[0].Y,
                            plr.TempPoints[1].X, plr.TempPoints[1].Y);

                        // 设置进度条件
                        clip.Conditions = conditions;

                        Map.SaveClip(name, clip);

                        if (conditions.Count > 0)
                        {
                            plr.SendSuccessMessage($"已复制区域 ({clip.Width}x{clip.Height}) [条件: {string.Join(", ", conditions)}]");
                        }
                        else
                        {
                            plr.SendSuccessMessage($"已复制区域 ({clip.Width}x{clip.Height})");
                        }

                        plr.SendInfoMessage($"粘贴指令:[c/64A1E0:/cb pt]");
                        plr.SendInfoMessage($"撤销指令:[c/64A1E0:/cb bk]");
                        plr.SendInfoMessage($"查建筑表:[c/64A1E0:/cb ls]");
                    }
                    break;

                case "pt":
                case "sp":
                case "spawn":
                case "create":
                case "paste":
                case "粘贴":
                    {
                        if (NeedWaitTask()) return;

                        string name = plr.Name; // 默认使用玩家自己的名字
                        string RegionName = name; // 用于区域创建的建筑名称

                        // 检查参数是否存在
                        if (args.Parameters.Count > 1)
                        {
                            string param = args.Parameters[1];
                            if (!string.IsNullOrWhiteSpace(param))
                            {
                                name = param;
                                RegionName = param;
                            }
                        }

                        // 尝试解析输入是否为索引号
                        Building clip;
                        if (int.TryParse(name, out int index))
                        {
                            // 按索引处理
                            var clipNames = Map.GetAllClipNames();

                            if (index < 1 || index > clipNames.Count)
                            {
                                plr.SendErrorMessage($"索引 {index} 无效，可用范围: 1-{clipNames.Count}");
                                plr.SendInfoMessage("使用 /cb list 查看建筑列表和索引号");
                                return;
                            }

                            string actualName = clipNames[index - 1];
                            clip = Map.LoadClip(actualName);
                            RegionName = actualName;
                        }
                        else
                        {
                            // 按名称处理
                            clip = Map.LoadClip(name);
                            RegionName = name; // 如果是名称，则区域建筑名称就是输入的名称
                        }

                        if (clip == null)
                        {
                            plr.SendErrorMessage($"未找到建筑: {name}");
                            plr.SendInfoMessage("复制指令:/cb save");
                            plr.SendInfoMessage("查建筑表:/cb list");
                            return;
                        }

                        // 新增：检查进度条件（管理员无视条件）
                        if (!plr.HasPermission(Config.IsAdamin) && clip.Conditions != null && clip.Conditions.Count > 0)
                        {
                            // 检查条件组中的所有条件是否都满足
                            if (!CheckGroup(plr.TPlayer, clip.Conditions))
                            {
                                plr.SendErrorMessage($"无法粘贴建筑 '{name}'，进度条件未满足！");
                                plr.SendInfoMessage($"所需条件: {string.Join(", ", clip.Conditions)}");
                                return;
                            }
                        }

                        int startX = 0;
                        int startY = 0;

                        // 用于获取已存在区域的索引号
                        string RegionName2 = "";
                        int Index = -1;
                        string Owner = "";

                        if (plr.RealPlayer) // 如果是真实玩家则当前位置为头顶
                        {
                            startX = plr.TileX - clip.Width / 2;
                            startY = plr.TileY - clip.Height;

                            // 检查玩家头顶区域是否已经有保护区域
                            if (RegionManager.IsAreaProtected(startX, startY, clip.Width, clip.Height, ref RegionName2))
                            {
                                if (!string.IsNullOrEmpty(RegionName2))
                                {
                                    Index = RegionManager.GetRegionIndex(RegionName2);
                                    Owner = RegionManager.GetRegionOwner(RegionName2);
                                }

                                plr.SendErrorMessage($"出生点已有保护区域 {RegionName2} 无法在此处粘贴建筑！");
                                plr.SendInfoMessage("请移动到没有保护区域的空地再进行粘贴。");

                                // 如果是管理员或区域所有者 则提示删除指令
                                if (plr.HasPermission(Config.IsAdamin) || plr.Name == Owner)
                                {
                                    plr.SendInfoMessage($"或使用/cb del {Index} 移除！");
                                }
                                return;
                            }
                        }
                        else if (plr == TSPlayer.Server) //如果是服务器 则使用出生点
                        {
                            startX = Terraria.Main.spawnTileX - Config.CentreX + Config.AdjustX;
                            startY = Terraria.Main.spawnTileY - Config.CountY + Config.AdjustY;

                            // 检查出生点有保护区域
                            if (RegionManager.IsAreaProtected(startX, startY, clip.Width, clip.Height, ref RegionName2))
                            {
                                if (!string.IsNullOrEmpty(RegionName2))
                                {
                                    Index = RegionManager.GetRegionIndex(RegionName2);
                                    Owner = RegionManager.GetRegionOwner(RegionName2);
                                }

                                TSPlayer.Server.SendErrorMessage($"出生点已有保护区域 {RegionName} 无法在此处粘贴建筑！");
                                TSPlayer.Server.SendInfoMessage($"可使用/cb del {Index} 移除！");
                                return;
                            }
                        }

                        await SpawnBuilding(plr, startX, startY, clip, RegionName);
                    }
                    break;

                case "bk":
                case "back":
                case "fix":
                case "还原":
                    {
                        if (NeedWaitTask()) return;
                        // 先获取操作记录
                        var operation = Map.PopOperation(plr.Name);
                        if (operation == null)
                        {
                            plr.SendErrorMessage("没有可撤销的操作");
                            return;
                        }

                        await AsyncBack(plr, plr.TempPoints[0].X, plr.TempPoints[0].Y,
                                       plr.TempPoints[1].X, plr.TempPoints[1].Y, operation);
                    }
                    break;

                case "r":
                case "region":
                    {
                        // 查找由本插件创建的区域（名称包含时间戳格式）
                        var Regions = RegionManager.GetPluginRegions();

                        if (Regions.Count == 0)
                        {
                            plr.SendInfoMessage("没有找到由复制建筑插件创建的区域。");
                            return;
                        }

                        plr.SendInfoMessage($"由复制建筑插件创建的区域 ({Regions.Count} 个):");
                        for (int i = 0; i < Regions.Count; i++)
                        {
                            var region = Regions[i];
                            plr.SendMessage($"{i + 1}. [c/15EDDB:{region.Name}]\n" +
                                $"所有者: [c/4EA4F2:{region.Owner}], 范围: [c/E74F5E:{region.Area.X}]," +
                                $"[c/E74F5E:{region.Area.Y}] 到 [c/E74F5E:{region.Area.X + region.Area.Width}]," +
                                $"[c/E74F5E:{region.Area.Y + region.Area.Height}]", 240, 250, 150);
                        }

                        if (plr.HasPermission(Config.IsAdamin))
                            plr.SendInfoMessage($"可以使用索引号代替完整区域名，例如: /cb del 1 或 /cb up 1 0");

                        if (Config.ShowArea is not null && Config.ShowArea.Enabled)
                        {
                            foreach (var Region in Regions)
                            {
                                if (RegionManager.InRegion(plr, Region.Name))
                                {
                                    // 切换跑马灯效果
                                    if (MyProjectile.ProjectilesInfo.ContainsKey(plr.Index))
                                    {
                                        MyProjectile.Stop(plr.Index);
                                        plr.SendInfoMessage("已停止区域边界效果。");
                                    }
                                    else
                                    {
                                        MyProjectile.ProjectilesInfo.Add(plr.Index, new ProjectileManager
                                        {
                                            RegionName = Region.Name,
                                            Area = Region.Area,
                                            StopTimer = 0,
                                            Position = 0,
                                            UpdateCount = 0,
                                            Projectiles = new List<int>()
                                        });

                                        plr.SendInfoMessage("已启动区域边界效果。");
                                    }
                                    break;
                                }
                            }
                        }
                    }
                    break;

                case "rd":
                case "record":
                case "访客":
                    {
                        if (args.Parameters.Count < 2)
                        {
                            plr.SendErrorMessage("用法: /cb rd <索引/区域名称>");
                            plr.SendErrorMessage("使用 /cb r 查看区域列表和索引号");
                            return;
                        }

                        string Input = args.Parameters[1];

                        var Region = RegionManager.ParseRegionInput(plr, Input);
                        if (Region == null) return;

                        if (!RegionManager.HasRegionPermission(plr, Region.Name))
                        {
                            plr.SendErrorMessage($"你没有权限查看区域 '{Region.Name}' 的访客记录");
                            plr.SendInfoMessage($"该区域的所有者是: {Region.Owner}");
                            return;
                        }

                        // 直接调用 RegionTracker 的方法
                        CreateSpawn.RegionTracker.ShowRegionVisitRecords(plr, Region.Name);
                    }
                    break;

                case "del":
                    {
                        if (args.Parameters.Count < 2)
                        {
                            plr.SendErrorMessage("用法: /cb del <索引/区域名称>");
                            plr.SendErrorMessage("使用 /cb r 查看区域列表和索引号");
                            return;
                        }

                        string Input = args.Parameters[1];
                        // 删除区域并同时还原建筑
                        await AutoClear.DeleteWithBuilding(plr, Input);
                    }
                    break;

                case "up":
                case "update":
                case "更新":
                    {
                        if (args.Parameters.Count < 3)
                        {
                            plr.SendErrorMessage("用法: /cb up <索引/区域名> <操作>");
                            plr.SendErrorMessage("操作: 0或1 ——允许或禁止建筑");
                            plr.SendErrorMessage("操作: 玩家名 ——添加或移除玩家");
                            plr.SendErrorMessage("操作: +组名 ——添加组权限");
                            plr.SendErrorMessage("操作: -组名 ——移除组权限");
                            plr.SendErrorMessage("使用 /cb r 查看区域列表和索引号");
                            return;
                        }

                        string Index = args.Parameters[1];
                        string action = args.Parameters[2];
                        RegionManager.UpdateRegion(plr, Index, action);
                    }
                    break;

                case "at":
                case "auto":
                case "自动清理":
                    {
                        if (!plr.HasPermission(Config.IsAdamin))
                        {
                            plr.SendErrorMessage("你没有权限执行自动清理操作");
                            return;
                        }

                        if (args.Parameters.Count >= 2)
                        {
                            switch (args.Parameters[1].ToLower())
                            {
                                case "on":
                                case "启用":
                                    Config.AutoClear.Enabled = true;
                                    Config.Write();
                                    plr.SendSuccessMessage("已启用自动清理功能");
                                    break;

                                case "off":
                                case "关闭":
                                    Config.AutoClear.Enabled = false;
                                    Config.Write();
                                    plr.SendSuccessMessage("已关闭自动清理功能");
                                    break;

                                case "now":
                                case "立即":
                                    // 立即执行清理检查
                                    _ = AutoClear.CheckAllRegions();
                                    plr.SendSuccessMessage("已立即执行区域清理检查");
                                    break;

                                case "ck":
                                case "st":
                                case "stats":
                                case "统计":
                                    // 显示待清理区域统计
                                    AutoClear.ShowStats(plr);
                                    break;

                                case "add":
                                case "添加":
                                    // 添加免清理玩家
                                    if (args.Parameters.Count >= 3)
                                    {
                                        AutoClear.AddExempt(plr, args.Parameters[2]);
                                    }
                                    else
                                    {
                                        plr.SendErrorMessage("用法: /cb auto add <玩家名>");
                                    }
                                    break;

                                case "del":
                                case "remove":
                                case "删除":
                                case "移除":
                                    // 移除免清理玩家
                                    if (args.Parameters.Count >= 3)
                                    {
                                        AutoClear.RemoveExempt(plr, args.Parameters[2]);
                                    }
                                    else
                                    {
                                        plr.SendErrorMessage("用法: /cb auto remove <玩家名>");
                                    }
                                    break;

                                case "l":
                                case "ls":
                                case "list":
                                case "列表":
                                    // 显示免清理玩家列表
                                    AutoClear.ShowExemptList(plr);
                                    break;

                                default:
                                    plr.SendInfoMessage("用法: /cb auto <on|off|now|stats|test|add|remove|list>");
                                    plr.SendInfoMessage("on/off - 启用/关闭自动清理");
                                    plr.SendInfoMessage("now - 立即执行清理");
                                    plr.SendInfoMessage("ck - 显示待清理区域统计");
                                    plr.SendInfoMessage("cs - 测试清理单个区域");
                                    plr.SendInfoMessage("add - 添加免清理玩家");
                                    plr.SendInfoMessage("del - 移除免清理玩家");
                                    plr.SendInfoMessage("ls - 显示免清理玩家列表");
                                    break;
                            }
                        }
                        else
                        {
                            var mess = new StringBuilder(); //用于存储指令内容
                            // 显示自动清理配置信息
                            mess.Append("自动清理配置:\n"+
                                $"启用: {Config.AutoClear.Enabled} \n" +
                                $"检查间隔: {Config.AutoClear.CheckSec}秒\n" +
                                $"清理阈值: {Config.AutoClear.ClearMins}分钟 ({Config.AutoClear.ClearMins / 1440:F1}天)\n" +
                                $"移除建筑: {Config.AutoClear.ClearBuild}\n" +
                                $"不清理管理员区域: {Config.AutoClear.ExemptAdmin}\n" +
                                $"免清理玩家数: {Config.AutoClear.ExemptPlayers.Count}\n" +
                                "/cb at on|off 切换自动清理开关\n" +
                                "/cb at ck 查看待清理区域\n" +
                                "/cb at now 立即执行检查\n" +
                                "/cb at add 玩家名 添加免清玩家\n" +
                                "/cb at del 玩家名 移除免清玩家\n" +
                                "/cb at ls 查看免清理玩家");

                            var Text = mess.ToString();
                            var lines = Text.Split('\n');
                            var GradMess = new StringBuilder();
                            var start = new Color(166, 213, 234);
                            var end = new Color(245, 247, 175);
                            for (int i = 0; i < lines.Length; i++)
                            {
                                if (!string.IsNullOrEmpty(lines[i]))
                                {
                                    float ratio = (float)i / (lines.Length - 1);
                                    var gradColor = Color.Lerp(start, end, ratio);

                                    // 将颜色转换为十六进制格式
                                    string colorHex = $"{gradColor.R:X2}{gradColor.G:X2}{gradColor.B:X2}";

                                    // 使用颜色标签包装每一行
                                    GradMess.AppendLine($"[c/{colorHex}:{lines[i]}]");
                                }
                            }

                            plr.SendMessage(GradMess.ToString(), 240, 250, 150);
                        }
                    }
                    break;

                case "zip":
                case "backup":
                    {
                        if (!plr.HasPermission(Config.IsAdamin)) return;
                        RegionManager.ClearAllRegions();  // 清理所有由本插件创建的区域
                        Map.BackupAndDeleteAllDataFiles();
                    }
                    break;

                default:
                    HelpCmd(plr);
                    break;
            }
        }

        bool NeedInGame() => TileHelper.NeedInGame(plr);
        bool NeedWaitTask() => TileHelper.NeedWaitTask(plr);
    }
    #endregion

    #region 显示条件列表的方法
    private static void ShowConditions(TSPlayer plr, int page)
    {
        int itemsPerPage = 10; // 每页显示10个条件组
        int totalGroups = ConditionGroups.Count;
        int totalPages = (int)Math.Ceiling(totalGroups / (double)itemsPerPage);

        if (page > totalPages)
        {
            plr.SendErrorMessage($"只有 {totalPages} 页可用！");
            return;
        }

        int startIndex = (page - 1) * itemsPerPage;
        int endIndex = Math.Min(startIndex + itemsPerPage, totalGroups);

        var groupsToShow = ConditionGroups.Skip(startIndex).Take(itemsPerPage);

        plr.SendMessage($"\n[c/47D3C3:═══ 进度条件列表 (第 {page}/{totalPages} 页) ═══]", Color.Cyan);
        plr.SendMessage($"[c/FFA500:共 {totalGroups} 个条件组，使用 /cb cd <页码> 翻页]", Color.Orange);

        int index = startIndex + 1;
        foreach (var group in groupsToShow)
        {
            string mainName = group.Key;
            var aliases = group.Value;

            // 构建显示字符串：主名称 + (别名1,别名2)
            string displayString = mainName;
            if (aliases.Count > 1)
            {
                // 排除主名称本身，只显示其他别名
                var otherNames = aliases.Where(a => a != mainName).ToList();
                if (otherNames.Count > 0)
                {
                    displayString += $" ({string.Join(", ", otherNames)})";
                }
            }

            plr.SendMessage($"[c/00FF00:{index}.] [c/FFFFFF:{displayString}]", Color.White);
            index++;
        }

        // 显示翻页提示
        if (totalPages > 1)
        {
            string pageInfo = $"[c/FFFF00:第 {page}/{totalPages} 页]";
            if (page < totalPages)
            {
                pageInfo += $"[c/00FFFF: - 输入 /cb cd {page + 1} 查看下一页]";
            }
            if (page > 1)
            {
                pageInfo += $"[c/00FFFF: - 输入 /cb cd {page - 1} 查看上一页]";
            }
            plr.SendMessage(pageInfo, Color.Yellow);
        }

        plr.SendMessage($"[c/FFA500:提示:] /cb add 建筑名 条件1,条件2... 来设置进度限制", Color.Orange);
        plr.SendMessage($"[c/FFA500:帮助:] /cb cd help", Color.Orange);
    }
    #endregion

    #region 显示条件帮助信息
    private static void ShowConditionHelp(TSPlayer plr)
    {
        plr.SendMessage("\n[c/00FFFF:进度条件使用说明]", Color.Cyan);
        plr.SendMessage("[c/FFFFFF:1. 复制时设置条件:]", Color.White);
        plr.SendMessage("[c/FFFF00:  /cb add 我的建筑 困难模式,世纪之花]", Color.Yellow);
        plr.SendMessage("[c/FFFF00:  /cb add 新手建筑 史莱姆王,克眼]", Color.Yellow);

        plr.SendMessage("[c/FFFFFF:2. 粘贴时检查条件:]", Color.White);
        plr.SendMessage("[c/FFFF00:  只有满足所有条件的玩家才能粘贴建筑]", Color.Yellow);
        plr.SendMessage("[c/FFFF00:  管理员无视所有条件限制]", Color.Yellow);

        plr.SendMessage("[c/FFFFFF:3. 查看条件列表:]", Color.White);
        plr.SendMessage("[c/FFFF00:  /cb cond - 查看第一页]", Color.Yellow);
        plr.SendMessage("[c/FFFF00:  /cb cond 2 - 查看第二页]", Color.Yellow);
        plr.SendMessage("[c/FFFF00:  /cb cond help - 显示此帮助]", Color.Yellow);

        plr.SendMessage("[c/FFFFFF:4. 同义条件:]", Color.White);
        plr.SendMessage("[c/FFFF00:  括号内的名称是等效的别名，可以互换使用]", Color.Yellow);
    }
    #endregion

    #region 菜单方法
    private static void HelpCmd(TSPlayer plr)
    {
        var mess = new StringBuilder(); //用于存储指令内容

        // 先构建消息内容
        if (plr.RealPlayer)
        {
            plr.SendMessage("[i:3455][c/AD89D5:复][c/D68ACA:制][c/DF909A:建][c/E5A894:筑][i:3454] " +
            "[i:3456][C/F2F2C7:开发] [C/BFDFEA:by] [c/00FFFF:羽学] | [c/7CAEDD:少司命][i:3459]", 240, 250, 150);

            if (plr.HasPermission(Config.IsAdamin))
            {
                mess.Append($"/cb on与off ——启用与关闭开服出生点生成\n" +
                            $"/cb s 1 ——敲击或放置一个方块到左上角\n" +
                            $"/cb s 2 ——敲击或放置一个方块到右下角\n" +
                            $"/cb add 名字 ——添加建筑(sv)\n" +
                            $"/cb sp <索引/名字> ——生成建筑(pt)\n" +
                            $"/cb bk ——还原图格\n" +
                            $"/cb list ——列出建筑(ls)\n" +
                            $"/cb r ——列出区域(在区域里切换高亮边界显示)\n" +
                            $"/cb rd <索引/区域名> ——查看该区域访客记录\n" +
                            $"/cb del <索引/区域名> ——移除区域与建筑\n" +
                            $"/cb up <索引/区域名> <0或1> <玩家名> <+-组名> ——更新区域\n" +
                            $"/cb at  ——自动清理建筑与区域功能\n" +
                            $"/cb zip ——清空建筑与保护区域并备份为zip\n" +
                            $"/cb coud  ——显示进度参考(cd)\n");
            }
            else
            {
                mess.Append($"/cb s 1 ——敲击或放置一个方块到左上角\n" +
                            $"/cb s 2 ——敲击或放置一个方块到右下角\n" +
                            $"/cb add 名字 ——添加建筑(sv)\n" +
                            $"/cb sp <索引/名字> ——生成建筑(pt)\n" +
                            $"/cb bk ——还原图格\n" +
                            $"/cb list ——列出建筑(ls)\n" +
                            $"/cb r ——列出区域(在区域里切换高亮边界显示)\n" +
                            $"/cb rd <索引/区域名> ——查看该区域访客记录\n" +
                            $"/cb del <索引/区域名> ——移除自己的区域与建筑\n" +
                            $"/cb up <索引/区域名> <0或1> <玩家名> <+-组名> ——更新自己的区域\n" +
                            $"/cb coud  ——显示进度参考(cd)\n");

            }

            // 现在对消息内容应用渐变色
            var Text = mess.ToString();
            var lines = Text.Split('\n');
            var GradMess = new StringBuilder();
            var start = new Color(166, 213, 234);
            var end = new Color(245, 247, 175);
            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrEmpty(lines[i]))
                {
                    float ratio = (float)i / (lines.Length - 1);
                    var gradColor = Color.Lerp(start, end, ratio);

                    // 将颜色转换为十六进制格式
                    string colorHex = $"{gradColor.R:X2}{gradColor.G:X2}{gradColor.B:X2}";

                    // 使用颜色标签包装每一行
                    GradMess.AppendLine($"[c/{colorHex}:{lines[i]}]");
                }
            }

            plr.SendMessage(GradMess.ToString(), 240, 250, 150);
        }
        else
        {
            plr.SendMessage("《复制建筑》\n" +
                        $"/cb on与off ——启用关闭开服出生点生成\n" +
                        $"/cb s 1 ——敲击或放置一个方块到左上角\n" +
                        $"/cb s 2 ——敲击或放置一个方块到右下角\n" +
                        $"/cb add 名字 ——添加建筑(sv)\n" +
                        $"/cb sp [索引/名字] ——生成建筑(pt)\n" +
                        $"/cb bk ——还原图格\n" +
                        $"/cb list ——列出建筑(ls)\n" +
                        $"/cb r ——列出区域(在区域里切换高亮边界显示)\n" +
                        $"/cb rd [索引/区域名] ——查看该区域访客记录\n" +
                        $"/cb del [索引/区域名] ——移除区域\n" +
                        $"/cb up [索引/区域名] [0或1] [玩家名] [+-组名] ——更新区域\n" +
                        $"/cb at  ——自动清理建筑与区域功能\n" +
                        $"/cb coud  ——显示进度参考(cd)\n" +
                        $"/cb zip ——清空建筑与保护区域并备份为zip", 240, 250, 150);
        }
    }
    #endregion

    #region 随机颜色
    private static Color RandomColors(Random random)
    {
        var r = random.Next(200, 255);
        var g = random.Next(200, 255);
        var b = random.Next(150, 200);
        var color = new Color(r, g, b);
        return color;
    }
    #endregion
}
