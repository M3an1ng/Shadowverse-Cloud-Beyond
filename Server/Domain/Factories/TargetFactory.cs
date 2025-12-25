using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;

namespace ShadowverseBeyond.Server.Domain.Factories
{
    public class TargetFactory : ITargetFactory
    {
        private readonly TargetSelectionService _targetSelectionService;
        private readonly ILogger<Target> _logger;

        public TargetFactory(TargetSelectionService targetSelectionService, ILogger<Target> logger)
        {
            _targetSelectionService = targetSelectionService;
            _logger = logger;
        }

        public Target Create(string key, TargetConfig config)
        {
            return new Target(key, config, _targetSelectionService, _logger);
        }
    }
}