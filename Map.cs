using System.IO.Compression;
using System.Text;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Utilities;
using TShockAPI;
using static CreateSpawn.Map;
using static CreateSpawn.Plugin;
using static CreateSpawn.Utils;
using static CreateSpawn.WorldTile;

namespace CreateSpawn;
internal class Map
{
    #region 内部数据结构
    // 内部类 TileData：用于封装一个区域内的所有图格、箱子、实体和标牌数据
    public class TileData
    {
        public Tile[,]? Tiles; // 图格二维数组，[x,y] 对应区域内的图格
        public List<Chest>? Chests; // 箱子列表
        public List<EntityData>? EntData; // 实体列表（如训练假人、物品框等）
        public List<Sign>? Signs; // 标牌列表
    }

    // 内部类 EntityData：用于存储实体的必要信息
    public class EntityData
    {
        public byte Type; // 实体类型（例如 0=训练假人，1=物品框等）
        public short X, Y; // 实体在世界中的坐标
        public byte[]? ExtraData; // 实体的额外二进制数据（用于序列化/反序列化）
    }

    // 内部类 UndoOperation：撤销操作记录，保存操作前后的区域状态
    public class UndoOperation
    {
        public string RegionName { get; set; } = string.Empty; // TShock区域名称
        public Rectangle Area { get; set; } // 操作影响的矩形区域
        public TileData BeforeState { get; set; } = new(); // 操作前的区域数据
        public DateTime Timestamp { get; set; } // 操作时间戳，用于排序或清理旧记录
    }
    #endregion

    #region 撤销栈操作
    /// <summary>
    /// 将玩家的撤销栈保存到文件（GZip 压缩）
    /// </summary>
    public static void SaveUndo(string playerName, Stack<UndoOperation> stack)
    {
        if (!Directory.Exists(RestoreDir)) Directory.CreateDirectory(RestoreDir);
        string path = Path.Combine(RestoreDir, $"{playerName}_undo.bak");
        using var fs = new FileStream(path, FileMode.Create);
        using var gz = new GZipStream(fs, CompressionLevel.Optimal);
        using var writer = new BinaryWriter(gz);
        writer.Write(stack.Count); // 写入栈大小
        foreach (var op in stack)
        {
            writer.Write(op.RegionName ?? "");
            writer.Write(op.Area.X);
            writer.Write(op.Area.Y);
            writer.Write(op.Area.Width);
            writer.Write(op.Area.Height);
            writer.Write(op.Timestamp.Ticks);
            WriteTileData(writer, op.BeforeState);
        }
    }

    /// <summary>
    /// 从文件加载玩家的撤销栈
    /// </summary>
    public static Stack<UndoOperation> LoadUndo(string playerName)
    {
        string path = Path.Combine(RestoreDir, $"{playerName}_undo.bak");
        if (!File.Exists(path)) return new Stack<UndoOperation>();
        using var stream = new GZipStream(new FileStream(path, FileMode.Open), CompressionMode.Decompress);
        using var reader = new BinaryReader(stream);
        int count = reader.ReadInt32();
        var list = new List<UndoOperation>(count);
        for (int i = 0; i < count; i++) list.Add(ReadUndoOp(reader));
        list.Reverse(); // 因为栈是后进先出，从文件读出的顺序是栈底到栈顶，反转后便于 Push/Pop
        return new Stack<UndoOperation>(list);
    }

    /// <summary>
    /// 从 BinaryReader 读取一个 UndoOperation 对象
    /// </summary>
    public static UndoOperation ReadUndoOp(BinaryReader reader)
    {
        return new UndoOperation
        {
            RegionName = reader.ReadString(),
            Area = new Rectangle(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()),
            Timestamp = new DateTime(reader.ReadInt64()),
            BeforeState = ReadTileData(reader)
        };
    }

    /// <summary>
    /// 从玩家的撤销栈弹出一个操作（从文件加载后删除）
    /// </summary>
    public static UndoOperation? PopUndo(string playerName)
    {
        var stack = LoadUndo(playerName);
        if (stack.Count == 0) return null;
        var op = stack.Pop();
        SaveUndo(playerName, stack);
        return op;
    }
    #endregion

