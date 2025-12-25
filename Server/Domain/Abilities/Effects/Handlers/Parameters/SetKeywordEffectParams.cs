using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;
using ShadowverseBeyond.Server.Domain.Constants;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters
{
    public enum KeywordOperation
    {
        Add = 0,
        Remove = 1
    }

    /// <summary>
    /// Parameters for the SetKeyword effect.
    /// </summary>
    [EffectMetadata("设置关键字", "为目标添加或移除指定的关键字能力。")]
    public class SetKeywordEffectParams : BaseEffectParams
    {
        [ParameterMetadata("操作", UiControlType.Dropdown, DefaultValue = KeywordOperation.Add, Description = "添加或移除关键字。")]
        public KeywordOperation Operation { get; set; } = KeywordOperation.Add;

        [ParameterMetadata("关键字", UiControlType.Dropdown, DefaultValue = CardKeyword.None, Description = "要添加/移除的关键字能力。")]
        public CardKeyword Keyword { get; set; } = CardKeyword.None;
    }
}

