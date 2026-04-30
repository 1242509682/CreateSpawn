using System.IO.Compression;
using System.Text;
using Microsoft.Xna.Framework;
using ReLogic.Peripherals.RGB.Logitech;
using Terraria;
using Terraria.ID;
using TShockAPI;
using static CreateSpawn.Map;
using static CreateSpawn.PlayerState;
using static CreateSpawn.Plugin;
using static CreateSpawn.Utils;
using static CreateSpawn.WorldTile;

namespace CreateSpawn;

internal class MyCmd
{
    #region 指令参数
    // 指令
    public static string cmd => "cb";
    // 玩家权限
    public static string prem => Config.IsUse;
    // 管理权限
    public static bool IsAdmin(TSPlayer plr) => plr.HasPermission(Config.IsAdamin);
    // 判断玩家是否在游戏
    public static bool InGame(TSPlayer plr)
    {
        if (!plr.RealPlayer)
        {
            plr.SendMessage($"请进入游戏后再使用{PluginName}的{cmd}指令", color);
            return false;
        }
        return true;
    }
    #endregion

    #region 菜单指令
    private static void ShowHelp(TSPlayer plr)
    {
        var sb = new StringBuilder();

        if (plr.RealPlayer)
        {
            sb.AppendLine($"\n{Icon(ItemID.NebulaPickup3)}" +
                $"[c/AD89D5:复][c/D68ACA:制][c/DF909A:建][c/E5A894:筑]" +
                $"{Icon(ItemID.NebulaPickup2)} " +
                $"{Icon(ItemID.FragmentVortex)}" +
                $"[c/F2F2C7:开发] [c/BFDFEA:by] [c/00FFFF:羽学] " +
                $"{Icon(ItemID.FragmentStardust)}");
        }
        else
        {
            sb.AppendLine($"\n《{PluginName}》");
        }

        sb.AppendLine($"/{cmd} sv --复制建筑");
        sb.AppendLine($"/{cmd} pt --粘贴建筑");
        sb.AppendLine($"/{cmd} bk --撤销操作");
        sb.AppendLine($"/{cmd} fix --修复图格");

        if (IsAdmin(plr))
        {
            sb.AppendLine($"/{cmd} t --操作图格");
            sb.AppendLine($"/{cmd} r --修改区域");
            sb.AppendLine($"/{cmd} c --修改配置");
            sb.AppendLine($"/{cmd} rs --重置数据");
        }
        SendMess(plr, sb.ToString());
    }
    #endregion