    #region 从文件读取建筑
    /// <summary>
    /// 从建筑文件加载 TileData
    /// </summary>
    public static TileData? LoadClip(string name)
    {
        string path = GetClipPath(name);
        if (!File.Exists(path)) return null;
        using var baseStream = new GZipStream(new FileStream(path, FileMode.Open), CompressionMode.Decompress);
        using var reader = new BinaryReader(baseStream);
        return ReadTileData(reader);
    }
    #endregion

    #region 标牌序列化辅助
    /// <summary>
    /// 将标牌列表保存为 GZip 压缩的 JSON 文件
    /// </summary>
    public static void SaveSigns(string path, List<Sign> signs)
    {
        string json = JsonConvert.SerializeObject(signs, Formatting.None);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        using var fs = new FileStream(path, FileMode.Create);
        using var gz = new GZipStream(fs, CompressionLevel.Optimal);
        gz.Write(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// 从 GZip 压缩的 JSON 文件加载标牌列表
    /// </summary>
    public static List<Sign> LoadSigns(string path)
    {
        using var stream = new GZipStream(new FileStream(path, FileMode.Open), CompressionMode.Decompress);
        using var sr = new StreamReader(stream);
        string json = sr.ReadToEnd();
        return JsonConvert.DeserializeObject<List<Sign>>(json) ?? new List<Sign>();
    }
    #endregion

    #region 克隆方法
    /// <summary>
    /// 深拷贝箱子，并可选择偏移坐标
    /// </summary>
    public static Chest CloneChest(Chest src, int offX = 0, int offY = 0)
    {
        var chest = new Chest(0, src.x + offX, src.y + offY, src.bankChest, src.maxItems)
        {
            name = src.name ?? "",
            item = new Item[src.maxItems]
        };
        for (int i = 0; i < src.maxItems; i++)
            chest.item[i] = src.item[i]?.Clone() ?? new Item();
        return chest;
    }

    /// <summary>
    /// 从 TileEntity 创建 EntityData，并可选择偏移坐标
    /// </summary>
    public static EntityData CloneEntity(TileEntity src, int offX = 0, int offY = 0)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        TileEntity.Write(bw, src);
        return new EntityData
        {
            Type = src.type,
            X = (short)(src.Position.X + offX),
            Y = (short)(src.Position.Y + offY),
            ExtraData = ms.ToArray()
        };
    }

    /// <summary>
    /// 深拷贝标牌，并可选择偏移坐标
    /// </summary>
    public static Sign CloneSign(Sign src, int offX = 0, int offY = 0)
    {
        return new Sign
        {
            x = src.x + offX,
            y = src.y + offY,
            text = src.text
        };
    }
    #endregion

    #region 保存建筑到文件
    /// <summary>
    /// 将指定矩形区域的图格、箱子、实体、标牌保存为建筑文件（相对坐标）
    /// </summary>
    public static void SaveBuild(TSPlayer plr, string name, Rectangle rect)
    {
        var clip = GetTileData(rect); // 获取世界区域数据（绝对坐标）

        // 转换为相对坐标（相对于矩形左上角）直接调用 CloneOff 传入负偏移
        var relClip = CloneOff(clip, -rect.X, -rect.Y);

        string path = GetClipPath(name);
        using var fs = new FileStream(path, FileMode.Create);
        using var gz = new GZipStream(fs, CompressionLevel.Optimal);
        using var writer = new BinaryWriter(gz);
        WriteTileData(writer, relClip);

        plr.SendMessage($"已保存建筑 '{name}' ({rect.Width}x{rect.Height})", color);
        // 清除玩家的区域点标记（来自 TShock 的 TempPoints）
        plr.TempPoints[0] = Point.Zero;
        plr.TempPoints[1] = Point.Zero;
    }
    #endregion

    #region 保存世界快照（自动备份调用）
    /// <summary>
    /// 保存当前世界的快照（图格和标牌）到指定目录，用于自动备份
    /// </summary>
    public static void SaveSnapshot(TSPlayer plr, bool showMag, string worldName, string SaveDir)
    {
        string tmpPath = Path.GetTempFileName(); // 临时文件
        TileSnapshot.Create();    // 创建世界快照
        TileSnapshot.Save(tmpPath); // 保存到临时文件
        TileSnapshot.Clear();      // 清理快照

        string snapPath = Path.Combine(SaveDir, $"{worldName}{TwsExt}");
        using (var fs = new FileStream(snapPath, FileMode.Create))
        using (var gz = new GZipStream(fs, CompressionLevel.Optimal))
        using (var tmpFs = File.OpenRead(tmpPath))
        {
            tmpFs.CopyTo(gz); // 压缩临时文件到目标
        }
        File.Delete(tmpPath);
        if (showMag) plr.SendMessage($"已保存世界快照: {worldName}{TwsExt} (GZIP压缩)", color2);

        // 保存标牌
        string signPath = Path.Combine(SaveDir, $"{worldName}{SgnExt}");
        var signs = new List<Sign>();
        for (int i = 0; i < Main.sign.Length; i++)
        {
            var s = Main.sign[i];
            if (s != null && !string.IsNullOrEmpty(s.text))
                signs.Add(new Sign { x = s.x, y = s.y, text = s.text });
        }
        SaveSigns(signPath, signs);
        if (showMag) plr.SendMessage($"已保存标牌: {worldName}{SgnExt} (GZIP压缩)", color2);
    }
    #endregion

    #region TileData 序列化辅助
    /// <summary>
    /// 将 TileData 对象写入 BinaryWriter（用于保存到文件）
    /// </summary>
    public static void WriteTileData(BinaryWriter writer, TileData data)
    {
        // 写入图格数组维度
        if (data.Tiles == null) { writer.Write(0); writer.Write(0); }
        else
        {
            int w = data.Tiles.GetLength(0);
            int h = data.Tiles.GetLength(1);
            writer.Write(w); writer.Write(h);
            WriteTiles(writer, data.Tiles); // 压缩写入图格
        }

        // 写入箱子
        writer.Write(data.Chests?.Count ?? 0);
        if (data.Chests != null)
        {
            foreach (var c in data.Chests)
            {
                writer.Write(c.x); writer.Write(c.y); writer.Write(c.name ?? ""); writer.Write(c.maxItems);
                for (int i = 0; i < c.maxItems; i++)
                {
                    var item = c.item[i];
                    writer.Write(item?.type ?? 0); writer.Write(item?.stack ?? 0); writer.Write(item?.prefix ?? (byte)0);
                }
            }
        }

        // 写入实体
        writer.Write(data.EntData?.Count ?? 0);
        if (data.EntData != null)
        {
            foreach (var e in data.EntData)
            {
                writer.Write(e.Type); writer.Write(e.X); writer.Write(e.Y);
                writer.Write(e.ExtraData?.Length ?? 0);
                if (e.ExtraData != null) writer.Write(e.ExtraData);
            }
        }

        // 写入标牌
        writer.Write(data.Signs?.Count ?? 0);
        if (data.Signs != null)
        {
            foreach (var s in data.Signs)
            {
                writer.Write(s.x); writer.Write(s.y); writer.Write(s.text ?? "");
            }
        }
    }

    /// <summary>
    /// 从 BinaryReader 读取一个 TileData 对象
    /// </summary>
    private static TileData ReadTileData(BinaryReader reader)
    {
        var data = new TileData();
        int w = reader.ReadInt32(); int h = reader.ReadInt32();
        if (w > 0 && h > 0)
        {
            data.Tiles = new Tile[w, h];
            ReadTiles(reader, data.Tiles, w, h); // 解压图格
        }

        int chestCount = reader.ReadInt32();
        if (chestCount > 0)
        {
            data.Chests = new List<Chest>();
            for (int i = 0; i < chestCount; i++)
            {
                int cx = reader.ReadInt32();
                int cy = reader.ReadInt32();
                string cname = reader.ReadString();
                int max = reader.ReadInt32();
                var chest = new Chest(0, cx, cy, false, max)
                {
                    name = cname,
                    item = new Item[max]
                };

                for (int s = 0; s < max; s++)
                {
                    int type = reader.ReadInt32();
                    int stack = reader.ReadInt32();
                    byte prefix = reader.ReadByte();
                    var item = new Item();
                    item.SetDefaults(type);
                    item.stack = stack;
                    item.prefix = prefix;
                    chest.item[s] = item;
                }
                data.Chests.Add(chest);
            }
        }

        int entCount = reader.ReadInt32();
        if (entCount > 0)
        {
            data.EntData = new List<EntityData>();
            for (int i = 0; i < entCount; i++)
            {
                var ent = new EntityData
                {
                    Type = reader.ReadByte(),
                    X = reader.ReadInt16(),
                    Y = reader.ReadInt16()
                };
                int extraLen = reader.ReadInt32();
                ent.ExtraData = reader.ReadBytes(extraLen);
                data.EntData.Add(ent);
            }
        }

        int signCount = reader.ReadInt32();
        if (signCount > 0)
        {
            data.Signs = new List<Sign>();
            for (int i = 0; i < signCount; i++)
                data.Signs.Add(new Sign
                {
                    x = reader.ReadInt32(),
                    y = reader.ReadInt32(),
                    text = reader.ReadString()
                });
        }
        return data;
    }
    #endregion

    #region 压缩图格（使用RLE压缩写入图格二维数组）
    /// <summary>
    /// 使用RLE压缩写入图格二维数组,参考原版NetMessage.CompressTileBlock方法
    /// </summary>
    private static void WriteTiles(BinaryWriter writer, Tile[,] tiles)
    {
        int width = tiles.GetLength(0);
        int height = tiles.GetLength(1);

        short repeatCount = 0;          // 重复计数器（原版num4）
        int dataIdx = 4;                 // 数据起始索引（原版num5）
        int flagStartIdx = 3;            // 标志位起始索引（原版num6）
        byte flags1 = 0;                 // 第一层标志（原版b）
        byte[] buffer = new byte[16];    // 缓冲区

        Tile? prevTile = null;             // 前一个图格

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Tile curTile = tiles[x, y];

                // 如果当前图格与前一个相同且允许批量压缩，则增加计数并跳过
                if (prevTile != null && curTile.isTheSameAs(prevTile) && TileID.Sets.AllowsSaveCompressionBatching[curTile.type])
                {
                    repeatCount++;
                    continue;
                }

                // 处理前一个图格的重复计数
                if (prevTile != null)
                {
                    if (repeatCount > 0)
                    {
                        buffer[dataIdx] = (byte)(repeatCount & 0xFF);
                        dataIdx++;
                        if (repeatCount > 255)
                        {
                            flags1 |= 0x80; // 设置高位标志
                            buffer[dataIdx] = (byte)((repeatCount >> 8) & 0xFF);
                            dataIdx++;
                        }
                        else
                        {
                            flags1 |= 0x40;
                        }
                    }

                    // 写入前一个图格的数据（标志位+内容）
                    buffer[flagStartIdx] = flags1;
                    writer.Write(buffer, flagStartIdx, dataIdx - flagStartIdx);

                    repeatCount = 0;
                }

                // 开始处理当前图格
                dataIdx = 4;
                byte flags1cur = 0, flags2cur = 0, flags3cur = 0, flags4cur = 0; // 对应原版b, b4, b3, b2

                // 检查活动块
                if (curTile.active())
                {
                    flags1cur |= 2;
                    buffer[dataIdx] = (byte)curTile.type;
                    dataIdx++;
                    if (curTile.type > 255)
                    {
                        buffer[dataIdx] = (byte)(curTile.type >> 8);
                        dataIdx++;
                        flags1cur |= 0x20;
                    }

                    // 原版在这里会检查箱子/标牌/实体，直接跳过

                    // 写入帧数据（如果图格重要）
                    if (Main.tileFrameImportant[curTile.type])
                    {
                        buffer[dataIdx] = (byte)(curTile.frameX & 0xFF);
                        dataIdx++;
                        buffer[dataIdx] = (byte)((curTile.frameX >> 8) & 0xFF);
                        dataIdx++;
                        buffer[dataIdx] = (byte)(curTile.frameY & 0xFF);
                        dataIdx++;
                        buffer[dataIdx] = (byte)((curTile.frameY >> 8) & 0xFF);
                        dataIdx++;
                    }

                    // 块颜色
                    if (curTile.color() != 0)
                    {
                        flags3cur |= 8;
                        buffer[dataIdx] = curTile.color();
                        dataIdx++;
                    }
                }

                // 墙体
                if (curTile.wall != 0)
                {
                    flags1cur |= 4;
                    buffer[dataIdx] = (byte)curTile.wall;
                    dataIdx++;
                    if (curTile.wallColor() != 0)
                    {
                        flags3cur |= 0x10;
                        buffer[dataIdx] = curTile.wallColor();
                        dataIdx++;
                    }
                }

                // 液体
                if (curTile.liquid != 0)
                {
                    if (!curTile.shimmer())
                    {
                        if (curTile.lava())
                            flags1cur |= 0x10;
                        else if (curTile.honey())
                            flags1cur |= 0x18;
                        else
                            flags1cur |= 0x08;
                    }
                    else
                    {
                        flags3cur |= 0x80;
                        flags1cur |= 0x08;
                    }
                    buffer[dataIdx] = curTile.liquid;
                    dataIdx++;
                }

                // 电线
                if (curTile.wire()) flags4cur |= 2;
                if (curTile.wire2()) flags4cur |= 4;
                if (curTile.wire3()) flags4cur |= 8;

                // 半砖/斜坡
                int slopeFlag = curTile.halfBrick() ? 16 : (curTile.slope() != 0 ? (curTile.slope() + 1) << 4 : 0);
                flags4cur |= (byte)slopeFlag;

                // 制动器
                if (curTile.actuator()) flags3cur |= 2;
                if (curTile.inActive()) flags3cur |= 4;
                if (curTile.wire4()) flags3cur |= 0x20;

                // 墙体类型大于255
                if (curTile.wall > 255)
                {
                    buffer[dataIdx] = (byte)(curTile.wall >> 8);
                    dataIdx++;
                    flags3cur |= 0x40;
                }

                // 隐形/全亮
                if (curTile.invisibleBlock()) flags2cur |= 2;
                if (curTile.invisibleWall()) flags2cur |= 4;
                if (curTile.fullbrightBlock()) flags2cur |= 8;
                if (curTile.fullbrightWall()) flags2cur |= 0x10;

                // 组装标志位链（从后往前）
                flagStartIdx = 3;
                if (flags2cur != 0)
                {
                    flags3cur |= 1; // 标记存在flags2
                    buffer[flagStartIdx] = flags2cur;
                    flagStartIdx--;
                }
                if (flags3cur != 0)
                {
                    flags4cur |= 1; // 标记存在flags3
                    buffer[flagStartIdx] = flags3cur;
                    flagStartIdx--;
                }
                if (flags4cur != 0)
                {
                    flags1cur |= 1; // 标记存在flags4
                    buffer[flagStartIdx] = flags4cur;
                    flagStartIdx--;
                }
                // 将flags1放入最终位置
                buffer[flagStartIdx] = flags1cur;

                prevTile = curTile;
                flags1 = flags1cur; // 保存用于后续重复计数写入
            }


        // 处理最后一组
        if (prevTile != null)
        {
            if (repeatCount > 0)
            {
                buffer[dataIdx] = (byte)(repeatCount & 0xFF);
                dataIdx++;
                if (repeatCount > 255)
                {
                    flags1 |= 0x80;
                    buffer[dataIdx] = (byte)((repeatCount >> 8) & 0xFF);
                    dataIdx++;
                }
                else
                {
                    flags1 |= 0x40;
                }
            }

            buffer[flagStartIdx] = flags1;
            writer.Write(buffer, flagStartIdx, dataIdx - flagStartIdx);
        }
    }

