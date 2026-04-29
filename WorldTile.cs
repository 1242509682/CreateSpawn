using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Utilities;
using TShockAPI;
using Terraria.GameContent.Tile_Entities;
using static CreateSpawn.Map;
using static CreateSpawn.MyCmd;
using static CreateSpawn.PlayerState;
using static CreateSpawn.Plugin;
using static CreateSpawn.Utils;

namespace CreateSpawn;

/// <summary>
/// 世界图格操作类，提供地图快照修复、建筑复制粘贴、撤销等功能。
/// </summary>
public static class WorldTile
{
    #region 精密线控仪事件
    /// <summary>
    /// 处理 MassWireOperation 事件（玩家使用精密线控仪时触发）
    /// </summary>
    internal static void OnWire(object? sender, GetDataHandlers.MassWireOperationEventArgs e)
    {
        var plr = e.Player;
        if (plr is null || !plr.Active) return;

        var Mydata = GetData(plr.Name);
        int toolMode = e.ToolMode;

        // 计算框选区域的边界（确保 x1<=x2, y1<=y2）
        int x1 = Math.Min(e.StartX, e.EndX);
        int y1 = Math.Min(e.StartY, e.EndY);
        int x2 = Math.Max(e.StartX, e.EndX);
        int y2 = Math.Max(e.StartY, e.EndY);
        x2++; y2++; // 包含终点图格（通常拉线时是点对点，这里扩展到包含整个矩形）
        Rectangle rect = new Rectangle(x1, y1, x2 - x1, y2 - y1);

        if (Mydata.rwFix) // 修复模式
        {
            string snapPath = Mydata.rwSnap;
            string signPath = Mydata.rwSign;
            if (string.IsNullOrEmpty(snapPath) || !File.Exists(snapPath))
            {
                SendMess(plr, $"[{PluginName}] 快照文件已丢失，请重新选择备份");
                Mydata.rwFix = false;
                Mydata.rwSnap = string.Empty;
                Mydata.rwSign = string.Empty;
                e.Handled = true;
                return;
            }

            SendMess(plr, $"\n正在从备份恢复 ({x1},{y1}) => ({x2},{y2})");
            Fix(plr, Mydata, snapPath, signPath, rect, e);
        }
        else if (!string.IsNullOrEmpty(Mydata.rwPaste))
        {
            Paste(plr, Mydata, e.StartX, e.StartY, e.EndX, e.EndY, e);
        }
        else if (!string.IsNullOrEmpty(Mydata.rwCopy))
        {
            // 检查是否有待保存的建筑
            SaveBuild(plr, Mydata.rwCopy, rect);
            Mydata.rwCopy = string.Empty; // 清空状态
            e.Handled = true;
        }

        if (Mydata.rw != 0)
        {
            // 根据框选方向决定朝向（用于方块、斜坡、半砖）
            int dir = (e.EndX > e.StartX) ? 1 : -1;
            int op = Mydata.rw;
            int a1 = Mydata.rwA1;
            int a2 = Mydata.rwA2;
            int a3 = Mydata.rwA3;

            // 斜坡：1右斜坡 2左斜坡
            if (op == 20)
            {
                a1 = (dir == 1) ? 1 : 2;
                Mydata.rwA1 = a1;
            }
            // 半砖：3右半砖 4左半砖
            else if (op == 21)
            {
                a1 = (dir == 1) ? 3 : 4;
                Mydata.rwA1 = a1;
            }

            // 覆盖方向（用于方块放置的朝向）
            Mydata.rwDir = dir;

            // 保存撤销状态等（不变）
            var beforeState = GetTileData(rect);
            var stack = LoadUndo(plr.Name);
            stack.Push(new UndoOperation { Area = rect, BeforeState = beforeState, Timestamp = DateTime.Now });
            SaveUndo(plr.Name, stack);

            var sw = Stopwatch.StartNew();
            Task.Run(() => ExecuteEdit(rect, op, a1, a2, a3, dir, toolMode)).ContinueWith(_ =>
            {
                sw.Stop();
                AnimMag.Add(rect); // 显示区域动画
                SendMess(plr, $"操作完成，用时 {sw.ElapsedMilliseconds} ms\n撤销操作：/{cmd} bk");
                Mydata.rw = 0;
                Mydata.rwA1 = Mydata.rwA2 = Mydata.rwA3 = 0;
                Mydata.rwToolMode = 0;
            });
            e.Handled = true;
        }

        if (Mydata.Mode != 0)
        {
            SetRegion(e, plr, Mydata, x1, y1, rect);
        }
    }
    #endregion

