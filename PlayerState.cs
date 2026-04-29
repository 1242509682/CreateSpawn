using Microsoft.Xna.Framework;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using static CreateSpawn.Utils;

namespace CreateSpawn;

internal class PlayerState
{
    #region 玩家数据状态类（内存）
    public class MyData
    {
        // 创建时的区域名称
        public string RegionName { get; set; } = string.Empty;
        public List<string> AllowedPlayer { get; set; } = new();
        public List<string> AllowedGroup { get; set; } = new();

        // 1 创建 2 删除 3 修改禁止建筑 4 修改白名单玩家 5 修改白名单用户组
        public int Mode { get; set; } = 0;

        // 地图快照路径,用来修复局部图格
        public string rwSnap { get; set; } = string.Empty;
        // 标牌文件路径,用来修复标牌信息
        public string rwSign { get; set; } = string.Empty;
        // 获取到快照,开启精密线控仪检测
        public bool rwFix { get; set; } = false;
        // 待保存的建筑名称
        public string rwCopy { get; set; } = string.Empty;

        // 统一的区域操作模式
        public int rw = 0;          // 操作编号 1清理 2半砖 3方块 4墙壁 5电路 6喷漆 7液体
        public int rwA1 = 0;        // 参数1
        public int rwA2 = 0;        // 参数2
        public int rwA3 = 0;        // 参数3
        public int rwDir = 0;       // 操作时玩家的朝向（1右 -1左）
        public int rwToolMode = 0; // 当前操作的精密线控仪工具模式（电线颜色）

        public void Reset()
        {
            RegionName = string.Empty;
            AllowedPlayer.Clear();
            AllowedGroup.Clear();
            Mode = 0;
        }
    }
    #endregion

    #region 获取数据方法
    public static Dictionary<string, MyData> PlrData = new();
    public static MyData GetData(string name)
    {
        if (!PlrData.ContainsKey(name))
            PlrData[name] = new MyData();

        return PlrData[name];
    }
    #endregion

    #region 精密线控仪事件（创建与删除区域）
    public static void SetRegion(GetDataHandlers.MassWireOperationEventArgs e, TSPlayer plr, MyData Mydata, int x1, int y1, Rectangle rect)
    {
        var region = TShock.Regions.Regions.FirstOrDefault(r => r.InArea(rect));

        switch (Mydata.Mode)
        {
            case 1: // 创建区域
                if (!string.IsNullOrEmpty(Mydata.RegionName))
                {
                    // 检查名称是否已存在
                    var NewRegion = TShock.Regions.GetRegionByName(Mydata.RegionName);
                    if (NewRegion != null)
                    {
                        SendMess(plr, $"区域 {Mydata.RegionName} 已存在");
                        AnimMag.Add(NewRegion.Area);
                    }
                    else if (region != null)
                    {
                        SendMess(plr, $"区域创建失败：与现有区域重叠，请重试");
                        AnimMag.Add(region.Area);
                    }
                    else
                    {
                        var worldid = Main.worldID.ToString();
                        if (TShock.Regions.AddRegion(x1, y1, rect.Width, rect.Height, Mydata.RegionName, plr.Name, worldid))
                        {
                            // 记录到配置文件
                            if (!Plugin.Config.CreatedReg.Contains(Mydata.RegionName))
                            {
                                Plugin.Config.CreatedReg.Add(Mydata.RegionName);
                                Plugin.Config.Write();
                            }

                            SendMess(plr, $"区域 {Mydata.RegionName} 创建成功");
                            var created = TShock.Regions.GetRegionByName(Mydata.RegionName);
                            if (created != null) AnimMag.Add(created.Area);
                        }
                    }
                }
                Mydata.Reset();
                e.Handled = true;
                break;

            case 2: // 删除区域
                if (region == null)
                    SendMess(plr, $"未找到区域");
                else
                {
                    AnimMag.Add(region.Area);
                    TShock.Regions.DeleteRegion(region.Name);
                    SendMess(plr, $"已删除区域 {region.Name}");
                }

                Mydata.Reset();
                e.Handled = true;
                break;

            case 3: // 切换保护状态
                if (region == null)
                    SendMess(plr, $"未找到区域");
                else
                {
                    bool newState = !region.DisableBuild;
                    TShock.Regions.SetRegionState(region.Name, newState);
                    SendMess(plr, $"区域 {region.Name} 禁止建筑已切换为 {(newState ? "开启" : "关闭")}");
                    AnimMag.Add(region.Area);
                }

                Mydata.Reset();
                e.Handled = true;
                break;

            case 4: // 切换玩家白名单
                if (region == null)
                {
                    SendMess(plr, $"未找到区域");
                    Mydata.Reset();
                    e.Handled = true;
                    break;
                }

                if (Mydata.AllowedPlayer.Count == 0)
                {
                    SendMess(plr, $"未指定玩家名");
                    Mydata.Reset();
                    e.Handled = true;
                    break;
                }

                SetPlayers(plr, Mydata, region);
                AnimMag.Add(region.Area);
                Mydata.Reset();
                e.Handled = true;
                break;

            case 5: // 切换组白名单
                if (region == null)
                {
                    SendMess(plr, $"未找到区域");
                    Mydata.Reset();
                    e.Handled = true;
                    break;
                }

                if (Mydata.AllowedGroup.Count == 0)
                {
                    SendMess(plr, $"未指定组名");
                    Mydata.Reset();
                    e.Handled = true;
                    break;
                }

                SetGroup(plr, Mydata, region);
                AnimMag.Add(region.Area);
                Mydata.Reset();
                e.Handled = true;
                break;
        }
    }
    #endregion

