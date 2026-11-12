using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Abilities;
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
using System.Threading;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Hubs;

namespace ShadowverseBeyond.Server.Services
{
    /// <summary>
    /// Implements the AI decision-making logic.
    /// </summary>
    public class AiService : IAiService
    {
        private readonly ILogger<AiService> _logger;
        private readonly AiLearningModel _learned;
        private readonly bool _generatedDefenseModes;
        private readonly bool _isolateTargetEffects;
        private readonly bool _protectPlannedMaterials;
        private readonly IServiceScopeFactory _serviceScopeFactory; // To resolve IGameService if needed, avoiding circular dependency
        private readonly IGameDataProvider _gameDataProvider;

        public AiService(ILogger<AiService> logger, IServiceScopeFactory serviceScopeFactory, AiLearningModel learned, Microsoft.Extensions.Configuration.IConfiguration? configuration=null, IGameDataProvider? gameDataProvider=null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _learned = learned;
            _generatedDefenseModes=configuration?.GetValue("AiStrategy:GeneratedDefenseModes",true)??true;
            _isolateTargetEffects=configuration?.GetValue("AiStrategy:IsolateTargetEffects",true)??true;
            _protectPlannedMaterials=configuration?.GetValue("AiStrategy:ProtectPlannedMaterials",true)??true;
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _gameDataProvider = gameDataProvider ?? GameDataProvider.Instance;
        }

