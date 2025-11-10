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
        IceBolt,FrostBoltSword,FrostBoltSword,AmethystBolt,
        SapphireBolt,EmeraldBolt,RubyBolt,DiamondBolt
    };
    [JsonProperty("移动速度")]
    public int MoveSpeed { get; set; } = 1;
    [JsonProperty("光带长度")]
    public int BandLength { get; set; } = 5;
    [JsonProperty("更新间隔帧数")]
    public int UpdateInterval { get; set; } = 5;
    [JsonProperty("弹幕持续时间")]
    public int LifeTime { get; set; } = 60;
    [JsonProperty("自动停止秒数")]
    public int AutoStopTime { get; set; } = 0;
}

// 简化的数据结构
public class ProjectileManager
{
    public string RegionName; // 区域名称
    public Rectangle Area; // 区域边界
    public int Position; // 当前位置
    public int UpdateCount; // 更新计数器
    public int Timer;  // 自动停止计时器
    public List<int> Projectiles = new List<int>(); // 当前弹幕索引列表
}

internal class MyProjectile
{
    public static Dictionary<int, ProjectileManager> ProjectilesInfo = new Dictionary<int, ProjectileManager>();
    internal static void RegionProjectile()
    {
        // 快速检查是否有任何跑马灯需要更新
        if (ProjectilesInfo.Count == 0 || Config.ShowArea is null) return;

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

            // 检查玩家是否仍在区域内
            if (!InRegion(plr, data.RegionName))
            {
                Stop(PlayerIndex);
                continue;
            }

            // 自动停止检查
            if (Config.ShowArea.AutoStopTime > 0)
            {
                data.Timer++;
                if (data.Timer >= Config.ShowArea.AutoStopTime * 60) // 转换为帧数
                {
                    plr.SendInfoMessage("边界显示已自动停止。");
                    Stop(PlayerIndex);
                    continue;
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

            // 创建新弹幕（均匀分布在边界上）
            for (int i = 0; i < Config.ShowArea.BandLength; i++)
            {
                // 计算每个弹幕在边界上的位置，确保它们均匀分布
                int newPos = (pos + i * (total / Config.ShowArea.BandLength)) % total;
                Point point;
                point = GetPoint(area, newPos);

                // 选择弹幕ID（循环使用数组中的ID）
                int type = id[i % id.Length];

                // 创建弹幕
                float wx = point.X * 16f + 8f;
                float wy = point.Y * 16f + 8f;

                int projIndex = Projectile.NewProjectile(Projectile.GetNoneSource(), wx, wy, 0f, 0f, type, 0, 0f, PlayerIndex);
                var proj = Main.projectile[projIndex];
                if (projIndex >= 0 && projIndex < Main.maxProjectiles && proj.active)
                {
                    proj.timeLeft = Config.ShowArea.LifeTime;
                    data.Projectiles.Add(projIndex);
                    NetMessage.SendData((int)PacketTypes.ProjectileNew, PlayerIndex, -1, null, projIndex);
                }
            }

            data.Position = (pos + Config.ShowArea.MoveSpeed) % total; ;
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
