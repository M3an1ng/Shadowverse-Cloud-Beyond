using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Domain.Constants; // Add missing using for CardKeyword
using ShadowverseBeyond.Server.Domain.Extensions; // 【BUG-017修复】添加扩展方法的using指令
// Server/Services/AiService.cs
//
// 相关文档:
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
// - AI决策系统: 文档/05-游戏系统/05-AI决策系统.md
// - 回合管理系统: 文档/05-游戏系统/03-回合管理系统.md
// - 战斗系统: 文档/05-游戏系统/02-战斗系统.md
//
using Microsoft.Extensions.DependencyInjection; // For IServiceScopeFactory
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Models; // For GameActionContext
using ShadowverseBeyond.Server.Dtos; // For GameStateDto
using ShadowverseBeyond.Server.Dtos.Enums; // Found correct namespace via search
using ShadowverseBeyond.Server.Dtos.SignalR.Actions; // For Action DTOs
using System; // For Exception, ArgumentNullException
using System.Collections.Generic;
using System.Linq; // For Linq methods
using System.Text.Json; // For deep copy using JSON serialization
using System.Threading;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;

namespace ShadowverseBeyond.Server.Services
{
    /// <summary>
    /// Implements the AI decision-making logic.
    /// </summary>
    public class AiService : IAiService
    {
        private readonly ILogger<AiService> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory; // To resolve IGameService if needed, avoiding circular dependency

        public AiService(ILogger<AiService> logger, IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        }

        /// <inheritdoc />
        public Task<List<string>> DecideMulliganAsync(GameStateDto gameState, Guid aiPlayerId)
        {
            _logger.LogDebug("[AiService:{GameId}] DecideMulliganAsync called for AI {PlayerId}.", gameState?.GameId ?? "Unknown", aiPlayerId);
            var gameId = gameState?.GameId ?? "Unknown"; // Get gameId for logging
            List<string> cardsToReplace = new List<string>();
            var aiState = GetPlayerStates(gameState, aiPlayerId)?.AiState;

            if (aiState == null)
            {
                _logger.LogError("[AiService:{GameId}] Could not find AI player state for {PlayerId} in DecideMulliganAsync.", gameId, aiPlayerId);
                return Task.FromResult(cardsToReplace); // Return empty list on error
            }

            // Simple V1 Logic: Replace any card with cost > 3
            foreach (var card in aiState.Hand)
            {
                if (card.Cost > 3)
                {
                    cardsToReplace.Add(card.InstanceId.ToString()); // Fix: Use InstanceId instead of Uid
                }
            }

            _logger.LogInformation("[AiService:{GameId}] AI {PlayerId} mulligan decision: Replacing {Count} cards (Cost > 3). UIDs: [{Uids}]",
                gameId, aiPlayerId, cardsToReplace.Count, string.Join(", ", cardsToReplace));

            return Task.FromResult(cardsToReplace);
        }

