using TShockAPI;
using Terraria;
using Terraria.ID;

namespace CreateSpawn;

internal class TileHelper
{
    public static bool isTaskRunning { get; set; }

    public static void StartGen()
    {
        isTaskRunning = true;
    }

    public static void FinishGen()
    {
        isTaskRunning = false;
        TShock.Utils.SaveWorld();
    }

    public static bool NeedWaitTask(TSPlayer op)
    {
        if (isTaskRunning)
        {
            op?.SendErrorMessage("另一个创建任务正在执行，请稍后再操作");
        }
        return isTaskRunning;
    }

    public static void ClearEverything(int x, int y)
    {
        Main.tile[x, y].ClearEverything();
        NetMessage.SendTileSquare(-1, x, y, TileChangeType.None);
    }

    public static bool NeedInGame(TSPlayer plr)
    {
        if (!plr.RealPlayer)
        {
            plr.SendErrorMessage("请进入游戏后再操作！");
        }
        return !plr.RealPlayer;
    }


    public static void InformPlayers()
    {
        TSPlayer[] players = TShock.Players;
        foreach (TSPlayer tSPlayer in players)
        {
            if (tSPlayer == null || !tSPlayer.Active)
            {
                continue;
            }
            for (int j = 0; j < 255; j++)
            {
                for (int k = 0; k < Main.maxSectionsX; k++)
                {
                    for (int l = 0; l < Main.maxSectionsY; l++)
                    {
                        Netplay.Clients[j].TileSections[k, l] = false;
                    }
                }
            }
        }
    }

    public static void GenAfter()
    {
        InformPlayers();
        FinishGen();
    }

    #region 更新整个世界图格方法
    public static void UpdateWorld()
    {
        foreach (RemoteClient sock in Netplay.Clients.Where(s => s.IsActive))
        {
            for (int i = Netplay.GetSectionX(0); i <= Netplay.GetSectionX(Main.maxTilesX); i++)
            {
                for (int j = Netplay.GetSectionY(0); j <= Netplay.GetSectionY(Main.maxTilesY); j++)
                    sock.TileSections[i, j] = false;
            }
        }
    }
    #endregion
}