        public async Task<MirageAiActionDecision> DecideMirageRecruitActionAsync(GameStateDto gameState, Guid aiPlayerId)
        {
            if (gameState.Mode != GameMode.Mirage || gameState.Phase != GamePhase.Recruit || gameState.IsGameOver)
                throw new InvalidOperationException("Mirage AI decisions require an active Recruit phase.");

            var aiState = gameState.Players.Single(p => p.PlayerId == aiPlayerId);
            var mirage = aiState.Mirage ?? throw new InvalidOperationException("Mirage AI player state is missing.");

            if (!mirage.HasSelectedLeader && mirage.LeaderCandidateCardIds.Count > 0)
            {
                var preferredLeaderId = mirage.LeaderCandidateCardIds
                    .OrderByDescending(id => id == 900102 ? 100 : id == 900101 ? 90 : 0)
                    .ThenBy(id => id)
                    .First();
                return new("MirageSelectLeader", preferredLeaderId);
            }

            var fieldUnits = aiState.Field.Where(c => c.Type != CardType.Leader).ToList();
            var weakestSellableFollower = fieldUnits
                .Where(c => c.Type == CardType.Follower && !c.IsMirageTriple)
                .OrderBy(FollowerCombatScore).ThenBy(c => c.InstanceId).FirstOrDefault();
            var handDeployableFollowers = aiState.Hand
                .Where(c => c.Type == CardType.Follower && !c.HasKeyword(CardKeyword.CannotUse)
                    && (c.IsMirageRecruited || c.CurrentCost <= aiState.CurrentPP))
                .OrderByDescending(FollowerCombatScore).ThenBy(c => c.InstanceId).ToList();
            var ownedCopies = aiState.Hand.Concat(aiState.Field)
                .Where(c => c.Type == CardType.Follower && !c.IsMirageTriple)
                .GroupBy(c => c.CardId).ToDictionary(g => g.Key, g => g.Count());
            int Copies(MirageShopOfferDto offer) => ownedCopies.GetValueOrDefault(offer.CardDefinitionId);
            var canReceiveCard = aiState.Hand.Count < GameConstants.Hand.MaxHandSize;
            var affordableOffers = mirage.ShopOffers.Where(o => o.BuyCost <= aiState.CurrentPP).ToList();
            var buyableOffers = canReceiveCard ? affordableOffers : new List<MirageShopOfferDto>();

            if (canReceiveCard && mirage.PendingTickets.Count > 0)
            {
                var ticket = mirage.PendingTickets[0];
                var chosen = ticket.CandidateCardDefinitionIds.Select(_gameDataProvider.GetCardDefinition)
                    .Where(def => def != null && def.IsMirageCard)
                    .OrderByDescending(def => def!.Attack * 10 + def.Health * 10 + (def.MirageTavernTier ?? def.Cost) * 5)
                    .ThenBy(def => def!.Id).FirstOrDefault()
                    ?? throw new InvalidOperationException($"Mirage discover ticket {ticket.TicketId} has no valid Mirage candidates.");
                return new("MirageRedeemTicket", new MirageRedeemTicketActionDto(ticket.TicketId, chosen.Id));
            }

            if (fieldUnits.Count < GameConstants.Field.MaxFieldSize)
            {
                var playableCards = await GetPlayableCardsAsync(gameState.GameId, aiPlayerId, aiState);
                var cardToPlay = playableCards
                    .OrderByDescending(c => c.Type == CardType.Follower)
                    .ThenByDescending(FollowerCombatScore).ThenBy(c => c.InstanceId).FirstOrDefault();
                if (cardToPlay != null)
                    return new("PlayCard", new PlayCardActionDto { CardUid = cardToPlay.InstanceId, TargetUids = null });
            }

            // 满场可玩性检查会排除全部随从；换怪先比较手牌，卖出后再走正常部署校验。
            if (fieldUnits.Count >= GameConstants.Field.MaxFieldSize && weakestSellableFollower != null
                && handDeployableFollowers.Any(c => FollowerCombatScore(c) >= FollowerCombatScore(weakestSellableFollower) + 20))
                return new("MirageSellCard", weakestSellableFollower.InstanceId);

            if (mirage.CanUpgradeTier && !buyableOffers.Any(o => Copies(o) >= 2)
                && (gameState.Mirage?.RoundNumber == 2 && mirage.TavernTier == 1
                    || aiState.CurrentPP - mirage.UpgradeCost >= MirageRuleConfig.DefaultBuyCost
                    || mirage.UpgradeCost <= Math.Max(2, mirage.TavernTier)
                    || fieldUnits.Count >= GameConstants.Field.MaxFieldSize && aiState.CurrentPP >= mirage.UpgradeCost))
                return new("MirageUpgradeTier", new { });

            var tripleOffer = buyableOffers.Where(o => Copies(o) >= 2)
                .OrderByDescending(OfferCombatScore).ThenBy(o => o.CardDefinitionId).ThenBy(o => o.OfferId).FirstOrDefault();
            if (tripleOffer != null)
                return new("MirageBuyOffer", tripleOffer.OfferId);

            var bestOffer = buyableOffers.Where(o => fieldUnits.Count < GameConstants.Field.MaxFieldSize
                    || weakestSellableFollower != null && OfferCombatScore(o) >= FollowerCombatScore(weakestSellableFollower) + 20)
                .OrderByDescending(o => OfferCombatScore(o) + o.TavernTier * 5 + (Copies(o) == 1 ? 50 : 0))
                .ThenBy(o => o.CardDefinitionId).ThenBy(o => o.OfferId).FirstOrDefault();
            if (bestOffer != null)
                return new("MirageBuyOffer", bestOffer.OfferId);

            if (aiState.LeaderCard != null && aiState.CurrentPP > 0)
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var gameService = scope.ServiceProvider.GetRequiredService<IGameService>();
                var actPlayability = await gameService.GetActPlayabilityAsync(
                    gameState.GameId,
                    aiPlayerId,
                    aiState.LeaderCard.InstanceId,
                    CancellationToken.None);
                var playableAbility = actPlayability.Abilities.FirstOrDefault(a =>
                    a.IsPlayable && (!a.RequiresTargetSelection || a.HasValidTargets));
                if (playableAbility != null || (actPlayability.Abilities.Count == 0
                    && actPlayability.IsPlayable
                    && (!actPlayability.RequiresTargetSelection || actPlayability.HasValidTargets)))
                {
                    return new("RequestAct", new ActActionDto
                    {
                        CardUid = aiState.LeaderCard.InstanceId,
                        AbilityRuntimeId = playableAbility?.AbilityRuntimeId
                    });
                }
            }

            if (aiState.CurrentPP >= mirage.RefreshCost + MirageRuleConfig.DefaultBuyCost
                && (fieldUnits.Count < GameConstants.Field.MaxFieldSize || weakestSellableFollower != null || ownedCopies.Count > 0))
                return new("MirageRefreshShop", new { });