    #region 处理修复模式
    /// <summary>
    /// 处理修复模式：从快照恢复指定区域，删除重叠区域，保存撤销状态，异步执行修复
    /// </summary>
    private static void Fix(TSPlayer plr, MyData Mydata, string snapPath, string signPath, Rectangle rect,
                                   GetDataHandlers.MassWireOperationEventArgs e)
    {
        // 1. 删除矩形内的所有现有区域（如果配置开启）
        if (Config.DelRegForFix)
        {
            var InRegions = TShock.Regions.Regions.Where(r => r.InArea(rect)).ToList();
            foreach (var reg in InRegions)
            {
                TShock.Regions.DeleteRegion(reg.Name);
                if (Plugin.Config.CreatedReg.Contains(reg.Name))
                    Plugin.Config.CreatedReg.Remove(reg.Name);
            }

            if (InRegions.Any())
            {
                SendMess(plr, $"已删除 {InRegions.Count} 个与修复区域重叠的区域");
                Config.Write();
            }
        }

        // 2. 从快照文件中读取指定区域的数据
        var data = ReadWTile(snapPath, rect, signPath);
        if (data == null)
        {
            SendMess(plr, $"[{PluginName}] 无法读取快照数据，修复取消");

            // 清理临时文件
            if (File.Exists(snapPath)) File.Delete(snapPath);
            if (File.Exists(signPath)) File.Delete(signPath);
            Mydata.rwFix = false;
            Mydata.rwSnap = string.Empty;
            Mydata.rwSign = string.Empty;
            e.Handled = true;
            return;
        }

        // 3. 保存当前区域状态以便撤销
        var beforeState = GetTileData(rect);
        var stack = LoadUndo(plr.Name);
        stack.Push(new UndoOperation { Area = rect, BeforeState = beforeState, Timestamp = DateTime.Now });
        SaveUndo(plr.Name, stack);

        // 4. 异步执行修复
        var count = 0;
        var sw = Stopwatch.StartNew();

        Task.Run(() =>
        {
            KillAll(rect.Left, rect.Right, rect.Top, rect.Bottom);
            count = FixTile(rect, data, count);
        }).ContinueWith(_ =>
        {
            FixItem(data, plr);

            // 删除临时快照文件
            if (File.Exists(snapPath)) File.Delete(snapPath);
            if (File.Exists(signPath)) File.Delete(signPath);

            // 清除玩家的修复状态
            Mydata.rwFix = false;
            Mydata.rwSnap = string.Empty;
            Mydata.rwSign = string.Empty;
            sw.Stop();
            AnimMag.Add(rect);
            SendMess(plr, $"已恢复区域: {count} 个图格, 用时 {sw.ElapsedMilliseconds} ms\n撤销操作:/{cmd} bk");
        });

        // 5. 标记事件已处理，阻止后续逻辑
        e.Handled = true;
    }
    #endregion

