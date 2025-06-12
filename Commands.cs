using Microsoft.Xna.Framework;
using TShockAPI;
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
                        Config.Enabled = true;
                        Config.Write();
                        plr.SendMessage($"已开启出生点生成功能,请重启服务器", 240, 250, 150);
                        plr.SendInfoMessage($"或在控制台使用:/cb sp 出生点", 240, 250, 150);
                    }
                    break;

                case "off":
                    {
                        Config.Enabled = false;
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

                        var clip = Map.LoadClip(name);
                        if (clip == null)
                        {
                            plr.SendErrorMessage("剪贴板为空!");
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

                        await SpawnBuilding(plr, startX, startY, clip);
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

    #region 菜单方法
    private static void HelpCmd(TSPlayer plr)
    {
        var random = new Random();
        Color color = RandomColors(random);

        plr.SendInfoMessage($"复制建筑指令菜单");
        plr.SendMessage($"/cb on ——启用开服出生点生成", color);
        plr.SendMessage($"/cb off ——关闭开服出生点生成", color);
        plr.SendMessage($"/cb s 1 ——敲击或放置一个方块到左上角", color);
        plr.SendMessage($"/cb s 2 ——敲击或放置一个方块到右下角", color);
        plr.SendMessage($"/cb add 名字 ——添加建筑(sv)", color);
        plr.SendMessage($"/cb spawn 名字 ——生成建筑(pt)", color);
        plr.SendMessage($"/cb back ——还原图格(bk)", color);
        plr.SendMessage($"/cb list ——还原图格(ls)", color);
        plr.SendMessage($"/cb zip ——清空建筑并备份为zip", color);
    }
    #endregion

    #region 随机颜色
    private static Color RandomColors(Random random)
    {
        var r = random.Next(150, 200);
        var g = random.Next(170, 200);
        var b = random.Next(170, 200);
        var color = new Color(r, g, b);
        return color;
    } 
    #endregion
}
