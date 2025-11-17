using System.Collections.Concurrent;
using TShockAPI;

namespace CreateSpawn;

public class TaskManager
{
    // 用于追踪每个玩家的放置任务执行状态
    private static readonly ConcurrentDictionary<string, (Task task, CancellationTokenSource cts, DateTime startTime)> PlayerTasks = new();

    #region 检查玩家是否有运行中的任务
    public static bool IsPlayerTaskRunning(TSPlayer player)
    {
        if (PlayerTasks.TryGetValue(player.Name, out var taskInfo))
        {
            // 检查任务状态
            if (taskInfo.task.IsCompleted || taskInfo.task.IsCanceled || taskInfo.task.IsFaulted)
            {
                // 清理已完成的任务
                PlayerTasks.TryRemove(player.Name, out _);
                return false;
            }

            // 检查超时（10分钟）
            if ((DateTime.Now - taskInfo.startTime).TotalMinutes > 10)
            {
                taskInfo.cts.Cancel();
                PlayerTasks.TryRemove(player.Name, out _);
                return false;
            }

            return true;
        }
        return false;
    }
    #endregion

    #region 开始新任务
    public static bool StartPlayerTask(TSPlayer player, out CancellationTokenSource cts)
    {
        cts = null;

        if (IsPlayerTaskRunning(player))
        {
            return false;
        }

        cts = new CancellationTokenSource();
        var task = Task.CompletedTask; // 占位符

        PlayerTasks[player.Name] = (task, cts, DateTime.Now);
        return true;
    }
    #endregion

    #region 设置实际任务
    public static void SetPlayerTask(TSPlayer player, Task task)
    {
        if (PlayerTasks.TryGetValue(player.Name, out var existing))
        {
            PlayerTasks[player.Name] = (task, existing.cts, existing.startTime);
        }
    }
    #endregion

    #region 取消任务
    public static bool CancelPlayerTask(TSPlayer player)
    {
        if (PlayerTasks.TryGetValue(player.Name, out var taskInfo))
        {
            taskInfo.cts.Cancel();
            PlayerTasks.TryRemove(player.Name, out _);
            return true;
        }
        return false;
    }
    #endregion

    #region 完成任务
    public static void FinishPlayerTask(TSPlayer player)
    {
        PlayerTasks.TryRemove(player.Name, out _);
    }
    #endregion

    #region 清理所有任务（服务器关闭时）
    public static void CleanupAllTasks()
    {
        foreach (var taskInfo in PlayerTasks.Values)
        {
            taskInfo.cts.Cancel();
        }
        PlayerTasks.Clear();
    }
    #endregion

    #region 强制终止所有玩家的任务（管理员专用）
    public static int KillAllTasks()
    {
        int killedCount = 0;
        var tasksToKill = new List<string>();

        // 先收集所有需要终止的任务
        foreach (var kvp in PlayerTasks)
        {
            var playerName = kvp.Key;
            var taskInfo = kvp.Value;

            if (!taskInfo.task.IsCompleted && !taskInfo.task.IsCanceled)
            {
                tasksToKill.Add(playerName);
            }
        }

        // 终止任务
        foreach (var playerName in tasksToKill)
        {
            if (PlayerTasks.TryGetValue(playerName, out var taskInfo))
            {
                try
                {
                    taskInfo.cts.Cancel();
                    PlayerTasks.TryRemove(playerName, out _);
                    killedCount++;

                    // 通知在线玩家（如果在线）
                    var onlinePlayer = TShock.Players.FirstOrDefault(p => p?.Name == playerName);
                    onlinePlayer?.SendInfoMessage("您的建筑任务已被管理员强制终止");
                }
                catch (Exception ex)
                {
                    TShock.Log.ConsoleError($"[复制建筑] 终止玩家 {playerName} 的任务时出错: {ex.Message}");
                }
            }
        }

        return killedCount;
    }
    #endregion

    #region 获取所有运行中任务的详细信息
    public static List<(string playerName, DateTime startTime, TimeSpan runningTime)> GetRunningTasksInfo()
    {
        var runningTasks = new List<(string, DateTime, TimeSpan)>();
        var now = DateTime.Now;

        foreach (var kvp in PlayerTasks)
        {
            var playerName = kvp.Key;
            var taskInfo = kvp.Value;

            if (!taskInfo.task.IsCompleted && !taskInfo.task.IsCanceled)
            {
                var runningTime = now - taskInfo.startTime;
                runningTasks.Add((playerName, taskInfo.startTime, runningTime));
            }
        }

        return runningTasks;
    }
    #endregion

    #region 需要等待任务完成
    public static bool NeedWaitTask(TSPlayer plr)
    {
        if (IsPlayerTaskRunning(plr))
        {
            plr?.SendErrorMessage("您有一个任务正在执行，请等待完成后再操作");
            return true;
        }
        return false;
    } 
    #endregion
}