    #region 处理粘贴模式
    /// <summary>
    /// 处理粘贴模式：根据玩家框选的起点和终点，计算粘贴位置并执行粘贴
    /// </summary>
    private static void Paste(TSPlayer plr, MyData Mydata, int startX, int startY, int endX, int endY, GetDataHandlers.MassWireOperationEventArgs e)
    {
        string buildName = Mydata.rwPaste;
        Mydata.rwPaste = string.Empty; // 清空状态，只执行一次

        // 加载建筑
        var clip = LoadClip(buildName);
        if (clip == null)
        {
            SendMess(plr, $"建筑 '{buildName}' 加载失败");
            e.Handled = true;
            return;
        }

        int w = clip.Tiles?.GetLength(0) ?? 0;
        int h = clip.Tiles?.GetLength(1) ?? 0;
        if (w == 0 || h == 0)
        {
            SendMess(plr, "建筑数据无效");
            e.Handled = true;
            return;
        }

        Rectangle rect;

        // 单点：起点=终点 → 居中
        if (startX == endX && startY == endY)
        {
            int baseX = startX - w / 2;
            int baseY = startY - h / 2;
            rect = new Rectangle(baseX, baseY, w, h);
            SendMess(plr, "模式：中心点");
        }
        // 垂直线：X相同，Y不同
        else if (startX == endX && startY != endY)
        {
            // 水平居中
            int baseX = startX - w / 2;
            int baseY;
            if (startY < endY) // 从上往下拉 → 建筑顶部对齐起点（中上）
                baseY = startY;
            else               // 从下往上拉 → 建筑底部对齐起点（中下）
                baseY = startY - h + 1;
            rect = new Rectangle(baseX, baseY, w, h);
            SendMess(plr, "模式：垂直线（居中）");
        }
        // 水平线：Y相同，X不同
        else if (startY == endY && startX != endX)
        {
            // 垂直居中
            int baseY = startY - h / 2;
            int baseX;
            if (startX < endX) // 从左往右拉 → 建筑左侧对齐起点
                baseX = startX;
            else               // 从右往左拉 → 建筑右侧对齐起点
                baseX = startX - w + 1;
            rect = new Rectangle(baseX, baseY, w, h);
            SendMess(plr, "模式：水平线（居中）");
        }
        else
        {
            // 矩形框选：四角对齐逻辑
            bool rightDir = endX > startX;
            bool downDir = endY > startY;
            int baseX, baseY;
            if (rightDir && downDir)        // 起点左上
            {
                baseX = startX;
                baseY = startY;
            }
            else if (!rightDir && downDir)  // 起点右上
            {
                baseX = startX - w + 1;
                baseY = startY;
            }
            else if (rightDir && !downDir)  // 起点左下
            {
                baseX = startX;
                baseY = startY - h + 1;
            }
            else
            {
                baseX = startX - w + 1;     // 起点右下
                baseY = startY - h + 1;
            }
            rect = new Rectangle(baseX, baseY, w, h);
            SendMess(plr, "模式：斜角对齐");
        }

        // 边界检查
        if (rect.X < 0 || rect.X + w >= Main.maxTilesX ||
            rect.Y < 0 || rect.Y + h >= Main.maxTilesY)
        {
            SendMess(plr, "建筑超出世界边界，已取消粘贴");
            e.Handled = true;
            return;
        }

        // 检查是否与现有区域相交（只检查，不删除）
        if (TShock.Regions.Regions.Any(r => r.Area.Intersects(rect)))
        {
            SendMess(plr, "粘贴区域与其他区域重叠，已取消");
            e.Handled = true;
            return;
        }

        SendMess(plr, $"正在粘贴建筑 '{buildName}' 到矩形区域 ({rect.X},{rect.Y}) 尺寸 {w}x{h}");

        // 自动创建区域
        string regName = $"{plr.Name}_{DateTime.Now:yyyyMMddHHmmss}";
        if (Config.CreateRegion)
        {
            if (!TShock.Regions.AddRegion(rect.X, rect.Y, w, h, regName, plr.Name, Main.worldID.ToString()))
            {
                SendMess(plr, "自动创建区域失败，粘贴已取消");
                e.Handled = true;
                return;
            }
            TShock.Regions.SetRegionState(regName, true);
            if (!Config.CreatedReg.Contains(regName))
            {
                Config.CreatedReg.Add(regName);
                Config.Write();
            }
        }

        // 保存撤销状态
        var beforeState = GetTileData(rect);
        var stack = LoadUndo(plr.Name);
        stack.Push(new UndoOperation
        {
            RegionName = regName,
            Area = rect,
            BeforeState = beforeState,
            Timestamp = DateTime.Now
        });
        SaveUndo(plr.Name, stack);

        // 偏移建筑数据到 rect 左上角
        var data = CloneOff(clip, rect.X, rect.Y);

        int count = 0;
        var sw = Stopwatch.StartNew();

        Task.Run(() =>
        {
            KillAll(rect.Left, rect.Right - 1, rect.Top, rect.Bottom - 1);
            count = FixTile(rect, data, count);
        }).ContinueWith(_ =>
        {
            FixItem(data, plr);
            sw.Stop();
            AnimMag.Add(rect);
            SendMess(plr, $"粘贴 '{buildName}' 完成！已粘贴 {count} 个图格，" +
                          $"用时 {sw.ElapsedMilliseconds} ms\n" +
                          $"撤销操作：/{MyCmd.cmd} bk");
        });

        e.Handled = true;
    }
    #endregion

