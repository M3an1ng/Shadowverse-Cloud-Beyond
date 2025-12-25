using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Domain.Abilities.Common
{
    public interface IValueSource
    {
        object? Resolve(GameContext context);
    }
}