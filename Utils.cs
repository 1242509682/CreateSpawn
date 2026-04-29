using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Terraria;
using TShockAPI;
using static CreateSpawn.Plugin;

namespace CreateSpawn;

internal class Utils
{
    #region 调试信息
    public static void Log(string text)
    {
        TSPlayer.All.SendMessage(TextGrad(text),color);
        TShock.Log.ConsoleInfo(text,color2);
    }
    #endregion

    #region 根据真实玩家发送消息
    public static void SendMess(TSPlayer plr, string help)
    {
        if (plr.RealPlayer)
            plr.SendMessage(TextGrad(help), color);
        else
            plr.SendMessage(help, color);
    }
    #endregion

    #region 单色与随机色
    public static Color color => new(240, 250, 150); // 奶黄色
    public static Color color2 => new(Main.rand.Next(180, 250), // 随机色
                                      Main.rand.Next(180, 250),
                                      Main.rand.Next(180, 250));
    #endregion

    #region 渐变色方法
    public static string TextGrad(string text, TSPlayer? plr = null)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // 检查是否已包含颜色标签
        if (text.Contains("[c/") || text.Contains("[i:"))
        {
            // 如果有颜色标签，保留它们并处理其他部分
            return MixedText(text);
        }
        else
        {
            // 如果没有颜色标签，直接应用渐变
            return Grad(text);
        }
    }
    #endregion

    #region 混合文本
    // 匹配颜色标签 [c/颜色:文本] 或 物品图标标签 [i:物品ID] 或 [i/s数量:物品ID]
    private static Regex regex => new Regex(@"(\[c/([0-9a-fA-F]+):([^\]]+)\]|\[i(?:/s\d+)?:\d+\])");
    private static string MixedText(string text)
    {
        var res = new StringBuilder();
        var mats = regex.Matches(text);
        if (mats.Count == 0) return Grad(text);

        int idx = 0;
        foreach (Match m in mats.Cast<Match>())
        {
            // 添加标签前的普通文本（应用渐变）
            if (m.Index > idx) res.Append(Grad(text.Substring(idx, m.Index - idx)));

            // 添加标签本身
            res.Append(m.Value);
            idx = m.Index + m.Length;
        }

        // 添加最后一个标签后的普通文本
        if (idx < text.Length) res.Append(Grad(text.Substring(idx)));

        return res.ToString();
    }
    #endregion

    #region 返回物品图标方法
    // 根据物品ID返回物品图标
    public static string Icon(int itemID) => $"[i:{itemID}]";
    // 返回带数量的物品图标
    public static string Icon(int itemID, int stack = 1) => $"[i/s{stack}:{itemID}]";
    #endregion

    #region 文本渐变方法
    public static string Grad(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var res = new StringBuilder();
        var start = new Color(165, 210, 235);
        var end = new Color(245, 250, 175);

        // 计算有效字符数（排除换行符）
        int cnt = 0;

        foreach (char c in text)
            if (c != '\n' && c != '\r') cnt++;

        // 如果没有有效字符，直接返回
        if (cnt == 0) return text;

        int idx = 0;

        foreach (char c in text)
        {
            if (c == '\n' || c == '\r')
            {
                res.Append(c);
                continue;
            }

            // 计算渐变比例
            float ratio = (float)idx / (cnt - 1);
            var clr = Color.Lerp(start, end, ratio);

            // 添加到结果
            res.Append($"[c/{clr.Hex3()}:{c}]");
            idx++;
        }

        return res.ToString();
    }
    #endregion

    #region 格式化备份文件信息
    public static string FormatBakInfo(string filePath, int index)
    {
        try
        {
            var name = Path.GetFileNameWithoutExtension(filePath);
            var time = File.GetCreationTime(filePath);
            var size = new FileInfo(filePath).Length;
            var sizeText = size < 1024 * 1024 ?
                $"{(size / 1024.0):F1}KB" :
                $"{(size / (1024.0 * 1024.0)):F1}MB";

            return $"{index}. {name} ({time:MM-dd HH:mm}, {sizeText})";
        }
        catch
        {
            return $"{index}. 文件信息获取失败";
        }
    }
    #endregion

    #region 获取备份文件列表
    public static List<string> GetBakList()
    {
        var list = new List<string>();
        try
        {
            var files = GetBakFiles();
            for (int i = 0; i < files.Length; i++)
            {
                list.Add(FormatBakInfo(files[i], i + 1));
            }
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"获取地图快照列表失败: {ex}");
        }
        return list;
    }
    #endregion

    #region 获取备份文件压缩包
    public static string[] GetBakFiles()
    {
        try
        {
            if (!Directory.Exists(AutoSaveDir))
                return Array.Empty<string>();

            var files = Directory.GetFiles(AutoSaveDir, "*.zip")
                .OrderByDescending(File.GetCreationTime)
                .ToArray();

            return files;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
    #endregion
}