    #region 批量图格操作指令
    public static void TileOp(CommandArgs args, TSPlayer plr)
    {
        if (args.Parameters.Count < 2)
        {
            var sb = new StringBuilder();
            sb.AppendLine("\n《编辑列表》");
            sb.AppendLine($"1清理{Icon(ItemID.Wood)} 2填充{Icon(ItemID.Wood)} 3替换{Icon(ItemID.Wood)} 4覆盖{Icon(ItemID.Wood)} 5涂装{Icon(ItemID.Wood)}");
            sb.AppendLine($"6清理{Icon(ItemID.WoodWall)} 7填充{Icon(ItemID.WoodWall)} 8替换{Icon(ItemID.WoodWall)} 9覆盖{Icon(ItemID.WoodWall)}  10涂装{Icon(ItemID.WoodWall)}");
            sb.AppendLine($"11清理涂装{Icon(ItemID.WhitePaint)} 12全部涂装{Icon(ItemID.WhitePaint)} 13虚化切换{Icon(ItemID.ActuationRod)}");
            sb.AppendLine($"14清理液体{Icon(ItemID.SuperAbsorbantSponge)} 15放{Icon(ItemID.WaterBucket)} 16放{Icon(ItemID.LavaBucket)} 17放{Icon(ItemID.HoneyBucket)} 18放{Icon(ItemID.BottomlessShimmerBucket)}");
            sb.AppendLine($"19电路修改{Icon(ItemID.WireKite)} 20斜坡{Icon(ItemID.Wood)} 21半砖{Icon(ItemID.Wood)} 22全砖{Icon(ItemID.Wood)} 23清理所有{Icon(ItemID.Dynamite)}");

            sb.AppendLine($"\n范围编辑图格: /{cmd} t <编号>");
            sb.AppendLine($"撤销编辑操作: /{cmd} bk");
            SendMess(plr, sb.ToString());
            return;
        }

        if (!int.TryParse(args.Parameters[1], out int op) || op < 1 || op > 23)
        {
            SendMess(plr, "操作编号为 1-23");
            return;
        }

        var sel = plr.SelectedItem;
        switch (op)
        {
            case 1: SetOpMode(plr, 1); break;
            case 2:
            case 3:
            case 4:
                if (sel.createTile < 0) { SendMess(plr, "请手持需要放置的方块"); return; }
                SetOpMode(plr, op, sel.createTile, sel.placeStyle);
                break;
            case 5:
                byte paintId; bool isPaint;
                if (sel.paint > 0) { paintId = sel.paint; isPaint = true; }
                else if (sel.paintCoating > 0) { paintId = sel.paintCoating; isPaint = false; }
                else { SendMess(plr, "请手持油漆或涂料"); return; }
                SetOpMode(plr, 5, paintId, isPaint ? 1 : 0);
                break;
            case 6: SetOpMode(plr, 6); break;
            case 7: // 填充墙壁（保留原有）
                if (sel.createWall < 0) { SendMess(plr, "请手持需要放置的墙壁"); return; }
                SetOpMode(plr, 7, sel.createWall);
                break;

            case 8: // 替换墙壁
                if (sel.createWall < 0) { SendMess(plr, "请手持需要放置的墙壁"); return; }
                SetOpMode(plr, 8, sel.createWall);
                break;

            case 9: // 覆盖墙壁（清后放）
                if (sel.createWall < 0) { SendMess(plr, "请手持需要放置的墙壁"); return; }
                SetOpMode(plr, 9, sel.createWall);
                break;
            case 10: // 涂装墙壁
                if (sel.paint > 0) { paintId = sel.paint; isPaint = true; }
                else if (sel.paintCoating > 0) { paintId = sel.paintCoating; isPaint = false; }
                else { SendMess(plr, "请手持油漆或涂料"); return; }
                SetOpMode(plr, 10, paintId, isPaint ? 1 : 0);
                break;
            case 11: SetOpMode(plr, 11); break; // 清理涂装
            case 12: // 涂装所有
                byte paintIdAll; bool isPaintAll;
                if (sel.paint > 0) { paintIdAll = sel.paint; isPaintAll = true; }
                else if (sel.paintCoating > 0) { paintIdAll = sel.paintCoating; isPaintAll = false; }
                else { SendMess(plr, "请手持油漆或涂料"); return; }
                SetOpMode(plr, 12, paintIdAll, isPaintAll ? 1 : 0);
                break;
            case 13: SetOpMode(plr, 13); break; // 虚化切换
            case 14: SetOpMode(plr, 14); break; // 清理液体
            case 15: SetOpMode(plr, 15); break; // 放水
            case 16: SetOpMode(plr, 16); break; // 放岩浆
            case 17: SetOpMode(plr, 17); break; // 放蜂蜜
            case 18: SetOpMode(plr, 18); break; // 放微光
            case 19: SetOpMode(plr, 19); break; // 电路修改
            case 20: // 斜坡
                int slopeType = (plr.TPlayer.direction == 1) ? 1 : 2;
                SetOpMode(plr, 20, slopeType);
                break;
            case 21: // 半砖
                int halfType = (plr.TPlayer.direction == 1) ? 3 : 4;
                SetOpMode(plr, 21, halfType);
                break;
            case 22: SetOpMode(plr, 22); break; // 全砖
            case 23: SetOpMode(plr, 23); break; // 清理所有
        }
    }
    #endregion

    #region 设置统一图格操作模式
    public static void SetOpMode(TSPlayer plr, int op, int arg1 = 0, int arg2 = 0, int arg3 = 0)
    {
        var data = GetData(plr.Name);
        data.rw = op;
        data.rwA1 = arg1;
        data.rwA2 = arg2;
        data.rwA3 = arg3;
        data.rwDir = plr.TPlayer.direction;

        string msg = op switch
        {
            1 => $"清理{Icon(ItemID.Wood)}",
            2 => $"填充{Icon(ItemID.Wood)}(根据玩家朝向)",
            3 => $"替换{Icon(ItemID.Wood)}(根据玩家朝向)",
            4 => $"覆盖{Icon(ItemID.Wood)}(根据玩家朝向)",
            5 => $"涂装{Icon(ItemID.SpectrePaintbrush)} -> {Icon(ItemID.Wood)}",
            6 => $"清理{Icon(ItemID.WoodWall)}",
            7 => $"填充{Icon(ItemID.WoodWall)}",
            8 => $"替换{Icon(ItemID.WoodWall)}",
            9 => $"覆盖{Icon(ItemID.WoodWall)}",
            10 => $"涂装{Icon(ItemID.SpectrePaintRoller)} -> {Icon(ItemID.WoodWall)}",
            11 => $"清理涂装{Icon(ItemID.WhitePaint)}",
            12 => $"全部涂装{Icon(ItemID.WhitePaint)}",
            13 => $"虚化切换{Icon(ItemID.ActuationRod)}（根据范围统一）",
            14 => $"清理液体{Icon(ItemID.SuperAbsorbantSponge)}",
            15 => $"放水{Icon(ItemID.WaterBucket)}",
            16 => $"放岩浆{Icon(ItemID.LavaBucket)}",
            17 => $"放蜂蜜{Icon(ItemID.HoneyBucket)}",
            18 => $"放微光{Icon(ItemID.BottomlessShimmerBucket)}",
            19 => $"电路修改{Icon(ItemID.WireKite)}",
            20 => $"斜坡{Icon(ItemID.Wood)}(根据玩家朝向)",
            21 => $"半砖{Icon(ItemID.Wood)}(根据玩家朝向)",
            22 => $"全砖{Icon(ItemID.Wood)}",
            23 => $"清理所有{Icon(ItemID.Dynamite)}",
            _ => "未知操作"
        };
        SendMess(plr, $"模式:{msg} 请用{Icon(ItemID.WireKite)}框选范围");
    }
    #endregion

