using System.Collections.Concurrent;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;
using Terraria.ID;
using TShockAPI;
using TShockAPI.DB;
using static CreateSpawn.CreateSpawn;
using static Terraria.ID.ProjectileID;

namespace CreateSpawn;

public class ProjectileData
{
    [JsonProperty("启用")]
    public bool Enabled { get; set; } = true;
    [JsonProperty("仅管理可用")]
    public bool AdminOnly { get; set; } = false;
    [JsonProperty("弹幕ID")]
    public int[] ProjList { get; set; } = new int[]
    {
        IceBolt, FrostBoltSword, FrostBoltSword, AmethystBolt,
        SapphireBolt, EmeraldBolt, RubyBolt, DiamondBolt
    };
    [JsonProperty("弹幕变化(0到5)")]
    public int DirectionMode { get; set; } = 0;
    [JsonProperty("变化强度")]
    public float ProjectileSpeed { get; set; } = 10;
    [JsonProperty("弹幕伤害")]
    public int Damage { get; set; } = 20;
    [JsonProperty("移动速度")]
    public int MoveSpeed { get; set; } = 1;
    [JsonProperty("光带长度")]
    public int BandLength { get; set; } = 5;
    [JsonProperty("更新间隔帧数")]
    public int UpdateInterval { get; set; } = 5;
    [JsonProperty("弹幕持续帧数")]
    public int LifeTime { get; set; } = 60;
    [JsonProperty("自动停止秒数")]
    public int AutoStopTime { get; set; } = 0;
    [JsonProperty("离开区域停止")]
    public bool StopWhenLeaveRegion { get; set; } = true;
}

// 弹幕显示管理器
public class ProjectileState
{
    public string RegionName { get; set; }
    public Rectangle Area { get; set; }
    public int Position { get; set; }
    public int UpdateCounter { get; set; }
    public int StopTimer { get; set; }
    public List<int> ActiveProjectiles { get; set; } = new();
    public DateTime LastUpdateTime { get; set; } = DateTime.Now;
}

// 优化的弹幕管理器
public static class MyProjectile
{
    private static readonly ConcurrentDictionary<int, ProjectileState> ProjState = new();
    private static DateTime LastClearTime = DateTime.Now;

    #region 处理玩家进入区域
    public static void RegionEntry(TSPlayer plr, Region region)
    {
        if (!Config.ShowArea.Enabled ||
            plr == null || !plr.Active)
            return;

        // 检查权限
        if (Config.ShowArea.AdminOnly && !plr.HasPermission(Config.IsAdamin))
            return;

        // 检查是否已存在显示
        if (ProjState.ContainsKey(plr.Index)) return;

        // 创建边界显示
        InitState(plr, region);
    }
    #endregion

    #region 处理玩家离开区域
    public static void RegionExit(TSPlayer plr, string regionName)
    {
        if (!Config.ShowArea.Enabled || !Config.ShowArea.StopWhenLeaveRegion)
            return;

        if (ProjState.TryGetValue(plr.Index, out var state))
        {
            Stop(plr.Index);
            plr.SendInfoMessage("已离开区域，边界显示已停止。");
        }

        ProjState.Remove(plr.Index, out _);
    } 
    #endregion

    #region 停止指定玩家的边界显示
    public static void Stop(int Index)
    {
        if (ProjState.TryRemove(Index, out var state))
        {
            ClearProj(state.ActiveProjectiles, Index);
        }
    }

    public static void StopAll()
    {
        foreach (var kvp in ProjState)
        {
            ClearProj(kvp.Value.ActiveProjectiles, kvp.Key);
        }

        ProjState.Clear();
    }
    #endregion

    #region 创建玩家数据
    private static void InitState(TSPlayer plr, Region region)
    {
        var state = new ProjectileState
        {
            RegionName = region.Name,
            Area = new Rectangle(region.Area.Left, region.Area.Top,
                                 region.Area.Width, region.Area.Height),
            Position = 0,
            UpdateCounter = 0,
            StopTimer = 0,
            LastUpdateTime = DateTime.Now
        };

        if (state != null)
        {
            ProjState[plr.Index] = state;
        }
    }
    #endregion

    #region 更新所有玩家显示
    public static void UpdateAll()
    {
        if (!Config.ShowArea.Enabled || ProjState.IsEmpty)
            return;

        // 定期清理（每30秒一次）
        if ((DateTime.Now - LastClearTime).TotalSeconds >= 30)
        {
            ClearState();
            LastClearTime = DateTime.Now;
        }

        foreach (var kvp in ProjState.ToArray())
        {
            Update(kvp.Key, kvp.Value);
        }
    }
    #endregion

    #region 更新单个玩家显示
    private static void Update(int Index, ProjectileState state)
    {
        var plr = TShock.Players[Index];

        // 检查玩家是否有效
        if (plr == null || !plr.Active || !plr.ConnectionAlive)
        {
            Stop(Index);
            return;
        }

        // 检查自动停止
        if (Config.ShowArea.AutoStopTime > 0)
        {
            state.StopTimer++;
            if (state.StopTimer >= Config.ShowArea.AutoStopTime * 60)
            {
                plr.SendInfoMessage($"显示超过 {Config.ShowArea.AutoStopTime} 秒,边界显示已自动停止。");
                Stop(Index);
                return;
            }
        }

        // 检查更新间隔
        state.UpdateCounter++;
        if (state.UpdateCounter < Config.ShowArea.UpdateInterval) return;
        state.UpdateCounter = 0;
        state.LastUpdateTime = DateTime.Now;

        // 清除旧弹幕
        ClearProj(state.ActiveProjectiles, Index);
        state.ActiveProjectiles.Clear();

        // 创建新弹幕
        CreateProjectiles(state, Index);
    }
    #endregion

