using System;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Abilities.Ordering
{
    public sealed class DefaultTriggerOrderingPolicy : ITriggerOrderingPolicy
    {
        public IReadOnlyList<OrderedTriggeredAbility> OrderTriggeredAbilities(
            IEnumerable<IAbility> triggeredAbilities,
            IGameEvent gameEvent,
            GameContext context,
            IReadOnlyDictionary<Guid, long> registrationOrders)
        {
            if (triggeredAbilities == null)
            {
                return Array.Empty<OrderedTriggeredAbility>();
            }

            var items = triggeredAbilities
                .Select(ability => new OrderedTriggeredAbility(
                    ability,
                    BuildOrderKey(ability, gameEvent, context, registrationOrders)))
                .OrderBy(item => item.Key.EventRolePriority)
                .ThenBy(item => item.Key.ZonePriority)
                .ThenBy(item => item.Key.ZonePosition)
                .ThenBy(item => item.Key.RegistrationOrder)
                .ToList();

            return items;
        }

        private static TriggerOrderKey BuildOrderKey(
            IAbility ability,
            IGameEvent gameEvent,
            GameContext context,
            IReadOnlyDictionary<Guid, long> registrationOrders)
        {
            var sourceCard = ability.SourceCard;
            var eventRolePriority = ResolveEventRolePriority(sourceCard, gameEvent);
            var zonePriority = ResolveZonePriority(sourceCard, context);
            var zonePosition = ResolveZonePosition(sourceCard, context.CurrentGameState);
            var registrationOrder = registrationOrders != null && registrationOrders.TryGetValue(ability.RuntimeId, out var seq)
                ? seq
                : long.MaxValue;

            return new TriggerOrderKey(
                EventRolePriority: eventRolePriority,
                ZonePriority: zonePriority,
                ZonePosition: zonePosition,
                RegistrationOrder: registrationOrder);
        }

        private static int ResolveEventRolePriority(CardInstance? sourceCard, IGameEvent gameEvent)
        {
            if (sourceCard == null)
            {
                return 2;
            }

            if (gameEvent.Source?.Card?.InstanceId == sourceCard.InstanceId)
            {
                return 0;
            }

            if (gameEvent.Target.Any(target => target?.Card?.InstanceId == sourceCard.InstanceId))
            {
                return 1;
            }

            return 2;
        }

        private static int ResolveZonePriority(CardInstance? sourceCard, GameContext context)
        {
            if (sourceCard == null)
            {
                return int.MaxValue;
            }

            var activePlayerId = context.CurrentGameState.CurrentPlayerId;
            var isMyTurn = activePlayerId.HasValue && sourceCard.OwnerPlayerId == activePlayerId.Value;
            return GameConstants.AbilityTrigger.GetZonePriority(sourceCard.CurrentZone, isMyTurn);
        }

        private static int ResolveZonePosition(CardInstance? sourceCard, GameState gameState)
        {
            if (sourceCard == null)
            {
                return int.MaxValue;
            }

            if (sourceCard.ZoneIndex.HasValue && sourceCard.ZoneIndex.Value >= 0)
            {
                return sourceCard.ZoneIndex.Value;
            }

            var owner = gameState.GetPlayerState(sourceCard.OwnerPlayerId);
            if (owner == null)
            {
                return int.MaxValue;
            }

            var zoneList = ResolveZoneList(owner, sourceCard.CurrentZone);
            if (zoneList == null)
            {
                return int.MaxValue;
            }

            var index = zoneList.IndexOf(sourceCard);
            return index >= 0 ? index : int.MaxValue;
        }

        private static List<CardInstance>? ResolveZoneList(PlayerState player, CardZone zone)
        {
            return zone switch
            {
                CardZone.Hand => player.Hand,
                CardZone.Deck => player.Deck,
                CardZone.Field => player.Field,
                CardZone.Graveyard => player.Graveyard,
                CardZone.Extra => player.Extra,
                CardZone.Emblem => player.Emblem,
                _ => null
            };
        }
    }
}
