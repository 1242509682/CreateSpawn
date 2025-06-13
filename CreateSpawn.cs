﻿using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Generation;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;
using static CreateSpawn.Utils;

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

    #region 全局变量
    internal static Configuration Config = new();
    public static int GetUnixTimestamp => (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds; 
    #endregion

    #region 配置重载读取与写入方法
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

    #region 生成建筑方法
    public static Task SpawnBuilding(TSPlayer plr, int startX, int startY, Building clip)
    {
        TileHelper.StartGen();
        //缓存 方便粘贴错了还原
        SaveOrigTile(plr, startX, startY, startX + clip.Width - 1, startY + clip.Height - 1);
        int secondLast = GetUnixTimestamp;

        // 定义偏移坐标（从原始世界坐标到玩家头顶）
        int baseX = startX - clip.Origin.X;
        int baseY = startY - clip.Origin.Y;

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

            // 修复家具实体
            FixAll(startX, startX + clip.Width - 1, startY, startY + clip.Height - 1);
            // 修复箱子内物品
            RestoreChestItems(clip.ChestItems!, new Point(baseX, baseY));
            // 修复标牌信息
            RestoreSignText(clip, baseX, baseY);

        }).ContinueWith(_ =>
        {
            TileHelper.GenAfter();
            int value = GetUnixTimestamp - secondLast;
            plr.SendSuccessMessage($"已粘贴区域 ({clip.Width}x{clip.Height})，用时{value}秒。");
        });
    }
    #endregion

    #region 还原建筑方法
    public static Task AsyncBack(TSPlayer plr, int startX, int startY, int endX, int endY)
    {
        TileHelper.StartGen();
        int secondLast = GetUnixTimestamp;
        return Task.Run(delegate
        {
            //还原前缓存一遍
            SaveOrigTile(plr, startX, startY, endX, endY);
            //还原方法
            RollbackBuilding(plr);

        }).ContinueWith(delegate
        {
            TileHelper.GenAfter();
            int value = GetUnixTimestamp - secondLast;
            plr.SendSuccessMessage($"已将选区还原，用时{value}秒。");
        });
    }
    #endregion

}