    #region 修复图格（核心）- 不分块，直接发送整个区域
    /// <summary>
    /// 将指定区域的图格替换为 TileData 中的数据，并返回修改的图格数量。
    /// </summary>
    internal static int FixTile(Rectangle rect, TileData data, int count)
    {
        if (data.Tiles != null)
        {
            for (int x = 0; x < rect.Width; x++)
            {
                for (int y = 0; y < rect.Height; y++)
                {
                    int wx = rect.X + x, wy = rect.Y + y;
                    if (wx < 0 || wx >= Main.maxTilesX ||
                        wy < 0 || wy >= Main.maxTilesY) continue;

                    var backup = data.Tiles[x, y];      // 要恢复的图格
                    var current = Main.tile[wx, wy] ?? new Tile(); // 当前图格（若为 null 则新建）

                    // 使用 TileSnapshot.TileStruct 比较两个图格是否相同（避免不必要的网络发送）
                    var tsBackup = TileSnapshot.TileStruct.From(backup);
                    var tsCurrent = TileSnapshot.TileStruct.From(current);
                    if (!tsBackup.Equals(tsCurrent))
                    {
                        current.CopyFrom(backup); // 复制数据
                        count++;
                        NetMessage.SendTileSquare(-1, wx, wy); // 向所有客户端发送该图格的更新
                    }
                }
            }
        }

        return count;
    }
    #endregion

    #region 修复家具/实体/箱子/标牌
    /// <summary>
    /// 根据 TileData 中的数据，在世界上放置箱子、实体和标牌
    /// </summary>
    internal static void FixItem(TileData data, TSPlayer plr)
    {
        if (data.EntData != null)
        {
            foreach (var ed in data.EntData)
            {
                // 放置实体，返回实体 ID
                int id = TileEntity.Place(ed.X, ed.Y, ed.Type);
                if (id == -1 || ed.ExtraData == null) continue;
                if (!TileEntity.ByID.TryGetValue(id, out var ent)) continue;

                // 从 ExtraData 中读取实体的额外数据（需要模拟 BinaryReader）
                using var ms = new MemoryStream(ed.ExtraData);
                using var br = new BinaryReader(ms);

                // 跳过 TileEntity.Write 写入的前缀（type, id, X, Y）
                br.ReadByte(); // type
                br.ReadInt32(); // id
                br.ReadInt16(); // X
                br.ReadInt16(); // Y

                if (plr.HasPermission(Config.IsAdamin) || Config.FixItem)
                {
                    // 根据游戏版本读取剩余数据
                    ent.ReadExtraData(br, GameVersionID.Latest, false);
                }
            }
        }

        if (data.Chests != null)
        {
            foreach (var chest in data.Chests)
            {
                // 创建箱子，返回箱子索引
                int idx = Chest.CreateChest(chest.x, chest.y);
                if (idx == -1) continue;
                var target = Main.chest[idx];
                target.name = chest.name ?? "";

                if (plr.HasPermission(Config.IsAdamin) || Config.FixItem)
                {
                    target.maxItems = chest.maxItems;
                    for (int s = 0; s < chest.maxItems; s++)
                        target.item[s] = chest.item[s]?.Clone() ?? new Item();
                }
            }
        }

        if (data.Signs != null)
        {
            foreach (var sign in data.Signs)
            {
                // 读取标牌（如果不存在则创建）
                int sid = Sign.ReadSign(sign.x, sign.y, true);
                Main.sign[sid].text = sign.text; // 设置文本
            }
        }

        // 强制所有玩家重新加载图格区域（将他们的 TileSections 标记为未加载）
        for (int i = 0; i < TShock.Players.Length; i++)
            if (TShock.Players[i]?.Active == true)
                for (int j = 0; j < Main.maxSectionsX; j++)
                    for (int k = 0; k < Main.maxSectionsY; k++)
                        Netplay.Clients[i].TileSections[j, k] = false;
    }
    #endregion

