//
// 相关文档:
// - 实体关系图: 文档/04-数据库设计/01-实体关系图.md
// - 用户认证与会话管理: 文档/05-游戏系统/07-用户认证与会话管理.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
//
using System.ComponentModel.DataAnnotations;

namespace ShadowverseBeyond.Server.Domain.Entities;

/// <summary>
/// 游戏会话实体 - 记录用户的游戏会话信息
/// </summary>
public class GameSession
{
    /// <summary>
    /// 游戏会话唯一标识符
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 关联的用户ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// 游戏状态ID (关联到GameState)
    /// </summary>
    [Required]
    [StringLength(100)]
    public string GameStateId { get; set; } = string.Empty;

    /// <summary>
    /// 游戏开始时间
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 游戏结束时间 (可选)
    /// </summary>
    public DateTime? EndedAt { get; set; }

    /// <summary>
    /// 游戏结果
    /// </summary>
    public GameResult? Result { get; set; }

    /// <summary>
    /// 游戏时长 (以秒为单位)
    /// </summary>
    public long Duration { get; set; } = 0;

    /// <summary>
    /// 是否为活跃会话
    /// </summary>
    public bool IsActive { get; set; } = true;

    // 导航属性
    /// <summary>
    /// 关联的用户
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// 结束游戏会话
    /// </summary>
    /// <param name="result">游戏结果</param>
    public void EndSession(GameResult result)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("游戏会话已经结束");
        }

        EndedAt = DateTime.UtcNow;
        Result = result;
        IsActive = false;

        // 计算游戏时长
        Duration = (long)(EndedAt.Value - StartedAt).TotalSeconds;
    }

    /// <summary>
    /// 检查游戏是否正在进行
    /// </summary>
    /// <returns>是否正在进行</returns>
    public bool IsInProgress()
    {
        return IsActive && EndedAt == null;
    }

    /// <summary>
    /// 获取当前游戏时长
    /// </summary>
    /// <returns>当前游戏时长（秒）</returns>
    public long GetCurrentDuration()
    {
        if (EndedAt.HasValue)
        {
            return Duration;
        }
        else
        {
            return (long)(DateTime.UtcNow - StartedAt).TotalSeconds;
        }
    }

    /// <summary>
    /// 获取游戏时长的友好显示
    /// </summary>
    /// <returns>时长显示字符串</returns>
    public string GetDurationDisplay()
    {
        var totalSeconds = GetCurrentDuration();
        var timeSpan = TimeSpan.FromSeconds(totalSeconds);

        if (timeSpan.TotalHours >= 1)
        {
            return $"{timeSpan.Hours}小时{timeSpan.Minutes}分钟";
        }
        else if (timeSpan.TotalMinutes >= 1)
        {
            return $"{timeSpan.Minutes}分钟{timeSpan.Seconds}秒";
        }
        else
        {
            return $"{timeSpan.Seconds}秒";
        }
    }

    /// <summary>
    /// 获取游戏结果描述
    /// </summary>
    /// <returns>结果描述</returns>
    public string GetResultDescription()
    {
        if (!Result.HasValue)
        {
            return IsActive ? "进行中" : "未知";
        }

        return Result.Value switch
        {
            GameResult.Win => "胜利",
            GameResult.Loss => "失败",
            GameResult.Draw => "平局",
            GameResult.Surrender => "投降",
            GameResult.Disconnect => "断线",
            _ => "未知"
        };
    }

    /// <summary>
    /// 检查是否为胜利
    /// </summary>
    /// <returns>是否胜利</returns>
    public bool IsWin()
    {
        return Result == GameResult.Win;
    }

    /// <summary>
    /// 检查是否为失败
    /// </summary>
    /// <returns>是否失败</returns>
    public bool IsLoss()
    {
        return Result == GameResult.Loss || Result == GameResult.Surrender;
    }
}

/// <summary>
/// 游戏结果枚举
/// </summary>
public enum GameResult
{
    /// <summary>
    /// 胜利
    /// </summary>
    Win = 1,

    /// <summary>
    /// 失败
    /// </summary>
    Loss = 2,

    /// <summary>
    /// 平局
    /// </summary>
    Draw = 3,

    /// <summary>
    /// 投降
    /// </summary>
    Surrender = 4,

    /// <summary>
    /// 断线
    /// </summary>
    Disconnect = 5
}
