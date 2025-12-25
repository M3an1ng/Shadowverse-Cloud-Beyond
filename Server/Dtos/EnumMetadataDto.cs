namespace ShadowverseBeyond.Server.Dtos
{
    /// <summary>
    /// 枚举元数据DTO
    /// 用于传递枚举值的显示信息
    /// </summary>
    public class EnumMetadataDto
    {
        /// <summary>
        /// 枚举值（字符串形式）
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// 显示名称（通常来自Description特性）
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 详细描述
        /// </summary>
        public string Description { get; set; } = string.Empty;
    }
}