    #region 主指令
    internal static void MainCmd(CommandArgs args)
    {
        var plr = args.Player;

        if (args.Parameters.Count == 0)
        {
            ShowHelp(plr);
            return;
        }

        if (args.Parameters.Count >= 1)
        {
            switch (args.Parameters[0].ToLower())
            {
                case "t" when InGame(plr) && IsAdmin(plr):
                case "tile" when InGame(plr) && IsAdmin(plr):
                    TileOp(args, plr); // 范围操作图格指令
                    break;

                case "fix" when InGame(plr):
                case "修复" when InGame(plr):
                    HandleFix(args, plr);  // 从快照修复图格命令
                    break;

                case "bk":
                case "back":
                case "撤销":
                    UndoCmd(plr);  // 图格撤销指令
                    break;

                case "add" when InGame(plr):
                case "sv" when InGame(plr):
                case "save" when InGame(plr):
                case "copy" when InGame(plr):
                case "复制" when InGame(plr):
                    HandleAdd(args, plr); // 复制命令
                    break;

                case "pt" when InGame(plr):
                case "sp" when InGame(plr):
                case "粘贴" when InGame(plr):
                    HandlePst(args, plr); // 粘贴命令
                    break;

                case "c" when IsAdmin(plr):
                case "cfg" when IsAdmin(plr):
                    SetCfg(args, plr); // 修改配置项
                    break;

                case "r" when InGame(plr) && IsAdmin(plr):
                    SetRegion(args, plr); // 修改区域指令
                    break;

                case "rs" when IsAdmin(plr):
                case "reset" when IsAdmin(plr):
                    {
                        // 1. 删除所有由本插件创建的区域
                        foreach (string regName in Plugin.Config.CreatedReg)
                        {
                            var reg = TShock.Regions.GetRegionByName(regName);
                            if (reg != null)
                                TShock.Regions.DeleteRegion(regName);
                        }

                        // 2. 清空区域记录
                        Plugin.Config.CreatedReg.Clear();
                        Plugin.Config.Write();

                        // 3. 清空玩家内存数据
                        PlayerState.PlrData.Clear();

                        // 4. 清理自动备份快照（备份存档目录下的所有 .zip 文件）
                        if (Directory.Exists(Plugin.AutoSaveDir))
                        {
                            var zipFiles = Directory.GetFiles(Plugin.AutoSaveDir, "*.zip");
                            foreach (var zip in zipFiles)
                            {
                                try { File.Delete(zip); }
                                catch (Exception ex) { TShock.Log.ConsoleError($"删除备份文件失败: {zip}, {ex.Message}"); }
                            }
                            SendMess(plr, $"已清理 {zipFiles.Length} 个自动备份快照");
                        }

                        // 5. 清理修复临时目录（撤销栈、临时快照等）
                        if (Directory.Exists(Plugin.RestoreDir))
                        {
                            var tempFiles = Directory.GetFiles(Plugin.RestoreDir);
                            foreach (var file in tempFiles)
                            {
                                try { File.Delete(file); }
                                catch (Exception ex) { TShock.Log.ConsoleError($"删除临时文件失败: {file}, {ex.Message}"); }
                            }
                            SendMess(plr, $"已清理 {tempFiles.Length} 个临时修复文件");
                        }

                        SendMess(plr, $"{PluginName} 插件数据已完全重置");
                        break;
                    }

                default: ShowHelp(plr); break;
            }
        }
    }
    #endregion

