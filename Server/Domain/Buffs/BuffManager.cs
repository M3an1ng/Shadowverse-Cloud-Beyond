using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Common; // For DurationType
using ShadowverseBeyond.Server.Domain.Abilities; // For Ability class
using ShadowverseBeyond.Server.Domain.Events; // For IEventProcessor
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Domain.Abilities.Conditions; // For ConditionConfig
using ShadowverseBeyond.Server.Domain.Abilities.Targets; // For TargetConfig
using ShadowverseBeyond.Server.Domain.Abilities.Effects; // For DomainOperationResult
using ShadowverseBeyond.Server.Domain.Identifiers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Domain.Buffs
{
    /// <summary>
    /// Manages Buff instances within a game session.
    /// Stores buffs associated with card instances and provides methods for registration, removal, and querying.
    /// Note: This is a basic in-memory implementation. For persistence across sessions or complex scenarios,
    /// a more robust storage mechanism might be needed.
    /// </summary>
    public class BuffManager : IBuffManager
    {
        // Internal storage: Dictionary mapping CardInstanceId to a list of Buffs applied to it.
        // The List preserves the order of application.
        private readonly Dictionary<Guid, List<Buff>> _buffsByCardInstanceId = new();
        // Reverse lookup for performance: BuffId -> CardInstanceId
        private readonly Dictionary<Guid, Guid> _cardIdByBuffId = new();
        private readonly ILogger<BuffManager> _logger;
        private readonly IAbilityManager _abilityManager;

        public BuffManager(ILogger<BuffManager> logger, IAbilityManager abilityManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _abilityManager = abilityManager ?? throw new ArgumentNullException(nameof(abilityManager));
        }


        /// <inheritdoc />
        public List<Buff> GetBuffsForCard(Guid cardInstanceId)
        {
            if (cardInstanceId == Guid.Empty)
            {
                return new List<Buff>(); // Return empty list for invalid ID
            }

            if (_buffsByCardInstanceId.TryGetValue(cardInstanceId, out var buffList))
            {
                // Return a copy to prevent external modification of the internal list
                return buffList.ToList();
            }

            return new List<Buff>(); // No buffs found for this card
        }

        /// <inheritdoc />
        public Buff? FindBuffInstance(Guid buffId)
        {
            if (!_cardIdByBuffId.TryGetValue(buffId, out var cardId))
            {
                return null; // Buff doesn't exist.
            }

            if (_buffsByCardInstanceId.TryGetValue(cardId, out var buffList))
            {
                // Find the specific buff in the card's list.
                return buffList.FirstOrDefault(b => b.Id == buffId);
            }

            // This would indicate an inconsistent state.
            _logger.LogError("Inconsistent state: Buff {BuffId} has a lookup entry but its card {CardId} has no buff list.", buffId, cardId);
            return null;
        }

        // All old methods are removed from here.

        /// <summary>
        /// Core method: Registers a buff instance to the internal dictionary.
        /// This method does not handle stacking, events, or ability interactions.
        /// </summary>
        /// <param name="buff">The buff instance to add.</param>
        public void RegisterBuffInstance(Buff buff, GameState gameState)
        {
            var cardId = buff.TargetCardInstanceId;
            if (!_buffsByCardInstanceId.TryGetValue(cardId, out var buffList))
            {
                buffList = new List<Buff>();
                _buffsByCardInstanceId[cardId] = buffList;
            }
            buffList.Add(buff);
            _cardIdByBuffId[buff.Id] = cardId;

            gameState.InGameIds.GetOrCreate(InGameIdKind.Buff, buff.Id);

            // Automatically handle the registration of granted abilities.
            HandleAbilityBuffRegistration(buff, gameState);
        }

        /// <summary>
        /// Core method: Removes a buff instance from the internal dictionary.
        /// This method does not handle events or ability interactions.
        /// </summary>
        /// <param name="buffId">The ID of the buff to remove.</param>
        /// <returns>The removed buff instance, or null if not found.</returns>
        public Buff? UnregisterBuffInstance(Guid buffId)
        {
            if (!_cardIdByBuffId.TryGetValue(buffId, out var cardId))
            {
                // Buff does not exist in our lookup, so it's not registered.
                return null;
            }

            if (!_buffsByCardInstanceId.TryGetValue(cardId, out var buffList))
            {
                // This indicates an inconsistent state, which should be logged.
                _logger.LogError("Inconsistent state: Buff {BuffId} has a lookup entry but its card {CardId} has no buff list.", buffId, cardId);
                _cardIdByBuffId.Remove(buffId); // Clean up the inconsistent entry
                return null;
            }

            var buffToRemove = buffList.FirstOrDefault(b => b.Id == buffId);
            if (buffToRemove != null)
            {
                // Automatically handle the unregistration of granted abilities before removing the buff.
                HandleAbilityBuffRemoval(buffToRemove);

                buffList.Remove(buffToRemove);
                _cardIdByBuffId.Remove(buffId);

                if (buffList.Count == 0)
                {
                    _buffsByCardInstanceId.Remove(cardId);
                }
                return buffToRemove;
            }

            // This also indicates an inconsistent state.
            _logger.LogError("Inconsistent state: Buff {BuffId} was in the lookup but not in the card's ({CardId}) buff list.", buffId, cardId);
            _cardIdByBuffId.Remove(buffId); // Clean up
            return null;
        }

        /// <summary>
        /// 处理AbilityBuffEffect的注册到事件系统
        /// </summary>
        /// <param name="buff">包含AbilityBuffEffect的Buff</param>
        private void HandleAbilityBuffRegistration(Buff buff, GameState gameState)
        {
            try
            {
                var abilityEffects = buff.Effects.OfType<AbilityBuffEffect>().ToList();
                if (!abilityEffects.Any())
                    return;

                // The dependency is now the minimal required: GameState.
                var targetCardInstance = gameState.FindCardInstance(buff.TargetCardInstanceId);
                if (targetCardInstance == null)
                {
                    _logger.LogWarning("Cannot register temporary abilities for Buff {BuffId}: CardInstance {CardId} not found in the provided game state.",
                        buff.Id, buff.TargetCardInstanceId);
                    return;
                }

                foreach (var abilityEffect in abilityEffects)
                {
                    // The new, cleaner approach:
                    // Tell the AbilityManager to create and register all abilities from the configs,
                    // associating them with the source buff's ID.
                    var created = _abilityManager.CreateAbilitiesForCard(
                        targetCardInstance,
                        abilityEffect.GrantedAbilityConfigs,
                        buff.Id);

                    foreach (var ability in created)
                    {
                        gameState.InGameIds.GetOrCreate(InGameIdKind.Ability, ability.RuntimeId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling AbilityBuffEffect registration for Buff {BuffId}", buff.Id);
            }
        }



        /// <summary>
        /// 处理AbilityBuffEffect从事件系统的移除
        /// </summary>
        /// <param name="buff">包含AbilityBuffEffect的Buff</param>
        private void HandleAbilityBuffRemoval(Buff buff)
        {
            try
            {
                // The logic is now vastly simplified and no longer needs the service locator.
                // We just tell the injected AbilityManager to unregister all abilities
                // associated with this buff's ID.
                _abilityManager.UnregisterAbilitiesFromBuff(buff.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling AbilityBuffEffect removal for Buff {BuffId}", buff.Id);
            }
        }



    }
}
