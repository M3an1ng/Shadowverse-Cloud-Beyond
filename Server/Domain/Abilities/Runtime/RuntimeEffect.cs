using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Domain.Abilities.Runtime
{
    /// <summary>
    /// 运行时效果包装类
    /// 包装EffectConfig并添加执行上下文和状态
    /// </summary>
    public class Effect
    {
        /// <summary>
        /// 静态效果配置
        /// </summary>
        public EffectConfig Config { get; }

        /// <summary>
        /// 效果执行次数
        /// </summary>
        public int ExecutionCount { get; private set; }

        /// <summary>
        /// 上次执行时间
        /// </summary>
        public DateTime? LastExecutionTime { get; private set; }

        /// <summary>
        /// 是否正在执行
        /// </summary>
        public bool IsExecuting { get; private set; }

        /// <summary>
        /// 执行上下文
        /// </summary>
        public EffectExecutionContext? CurrentContext { get; private set; }

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 执行结果历史
        /// </summary>
        public List<EffectExecutionResult> ExecutionHistory { get; } = new();

        public Effect(EffectConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// 检查是否可以执行
        /// </summary>
        /// <param name="context">执行上下文</param>
        /// <returns>是否可以执行</returns>
        public bool CanExecute(EffectExecutionContext context)
        {
            if (!IsEnabled || !Config.IsEnabled)
                return false;

            if (IsExecuting)
                return false;

            // TODO: 检查执行条件
            // foreach (var condition in Config.ExecutionConditions)
            // {
            //     if (!condition.Evaluate(context))
            //         return false;
            // }

            return true;
        }

        /// <summary>
        /// 开始执行
        /// </summary>
        /// <param name="context">执行上下文</param>
        public void BeginExecution(EffectExecutionContext context)
        {
            if (IsExecuting)
                throw new InvalidOperationException("Effect is already executing");

            IsExecuting = true;
            CurrentContext = context;
            LastExecutionTime = DateTime.UtcNow;
        }

        /// <summary>
        /// 结束执行
        /// </summary>
        /// <param name="result">执行结果</param>
        public void EndExecution(EffectExecutionResult result)
        {
            if (!IsExecuting)
                throw new InvalidOperationException("Effect is not executing");

            IsExecuting = false;
            CurrentContext = null;
            ExecutionCount++;

            ExecutionHistory.Add(result);

            // 保留最近100次执行历史
            if (ExecutionHistory.Count > 100)
            {
                ExecutionHistory.RemoveAt(0);
            }
        }

        /// <summary>
        /// 重置执行状态
        /// </summary>
        public void Reset()
        {
            ExecutionCount = 0;
            LastExecutionTime = null;
            IsExecuting = false;
            CurrentContext = null;
            ExecutionHistory.Clear();
        }
    }

    /// <summary>
    /// 效果执行上下文
    /// </summary>
    public class EffectExecutionContext
    {
        /// <summary>
        /// 源卡牌
        /// </summary>
        public CardInstance? SourceCard { get; set; }

        /// <summary>
        /// 触发事件
        /// </summary>
        public IGameEvent? TriggeringEvent { get; set; }

        /// <summary>
        /// 选中的目标
        /// </summary>
        public List<CardInstance> SelectedTargets { get; set; } = new();

        /// <summary>
        /// 动态变量
        /// </summary>
        public Dictionary<string, object> Variables { get; set; } = new();

        /// <summary>
        /// 游戏状态
        /// </summary>
        public GameState? GameState { get; set; }

        /// <summary>
        /// 执行时间戳
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// 效果执行结果
    /// </summary>
    public class EffectExecutionResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 生成的事件
        /// </summary>
        public List<IGameEvent> GeneratedEvents { get; set; } = new();

        /// <summary>
        /// 执行时间
        /// </summary>
        public DateTime ExecutionTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 执行持续时间
        /// </summary>
        public TimeSpan Duration { get; set; }
    }
}