        /// <inheritdoc />
        public async Task<PlayCardActionDto?> DecidePlayCardAsync(GameStateDto gameState, Guid aiPlayerId)
        {
            _logger.LogDebug("[AiService:{GameId}] DecidePlayCardAsync called for AI {PlayerId}.", gameState?.GameId ?? "Unknown", aiPlayerId);
            var gameId = gameState?.GameId ?? "Unknown"; // Get gameId for logging
            var states = GetPlayerStates(gameState, aiPlayerId);
            if (states == null)
            {
                _logger.LogError("[AiService:{GameId}] Could not get player states for AI {PlayerId}.", gameId, aiPlayerId);
                return null;
            }
            var (aiState, opponentState) = states.Value;

            var playableCards = await GetPlayableCardsAsync(gameId, aiPlayerId, aiState);
            if (!playableCards.Any())
            {
                _logger.LogDebug("[AiService:{GameId}] No playable cards found for AI {PlayerId}.", gameId, aiPlayerId);
                return null;
            }

            // V1 Logic: Prioritize playing the highest-cost follower if possible
            var playableFollowers = playableCards
                .Where(c => c.Type == CardType.Follower)
                .OrderByDescending(c => c.Cost)
                .ToList();

            var aiFieldCount = aiState.Field.Count(c => c != null && c.Type != CardType.Leader);
            if (aiFieldCount < GameConstants.Field.MaxFieldSize && playableFollowers.Any())
            {
                var cardToPlay = playableFollowers.First();
                _logger.LogInformation("[AiService:{GameId}] AI {PlayerId} decided to play Follower: {CardName} ({CardInstanceId}), Cost: {Cost}.", // Changed Uid to InstanceId in log message
                    gameId, aiPlayerId, cardToPlay.Name, cardToPlay.InstanceId, cardToPlay.Cost); // Changed Uid to InstanceId
                // Followers generally don't require a target when played, unless they have a specific Fanfare.
                // Simple V1 plays without target.
                return new PlayCardActionDto { CardUid = cardToPlay.InstanceId, TargetUids = null };
            }

            // If no follower can be played, try playing the highest-cost spell
            var playableSpells = playableCards
                 .Where(c => c.Type == CardType.Spell)
                 .OrderByDescending(c => c.Cost)
                 .ToList();

            if (playableSpells.Any())
            {
                var cardToPlay = playableSpells.First();
                // Simple V1 Targeting: Target highest attack opponent unit, else opponent leader
                Guid? targetUid = null;
                // Use Select projection to handle nullable before OrderByDescending
                var opponentUnits = opponentState?.Field
                                      // Removed redundant null check on non-nullable int CurrentAttack
                                      .Select(u => new { Unit = u, SortAttack = u.CurrentAttack })
                                      .OrderByDescending(x => x.SortAttack)
                                      .Select(x => x.Unit)
                                      .ToList();
                if (opponentUnits != null && opponentUnits.Any())
                {
                    targetUid = opponentUnits.First().InstanceId; // Fix: Use InstanceId
                    _logger.LogDebug("[AiService:{GameId}] Spell target selected: Opponent Unit {TargetUid}", gameId, targetUid);
                }
                else if (opponentState != null) // Target opponent leader if no units
                {
                    targetUid = opponentState.PlayerId; // Assuming PlayerId can represent the leader target
                    _logger.LogDebug("[AiService:{GameId}] Spell target selected: Opponent Leader {TargetUid}", gameId, targetUid);
                }
                else
                {
                    _logger.LogWarning("[AiService:{GameId}] Cannot determine target for spell {CardName} ({CardInstanceId}). Opponent state is null.", gameId, cardToPlay.Name, cardToPlay.InstanceId); // Changed Uid to InstanceId
                }


                _logger.LogInformation("[AiService:{GameId}] AI {PlayerId} decided to play Spell: {CardName} ({CardInstanceId}), Cost: {Cost}, Target: {TargetUid}.", // Changed Uid to InstanceId in log message
                    gameId, aiPlayerId, cardToPlay.Name, cardToPlay.InstanceId, cardToPlay.Cost, targetUid.ToString() ?? "None"); // Changed Uid to InstanceId

                return new PlayCardActionDto { CardUid = cardToPlay.InstanceId, TargetUids = targetUid.HasValue ? new List<Guid> { targetUid.Value } : null };
            }

            // If neither follower nor spell can be played (e.g., amulets only, field full, no PP), play nothing.
            _logger.LogDebug("[AiService:{GameId}] AI {PlayerId} decided not to play any card this turn.", gameId, aiPlayerId);
            return null;
        }

        /// <inheritdoc />
        public Task<bool> DecidePpBoostAsync(GameStateDto gameState, Guid aiPlayerId)
        {
            _logger.LogDebug("[AiService:{GameId}] DecidePpBoostAsync called for AI {PlayerId}.", gameState?.GameId ?? "Unknown", aiPlayerId);
            var gameId = gameState?.GameId ?? "Unknown";
            var states = GetPlayerStates(gameState, aiPlayerId);
            if (states == null)
            {
                _logger.LogError("[AiService:{GameId}] Could not get player states for AI {PlayerId} in DecidePpBoostAsync.", gameId, aiPlayerId);
                return Task.FromResult(false);
            }

            var aiState = states.Value.AiState;
            if (gameState.CurrentPlayerId != aiPlayerId)
            {
                return Task.FromResult(false);
            }

            if (aiState.PpBoostUsesAvailable <= 0)
            {
                return Task.FromResult(false);
            }

            var allowedMax = aiState.MaxPP + GameConstants.PlayPoints.PpBoostOverflowAmount;
            if (aiState.CurrentPP >= allowedMax)
            {
                return Task.FromResult(false);
            }

            var canEnablePlay = aiState.Hand.Any(card =>
                card != null &&
                card.Cost > aiState.CurrentPP &&
                card.Cost <= aiState.CurrentPP + GameConstants.PlayPoints.PpBoostAmount);

            if (!canEnablePlay)
            {
                return Task.FromResult(false);
            }

            _logger.LogInformation("[AiService:{GameId}] AI {PlayerId} decided to use PP+1.", gameId, aiPlayerId);
            return Task.FromResult(true);
        }

