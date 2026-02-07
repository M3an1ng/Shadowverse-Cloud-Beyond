using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Targets; // For TargetId, TargetIdType
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters; // For SummonUnitEffectParams
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Services; // For ITargetProcessingService
using ShadowverseBeyond.Server.Domain.Constants; // For GameConstants, CardKeyword
using ShadowverseBeyond.Server.Services.Abstractions; // For IGameDataProvider (assuming location)
using ShadowverseBeyond.Server.Dtos; // For CardZone
using ShadowverseBeyond.Server.Domain.Abilities.Runtime; // For RuntimeAbility
// Removed using for DTO: using ShadowverseBeyond.Server.Dtos.SignalR.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Bases;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;

using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Metadata;
using ShadowverseBeyond.Server.Domain.Effects;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers
{
    /// <summary>
    /// Handles the application of the SummonUnit effect.
    /// Targets Players to summon units onto their field.
    /// </summary>
    [EffectHandler(EffectType.Summon)]
    public class SummonEffectHandler : EffectHandlerBase<SummonEffectParams>
    {
        private readonly ITargetProcessingService _targetProcessingService;
        private readonly ShadowverseBeyond.Server.Domain.Factories.ICardFactory _cardFactory;

        public SummonEffectHandler(
            ILogger<SummonEffectHandler> logger,
            ITargetProcessingService targetProcessingService,
            ShadowverseBeyond.Server.Domain.Factories.ICardFactory cardFactory)
            : base(logger)
        {
            _targetProcessingService = targetProcessingService ?? throw new ArgumentNullException(nameof(targetProcessingService));
            _cardFactory = cardFactory ?? throw new ArgumentNullException(nameof(cardFactory));
        }

        public override GameStateActionResult Apply(Effect effect, GameContext context, SummonEffectParams parameters)
        {
            const string effectName = "SummonEffect";
            var cardDefinitionIds = parameters.CardDefinitionId?.Where(id => id > 0).ToList() ?? new List<int>();
            var cardIdText = cardDefinitionIds.Count > 0 ? string.Join(",", cardDefinitionIds) : "[]";
            Logger.LogDebug("{EffectName}: Starting with CardDefinitionIds={CardDefIds}, Count={Count}.",
                           effectName, cardIdText, parameters.SummonCount);

            var expectedPositionByPlayer = new Dictionary<Guid, int>();

            int NextExpectedPosition(PlayerState player)
            {
                if (!expectedPositionByPlayer.TryGetValue(player.PlayerId, out var nextPosition))
                {
                    nextPosition = player.Field.Count + 1;
                }
                expectedPositionByPlayer[player.PlayerId] = nextPosition + 1;
                return nextPosition;
            }

            DomainOperationResult CreatePackedResult(PlayerState owner, CardInstance card)
            {
                var packedResult = new DomainOperationResult { Success = true };
                packedResult.GeneratedEvents.Add(new CardMovedEvent(card, card.CurrentZone, CardZone.Extra, CardMoveReason.Packed));
                packedResult.GeneratedEvents.Add(new CardPackedEvent(owner, card));
                return packedResult;
            }

            if (parameters.CardSourceMode == Common.CardSourceMode.Instance)
            {
                var targetCards = effect.SelectedTargets
                    .Where(target => target.Card != null)
                    .Select(target => target.Card!)
                    .ToList();

                if (targetCards.Count == 0)
                {
                    return new GameStateActionResult
                    {
                        Success = false,
                        ErrorMessage = "Summon instance mode requires card targets."
                    };
                }

                var instanceResult = new GameStateActionResult { Success = true };
                var availableSlotsByPlayer = new Dictionary<Guid, int>();
                foreach (var card in targetCards)
                {
                    var owner = context.CurrentGameState.GetPlayerState(card.OwnerPlayerId);
                    if (owner == null)
                    {
                        return new GameStateActionResult
                        {
                            Success = false,
                            ErrorMessage = $"Summon instance mode owner not found for card {card.InstanceId}."
                        };
                    }

                    if (!availableSlotsByPlayer.TryGetValue(owner.PlayerId, out var availableSlots))
                    {
                        availableSlots = Math.Max(0, GameConstants.Field.MaxFieldSize - owner.FieldOccupancyCount);
                    }
                    if (availableSlots <= 0)
                    {
                        if (card.CurrentZone == CardZone.Staging)
                        {
                            Logger.LogInformation("{EffectName}: Field is full for player {PlayerId}; packing staged card {CardId}.", effectName, owner.PlayerId, card.InstanceId);
                            var packedResult = CreatePackedResult(owner, card);
                            instanceResult.GeneratedEvents.AddRange(packedResult.GeneratedEvents);
                        }
                        else
                        {
                            Logger.LogInformation("{EffectName}: Skipping summon for card {CardId} because field is full for player {PlayerId}.", effectName, card.InstanceId, owner.PlayerId);
                        }
                        availableSlotsByPlayer[owner.PlayerId] = 0;
                        continue;
                    }

                    var expectedPosition = NextExpectedPosition(owner);
                    instanceResult.GeneratedEvents.Add(new SummonEvent(owner, card, expectedPosition));
                    availableSlotsByPlayer[owner.PlayerId] = availableSlots - 1;
                }

                return instanceResult;
            }

            if (parameters.CardSourceMode == Common.CardSourceMode.Copy)
            {
                var targetCards = effect.SelectedTargets
                    .Where(target => target.Card != null)
                    .Select(target => target.Card!)
                    .ToList();

                if (targetCards.Count == 0)
                {
                    return new GameStateActionResult
                    {
                        Success = false,
                        ErrorMessage = "Summon copy mode requires card targets."
                    };
                }

                var copyPlayerTargets = PlayerScopeResolver.ResolvePlayers(
                    context,
                    parameters.PlayerScope);

                if (copyPlayerTargets.Count == 0)
                {
                    return new GameStateActionResult
                    {
                        Success = false,
                        ErrorMessage = "Summon copy mode could not resolve player scope."
                    };
                }

                var copyResult = new GameStateActionResult { Success = true };
                foreach (var player in copyPlayerTargets)
                {
                    var availableSlots = Math.Max(0, GameConstants.Field.MaxFieldSize - player.FieldOccupancyCount);

                    var packedCount = 0;
                    foreach (var card in targetCards)
                    {
                        for (int i = 0; i < parameters.SummonCount; i++)
                        {
                            var copy = new CardInstance(Guid.NewGuid(), card.CurrentDefinitionId, player.PlayerId)
                            {
                                CurrentZone = CardZone.Staging
                            };
                            copyResult.GeneratedEvents.Add(new CopyCardEvent(player, card, copy));
                            if (availableSlots > 0)
                            {
                                var expectedPosition = NextExpectedPosition(player);
                                copyResult.GeneratedEvents.Add(new SummonEvent(player, copy, expectedPosition));
                                availableSlots -= 1;
                            }
                            else
                            {
                                var packedResult = CreatePackedResult(player, copy);
                                copyResult.GeneratedEvents.AddRange(packedResult.GeneratedEvents);
                                packedCount += 1;
                            }
                        }
                    }

                    if (packedCount > 0)
                    {
                        Logger.LogInformation("{EffectName}: Field is full; packed {PackedCount} copied card(s) for player {PlayerId}.", effectName, packedCount, player.PlayerId);
                    }
                }

                return copyResult;
            }

            Func<TargetInfo, GameContext, DomainOperationResult> summonAction =
                (targetInfo, ctx) =>
                {
                    if (targetInfo.Type != Common.TargetType.Player || targetInfo.Player == null)
                    {
                        Logger.LogWarning("{EffectName}: Target is not a player or player data is missing. TargetType: {TargetType}", effectName, targetInfo.Type);
                        return new DomainOperationResult { Success = false, ErrorMessage = "Summon effect can only be applied to players." };
                    }

                    var player = targetInfo.Player;
                    var availableSlots = Math.Max(0, GameConstants.Field.MaxFieldSize - player.FieldOccupancyCount);
                    if (parameters.SummonCount <= 0)
                    {
                        Logger.LogInformation("{EffectName}: Summon count is zero for player {PlayerId}.", effectName, player.PlayerId);
                        return new DomainOperationResult { Success = true };
                    }

                    if (cardDefinitionIds.Count == 0)
                    {
                        Logger.LogWarning("{EffectName}: No CardDefinitionIds provided for summon.", effectName);
                        return new DomainOperationResult { Success = false, ErrorMessage = "Summon requires at least one CardDefinitionId." };
                    }

                    var tokensToSummon = new List<CardInstance>();
                    foreach (var cardDefinitionId in cardDefinitionIds)
                    {
                        for (int i = 0; i < parameters.SummonCount; i++)
                        {
                            var token = _cardFactory.CreateAndRegisterNewCard(
                                ctx.CurrentGameState,
                                cardDefinitionId,
                                player.PlayerId,
                                CardZone.Staging // Initial zone before being placed on field
                            );
                            tokensToSummon.Add(token);
                        }
                    }

                    var combinedResult = new DomainOperationResult { Success = true };
                    var packedCount = 0;
                    var remainingSlots = availableSlots;
                    foreach (var token in tokensToSummon)
                    {
                        // 支持位置参数：从参数传递 Position（ZoneIndex），null 表示追加到末尾
                        if (remainingSlots > 0)
                        {
                            var expectedPosition = NextExpectedPosition(player);
                            var evt = new SummonEvent(player, token, expectedPosition);
                            combinedResult.GeneratedEvents.Add(evt);
                            remainingSlots -= 1;
                        }
                        else
                        {
                            var packedResult = CreatePackedResult(player, token);
                            combinedResult.GeneratedEvents.AddRange(packedResult.GeneratedEvents);
                            packedCount += 1;
                        }
                    }
                    if (packedCount > 0)
                    {
                        Logger.LogInformation("{EffectName}: Field is full; packed {PackedCount} summoned card(s) for player {PlayerId}.",
                            effectName, packedCount, player.PlayerId);
                    }
                    return combinedResult;
                };

            var playerTargets = PlayerScopeResolver.ResolvePlayerTargets(
                context,
                parameters.PlayerScope);

            return _targetProcessingService.ProcessTargets(
                context,
                playerTargets,
                summonAction,
                effectName);
        }
    }
}
