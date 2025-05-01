using TShockAPI;
using static CreateSpawn.CreateSpawn;

namespace CreateSpawn;

internal class Commands
{
    #region 主指令方法
    internal static void CMD(CommandArgs args)
    {
        var plr = args.Player;

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
                        plr.SendMessage($"已开启复制建筑功能", 240, 250, 150);
                    }
                    break;

                case "off":
                    {
                        Config.Enabled = false;
                        Config.Write();
                        plr.SendMessage($"已关闭复制建筑功能", 240, 250, 150);
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

                case "sv":
                case "add":
                case "save":
                    {
                        if (plr.TempPoints[0].X == 0 || plr.TempPoints[1].X == 0)
                        {
                            plr.SendMessage("您还没有选择复制的区域！", 240, 250, 150);
                            plr.SendMessage("请使用指令 /cb set <1/2> 选择区域", 240, 250, 150);
                            return;
                        }
                        else if (Config.Enabled)
                        {
                            CopyBuilding(plr.TempPoints[0].X, plr.TempPoints[0].Y, plr.TempPoints[1].X, plr.TempPoints[1].Y);
                            plr.SendMessage("建筑保存成功！", 240, 250, 150);
                        }
                    }
                    break;

                case "sp":
                case "spawn":
                case "create":
                    {
                        if (Config.Enabled)
                        {
                            SpawnBuilding(plr);
                            plr.SendMessage("建筑正在生成中！", 240, 250, 150);
                        }
                    }
                    break;

                case "bk":
                case "fix":
                case "back":
                    {
                        if (Config.Enabled)
                        {
                            RollbackTiles();
                            plr.SendMessage("正在清除建筑还原图格！", 240, 250, 150);
                        }
                    }
                    break;

                default:
                    HelpCmd(plr);
                    break;
            }
        }
    }
    #endregion

    #region 菜单方法
    private static void HelpCmd(TSPlayer plr)
    {
        plr.SendMessage($"复制建筑指令菜单\n" +
            $"/cb on ——开启插件功能\n" +
            $"/cb off ——关闭插件功能\n" +
            $"/cb s 1 ——敲击或放置一个方块到左上角\n" +
            $"/cb s 2 ——敲击或放置一个方块到右下角\n" +
            $"/cb sv ——添加建筑\n" +
            $"/cb sp ——生成建筑\n" +
            $"/cb bk ——还原图格", 240, 250, 150);
    }
    #endregion
}
