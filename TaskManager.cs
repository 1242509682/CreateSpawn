using System.Collections.Concurrent;
using Newtonsoft.Json;
using TShockAPI;
using static CreateSpawn.CreateSpawn;

namespace CreateSpawn;

// 任务配置数据类
public class TaskConfigData
{
    [JsonProperty("多少图格数启用分帧处理")]
    public int FrameCheckRange { get; set; } = 6000;
    [JsonProperty("处理间隔毫秒")]
    public int FrameInterval { get; set; } = 100;
    [JsonProperty("每帧最大图格数")]
    public int MaxTilesPerFrame { get; set; } = 3000;
    [JsonProperty("每帧最大任务数")]
    public int MaxTasksPerUpdate { get; set; } = 3;
}

public class TaskManager
{
    private static readonly ConcurrentDictionary<string, Task> AsyncTasks = new(); // 异步任务管理

    // 分帧任务管理
    private static readonly ConcurrentDictionary<string, FrameTaskBase> FrameTasks = new();
    private static readonly ConcurrentQueue<FrameTaskBase> FrameTaskQueue = new();
    private static DateTime LastFrameTime = DateTime.Now;

    //  分帧任务基类
    public abstract class FrameTaskBase
    {
        public TSPlayer Player { get; set; }
        public string TaskType { get; set; }
        public int TotalFrames { get; set; }
        public int ActiveFrame { get; set; }
        public DateTime StartTime { get; set; }
        public BuildOperation Operation { get; set; }
        public abstract void HandleFrame(int frameIndex);
        public abstract void OnComplete();
    }

    // 生成建筑分帧任务
    public class SpawnFrameTask : FrameTaskBase
    {
        public Building BuildingData { get; set; }
        public int StartX { get; set; }
        public int StartY { get; set; }
        public new int StartTime { get; set; }
        public string BuildName { get; set; }
        public override void HandleFrame(int frameIndex)
        {
            FrameSpawn(Player, StartX, StartY, BuildingData, frameIndex, TotalFrames);
        }

        public override void OnComplete()
        {
            FrameSpawnEnd(Player, StartX, StartY, BuildingData, StartTime);
        }
    }

    // 还原建筑分帧任务
    public class BackFrameTask : FrameTaskBase
    {
        public Building BuildingData { get; set; }
        public BuildOperation OperationData { get; set; }
        public new int StartTime { get; set; }

        public override void HandleFrame(int frameIndex)
        {
            FrameBack(Player, BuildingData, frameIndex, TotalFrames);
        }

        public override void OnComplete()
        {
            FrameBackEnd(Player, OperationData, StartTime);
        }
    }

    #region 智能选择执行模式 - 根据图格数量决定使用异步还是分帧
    public static bool StartSmartTask(TSPlayer plr, int TotalTile, Action AsyncAction, FrameTaskBase FrameTask, string TaskType)
    {
        if (NeedWaitTask(plr)) return false;

        // 智能选择执行模式
        if (TotalTile <= Config?.TaskConfig?.FrameCheckRange)
        {
            // 小型任务：使用异步模式
            return StartAsyncTask(plr, AsyncAction, TaskType);
        }
        else
        {
            // 大型任务：使用分帧模式
            return StartFrameTask(plr, FrameTask);
        }
    }
    #endregion

    #region 检查是否需要等待任务完成
    public static bool NeedWaitTask(TSPlayer plr)
    {
        if (AsyncTasks.ContainsKey(plr.Name) || FrameTasks.ContainsKey(plr.Name))
        {
            plr.SendErrorMessage("您有一个任务正在执行，请等待完成后再操作");
            return true;
        }
        return false;
    }
    #endregion