        /// <inheritdoc />
        public Task<EvolveActionDto?> DecideEvolveAsync(GameStateDto gameState, Guid aiPlayerId)
        {
            _logger.LogDebug("[AiService:{GameId}] DecideEvolveAsync called for AI {PlayerId}.", gameState?.GameId ?? "Unknown", aiPlayerId);
            var gameId = gameState?.GameId ?? "Unknown";
            var states = GetPlayerStates(gameState, aiPlayerId);
            if (states == null)
            {
                _logger.LogError("[AiService:{GameId}] Could not get player states for AI {PlayerId} in DecideEvolveAsync.", gameId, aiPlayerId);
                return Task.FromResult<EvolveActionDto?>(null);
            }
            var (aiState, _) = states.Value;

            // 1. Check if AI has evolution right this turn.
            if (!aiState.CanEvolve)
            {
                return Task.FromResult<EvolveActionDto?>(null);
            }

            // 2. Find all followers on the field that can be evolved based on their definition
            var evolvableUnits = new List<CardInstanceDto>();
            foreach (var unit in aiState.Field)
            {
                if (unit != null && unit.Type == CardType.Follower && !unit.IsEvolved)
                {
                    var hasBanEvolve = unit.Keywords?.Any(k => k == CardKeyword.BanEvolve) ?? false;
                    var hasFreeEvolve = unit.Keywords?.Any(k => k == CardKeyword.FreeEvolve) ?? false;
                    var hasEvolutionResource = aiState.CurrentEP > 0 || hasFreeEvolve;
                    if (unit.CanEvolve && !hasBanEvolve && hasEvolutionResource)
                    {
                        evolvableUnits.Add(unit);
                    }
                }
            }

            if (!evolvableUnits.Any())
            {
                _logger.LogDebug("[AiService:{GameId}] AI {PlayerId} has no evolvable units on the field.", gameId, aiPlayerId);
                return Task.FromResult<EvolveActionDto?>(null);
            }

            // 3. Simple V2 Logic: Evolve the highest attack evolvable unit
            var unitToEvolve = evolvableUnits.OrderByDescending(u => u.CurrentAttack).First();

            _logger.LogInformation("[AiService:{GameId}] AI {PlayerId} decided to evolve Unit: {UnitName} ({UnitUid}), Attack: {Attack}.",
                gameId, aiPlayerId, unitToEvolve.Name, unitToEvolve.InstanceId, unitToEvolve.CurrentAttack);

            var action = new EvolveActionDto { UnitUid = unitToEvolve.InstanceId.ToString() };
            return Task.FromResult<EvolveActionDto?>(action);
        }

        /// <inheritdoc />
        public Task<AttackActionDto?> DecideNextAttackAsync(GameStateDto gameState, Guid aiPlayerId)
        {
            _logger.LogDebug("[AiService:{GameId}] DecideNextAttackAsync called for AI {PlayerId}.", gameState?.GameId ?? "Unknown", aiPlayerId);
            var gameId = gameState?.GameId ?? "Unknown";
            var states = GetPlayerStates(gameState, aiPlayerId);
            if (states == null)
            {
                _logger.LogError("[AiService:{GameId}] Could not get player states for AI {PlayerId} in DecideNextAttackAsync.", gameId, aiPlayerId);
                return Task.FromResult<AttackActionDto?>(null);
            }
            var (aiState, opponentState) = states.Value;

            var readyUnit = aiState.Field
                .Where(u => u != null && u.CanPerformAttackDto(aiState.PlayerId, gameState.CurrentPlayerId == aiState.PlayerId))
                .Select(u => new { Unit = u, SortAttack = u.CurrentAttack })
                .OrderByDescending(x => x.SortAttack)
                .Select(x => x.Unit)
                .FirstOrDefault();

            if (readyUnit == null)
            {
                _logger.LogDebug("[AiService:{GameId}] AI {PlayerId} has no more units ready to attack.", gameId, aiPlayerId);
                return Task.FromResult<AttackActionDto?>(null);
            }

            Guid? targetUid = SelectAttackTarget(readyUnit, opponentState);

            if (targetUid != null)
            {
                var attackAction = new AttackActionDto { AttackerUid = readyUnit.InstanceId, TargetUid = targetUid.Value };
                _logger.LogInformation("[AiService:{GameId}] AI {PlayerId} decided next attack: Unit {AttackerUid} -> Target {TargetUid}.",
                    gameId, aiPlayerId, readyUnit.InstanceId, targetUid);
                return Task.FromResult<AttackActionDto?>(attackAction);
            }
            else
            {
                _logger.LogDebug("[AiService:{GameId}] No valid target found for AI attacker {AttackerUid}.", gameId, readyUnit.InstanceId);
                return Task.FromResult<AttackActionDto?>(null);
            }
        }

