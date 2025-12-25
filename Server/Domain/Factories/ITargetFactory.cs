using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;

namespace ShadowverseBeyond.Server.Domain.Factories
{
    public interface ITargetFactory
    {
        Target Create(string key, TargetConfig config);
    }
}