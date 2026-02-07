using System;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata
{
    /// <summary>
    /// 为效果参数类的属性提供UI元数据，定义其在编辑器中的标签、控件类型及其他UI行为。
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class ParameterMetadataAttribute : Attribute
    {
        /// <summary>
        /// 在UI编辑器中显示的参数标签（例如，“伤害值”）。
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// 指示前端应为此参数渲染的UI控件类型。
        /// </summary>
        public UiControlType ControlType { get; }

        /// <summary>
        /// 参数的默认值。
        /// </summary>
        public object? DefaultValue { get; set; }

        /// <summary>
        /// 参数的详细描述或用法说明，可在UI中作为提示（Tooltip）显示。
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 对于数字类型的控件，设置允许的最小值。
        /// </summary>
        public double Min { get; set; } = double.MinValue;

        /// <summary>
        /// 对于数字类型的控件，设置允许的最大值。
        /// </summary>
        public double Max { get; set; } = double.MaxValue;

        /// <summary>
        /// Optional: enum name used to populate dropdown options on the frontend.
        /// When not set and the parameter type is an enum, the backend will infer it automatically.
        /// </summary>
        public string? EnumName { get; set; }

        /// <summary>
        /// Optional: backend option provider key used to populate dropdown options when not enum-backed.
        /// </summary>
        public string? OptionsProviderKey { get; set; }

        /// <summary>
        /// Whether this parameter allows DSL input in the editor.
        /// </summary>
        public bool AllowDsl { get; set; } = true;

        /// <summary>
        /// Optional: override the expected DSL type name used by the editor/type checker.
        /// </summary>
        public string? DslType { get; set; }

        /// <summary>
        /// DSL input availability mode (optional/forced/disabled).
        /// </summary>
        public DslMode DslMode { get; set; } = DslMode.Optional;

        public ParameterMetadataAttribute(string displayName, UiControlType controlType)
        {
            DisplayName = displayName;
            ControlType = controlType;
        }
    }
}