            var hasTripleFreezeTarget = mirage.ShopOffers.Any(o => Copies(o) >= 2);
            if (hasTripleFreezeTarget != mirage.IsShopFrozen)
                return new("MirageToggleFreeze", new { });

            return new("MirageSetReady", new { });
        }

        private static int FollowerCombatScore(CardInstanceDto card) =>
            card.CurrentAttack * 10 + card.CurrentHealth * 10 + (card.IsMirageTriple ? 500 : 0);

        private static int OfferCombatScore(MirageShopOfferDto offer) => offer.Attack * 10 + offer.Health * 10;

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

            using var eternalContext = _serviceScopeFactory.CreateScope();
            var eternalGame = eternalContext.ServiceProvider.GetService<IGameService>()?.GetGameState(gameId);
            using var eternal = AiEternalEngine.Use(eternalGame,aiPlayerId);
            if(AiEternalEngine.Mulligan(aiState) is {} engineReplacement)
                return Task.FromResult(engineReplacement);
            if(eternalGame?.LegacyAiPlayerId!=aiPlayerId&&AiWeaverPlan.Mulligan(aiState,eternalGame?.GetPlayerState(aiPlayerId)?.InitialDeckDefinitionIds) is {} weaverReplacement)
                return Task.FromResult(weaverReplacement);

            // Simple V1 fallback for decks without the verified resource engine.
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
        public async Task<PlayCardActionDto?> DecidePlayCardAsync(GameStateDto gameState, Guid aiPlayerId, AiStrategyPlanner.TurnPlan? strategy = null)
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
            using var eternalContext = _serviceScopeFactory.CreateScope();
            var decisionGame=eternalContext.ServiceProvider.GetService<IGameService>()?.GetGameState(gameId);
            using var recovery=AiRecoveryPolicy.Use(decisionGame,aiPlayerId);
            using var eternal = AiEternalEngine.Use(decisionGame,aiPlayerId);

            var playableOptions = await GetPlayableOptionsAsync(gameId, aiPlayerId, aiState);
            Dictionary<Guid,AiHandEconomy.Rule> handEconomy;
            var economyGame=eternalContext.ServiceProvider.GetService<IGameService>()?.GetGameState(gameId);
            var economyMapper=eternalContext.ServiceProvider.GetService<AutoMapper.IMapper>();
            var economyAccessor=eternalContext.ServiceProvider.GetService<IGameContextAccessor>();
            using(var economyContext=economyGame==null||economyMapper==null||economyAccessor==null?null:new GameContext(eternalContext.ServiceProvider,
                economyMapper,economyAccessor,economyGame,aiPlayerId))
                handEconomy=AiHandEconomy.Read(economyContext,aiState,opponentState);
            var playableCards = playableOptions.Select(option=>option.View).ToList();
            if (!playableCards.Any())
            {
                _logger.LogDebug("[AiService:{GameId}] No playable cards found for AI {PlayerId}.", gameId, aiPlayerId);
                return null;
            }

            if (_learned.Current != null || strategy != null || AiEternalEngine.Active(aiState))
            {
                var w = LearnedWeights(gameId, aiPlayerId);
                var needsDefense = opponentState != null && aiState.LeaderCard != null
                    && AiTacticalSearch.EstimateBoardDamage(opponentState.Field, aiState.Field,aiState.LeaderCard) >= aiState.LeaderCard.CurrentHealth;
                var selected = AiPlayCandidates.Select(playableOptions, strategy,
                    AiStrategyPlanner.PositionKey(aiState,opponentState ?? new PlayerStateDto()), option => _learned.Score(gameState!, aiPlayerId, "play", option.View, weights:w)
                        + option.Cost*w.Tempo + (option.View.Type==CardType.Follower ? (option.View.CurrentAttack+option.View.CurrentHealth)*.35*w.Board : w.Utility)
                        + (strategy?.PlayBias(option.Original.CardId) ?? 0)
                        + AiEternalEngine.RoleBias(aiState,opponentState,option.Original,playableOptions.Any(other=>other.Original.InstanceId!=option.Original.InstanceId))
                        + AiHandEconomy.PlayBias(aiState,option.Original,option.Cost,handEconomy.GetValueOrDefault(option.Original.InstanceId))
                        + (needsDefense ? 0 : AiAbilityScoring.FragileHandPriority(aiState, option.Original) + AiEternalEngine.PlayBias(aiState,option.Original,option.Cost)));
                return selected?.Action();
            }

