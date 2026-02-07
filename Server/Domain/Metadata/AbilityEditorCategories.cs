namespace ShadowverseBeyond.Server.Domain.Metadata
{
    public static class AbilityEditorCategories
    {
        public const string BaseActionId = "base-action";
        public const string BaseActionLabel = "基础行动";

        public const string PositionChangeId = "position-change";
        public const string PositionChangeLabel = "位置移动";

        public const string TempStateId = "temp-state";
        public const string TempStateLabel = "状态变化";

        public const string ResourceValueId = "resource-value";
        public const string ResourceValueLabel = "资源数值";

        public const string TurnFlowId = "turn-flow";
        public const string TurnFlowLabel = "回合流程";

        public const string SpecialId = "special";
        public const string SpecialLabel = "特殊事件";

        public const string SystemRequestId = "system-request";
        public const string SystemRequestLabel = "系统请求";

        public const string UncategorizedId = "uncategorized";
        public const string UncategorizedLabel = "未分类";

        public static string GetLabelOrDefault(string? categoryId, string? fallbackLabel = null)
        {
            if (!string.IsNullOrWhiteSpace(fallbackLabel))
            {
                return fallbackLabel;
            }

            return categoryId switch
            {
                BaseActionId => BaseActionLabel,
                PositionChangeId => PositionChangeLabel,
                TempStateId => TempStateLabel,
                ResourceValueId => ResourceValueLabel,
                TurnFlowId => TurnFlowLabel,
                SpecialId => SpecialLabel,
                SystemRequestId => SystemRequestLabel,
                UncategorizedId => UncategorizedLabel,
                _ => UncategorizedLabel
            };
        }

        public static string NormalizeId(string? categoryId)
        {
            return categoryId switch
            {
                BaseActionId => BaseActionId,
                PositionChangeId => PositionChangeId,
                TempStateId => TempStateId,
                ResourceValueId => ResourceValueId,
                TurnFlowId => TurnFlowId,
                SpecialId => SpecialId,
                SystemRequestId => SystemRequestId,
                UncategorizedId => UncategorizedId,
                _ => UncategorizedId
            };
        }
    }
}
