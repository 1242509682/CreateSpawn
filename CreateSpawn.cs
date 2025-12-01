using System.Reflection;
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
    public override Version Version => new(1, 2, 0);
    public override string Description => "使用指令复制区域建筑,支持保存建筑文件、跨地图粘贴、自动区域保护、访客统计、自动清理建筑、区域边界显示、进度限制粘贴";
    #endregion

    #region 注册与释放
    public CreateSpawn(Main game) : base(game) { }
    public override void Initialize()
    {
        LoadConfig();
        ExtractData(); //内嵌资源
        GeneralHooks.ReloadEvent += ReloadConfig;
        ServerApi.Hooks.GamePostInitialize.Register(this, this.GamePost);
        ServerApi.Hooks.GameUpdate.Register(this, OnGameUpdate);
        ServerApi.Hooks.ServerLeave.Register(this, OnServerLeave);
        On.Terraria.WorldGen.AddGenerationPass_string_WorldGenLegacyMethod += WorldGen_AddGenerationPass_string_WorldGenLegacyMethod;
        TShockAPI.Commands.ChatCommands.Add(new Command("create.copy", Commands.CreateSpawnCMD, "cb", "复制建筑"));
        // 注册 RegionHooks 事件
        RegionHooks.RegionEntered += OnRegionEntered;
        RegionHooks.RegionLeft += OnRegionLeft;
        RegionHooks.RegionDeleted += OnRegionDeleted;
        GetDataHandlers.PlayerBuffUpdate.Register(this.PlayerBuffUpdate!);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            TaskManager.ClearAllTasks();
            GeneralHooks.ReloadEvent -= ReloadConfig;
            ServerApi.Hooks.GamePostInitialize.Deregister(this, this.GamePost);
            ServerApi.Hooks.GameUpdate.Deregister(this, OnGameUpdate);
            ServerApi.Hooks.ServerLeave.Deregister(this, OnServerLeave);
            On.Terraria.WorldGen.AddGenerationPass_string_WorldGenLegacyMethod -= WorldGen_AddGenerationPass_string_WorldGenLegacyMethod;
            TShockAPI.Commands.ChatCommands.RemoveAll(x => x.CommandDelegate == Commands.CreateSpawnCMD);
            // 注销 RegionHooks 事件
            RegionHooks.RegionEntered -= OnRegionEntered;
            RegionHooks.RegionLeft -= OnRegionLeft;
            RegionHooks.RegionDeleted -= OnRegionDeleted;
            GetDataHandlers.PlayerBuffUpdate.UnRegister(this.PlayerBuffUpdate!);
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
        if (Config.SpawnEnabled)
        {
            var name = "出生点";
            var clip = Map.LoadClip(name);
            if (clip == null)
            {
                TShock.Utils.Broadcast($"未找到名为 {name} 的建筑!", 240, 250, 150);
                return;
            }

            // 微调参数计算（仿照 SmartSpawn）
            int spwx = Main.spawnTileX; // 出生点 X（单位是图格）
            int spwy = Main.spawnTileY; // 出生点 Y

            int startX = spwx - Config.CentreX + Config.AdjustX;
            int startY = spwy - Config.CountY + Config.AdjustY;

            SmartSpawn(TSPlayer.Server, startX, startY, clip, name);

            Config.SpawnEnabled = false;
            Config.Write();
        }

        AutoClear = new AutoClear(); // 初始化自动清理

        if (Config.VisitRecord.Enabled &&
            Config.VisitRecord.SaveVisitData)
            Map.LoadAllRecords(); // 加载访问记录
    }

    private void WorldGen_AddGenerationPass_string_WorldGenLegacyMethod(On.Terraria.WorldGen.orig_AddGenerationPass_string_WorldGenLegacyMethod orig, string name, WorldGenLegacyMethod method)
    {
        Config.SpawnEnabled = true;
        Config.Write();
        orig(name, method);
    }
    #endregion

    #region 内嵌资源管理
    private void ExtractData()
    {
        if (Directory.Exists(Map.Paths)) return;

        Directory.CreateDirectory(Map.Paths);

        var asm = Assembly.GetExecutingAssembly();
        var files = new List<string>
        {
            "出生点_cp.map",
            "岛主刷怪场_cp.map",
            "岛主天顶刷怪场_cp.map",
        };

        int count = 0;
        foreach (var file in files)
        {
            var res = $"{asm.GetName().Name}.内嵌资源.{file}";

            using (var stream = asm.GetManifestResourceStream(res))
            {
                if (stream == null)
                {
                    TShock.Log.ConsoleError($"[复制建筑] 资源未找到: {res}");
                    continue;
                }

                try
                {
                    using (var fs = File.Create(Path.Combine(Map.Paths, file)))
                    {
                        stream.CopyTo(fs);
                    }
                    count++;
                    TShock.Log.ConsoleInfo($"[复制建筑] 释放: {file}");
                }
                catch (Exception ex)
                {
                    TShock.Log.ConsoleError($"[复制建筑] 释放失败 {file}: {ex.Message}");
                }
            }
        }

        TShock.Log.ConsoleInfo($"[复制建筑] 初始化完成: {count}个文件");
    }
    #endregion

    #region 游戏更新触发事件
    public static int GetUnixTimestamp => (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
    internal static AutoClear AutoClear { get; private set; } // 自动清理管理器
    private void OnGameUpdate(EventArgs args)
    {
        // 更新边界显示（不再需要检查玩家位置）
        MyProjectile.UpdateAll();

        // 自动清理检查
        AutoClear.CheckAutoClear();

        // 处理分帧任务
        TaskManager.OnGameUpdate();
    }
    #endregion

    #region 玩家出服事件
    private void OnServerLeave(LeaveEventArgs args)
    {
        MyProjectile.Stop(args.Who);
        RegionTracker.OnPlayerLeave(args.Who); // 清理区域追踪器
        TaskManager.CancelTask(TShock.Players[args.Who]); // 取消玩家的所有任务
    }
    #endregion

    #region 区域检测事件
    internal static RegionTracker RegionTracker = new(); // 区域访问记录
    private void OnRegionEntered(RegionHooks.RegionEnteredEventArgs args)
    {
        if (RegionManager.IsPluginRegion(args.Region.Name))
        {
            RegionTracker.RegionEntry(args.Player, args.Region.Name);
            MyProjectile.RegionEntry(args.Player, args.Region); // 自动启动边界显示
        }
    }

    private void OnRegionLeft(RegionHooks.RegionLeftEventArgs args)
    {
        if (RegionManager.IsPluginRegion(args.Region.Name))
        {
            RegionTracker.RegionExit(args.Player, args.Region.Name);
            MyProjectile.RegionExit(args.Player, args.Region.Name); // 自动关闭边界显示
        }
    }

    private void OnRegionDeleted(RegionHooks.RegionDeletedEventArgs args)
    {
        if (RegionManager.IsPluginRegion(args.Region.Name))
        {
            RegionTracker.RegionDeleted(args.Region.Name);
        }
    }

    private void PlayerBuffUpdate(object o, GetDataHandlers.PlayerBuffUpdateEventArgs args)
    {
        args.Handled = true;
        var plr = args.Player;
        RegionTracker.RefreshBuffs(plr); // 刷新区域BUFF
    }
    #endregion

    #region 智能选择生成建筑执行模式
    public static void SmartSpawn(TSPlayer plr, int startX, int startY, Building clip, string buildName)
    {
        // 先检查任务
        if (TaskManager.NeedWaitTask(plr)) return;

        // 保存粘贴前的状态
        var beforeState = CopyBuilding(startX, startY, startX + clip.Width - 1, startY + clip.Height - 1);

        // 创建保护区域
        string regionName = "";
        if (Config.AutoCreateRegion)
        {
            regionName = RegionManager.CreateRegion(plr, startX, startY, startX + clip.Width - 1, startY + clip.Height - 1, buildName, clip);
        }

        // 创建操作记录
        var operation = new BuildOperation
        {
            BeforeState = beforeState,
            CreatedRegion = regionName,
            Area = new Rectangle(startX, startY, clip.Width, clip.Height),
            Timestamp = DateTime.Now
        };

        // 保存操作记录
        Map.SaveOperation(plr.Name, operation);

        int StartTime = GetUnixTimestamp;
        int baseX = startX - clip.Origin.X;
        int baseY = startY - clip.Origin.Y;
        int TotalTiles = clip.Width * clip.Height;

        // 先销毁目标区域的互动家具实体（同步执行，操作很快）
        KillAll(startX, startX + clip.Width - 1, startY, startY + clip.Height - 1);

        // 智能选择执行模式
        bool taskStarted = TaskManager.StartSmartTask(plr, TotalTiles,
            () => AsyncSpawn(plr, startX, startY, clip, StartTime),
            new TaskManager.SpawnFrameTask
            {
                Player = plr,
                BuildingData = clip,
                StartX = startX,
                StartY = startY,
                StartTime = StartTime,
                BuildName = buildName,
                Operation = operation,
                TaskType = "生成建筑",
                TotalFrames = (int)Math.Ceiling((double)TotalTiles / Config.TaskConfig.MaxTilesPerFrame),
                ActiveFrame = 0
            },
            "生成建筑"
        );

        if (!taskStarted)
        {
            plr.SendErrorMessage("任务启动失败");
        }
    }
    #endregion

    #region 异步执行生成建筑（小型建筑）
    public static void AsyncSpawn(TSPlayer plr, int startX, int startY, Building clip, int StartTime)
    {
        for (int x = 0; x < clip.Width; x++)
        {
            for (int y = 0; y < clip.Height; y++)
            {
                int worldX = startX + x;
                int worldY = startY + y;

                // 边界检查
                if (worldX < 0 || worldX >= Main.maxTilesX || worldY < 0 || worldY >= Main.maxTilesY)
                {
                    continue;
                }

                // 直接复制图格数据，避免不必要的克隆
                var source = clip.Tiles?[x, y];
                if (source != null)
                {
                    // 使用 CopyFrom 而不是 Clone，性能更好
                    Main.tile[worldX, worldY].CopyFrom(source);
                }
            }
        }

        // 批量发送图格更新（更高效）
        TSPlayer.All.SendTileSquareCentered(startX + clip.Width / 2, startY + clip.Height / 2, (byte)Math.Max(clip.Width, clip.Height));

        // 完成后的操作
        AsyncSpawnEnd(plr, startX, startY, clip, StartTime);
    }
    #endregion

    #region 异步模式完成建筑生成
    public static void AsyncSpawnEnd(TSPlayer plr, int startX, int startY, Building clip, int StartTime)
    {
        int baseX = startX - clip.Origin.X;
        int baseY = startY - clip.Origin.Y;

        // 修复家具和发送完成消息
        FixAll(startX, startX + clip.Width - 1, startY, startY + clip.Height - 1);

        if (plr.HasPermission(Config.IsAdamin) || Config.FixItem)
        {
            RestoreChestItems(clip.ChestItems!, new Point(baseX, baseY));
            RestoreItemFrames(clip.ItemFrames, new Point(baseX, baseY));
            RestorefoodPlatter(clip.FoodPlatters, new Point(baseX, baseY));
            RestoreWeaponsRack(clip.WeaponsRacks, new Point(baseX, baseY));
            RestoreDisplayDoll(clip.DisplayDolls, new Point(baseX, baseY));
            RestoreHatRack(clip.HatRacks, new Point(baseX, baseY));
        }

        RestoreSignText(clip, baseX, baseY);
        RestoreLogicSensor(clip.LogicSensors, new Point(baseX, baseY));

        TileHelper.GenAfter();
        int duration = GetUnixTimestamp - StartTime;
        plr.SendSuccessMessage($"【异步】已生成区域 {clip.RegionName} ({clip.Width} x {clip.Height}), 用时{duration}秒");
    }
    #endregion

    #region 分帧模式生成建筑（大型建筑）
    public static void FrameSpawn(TSPlayer player, int startX, int startY, Building clip, int frameIndex, int totalFrames)
    {
        int MaxTilesPerFrame = Config.TaskConfig.MaxTilesPerFrame;
        int TotalTiles = clip.Width * clip.Height;
        int StartTile = frameIndex * MaxTilesPerFrame;
        int EndTile = Math.Min(StartTile + MaxTilesPerFrame, TotalTiles);

        for (int i = StartTile; i < EndTile; i++)
        {
            int y = i % clip.Height;  // 先计算y坐标（行）
            int x = i / clip.Height;  // 再计算x坐标（列）

            // 边界检查
            if (x < 0 || x >= clip.Width || y < 0 || y >= clip.Height)
                continue;

            int worldX = startX + x;
            int worldY = startY + y;

            // 世界边界检查
            if (worldX < 0 || worldX >= Main.maxTilesX || worldY < 0 || worldY >= Main.maxTilesY)
                continue;

            var source = clip.Tiles?[x, y];
            if (source != null)
            {
                Main.tile[worldX, worldY].CopyFrom(source);
            }
        }

        // 批量发送图格更新（更高效）
        TSPlayer.All.SendTileSquareCentered(startX + clip.Width / 2, startY + clip.Height / 2, (byte)Math.Max(clip.Width, clip.Height));
    }
    #endregion

    #region 分帧模式完成建筑生成
    public static void FrameSpawnEnd(TSPlayer plr, int startX, int startY, Building clip, int StartTime)
    {
        int baseX = startX - clip.Origin.X;
        int baseY = startY - clip.Origin.Y;

        // 修复家具和发送完成消息
        FixAll(startX, startX + clip.Width - 1, startY, startY + clip.Height - 1);

        if (plr.HasPermission(Config.IsAdamin) || Config.FixItem)
        {
            RestoreChestItems(clip.ChestItems!, new Point(baseX, baseY));
            RestoreItemFrames(clip.ItemFrames, new Point(baseX, baseY));
            RestorefoodPlatter(clip.FoodPlatters, new Point(baseX, baseY));
            RestoreWeaponsRack(clip.WeaponsRacks, new Point(baseX, baseY));
            RestoreDisplayDoll(clip.DisplayDolls, new Point(baseX, baseY));
            RestoreHatRack(clip.HatRacks, new Point(baseX, baseY));
        }

        RestoreSignText(clip, baseX, baseY);
        RestoreLogicSensor(clip.LogicSensors, new Point(baseX, baseY));

        TileHelper.GenAfter();

        // 发送完整的图格更新
        TSPlayer.All.SendTileSquareCentered(startX + clip.Width / 2, startY + clip.Height / 2, (byte)Math.Max(clip.Width, clip.Height));

        int duration = GetUnixTimestamp - StartTime;
        plr.SendSuccessMessage($"【分帧】已生成区域 {clip.RegionName} ({clip.Width} x {clip.Height})，用时{duration}秒。");
    }
    #endregion

    #region 智能选择还原建筑执行模式
    public static void SmartBack(TSPlayer plr, int startX, int startY, int endX, int endY, BuildOperation op)
    {
        if (TaskManager.NeedWaitTask(plr)) return;

        int StartTime = GetUnixTimestamp;

        // 检查操作记录
        if (op?.BeforeState == null)
        {
            plr.SendErrorMessage("操作记录数据不完整，无法还原");
            return;
        }

        var building = op.BeforeState;
        int TotalTile = building.Width * building.Height;

        // 智能选择执行模式
        bool TaskStarted = TaskManager.StartSmartTask(plr, TotalTile,
            () => AsyncBack(plr, op, StartTime),
            new TaskManager.BackFrameTask
            {
                Player = plr,
                BuildingData = building,
                OperationData = op,
                StartTime = StartTime,
                TaskType = "还原建筑",
                TotalFrames = (int)Math.Ceiling((double)TotalTile / Config.TaskConfig.MaxTilesPerFrame),
                ActiveFrame = 0
            },
            "还原建筑"
        );

        if (!TaskStarted)
        {
            plr.SendErrorMessage("还原任务启动失败");
        }
    }
    #endregion

    #region 异步模式建筑还原（小型区域）
    public static void AsyncBack(TSPlayer plr, BuildOperation op, int StartTime)
    {
        // 检查操作记录
        if (op == null)
        {
            plr.SendErrorMessage("没有可撤销的操作记录");
            return;
        }

        // 移除保护区域与访问记录文件、内存
        if (!string.IsNullOrEmpty(op.CreatedRegion))
        {
            var region = RegionManager.ParseRegionInput(plr, op.CreatedRegion)!;
            if (region != null)
                TShock.Regions.DeleteRegion(region.Name);
        }

        // 还原建筑
        RollbackBuilding(plr, op.BeforeState);
    }
    #endregion

    #region 异步模式建筑还原完成
    public static void AsyncBackEnd(TSPlayer plr, BuildOperation op, int StartTime)
    {
        TileHelper.GenAfter();
        int duration = GetUnixTimestamp - StartTime;
        plr.SendSuccessMessage($"【异步】{op.CreatedRegion} 建筑还原完成，用时{duration}秒。");
    }
    #endregion

    #region 分帧模式建筑还原（大型区域）
    public static void FrameBack(TSPlayer player, Building building, int FrameIndex, int TotalFrames)
    {
        int MaxTilesPerFrame = Config.TaskConfig.MaxTilesPerFrame;
        int TotalTiles = building.Width * building.Height;
        int StartTile = FrameIndex * MaxTilesPerFrame;
        int EndTile = Math.Min(StartTile + MaxTilesPerFrame, TotalTiles);

        int startX = building.Origin.X;
        int startY = building.Origin.Y;

        for (int i = StartTile; i < EndTile; i++)
        {
            int y = i % building.Height;  // 先计算y坐标（行）
            int x = i / building.Height;  // 再计算x坐标（列）

            int worldX = startX + x;
            int worldY = startY + y;

            // 边界检查
            if (worldX < 0 || worldX >= Main.maxTilesX || worldY < 0 || worldY >= Main.maxTilesY)
                continue;

            // 还原图格数据
            if (building.Tiles != null && x < building.Width && y < building.Height)
            {
                Main.tile[worldX, worldY].CopyFrom(building.Tiles[x, y]);
            }
        }

        // 批量发送图格更新（更高效）
        TSPlayer.All.SendTileSquareCentered(startX + building.Width / 2, startY + building.Height / 2, (byte)Math.Max(building.Width, building.Height));

    }
    #endregion

    #region 分帧模式建筑还原完成
    public static void FrameBackEnd(TSPlayer plr, BuildOperation op, int StartTime)
    {
        // 移除保护区域与访问记录文件、内存
        if (!string.IsNullOrEmpty(op.CreatedRegion))
        {
            var region = RegionManager.ParseRegionInput(plr, op.CreatedRegion);
            if (region != null)
                TShock.Regions.DeleteRegion(region.Name);
        }

        // 还原建筑
        RollbackBuilding(plr, op.BeforeState);

        TileHelper.GenAfter();
        int duration = GetUnixTimestamp - StartTime;
        plr.SendSuccessMessage($"【分帧】{op.CreatedRegion} 建筑还原完成，用时{duration}秒。");
    }
    #endregion
}