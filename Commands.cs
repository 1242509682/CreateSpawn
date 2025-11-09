using Microsoft.Xna.Framework;
using TShockAPI;
using static CreateSpawn.Utils;
using static CreateSpawn.CreateSpawn;

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
                    {
                        if (!plr.HasPermission(Config.IsAdamin)) return;

                        Config.SpawnEnabled = true;
                        Config.Write();
                        plr.SendMessage($"已开启出生点生成功能,请重启服务器", 240, 250, 150);
                        plr.SendInfoMessage($"或在控制台使用:/cb sp 出生点", 240, 250, 150);
                    }
                    break;

                case "off":
                    {
                        if (!plr.HasPermission(Config.IsAdamin)) return;

                        Config.SpawnEnabled = false;
                        Config.Write();
                        plr.SendMessage($"已关闭出生点生成功能", 240, 250, 150);
                    }
                    break;

                case "s":
                case "set":
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
                                string msg = $"[c/D0AFEB:{i + 1}.] [c/FFFFFF:{clipNames[i]}]";
                                plr.SendMessage(msg, Color.AntiqueWhite);
                            }

                            plr.SendMessage($"可使用指定粘贴指令:[c/D0AFEB:/cb pt 名字]", color);
                            plr.SendMessage($"或使用索引号:[c/D0AFEB:/cb pt 索引号]", color);
                        }
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
                        if (args.Parameters.Count >= 2 && !string.IsNullOrWhiteSpace(args.Parameters[1]))
                        {
                            name = args.Parameters[1]; // 使用指定的名字
                        }

                        // 保存到剪贴板
                        var clip = CopyBuilding(
                            plr.TempPoints[0].X, plr.TempPoints[0].Y,
                            plr.TempPoints[1].X, plr.TempPoints[1].Y);

                        Map.SaveClip(name, clip);
                        plr.SendSuccessMessage($"已复制区域 ({clip.Width}x{clip.Height})");
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
                    {
                        if (NeedWaitTask()) return;

                        string name = plr.Name; // 默认使用玩家自己的名字

                        // 检查参数是否存在
                        if (args.Parameters.Count > 1)
                        {
                            string param = args.Parameters[1];
                            if (!string.IsNullOrWhiteSpace(param))
                            {
                                name = param;
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
                            plr.SendInfoMessage($"使用索引 {index} 对应的建筑: {actualName}");
                        }
                        else
                        {
                            // 按名称处理
                            clip = Map.LoadClip(name);
                        }

                        if (clip == null)
                        {
                            plr.SendErrorMessage($"未找到建筑: {name}");
                            plr.SendInfoMessage("复制指令:/cb save");
                            plr.SendInfoMessage("查建筑表:/cb list");
                            return;
                        }

                        int startX = 0;
                        int startY = 0;

                        if (plr.RealPlayer) // 如果是真实玩家则当前位置为头顶
                        {
                            startX = plr.TileX - clip.Width / 2;
                            startY = plr.TileY - clip.Height;
                        }
                        else if (plr == TSPlayer.Server) //如果是服务器 则使用出生点
                        {
                            startX = Terraria.Main.spawnTileX - Config.CentreX + Config.AdjustX;
                            startY = Terraria.Main.spawnTileY - Config.CountY + Config.AdjustY;
                        }

                        await SpawnBuilding(plr, startX, startY, clip, name);
                    }
                    break;

                case "bk":
                case "back":
                case "fix":
                case "还原":
                    {
                        if (NeedWaitTask()) return;

                        await AsyncBack(plr, plr.TempPoints[0].X, plr.TempPoints[0].Y, plr.TempPoints[1].X, plr.TempPoints[1].Y);
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

                case "r":
                case "region":
                    {
                        // 查找由本插件创建的区域（名称包含时间戳格式）
                        var regions = RegionManager.GetPluginRegions();

                        if (regions.Count == 0)
                        {
                            plr.SendInfoMessage("没有找到由复制建筑插件创建的区域。");
                            return;
                        }

                        plr.SendInfoMessage($"由复制建筑插件创建的区域 ({regions.Count} 个):");
                        for (int i = 0; i < regions.Count; i++)
                        {
                            var region = regions[i];
                            plr.SendInfoMessage($"{i + 1}. {region.Name} (所有者: {region.Owner}, 范围: {region.Area.X},{region.Area.Y} 到 {region.Area.X + region.Area.Width},{region.Area.Y + region.Area.Height})");
                        }

                        if (plr.HasPermission(Config.IsAdamin))
                            plr.SendInfoMessage($"可以使用索引号代替完整区域名，例如: /cb del 1 或 /cb up 1 0");
                    }
                    break;

                case "del":
                    {
                        if (!plr.HasPermission(Config.IsAdamin)) return;

                        if (args.Parameters.Count < 2)
                        {
                            plr.SendErrorMessage("用法: /cb del <索引/区域名称>");
                            plr.SendErrorMessage("使用 /cb r 查看区域列表和索引号");
                            return;
                        }

                        string regionInput = args.Parameters[1];
                        RegionManager.DeleteRegion(plr, regionInput);
                    }
                    break;

                case "up":
                    {
                        if (!plr.HasPermission(Config.IsAdamin)) return;

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

                        string regionInput = args.Parameters[1];
                        string operation = args.Parameters[2];
                        RegionManager.UpdateRegion(plr, regionInput, operation);
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

    #region 菜单方法
    private static void HelpCmd(TSPlayer plr)
    {
        var random = new Random();
        Color color = RandomColors(random);
        
        if (plr.HasPermission(Config.IsAdamin))
        {
            plr.SendInfoMessage($"复制建筑指令菜单");
            plr.SendMessage($"/cb on ——启用开服出生点生成", color);
            plr.SendMessage($"/cb off ——关闭开服出生点生成", color);
            plr.SendMessage($"/cb s 1 ——敲击或放置一个方块到左上角", color);
            plr.SendMessage($"/cb s 2 ——敲击或放置一个方块到右下角", color);
            plr.SendMessage($"/cb add 名字 ——添加建筑(sv)", color);
            plr.SendMessage($"/cb sp [索引/名字] ——生成建筑(pt)", color);
            plr.SendMessage($"/cb back ——还原图格(bk)", color);
            plr.SendMessage($"/cb list ——列出建筑(ls)", color);
            plr.SendMessage($"/cb r ——列出区域", color);
            plr.SendMessage($"/cb up [索引/名字] [0或1] [玩家名] [+-组名] ——更新区域", color);
            plr.SendMessage($"/cb del [索引/名字] ——移除区域", color);
            plr.SendMessage($"/cb zip ——清空建筑并备份为zip", color);
        }
        else
        {
            plr.SendInfoMessage($"复制建筑指令菜单");
            plr.SendMessage($"/cb s 1 ——敲击或放置一个方块到左上角", color);
            plr.SendMessage($"/cb s 2 ——敲击或放置一个方块到右下角", color);
            plr.SendMessage($"/cb add 名字 ——添加建筑(sv)", color);
            plr.SendMessage($"/cb spawn [索引/名字] ——生成建筑(pt)", color);
            plr.SendMessage($"/cb back ——还原图格(bk)", color);
            plr.SendMessage($"/cb list ——列出建筑(ls)", color);
            plr.SendMessage($"/cb r ——列出区域(r)", color);
        }
    }
    #endregion

    #region 随机颜色
    private static Color RandomColors(Random random)
    {
        var r = random.Next(170, 255);
        var g = random.Next(170, 255);
        var b = random.Next(170, 255);
        var color = new Color(r, g, b);
        return color;
    }
    #endregion
}