    #region 清理所有任务（包括关联区域）
    public static void ClearAllTasks()
    {
        try
        {
            // 1. 清理所有分帧任务关联的区域
            foreach (var taskPair in FrameTasks)
            {
                var task = taskPair.Value;
                if (!string.IsNullOrEmpty(task.Operation.CreatedRegion) && task.Operation != null)
                {
                    try
                    {
                        // 清理区域和访客记录
                        var region = TShock.Regions.GetRegionByName(task.Operation.CreatedRegion);
                        if (region != null)
                        {
                            TShock.Regions.DeleteRegion(task.Operation.CreatedRegion);
                        }

                        // 清理访客记录
                        RegionTracker.RegionVisits.Remove(task.Operation.CreatedRegion);
                        RegionTracker.LastVisitors.Remove(task.Operation.CreatedRegion);

                        // 清理操作记录
                        Map.DeleteRecord(task.Operation.CreatedRegion);

                        TShock.Log.ConsoleInfo($"[复制建筑] 清理任务关联区域: {task.Operation.CreatedRegion}");
                    }
                    catch (Exception ex)
                    {
                        TShock.Log.ConsoleError($"[复制建筑] 清理区域 {task.Operation.CreatedRegion} 时出错: {ex}");
                    }
                }

                // 通知玩家任务被取消
                if (task.Player != null && task.Player.Active)
                {
                    task.Player.SendErrorMessage("您的建筑任务已被管理员取消");
                    task.Player.SendMessage("请使用:/cb bk 还原", Tool.RandomColors());
                }
            }

            // 2. 清理异步任务并通知玩家
            foreach (var taskPair in AsyncTasks)
            {
                try
                {
                    var playerName = taskPair.Key;
                    var task = taskPair.Value;

                    // 查找对应的玩家
                    var other = TShock.Players.FirstOrDefault(p => p?.Name == playerName);
                    if (other != null && other.Active)
                    {
                        other.SendErrorMessage("您的异步建筑任务已被管理员取消");
                        other.SendMessage("请使用:/cb bk 还原", Tool.RandomColors());
                    }
                }
                catch (Exception ex)
                {
                    TShock.Log.ConsoleError($"[复制建筑] 清理异步任务时出错: {ex}");
                }
            }

            // 3. 清空所有任务集合
            AsyncTasks.Clear();     // 清空异步任务字典
            FrameTasks.Clear();     // 清空分帧任务字典
            FrameTaskQueue.Clear(); // 清空分帧任务队列

            TShock.Log.ConsoleInfo("[复制建筑] 已清理所有任务和关联区域");
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[复制建筑] 清理所有任务时发生错误: {ex}");
        }
    }
    #endregion

    #region 清理单个任务及其区域
    public static void CancelTask(TSPlayer plr)
    {
        string name = plr.Name;

        // 清理分帧任务
        if (FrameTasks.TryRemove(name, out FrameTaskBase? frameTask))
        {
            // 通过 Operation 清理关联区域
            if (frameTask.Operation != null && !string.IsNullOrEmpty(frameTask.Operation.CreatedRegion))
            {
                try
                {
                    string RegionName = frameTask.Operation.CreatedRegion;
                    var region = TShock.Regions.GetRegionByName(RegionName);
                    if (region != null)
                    {
                        TShock.Regions.DeleteRegion(RegionName);
                    }

                    RegionTracker.RegionVisits.Remove(RegionName);
                    RegionTracker.LastVisitors.Remove(RegionName);
                    Map.DeleteRecord(RegionName);
                }
                catch (Exception ex)
                {
                    TShock.Log.ConsoleError($"[复制建筑] 清理任务区域失败: {ex}");
                }
            }

            // 从队列中移除
            var newQueue = new ConcurrentQueue<FrameTaskBase>(FrameTaskQueue.Where(t => t.Player.Name != name));
            FrameTaskQueue.Clear();
            foreach (var item in newQueue) FrameTaskQueue.Enqueue(item);

            plr.SendInfoMessage("已取消当前任务及相关区域");
        }

        // 清理异步任务
        if (AsyncTasks.TryRemove(name, out Task? asyncTask))
        {
            plr.SendInfoMessage("已取消当前异步任务");
            plr.SendMessage("请使用:/cb bk 还原", Tool.RandomColors());
        }
    }
    #endregion

    // 模式1 ———— 异步任务管理

    #region  开始异步任务（用于小型建筑）
    public static bool StartAsyncTask(TSPlayer plr, Task task)
    {
        string name = plr.Name;

        // 清理已完成的任务
        if (AsyncTasks.TryGetValue(name, out Task? existingTask))
        {
            if (existingTask.IsCompleted || existingTask.IsFaulted || existingTask.IsCanceled)
            {
                AsyncTasks.TryRemove(name, out _);
            }
            else
            {
                return false;
            }
        }

        return AsyncTasks.TryAdd(name, task);
    }
    #endregion

    #region 完成异步任务
    public static void AsyncTaskEnd(TSPlayer plr)
    {
        string name = plr.Name;
        AsyncTasks.TryRemove(name, out _);
    }
    #endregion

    #region 获取玩家的异步任务
    public static Task? GetAsyncTask(TSPlayer plr)
    {
        string name = plr.Name;
        AsyncTasks.TryGetValue(name, out Task? task);
        return task;
    }
    #endregion

