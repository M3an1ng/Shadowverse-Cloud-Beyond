namespace ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata
{
    /// <summary>
    /// 定义了卡牌编辑器中可用于配置效果参数的UI控件类型。
    /// </summary>
    public enum UiControlType
    {
        /// <summary>
        /// 默认文本输入框。
        /// </summary>
        TextInput,

        /// <summary>
        /// 数字步进器，用于输入整数或小数。
        /// </summary>
        NumberStepper,

        /// <summary>
        /// 复选框，用于布尔值。
        /// </summary>
        Checkbox,

        /// <summary>
        /// 下拉选择框，用于从预定义列表中选择。
        /// </summary>
        Dropdown,

        /// <summary>
        /// 文本区域，用于多行文本输入。
        /// </summary>
        TextArea,

        /// <summary>
        /// 一个复合控件，用于定义一个目标选择器（TargetSelector）。
        /// </summary>
        TargetSelector,

        /// <summary>
        /// 一个复合控件，用于配置一个 `ValueConfig` 对象，允许选择固定值或DSL公式。
        /// </summary>
        ValueConfigEditor
    }
}