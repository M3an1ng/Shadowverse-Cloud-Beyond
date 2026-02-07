using ShadowverseBeyond.Server.Domain.Constants;

namespace ShadowverseBeyond.Server.Domain.Abilities
{
    public class AbilityKeywordConfig
    {
        public AbilityKeyword Keyword { get; set; } = AbilityKeyword.None;
        public int? Value { get; set; }

        public override string ToString()
        {
            return Value.HasValue ? $"{Keyword}:{Value.Value}" : Keyword.ToString();
        }
    }
}
