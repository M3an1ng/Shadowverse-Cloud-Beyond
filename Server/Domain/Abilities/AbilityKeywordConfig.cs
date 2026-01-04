using ShadowverseBeyond.Server.Domain.Constants;

namespace ShadowverseBeyond.Server.Domain.Abilities
{
    public class AbilityKeywordConfig
    {
        public EffectKeyword Keyword { get; set; } = EffectKeyword.None;
        public int? Value { get; set; }

        public override string ToString()
        {
            return Value.HasValue ? $"{Keyword}:{Value.Value}" : Keyword.ToString();
        }
    }
}
