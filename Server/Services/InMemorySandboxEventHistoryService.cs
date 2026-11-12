using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.SignalR;
using ShadowverseBeyond.Server.Dtos.SignalR.Events;

namespace ShadowverseBeyond.Server.Services
{
    public sealed class InMemorySandboxEventHistoryService : IEventHistoryService
    {
        private readonly List<IGameEvent> _events = new();
        private readonly Dictionary<Guid, IGameEvent> _eventsById = new();
        private readonly List<JsonElement> _currentStepEventDtos = new();
        private readonly Dictionary<Guid, MiragePermanentGrowthEntry> _permanentGrowthByBuffId = new();
        private readonly List<Guid> _permanentGrowthOrder = new();

        public HashSet<Guid> InitialSandboxBuffIds { get; } = new();
        public bool IsCombatStepRecordingEnabled { get; set; }

        public Task StoreEventAsync(string gameId, IGameEvent gameEvent)
        {
            if (gameEvent != null)
            {
                _events.Add(gameEvent);
                _eventsById[gameEvent.EventId] = gameEvent;
            }
            return Task.CompletedTask;
        }

        public async Task OnSandboxEventCommittedAsync(
            GameContext gameContext,
            IGameEvent gameEvent,
            IEventProcessor eventProcessor)
        {
            if (gameContext?.CurrentGameState == null || gameEvent == null)
            {
                return;
            }

            if (!IsCombatStepRecordingEnabled)
            {
                if (gameEvent is BuffAppliedEvent initApplied && initApplied.AppliedBuff != null)
                {
                    InitialSandboxBuffIds.Add(initApplied.AppliedBuff.Id);
                }
                return;
            }

            // 1. Classify and capture permanent combat growth at the exact moment BuffAppliedEvent commits
            if (gameEvent is BuffAppliedEvent buffApplied && buffApplied.AppliedBuff != null)
            {
                var buff = buffApplied.AppliedBuff;
                var isAura = buff.IsAuraGenerated
                    || MirageCombatSnapshotBuilder.IsAuraGeneratedBuff(buff, gameContext.AbilityManager);
                if (!InitialSandboxBuffIds.Contains(buff.Id)
                    && !isAura
                    && buff.DurationType == DurationType.Permanent)
                {
                    var targetCard = buffApplied.BuffTarget ?? gameContext.CurrentGameState.FindCardInstance(buff.TargetCardInstanceId);
                    if (targetCard != null && targetCard.OriginInstanceId.HasValue)
                    {
                        var serializedBuff = JsonSerializer.SerializeToElement(buff, ReplayExecutionState.Json);
                        if (!_permanentGrowthByBuffId.ContainsKey(buff.Id))
                        {
                            _permanentGrowthOrder.Add(buff.Id);
                        }
                        _permanentGrowthByBuffId[buff.Id] = new MiragePermanentGrowthEntry
                        {
                            PlayerId = targetCard.OwnerPlayerId,
                            OriginInstanceId = targetCard.OriginInstanceId.Value,
                            SerializedBuff = serializedBuff
                        };
                    }
                }
            }
            else if (gameEvent is BuffRemovedEvent buffRemoved
                     && !string.Equals(buffRemoved.RemovalReason, "removed_all", StringComparison.Ordinal))
            {
                foreach (var removed in buffRemoved.RemovedBuffs)
                {
                    if (removed != null)
                    {
                        _permanentGrowthByBuffId.Remove(removed.Id);
                    }
                }
            }

            // 2. Materialize GameEventDto immediately after this event's Commit, before subsequent events mutate/destroy intermediate state
            gameContext.BuffService.RefreshDerivedStateCache(gameContext, "sandbox_event_dto_capture");
            var announcement = (gameEvent as ILogAnnounceableEvent)?.GetAnnouncement(gameContext.CurrentGameState);
            if (!string.IsNullOrWhiteSpace(announcement))
            {
                announcement = AnnouncementTokens.WithTurn(gameContext.CurrentGameState, announcement);
            }

            var processorResult = await eventProcessor.ProcessEventAsync(gameEvent, gameContext);
            var errorDto = processorResult.ResultingDtos.OfType<ErrorOccurredEventDto>().FirstOrDefault();
            if (errorDto != null)
            {
                throw new InvalidOperationException(
                    $"Sandbox event DTO conversion failed for {gameEvent.EventType} ({gameEvent.EventId}): {errorDto.ErrorMessage}");
            }
            var dtos = processorResult.ResultingDtos.ToList();
            if (gameEvent is AttackEvent attackEvent
                && attackEvent.Attacker != null
                && attackEvent.TargetCard != null
                && !dtos.OfType<AttackDeclaredEventDto>().Any())
            {
                dtos.Insert(0, new AttackDeclaredEventDto(
                    attackEvent.Attacker.OwnerPlayerId,
                    attackEvent.Attacker.InstanceId.ToString(),
                    attackEvent.TargetCard.InstanceId.ToString()));
            }

            if (dtos.Count == 0 && !string.IsNullOrWhiteSpace(announcement))
            {
                var sourcePlayerId = gameEvent.Source?.Player?.PlayerId ?? Guid.Empty;
                dtos.Add(new LogEventDto(gameEvent.EventId, gameEvent.EventType.ToString(), announcement, sourcePlayerId));
            }
            else
            {
                foreach (var dto in dtos)
                {
                    if (!string.IsNullOrWhiteSpace(announcement))
                    {
                        dto.Announcement ??= announcement;
                    }
                }
            }

            foreach (var dto in dtos)
            {
                _currentStepEventDtos.Add(JsonSerializer.SerializeToElement(dto, dto.GetType(), MatchCommandObservation.Json));
            }
        }

        public List<JsonElement> DrainCurrentStepEventDtos()
        {
            var list = _currentStepEventDtos.ToList();
            _currentStepEventDtos.Clear();
            return list;
        }

        public List<MiragePermanentGrowthEntry> GetOrderedPermanentGrowthLog()
        {
            return _permanentGrowthOrder
                .Where(id => _permanentGrowthByBuffId.ContainsKey(id))
                .Select(id => _permanentGrowthByBuffId[id])
                .ToList();
        }

        public IReadOnlyList<IGameEvent> GetEvents(string gameId)
        {
            return _events.ToList();
        }

        public Task<IGameEvent?> GetEventByIdAsync(string gameId, Guid eventId)
        {
            _eventsById.TryGetValue(eventId, out var found);
            return Task.FromResult(found);
        }

        public Task ClearHistoryForGameAsync(string gameId)
        {
            _events.Clear();
            _eventsById.Clear();
            return Task.CompletedTask;
        }
    }
}
