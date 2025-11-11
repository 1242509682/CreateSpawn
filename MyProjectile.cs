using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;
using Terraria.ID;
using TShockAPI;
using static Terraria.ID.ProjectileID;
using static CreateSpawn.CreateSpawn;

namespace CreateSpawn;

public class ProjData
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
    public int DirectionMode { get; set; } = 0; // 0=静止, 1=向外, 2=向内, 3=随机, 4=顺时针切线, 5=逆时针切线
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
    public int AutoStopTime { get; set; } = 10;
    [JsonProperty("离开区域停止")]
    public bool StopWhenLeaveRegion { get; set; } = true;
}

// 简化的数据结构
public class ProjectileManager
{
    public string RegionName; // 区域名称
    public Rectangle Area; // 区域边界
    public int Position; // 当前位置
    public int UpdateCount; // 更新计数器
    public int StopTimer;  // 自动停止计时器
    public List<int> Projectiles = new List<int>(); // 当前弹幕索引列表
}

internal class MyProjectile
{
    public static Dictionary<int, ProjectileManager> ProjectilesInfo = new Dictionary<int, ProjectileManager>();
    internal static void RegionProjectile()
    {
        // 快速检查是否有任何跑马灯需要更新
        if (ProjectilesInfo.Count == 0 || Config.ShowArea is null || !Config.ShowArea.Enabled) return;

        // 使用数组避免字典枚举开销
        var Players = new int[ProjectilesInfo.Count];
        ProjectilesInfo.Keys.CopyTo(Players, 0);

        foreach (int PlayerIndex in Players)
        {
            var data = ProjectilesInfo[PlayerIndex];
            if (data is null) continue;

            var plr = TShock.Players[PlayerIndex];

            // 检查是否仅管理员可用
            if (Config.ShowArea.AdminOnly && !plr.HasPermission(Config.IsAdamin))
            {
                Stop(PlayerIndex);
                continue;
            }

            // 快速玩家检查
            if (plr is null || !plr.Active || !plr.ConnectionAlive || PlayerIndex < 0 || PlayerIndex >= Main.maxPlayers)
            {
                Stop(PlayerIndex);
                continue;
            }

            // 检查玩家是否仍在区域内（如果启用了离开区域停止）
            if (Config.ShowArea.StopWhenLeaveRegion && !InRegion(plr, data.RegionName))
            {
                plr.SendInfoMessage("你已离开区域,边界显示已停止。");
                Stop(PlayerIndex);
                continue;
            }

            // 自动停止检查
            if (Config.ShowArea.AutoStopTime > 0)
            {
                data.StopTimer++;
                if (data.StopTimer >= Config.ShowArea.AutoStopTime * 60) // 转换为秒
                {
                    plr.SendInfoMessage("边界显示已自动停止。");
                    Stop(PlayerIndex);
                    continue;
                }
                else if (InRegion(plr, data.RegionName) && data.StopTimer % 60 == 0) // 在建筑内自动停止前每秒发送一次倒计时秒数
                {
                    // 计算剩余时间（倒计时）- 将帧转换为秒
                    int Remaining = Config.ShowArea.AutoStopTime - (data.StopTimer / 60);
                    // 格式化倒计时文本
                    string text = $"{Remaining:F0}";
                    var color = new Color(250, 240, 150);
                    TSPlayer.All.SendData(PacketTypes.CreateCombatTextExtended, text,
                                         (int)color.PackedValue,
                                         plr.TPlayer.position.X,
                                         plr.TPlayer.position.Y - 3, 0f, 0);
                }
            }

            // 更新计数器
            data.UpdateCount++;
            if (data.UpdateCount < Config.ShowArea.UpdateInterval) continue;
            data.UpdateCount = 0;

            // 清除旧弹幕
            Clear(data.Projectiles, PlayerIndex);
            data.Projectiles.Clear();

            // 计算边界点并创建新弹幕
            var area = data.Area;
            int total = (area.Width + area.Height) * 2;
            int pos = data.Position;

            int[] id = Config.ShowArea.ProjList;
            if (id == null || id.Length == 0)
                id = new int[] { ProjectileID.TopazBolt };

            // 计算区域中心点（用于方向计算）
            Vector2 center = new Vector2(
                (area.X + area.Width / 2f) * 16f + 8f,
                (area.Y + area.Height / 2f) * 16f + 8f
            );

            // 创建新弹幕（均匀分布在边界上）
            for (int i = 0; i < Config.ShowArea.BandLength; i++)
            {
                // 计算每个弹幕在边界上的位置，确保它们均匀分布
                int newPos = (pos + i * (total / Config.ShowArea.BandLength)) % total;
                Point point;
                point = GetPoint(area, newPos);

                // 选择弹幕ID（循环使用数组中的ID）
                int type = id[i % id.Length];

                // 计算弹幕位置
                float wx = point.X * 16f + 8f;
                float wy = point.Y * 16f + 8f;
                Vector2 position = new Vector2(wx, wy);

                // 计算弹幕速度
                Vector2 velocity = GetVelocity(position, center, Config.ShowArea.DirectionMode, Config.ShowArea.ProjectileSpeed);

                // 创建弹幕
                int Index = Projectile.NewProjectile(Projectile.GetNoneSource(), position.X, position.Y, velocity.X, velocity.Y, type, Config.ShowArea.Damage, 0, PlayerIndex);

                var proj = Main.projectile[Index];

                if (Index >= 0 && Index < Main.maxProjectiles && proj.active)
                {
                    proj.timeLeft = Config.ShowArea.LifeTime;
                    data.Projectiles.Add(Index);
                    NetMessage.SendData((int)PacketTypes.ProjectileNew, PlayerIndex, -1, null, Index);
                }
            }

            data.Position = (pos + Config.ShowArea.MoveSpeed) % total;
        }
    }

