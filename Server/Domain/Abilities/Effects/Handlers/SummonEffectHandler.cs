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
            Logger.LogDebug("{EffectName}: Starting with CardDefinitionId={CardDefId}, Count={Count}.",
                           effectName, parameters.CardDefinitionId, parameters.SummonCount);

            Func<TargetInfo, GameContext, DomainOperationResult> summonAction =
                (targetInfo, ctx) =>
                {
                    if (targetInfo.Type != Common.TargetType.Player || targetInfo.Player == null)
                    {
                        Logger.LogWarning("{EffectName}: Target is not a player or player data is missing. TargetType: {TargetType}", effectName, targetInfo.Type);
                        return new DomainOperationResult { Success = false, ErrorMessage = "Summon effect can only be applied to players." };
                    }

                    var player = targetInfo.Player;
                    var tokensToSummon = new List<CardInstance>();
                    for (int i = 0; i < parameters.SummonCount; i++)
                    {
                        var token = _cardFactory.CreateAndRegisterNewCard(
                            ctx.CurrentGameState,
                            parameters.CardDefinitionId,
                            player.PlayerId,
                            CardZone.Playing // Initial zone before being placed on field
                        );
                        tokensToSummon.Add(token);
                    }

                    var combinedResult = new DomainOperationResult { Success = true };
                    foreach (var token in tokensToSummon)
                    {
                        // 支持位置参数：从参数传递 Position（ZoneIndex），null 表示追加到末尾
                        var evt = new SummonEvent(player, token, parameters.Position);
                        combinedResult.GeneratedEvents.Add(evt);
                    }
                    return combinedResult;
                };

            return _targetProcessingService.ProcessTargets(
                context,
                effect.SelectedTargets,
                summonAction,
                effectName);
        }
    }
}