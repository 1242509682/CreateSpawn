using TShockAPI;
using Terraria;
using Terraria.ID;
using Terraria.DataStructures;
using Microsoft.Xna.Framework;
using static CreateSpawn.CreateSpawn;
using static Terraria.GameContent.Tile_Entities.TELogicSensor;

namespace CreateSpawn;

internal static class Utils
{
    #region 缓存原始图格
    public static void SaveOrigTile(TSPlayer plr, int startX, int startY, int endX, int endY)
    {
        // 使用CopyBuilding方法创建当前选区的原始建筑数据
        var building = CopyBuilding(startX, startY, endX, endY);

        // 将building对象压入栈中
        var stack = Map.LoadBack(plr.Name);
        stack.Push(building);
        Map.SaveBack(plr.Name, stack);
    }
    #endregion

    #region 还原图格实体方法
    public static void RollbackBuilding(TSPlayer plr)
    {
        var stack = Map.LoadBack(plr.Name);
        if (stack.Count == 0)
        {
            plr.SendErrorMessage("没有可还原的图格");
            return;
        }

        var building = stack.Pop();
        Map.SaveBack(plr.Name, stack);

        // 计算选区边界
        int startX = building.Origin.X;
        int startY = building.Origin.Y;
        int endX = startX + building.Width - 1;
        int endY = startY + building.Height - 1;

        if (building.Tiles == null) return;

        // 0. 还原前先销毁当前区域的互动家具实体
        KillAll(startX, endX, startY, endY);

        // 1. 还原图格数据
        for (int x = 0; x < building.Width; x++)
        {
            for (int y = 0; y < building.Height; y++)
            {
                int worldX = startX + x;
                int worldY = startY + y;

                if (worldX < 0 || worldX >= Main.maxTilesX ||
                    worldY < 0 || worldY >= Main.maxTilesY) continue;

                Main.tile[worldX, worldY].CopyFrom(building.Tiles[x, y]);
                TSPlayer.All.SendTileSquareCentered(worldX, worldY, 1);
            }
        }

        // 2. 修复实体
        FixAll(startX, endX, startY, endY);

        // 3. 恢复箱子物品（依赖已存在的箱子实体）
        if (building.ChestItems != null)
        {
            RestoreChestItems(building.ChestItems, new Point(0, 0));
        }

        // 4. 恢复标牌信息
        RestoreSignText(building, 0, 0);

        plr.SendSuccessMessage($"已还原区域 ({building.Width}x{building.Height})");
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
        var signText = new List<Sign>(); // 用于存储标牌数据

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                int indexX = x - minX;
                int indexY = y - minY;
                tiles[indexX, indexY] = (Terraria.Tile)Main.tile[x, y].Clone();

                GetChestItems(chestItems, x, y); //获取箱子物品
                GetSign(signText, x, y); //获取标牌、广播盒、墓碑上等可阅读家具的信息
            }
        }

        return new Building
        {
            Width = width,
            Height = height,
            Tiles = tiles,
            Origin = new Point(minX, minY),
            ChestItems = chestItems,
            Signs = signText
        };
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
                if (tile.type == TileID.ItemFrame && tile.frameX == 0 && tile.frameY == 0)
                {
                    var ItemFrame = Terraria.GameContent.Tile_Entities.TEItemFrame.Place(x, y);
                    WorldGen.SquareTileFrame(x, y, true);
                    if (ItemFrame == -1) continue;
                }

                //武器架
                if ((tile.type == TileID.WeaponsRack || tile.type == TileID.WeaponsRack2) && tile.frameX == 0 && tile.frameY == 0)
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
                    Terraria.GameContent.Tile_Entities.TELogicSensor.Find(x, y) == -1 && tile.frameX == 0 && tile.frameY == 0)
                {
                    int LogicSensor = Terraria.GameContent.Tile_Entities.TELogicSensor.Place(x, y);
                    if (LogicSensor == -1) continue;

                    ((Terraria.GameContent.Tile_Entities.TELogicSensor)TileEntity.ByID[LogicSensor]).logicCheck = (LogicCheckType)(tile.frameY / 18 + 1);
                }

                //人体模型
                if (tile.type == TileID.DisplayDoll && tile.frameX == 0 && tile.frameY == 0)
                {
                    var DisplayDoll = Terraria.GameContent.Tile_Entities.TEDisplayDoll.Place(x, y);
                    if (DisplayDoll == -1) continue;
                }

                //盘子
                if (tile.type == TileID.FoodPlatter && tile.frameX == 0 && tile.frameY == 0)
                {
                    var FoodPlatter = Terraria.GameContent.Tile_Entities.TEFoodPlatter.Place(x, y);
                    WorldGen.SquareTileFrame(x, y, true);
                    if (FoodPlatter == -1) continue;
                }

                //晶塔
                if (tile.type == TileID.TeleportationPylon && tile.frameX == 0 && tile.frameY == 0)
                {
                    var TeleportationPylon = Terraria.GameContent.Tile_Entities.TETeleportationPylon.Place(x, y);
                    if (TeleportationPylon == -1) continue;
                }

                //训练假人（稻草人）
                if (tile.type == TileID.TargetDummy && tile.frameX == 0 && tile.frameY == 0)
                {
                    var TrainingDummy = Terraria.GameContent.Tile_Entities.TETrainingDummy.Place(x, y);
                    if (TrainingDummy == -1) continue;
                }

                //衣帽架
                if (tile.type == TileID.HatRack && tile.frameX == 0 && tile.frameY == 0)
                {
                    var HatRack = Terraria.GameContent.Tile_Entities.TEHatRack.Place(x, y);
                    WorldGen.SquareTileFrame(x, y, true);
                    if (HatRack == -1) continue;
                }
            }
        }
    }
    #endregion

    #region 销毁所有互动家具实体
    public static void KillAll(int startX, int endX, int startY, int endY)
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
                    Chest.DestroyChest(x, y);
                }

                //物品框
                if (tile.type == TileID.ItemFrame)
                {
                    Terraria.GameContent.Tile_Entities.TEItemFrame.Kill(x, y);
                }

                //武器架
                if (tile.type == TileID.WeaponsRack || tile.type == TileID.WeaponsRack2)
                {
                    Terraria.GameContent.Tile_Entities.TEWeaponsRack.Kill(x, y);
                }

                //标牌 墓碑  广播盒
                if ((tile.type == TileID.Signs ||
                    tile.type == TileID.Tombstones ||
                    tile.type == TileID.AnnouncementBox))
                {
                    Sign.KillSign(x, y);
                }

                //逻辑感应器
                if (tile.type == TileID.LogicSensor)
                {
                    Terraria.GameContent.Tile_Entities.TELogicSensor.Kill(x, y);
                }

                //人体模型
                if (tile.type == TileID.DisplayDoll)
                {
                    Terraria.GameContent.Tile_Entities.TEDisplayDoll.Kill(x, y);
                }

                //盘子
                if (tile.type == TileID.FoodPlatter)
                {
                    Terraria.GameContent.Tile_Entities.TEFoodPlatter.Kill(x, y);
                }

                //晶塔
                if (tile.type == TileID.TeleportationPylon)
                {
                    Terraria.GameContent.Tile_Entities.TETeleportationPylon.Kill(x, y);
                }

                //训练假人（稻草人）
                if (tile.type == TileID.TargetDummy)
                {
                    Terraria.GameContent.Tile_Entities.TETrainingDummy.Kill(x, y);
                }

                //衣帽架
                if (tile.type == TileID.HatRack)
                {
                    Terraria.GameContent.Tile_Entities.TEHatRack.Kill(x, y);
                }
            }
        }
    } 
    #endregion
}