    #region 读取世界快照
    /// <summary>
    /// 从世界快照文件中读取指定矩形区域的数据
    /// </summary>
    internal static TileData? ReadWTile(string path, Rectangle rect, string? signPath = null)
    {
        if (!File.Exists(path))
        {
            TShock.Log.ConsoleError($"[{PluginName}] 快照文件不存在: " + path);
            return null;
        }

        try
        {
            using var stream = new GZipStream(new FileStream(path, FileMode.Open), CompressionMode.Decompress);
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            ms.Position = 0;
            using var br = new BinaryReader(ms);
            TileSnapshot.Load(br); // 从流中加载快照到 TileSnapshot 静态类

            if (!TileSnapshot.IsCreated)
            {
                TShock.Log.ConsoleError($"[{PluginName}] 地图快照加载失败");
                return null;
            }

            int w = TileSnapshot._worldFile.WorldSizeX; // 快照世界的宽度
            int h = TileSnapshot._worldFile.WorldSizeY; // 快照世界的高度
            if (rect.X < 0 || rect.Y < 0 || rect.Right > w || rect.Bottom > h)
            {
                TileSnapshot.Clear();
                return null;
            }

            var res = new TileData
            {
                Tiles = new Tile[rect.Width, rect.Height],
                Chests = new List<Chest>(),
                EntData = new List<EntityData>(),
                Signs = new List<Sign>()
            };

            // 从快照中复制图格
            for (int x = 0; x < rect.Width; x++)
                for (int y = 0; y < rect.Height; y++)
                {
                    int wx = rect.X + x, wy = rect.Y + y;
                    var ts = TileSnapshot._tiles[wx * h + wy]; // 快照图格（一维数组，索引 = x * 高度 + y）
                    res.Tiles[x, y] = new Tile();
                    ts.Apply(res.Tiles[x, y]); // 将快照数据应用到新 Tile 对象
                }

            // 复制箱子（只要箱子覆盖的任何一个图格在区域内就包含）
            foreach (var c in TileSnapshot._chests)
            {
                if (c == null) continue;
                if (rect.Contains(c.x, c.y) || rect.Contains(c.x + 1, c.y) ||
                    rect.Contains(c.x, c.y + 1) || rect.Contains(c.x + 1, c.y + 1))
                    res.Chests.Add(CloneChest(c, 0, 0)); // 使用辅助方法深拷贝
            }

            // 复制实体
            foreach (var e in TileSnapshot._tileEntities)
            {
                if (e == null || !rect.Contains(e.Position.X, e.Position.Y)) continue;
                res.EntData.Add(CloneEntity(e, 0, 0)); // 使用辅助方法
            }

            TileSnapshot.Clear(); // 清理快照

            if (signPath != null && File.Exists(signPath))
                res.Signs = LoadSigns(signPath); // 加载标牌

            return res;
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[{PluginName}] 快照读取失败: {ex.Message}");
            TileSnapshot.Clear();
            return null;
        }
    }
    #endregion

    #region 销毁区域实体
    /// <summary>
    /// 销毁指定矩形区域内的所有箱子、实体和标牌
    /// </summary>
    public static void KillAll(int startX, int endX, int startY, int endY)
    {
        // 遍历区域内每个图格
        for (int x = startX; x <= endX; x++)
            for (int y = startY; y <= endY; y++)
            {
                var tile = Main.tile[x, y];
                if (tile == null || !tile.active()) continue;

                // 销毁箱子
                if (TileID.Sets.BasicChest[tile.type] ||
                    TileID.Sets.BasicChestFake[tile.type] ||
                    TileID.Sets.BasicDresser[tile.type])
                    Chest.DestroyChest(x, y);

                // 销毁标牌
                if (tile.type == TileID.Signs ||
                    tile.type == TileID.Tombstones ||
                    tile.type == TileID.AnnouncementBox)
                    Sign.KillSign(x, y);

                // 物品框
                if (tile.type == TileID.ItemFrame)
                    TEItemFrame.Kill(x, y);
                
                // 武器架
                if (tile.type == TileID.WeaponsRack ||
                    tile.type == TileID.WeaponsRack2)
                    TEWeaponsRack.Kill(x, y);
                
                // 逻辑感应器
                if (tile.type == TileID.LogicSensor)
                    TELogicSensor.Kill(x, y);

                // 人体模型
                if (tile.type == TileID.DisplayDoll)
                    TEDisplayDoll.Kill(x, y);

                // 盘子
                if (tile.type == TileID.FoodPlatter)
                    TEFoodPlatter.Kill(x, y);

                // 销毁晶塔
                if (tile.type == TileID.TeleportationPylon)
                    TETeleportationPylon.Kill(x, y);

                // 训练假人（稻草人）
                if (tile.type == TileID.TargetDummy)
                    TETrainingDummy.Kill(x, y);
                
                // 衣帽架
                if (tile.type == TileID.HatRack)
                    TEHatRack.Kill(x, y);

                // 1.4.5 新增实体
                // 物品瓶（死亡细胞瓶）
                if (tile.type == TileID.DeadCellsDisplayJar)
                    TEDeadCellsDisplayJar.Kill(x, y);
                // 小动物锚点
                if (tile.type == TileID.CritterAnchor)
                    TECritterAnchor.Kill(x, y);
                // 风筝锚点
                if(tile.type == TileID.KiteAnchor)
                    TEKiteAnchor.Kill(x, y);
            }

        // 移除所有在区域内的实体
        Rectangle rect = new Rectangle(startX, startY, endX - startX + 1, endY - startY + 1);
        var toRemove = TileEntity.ByPosition.Values
            .Where(te => rect.Contains(te.Position.X, te.Position.Y)).ToList();
        foreach (var te in toRemove)
            TileEntity.Remove(te);
    }
    #endregion