            // V1 Logic: Prioritize playing the highest-cost follower if possible
            var playableFollowers = playableCards
                .Where(c => c.Type is CardType.Follower or CardType.Amulet)
                .OrderByDescending(c => c.CurrentCost)
                .ToList();

            var aiFieldCount = aiState.Field.Count(c => c != null && c.Type != CardType.Leader);
            if (aiFieldCount < GameConstants.Field.MaxFieldSize && playableFollowers.Any())
            {
                var cardToPlay = playableFollowers.First();
                _logger.LogInformation("[AiService:{GameId}] AI {PlayerId} decided to play Follower: {CardName} ({CardInstanceId}), Cost: {Cost}.", // Changed Uid to InstanceId in log message
                    gameId, aiPlayerId, cardToPlay.Name, cardToPlay.InstanceId, cardToPlay.Cost); // Changed Uid to InstanceId
                // Followers generally don't require a target when played, unless they have a specific Fanfare.
                // Simple V1 plays without target.
                return playableOptions.First(option=>ReferenceEquals(option.View,cardToPlay)).Action();
            }

            // If no follower can be played, try playing the highest-cost spell
            var playableSpells = playableCards
                 .Where(c => c.Type == CardType.Spell)
                 .OrderByDescending(c => c.CurrentCost)
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

                var action = playableOptions.First(option=>ReferenceEquals(option.View,cardToPlay)).Action();
                action.TargetUids = targetUid.HasValue ? [targetUid.Value] : null;
                return action;
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

            if (aiState.IsPpBoostArmed)
            {
                return Task.FromResult(false);
            }

            var canEnablePlay = aiState.Hand.Any(card =>
                card != null &&
                card.CurrentCost > aiState.CurrentPP &&
                card.CurrentCost <= aiState.CurrentPP + GameConstants.PlayPoints.PpBoostAmount);

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
            var unitToEvolve = evolvableUnits.OrderByDescending(u => u.CurrentAttack + _learned.Score(gameState!, aiPlayerId, "evolve", u, weights:LearnedWeights(gameId,aiPlayerId))).First();

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

            if (_learned.Current != null && opponentState != null)
            {
                var w=LearnedWeights(gameId,aiPlayerId);
                var options=new List<(CardInstanceDto source,CardInstanceDto target,double score)>();
                foreach(var attacker in aiState.Field.Where(u=>u.CanPerformAttackDto(aiPlayerId,gameState!.CurrentPlayerId==aiPlayerId)))
                {
                    foreach(var target in LegalAttackTargets(attacker,opponentState))
                    {
                        var face=target.Type==CardType.Leader;
                        var lethal=face&&attacker.CurrentAttack>=target.CurrentHealth;
                        var kill=!face&&(attacker.CurrentAttack>=target.CurrentHealth||attacker.Keywords.Contains(CardKeyword.Bane));
                        var survives=face||target.CurrentAttack<attacker.CurrentHealth;
                        var baseline=lethal?10000:face?attacker.CurrentAttack*w.Face:((kill?target.CurrentAttack+target.CurrentHealth:attacker.CurrentAttack)*w.Board+(survives?2:-attacker.CurrentAttack)*w.Survival);
                        options.Add((attacker,target,baseline+_learned.Score(gameState!,aiPlayerId,"attack",attacker,target,w)));
                    }
                }
                if(options.Count==0)return Task.FromResult<AttackActionDto?>(null);
                var best=options.OrderByDescending(o=>o.score).First();
                return Task.FromResult<AttackActionDto?>(new AttackActionDto{AttackerUid=best.source.InstanceId,TargetUid=best.target.InstanceId});
            }

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

