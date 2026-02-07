using System;
using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Metadata;
using ShadowverseBeyond.Server.Domain.Events.Services;

namespace ShadowverseBeyond.Server.Domain.Events
{
    [DslExposable("事件历史", Description = "用于查询当前对局的事件历史记录")]
    public sealed class EventHistoryReader
    {
        private readonly IEventHistoryService _eventHistoryService;
        private readonly string _gameId;
        private IReadOnlyList<IGameEvent>? _cachedEvents;

        public EventHistoryReader(IEventHistoryService eventHistoryService, string gameId)
        {
            _eventHistoryService = eventHistoryService ?? throw new ArgumentNullException(nameof(eventHistoryService));
            _gameId = gameId ?? string.Empty;
        }

        [DslExposable("事件列表", Description = "当前对局已记录的事件列表")]
        public IEnumerable<IGameEvent> Events
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_gameId))
                {
                    return Array.Empty<IGameEvent>();
                }

                _cachedEvents ??= _eventHistoryService.GetEvents(_gameId) ?? Array.Empty<IGameEvent>();
                return _cachedEvents;
            }
        }
    }
}