    #region 启动异步任务
    private static bool StartAsyncTask(TSPlayer plr, Action asyncAction, string taskType)
    {
        var task = Task.Run(asyncAction);

        if (!StartAsyncTask(plr, task))
        {
            return false;
        }

        task.ContinueWith(t =>
        {
            try
            {
                if (t.IsFaulted)
                {
                    plr.SendErrorMessage($"{taskType}失败: {t.Exception?.InnerException?.Message}");
                }
                else
                {
                    // 任务成功完成，在这里调用完成方法
                    if (taskType == "生成建筑")
                    {
                        // 获取任务参数并调用完成方法
                        var frameTask = GetFrameTask(plr) as SpawnFrameTask;
                        if (frameTask != null)
                        {
                            AsyncSpawnEnd(plr, frameTask.StartX, frameTask.StartY, frameTask.BuildingData, frameTask.StartTime);
                        }
                    }
                    else if (taskType == "还原建筑")
                    {
                        var frameTask = GetFrameTask(plr) as BackFrameTask;
                        if (frameTask != null)
                        {
                            AsyncBackEnd(plr, frameTask.OperationData, frameTask.StartTime);
                        }
                    }
                }
            }
            finally
            {
                AsyncTaskEnd(plr);
            }
        });

        plr.SendInfoMessage($"开始{taskType}任务（异步模式）");
        return true;
    }
    #endregion

    // 模式2 ———— 分帧任务管理

    #region 开始分帧任务（用于大型建筑）
    public static bool StartFrameTask(TSPlayer plr, FrameTaskBase task)
    {
        string name = plr.Name;

        if (FrameTasks.ContainsKey(name))
        {
            plr.SendErrorMessage("您已有一个任务在运行，请等待完成");
            return false;
        }

        task.StartTime = DateTime.Now;
        FrameTasks[name] = task;
        FrameTaskQueue.Enqueue(task);

        plr.SendInfoMessage($"开始{task.TaskType}任务，预计需要{task.TotalFrames}批次完成");
        return true;
    }
    #endregion

    #region 取消玩家的分帧任务
    public static void CancelFrameTask(TSPlayer player)
    {
        string playerName = player.Name;
        if (FrameTasks.TryRemove(playerName, out FrameTaskBase? task))
        {
            // 从队列中移除
            var newQueue = new ConcurrentQueue<FrameTaskBase>(FrameTaskQueue.Where(t => t.Player.Name != playerName));
            FrameTaskQueue.Clear();
            foreach (var item in newQueue) FrameTaskQueue.Enqueue(item);

            player.SendInfoMessage("已取消当前任务");
        }
    }
    #endregion

    #region 获取玩家的分帧任务
    public static FrameTaskBase? GetFrameTask(TSPlayer player)
    {
        string playerName = player.Name;
        FrameTasks.TryGetValue(playerName, out FrameTaskBase? task);
        return task;
    }
    #endregion

    #region 每帧更新处理分帧任务
    public static void OnGameUpdate()
    {
        if (FrameTaskQueue is null ||
            FrameTaskQueue.IsEmpty) return;

        DateTime now = DateTime.Now;

        // 控制处理频率
        if ((now - LastFrameTime).TotalMilliseconds < Config?.TaskConfig?.FrameInterval)
            return;

        LastFrameTime = now;

        int Frame = 0;
        int maxTasks = Config?.TaskConfig.MaxTasksPerUpdate ?? 3;

        // 处理多个任务，避免排队
        while (Frame < maxTasks && FrameTaskQueue.TryDequeue(out FrameTaskBase? task))
        {
            SoloFrameTask(task);
            Frame++;
        }
    }
    #endregion

    #region 处理单个分帧任务
    private static void SoloFrameTask(FrameTaskBase task)
    {
        try
        {
            // 检查玩家是否在线（排除控制台）
            if (task.Player.Active != true && task.Player != TSPlayer.Server)
            {
                FrameTasks.TryRemove(task.Player.Name, out _);
                TShock.Log.ConsoleInfo($"[复制建筑] 玩家已离线，取消任务: {task.TaskType}");
                return;
            }

            // 执行当前帧
            task.HandleFrame(task.ActiveFrame);
            task.ActiveFrame++;

            // 检查是否完成
            if (task.ActiveFrame >= task.TotalFrames)
            {
                FrameTasks.TryRemove(task.Player.Name, out _);
                task.OnComplete();

                var duration = (DateTime.Now - task.StartTime).TotalSeconds;
                task.Player.SendSuccessMessage($" {task.TaskType} 任务完成，用时{duration:F1}秒");
            }
            else
            {
                // 未完成，重新加入队列
                FrameTaskQueue.Enqueue(task);
            }
        }
        catch (Exception ex)
        {
            // 任务出错，清理并通知玩家
            FrameTasks.TryRemove(task.Player.Name, out _);
            task.Player?.SendErrorMessage($"{task.TaskType}任务执行失败");
            TShock.Log.ConsoleError($"[复制建筑] 分帧任务失败: {ex}");
        }
    }
    #endregion
}