    #region 设置区域方法
    private static void ShowReg(TSPlayer plr)
    {
        StringBuilder sb = new($"\n《区域修改指令》\n");
        sb.AppendLine($"/{cmd} r a --创建区域");
        sb.AppendLine($"/{cmd} r d --删除区域");
        sb.AppendLine($"/{cmd} r b --修改禁止建筑");
        sb.AppendLine($"/{cmd} r p --修改白名单玩家");
        sb.AppendLine($"/{cmd} r g --修改白名单组");

        SendMess(plr, sb.ToString());
    }
    private static void SetRegion(CommandArgs args, TSPlayer plr)
    {
        var data = PlayerState.GetData(plr.Name);
        if (data == null) return;

        if (args.Parameters.Count < 2)
        {
            ShowReg(plr);
            data.Reset();
            return;
        }

        switch (args.Parameters[1].ToLower())
        {
            case "+":
            case "a":
            case "add":
                if (args.Parameters.Count < 3)
                {
                    SendMess(plr, $"请输入区域名称: /{cmd} r a 区域名");
                    return;
                }

                string newName = args.Parameters[2];
                var region = TShock.Regions.GetRegionByName(newName);
                if (region != null)
                {
                    SendMess(plr, $"区域 {newName} 已存在");
                    AnimMag.Add(region.Area);
                    data.Reset();
                    return;
                }

                data.RegionName = newName;
                data.Mode = 1;
                SendMess(plr, $"{data.RegionName}创建 请使用{Icon(ItemID.WireKite)}");
                break;

            case "-":
            case "d":
            case "del":
                data.Mode = 2;
                SendMess(plr, $"区域删除 请使用{Icon(ItemID.WireKite)}");
                break;

            case "b":
            case "build":
                data.Mode = 3;
                SendMess(plr, $"禁止建筑 请使用{Icon(ItemID.WireKite)}");
                break;

            case "p":
            case "plr":
                if (args.Parameters.Count < 3)
                {
                    SendMess(plr, $"请输入玩家名: /{cmd} r p 玩家名1 玩家名2 ...");
                    SendMess(plr, $"不在则添加,存在则移除");
                    return;
                }

                // 收集第2个参数及之后的所有参数
                data.AllowedPlayer.Clear();
                for (int i = 2; i < args.Parameters.Count; i++)
                    data.AllowedPlayer.Add(args.Parameters[i]);

                data.Mode = 4;
                SendMess(plr, $"区域玩家修改[c/FAFAFA:{data.AllowedPlayer.Count}]个 请使用{Icon(ItemID.WireKite)}");
                break;

            case "g":
            case "group":
                if (args.Parameters.Count < 3)
                {
                    SendMess(plr, $"请输入组名: /{cmd} r gp 组名1 组名2 ...");
                    SendMess(plr, $"不在则添加,存在则移除");
                    return;
                }

                // 收集第2个参数及之后的所有参数
                data.AllowedGroup.Clear();
                for (int i = 2; i < args.Parameters.Count; i++)
                    data.AllowedGroup.Add(args.Parameters[i]);

                data.Mode = 5;
                SendMess(plr, $"区域组修改[c/FAFAFA:{data.AllowedGroup.Count}]个 请使用{Icon(ItemID.WireKite)}");
                break;

            case "t":
            case "tile":
                {
                    var it = plr.SelectedItem;
                    if (it.createTile >= 0)
                    {
                        data.rw = 31;            // 物块替换
                        data.rwA1 = it.createTile;
                        data.rwA2 = it.placeStyle;
                    }
                    else if (it.createWall >= 0)
                    {
                        data.rw = 32;            // 墙壁替换
                        data.rwA1 = it.createWall;
                    }
                    else if (it.paint > 0)
                    {
                        data.rw = 33;            // 油漆替换
                        data.rwA1 = it.paint;
                    }
                    else if (it.paintCoating > 0)
                    {
                        data.rw = 34;            // 涂料替换
                        data.rwA1 = it.paintCoating;
                    }
                    else if (it.type == ItemID.WaterBucket || it.type == ItemID.BottomlessBucket)
                    {
                        data.rw = 35;            // 液体替换
                        data.rwA1 = 0;          // 水
                    }
                    else if (it.type == ItemID.LavaBucket || it.type == ItemID.BottomlessLavaBucket)
                    {
                        data.rw = 35;
                        data.rwA1 = 1;          // 岩浆
                    }
                    else if (it.type == ItemID.HoneyBucket || it.type == ItemID.BottomlessHoneyBucket)
                    {
                        data.rw = 35;
                        data.rwA1 = 2;          // 蜂蜜
                    }
                    else if (it.type == ItemID.BottomlessShimmerBucket)
                    {
                        data.rw = 35;
                        data.rwA1 = 3;          // 微光
                    }
                    else
                    {
                        SendMess(plr, "请手持要替换的物块、墙壁、油漆、涂料或液体桶");
                        return;
                    }
                    data.Mode = 6;
                    SendMess(plr, $"连锁替换模式 请使用{Icon(ItemID.WireKite)}框选区域");
                    break;
                }

            default:
                ShowReg(plr);
                data.Reset();
                break;
        }
    }
    #endregion

    #region 修改配置项方法
    private static void ShowCfg(TSPlayer plr)
    {
        StringBuilder sb = new($"\n《{PluginName}配置表》\n");
        sb.AppendLine($"/{cmd} c en --插件开关");
        sb.AppendLine($"/{cmd} c ss --备份快照开关");
        sb.AppendLine($"/{cmd} c sp --出生点建筑开关");
        sb.AppendLine($"/{cmd} c cr --粘贴建区域开关");
        sb.AppendLine($"/{cmd} c dr --撤销删区域开关");
        sb.AppendLine($"/{cmd} c fr --修复删区域开关");
        sb.AppendLine($"/{cmd} c cd --改备份快照间隔");
        sb.AppendLine($"/{cmd} c ab --修改管理专用表");
        SendMess(plr, sb.ToString());
    }