    #region 获取世界区域数据
    /// <summary>
    /// 从当前世界获取指定矩形区域的图格、箱子、实体、标牌数据
    /// </summary>
    internal static TileData GetTileData(Rectangle rect)
    {
        var data = new TileData
        {
            Tiles = new Tile[rect.Width, rect.Height],
            Chests = new List<Chest>(),
            EntData = new List<EntityData>(),
            Signs = new List<Sign>()
        };

        // 复制图格
        for (int x = 0; x < rect.Width; x++)
            for (int y = 0; y < rect.Height; y++)
            {
                int wx = rect.X + x, wy = rect.Y + y;
                var tile = Main.tile[wx, wy];
                data.Tiles[x, y] = (Tile)(tile?.Clone() ?? new Tile());
            }

        // 复制箱子（只要箱子覆盖的区域与矩形有交集）
        foreach (var c in Main.chest)
        {
            if (c == null) continue;
            if (rect.Contains(c.x, c.y) || rect.Contains(c.x + 1, c.y) || rect.Contains(c.x, c.y + 1) || rect.Contains(c.x + 1, c.y + 1))
                data.Chests.Add(CloneChest(c, 0, 0)); // 使用辅助方法
        }

        // 复制实体
        foreach (var kv in TileEntity.ByPosition)
        {
            var pos = kv.Key;
            if (rect.Contains(pos.X, pos.Y))
                data.EntData.Add(CloneEntity(kv.Value, 0, 0)); // 使用辅助方法
        }

        // 复制标牌
        foreach (var s in Main.sign)
            if (s != null && rect.Contains(s.x, s.y))
                data.Signs.Add(CloneSign(s, 0, 0)); // 使用辅助方法

        return data;
    }
    #endregion