    #region 边界弹幕
    private static void CreateProjectiles(ProjectileState state, int owner)
    {
        var area = state.Area;
        int total = (area.Width + area.Height) * 2;

        // 计算区域中心点
        Vector2 center = new Vector2(
            (area.X + area.Width / 2f) * 16f + 8f,
            (area.Y + area.Height / 2f) * 16f + 8f
        );

        // 获取弹幕ID列表
        int[] projIds = Config.ShowArea.ProjList;
        if (projIds == null || projIds.Length == 0)
            projIds = new int[] { ProjectileID.TopazBolt };

        // 创建均匀分布的弹幕
        for (int i = 0; i < Config.ShowArea.BandLength; i++)
        {
            int pos = (state.Position + i * (total / Config.ShowArea.BandLength)) % total;
            Point point = GetPoint(area, pos);

            // 弹幕类型
            int type = projIds[i % projIds.Length];

            // 计算位置和速度
            Vector2 pos2 = new Vector2(point.X * 16f + 8f, point.Y * 16f + 8f);
            Vector2 vel = CalcVel(pos2, center, Config.ShowArea.DirectionMode, Config.ShowArea.ProjectileSpeed);

            // 创建弹幕
            int projID = NewProjectile(owner, pos2, vel, type);
            if (projID >= 0)
            {
                state.ActiveProjectiles.Add(projID);
            }
        }

        // 更新位置
        state.Position = (state.Position + Config.ShowArea.MoveSpeed) % total;
    } 
    #endregion

    #region 创建弹幕
    private static int NewProjectile(int owner, Vector2 pos, Vector2 vel, int type)
    {
        int index = Projectile.NewProjectile(Projectile.GetNoneSource(), pos.X, pos.Y, vel.X, vel.Y, type, Config.ShowArea.Damage, 0, owner);
        if (index >= 0 && index < Main.maxProjectiles)
        {
            var proj = Main.projectile[index];
            proj.timeLeft = Config.ShowArea.LifeTime;
            NetMessage.SendData((int)PacketTypes.ProjectileNew, owner, -1, null, index);
            return index;
        }

        return -1;
    } 
    #endregion

    #region 获取边界点
    private static Point GetPoint(Rectangle area, int pos)
    {
        int w = area.Width;
        int h = area.Height;

        if (pos < w)
            return new Point(area.X + pos, area.Y);
        else if (pos < w + h)
            return new Point(area.X + w, area.Y + (pos - w));
        else if (pos < w * 2 + h)
            return new Point(area.X + w - (pos - w - h), area.Y + h);
        else
            return new Point(area.X, area.Y + h - (pos - w * 2 - h));
    }
    #endregion

    #region 计算弹幕速度
    private static Vector2 CalcVel(Vector2 pos, Vector2 center, int mode, float speed)
    {
        if (speed == 0f || mode == 0)
            return Vector2.Zero;

        Vector2 dire = Vector2.Zero;

        switch (mode)
        {
            case 1: // 向外
                dire = Vector2.Normalize(pos - center);
                break;

            case 2: // 向内
                dire = Vector2.Normalize(center - pos);
                break;

            case 3: // 随机
                Random rand = new Random();
                float angle = (float)(rand.NextDouble() * Math.PI * 2);
                dire = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
                break;

            case 4: // 顺时针切线
                Vector2 toCenter1 = center - pos;
                dire = new Vector2(-toCenter1.Y, toCenter1.X);
                dire = Vector2.Normalize(dire);
                break;

            case 5: // 逆时针切线
                Vector2 toCenter2 = center - pos;
                dire = new Vector2(toCenter2.Y, -toCenter2.X);
                dire = Vector2.Normalize(dire);
                break;
        }

        return dire * speed;
    } 
    #endregion

    #region 清理逻辑
    private static void ClearState()
    {
        var now = DateTime.Now;
        var toRemove = new List<int>();

        foreach (var kvp in ProjState)
        {
            // 清理超过60秒没有活动的显示
            if ((now - kvp.Value.LastUpdateTime).TotalSeconds > 60)
            {
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var Index in toRemove)
        {
            Stop(Index);
        }
    }

    // 清理弹幕
    private static void ClearProj(List<int> projs, int owner)
    {
        foreach (int index in projs)
        {
            if (index >= 0 && index < Main.maxProjectiles)
            {
                var proj = Main.projectile[index];
                if (proj.active && proj.owner == owner)
                {
                    proj.timeLeft = 0;
                    proj.Kill();
                    NetMessage.SendData((int)PacketTypes.ProjectileDestroy, owner, -1, null, index);
                }
            }
        }

        projs.Clear();
    }
    #endregion
}