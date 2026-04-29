using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Drawing;
using Terraria.ID;
using TShockAPI;

namespace CreateSpawn;

internal class AnimMag
{
    #region 动画队列数据
    public static List<AnimTask> anim = new();
    public class AnimTask
    {
        // 四个顶点（顺时针或逆时针）
        public Vector2 A, B, C, D;
        // 当前绘制到第几条边 (0-3)
        public short Edge;
        // 上次绘制时间帧
        public long Last;
    } 
    #endregion

    #region 添加动画方法
    public static void Add(Rectangle Area)
    {
        // 计算四个顶点世界坐标（像素）
        Vector2 a = new(Area.X * 16f + 8f, Area.Y * 16f + 8f);               // 左上
        Vector2 b = new((Area.X + Area.Width) * 16f + 8f, Area.Y * 16f + 8f);   // 右上
        Vector2 c = new((Area.X + Area.Width) * 16f + 8f, (Area.Y + Area.Height) * 16f + 8f); // 右下
        Vector2 d = new(Area.X * 16f + 8f, (Area.Y + Area.Height) * 16f + 8f);   // 左下

        // 添加粒子动画,每个延迟30帧（半秒）
        anim.Add(new AnimTask() { A = a, B = b, C = c, D = d, Edge = 0, Last = Plugin.Timer - 30 });
    }
    #endregion

    #region 更新动画方法
    public static void Update()
    {
        long now = Plugin.Timer;
        for (int i = anim.Count - 1; i >= 0; i--)
        {
            var t = anim[i];
            if (now - t.Last < 60) continue; // 每秒绘制一条边

            t.Last = now;

            if (t.Edge == 0)      // 上边 a->b
                Spawn(t.A, t.B);
            else if (t.Edge == 1) // 右边 b->c
                Spawn(t.B, t.C);
            else if (t.Edge == 2) // 下边 c->d
                Spawn(t.C, t.D);
            else if (t.Edge == 3) // 左边 d->a
                Spawn(t.D, t.A);

            t.Edge++;
            if (t.Edge >= 4)
                anim.RemoveAt(i);
        }
    }
    #endregion

    #region 生成粒子动画方法
    private static void Spawn(Vector2 from, Vector2 to)
    {
        int[] itemID =
        [
            ItemID.FragmentVortex,
            ItemID.FragmentNebula,
            ItemID.FragmentSolar,
            ItemID.FragmentStardust,
            ItemID.Yoraiz0rWings,
            ItemID.Yoraiz0rDarkness,
            ItemID.NebulaPickup1,
            ItemID.NebulaPickup2,
            ItemID.NebulaPickup3
        ];

        var set = new ParticleOrchestraSettings
        {
            PositionInWorld = from,
            MovementVector = to - from,
            UniqueInfoPiece = itemID[Main.rand.Next(itemID.Length)],
            IndexOfPlayerWhoInvokedThis = 255
        };
        ParticleOrchestrator.BroadcastOrRequestParticleSpawn(ParticleOrchestraType.ItemTransfer, set);
    } 
    #endregion
}
