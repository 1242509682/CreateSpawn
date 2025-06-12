using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Generation;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;
using static Terraria.GameContent.Tile_Entities.TELogicSensor;

namespace CreateSpawn;

[ApiVersion(2, 1)]
public class CreateSpawn : TerrariaPlugin
{
    #region 插件信息
    public override string Name => "复制建筑";
    public override string Author => "少司命 羽学";
    public override Version Version => new(1, 0, 0, 7);
    public override string Description => "使用指令复制区域建筑,支持保存建筑文件、跨地图粘贴";
    #endregion

    #region 注册与释放
    public CreateSpawn(Main game) : base(game) { }
    public override void Initialize()
    {
        LoadConfig();
        ExtractData(); //内嵌资源
        GeneralHooks.ReloadEvent += ReloadConfig;
        ServerApi.Hooks.GamePostInitialize.Register(this, this.GamePost);
        On.Terraria.WorldGen.AddGenerationPass_string_WorldGenLegacyMethod += WorldGen_AddGenerationPass_string_WorldGenLegacyMethod;
        TShockAPI.Commands.ChatCommands.Add(new Command("create.copy", Commands.CMDAsync, "cb", "复制建筑"));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            GeneralHooks.ReloadEvent -= ReloadConfig;
            ServerApi.Hooks.GamePostInitialize.Deregister(this, this.GamePost);
            On.Terraria.WorldGen.AddGenerationPass_string_WorldGenLegacyMethod -= WorldGen_AddGenerationPass_string_WorldGenLegacyMethod;
            TShockAPI.Commands.ChatCommands.RemoveAll(x => x.CommandDelegate == Commands.CMDAsync);
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
    private async void GamePost(EventArgs args)
    {
        if (Config.Enabled)
        {
            var name = "出生点";
            var clip = Map.LoadClip(name);
            if (clip == null)
            {
                TShock.Utils.Broadcast($"未找到名为 {name} 的建筑!", 240, 250, 150);
                return;
            }

            // 微调参数计算（仿照 SpawnBuilding）
            int spwx = Main.spawnTileX; // 出生点 X（单位是图格）
            int spwy = Main.spawnTileY; // 出生点 Y

            int startX = spwx - Config.CentreX + Config.AdjustX;
            int startY = spwy - Config.CountY + Config.AdjustY;

            // 传入新的坐标
            await SpawnBuilding(TSPlayer.Server, startX, startY, clip);

            Config.Enabled = false;
            Config.Write();
        }
    }

    private void WorldGen_AddGenerationPass_string_WorldGenLegacyMethod(On.Terraria.WorldGen.orig_AddGenerationPass_string_WorldGenLegacyMethod orig, string name, WorldGenLegacyMethod method)
    {
        Config.Enabled = true;
        Config.Write();
        orig(name, method);
    }
    #endregion

    #region 内嵌出生点
    private void ExtractData()
    {
        // 检查文件夹是否存在（不是检查文件！）
        if (Directory.Exists(Map.Paths)) return;

        // 创建文件夹并释放内嵌资源
        Directory.CreateDirectory(Map.Paths);

        var assembly = Assembly.GetExecutingAssembly();
        var resource = $"{assembly.GetName().Name}.内嵌资源.出生点_cp.map";

        using (var stream = assembly.GetManifestResourceStream(resource))
        {
            if (stream == null)
            {
                TShock.Log.ConsoleError("[复制建筑] 内嵌资源未找到！");
                return;
            }

            using (var fileStream = File.Create(Path.Combine(Map.Paths, "出生点_cp.map")))
            {
                stream.CopyTo(fileStream);
            }
        }

        TShock.Log.ConsoleInfo("[复制建筑] 已初始化默认出生点数据");
    }
    #endregion

    #region 还原选区指令方法
    public static int GetUnixTimestamp => (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
    public static Task AsyncBack(TSPlayer plr, int startX, int startY, int endX, int endY)
    {
        TileHelper.StartGen();
        int secondLast = GetUnixTimestamp;
        return Task.Run(delegate
        {
            RollbackTiles(plr);

        }).ContinueWith(delegate
        {
            //还原也修一遍 避免互动家具失效
            FixAll(startX, endX, startY, endY);

            TileHelper.GenAfter();
            int value = GetUnixTimestamp - secondLast;
            plr.SendSuccessMessage($"已将选区还原，用时{value}秒。");
        });
    }
    #endregion

    #region 缓存选区原始图格方法
    public static void SaveOrigTiles(TSPlayer plr, int startX, int startY, int endX, int endY)
    {
        var snapshot = new Dictionary<Point, Terraria.Tile>();

        for (int x = Math.Min(startX, endX); x <= Math.Max(startX, endX); x++)
            for (int y = Math.Min(startY, endY); y <= Math.Max(startY, endY); y++)
                snapshot[new Point(x, y)] = (Terraria.Tile)Main.tile[x, y].Clone();

        var stack = Map.LoadBack(plr.Name);
        stack.Push(snapshot);
        Map.SaveBack(plr.Name, stack);
    }
    #endregion

    #region 还原图格方法
    public static void RollbackTiles(TSPlayer plr)
    {
        var stack = Map.LoadBack(plr.Name);
        if (stack.Count == 0)
        {
            plr.SendErrorMessage("没有可还原的图格");
            return;
        }

        var snapshot = stack.Pop();
        Map.SaveBack(plr.Name, stack);

        foreach (var t in snapshot)
        {
            var pos = t.Key;
            var orig = t.Value;

            Main.tile[pos.X, pos.Y].CopyFrom(orig);
            TSPlayer.All.SendTileSquareCentered(pos.X, pos.Y, 1);
        }
    }
    #endregion

    #region 创建剪贴板数据
    public static Building CopyBuilding(int startX, int startY, int endX, int endY)
    {
        int minX = Math.Min(startX, endX);
        int maxX = Math.Max(startX, endX);
        int minY = Math.Min(startY, endY);
        int maxY = Math.Max(startY, endY);

        int width = maxX - minX + 1;
        int height = maxY - minY + 1;

        Terraria.Tile[,] tiles = new Terraria.Tile[width, height];

        var chestItems = new List<ChestItemData>();  // 定义箱子物品及其位置
        var signs = new List<Sign>(); // 用于存储标牌数据

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                int indexX = x - minX;
                int indexY = y - minY;
                tiles[indexX, indexY] = (Terraria.Tile)Main.tile[x, y].Clone();

                GetChestItems(chestItems, x, y); //获取箱子物品
                GetSign(signs, x, y); //获取标牌、广播盒、墓碑上等可阅读家具的信息
            }
        }

        return new Building
        {
            Width = width,
            Height = height,
            Tiles = tiles,
            Origin = new Point(minX, minY),
            ChestItems = chestItems,
            Signs = signs
        };
    }
    #endregion

