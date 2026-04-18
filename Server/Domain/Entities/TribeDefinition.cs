using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShadowverseBeyond.Server.Domain.Entities
{
    public enum TribeBehavior
    {
        Normal = 0,
        MatchAllKnownTribes = 1
    }

    public class TribeDefinition
    {
        [Key]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public List<string> Aliases { get; set; } = new();

        public TribeBehavior Behavior { get; set; } = TribeBehavior.Normal;

        public bool IsReserved { get; set; }

        public bool IsEnabled { get; set; } = true;
    }
}