    #region 建筑数据克隆与偏移
    /// <summary>
    /// 克隆建筑数据，并将所有坐标偏移到目标位置
    /// </summary>
    internal static TileData CloneOff(TileData src, int offX, int offY)
    {
        var dst = new TileData();

        // 复制图格（图格本身不包含坐标，直接复制）
        if (src.Tiles != null)
        {
            int w = src.Tiles.GetLength(0);
            int h = src.Tiles.GetLength(1);
            dst.Tiles = new Tile[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    dst.Tiles[x, y] = (Tile)src.Tiles[x, y].Clone();
        }

        // 复制箱子，并偏移坐标
        if (src.Chests != null)
        {
            dst.Chests = new List<Chest>();
            foreach (var chest in src.Chests)
                dst.Chests.Add(CloneChest(chest, offX, offY));
        }

        // 复制实体，偏移坐标
        if (src.EntData != null)
        {
            dst.EntData = new List<EntityData>();
            foreach (var ent in src.EntData)
            {
                // 注意：EntityData 需要从 ExtraData 反序列化才能获得 TileEntity，此处直接复制并偏移坐标
                dst.EntData.Add(new EntityData
                {
                    Type = ent.Type,
                    X = (short)(ent.X + offX),
                    Y = (short)(ent.Y + offY),
                    ExtraData = ent.ExtraData?.ToArray()
                });
            }
        }

        // 复制标牌，偏移坐标
        if (src.Signs != null)
        {
            dst.Signs = new List<Sign>();
            foreach (var sign in src.Signs)
                dst.Signs.Add(CloneSign(sign, offX, offY));
        }

        return dst;
    }
    #endregion

    #region 图格编辑实现
    private static void ExecuteEdit(Rectangle rect, int op, int a1, int a2, int a3, int dir, int toolMode)
    {
        // 对于 op == 13，需要先扫描区域
        if (op == 13)
        {
            bool hasInactive = false;
            // 扫描
            for (int x = rect.X; x < rect.Right && !hasInactive; x++)
                for (int y = rect.Y; y < rect.Bottom && !hasInactive; y++)
                    hasInactive = Main.tile[x, y]?.inActive() == true;
            // 统一设置
            for (int x = rect.X; x < rect.Right; x++)
                for (int y = rect.Y; y < rect.Bottom; y++)
                    if (Main.tile[x, y] is Tile t)
                    {
                        t.inActive(!hasInactive);
                        NetMessage.SendTileSquare(-1, x, y);
                    }
            return;
        }

        // 其他操作正常循环
        for (int x = rect.X; x < rect.Right; x++)
            for (int y = rect.Y; y < rect.Bottom; y++)
            {
                var tile = Main.tile[x, y];
                if (tile == null) continue;

                switch (op)
                {
                    case 1: tile.Clear(TileDataType.Tile); break;
                    case 2: if (!WorldGen.SolidTile(tile)) { WorldGen.PlaceTile(x, y, a1, mute: true, style: a2); SetDire(tile, dir); } break;
                    case 3: WorldGen.ReplaceTile(x, y, (ushort)a1, a2); SetDire(tile, dir); break;
                    case 4: tile.Clear(TileDataType.Tile); WorldGen.PlaceTile(x, y, a1, mute: true, style: a2); SetDire(tile, dir); break;
                    case 5: if (a2 == 1) WorldGen.paintTile(x, y, (byte)a1); else WorldGen.paintCoatTile(x, y, (byte)a1); break;
                    case 6: WorldGen.KillWall(x, y, false); break;
                    case 7: if (tile.wall == 0) WorldGen.PlaceWall(x, y, a1); break;
                    case 8: WorldGen.ReplaceWall(x, y, (ushort)a1); break;
                    case 9: WorldGen.KillWall(x, y, false); WorldGen.PlaceWall(x, y, a1); break;
                    case 10: if (a2 == 1) WorldGen.paintWall(x, y, (byte)a1); else WorldGen.paintCoatWall(x, y, (byte)a1); break;
                    case 11:
                        WorldGen.paintTile(x, y, 0, broadCast: true, paintEffects: true);
                        WorldGen.paintWall(x, y, 0, broadCast: true, paintEffects: true);
                        WorldGen.paintCoatTile(x, y, 0, broadcast: true, coatingEffects: true);
                        WorldGen.paintCoatWall(x, y, 0, broadcast: true, coatingEffects: true);
                        break;
                    case 12: // 涂装所有
                        if (a2 == 1) { WorldGen.paintTile(x, y, (byte)a1); WorldGen.paintWall(x, y, (byte)a1); }
                        else { WorldGen.paintCoatTile(x, y, (byte)a1); WorldGen.paintCoatWall(x, y, (byte)a1); }
                        break;
                    case 14: // 清理液体
                        WorldGen.EmptyLiquid(x, y);
                        break;
                    case 15: // 放水
                        ClearEverything(x, y); tile.liquid = byte.MaxValue; tile.liquidType(0);
                        break;
                    case 16: // 放岩浆
                        ClearEverything(x, y); tile.liquid = byte.MaxValue; tile.liquidType(1);
                        break;
                    case 17: // 放蜂蜜
                        ClearEverything(x, y); tile.liquid = byte.MaxValue; tile.liquidType(2);
                        break;
                    case 18: // 放微光
                        ClearEverything(x, y); tile.liquid = byte.MaxValue; tile.liquidType(3);
                        break;
                    case 19: // 电路修改
                        bool isPlace = toolMode >= 1 && toolMode <= 31;
                        SetWire(x, y, toolMode, isPlace);
                        break;
                    case 20: // 斜坡
                        if (a1 == 1 || a1 == 2) WorldGen.SlopeTile(x, y, a1);
                        break;
                    case 21: // 半砖
                        if (a1 == 3 || a1 == 4) WorldGen.SlopeTile(x, y, a1);
                        break;
                    case 22: // 全砖
                        tile.Clear(TileDataType.Slope);
                        break;
                    case 23: // 清理所有
                        ClearEverything(x, y);
                        break;
                }
                NetMessage.SendTileSquare(-1, x, y);
            }
    }
    #endregion

    #region 设置方块朝向
    private static void SetDire(ITile tile, int dir)
    {
        if (tile == null || !tile.active()) return;

        // 单格物品（frameX 为 -1）直接翻转
        if (dir == 1) // 右朝向
        {
            // 直接增加 frameX，假设每个样式占 18 像素
            int newFrameX = tile.frameX + 18;
            // 简单处理溢出：如果超过 54（假设最多3个样式）则回绕，可根据需要调整
            if (newFrameX >= 54) newFrameX -= 54;
            tile.frameX = (short)newFrameX;
        }

    }
    #endregion

    #region 根据精密线控模式自动设置电线与制动器状态
    private static void SetWire(int x, int y, int toolMode, bool isPlace)
    {
        // 清理模式编号范围 33~63，对应放置模式编号 1~31 加上偏移 32
        int mode = toolMode;
        bool isClean = mode >= 33 && mode <= 63;
        if (isClean) mode -= 32; // 转换为对应的放置模式编号

        if (mode < 1 || mode > 31) return;

        // 根据位掩码执行操作
        if (isPlace && !isClean || !isPlace && isClean)
        {
            if ((mode & 1) != 0) // 红
            {
                if (isPlace) WorldGen.PlaceWire(x, y);
                else WorldGen.KillWire(x, y);
            }
            if ((mode & 2) != 0) // 绿
            {
                if (isPlace) WorldGen.PlaceWire2(x, y);
                else WorldGen.KillWire2(x, y);
            }
            if ((mode & 4) != 0) // 蓝
            {
                if (isPlace) WorldGen.PlaceWire3(x, y);
                else WorldGen.KillWire3(x, y);
            }
            if ((mode & 8) != 0) // 黄
            {
                if (isPlace) WorldGen.PlaceWire4(x, y);
                else WorldGen.KillWire4(x, y);
            }
            if ((mode & 16) != 0) // 制动器
            {
                if (isPlace) WorldGen.PlaceActuator(x, y);
                else WorldGen.KillActuator(x, y);
            }
        }
    }
    #endregion

    #region 清理一切方法
    public static void ClearEverything(int x, int y)
    {
        Main.tile[x, y].ClearEverything();
        NetMessage.SendTileSquare(-1, x, y, TileChangeType.None);
    }
    #endregion
}