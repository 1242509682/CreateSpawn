using Microsoft.Xna.Framework;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace CreateSpawn;

[ApiVersion(2, 1)]
public class CreateSpawn : TerrariaPlugin
{
    #region 插件信息
    public override string Name => "复制建筑";
    public override string Author => "少司命 羽学";
    public override Version Version => new(1, 0, 0, 6);
    public override string Description => "使用指令复制区域建筑";
    #endregion

    #region 注册与释放
    public CreateSpawn(Main game) : base(game) { }
    public override void Initialize()
    {
        LoadConfig();
        GeneralHooks.ReloadEvent += ReloadConfig;
        On.Terraria.WorldBuilding.GenerationProgress.End += this.GenerationProgress_End;
        ServerApi.Hooks.GamePostInitialize.Register(this, this.GamePost);
        TShockAPI.Commands.ChatCommands.Add(new Command("create.copy", Commands.CMD, "cb", "复制建筑"));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            GeneralHooks.ReloadEvent -= ReloadConfig;
            On.Terraria.WorldBuilding.GenerationProgress.End -= this.GenerationProgress_End;
            ServerApi.Hooks.GamePostInitialize.Deregister(this, this.GamePost);
            TShockAPI.Commands.ChatCommands.RemoveAll(x => x.CommandDelegate == Commands.CMD);
        }
        base.Dispose(disposing);
    }
    #endregion

    #region 配置重载读取与写入方法
    internal static Configuration Config = new();
    private static void ReloadConfig(ReloadEventArgs args = null!)
    {
        LoadConfig();
        args.Player.SendInfoMessage("[复制建筑]重新加载配置完毕。");
    }
    private static void LoadConfig()
    {
        Config = Configuration.Read();
        Config.Write();
    }
    #endregion

    #region 加载完世界后打开插件开关方法
    private void GamePost(EventArgs args)
    {
        if (Config.Enabled)
        {
            SpawnBuilding(null!);
        }
    }

    private void GenerationProgress_End(On.Terraria.WorldBuilding.GenerationProgress.orig_End orig, Terraria.WorldBuilding.GenerationProgress self)
    {
        Config.Enabled = true;
        Config.Write();
    }
    #endregion

    #region 保存 需要复制的建筑范围方法
    //public static Database DB = new();
    public static void CopyBuilding(int x1, int y1, int x2, int y2)
    {
        var Building = new List<Building>();
        Config.CentreX = (x2 - x1) / 2;
        Config.CountY = y2 - y1;
        for (var i = x1; i < x2; i++)
        {
            for (var j = y1; j < y2; j++)
            {
                var t = Main.tile[i, j];
                Building.Add(new Building()
                {
                    bTileHeader = t.bTileHeader,
                    bTileHeader2 = t.bTileHeader2,
                    bTileHeader3 = t.bTileHeader3,
                    frameX = t.frameX,
                    frameY = t.frameY,
                    liquid = t.liquid,
                    sTileHeader = t.sTileHeader,
                    type = t.type,
                    wall = t.wall,
                });
            }
        }
        Map.SaveMap(Building);
    }
    #endregion

    #region 生成建筑方法
    public static void SpawnBuilding(TSPlayer plr)
    {
        Task.Factory.StartNew(() =>
        {
            var Building = Map.LoadMap();
            //出生点X
            var spwx = (int)plr.X / 16;
            //出生点Y
            var spwy = (int)plr.Y / 16;
            //计算左X
            var x1 = spwx - Config.CentreX + Config.AdjustX;
            //计算左Y
            var y1 = spwy - Config.CountY + Config.AdjustY;
            //计算右x
            var x2 = Config.CentreX + spwx + Config.AdjustX;
            //计算右y
            var y2 = spwy + Config.AdjustY;

            //保存原始图格数据
            SaveOrigTiles(x1, y1, x2, y2);

            var n = 0;

            for (var i = x1; i < x2; i++)
            {
                for (var j = y1; j < y2; j++)
                {
                    var t = Main.tile[i, j];
                    t.bTileHeader = Building[n].bTileHeader;
                    t.bTileHeader2 = Building[n].bTileHeader2;
                    t.bTileHeader3 = Building[n].bTileHeader3;
                    t.frameX = Building[n].frameX;
                    t.frameY = Building[n].frameY;
                    t.liquid = Building[n].liquid;
                    t.sTileHeader = Building[n].sTileHeader;
                    t.type = Building[n].type;
                    t.wall = Building[n].wall;
                    n++;
                    TSPlayer.All.SendTileSquareCentered(i, j);
                }
            }
        });
    }
    #endregion

    #region 保存与回滚原始图格数据
    public static Building BD = new();
    public static void SaveOrigTiles(int x, int y, int x2, int y2)
    {
        for (var i = x; i <= x2; i++)
        {
            for (var j = y; j <= y2; j++)
            {
                BD.OrigTiles[new Point(i, j)] = (Tile)Main.tile[i, j].Clone();
            }
        }
    }

    public static void RollbackTiles()
    {
        Task.Factory.StartNew(static () =>
        {
            foreach (var tile in BD.OrigTiles)
            {
                var pos = tile.Key;
                var orig = tile.Value;

                Main.tile[pos.X, pos.Y].CopyFrom(orig);

                TSPlayer.All.SendTileSquareCentered(pos.X, pos.Y, 1);
            }

            // 清空字典，避免重复回滚
            // BD.OrigTiles.Clear(); 
        });
    }
    #endregion
}