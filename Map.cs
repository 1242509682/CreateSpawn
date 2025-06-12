using System.IO.Compression;
using Microsoft.Xna.Framework;
using Terraria;
using TShockAPI;

namespace CreateSpawn;

public class Map
{
    //存储图格数据的目录
    internal static readonly string Paths = Path.Combine(TShock.SavePath, "CreateSpawn");

    #region GZip 压缩辅助方法
    private static Stream GZipWrite(string filePath)
    {
        var fileStream = new FileStream(filePath, FileMode.Create);
        return new GZipStream(fileStream, CompressionLevel.Optimal);
    }

    private static Stream GZipRead(string filePath)
    {
        var fileStream = new FileStream(filePath, FileMode.Open);
        return new GZipStream(fileStream, CompressionMode.Decompress);
    }
    #endregion

    #region 加载磁盘中的原始图格写到内存方法
    public static Stack<Dictionary<Point, Terraria.Tile>> LoadBack(string name)
    {
        string filePath = Path.Combine(Paths, $"{name}_bk.map");
        if (!File.Exists(filePath)) return new Stack<Dictionary<Point, Terraria.Tile>>();

        var stack = new Stack<Dictionary<Point, Terraria.Tile>>();
        using (var fs = GZipRead(filePath))
        using (var reader = new BinaryReader(fs))
        {
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                int size = reader.ReadInt32();
                var snapshot = new Dictionary<Point, Terraria.Tile>(size);

                for (int j = 0; j < size; j++)
                {
                    int x = reader.ReadInt32();
                    int y = reader.ReadInt32();
                    var tile = new Terraria.Tile
                    {
                        bTileHeader = reader.ReadByte(),
                        bTileHeader2 = reader.ReadByte(),
                        bTileHeader3 = reader.ReadByte(),
                        frameX = reader.ReadInt16(),
                        frameY = reader.ReadInt16(),
                        liquid = reader.ReadByte(),
                        sTileHeader = reader.ReadUInt16(),
                        type = reader.ReadUInt16(),
                        wall = reader.ReadUInt16()
                    };
                    snapshot[new Point(x, y)] = tile;
                }
                stack.Push(snapshot);
            }
        }
        return stack;
    }
    #endregion

    #region 保存原始图格从内存写到磁盘方法
    public static void SaveBack(string name, Stack<Dictionary<Point, Terraria.Tile>> stack)
    {
        Directory.CreateDirectory(Paths);
        string filePath = Path.Combine(Paths, $"{name}_bk.map");

        using (var fs = GZipWrite(filePath))
        using (var writer = new BinaryWriter(fs))
        {
            writer.Write(stack.Count);
            foreach (var snapshot in stack)
            {
                writer.Write(snapshot.Count);
                foreach (var pair in snapshot)
                {
                    writer.Write(pair.Key.X);
                    writer.Write(pair.Key.Y);
                    var tile = pair.Value;
                    writer.Write(tile.bTileHeader);
                    writer.Write(tile.bTileHeader2);
                    writer.Write(tile.bTileHeader3);
                    writer.Write(tile.frameX);
                    writer.Write(tile.frameY);
                    writer.Write(tile.liquid);
                    writer.Write(tile.sTileHeader);
                    writer.Write(tile.type);
                    writer.Write(tile.wall);
                }
            }
        }
    }
    #endregion

    #region 读取剪贴板方法
    internal static Building LoadClip(string name)
    {
        string filePath = Path.Combine(Paths, $"{name}_cp.map");
        if (!File.Exists(filePath)) return null!;

        using (var fs = GZipRead(filePath))
        using (var reader = new BinaryReader(fs))
        {
            int originX = reader.ReadInt32();
            int originY = reader.ReadInt32();
            int width = reader.ReadInt32();
            int height = reader.ReadInt32();

            var tiles = new Terraria.Tile[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var tile = new Terraria.Tile
                    {
                        bTileHeader = reader.ReadByte(),
                        bTileHeader2 = reader.ReadByte(),
                        bTileHeader3 = reader.ReadByte(),
                        frameX = reader.ReadInt16(),
                        frameY = reader.ReadInt16(),
                        liquid = reader.ReadByte(),
                        sTileHeader = reader.ReadUInt16(),
                        type = reader.ReadUInt16(),
                        wall = reader.ReadUInt16()
                    };
                    tiles[x, y] = tile;
                }
            }

            // 添加箱子物品加载
            int Count = reader.ReadInt32();
            var chestItems = new List<ChestItemData>(Count);
            for (int i = 0; i < Count; i++)
            {
                int posX = reader.ReadInt32();
                int posY = reader.ReadInt32();
                int slot = reader.ReadInt32();
                int type = reader.ReadInt32();
                int netId = reader.ReadInt32();
                int stack = reader.ReadInt32();
                byte prefix = reader.ReadByte();

                var item = new Item();
                item.SetDefaults(type);
                item.netID = netId;
                item.stack = stack;
                item.prefix = prefix;

                chestItems.Add(new ChestItemData
                {
                    Position = new Point(posX, posY),
                    Slot = slot,
                    Item = item
                });
            }

            //添加标牌
            int signCount = reader.ReadInt32();
            var signText = new List<Sign>(signCount);
            for (int i = 0; i < signCount; i++)
            {
                int xRel = reader.ReadInt32();
                int yRel = reader.ReadInt32();
                string text = reader.ReadString();

                var sign = new Sign
                {
                    x = originX + xRel,
                    y = originY + yRel,
                    text = text
                };

                signText.Add(sign);
            }

            return new Building
            {
                Origin = new Point(originX, originY),
                Width = width,
                Height = height,
                Tiles = tiles,
                ChestItems = chestItems,
                Signs = signText
            };
        }
    }
    #endregion

    #region 保存剪贴板方法
    internal static void SaveClip(string name, Building clip)
    {
        Directory.CreateDirectory(Paths);
        string filePath = Path.Combine(Paths, $"{name}_cp.map");

        using (var fs = GZipWrite(filePath))
        using (var writer = new BinaryWriter(fs))
        {
            writer.Write(clip.Origin.X);
            writer.Write(clip.Origin.Y);
            writer.Write(clip.Width);
            writer.Write(clip.Height);

            for (int x = 0; x < clip.Width; x++)
            {
                for (int y = 0; y < clip.Height; y++)
                {
                    var tile = clip.Tiles![x, y];
                    writer.Write(tile.bTileHeader);
                    writer.Write(tile.bTileHeader2);
                    writer.Write(tile.bTileHeader3);
                    writer.Write(tile.frameX);
                    writer.Write(tile.frameY);
                    writer.Write(tile.liquid);
                    writer.Write(tile.sTileHeader);
                    writer.Write(tile.type);
                    writer.Write(tile.wall);
                }
            }

            // 保存箱子物品数据
            writer.Write(clip.ChestItems?.Count ?? 0);
            if (clip.ChestItems != null)
            {
                foreach (var data in clip.ChestItems)
                {
                    writer.Write(data.Position.X);
                    writer.Write(data.Position.Y);
                    writer.Write(data.Slot);
                    writer.Write(data.Item?.type ?? 0);
                    writer.Write(data.Item?.netID ?? 0);
                    writer.Write(data.Item?.stack ?? 0);
                    writer.Write(data.Item?.prefix ?? 0);
                }
            }

            // 保存标牌数据
            writer.Write(clip.Signs?.Count ?? 0);
            if (clip.Signs != null)
            {
                foreach (var sign in clip.Signs)
                {
                    writer.Write(sign.x - clip.Origin.X); // 相对坐标
                    writer.Write(sign.y - clip.Origin.Y);
                    writer.Write(sign.text ?? "");
                }
            }
        }
    }
    #endregion

    #region 获取所有已存在的剪贴板名称
    public static List<string> GetAllClipNames()
    {
        if (!Directory.Exists(Map.Paths))
            return new List<string>();

        return Directory.GetFiles(Map.Paths, "*_cp.map")
                        .Select(f => Path.GetFileNameWithoutExtension(f).Replace("_cp", ""))
                        .ToList();
    }
    #endregion

    #region 备份并压缩所有 .dat 文件后删除
    public static void BackupAndDeleteAllDataFiles()
    {
        if (!Directory.Exists(Map.Paths)) return;

        // 构建压缩包保存路径
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string backupFolder = Path.Combine(Map.Paths, $"{timestamp}");
        string zipFilePath = Path.Combine(Map.Paths, $"{timestamp}.zip");

        try
        {
                // 创建临时备份文件夹
                Directory.CreateDirectory(backupFolder);

                // 将所有 .dat 文件复制到备份文件夹
                foreach (var file in Directory.GetFiles(Map.Paths, "*_cp.map"))
                {
                    string destFile = Path.Combine(backupFolder, Path.GetFileName(file));
                    File.Copy(file, destFile, overwrite: true);
                }

                // 压缩文件夹为 .zip
                ZipFile.CreateFromDirectory(backupFolder, zipFilePath, CompressionLevel.SmallestSize, false);

                // 删除临时文件夹（不再需要了）
                Directory.Delete(backupFolder, recursive: true);

                TShock.Log.ConsoleInfo($"已成功备份所有 .map 文件，压缩包保存于:\n {zipFilePath}");

                // 删除原始 .dat 文件
                foreach (var file in Directory.GetFiles(Map.Paths, "*.map"))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        TShock.Log.ConsoleInfo($"删除文件失败: {file}, 错误: {ex.Message}");
                    }
                }

                TShock.Log.ConsoleInfo($"已成功删除所有 .map 文件");
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleInfo($"备份和删除过程中出错: {ex.Message}");
        }
    }

    #endregion
}