// Server/Services/AiService.cs
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
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Services
{
    /// <summary>
    /// Implements the AI decision-making logic.
    /// </summary>
    public class AiService : IAiService
    {
        private readonly ILogger<AiService> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory; // To resolve IGameService if needed, avoiding circular dependency

        // Define constants (like MAX_FIELD_SIZE from ai.js)
        private const int MaxFieldSize = 5; // Should match game rules

        public AiService(ILogger<AiService> logger, IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        }

        /// <inheritdoc />
        public Task<List<string>> DecideMulliganAsync(GameStateDto gameState, string aiPlayerId)
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
        public Task<PlayCardActionDto?> DecidePlayCardAsync(GameStateDto gameState, string aiPlayerId)
        {
            _logger.LogDebug("[AiService:{GameId}] DecidePlayCardAsync called for AI {PlayerId}.", gameState?.GameId ?? "Unknown", aiPlayerId);
            var gameId = gameState?.GameId ?? "Unknown"; // Get gameId for logging
            var states = GetPlayerStates(gameState, aiPlayerId);
            if (states == null)
            {
                _logger.LogError("[AiService:{GameId}] Could not get player states for AI {PlayerId}.", gameId, aiPlayerId);
                return Task.FromResult<PlayCardActionDto?>(null);
            }
            var (aiState, opponentState) = states.Value;

            var playableCards = GetPlayableCards(aiState);
            if (!playableCards.Any())
            {
                _logger.LogDebug("[AiService:{GameId}] No playable cards found for AI {PlayerId}.", gameId, aiPlayerId);
                return Task.FromResult<PlayCardActionDto?>(null);
            }

            // V1 Logic: Prioritize playing the highest-cost follower if possible
            var playableFollowers = playableCards
                .Where(c => Enum.TryParse<CardType>(c.Type, true, out var type) && type == CardType.Follower)
                .OrderByDescending(c => c.Cost)
                .ToList();

            if (aiState.Field.Count < MaxFieldSize && playableFollowers.Any())
            {
                var cardToPlay = playableFollowers.First();
                _logger.LogInformation("[AiService:{GameId}] AI {PlayerId} decided to play Follower: {CardName} ({CardInstanceId}), Cost: {Cost}.", // Changed Uid to InstanceId in log message
                    gameId, aiPlayerId, cardToPlay.Name, cardToPlay.InstanceId, cardToPlay.Cost); // Changed Uid to InstanceId
                // Followers generally don't require a target when played, unless they have a specific Fanfare.
                // Simple V1 plays without target.
                return Task.FromResult<PlayCardActionDto?>(new PlayCardActionDto { CardUid = cardToPlay.InstanceId.ToString(), TargetUid = null }); // Changed Uid to InstanceId
            }

            // If no follower can be played, try playing the highest-cost spell
            var playableSpells = playableCards
                 .Where(c => Enum.TryParse<CardType>(c.Type, true, out var type) && type == CardType.Spell)
                 .OrderByDescending(c => c.Cost)
                 .ToList();

            if (playableSpells.Any())
            {
                var cardToPlay = playableSpells.First();
                // Simple V1 Targeting: Target highest attack opponent unit, else opponent leader
                string? targetUid = null;
                // Use Select projection to handle nullable before OrderByDescending
                var opponentUnits = opponentState?.Field
                                      // Removed redundant null check on non-nullable int CurrentAttack
                                      .Select(u => new { Unit = u, SortAttack = u.CurrentAttack })
                                      .OrderByDescending(x => x.SortAttack)
                                      .Select(x => x.Unit)
                                      .ToList();
                if (opponentUnits != null && opponentUnits.Any())
                {
                    targetUid = opponentUnits.First().InstanceId.ToString(); // Fix: Use InstanceId
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
                    gameId, aiPlayerId, cardToPlay.Name, cardToPlay.InstanceId, cardToPlay.Cost, targetUid ?? "None"); // Changed Uid to InstanceId

                return Task.FromResult<PlayCardActionDto?>(new PlayCardActionDto { CardUid = cardToPlay.InstanceId.ToString(), TargetUid = targetUid }); // Changed Uid to InstanceId
            }

            // If neither follower nor spell can be played (e.g., amulets only, field full, no PP), play nothing.
            _logger.LogDebug("[AiService:{GameId}] AI {PlayerId} decided not to play any card this turn.", gameId, aiPlayerId);
            return Task.FromResult<PlayCardActionDto?>(null);
        }

        /// <inheritdoc />
        public Task<EvolveActionDto?> DecideEvolveAsync(GameStateDto gameState, string aiPlayerId)
        {
            _logger.LogDebug("[AiService:{GameId}] DecideEvolveAsync called for AI {PlayerId}.", gameState?.GameId ?? "Unknown", aiPlayerId);
            var gameId = gameState?.GameId ?? "Unknown";
            var states = GetPlayerStates(gameState, aiPlayerId);
            if (states == null)
            {
                _logger.LogError("[AiService:{GameId}] Could not get player states for AI {PlayerId} in DecideEvolveAsync.", gameId, aiPlayerId);
                return Task.FromResult<EvolveActionDto?>(null);
            }
            var (aiState, _) = states.Value; // Don't need opponent state here

            // Check if AI has evolution points
            // Check if AI has evolution points (Corrected property name)
            if (aiState.CurrentEP <= 0)
            {
                _logger.LogDebug("[AiService:{GameId}] AI {PlayerId} has no evolution points.", gameId, aiPlayerId);
                return Task.FromResult<EvolveActionDto?>(null);
            }

            // Find evolvable units on the field that haven't evolved yet
            var evolvableUnits = aiState.Field
                // Corrected Evolve Check: Player must be able to evolve this turn AND unit must not be evolved yet.
                .Where(u => aiState.CanEvolve && !u.IsEvolved)
                // Use Select projection to handle nullable before OrderByDescending
                // Removed redundant null check on non-nullable int CurrentAttack
                .Select(u => new { Unit = u, SortAttack = u.CurrentAttack })
                .OrderByDescending(x => x.SortAttack)
                .Select(x => x.Unit) // Prioritize highest attack
                .ToList();

            if (!evolvableUnits.Any())
            {
                _logger.LogDebug("[AiService:{GameId}] AI {PlayerId} has no evolvable units on the field.", gameId, aiPlayerId);
                return Task.FromResult<EvolveActionDto?>(null);
            }

            // V1 Logic: Evolve the highest attack evolvable unit
            var unitToEvolve = evolvableUnits.First();
            _logger.LogInformation("[AiService:{GameId}] AI {PlayerId} decided to evolve Unit: {UnitName} ({UnitUid}), Attack: {Attack}.",
                gameId, aiPlayerId, unitToEvolve.Name, unitToEvolve.InstanceId, unitToEvolve.CurrentAttack); // Fix: Use InstanceId

            // Evolution typically doesn't require a target, but the action might allow one for specific effects.
            // Simple V1 evolves without target.
            return Task.FromResult<EvolveActionDto?>(new EvolveActionDto { UnitUid = unitToEvolve.InstanceId.ToString() }); // Fix: Use InstanceId // Convert Guid to string for DTO
        }

        /// <inheritdoc />
        public Task<List<AttackActionDto>> DecideAttacksAsync(GameStateDto gameState, string aiPlayerId)
        {
            _logger.LogDebug("[AiService:{GameId}] DecideAttacksAsync called for AI {PlayerId}.", gameState?.GameId ?? "Unknown", aiPlayerId);
            var gameId = gameState?.GameId ?? "Unknown";
            List<AttackActionDto> attacks = new List<AttackActionDto>();
            var states = GetPlayerStates(gameState, aiPlayerId);
            if (states == null)
            {
                _logger.LogError("[AiService:{GameId}] Could not get player states for AI {PlayerId} in DecideAttacksAsync.", gameId, aiPlayerId);
                return Task.FromResult(attacks);
            }
            var (aiState, opponentState) = states.Value;

            // Find AI units that can attack and create deep copies for simulation
            var readyUnits = aiState.Field
                .Where(u => u != null && u.CanAttack) // Ensure unit is not null and can attack
                .Select(u => DeepCopy(u)) // Create deep copies to avoid modifying original state
                .Where(u => u != null) // Filter out potential nulls from failed deep copy (defensive)
                .Select(u => new { Unit = u, SortAttack = u.CurrentAttack }) // Project for sorting
                .OrderByDescending(x => x.SortAttack) // Prioritize higher attack units
                .Select(x => x.Unit) // Select the copied unit
                .ToList();


            if (!readyUnits.Any())
            {
                _logger.LogDebug("[AiService:{GameId}] AI {PlayerId} has no units ready to attack.", gameId, aiPlayerId);
                return Task.FromResult(attacks);
            }

            // V1 Logic: Attack with all ready units. Prioritize targets: Ward > Highest Attack Follower > Opponent Leader
            foreach (var attacker in readyUnits)
            {
                string? targetUid = SelectAttackTarget(attacker, opponentState); // Use helper

                if (targetUid != null)
                {
                    attacks.Add(new AttackActionDto { AttackerUid = attacker.InstanceId.ToString(), TargetUid = targetUid }); // Fix: Use InstanceId // Convert Guid to string for DTO
                    _logger.LogInformation("[AiService:{GameId}] AI {PlayerId} decided attack: Unit {AttackerUid} -> Target {TargetUid}.",
                        gameId, aiPlayerId, attacker.InstanceId, targetUid); // Fix: Use InstanceId

                    // Basic simulation: Mark attacker as unable to attack again this turn
                    // (A more robust simulation would update game state based on attack outcome)
                    attacker.CanAttack = false; // Simplistic: Assume CanAttack is mutable for simulation state
                }
                else
                {
                    _logger.LogDebug("[AiService:{GameId}] No valid target found for AI attacker {AttackerUid}.", gameId, attacker.InstanceId); // Fix: Use InstanceId
                }
            }

            return Task.FromResult(attacks);
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
        private (PlayerStateDto AiState, PlayerStateDto? OpponentState)? GetPlayerStates(GameStateDto? gameState, string aiPlayerId)
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
        private List<CardInstanceDto> GetPlayableCards(PlayerStateDto aiState) // Changed return type
        {
            // Ensure aiState and Hand are not null before accessing
            if (aiState?.Hand == null)
            {
                _logger.LogWarning("[AiService:Helper] AI state or hand is null in GetPlayableCards.");
                return new List<CardInstanceDto>(); // Return empty list of correct type
            }
            // Return playable CardInstanceDto directly
            return aiState.Hand
                .Where(card => card != null && card.Cost <= aiState.CurrentPP) // Filter playable instances
                .ToList(); // Return List<CardInstanceDto>
        }
        /// <summary>
        /// Selects the best target for an attacking unit based on V1 AI logic.
        /// Prioritizes: Ward > Highest Attack Follower > Opponent Leader.
        /// Returns the target's UID (Unit UID or Player ID for leader) or null if no valid target.
        /// </summary>
        private string? SelectAttackTarget(CardInstanceDto attacker, PlayerStateDto? opponentState) // Fix: Change parameter type to CardInstanceDto
        {
            if (opponentState == null)
            {
                _logger.LogWarning("[AiService:Helper] Opponent state is null in SelectAttackTarget for attacker {AttackerUid}. Cannot select target.", attacker?.InstanceId.ToString() ?? "Unknown"); // Fix: Use InstanceId
                return null; // Cannot attack if no opponent state
            }
            // Ensure opponent field is not null before accessing
            // Corrected default list type to match PlayerStateDto.Field
            var opponentField = opponentState.Field ?? new List<CardInstanceDto>();


            // 1. Prioritize units with Ward
            var wardTargets = opponentField
                // Corrected Keywords check for case-insensitivity and Contains signature
                .Where(u => u != null && (u.Keywords?.Any(k => string.Equals(k, "Ward", StringComparison.OrdinalIgnoreCase)) ?? false))
                // Removed redundant null check on non-nullable int CurrentAttack
                .Select(u => new { Unit = u, SortAttack = u.CurrentAttack }) // Project before ordering
                .OrderByDescending(x => x.SortAttack) // Order by projected value
                .Select(x => x.Unit) // Select original unit back // Prioritize highest attack among Ward units
                .ToList();

            if (wardTargets.Any())
            {
                var target = wardTargets.First();
                _logger.LogDebug("[AiService:Helper] Attacker {AttackerUid} targeting Ward unit {TargetUid}", attacker?.InstanceId.ToString() ?? "Unknown", target.InstanceId); // Fix: Use InstanceId for both attacker and target
                return target.InstanceId.ToString(); // Fix: Use InstanceId // Convert Guid to string for return type
            }

            // 2. If no Ward, prioritize highest attack non-Ward follower
            var highestAttackFollower = opponentField
                 .Where(u => u != null) // Add null check
                                        // Removed redundant null check on non-nullable int CurrentAttack
                .Select(u => new { Unit = u, SortAttack = u.CurrentAttack }) // Project before ordering
                .OrderByDescending(x => x.SortAttack) // Order by projected value
                .Select(x => x.Unit) // Select original unit back
                .FirstOrDefault(); // Find the highest attack unit

            if (highestAttackFollower != null)
            {
                _logger.LogDebug("[AiService:Helper] Attacker {AttackerUid} targeting highest attack unit {TargetUid}", attacker?.InstanceId.ToString() ?? "Unknown", highestAttackFollower.InstanceId); // Fix: Use InstanceId for both attacker and target
                return highestAttackFollower.InstanceId.ToString(); // Fix: Use InstanceId // Convert Guid to string for return type
            }

            // 3. If opponent has no units on field, attack the opponent leader
            // Check if the attacker itself can attack the leader (some units might be restricted)
            // V1 assumes any unit can attack leader if field is empty.
            if (!opponentField.Any())
            {
                _logger.LogDebug("[AiService:Helper] Attacker {AttackerUid} targeting opponent leader {TargetUid}", attacker?.InstanceId.ToString() ?? "Unknown", opponentState.PlayerId); // Fix: Use InstanceId
                return opponentState.PlayerId; // Target opponent leader by PlayerId
            }

            // Should not happen if opponent has a state, but defensively return null
            _logger.LogWarning("[AiService:Helper] No target found for attacker {AttackerUid}, opponent has state but no units or leader target applicable?", attacker?.InstanceId.ToString() ?? "Unknown"); // Fix: Use InstanceId
            return null;
        }

        #endregion Helper Methods

        /// <inheritdoc />
        public Task<bool> IsAiPlayer(string playerId)
        {
            // Simple V1 implementation: Assumes the AI player always has the ID "player2".
            // TODO: Implement a more robust mechanism if player IDs can change or be configured.
            bool isAi = playerId == "player2";
            _logger.LogDebug("IsAiPlayer check for '{PlayerId}': Result={IsAi}", playerId, isAi);
            return Task.FromResult(isAi);
        }

        /// <inheritdoc />
        public async Task RequestAiActionAsync(string gameId, string aiPlayerId, GameActionContext context)
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
                    // Pass the context down
                    await aiTurnExecutor.ExecuteTurnAsync(gameId, aiPlayerId, context);

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
    }
}