    #region 异步粘贴实现
    public static Task SpawnBuilding(TSPlayer plr, int startX, int startY, Building clip)
    {
        TileHelper.StartGen();
        //缓存 方便粘贴错了还原
        SaveOrigTiles(plr, startX, startY, startX + clip.Width, startY + clip.Height);
        int secondLast = GetUnixTimestamp;

        return Task.Run(() =>
        {
            for (int x = 0; x < clip.Width; x++)
            {
                for (int y = 0; y < clip.Height; y++)
                {
                    int worldX = startX + x;
                    int worldY = startY + y;

                    // 边界检查
                    if (worldX < 0 || worldX >= Main.maxTilesX ||
                        worldY < 0 || worldY >= Main.maxTilesY) continue;

                    // 完全复制图格数据
                    Main.tile[worldX, worldY] = (Terraria.Tile)clip.Tiles![x, y].Clone();
                }
            }
        }).ContinueWith(_ =>
        {
            // 修复箱子、物品框、武器架、标牌、墓碑、广播盒、逻辑感应器、人偶模特、盘子、晶塔、稻草人、衣帽架
            FixAll(startX, startX + clip.Width - 1, startY, startY + clip.Height - 1);

            // 定义偏移坐标（从原始世界坐标到玩家头顶）
            int baseX = startX - clip.Origin.X;
            int baseY = startY - clip.Origin.Y;
            //修复箱子内物品
            RestoreChestItems(clip.ChestItems!, new Point(baseX, baseY));
            //修复标牌信息
            RestoreSignText(clip, baseX, baseY);

            TileHelper.GenAfter();
            int value = GetUnixTimestamp - secondLast;
            plr.SendSuccessMessage($"已粘贴区域 ({clip.Width}x{clip.Height})，用时{value}秒。");
        });
    }
    #endregion

