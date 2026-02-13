//
// 相关文档:
// - API集成: 文档/03-前端架构/03-API集成.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
// - 错误处理与日志系统: 文档/05-游戏系统/08-错误处理与日志系统.md
//
using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Hubs.ClientInterfaces;
using ShadowverseBeyond.Server.Dtos.SignalR;
using ShadowverseBeyond.Server.Hubs;
using ShadowverseBeyond.Server.Services.Abstractions;
using System;
using System.Collections.Generic; // Keep for GroupExcept potentially needing list later if API changes
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Services
{
    public class SignalRClientMethodMapper : IClientMethodMapper
    {
        private readonly IHubContext<GameHub, IGameClient> _hubContext;
        private readonly ILogger<SignalRClientMethodMapper> _logger;

        // Constructor without dictionary initialization
        public SignalRClientMethodMapper(IHubContext<GameHub, IGameClient> hubContext, ILogger<SignalRClientMethodMapper> logger)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logger.LogInformation("SignalRClientMethodMapper initialized (using switch expression for event mapping).");
        }

        // Private helper method containing the switch expression logic
        private async Task ExecuteClientActionAsync(IGameClient clientProxy, GameEventDto eventDto, string targetDescription)
        {
            if (eventDto == null)
            {
                _logger.LogWarning("[Mapper] ExecuteClientActionAsync called with null eventDto for target {TargetDescription}.", targetDescription);
                return; // Avoid null reference in switch
            }

            _logger.LogDebug("[Mapper] Executing action for {EventType} targeting {TargetDescription}.", eventDto.GetType().Name, targetDescription);
            try
            {
                // Use switch expression for mapping
                Task actionTask = eventDto switch
                {
                    // --- Specific Handlers ---
                    TurnStartedEventDto e => clientProxy.TurnStarted(e), // Added handler for TurnStarted event
                    PpUpdateEventDto e => clientProxy.PpUpdate(e),
                    HandUpdatedEventDto e => clientProxy.HandUpdated(e), // Restored for mulligan functionality
                    CardInstanceUpdatedEventDto e => clientProxy.CardInstanceUpdated(e),
                    CardMovedEventDto e => clientProxy.CardMoved(e),
                    CounterChangedEventDto e => clientProxy.CounterChanged(e),
                    DeckUpdateEventDto e => clientProxy.DeckUpdate(e), // Restored case
                    AttackDeclaredEventDto e => clientProxy.AttackDeclared(e),
                    DamageDealtEventDto e => clientProxy.DamageDealt(e),
                    HealAppliedEventDto e => clientProxy.HealApplied(e),
                    UnitDestroyedEventDto e => clientProxy.UnitDestroyed(e),
                    CardVanishedEventDto e => clientProxy.CardVanished(e),
                    CardReturnedDto e => clientProxy.CardReturned(e),
                    TuckEventDto e => clientProxy.Tuck(e),
                    GameOverEventDto e => clientProxy.GameOver(e),
                    TurnEndedEventDto e => clientProxy.TurnEnded(e),
                    ErrorOccurredEventDto e => clientProxy.ReceiveError(e.ErrorMessage),
                    // Removed: UnitPlayedEventDto e => clientProxy.UnitPlayed(e),
                    // Removed: AmuletPlayedEventDto e => clientProxy.AmuletPlayed(e),
                    // Removed: SpellPlayedEventDto e => clientProxy.SpellPlayed(e),
                    CardPlayedEventDto e => clientProxy.CardPlayed(e),             // Added
                    CardSummonedEventDto e => clientProxy.CardSummoned(e), // Added
                    UnitEvolvedEventDto e => clientProxy.UnitEvolved(e),
                    EpUpdateEventDto e => clientProxy.EpUpdate(e),
                    SepUpdateEventDto e => clientProxy.SepUpdate(e),
                    PpMaxUpdateEventDto e => clientProxy.PpMaxUpdate(e),
                    EpMaxUpdateEventDto e => clientProxy.EpMaxUpdate(e),
                    SepMaxUpdateEventDto e => clientProxy.SepMaxUpdate(e),
                    GraveyardUpdateEventDto e => clientProxy.GraveyardUpdate(e),
                    EmblemUpdatedEventDto e => clientProxy.EmblemUpdated(e),
                    HealthChangedEventDto e => clientProxy.HealthChanged(e),
                    StatChangeEventDto e => clientProxy.StatChange(e),
                    KeywordAddedEventDto e => clientProxy.KeywordAdded(e),
                    KeywordRemovedEventDto e => clientProxy.KeywordRemoved(e),
                    MulliganCompletedEventDto e => clientProxy.PlayerMulliganCompleted(e),
                    AttackStateUpdatedEventDto e => clientProxy.AttackStateUpdated(e), // 【BUG修复】添加缺失的事件映射
                    EvolveStateUpdatedEventDto e => clientProxy.EvolveStateUpdated(e),
                    SuperEvolveStateUpdatedEventDto e => clientProxy.SuperEvolveStateUpdated(e),
                    ActStateUpdatedEventDto e => clientProxy.ActStateUpdated(e),
                    PpBoostStateUpdatedEventDto e => clientProxy.PpBoostStateUpdated(e),
                    PlayerChoiceRequestedEventDto e => clientProxy.PlayerChoiceRequested(e), // 【TARGETING_FIX】添加缺失的目标选择事件映射
                    LogEventDto e => clientProxy.LogEvent(e),
                    CardRevealedEventDto e => clientProxy.CardRevealed(e),
                    FusionEventDto e => clientProxy.Fusion(e),

                    // --- Default Case for Unmapped Events (Fallback) ---
                    // Includes CardDrawnEventDto, PlayerAttackedEventDto, UnitAttackedEventDto implicitly
                    _ => clientProxy.ReceiveGameEvent(eventDto)
                };

                await actionTask;
                _logger.LogDebug("[Mapper] Successfully executed action for {EventType} targeting {TargetDescription}.", eventDto.GetType().Name, targetDescription);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Mapper] Error executing action for event {EventType} targeting {TargetDescription}.", eventDto.GetType().Name, targetDescription);
            }
        }

        // --- Implementation of IClientMethodMapper ---

        public async Task BroadcastEventToClientAsync(string connectionId, GameEventDto eventDto)
        {
            if (string.IsNullOrEmpty(connectionId)) // Null check for eventDto is in helper
            {
                _logger.LogWarning("[Mapper] Invalid arguments for BroadcastEventToClientAsync (ConnectionId: {ConnIdIsNullOrEmpty}).", string.IsNullOrEmpty(connectionId));
                return;
            }
            var clientProxy = _hubContext.Clients.Client(connectionId);
            await ExecuteClientActionAsync(clientProxy, eventDto, $"Client: {connectionId}");
        }

        public async Task BroadcastEventToGroupAsync(string groupName, GameEventDto eventDto)
        {
            if (string.IsNullOrEmpty(groupName)) // Null check for eventDto is in helper
            {
                _logger.LogWarning("[Mapper] Invalid arguments for BroadcastEventToGroupAsync (GroupName: {GroupIsNullOrEmpty}).", string.IsNullOrEmpty(groupName));
                return;
            }
            var groupProxy = _hubContext.Clients.Group(groupName);
            await ExecuteClientActionAsync(groupProxy, eventDto, $"Group: {groupName}");
        }

        public async Task BroadcastEventToGroupAllAsync(string groupName, GameEventDto eventDto)
        {
            if (string.IsNullOrEmpty(groupName)) // Null check for eventDto is in helper
            {
                _logger.LogWarning("[Mapper] Invalid arguments for BroadcastEventToGroupAllAsync (GroupName: {GroupIsNullOrEmpty}).", string.IsNullOrEmpty(groupName));
                return;
            }
            // Functionally same as BroadcastEventToGroupAsync in SignalR Core
            var groupProxy = _hubContext.Clients.Group(groupName);
            await ExecuteClientActionAsync(groupProxy, eventDto, $"Group (All): {groupName}");
        }

        public async Task BroadcastEventToGroupExceptAsync(string groupName, string connectionIdToExclude, GameEventDto eventDto)
        {
            if (string.IsNullOrEmpty(groupName) || string.IsNullOrEmpty(connectionIdToExclude)) // Null check for eventDto is in helper
            {
                _logger.LogWarning("[Mapper] Invalid arguments for BroadcastEventToGroupExceptAsync (GroupName: {GroupIsNullOrEmpty}, ExcludedId: {ExcludedIdIsNullOrEmpty}).", string.IsNullOrEmpty(groupName), string.IsNullOrEmpty(connectionIdToExclude));
                return;
            }
            var groupProxy = _hubContext.Clients.GroupExcept(groupName, connectionIdToExclude);
            await ExecuteClientActionAsync(groupProxy, eventDto, $"Group: {groupName} (Except: {connectionIdToExclude})");
        }
    }
}
