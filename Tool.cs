using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using System.Text;

using TShockAPI;

namespace CreateSpawn;

internal class Tool
{
    #region 逐行渐变色方法
    public static void GradMess(TSPlayer plr, StringBuilder mess)
    {
        var Text = mess.ToString();
        var lines = Text.Split('\n');
        var GradMess = new StringBuilder();
        var start = new Color(166, 213, 234);
        var end = new Color(245, 247, 175);
        for (int i = 0; i < lines.Length; i++)
        {
            if (!string.IsNullOrEmpty(lines[i]))
            {
                float ratio = (float)i / (lines.Length - 1);
                var gradColor = Color.Lerp(start, end, ratio);

                // 将颜色转换为十六进制格式
                string colorHex = $"{gradColor.R:X2}{gradColor.G:X2}{gradColor.B:X2}";

                // 使用颜色标签包装每一行
                GradMess.AppendLine($"[c/{colorHex}:{lines[i]}]");
            }
        }

        plr.SendMessage(GradMess.ToString(), 240, 250, 150);
    }
    #endregion

    #region 随机颜色
    public static Color RandomColors()
    {
        var random = Terraria.Main.rand;
        var r = random.Next(200, 255);
        var g = random.Next(200, 255);
        var b = random.Next(150, 200);
        var color = new Color(r, g, b);
        return color;
    }
    #endregion

    #region 渐变着色方法 + 物品图标解析
    public static string TextGradient(string text)
    {
        // 如果文本中已包含 [c/xxx:] 自定义颜色标签，则不做渐变，只替换图标
        if (text.Contains("[c/"))
        {
            return ReplaceIconsOnly(text);
        }

        var name = new StringBuilder();
        int length = text.Length;

        for (int i = 0; i < length; i++)
        {
            char c = text[i];

            // 检查是否是图标标签 [i:xxx]
            if (c == '[' && i + 2 < length && text[i + 1] == 'i' && text[i + 2] == ':')
            {
                int end = text.IndexOf(']', i);
                if (end != -1)
                {
                    string tag = text.Substring(i, end - i + 1);
                    string content = tag[3..^1]; // 去掉 "[i:" 和 "]"

                    if (int.TryParse(content, out int itemID))
                    {
                        name.Append(ItemIcon(itemID));
                    }
                    else
                    {
                        name.Append(tag); // 无效ID保留原标签
                    }

                    i = end; // 跳过整个标签
                }
                else
                {
                    name.Append(c);
                    i++;
                }
            }
            else
            {
                // 渐变颜色计算
                var start = new Color(166, 213, 234);
                var endColor = new Color(245, 247, 175);
                float ratio = (float)i / (length - 1);
                var color = Color.Lerp(start, endColor, ratio);

                name.Append($"[c/{color.Hex3()}:{c}]");
            }
        }

        return name.ToString();
    }
    #endregion

    #region 只替换图标，不做渐变
    public static string ReplaceIconsOnly(string text)
    {
        var result = new StringBuilder();
        int index = 0;
        int length = text.Length;

        while (index < length)
        {
            char c = text[index];

            if (c == '[' && index + 2 < length && text[index + 1] == 'i' && text[index + 2] == ':')
            {
                int end = text.IndexOf(']', index);
                if (end != -1)
                {
                    string tag = text.Substring(index, end - index + 1);
                    string content = tag[3..^1];

                    if (int.TryParse(content, out int itemID))
                    {
                        result.Append(ItemIcon(itemID));
                    }
                    else
                    {
                        result.Append(tag);
                    }

                    index = end + 1;
                }
                else
                {
                    result.Append(c);
                    index++;
                }
            }
            else
            {
                result.Append(c);
                index++;
            }
        }

        return result.ToString();
    }
    #endregion

    #region 返回物品图标方法
    // 方法：ItemIcon，根据给定的物品对象返回插入物品图标的格式化字符串
    public static string ItemIcon(Item item)
    {
        return ItemIcon(item.type);
    }

    // 方法：ItemIcon，根据给定的物品ID返回插入物品图标的格式化字符串
    public static string ItemIcon(ItemID itemID)
    {
        return ItemIcon(itemID);
    }

    // 方法：ItemIcon，根据给定的物品整型ID返回插入物品图标的格式化字符串
    public static string ItemIcon(int itemID)
    {
        return $"[i:{itemID}]";
    }
    #endregion
}