        private AiWeights LearnedWeights(string gameId,Guid playerId)
        {
            using var scope=_serviceScopeFactory.CreateScope();
            var game=scope.ServiceProvider.GetRequiredService<IGameService>().GetGameState(gameId);
            return _learned.Weights(game,playerId);
        }
        internal static IEnumerable<CardInstanceDto> LegalAttackTargets(CardInstanceDto attacker,PlayerStateDto opponent)
        {
            if(attacker.HasKeyword(CardKeyword.Shackle))yield break;
            var targets=opponent.Field.Where(c=>c.Type==CardType.Follower&&!c.HasKeyword(CardKeyword.Ambush)&&!c.HasKeyword(CardKeyword.Pressure)).ToList();
            var ward=targets.Any(c=>c.HasKeyword(CardKeyword.Ward))&&!attacker.HasKeyword(CardKeyword.IgnoreWard);
            if(!attacker.HasKeyword(CardKeyword.CannotAttackFollower))foreach(var target in targets.Where(c=>!ward||c.HasKeyword(CardKeyword.Ward)))yield return target;
            if(!ward&&!attacker.HasKeyword(CardKeyword.CannotAttackLeader)&&(attacker.ZoneStayTurn>0||attacker.HasStorm())&&opponent.LeaderCard!=null)yield return opponent.LeaderCard;
        }

        #region Helper Methods

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

        private async Task<List<CardInstanceDto>> GetPlayableCardsAsync(string gameId, Guid aiPlayerId, PlayerStateDto aiState)
        {
            if (aiState?.Hand == null)
            {
                _logger.LogWarning("[AiService:Helper] AI state or hand is null in GetPlayableCards.");
                return new List<CardInstanceDto>();
            }
            var spendablePp = aiState.CurrentPP + (aiState.IsPpBoostArmed ? GameConstants.PlayPoints.PpBoostAmount : 0);
            var costPlayable = aiState.Hand
                .Where(card => card != null && (card.IsMirageRecruited || card.CurrentCost <= spendablePp))
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
        /// Gets cards from the AI's hand that can currently be played based on Play Points.
        /// </summary>
        private async Task<List<AiPlayCandidates.Candidate>> GetPlayableOptionsAsync(string gameId, Guid actor, PlayerStateDto player)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IGameService>();
            return await AiPlayCandidates.Collect(player,
                (uid,token)=>service.GetCardPlayabilityAsync(gameId,actor,uid,token),
                (uid,mode,token)=>service.GetCardPlayabilityForModeAsync(gameId,actor,uid,mode,token),
                CancellationToken.None,_logger);
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
            var attackerIgnoresWard = attacker.HasKeyword(CardKeyword.IgnoreWard);

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

                if (hasWardFollowers && !attackerIgnoresWard)
                {
                    return false;
                }

                if (attackerCannotAttackLeader)
                {
                    return false;
                }

                if (attackerCard.ZoneStayTurn == 0 && !attackerCard.HasStorm())
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

            if (!attackerIgnoresWard && wardTargets.Any())
            {
                var target = wardTargets.First();
                _logger.LogDebug("[AiService:Helper] Attacker {AttackerUid} targeting Ward unit {TargetUid}", attacker?.InstanceId.ToString() ?? "Unknown", target.InstanceId); // Fix: Use InstanceId for both attacker and target
                return target.InstanceId; // Fix: Use InstanceId // Convert Guid to string for return type
            }
            else if (!attackerIgnoresWard && hasWardFollowers)
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
        public async Task RequestAiActionAsync(string gameId, Guid aiPlayerId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogInformation("[AiService:{GameId}] Received request to execute AI action for player {PlayerId}.", gameId, aiPlayerId);

            // 执行失败由调用方的后台任务边界记录；此处吞错会把失败回合伪装成正常结束。
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var aiTurnExecutor = scope.ServiceProvider.GetRequiredService<AiTurnExecutor>();
                _logger.LogDebug("[AiService:{GameId}] Resolved AiTurnExecutor within scope for player {PlayerId}.", gameId, aiPlayerId);

                await aiTurnExecutor.ExecuteTurnAsync(gameId, aiPlayerId, cancellationToken);

                _logger.LogInformation("[AiService:{GameId}] AiTurnExecutor completed action for player {PlayerId}.", gameId, aiPlayerId);
            }
        }

