using System.IO.Compression;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Generation;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;
using static CreateSpawn.Map;
using static CreateSpawn.Utils;
using static CreateSpawn.WorldTile;

namespace CreateSpawn;

[ApiVersion(2, 1)]
public class Plugin(Main game) : TerrariaPlugin(game)
{
    #region 插件信息
    public static string PluginName => "复制建筑"; // 插件名称
    public override string Name => PluginName;
    public override string Author => "少司命 羽学";
    public override Version Version => new(2, 0, 3);
    public override string Description => "使用指令复制区域建筑,支持保存建筑文件、跨地图粘贴、区域保护与修改、范围图格操作、修复局部图格、自动生成出生点建筑等";
    #endregion

    #region 文件后缀与路径
    // 路径
    public static readonly string MainPath = Path.Combine(TShock.SavePath, PluginName); // 主文件夹路径
    public static readonly string CfgPath = Path.Combine(MainPath, $"{PluginName}.json"); // 配置文件路径
    public static readonly string AutoSaveDir = Path.Combine(MainPath, "地图快照");     // 备份角色路径
    public static readonly string ClipDir = Path.Combine(MainPath, "建筑文件");
    public static readonly string RestoreDir = Path.Combine(MainPath, "操作记录");
    public static string GetClipPath(string name) => Path.Combine(ClipDir, $"{name}.clip");
    // 文件扩展名常量
    public static readonly string TwsExt = ".tws";      // 世界快照文件扩展名
    public static readonly string SgnExt = ".sgn";      // 标牌文件扩展名
    public static readonly string SnapPre = "snap_";    // 快照临时文件前缀
    public static readonly string SignPre = "sign_";    // 标牌临时文件前缀
    #endregion