    #region 获取坐标
    private static Point GetPoint(Rectangle area, int currPos)
    {
        Point point;
        // 内联边界点计算
        if (currPos < area.Width)
        {
            point = new Point(area.X + currPos, area.Y);
        }
        else if (currPos < area.Width + area.Height)
        {
            point = new Point(area.X + area.Width, area.Y + (currPos - area.Width));
        }
        else if (currPos < area.Width * 2 + area.Height)
        {
            point = new Point(area.X + area.Width - (currPos - area.Width - area.Height), area.Y + area.Height);
        }
        else
        {
            point = new Point(area.X, area.Y + area.Height - (currPos - area.Width * 2 - area.Height));
        }

        return point;
    }
    #endregion

    #region 计算弹幕速度
    private static Vector2 GetVelocity(Vector2 position, Vector2 center, int directionMode, float speed)
    {
        if (speed == 0f) return Vector2.Zero;

        Vector2 direction = Vector2.Zero;

        switch (directionMode)
        {
            case 0: // 静止
                return Vector2.Zero;

            case 1: // 向外
                direction = Vector2.Normalize(position - center);
                break;

            case 2: // 向内
                direction = Vector2.Normalize(center - position);
                break;

            case 3: // 随机
                Random rand = new Random();
                float angle = (float)(rand.NextDouble() * Math.PI * 2);
                direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
                break;

            case 4: // 切线方向（顺时针）
                Vector2 toCenter = center - position;
                direction = new Vector2(-toCenter.Y, toCenter.X); // 垂直向量
                direction = Vector2.Normalize(direction);
                break;

            case 5: // 切线方向（逆时针）
                Vector2 toCenter2 = center - position;
                direction = new Vector2(toCenter2.Y, -toCenter2.X); // 反向垂直向量
                direction = Vector2.Normalize(direction);
                break;

            default:
                return Vector2.Zero;
        }

        return direction * speed;
    }
    #endregion

    #region 清理弹幕方法
    private static void Clear(List<int> projectiles, int owner)
    {
        foreach (int Index in projectiles)
        {
            if (Index >= 0 && Index < Main.maxProjectiles)
            {
                var proj = Main.projectile[Index];
                if (proj.active && proj.owner == owner)
                {
                    proj.timeLeft = 0;
                    proj.Kill();
                    NetMessage.SendData((int)PacketTypes.ProjectileDestroy, owner, -1, null, Index);
                }
            }
        }
    }
    #endregion

    #region 停止弹幕方法
    public static void Stop(int PlayerIndex)
    {
        if (ProjectilesInfo.TryGetValue(PlayerIndex, out var data))
        {
            Clear(data.Projectiles, PlayerIndex);
            ProjectilesInfo.Remove(PlayerIndex);
        }
    }
    #endregion

    #region 判断玩家是否在区域
    public static bool InRegion(TSPlayer plr, string RegionName)
    {
        if (plr != null &&
            plr.Active &&
            plr.CurrentRegion != null &&
            plr.CurrentRegion.Name == RegionName)
            return true;

        return false;
    }
    #endregion
}