    /// <summary>
    /// 解压缩图格到目标二维数组,参考原版NetMessage.DecompressTileBlock方法
    /// </summary>
    private static void ReadTiles(BinaryReader reader, Tile[,] tiles, int width, int height)
    {
        int repeat = 0;
        Tile? prevTile = null;

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (repeat > 0)
                {
                    // 重复前一个图格
                    repeat--;
                    if (tiles[x, y] == null)
                        tiles[x, y] = new Tile(prevTile);
                    else
                        tiles[x, y].CopyFrom(prevTile);
                    continue;
                }

                // 读取标志位
                byte flags1 = reader.ReadByte();
                bool hasFlags4 = (flags1 & 1) == 1;
                byte flags4 = 0;
                if (hasFlags4)
                    flags4 = reader.ReadByte();

                bool hasFlags3 = hasFlags4 && (flags4 & 1) == 1;
                byte flags3 = 0;
                if (hasFlags3)
                    flags3 = reader.ReadByte();

                bool hasFlags2 = hasFlags3 && (flags3 & 1) == 1;
                byte flags2 = 0;
                if (hasFlags2)
                    flags2 = reader.ReadByte();

                // 创建当前图格
                Tile curTile = tiles[x, y];
                if (curTile == null)
                {
                    curTile = new Tile();
                    tiles[x, y] = curTile;
                }
                else
                {
                    curTile.ClearEverything();
                }

                // 解析活动块
                if ((flags1 & 2) == 2)
                {
                    curTile.active(true);
                    ushort type;
                    if ((flags1 & 0x20) == 0x20)
                    {
                        byte low = reader.ReadByte();
                        byte high = reader.ReadByte();
                        type = (ushort)((high << 8) | low);
                    }
                    else
                    {
                        type = reader.ReadByte();
                    }
                    curTile.type = type;

                    if (Main.tileFrameImportant[type])
                    {
                        curTile.frameX = reader.ReadInt16();
                        curTile.frameY = reader.ReadInt16();
                    }
                    else
                    {
                        curTile.frameX = -1;
                        curTile.frameY = -1;
                    }

                    if ((flags3 & 8) == 8)
                        curTile.color(reader.ReadByte());
                }

                // 墙体
                if ((flags1 & 4) == 4)
                {
                    curTile.wall = reader.ReadByte();
                    if ((flags3 & 0x10) == 0x10)
                        curTile.wallColor(reader.ReadByte());
                }

                // 液体
                byte liquidType = (byte)((flags1 & 0x18) >> 3);
                if (liquidType != 0)
                {
                    curTile.liquid = reader.ReadByte();
                    if ((flags3 & 0x80) == 0x80)
                        curTile.shimmer(true);
                    else if (liquidType == 2)
                        curTile.lava(true);
                    else if (liquidType == 3)
                        curTile.honey(true);
                }

                // 电线、斜坡等（flags4）
                if (hasFlags4)
                {
                    if ((flags4 & 2) == 2) curTile.wire(true);
                    if ((flags4 & 4) == 4) curTile.wire2(true);
                    if ((flags4 & 8) == 8) curTile.wire3(true);
                    byte slope = (byte)((flags4 & 0x70) >> 4);
                    if (slope != 0 && Main.tileSolid[curTile.type])
                    {
                        if (slope == 1)
                            curTile.halfBrick(true);
                        else
                            curTile.slope((byte)(slope - 1));
                    }
                }

                // 制动器、隐形等（flags3）
                if (hasFlags3)
                {
                    if ((flags3 & 2) == 2) curTile.actuator(true);
                    if ((flags3 & 4) == 4) curTile.inActive(true);
                    if ((flags3 & 0x20) == 0x20) curTile.wire4(true);
                    if ((flags3 & 0x40) == 0x40)
                    {
                        byte highWall = reader.ReadByte();
                        curTile.wall = (ushort)((highWall << 8) | curTile.wall);
                    }
                }

                // 隐形/全亮（flags2）
                if (hasFlags2)
                {
                    if ((flags2 & 2) == 2) curTile.invisibleBlock(true);
                    if ((flags2 & 4) == 4) curTile.invisibleWall(true);
                    if ((flags2 & 8) == 8) curTile.fullbrightBlock(true);
                    if ((flags2 & 0x10) == 0x10) curTile.fullbrightWall(true);
                }

                // 读取重复计数
                repeat = (flags1 & 0xC0) switch
                {
                    0x40 => reader.ReadByte(),
                    0x80 => reader.ReadInt16(),
                    _ => 0
                };

                prevTile = curTile;
            }
    }
    #endregion
}