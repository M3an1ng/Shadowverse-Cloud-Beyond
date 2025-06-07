using System;

namespace ShadowverseBeyond.Server.Domain.Abilities.Targets
{
    /// <summary>
    /// 表示一个可被能力选定的目标的唯一标识符，即卡牌实例的 ID。
    /// 使用 struct 以提高性能并减少内存分配。
    /// 根据M11目标选择系统设计文档实现
    /// </summary>
    public readonly struct TargetId : IEquatable<TargetId>
    {
        /// <summary>
        /// 目标的具体 ID，对应 CardInstance.InstanceId。
        /// </summary>
        public string Id { get; }

        public TargetId(string instanceId)
        {
            Id = instanceId ?? string.Empty;
        }

        // 实现 IEquatable<TargetId> 和重写 Equals, GetHashCode
        public bool Equals(TargetId other) => Id == other.Id;
        public override bool Equals(object? obj) => obj is TargetId other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();

        public static bool operator ==(TargetId left, TargetId right) => left.Equals(right);
        public static bool operator !=(TargetId left, TargetId right) => !left.Equals(right);

        public override string ToString() => $"CardInstance:{Id}";

        // --- 静态工厂方法 ---
        /// <summary>
        /// 创建一个指向特定卡牌实例的目标 ID。
        /// </summary>
        public static TargetId Card(string instanceId)
        {
            return new TargetId(instanceId);
        }

        /// <summary>
        /// 创建一个指向特定玩家的目标 ID。
        /// </summary>
        /// <param name="playerId">玩家的 ID (string)。</param>
        public static TargetId Player(string playerId)
        {
            // Use a prefix to distinguish player IDs from card instance IDs
            // Ensure playerId is not null or empty? Or allow it? For now, allow.
            return new TargetId($"Player:{playerId ?? string.Empty}");
        }

        /// <summary>
        /// 空目标ID
        /// </summary>
        public static TargetId Empty => new TargetId(string.Empty);

        /// <summary>
        /// 检查是否为空目标ID
        /// </summary>
        public bool IsEmpty => string.IsNullOrEmpty(Id);
    }
}
