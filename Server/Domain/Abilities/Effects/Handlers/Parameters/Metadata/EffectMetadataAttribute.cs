using System;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata
{
    /// <summary>
    /// 为效果参数类提供UI元数据，定义其在编辑器中的显示名称和描述。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class EffectMetadataAttribute : Attribute
    {
        /// <summary>
        /// 在UI编辑器中显示的效果名称（例如，“造成伤害”）。
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// 效果的详细描述，可在UI中作为提示（Tooltip）显示。
        /// </summary>
        public string Description { get; }

        public string? CategoryId { get; }

        public string? CategoryLabel { get; }

        public EffectMetadataAttribute(
            string displayName,
            string description = "",
            string? categoryId = null,
            string? categoryLabel = null)
        {
            DisplayName = displayName;
            Description = description;
            CategoryId = categoryId;
            CategoryLabel = categoryLabel;
        }
    }
}