    #region 修改区域白名单玩家方法
    public static void SetPlayers(TSPlayer plr, MyData data, TShockAPI.DB.Region region)
    {
        List<string> added = new();
        List<string> removed = new();
        List<string> invalid = new();
        foreach (string name in data.AllowedPlayer)
        {
            var acc = TShock.UserAccounts.GetUserAccountByName(name);
            if (acc == null)
            {
                invalid.Add(name);
                continue;
            }

            if (region.AllowedIDs.Contains(acc.ID))
            {
                TShock.Regions.RemoveUser(region.Name, name);
                removed.Add(name);
            }
            else
            {
                TShock.Regions.AddNewUser(region.Name, name);
                added.Add(name);
            }
        }
        if (added.Count > 0)
            SendMess(plr, $"已添加：{string.Join(",", added)}");
        if (removed.Count > 0)
            SendMess(plr, $"已移除：{string.Join(",", removed)}");
        if (invalid.Count > 0)
            SendMess(plr, $"无效玩家：{string.Join(",", invalid)}");
    }
    #endregion

    #region 修改区域白名单组方法
    public static void SetGroup(TSPlayer plr, MyData data, TShockAPI.DB.Region region)
    {
        List<string> gadded = new();
        List<string> gremoved = new();
        List<string> ginvalid = new();
        foreach (string gp in data.AllowedGroup)
        {
            var group = TShock.Groups.GetGroupByName(gp);
            if (group == null)
            {
                ginvalid.Add(gp);
                continue;
            }
            if (region.AllowedGroups.Contains(gp))
            {
                TShock.Regions.RemoveGroup(region.Name, gp);
                gremoved.Add(gp);
            }
            else
            {
                TShock.Regions.AllowGroup(region.Name, gp);
                gadded.Add(gp);
            }
        }
        if (gadded.Count > 0)
            SendMess(plr, $"已添加组：{string.Join(",", gadded)}");
        if (gremoved.Count > 0)
            SendMess(plr, $"已移除组：{string.Join(",", gremoved)}");
        if (ginvalid.Count > 0)
            SendMess(plr, $"无效组：{string.Join(",", ginvalid)}");
    }
    #endregion

    #region 玩家离开服务器清理数据方法
    internal static void OnLeave(LeaveEventArgs args)
    {
        var plr = TShock.Players[args.Who];
        if (plr != null && PlrData.ContainsKey(plr.Name))
            PlrData.Remove(plr.Name);
    }
    #endregion
}