    #region 获取箱子物品方法
    public static void GetChestItems(List<ChestItemData> chestItems, int x, int y)
    {
        // 判断是否是箱子图格
        if (!TileID.Sets.BasicChest[Main.tile[x, y].type]) return;

        // 查找对应的 Chest 对象
        int index = Chest.FindChest(x, y);
        if (index < 0) return;

        Chest chest = Main.chest[index];
        if (chest == null) return;

        // 只处理箱子的左上角图格
        if (x != chest.x || y != chest.y) return;

        //定义箱子内的物品格子位置
        for (int slot = 0; slot < 40; slot++)
        {
            Item item = chest.item[slot];
            if (item?.active == true)
            {
                // 克隆物品并记录其原来所在箱子的位置
                chestItems.Add(new ChestItemData
                {
                    Item = (Item)item.Clone(), // 克隆物品
                    Position = new Point(x, y), // 记录箱子位置
                    Slot = slot
                });
            }
        }
    }
    #endregion

    #region 还原箱子物品方法
    public static void RestoreChestItems(IEnumerable<ChestItemData> Chests, Point offset)
    {
        if (Chests == null || !Chests.Any()) return;

        foreach (var data in Chests)
        {
            try
            {
                if (data.Item == null || data.Item.IsAir) continue;

                // 计算新的箱子位置
                int newX = data.Position.X + offset.X;
                int newY = data.Position.Y + offset.Y;

                // 查找箱子
                int index = Chest.FindChest(newX, newY);
                if (index == -1) continue;

                //获取箱子对象
                Chest chest = Main.chest[index];

                // 检查槽位是否合法
                if (data.Slot < 0 || data.Slot >= 40) continue;

                // 克隆物品并设置到箱子槽位
                chest.item[data.Slot] = (Item)data.Item.Clone();
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"还原箱子物品出错 @ {data.Position}: {ex}");
            }
        }
    }
    #endregion

    #region 获取标牌内容方法
    public static void GetSign(List<Sign> signs, int x, int y)
    {
        if (Main.tile[x, y]?.active() == true && Main.tileSign[Main.tile[x, y].type])
        {
            // 获取 Sign ID
            int signId = Sign.ReadSign(x, y);
            if (signId >= 0)
            {
                Sign origSign = Main.sign[signId];
                if (origSign != null)
                {
                    signs.Add(new Sign
                    {
                        x = x,
                        y = y,
                        text = origSign.text ?? ""
                    });
                }
            }
        }
    }
    #endregion

    #region 创建标牌方法
    private static int CreateNewSign(int x, int y)
    {
        // 查找是否有重复的 Sign
        for (int i = 0; i < Sign.maxSigns; i++)
        {
            if (Main.sign[i] != null && Main.sign[i].x == x && Main.sign[i].y == y)
            {
                return i;
            }
        }

        // 找一个空槽位插入新的 Sign
        for (int i = 0; i < Sign.maxSigns; i++)
        {
            if (Main.sign[i] == null)
            {
                Main.sign[i] = new Sign
                {
                    x = x,
                    y = y,
                    text = ""
                };
                return i;
            }
        }

        return -1; // 没有可用槽位
    }
    #endregion

    #region 还原标牌内容方法
    public static void RestoreSignText(Building clip, int baseX, int baseY)
    {
        if (clip.Signs != null)
        {
            foreach (var sign in clip.Signs)
            {
                int newX = sign.x + baseX;
                int newY = sign.y + baseY;

                if (newX < 0 || newY < 0 || newX >= Main.maxTilesX || newY >= Main.maxTilesY)
                    continue;

                var tile = Main.tile[newX, newY];
                if (tile == null || !tile.active() || !Main.tileSign[tile.type]) continue;

                // 创建新的 Sign 或复用已有的
                int signId = CreateNewSign(newX, newY);
                if (signId >= 0)
                {
                    Main.sign[signId].text = sign.text;
                }
            }
        }
    }
    #endregion

    #region 修复粘贴后家具无法互动：箱子、物品框、武器架、标牌、墓碑、广播盒、逻辑感应器、人偶模特、盘子、晶塔、稻草人、衣帽架
    public static void FixAll(int startX, int endX, int startY, int endY)
    {
        for (int x = startX; x <= endX; x++)
        {
            for (int y = startY; y <= endY; y++)
            {
                var tile = Main.tile[x, y];
                if (tile == null || !tile.active()) continue;

                //如果查找图格里是箱子
                if (TileID.Sets.BasicChest[tile.type] && Chest.FindChest(x, y) == -1)
                {
                    // 创建新的 Chest  
                    int newChest = Chest.CreateChest(x, y);
                    if (newChest == -1) continue;
                }

                // 同步箱子图格到主位置
                if (tile.type == TileID.Containers || tile.type == TileID.Containers2)
                {
                    WorldGen.SquareTileFrame(x, y, true);
                }

                //物品框
                if (tile.type == TileID.ItemFrame)
                {
                    var ItemFrame = Terraria.GameContent.Tile_Entities.TEItemFrame.Place(x, y);
                    WorldGen.SquareTileFrame(x, y, true);
                    if (ItemFrame == -1) continue;
                }

                //武器架
                if (tile.type == TileID.WeaponsRack || tile.type == TileID.WeaponsRack2)
                {
                    var WeaponsRack = Terraria.GameContent.Tile_Entities.TEWeaponsRack.Place(x, y);
                    WorldGen.SquareTileFrame(x, y, true);
                    if (WeaponsRack == -1) continue;
                }

                //标牌 墓碑  广播盒
                if ((tile.type == TileID.Signs ||
                    tile.type == TileID.Tombstones ||
                    tile.type == TileID.AnnouncementBox) &&
                    tile.frameX % 36 == 0 && tile.frameY == 0 &&
                    Sign.ReadSign(x, y, false) == -1)
                {
                    var sign = Sign.ReadSign(x, y, true);
                    if (sign == -1) continue;
                }

                //逻辑感应器
                if (tile.type == TileID.LogicSensor &&
                    Terraria.GameContent.Tile_Entities.TELogicSensor.Find(x, y) == -1)
                {
                    int LogicSensor = Terraria.GameContent.Tile_Entities.TELogicSensor.Place(x, y);
                    if (LogicSensor == -1) continue;

                    ((Terraria.GameContent.Tile_Entities.TELogicSensor)TileEntity.ByID[LogicSensor]).logicCheck = (LogicCheckType)(tile.frameY / 18 + 1);
                }

                //人体模型
                if (tile.type == TileID.DisplayDoll)
                {
                    var DisplayDoll = Terraria.GameContent.Tile_Entities.TEDisplayDoll.Place(x, y);
                    if (DisplayDoll == -1) continue;
                }

                //盘子
                if (tile.type == TileID.FoodPlatter)
                {
                    var FoodPlatter = Terraria.GameContent.Tile_Entities.TEFoodPlatter.Place(x, y);
                    WorldGen.SquareTileFrame(x, y, true);
                    if (FoodPlatter == -1) continue;
                }

                //晶塔
                if (Config.FixPylon && tile.type == TileID.TeleportationPylon)
                {
                    var TeleportationPylon = Terraria.GameContent.Tile_Entities.TETeleportationPylon.Place(x, y);
                    if (TeleportationPylon == -1) continue;
                }

                //训练假人（稻草人）
                if (tile.type == TileID.TargetDummy)
                {
                    var TrainingDummy = Terraria.GameContent.Tile_Entities.TETrainingDummy.Place(x, y);
                    if (TrainingDummy == -1) continue;
                }

                //衣帽架
                if (tile.type == TileID.HatRack)
                {
                    var HatRack = Terraria.GameContent.Tile_Entities.TEHatRack.Place(x, y);
                    WorldGen.SquareTileFrame(x, y, true);
                    if (HatRack == -1) continue;
                }
            }
        }
    }
    #endregion
}