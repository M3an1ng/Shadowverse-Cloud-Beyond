using ShadowverseBeyond.Server.Domain.Entities;

namespace ShadowverseBeyond.Server.Domain.Abilities
{
    /// <summary>
    /// Factory for creating Ability instances.
    /// This factory abstracts away the complexity of creating abilities,
    /// especially handling scoped dependencies required by abilities.
    /// </summary>
    public interface IAbilityFactory
    {
        /// <summary>
        /// Creates an Ability instance from its configuration.
        /// </summary>
        /// <param name="config">The ability configuration.</param>
        /// <param name="owner">The card instance that owns this ability.</param>
        /// <returns>A new Ability instance.</returns>
        Ability CreateAbility(AbilityConfig config, CardInstance owner, Guid? sourceBuffId = null);

        /// <summary>
        /// Creates an Ability instance from another Ability.   
        /// </summary>
        /// <param name="ability"></param>
        /// <returns></returns>
        Ability CreateAbilityCopy(Ability ability);

        /// <summary>
        /// Creates a list of Ability instances for a card from a list of configurations.
        /// </summary>
        /// <param name="cardInstance">The card instance that will own the abilities.</param>
        /// <param name="abilityConfigs">The list of ability configurations.</param>
        /// <returns>A list of new Ability instances.</returns>
        List<Ability> CreateAbilities(CardInstance cardInstance, IEnumerable<AbilityConfig> abilityConfigs, Guid? sourceBuffId = null);
    }
}