    private static void SetCfg(CommandArgs args, TSPlayer plr)
    {
        if (args.Parameters.Count < 2)
        {
            ShowCfg(plr);
            return;
        }

        switch (args.Parameters[1].ToLower())
        {
            case "en":
                SetBool("插件开关", plr, () => Config.Enabled, (val) => Config.Enabled = val);
                break;
            case "sp":
                SetBool("出生点生成开关", plr, () => Config.SpawnEnabled, (val) => Config.SpawnEnabled = val);
                break;
            case "ss":
                SetBool("备份快照", plr, () => Config.SaveSnapshot, (val) => Config.SaveSnapshot = val);
                break;
            case "cr":
                SetBool("粘贴自动建区域", plr, () => Config.CreateRegion, (val) => Config.CreateRegion = val);
                break;
            case "dr":
                SetBool("撤销自动删区域", plr, () => Config.DeleteRegion, (val) => Config.DeleteRegion = val);
                break;
            case "fr":
                SetBool("修复自动删区域", plr, () => Config.DelRegForFix, (val) => Config.DelRegForFix = val);
                break;
            case "cd":
                if (args.Parameters.Count < 3)
                {
                    string help = $"使用方法: /{cmd} c cd 分钟";
                    SendMess(plr, help);
                    return;
                }
                SetInt("备份快照间隔", plr, (val) => Config.SaveSnapshotTime = val, args.Parameters[2], "分钟");
                break;
            case "ab":
                if (args.Parameters.Count < 3)
                {
                    SendMess(plr, $"使用方法: /{cmd} c ab <建筑名1> [建筑名2...]");
                    SendMess(plr, "若建筑已在管理表中则移除，否则添加");
                    return;
                }
                List<string> added = new();
                List<string> removed = new();
                for (int i = 2; i < args.Parameters.Count; i++)
                {
                    string building = args.Parameters[i];
                    if (Config.AdminBuilding.Contains(building))
                    {
                        Config.AdminBuilding.Remove(building);
                        removed.Add(building);
                    }
                    else
                    {
                        Config.AdminBuilding.Add(building);
                        added.Add(building);
                    }
                }
                Config.Write();
                if (added.Count > 0)
                    SendMess(plr, $"已添加管理专用建筑: {string.Join(", ", added)}");
                if (removed.Count > 0)
                    SendMess(plr, $"已移除管理专用建筑: {string.Join(", ", removed)}");
                break;

            default: ShowCfg(plr); break;
        }
    }
    #endregion

    #region 复制子命令（先输入名称）
    /// <summary>
    /// 处理 sv 等保存子命令：记录要保存的建筑名称，然后等待玩家用红电线框选区域
    /// </summary>
    private static void HandleAdd(CommandArgs args, TSPlayer plr)
    {
        if (args.Parameters.Count < 2)
        {
            SendMess(plr, $"请指定建筑名称: /{cmd} sv <名称>");
            return;
        }

        string name = args.Parameters[1];
        // 检查文件名非法字符
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            SendMess(plr, "建筑名称包含非法字符");
            return;
        }

        if (!Directory.Exists(ClipDir))
            Directory.CreateDirectory(ClipDir);

