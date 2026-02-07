using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Buffs;

namespace ShadowverseBeyond.Server.Domain.Abilities.Auras
{
    public class AuraConfig
    {
        public string Name { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;

        public List<BuffConfig> BuffConfigs { get; set; } = new();

        [global::System.Obsolete("Use BuffConfigs instead. This legacy template will be removed after migration.")]
        public BuffTemplateConfig BuffTemplate { get; set; } = new();
    }
}
