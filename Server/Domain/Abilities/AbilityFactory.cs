using ShadowverseBeyond.Server.Domain.Abilities.Conditions;
using ShadowverseBeyond.Server.Domain.Factories;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using System.Collections.Generic;
using System.Linq;
using System;
using Microsoft.Extensions.Logging;

namespace ShadowverseBeyond.Server.Domain.Abilities
{
    /// <summary>
    /// Default implementation of the <see cref="IAbilityFactory"/>.
    /// </summary>
    public class AbilityFactory : IAbilityFactory
    {
        private readonly ILogger<AbilityFactory> _logger;
        private readonly IConditionFactory _conditionFactory;
        private readonly ITargetFactory _targetFactory;
        private readonly TargetSelectionService _targetSelectionService;

        public AbilityFactory(
            ILogger<AbilityFactory> logger,
            IConditionFactory conditionFactory,
            ITargetFactory targetFactory,
            TargetSelectionService targetSelectionService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _conditionFactory = conditionFactory ?? throw new ArgumentNullException(nameof(conditionFactory));
            _targetFactory = targetFactory ?? throw new ArgumentNullException(nameof(targetFactory));
            _targetSelectionService = targetSelectionService ?? throw new ArgumentNullException(nameof(targetSelectionService));
        }

        public Ability CreateAbility(AbilityConfig config, CardInstance owner, Guid? sourceBuffId = null)
        {
            if (config == null)
            {
                _logger.LogError("AbilityConfig cannot be null.");
                throw new ArgumentNullException(nameof(config));
            }

            if (owner == null)
            {
                _logger.LogError("CardInstance owner cannot be null for ability: {AbilityName}", config.Name);
                throw new ArgumentNullException(nameof(owner));
            }

            _logger.LogDebug("Creating ability '{AbilityName}' for card {CardName} ({CardInstanceId}).", config.Name, owner.Name, owner.InstanceId);

            return new Ability(config, _conditionFactory, _targetFactory, _targetSelectionService, owner, sourceBuffId);
        }

        public Ability CreateAbilityCopy(Ability ability)
        {
            if (ability == null)
            {
                _logger.LogError("Source ability cannot be null for copying.");
                throw new ArgumentNullException(nameof(ability));
            }

            _logger.LogDebug("Creating a copy of ability '{AbilityName}' ({AbilityId}).", ability.Name, ability.RuntimeId);

            return new Ability(ability.Config, _conditionFactory, _targetFactory, _targetSelectionService, ability.SourceCard, ability.SourceBuffId);
        }

        public List<Ability> CreateAbilities(CardInstance cardInstance, IEnumerable<AbilityConfig> abilityConfigs, Guid? sourceBuffId = null)
        {
            if (cardInstance == null)
            {
                _logger.LogError("CardInstance cannot be null when creating abilities.");
                throw new ArgumentNullException(nameof(cardInstance));
            }
            if (abilityConfigs == null)
            {
                _logger.LogError("AbilityConfigs collection cannot be null.");
                throw new ArgumentNullException(nameof(abilityConfigs));
            }

            return abilityConfigs.Select(config => CreateAbility(config, cardInstance, sourceBuffId)).ToList();
        }
    }
}