        // 将建筑名称存入玩家数据，等待红电线拉取区域时使用
        GetData(plr.Name).rwCopy = name;
        SendMess(plr, $"准备保存建筑 {name}\n" +
                      $"请使用 [i:{ItemID.WireKite}] 拉取需要复制的区域");
    }
    #endregion

    #region 粘贴命令
    /// <summary>
    /// 处理 sp 等粘贴子命令：粘贴指定建筑到玩家头顶
    /// </summary>
    private static void HandlePst(CommandArgs args, TSPlayer plr)
    {
        // 无参数时列出所有可用建筑
        if (args.Parameters.Count < 2)
        {
            List<string> names = GetClipNames(); // 获取所有建筑名称（不带扩展名）
            if (names.Count == 0)
            {
                SendMess(plr, "暂无保存的建筑");
                return;
            }
            var sb = new StringBuilder("\n建筑列表:\n");
            for (int i = 0; i < names.Count; i++)
                sb.AppendLine($"{i + 1}. {names[i]}");
            SendMess(plr, sb.ToString());
            SendMess(plr, $"用法: /{cmd} pt <名称/索引>");
            return;
        }

        string input = args.Parameters[1];
        string buildName = "";

        // 尝试按索引解析
        if (int.TryParse(input, out int idx))
        {
            var names = GetClipNames();
            if (idx < 1 || idx > names.Count)
            {
                SendMess(plr, $"索引 {idx} 无效，共 {names.Count} 个建筑");
                return;
            }

            buildName = names[idx - 1];
        }
        else
        {
            buildName = input;
        }

        // 验证建筑存在及权限
        if (!File.Exists(GetClipPath(buildName)))
        {
            SendMess(plr, $"未找到建筑 '{buildName}'");
            return;
        }

        if (Config.AdminBuilding.Contains(buildName) && !IsAdmin(plr))
        {
            SendMess(plr, $"建筑 '{buildName}' 为管理专用，您无权使用");
            return;
        }

        // 进入粘贴等待模式
        var data = GetData(plr.Name);
        data.rwPaste = buildName;
        SendMess(plr, $"粘贴建筑 '{buildName}' 请使用 [i:{ItemID.WireKite}] 框选目标区域");
        SendMess(plr, $"只点1下为建筑中心,否则以起点决定建筑延伸");
    }

    /// <summary>
    /// 获取所有建筑名称列表（不带扩展名）
    /// </summary>
    private static List<string> GetClipNames()
    {
        if (!Directory.Exists(ClipDir)) return new List<string>();
        return Directory.GetFiles(ClipDir, "*.clip")
                        .Select(f => Path.GetFileNameWithoutExtension(f)).ToList();
    }
    #endregion

    #region 撤销命令
    /// <summary>
    /// 处理 bk 子命令：撤销上一次操作
    /// </summary>
    public static void UndoCmd(TSPlayer plr)
    {
        // 弹出该玩家的撤销操作栈顶元素
        var op = PopUndo(plr.Name);
        if (op == null)
        {
            SendMess(plr, "\n没有可撤销的操作记录");
            return;
        }

        // 修复图格：将区域还原为操作前的状态，count 接收修复的图格数量
        int count = FixTile(op.Area, op.BeforeState, 0);
        // 修复箱子/实体/标牌
        FixItem(op.BeforeState, plr);

        // 自动删除区域
        if (Config.DeleteRegion && !string.IsNullOrEmpty(op.RegionName))
        {
            var region = TShock.Regions.GetRegionByName(op.RegionName);
            if (region != null)
            {
                TShock.Regions.DeleteRegion(op.RegionName);
                // 从配置中移除
                if (Config.CreatedReg.Contains(op.RegionName))
                {
                    Config.CreatedReg.Remove(op.RegionName);
                    Config.Write();
                }
                SendMess(plr, $"已删除关联区域 {op.RegionName}");
            }
        }

        if (plr.RealPlayer) AnimMag.Add(op.Area); // 显示区域动画
        SendMess(plr, $"\n撤销成功！恢复 {count} 个图格");
    }
    #endregion

    #region 修复命令
    /// <summary>
    /// 处理 fix 子命令：加载备份文件，准备修复区域
    /// </summary>
    private static void HandleFix(CommandArgs args, TSPlayer plr)
    {
        // 如果参数不足3（即没有指定索引），则列出所有备份
        if (args.Parameters.Count < 2)
        {
            var list = GetBakList(); // 获取备份文件列表（仅文件名，格式 "索引. 文件名"）
            if (list.Count == 0)
            {
                SendMess(plr, "暂无自动备份");
                return;
            }

            var sb = new StringBuilder("\n当前备份:\n");
            foreach (var item in list) sb.AppendLine(item);
            SendMess(plr, sb.ToString());
            SendMess(plr, $"用法: /{cmd} fix <索引>");
            SendMess(plr, $"注意:索引为文件名前面的序号");
            return;
        }

        // 尝试解析索引
        if (!int.TryParse(args.Parameters[1], out int idx) || idx < 1)
        {
            SendMess(plr, "索引必须是大于0的数字");
            return;
        }

        // 获取玩家数据（来自 PlayerState 类）
        var Mydata = PlayerState.GetData(plr.Name);
        if (Mydata == null) return;

        // 获取所有备份文件的完整路径数组
        var files = GetBakFiles();
        if (idx > files.Length)
        {
            SendMess(plr, $"索引超出范围，共有 {files.Length} 个备份");
            return;
        }

        // 确保修复临时目录存在
        if (!Directory.Exists(RestoreDir)) Directory.CreateDirectory(RestoreDir);

        string zipPath = files[idx - 1]; // 根据索引选取备份文件路径
        using (var zip = ZipFile.OpenRead(zipPath)) // 打开 ZIP 压缩包
        {
            // 查找 .tws 世界快照文件条目
            var snapEntry = zip.Entries.FirstOrDefault(e => e.Name.EndsWith(TwsExt));
            if (snapEntry == null)
            {
                SendMess(plr, $"备份文件 {Path.GetFileName(zipPath)} 中未找到世界快照");
                return;
            }

            // 将快照文件解压到临时目录，文件名包含时间戳
            string snapPath = Path.Combine(RestoreDir, $"{SnapPre}{DateTime.Now:HHmmss}{TwsExt}");
            snapEntry.Open().CopyTo(File.Create(snapPath));
            Mydata.rwSnap = snapPath; // 保存路径到玩家数据

            // 查找 .sgn 标牌文件条目（可选）
            var signEntry = zip.Entries.FirstOrDefault(e => e.Name.EndsWith(SgnExt, StringComparison.OrdinalIgnoreCase));
            if (signEntry != null)
            {
                string signPath = Path.Combine(RestoreDir, $"{SignPre}{DateTime.Now:HHmmss}{SgnExt}");
                signEntry.Open().CopyTo(File.Create(signPath));
                Mydata.rwSign = signPath; // 保存标牌文件路径
            }

            SendMess(plr, Grad($"成功加载世界快照: {snapEntry.Name}"));
        }

        Mydata.rwFix = true; // 标记玩家进入修复模式
        SendMess(plr, $"请使用 [i:{ItemID.WireKite}] 拉取需要恢复的区域");
    }
    #endregion

    #region 设置布尔方法
    public static void SetBool(string desc, TSPlayer plr, Func<bool> getVal, Action<bool> setVal)
    {
        bool cur = getVal();
        bool newVal = !cur;
        setVal(newVal);
        Config.Write();
        var state = newVal ? "开启" : "关闭";
        SendMess(plr, $"{desc}已切换为 {state}");
    }
    #endregion

    #region 设置数值方法
    public static void SetInt(string desc, TSPlayer plr, Action<int> setVal, string num, string unit = "")
    {
        if (!int.TryParse(num, out int val))
        {
            SendMess(plr, $"请输入正确数字 如: {desc} {unit}");
            return;
        }

        setVal(val);
        Config.Write();
        SendMess(plr, $"{desc}已设置为 {val}{unit}");
    }
    #endregion
}