        /// <inheritdoc />
        public async Task<PlayerChoiceResult> DecideTargetChoiceAsync(PlayerChoiceRequest request, GameStateDto gameState, CancellationToken token = default)
        {
            var gameId = gameState.GameId;
            var aiPlayerId = request.RequestingPlayerId;
            _logger.LogInformation("[AiService:{GameId}] AI {PlayerId} is deciding for choice {ChoiceId}.", gameId, aiPlayerId, request.PendingChoiceId);

            var chosenIds = new List<Guid>();
            var minSelectionCount = Math.Max(0, request.MinSelectionCount);
            var selectableTargetIds = request.SelectableTargetIds ?? new List<Guid>();

            using var scope = _serviceScopeFactory.CreateScope();
            var game = scope.ServiceProvider.GetRequiredService<IGameService>().GetGameState(gameId);
            using var recovery=AiRecoveryPolicy.Use(game,aiPlayerId);
            using var eternal = AiEternalEngine.Use(game,aiPlayerId);
            using var combatComparison = AiCombatThreat.UseLegacy(game?.IsLocalAiTraining == true && game.LegacyCombatThreatPlayerId == aiPlayerId);
            var source = game?.AbilityManager?.GetAbilityByRuntimeId(request.SourceAbilityRuntimeId);
            var player = gameState.Players.FirstOrDefault(p => p.PlayerId == aiPlayerId);
            var hand = player?.Hand;
            var choosesFusionMaterials = source?.HasKeyword(AbilityKeyword.Fusion) == true
                && hand != null && selectableTargetIds.Count > 0
                && selectableTargetIds.All(id => hand.Any(card => card.InstanceId == id));
            if (choosesFusionMaterials && request.MaxSelectionCount > 0)
                minSelectionCount = Math.Max(1, minSelectionCount);

            if (minSelectionCount > 0 && selectableTargetIds.Count == 0 && request.IsCancellable)
            {
                _logger.LogInformation("[AiService:{GameId}] AI {PlayerId} is cancelling choice {ChoiceId} because no selectable targets are available.",
                    gameId, aiPlayerId, request.PendingChoiceId);
                return PlayerChoiceResult.Cancelled;
            }

            var isOptionRequest = request.TargetKey is "decision" or "discover";
            var optionSource = request.OptionSourceCard ?? source?.SourceCard;
            var strategyPlanner = scope.ServiceProvider.GetService<AiStrategyPlanner>();
            if (game != null) strategyPlanner?.RecordChoicePosition(game, gameState, request);
            if (game?.AiGeneratedStormSource is Guid plannedSource && optionSource?.InstanceId == plannedSource
                && game.AiGeneratedStormTokenId is int plannedToken
                && request.TargetKey == "decision" && request.MinSelectionCount == 1 && request.MaxSelectionCount == 1)
            {
                var tokenOption = selectableTargetIds.FirstOrDefault(id => request.DecisionOptions.TryGetValue(id, out var config)
                    && AiGeneratedTokenTactics.Token(config) == plannedToken);
                if (tokenOption != Guid.Empty){
                    AiDecisionFlowFacts.Remember(game,request,[tokenOption]);return new PlayerChoiceResult { ChosenTargetIds = [tokenOption] };
                }
            }

            var modeEnemy=gameState.Players.FirstOrDefault(p=>p.PlayerId!=aiPlayerId);
            if(game!=null && player!=null && modeEnemy!=null
                && AiGeneratedTokenTactics.PreferSummonLethal(game,player,modeEnemy,request,optionSource) is Guid lethalMode)
            { AiDecisionFlowFacts.Remember(game,request,[lethalMode]);return new PlayerChoiceResult {ChosenTargetIds=[lethalMode]}; }

            var generatedDefense=_generatedDefenseModes&&game!=null
                ?AiGeneratedDefenseModes.Read(GameServiceProvider.CurrentGameContext,game,gameState,request,optionSource):null;
            if(generatedDefense?.PreferredIndex is int defensiveIndex){
                var chosen=new List<Guid>{selectableTargetIds[defensiveIndex]};AiGeneratedDefenseModes.Remember(game,request,chosen);
                return new PlayerChoiceResult{ChosenTargetIds=chosen};
            }

            var plannedModes = game == null ? null : strategyPlanner?.ChoosePlannedOptions(game, gameState, request);
            PlayerChoiceResult SubmitOption(List<Guid> local, List<Guid>? suggested = null, string policy = "model-option")
            {
                var selected = suggested ?? local;
                if (game != null && suggested != null)
                    strategyPlanner?.RecordOptionComparison(game, request, local, selected, policy);
                AiGeneratedDefenseModes.Remember(game, request, selected);
                return new PlayerChoiceResult { ChosenTargetIds = selected };
            }

            // A verified lethal route above keeps priority over a persistent-board setup.
            if (game?.LegacyAiPlayerId != aiPlayerId && optionSource?.BaseDefinitionId == AiStrategyPlanner.StarCallId
                && request.TargetKey == "decision" && request.MinSelectionCount == 1 && request.MaxSelectionCount == 1
                && player != null && player.Field.Count <= 2 && player.Hand.Count <= 5
                &&(!_generatedDefenseModes||modeEnemy==null||(!AiTacticalSearch.HasVisibleLethalThreat(player,modeEnemy)&&generatedDefense==null)||generatedDefense?.ExistingAnswer==true)
                && (player.Field.Any(card => card.CardId == 1474120) || player.Emblem.Any(card => card.CardId == 1474276))
                && GameDataProvider.Instance.GetCardDefinition(1250402)?.Cost == 0)
            {
                var skeletonMode = selectableTargetIds.FirstOrDefault(id => request.DecisionOptions.TryGetValue(id, out var config)
                    && AiGeneratedTokenTactics.Token(config) == 1250402);
                if (skeletonMode != Guid.Empty)
                    return SubmitOption([skeletonMode], plannedModes, "turn-plan-option");
            }

            var searchEnemy=gameState.Players.FirstOrDefault(p=>p.PlayerId!=aiPlayerId);
            if(game!=null && player!=null && searchEnemy!=null
                && AiEternalEngine.PreferSearch(player,searchEnemy,game,request,optionSource) is Guid searchOption)
            {
                if (plannedModes != null) return SubmitOption([searchOption], plannedModes, "turn-plan-option");
                AiDecisionFlowFacts.Remember(game, request, [searchOption]);
                return new PlayerChoiceResult { ChosenTargetIds = [searchOption] };
            }

            var useLegacyPolicy = game?.LegacyAiPlayerId == aiPlayerId;
            var opponent = gameState.Players.FirstOrDefault(p => p.PlayerId != aiPlayerId);
            var materialPlan = !useLegacyPolicy && game != null && player != null && opponent != null
                && !AiTurnExecutor.HasVisibleLethalThreat(player, opponent)
                ? strategyPlanner?.GetCurrentPlan(game, aiPlayerId) : null;
            var effects = !useLegacyPolicy && source is Ability ability
                ? AiTargetScoring.FindEffectsForChoice(ability.Config.Effects, request.TargetKey,_isolateTargetEffects)
                : Array.Empty<AiTargetScoring.Effect>();
            Dictionary<Guid,AiHandEconomy.Rule> discardEconomy=new();
            if(!useLegacyPolicy&&game!=null&&player!=null&&effects.Any(e=>e.Type==EffectType.Discard)
                &&scope.ServiceProvider.GetService<AutoMapper.IMapper>() is {} discardMapper
                &&scope.ServiceProvider.GetService<IGameContextAccessor>() is {} discardAccessor){
                using var economyContext=new GameContext(scope.ServiceProvider,discardMapper,discardAccessor,game,aiPlayerId);
                discardEconomy=AiHandEconomy.Read(economyContext,player,opponent);
            }
            var reservedMaterials = _protectPlannedMaterials && materialPlan!=null && game!=null
                ? strategyPlanner?.ReservedDiscardMaterials(game,gameState,request,source as Ability) ?? [] : new HashSet<Guid>();
            var playerTargets = gameState.Players.Where(p => p.LeaderCard != null)
                .ToDictionary(p => p.PlayerId, p => p.LeaderCard!);

            if (minSelectionCount > 0 && selectableTargetIds.Any())
            {
                if (gameState.Mode == GameMode.Mirage)
                {
                    var aiFieldById = gameState.Players
                        .FirstOrDefault(p => p.PlayerId == aiPlayerId)?
                        .Field
                        .ToDictionary(c => c.InstanceId) ?? new Dictionary<Guid, CardInstanceDto>();
                    chosenIds = selectableTargetIds
                        .OrderBy(id => aiFieldById.TryGetValue(id, out var card) && card.IsMirageTriple ? 1 : 0)
                        .ThenBy(id => id)
                        .Take(minSelectionCount)
                        .ToList();
                }
                else
                {
                    var random = new Random();
                    var w=LearnedWeights(gameId,aiPlayerId);
                    chosenIds = selectableTargetIds
                                       .OrderBy(id => reservedMaterials.Contains(id) ? 1 : 0)
                                       .ThenByDescending(id => {
                                           var card=request.SelectableCardInstances.FirstOrDefault(c=>c.InstanceId==id)??gameState.FindCardInstanceById(id.ToString());
                                           if (choosesFusionMaterials && card != null && !useLegacyPolicy && player != null)
                                               return -AiAbilityScoring.PlannedMaterialValue(player, card, materialPlan);
                                           if (choosesFusionMaterials && card != null)
                                               return _learned.Score(gameState, aiPlayerId, "choose", null, card, w)
                                                   - card.CurrentCost - (card.CurrentAttack + card.CurrentHealth) * .1;
                                           if (card == null && !useLegacyPolicy) playerTargets.TryGetValue(id, out card);
                                           if (effects.Count > 0 && effects.All(effect => effect.Type == EffectType.Discard)
                                               && player != null && card?.OwnerPlayerId == aiPlayerId)
                                               return -AiHandEconomy.DiscardValue(player, card, materialPlan,discardEconomy.GetValueOrDefault(card.InstanceId));
                                           if (effects.Count > 0 && card != null)
                                           {
                                               var learned = _learned.Current == null ? 0 : _learned.Score(gameState, aiPlayerId, "choose", null, card, w);
                                               return AiTargetScoring.Score(card, aiPlayerId, effects) + .04 * Math.Clamp(learned, -8, 8);
                                           }
                                           if (_learned.Current == null) return isOptionRequest && !useLegacyPolicy ? 0 : random.NextDouble();
                                           return _learned.Score(gameState,aiPlayerId,"choose",null,card,w)+(card==null?0:(card.CurrentAttack+card.CurrentHealth)*.1);
                                       })
                                       .Take(minSelectionCount)
                                       .ToList();
                }

                _logger.LogInformation("[AiService:{GameId}] AI {PlayerId} selected {Count} targets: [{Targets}]",
                    gameId, aiPlayerId, chosenIds.Count, string.Join(", ", chosenIds));
            }
            else
            {
                _logger.LogInformation("[AiService:{GameId}] AI {PlayerId} is choosing no targets as MinSelectionCount is 0 or no targets are available.", gameId, aiPlayerId);
            }

            if (plannedModes != null) return SubmitOption(chosenIds, plannedModes, "turn-plan-option");
            if (game != null && isOptionRequest && strategyPlanner != null
                && await strategyPlanner.ChooseFromOptionsAsync(game, gameState, request, optionSource, chosenIds, token) is { } modes)
                return SubmitOption(chosenIds, modes);

            var localChosen=chosenIds.ToArray();
            var targetDecision=!useLegacyPolicy&&game!=null?strategyPlanner?.SelectPlannedTarget(game,gameState,request,source as Ability):null;
            if(targetDecision?.Uid is Guid proposed) {
                // Retain the established local immediate-lethal preference. This score is bounded,
                // not a replay of every protection/response; no new damage or legality is invented.
                var locallyLethal=opponent?.LeaderCard is {} leader&&localChosen.Any(id=>id==leader.InstanceId||id==opponent.PlayerId)
                    &&effects.Count>0&&AiTargetScoring.Score(leader,aiPlayerId,effects)>=100;
                if(locallyLethal&&!localChosen.Contains(proposed))targetDecision=targetDecision with{Uid=null,Status="blocked",Reason="local-lethal-target-priority"};
                else chosenIds=[proposed];
            }
            if(game!=null)strategyPlanner?.RecordTargetSelection(game,gameState,request,source as Ability,localChosen,chosenIds,targetDecision);
            var result = new PlayerChoiceResult
            {
                WasCancelled = false,
                ChosenTargetIds = chosenIds
            };

            AiDecisionFlowFacts.Remember(game,request,chosenIds);
            return result;
        }
    }
}
