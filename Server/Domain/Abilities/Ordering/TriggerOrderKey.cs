using System;

namespace ShadowverseBeyond.Server.Domain.Abilities.Ordering
{
    public readonly record struct TriggerOrderKey(
        int EventRolePriority,
        int ZonePriority,
        int ZonePosition,
        long RegistrationOrder)
    {
        public override string ToString()
        {
            return $"role={EventRolePriority},zone={ZonePriority},pos={ZonePosition},reg={RegistrationOrder}";
        }
    }

    public readonly record struct OrderedTriggeredAbility(
        Common.IAbility Ability,
        TriggerOrderKey Key);
}