        #region Helper Methods

        /// <summary>
        /// Creates a deep copy of an object using JSON serialization.
        /// Returns default if serialization/deserialization fails.
        /// </summary>
        private T? DeepCopy<T>(T obj)
        {
            if (obj == null)
            {
                return default;
            }
            try
            {
                var json = JsonSerializer.Serialize(obj);
                return JsonSerializer.Deserialize<T>(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during deep copy of object type {ObjectType}", typeof(T).Name);
                return default; // Return default on error
            }
        }

        /// <summary>
        /// Gets the AI's state and the opponent's state from the game state.
        /// Returns null if states cannot be determined.
        /// </summary>
        private (PlayerStateDto AiState, PlayerStateDto? OpponentState)? GetPlayerStates(GameStateDto? gameState, Guid aiPlayerId)
        {
            if (gameState == null)
            {
                _logger.LogError("[AiService:Helper] GameStateDto is null in GetPlayerStates.");
                return null;
            }
            PlayerStateDto? aiState = gameState.Players.FirstOrDefault(p => p.PlayerId == aiPlayerId);
            PlayerStateDto? opponentState = gameState.Players.FirstOrDefault(p => p.PlayerId != aiPlayerId);

            if (aiState == null)
            {
                _logger.LogError("[AiService:{GameId}] AI player state not found for {PlayerId}.", gameState.GameId, aiPlayerId);
                return null;
            }
            if (opponentState == null)
            {
                // This might happen in some game modes or if called too early/late. Log warning but might be acceptable depending on caller.
                _logger.LogWarning("[AiService:{GameId}] Opponent player state not found for AI {PlayerId}.", gameState.GameId, aiPlayerId);
            }

            return (aiState, opponentState);
        }

        /// <summary>
        /// Gets cards from the AI's hand that can currently be played based on Play Points.
        /// </summary>
        private async Task<List<CardInstanceDto>> GetPlayableCardsAsync(string gameId, Guid aiPlayerId, PlayerStateDto aiState)
        {
            // Ensure aiState and Hand are not null before accessing
            if (aiState?.Hand == null)
            {
                _logger.LogWarning("[AiService:Helper] AI state or hand is null in GetPlayableCards.");
                return new List<CardInstanceDto>();
            }
            // Return playable CardInstanceDto directly
            var costPlayable = aiState.Hand
                .Where(card => card != null && card.Cost <= aiState.CurrentPP)
                .ToList();

            if (!costPlayable.Any())
            {
                return new List<CardInstanceDto>();
            }

            using var scope = _serviceScopeFactory.CreateScope();
            var gameService = scope.ServiceProvider.GetRequiredService<IGameService>();
            var playable = new List<CardInstanceDto>();

            foreach (var card in costPlayable)
            {
                try
                {
                    var result = await gameService.GetCardPlayabilityAsync(gameId, aiPlayerId, card.InstanceId, CancellationToken.None);
                    if (result.IsPlayable)
                    {
                        playable.Add(card);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[AiService:{GameId}] Failed to check playability for card {CardInstanceId}. Skipping.", gameId, card.InstanceId);
                }
            }

            return playable;
        }
        /// <summary>
        /// Selects the best target for an attacking unit based on V1 AI logic.
        /// Prioritizes: Ward > Highest Attack Follower > Opponent Leader.
        /// Returns the target's UID (Unit UID or Player ID for leader) or null if no valid target.
        /// </summary>
        private Guid? SelectAttackTarget(CardInstanceDto attacker, PlayerStateDto? opponentState) // Fix: Change parameter type to CardInstanceDto
        {
            if (opponentState == null)
            {
                _logger.LogWarning("[AiService:Helper] Opponent state is null in SelectAttackTarget for attacker {AttackerUid}. Cannot select target.", attacker?.InstanceId.ToString() ?? "Unknown"); // Fix: Use InstanceId
                return null; // Cannot attack if no opponent state
            }
            // Ensure opponent field is not null before accessing
            // Corrected default list type to match PlayerStateDto.Field
            var opponentField = opponentState.Field?
                .Where(u => u != null && u.Type == CardType.Follower && u.Zone == CardZone.Field)
                .ToList() ?? new List<CardInstanceDto>();
            var attackerCannotAttackFollowers = attacker.HasKeyword(CardKeyword.Shackle)
                                                || attacker.HasKeyword(CardKeyword.CannotAttackFollower);
            var attackerCannotAttackLeader = attacker.HasKeyword(CardKeyword.Shackle)
                                             || attacker.HasKeyword(CardKeyword.CannotAttackLeader);

            bool IsFollowerUnattackable(CardInstanceDto target)
            {
                if (attackerCannotAttackFollowers)
                {
                    return true;
                }

                return target.HasKeyword(CardKeyword.Ambush) || target.HasKeyword(CardKeyword.Pressure);
            }

            static bool HasEffectiveWard(CardInstanceDto target)
            {
                return target.HasKeyword(CardKeyword.Ward)
                       && !target.HasKeyword(CardKeyword.Ambush)
                       && !target.HasKeyword(CardKeyword.Pressure);
            }

            bool CanAttackLeader(CardInstanceDto attackerCard, CardInstanceDto? leaderCard, bool hasWardFollowers)
            {
                if (leaderCard == null)
                {
                    return false;
                }

                if (hasWardFollowers)
                {
                    return false;
                }

                if (attackerCannotAttackLeader)
                {
                    return false;
                }

                if (attackerCard.FieldTurn == 0 && !attackerCard.HasStorm())
                {
                    return false;
                }

                return true;
            }

            var hasWardFollowers = opponentField.Any(HasEffectiveWard);

            // 1. Prioritize units with Ward
            var wardTargets = opponentField
                .Where(u => u != null && HasEffectiveWard(u))
                .Where(u => !IsFollowerUnattackable(u))
                .Select(u => new { Unit = u, SortAttack = u.CurrentAttack })
                .OrderByDescending(x => x.SortAttack)
                .Select(x => x.Unit)
                .ToList();

            if (wardTargets.Any())
            {
                var target = wardTargets.First();
                _logger.LogDebug("[AiService:Helper] Attacker {AttackerUid} targeting Ward unit {TargetUid}", attacker?.InstanceId.ToString() ?? "Unknown", target.InstanceId); // Fix: Use InstanceId for both attacker and target
                return target.InstanceId; // Fix: Use InstanceId // Convert Guid to string for return type
            }
            else if (hasWardFollowers)
            {
                _logger.LogDebug("[AiService:Helper] Ward followers exist but none are attackable. Attacker {AttackerUid} cannot select a target.",
                    attacker?.InstanceId.ToString() ?? "Unknown");
                return null;
            }

            // 2. If no Ward, prioritize highest attack non-Ward follower
            var highestAttackFollower = opponentField
                 .Where(u => u != null && !IsFollowerUnattackable(u))
                                        // Removed redundant null check on non-nullable int CurrentAttack
                .Select(u => new { Unit = u, SortAttack = u.CurrentAttack }) // Project before ordering
                .OrderByDescending(x => x.SortAttack) // Order by projected value
                .Select(x => x.Unit) // Select original unit back
                .FirstOrDefault(); // Find the highest attack unit

            if (highestAttackFollower != null)
            {
                _logger.LogDebug("[AiService:Helper] Attacker {AttackerUid} targeting highest attack unit {TargetUid}", attacker?.InstanceId.ToString() ?? "Unknown", highestAttackFollower.InstanceId); // Fix: Use InstanceId for both attacker and target
                return highestAttackFollower.InstanceId; // Fix: Use InstanceId // Convert Guid to string for return type
            }

            // 3. If no valid follower targets, consider attacking the opponent leader.
            if (CanAttackLeader(attacker, opponentState.LeaderCard, hasWardFollowers))
            {
                if (opponentState.LeaderCard != null)
                {
                    _logger.LogDebug("[AiService:Helper] Attacker {AttackerUid} targeting opponent leader {TargetUid}", attacker?.InstanceId.ToString() ?? "Unknown", opponentState.LeaderCard.InstanceId);
                    return opponentState.LeaderCard.InstanceId;
                }

                // Fallback with a warning if LeaderCard is null, to prevent crash and highlight the mapping issue.
                _logger.LogWarning("[AiService:Helper] Opponent LeaderCard DTO is null. Cannot target leader. Attacker: {AttackerUid}", attacker?.InstanceId.ToString() ?? "Unknown");
                return null;
            }

            // Should not happen if opponent has a state, but defensively return null
            _logger.LogWarning("[AiService:Helper] No target found for attacker {AttackerUid}, opponent has state but no units or leader target applicable?", attacker?.InstanceId.ToString() ?? "Unknown"); // Fix: Use InstanceId
            return null;
        }


        #endregion Helper Methods

        /// <inheritdoc />
        public Task<bool> IsAiPlayer(Guid playerId)
        {
            // 简化的实现：在当前架构中，我们假设所有非空的GUID都可能是AI玩家
            // 实际的AI检查将在GameService中通过PlayerState.IsAI属性进行
            // 这里我们返回true让GameService进行进一步的验证
            bool isAi = playerId != Guid.Empty;

            _logger.LogDebug("IsAiPlayer check for '{PlayerId}': Result={IsAi} (simplified check)", playerId, isAi);
            return Task.FromResult(isAi);
        }

        /// <inheritdoc />
        public async Task RequestAiActionAsync(string gameId, Guid aiPlayerId)
        {
            _logger.LogInformation("[AiService:{GameId}] Received request to execute AI action for player {PlayerId}.", gameId, aiPlayerId);

            try
            {
                // 创建作用域以解析 Scoped 依赖项，如 AiTurnExecutor
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var aiTurnExecutor = scope.ServiceProvider.GetRequiredService<AiTurnExecutor>();
                    _logger.LogDebug("[AiService:{GameId}] Resolved AiTurnExecutor within scope for player {PlayerId}.", gameId, aiPlayerId);

                    // 异步执行 AI 的回合逻辑
                    await aiTurnExecutor.ExecuteTurnAsync(gameId, aiPlayerId);

                    _logger.LogInformation("[AiService:{GameId}] AiTurnExecutor completed action for player {PlayerId}.", gameId, aiPlayerId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AiService:{GameId}] Error occurred while executing AI action for player {PlayerId}.", gameId, aiPlayerId);
                // 根据需求，我们可能需要更优雅地处理此问题，
                // 例如，以错误状态结束 AI 回合，但目前仅记录日志。
            }
        }

        /// <inheritdoc />
        public Task<PlayerChoiceResult> DecideTargetChoiceAsync(PlayerChoiceRequest request, GameStateDto gameState)
        {
            var gameId = gameState.GameId;
            var aiPlayerId = request.RequestingPlayerId;
            _logger.LogInformation("[AiService:{GameId}] AI {PlayerId} is deciding for choice {ChoiceId}.", gameId, aiPlayerId, request.PendingChoiceId);

            // V1 Logic: For now, the AI will make a very simple choice.
            // If the minimum selection is 0, it means choosing is optional or not applicable (like Goblin's Fanfare).
            // In this case, the AI will choose nothing.
            // Otherwise, it will randomly pick the minimum required number of targets.
            var chosenIds = new List<Guid>();
            if (request.MinSelectionCount > 0 && request.SelectableTargetIds.Any())
            {
                // Simple random selection
                var random = new Random();
                chosenIds = request.SelectableTargetIds
                                   .OrderBy(x => random.Next())
                                   .Take(request.MinSelectionCount)
                                   .ToList();

                _logger.LogInformation("[AiService:{GameId}] AI {PlayerId} randomly selected {Count} targets: [{Targets}]",
                    gameId, aiPlayerId, chosenIds.Count, string.Join(", ", chosenIds));
            }
            else
            {
                _logger.LogInformation("[AiService:{GameId}] AI {PlayerId} is choosing no targets as MinSelectionCount is 0 or no targets are available.", gameId, aiPlayerId);
            }

            var result = new PlayerChoiceResult
            {
                WasCancelled = false,
                ChosenTargetIds = chosenIds
            };

            return Task.FromResult(result);
        }
    }
}
