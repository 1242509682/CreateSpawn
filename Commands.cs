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
    internal static void CreateSpawnCMD(CommandArgs args)
    {
        var plr = args.Player;
        Color color = Tool.RandomColors();
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
                        ListCMD(plr, color);
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

                        Copy(args, plr);
                    }
                    break;

                case "pt":
                case "sp":
                case "spawn":
                case "create":
                case "paste":
                case "粘贴":
                    {
                        Paste(args, plr);
                    }
                    break;

                case "bk":
                case "back":
                case "fix":
                case "还原":
                    {
                        // 直接获取最新的操作记录，不管区域是否存在
                        var operation = Map.PopOperation(plr.Name);
                        if (operation == null)
                        {
                            plr.SendErrorMessage("没有可撤销的操作");
                            return;
                        }

                        SmartBack(plr, plr.TempPoints[0].X, plr.TempPoints[0].Y,
                                  plr.TempPoints[1].X, plr.TempPoints[1].Y, operation);
                    }
                    break;

                case "r":
                case "region":
                case "区域":
                case "领地":
                    {
                        ShowRegionCMD(plr);
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

                case "rm":
                case "re":
                case "remove":
                case "移除":
                    {
                        if (args.Parameters.Count < 2)
                        {
                            plr.SendErrorMessage("用法: /cb rm <索引/区域名称>");
                            plr.SendErrorMessage("使用 /cb r 查看区域列表和索引号");
                            return;
                        }

                        string Input = args.Parameters[1];
                        // 删除区域并同时还原建筑
                        RegionManager.RemoveRegion(plr, Input, true);
                    }
                    break;

                case "up":
                case "update":
                case "更新":
                    {
                        if (args.Parameters.Count < 3)
                        {
                            var mess = new StringBuilder();
                            mess.Append("用法: /cb up <索引/区域名> <操作>\n");
                            mess.Append("操作: 0或1 ——允许或禁止建筑\n");
                            mess.Append("操作: 玩家名 ——添加或移除玩家\n");
                            mess.Append("操作: +组名 ——添加组权限\n");
                            mess.Append("操作: -组名 ——移除组权限\n");
                            mess.Append("使用 /cb r 查看区域列表和索引号\n");
                            Tool.GradMess(plr, mess);
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

                        AutoClearCMD(args, plr);
                    }
                    break;

                case "d":
                case "de":
                case "delete":
                case "删除":
                    DeleteBuilding(args);
                    break;

                case "qx":
                case "cancel":
                case "取消":
                    {
                        TaskManager.CancelTask(plr);
                        plr.SendSuccessMessage("已取消当前运行的任务");
                    }
                    break;

                case "kill":
                    if (!plr.HasPermission(Config.IsAdamin))
                    {
                        plr.SendErrorMessage("你没有权限执行此操作");
                        return;
                    }
                    TaskManager.ClearAllTasks();
                    plr.SendMessage("已清理所有当前任务", Tool.RandomColors());
                    break;

                case "zip":
                case "backup":
                case "压缩":
                case "备份":
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
    }
    #endregion

    #region 列出建筑文件指令方法
    private static void ListCMD(TSPlayer plr, Color color)
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
                    msg += $" [c/FFA500:条件: {string.Join(", ", building.Conditions)}]";
                }

                plr.SendMessage(msg, Color.AntiqueWhite);
            }

            plr.SendMessage($"可使用指定粘贴指令:[c/D0AFEB:/cb pt 名字]", color);
            plr.SendMessage($"或使用索引号:[c/D0AFEB:/cb pt 索引号]", color);
        }
    }
    #endregion

    #region 复制指令方法
    private static void Copy(CommandArgs args, TSPlayer plr)
    {
        string name = plr.Name; // 默认使用玩家自己的名字
        List<string> conditions = new List<string>();

        if (args.Parameters.Count >= 2 &&
         !string.IsNullOrWhiteSpace(args.Parameters[1]))
        {
            name = args.Parameters[1]; // 使用指定的名字

            // 检查是否包含条件参数
            if (args.Parameters.Count > 2)
            {
                // 合并第2个参数之后的所有内容作为条件
                string CondStr = string.Join(" ", args.Parameters.Skip(2));

                // 支持逗号分隔的多个条件
                var CondList = CondStr.Split(new[] { ',', '，' },
                StringSplitOptions.RemoveEmptyEntries);

                foreach (var cond in CondList)
                {
                    string t = cond.Trim();
                    if (!string.IsNullOrEmpty(t))
                    {
                        conditions.Add(t);
                    }
                }

            }
        }

        // 检查复制区域内是否有受保护建筑，并验证权限
        if (!RegionManager.CanCopyFromArea
        (plr, plr.TempPoints[0].X, plr.TempPoints[0].Y,
        plr.TempPoints[1].X, plr.TempPoints[1].Y))
            return;

        // 保存到剪贴板
        var clip = CopyBuilding(
            plr.TempPoints[0].X, plr.TempPoints[0].Y,
            plr.TempPoints[1].X, plr.TempPoints[1].Y);

        // 设置进度条件
        clip.Conditions = conditions;

        Map.SaveClip(name, clip);

        plr.SendSuccessMessage($"已复制区域 {name} ({clip.Width}x{clip.Height})");
        if (conditions.Count > 0)
        {
            plr.SendSuccessMessage($"[条件: {string.Join(", ", conditions)}]");
        }

        var mess = new StringBuilder();
        mess.Append($"粘贴指令: /cb pt {name}\n");
        mess.Append("撤销指令: /cb bk\n");
        mess.Append("查建筑表: /cb ls\n");
        mess.Append("查区域表: /cb r\n");
        if (plr.HasPermission(Config.IsAdamin))
            mess.Append($"删除指令: /cb del {name}\n");
        Tool.GradMess(plr, mess);
    }
    #endregion

    #region 粘贴建筑指令方法
    private static void Paste(CommandArgs args, TSPlayer plr)
    {
        string name = plr.Name; // 默认使用玩家自己的名字
        string RegionName = name; // 用于区域创建的建筑名称
        int offset = 1; // 默认头顶模式 (1)

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

        // 检查是否有偏移模式参数
        if (args.Parameters.Count > 2)
        {
            if (int.TryParse(args.Parameters[2], out int mode))
            {
                if (mode >= 0 && mode <= 8) // 修改为0-8
                {
                    offset = mode;
                }
                else
                {
                    plr.SendErrorMessage("偏移模式必须是 0-8 之间的数字！");
                    plr.SendInfoMessage("0=中心, 1=头顶, 2=脚下, 3=左侧, 4=右侧");
                    plr.SendInfoMessage("5=左下, 6=右下, 7=左上, 8=右上");
                    return;
                }
            }
            else
            {
                plr.SendErrorMessage("偏移模式参数无效！");
                plr.SendInfoMessage("用法: /cb sp <建筑名> <偏移模式>");
                plr.SendInfoMessage("偏移模式: 0=中心, 1=头顶, 2=脚下, 3=左侧, 4=右侧");
                plr.SendInfoMessage("5=左下, 6=右下, 7=左上, 8=右上");
                return;
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
            plr.SendInfoMessage("使用 /cb list 查看所有可用建筑");
            plr.SendInfoMessage("用法: /cb sp <建筑名/索引> <偏移模式>");
            plr.SendInfoMessage("偏移模式: 中心0, 头顶1, 脚下2, 左侧3, 右侧4");
            plr.SendInfoMessage("左下5, 右下6, 左上7, 右上8");
            plr.SendInfoMessage("偏移模式不输默认为头顶");
            return;
        }

        // 检查进度条件（管理员无视条件）
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

        // 粘贴起始坐标
        int startX = 0;
        int startY = 0;

        // 用于获取已存在区域的索引号
        string RegionName2 = "";
        int Index = -1;
        string Owner = "";

        if (plr.RealPlayer) // 如果是真实玩家则当前位置为头顶
        {
            // 根据偏移模式计算起始坐标
            (startX, startY) = CalculateOffset(plr, clip, offset);

            plr.SendInfoMessage($"使用偏移模式: {GetOffsetModeName(offset)}");

            // 检查玩家头顶区域是否已经有保护区域
            if (RegionManager.IsAreaProtected(startX, startY, clip.Width, clip.Height, ref RegionName2))
            {
                if (!string.IsNullOrEmpty(RegionName2))
                {
                    Index = RegionManager.GetRegionIndex(RegionName2);
                    Owner = RegionManager.GetRegionOwner(RegionName2);
                }

                plr.SendErrorMessage($"目标区域已有保护区域 {RegionName2} 无法在此处粘贴建筑！");
                plr.SendInfoMessage("请移动到没有保护区域的空地再进行粘贴。");

                // 如果是管理员或区域所有者 则提示删除指令
                if (plr.HasPermission(Config.IsAdamin) || plr.Name == Owner)
                {
                    plr.SendInfoMessage($"或使用/cb rm {Index} 移除！");
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
                TSPlayer.Server.SendInfoMessage($"可使用/cb rm {Index} 移除！");
                return;
            }
        }

        SmartSpawn(plr, startX, startY, clip, RegionName);
    }

    // 计算偏移坐标
    private static (int x, int y) CalculateOffset(TSPlayer plr, Building clip, int Mode)
    {
        int x = plr.TileX;
        int y = plr.TileY;

        switch (Mode)
        {
            case 0: // 中心
                return (x - clip.Width / 2, y - clip.Height / 2);

            case 1: // 头顶 (默认)
                return (x - clip.Width / 2, y - clip.Height);

            case 2: // 脚下
                return (x - clip.Width / 2, y);

            case 3: // 左侧
                return (x - clip.Width, y - clip.Height / 2);

            case 4: // 右侧
                return (x, y - clip.Height / 2);

            case 5: // 左下
                return (x - clip.Width, y);

            case 6: // 右下
                return (x, y);

            case 7: // 左上
                return (x - clip.Width, y - clip.Height);

            case 8: // 右上
                return (x, y - clip.Height);

            default: // 默认头顶
                return (x - clip.Width / 2, y - clip.Height);
        }
    }

    // 获取偏移模式名称
    private static string GetOffsetModeName(int mode)
    {
        return mode switch
        {
            0 => "中心",
            1 => "头顶",
            2 => "脚下",
            3 => "左侧",
            4 => "右侧",
            5 => "左下",
            6 => "右下",
            7 => "左上",
            8 => "右上",
            _ => "未知"
        };
    }
    #endregion

    #region 显示区域列表与当前区域边界方法
    private static void ShowRegionCMD(TSPlayer plr)
    {
        // 查找由本插件创建的区域（名称包含时间戳格式）
        var Regions = RegionManager.GetPluginRegions();
        string RegionName = "";

        if (Regions.Count == 0)
        {
            plr.SendInfoMessage("没有找到由复制建筑插件创建的区域。");
            return;
        }

        plr.SendInfoMessage($"由复制建筑插件创建的区域 ({Regions.Count} 个):");

        for (int i = 0; i < Regions.Count; i++)
        {
            var region = Regions[i];

            // 确定区域名称的显示颜色
            string ListName;
            string Name;
            if (region.Owner == plr.Name)
            {
                // 当前玩家自己的区域，使用渐变色
                ListName = Tool.TextGradient(region.Name);
                RegionName = region.Name;
                Name = Tool.TextGradient(plr.Name);
            }
            else
            {
                // 其他玩家的区域，使用默认颜色
                ListName = $"[c/15EDDB:{region.Name}]";
                Name = $"[c/66AEF2:{region.Owner}]";
            }

            plr.SendMessage($"{i + 1}. {ListName.ToString()}\n" +
                $"所有者: {Name}, 范围: [c/E74F5E:{region.Area.X}]," +
                $"[c/F07F52:{region.Area.Y}] 到 [c/F0A852:{region.Area.X + region.Area.Width}]," +
                $"[c/F0C852:{region.Area.Y + region.Area.Height}]", 240, 250, 150);
        }

        if (RegionManager.HasRegionPermission(plr, RegionName))
            plr.SendMessage(Tool.TextGradient("————————————————————————"), Tool.RandomColors());
        plr.SendInfoMessage($"可以使用索引号代替完整区域名，\n" +
                            $"例如: /cb rm 1 或 /cb up 1 0");

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
    #endregion

    #region 自动清理无访客建筑指令方法
    private static void AutoClearCMD(CommandArgs args, TSPlayer plr)
    {
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

                case "admin":
                case "gm":
                    var ExAdmin = Config.AutoClear.ExemptAdmin;
                    Config.AutoClear.ExemptAdmin = !ExAdmin;
                    string statusStr = ExAdmin ? "禁用" : "启用";
                    plr.SendMessage($"已 {statusStr} 不清理管理区域", Tool.RandomColors());
                    Config.Write();
                    break;

                case "now":
                case "立即":
                    // 立即执行清理检查
                    AutoClear.CheckAllRegions(plr);
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
                        plr.SendErrorMessage("用法: /cb auto del <玩家名>");
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
                    var mess = new StringBuilder();
                    mess.Append("用法: /cb auto <on|off|now|stats|add|del|list>\n");
                    mess.Append("on/off - 启用/关闭自动清理\n");
                    mess.Append("now - 立即执行清理\n");
                    mess.Append("ck - 显示待清理区域统计\n");
                    mess.Append("cs - 测试清理单个区域\n");
                    mess.Append("add - 添加免清理玩家\n");
                    mess.Append("del - 移除免清理玩家\n");
                    mess.Append("ls - 显示免清理玩家列表\n");
                    mess.Append("gm - 切换是否清理管理区域\n");
                    Tool.GradMess(plr, mess);
                    break;
            }
        }
        else
        {
            var mess = new StringBuilder(); //用于存储指令内容

            // 显示自动清理配置信息
            mess.Append("自动清理配置:\n" +
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
                "/cb at ls 查看免清理玩家\n" +
                "/cb at gm 切换是否清理管理区域");

            Tool.GradMess(plr, mess);
        }
    }
    #endregion

    #region 删除建筑文件指令方法
    private static void DeleteBuilding(CommandArgs args)
    {
        var plr = args.Player;

        // 检查权限
        if (!plr.HasPermission(Config.IsAdamin))
        {
            plr.SendErrorMessage("你没有权限删除建筑文件！");
            return;
        }

        if (args.Parameters.Count < 2)
        {
            plr.SendErrorMessage("语法错误！正确用法: /cb del <建筑名称|索引>");
            plr.SendErrorMessage("示例: /cb del 我的房子 - 删除名为'我的房子'的建筑文件");
            plr.SendErrorMessage("示例: /cb del 1 - 删除索引为1的建筑文件");
            plr.SendInfoMessage("使用 /cb list 查看所有建筑列表和索引");
            return;
        }

        string input = args.Parameters[1];
        string buildingName = input;

        // 检查是否为受保护建筑
        if (Config.IgnoreList.Contains(input, StringComparer.OrdinalIgnoreCase))
        {
            plr.SendErrorMessage($"无法删除受保护建筑 '{input}'，该建筑在保护列表中！");
            return;
        }

        // 尝试按索引处理
        if (int.TryParse(input, out int index))
        {
            var buildingNames = Map.GetAllClipNames(); // 复用现有方法

            if (index < 1 || index > buildingNames.Count)
            {
                plr.SendErrorMessage($"索引 {index} 无效，可用范围: 1-{buildingNames.Count}");
                plr.SendInfoMessage("使用 /cb list 查看建筑列表和索引号");
                return;
            }

            buildingName = buildingNames[index - 1];

            // 再次检查保护列表（按名称）
            if (Config.IgnoreList.Contains(buildingName, StringComparer.OrdinalIgnoreCase))
            {
                plr.SendErrorMessage($"无法删除受保护建筑 '{buildingName}'，该建筑在保护列表中！");
                return;
            }
        }

        // 检查建筑文件是否存在
        if (!Map.BuildingExists(buildingName))
        {
            plr.SendErrorMessage($"建筑文件 '{buildingName}' 不存在！");
            plr.SendInfoMessage("使用 /cb list 查看所有可用建筑");
            return;
        }

        // 执行删除
        if (Map.DeleteBuildingFile(buildingName))
        {
            plr.SendSuccessMessage($"成功删除建筑文件: {buildingName}");

            // 记录操作日志
            TShock.Utils.Broadcast($"[复制建筑] 管理员 [c/46D2C2:{plr.Name}] 删除了建筑文件: [c/468DD2:{buildingName}]", 240, 250, 150);
        }
        else
        {
            plr.SendErrorMessage($"删除建筑文件 '{buildingName}' 时出错！");
        }
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
            plr.SendMessage(pageInfo, Tool.RandomColors());
        }

        plr.SendMessage($"[c/FFA500:提示:] /cb add 建筑名 条件1,条件2... 来设置进度限制", Color.Orange);
        plr.SendMessage($"[c/FFA500:帮助:] /cb cd help", Color.Orange);
    }
    #endregion

    #region 显示条件帮助信息
    private static void ShowConditionHelp(TSPlayer plr)
    {
        var mess = new StringBuilder(); //用于存储指令内容
        mess.Append("[c/00FFFF:进度条件使用说明]\n");
        mess.Append("[c/FFFFFF:1. 复制时设置条件:]\n");
        mess.Append("[c/FFFF00:  /cb add 我的建筑 困难模式,世纪之花]\n");
        mess.Append("[c/FFFF00:  /cb add 新手建筑 11,15]\n");
        mess.Append("[c/FFFFFF:2. 粘贴时检查条件:]\n");
        mess.Append("[c/FFFF00:  只有满足所有条件的玩家才能粘贴建筑]\n");
        mess.Append("[c/FFFF00:  管理员无视所有条件限制]\n");
        mess.Append("[c/FFFFFF:3. 查看条件列表:]\n");
        mess.Append("[c/FFFF00:  /cb cond - 查看第一页]\n");
        mess.Append("[c/FFFF00:  /cb cond 2 - 查看第二页]\n");
        mess.Append("[c/FFFF00:  /cb cond help - 显示此帮助]\n");
        mess.Append("[c/FFFFFF:4. 同义条件:]\n");
        mess.Append("[c/FFFF00:  括号内的名称是等效的别名，可以互换使用]\n");
        Tool.GradMess(plr, mess);
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
                            $"/cb qx ——放弃自己当前操作\n" +
                            $"/cb kill ——杀死所有当前任务\n" +
                            $"/cb list ——列出建筑(ls)\n" +
                            $"/cb r ——列出区域(在区域里切换高亮边界显示)\n" +
                            $"/cb rd <索引/区域名> ——查看该区域访客记录\n" +
                            $"/cb rm <索引/区域名> ——移除区域与建筑\n" +
                            $"/cb up <索引/区域名> <0或1> <玩家名> <+-组名> ——更新区域\n" +
                            $"/cb at  ——自动清理建筑与区域功能\n" +
                            $"/cb del ——删除指定建筑文件\n" +
                            $"/cb zip ——清空建筑与保护区域并备份为zip\n" +
                            $"/cb cd  ——显示进度参考(cd)\n");
            }
            else
            {
                mess.Append($"/cb s 1 ——敲击或放置一个方块到左上角\n" +
                            $"/cb s 2 ——敲击或放置一个方块到右下角\n" +
                            $"/cb add 名字 ——添加建筑(sv)\n" +
                            $"/cb sp <索引/名字> ——生成建筑(pt)\n" +
                            $"/cb bk ——还原图格\n" +
                            $"/cb qx ——放弃自己当前操作\n" +
                            $"/cb list ——列出建筑(ls)\n" +
                            $"/cb r ——列出区域(在区域里切换高亮边界显示)\n" +
                            $"/cb rd <索引/区域名> ——查看该区域访客记录\n" +
                            $"/cb rm <索引/区域名> ——移除自己的区域与建筑\n" +
                            $"/cb up <索引/区域名> <0或1> <玩家名> <+-组名> ——更新自己的区域\n" +
                            $"/cb cd  ——显示进度参考(cd)\n");

            }

            Tool.GradMess(plr, mess);
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
                        $"/cb qx ——放弃自己当前操作\n" +
                        $"/cb kill ——杀死所有当前任务\n" +
                        $"/cb list ——列出建筑(ls)\n" +
                        $"/cb r ——列出区域(在区域里切换高亮边界显示)\n" +
                        $"/cb rd [索引/区域名] ——查看该区域访客记录\n" +
                        $"/cb rm [索引/区域名] ——移除区域\n" +
                        $"/cb up [索引/区域名] [0或1] [玩家名] [+-组名] ——更新区域\n" +
                        $"/cb at  ——自动清理建筑与区域功能\n" +
                        $"/cb cd  ——显示进度参考(cd)\n" +
                        $"/cb del ——删除指定建筑文件\n" +
                        $"/cb zip ——清空建筑与保护区域并备份为zip", 240, 250, 150);
        }
    }
    #endregion

}
