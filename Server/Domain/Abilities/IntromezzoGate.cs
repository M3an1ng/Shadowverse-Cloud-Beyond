using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
namespace ShadowverseBeyond.Server.Domain.Abilities;
public static class IntromezzoGate
{
    public static PlayerState? Owner(IAbility ability, GameContext context) =>
        ability.Keywords.Contains(AbilityKeyword.Intromezzo) && ability.SourceCard != null
            ? context.CurrentGameState.GetPlayerState(ability.SourceCard.OwnerPlayerId) : null;
    public static bool Enter(IAbility ability, GameContext context) => Owner(ability, context)?.ConsumeIntromezzo() ?? true;
}