    #region 注册与释放
    public override void Initialize()
    {
        ExtractData();
        LoadConfig();
        GeneralHooks.ReloadEvent += ReloadConfig;
        ServerApi.Hooks.GameUpdate.Register(this, OnGameUpdate);
        ServerApi.Hooks.ServerLeave.Register(this, PlayerState.OnLeave);
        GetDataHandlers.MassWireOperation.Register(WorldTile.OnWire);
        ServerApi.Hooks.GamePostInitialize.Register(this, this.GamePost);
        On.Terraria.WorldGen.AddGenerationPass_string_WorldGenLegacyMethod += WorldGen_AddGenerationPass_string_WorldGenLegacyMethod;
        TShockAPI.Commands.ChatCommands.Add(new Command(MyCmd.prem, MyCmd.MainCmd, MyCmd.cmd));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            GeneralHooks.ReloadEvent -= ReloadConfig;
            ServerApi.Hooks.GameUpdate.Deregister(this, OnGameUpdate);
            ServerApi.Hooks.ServerLeave.Deregister(this, PlayerState.OnLeave);
            GetDataHandlers.MassWireOperation.UnRegister(WorldTile.OnWire);
            ServerApi.Hooks.GamePostInitialize.Deregister(this, this.GamePost);
            On.Terraria.WorldGen.AddGenerationPass_string_WorldGenLegacyMethod -= WorldGen_AddGenerationPass_string_WorldGenLegacyMethod;
            TShockAPI.Commands.ChatCommands.RemoveAll(x => x.CommandDelegate == MyCmd.MainCmd);
        }
        base.Dispose(disposing);
    }
    #endregion

    #region 配置重载读取与写入方法
    internal static Configuration Config = new(); // 配置文件实例
    private static void ReloadConfig(ReloadEventArgs args)
    {
        LoadConfig();
        args.Player.SendMessage($"[{PluginName}]重新加载配置完毕。", color);
    }

    private static void LoadConfig()
    {
        try
        {
            Config = Configuration.Read();
            Config.Write();
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[{PluginName}] 配置文件加载失败：\n{ex.Message}");
        }
    }
    #endregion

    #region 加载完世界后生成出生点方法
    private static bool Spawn = false;
    private void GamePost(EventArgs args)
    {
        if (!Spawn) return;

        var name = Config.SpawnName;
        var clip = Map.LoadClip(name);
        if (clip == null)
        {
            SendMess(TSPlayer.Server, $"未找到名为 {name} 的建筑!");
            return;
        }

        int w = clip.Tiles?.GetLength(0) ?? 0;
        int h = clip.Tiles?.GetLength(1) ?? 0;
        if (w == 0 || h == 0)
        {
            SendMess(TSPlayer.Server, "建筑数据无效");
            return;
        }

        // 微调参数计算
        int spwx = Main.spawnTileX;
        int spwy = Main.spawnTileY;
        int startX = spwx - Config.CentreX + Config.AdjustX;
        int startY = spwy - Config.CountY + Config.AdjustY;
        Rectangle rect = new Rectangle(startX, startY, w, h);

        #region 自动创建区域
        if (Config.CreateRegion)
        {
            // 生成唯一区域名
            if (!TShock.Regions.AddRegion(rect.X, rect.Y, rect.Width, rect.Height, name,
                 TSPlayer.Server.Name, Main.worldID.ToString()))
            {
                SendMess(TSPlayer.Server, "自动创建区域失败，粘贴已取消");
                return;
            }
            // 设置禁止建筑
            TShock.Regions.SetRegionState(name, true);
            // 记录到配置，便于重置时清理
            if (!Config.CreatedReg.Contains(name))
            {
                Config.CreatedReg.Add(name);
                Config.Write();
            }
        }
        #endregion

        // 保存粘贴前的区域状态以便撤销
        var before = GetTileData(rect);
        var stack = LoadUndo(TSPlayer.Server.Name);
        stack.Push(new UndoOperation
        {
            RegionName = name,
            Area = rect,
            BeforeState = before,
            Timestamp = DateTime.Now,
        });
        SaveUndo(TSPlayer.Server.Name, stack);

        // 将建筑数据偏移到目标坐标
        TileData? data = CloneOff(clip, startX, startY);

        int count = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        // 异步执行粘贴操作，避免阻塞主线程
        Task.Run(() =>
        {
            // 先清除目标区域内的所有箱子、实体、标牌
            KillAll(startX, startX + w - 1, startY, startY + h - 1);
            count = WorldTile.FixTile(rect, data, count); // 粘贴图格

        }).ContinueWith(_ =>
        {
            FixItem(data, TSPlayer.Server); // 粘贴箱子、实体、标牌
            sw.Stop();
            SendMess(TSPlayer.Server, $"粘贴 {name} 完成！已创造: {count} 个图格," +
                                      $"用时 {sw.ElapsedMilliseconds} ms\n" +
                                      $"撤销操作:/{MyCmd.cmd} bk");
        });

        Spawn = false;
    }

    private void WorldGen_AddGenerationPass_string_WorldGenLegacyMethod(On.Terraria.WorldGen.orig_AddGenerationPass_string_WorldGenLegacyMethod orig, string name, WorldGenLegacyMethod method)
    {
        Spawn = Config.SpawnEnabled;
        orig(name, method);
    }
    #endregion

    #region 内嵌资源管理
    private void ExtractData()
    {
        // 创建配置文件夹
        if (!Directory.Exists(MainPath))
            Directory.CreateDirectory(MainPath);
        if (!Directory.Exists(RestoreDir))
            Directory.CreateDirectory(RestoreDir);
        if (!Directory.Exists(AutoSaveDir))
            Directory.CreateDirectory(AutoSaveDir);

        if (Directory.Exists(ClipDir)) return;
        Directory.CreateDirectory(ClipDir);

        var asm = Assembly.GetExecutingAssembly();
        List<string> files = new() { "出生点.clip", "岛主刷怪场普通版.clip", "岛主刷怪场天顶版.clip" };

        foreach (var file in files)
        {
            var res = $"{asm.GetName().Name}.内嵌资源.{file}";

            using var stream = asm.GetManifestResourceStream(res);
            if (stream == null) continue;

            try
            {
                using var fs = File.Create(Path.Combine(ClipDir, file));
                stream.CopyTo(fs);
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"[{PluginName}] 释放内嵌失败 {file}: {ex.Message}");
            }
        }
    }
    #endregion

    #region 游戏更新事件（保存世界快照、渲染粒子）
    public static long Timer = 0;
    private static long SaveTime = 0;
    private void OnGameUpdate(EventArgs args)
    {
        if (!Config.Enabled) return;

        Timer++;

        // 每帧更新边界绘制队列
        if (AnimMag.anim.Count > 0)
            AnimMag.Update();

        if (Config.SaveSnapshot)
        {
            long interval = Config.SaveSnapshotTime * 60 * 60;
            if (Timer - SaveTime >= interval)
            {
                // 生成时间戳，例如 20250429_143022
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                // 临时文件夹路径：备份存档/世界名_时间戳/
                string tempFolder = Path.Combine(AutoSaveDir, $"{Main.worldName}_{timestamp}");
                // 最终的压缩包路径：备份存档/世界名_时间戳.zip
                string zipPath = Path.Combine(AutoSaveDir, $"{Main.worldName}_{timestamp}.zip");

                // 确保备份根目录存在
                if (!Directory.Exists(AutoSaveDir))
                    Directory.CreateDirectory(AutoSaveDir);

                // 创建临时文件夹
                Directory.CreateDirectory(tempFolder);

                // 将快照和标牌保存到临时文件夹内
                Map.SaveSnapshot(TSPlayer.Server, true, Main.worldName, tempFolder);

                // 压缩临时文件夹
                ZipFile.CreateFromDirectory(tempFolder, zipPath, CompressionLevel.Optimal, false);

                // 删除临时文件夹
                Directory.Delete(tempFolder, true);

                SaveTime = Timer;
            }
        }
    }
    #endregion

}