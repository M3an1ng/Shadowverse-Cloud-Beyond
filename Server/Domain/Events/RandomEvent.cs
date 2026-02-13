using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ShadowverseBeyond.Server.Domain.Events
{
    [GameEventMetadata("随机", "当随机效果完成选择时触发的事件。", EventType.Random, AbilityEditorCategories.SpecialId, AbilityEditorCategories.SpecialLabel)]
    public class RandomEvent : GameEventBase, ICommittableEvent
    {
        private List<Guid> _optionIds;
        private List<int> _selectedOptionIndexes = new();

        [GameEventParameter("选择数量", "配置要求的选择数量。", Scope = GameEventParameterScope.Input)]
        public int SelectionCount { get; set; }

        [GameEventParameter("允许重复", "是否允许重复选择。", Scope = GameEventParameterScope.Input)]
        public bool AllowRepeat { get; set; }

        [GameEventParameter("权重列表", "随机选择权重列表。", Scope = GameEventParameterScope.Input)]
        public List<double> Weights { get; set; } = new();

        [GameEventParameter("选项ID列表", "随机可选项ID列表。", Scope = GameEventParameterScope.Input)]
        public IReadOnlyList<Guid> OptionIds => _optionIds;

        [GameEventParameter("来源卡牌", "发动随机的卡牌。", DslDisplayTypeName = "事件源")]
        public CardInstance? SourceCard => Source?.Card;

        [GameEventParameter("选择结果索引", "最终选择的选项序号列表。", Scope = GameEventParameterScope.Output)]
        public IReadOnlyList<int> SelectedOptionIds => _selectedOptionIndexes;


        public RandomEvent(
            IEnumerable<Guid> optionIds,
            int selectionCount,
            bool allowRepeat,
            IReadOnlyList<double> weights,
            CardInstance? sourceCard = null,
            Guid? eventId = null,
            DateTime? timestamp = null)
            : base(EventType.Random, eventId, timestamp)
        {
            SelectionCount = selectionCount;
            AllowRepeat = allowRepeat;
            Weights = weights?.ToList() ?? new List<double>();
            _optionIds = optionIds?.ToList() ?? new List<Guid>();

            if (sourceCard != null)
            {
                Source = TargetInfo.ForCard(sourceCard);
            }
        }

        public DomainOperationResult Commit(GameContext context)
        {
            return new DomainOperationResult { Success = true };
        }

        internal void SetSelectedOptionIndexes(IEnumerable<int> indexes)
        {
            _selectedOptionIndexes = indexes?
                .OrderBy(index => index)
                .ToList() ?? new List<int>();
        }
    }
}
