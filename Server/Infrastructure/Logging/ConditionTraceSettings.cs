using System;
using System.Collections.Concurrent;
using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Infrastructure.Logging
{
    public interface IConditionTraceSettings
    {
        void SetConditionEnabled(Guid userId, bool enabled);
        void SetTargetEnabled(Guid userId, bool enabled);
        void SetEffectEnabled(Guid userId, bool enabled);
        bool IsConditionEnabled(Guid userId);
        bool IsTargetEnabled(Guid userId);
        bool IsEffectEnabled(Guid userId);
        bool IsConditionEnabledFor(GameContext context);
        bool IsTargetEnabledFor(GameContext context);
        bool IsEffectEnabledFor(GameContext context);
    }

    public sealed class ConditionTraceSettings : IConditionTraceSettings
    {
        private readonly ConcurrentDictionary<Guid, bool> _conditionEnabledUsers = new();
        private readonly ConcurrentDictionary<Guid, bool> _targetEnabledUsers = new();
        private readonly ConcurrentDictionary<Guid, bool> _effectEnabledUsers = new();

        public void SetConditionEnabled(Guid userId, bool enabled)
        {
            SetEnabledInternal(_conditionEnabledUsers, userId, enabled);
        }

        public void SetTargetEnabled(Guid userId, bool enabled) => SetEnabledInternal(_targetEnabledUsers, userId, enabled);

        public void SetEffectEnabled(Guid userId, bool enabled) => SetEnabledInternal(_effectEnabledUsers, userId, enabled);

        public bool IsConditionEnabled(Guid userId) => IsEnabledInternal(_conditionEnabledUsers, userId);

        public bool IsTargetEnabled(Guid userId) => IsEnabledInternal(_targetEnabledUsers, userId);

        public bool IsEffectEnabled(Guid userId) => IsEnabledInternal(_effectEnabledUsers, userId);

        public bool IsConditionEnabledFor(GameContext context) => IsEnabledForContext(_conditionEnabledUsers, context);

        public bool IsTargetEnabledFor(GameContext context) => IsEnabledForContext(_targetEnabledUsers, context);

        public bool IsEffectEnabledFor(GameContext context) => IsEnabledForContext(_effectEnabledUsers, context);

        private static void SetEnabledInternal(ConcurrentDictionary<Guid, bool> store, Guid userId, bool enabled)
        {
            if (userId == Guid.Empty)
            {
                return;
            }

            if (enabled)
            {
                store[userId] = true;
            }
            else
            {
                store.TryRemove(userId, out _);
            }
        }

        private static bool IsEnabledInternal(ConcurrentDictionary<Guid, bool> store, Guid userId)
        {
            return userId != Guid.Empty && store.TryGetValue(userId, out var enabled) && enabled;
        }

        private static bool IsEnabledForContext(ConcurrentDictionary<Guid, bool> store, GameContext context)
        {
            if (context == null || context.CurrentGameState == null)
            {
                return false;
            }

            var player1 = context.CurrentGameState.Player1?.PlayerId ?? Guid.Empty;
            var player2 = context.CurrentGameState.Player2?.PlayerId ?? Guid.Empty;
            return IsEnabledInternal(store, player1) || IsEnabledInternal(store, player2);
        }
    }
}
