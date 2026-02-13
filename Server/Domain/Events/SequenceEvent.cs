using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ShadowverseBeyond.Server.Domain.Events
{
    [GameEventMetadata("依序", "当依序效果完成选择时触发的事件。", EventType.Sequence, AbilityEditorCategories.SpecialId, AbilityEditorCategories.SpecialLabel)]
    public class SequenceEvent : GameEventBase, ICommittableEvent
    {
        private List<Guid> _optionIds;
        private List<int> _selectedOptionIndexes = new();

        [GameEventParameter("选择数量", "配置要求的选择数量。", Scope = GameEventParameterScope.Input)]
        public int SelectionCount { get; set; }

        [GameEventParameter("可循环", "是否在末尾回绕继续选取。", Scope = GameEventParameterScope.Input)]
        public bool LoopSelection { get; set; }

        [GameEventParameter("执行顺序", "依序或逆序。", Scope = GameEventParameterScope.Input)]
        public SequenceOrder SelectionOrder { get; set; }

        [GameEventParameter("选项ID列表", "依序可选项ID列表。", Scope = GameEventParameterScope.Input)]
        public IReadOnlyList<Guid> OptionIds => _optionIds;

        [GameEventParameter("来源卡牌", "发动依序的卡牌。", DslDisplayTypeName = "事件源")]
        public CardInstance? SourceCard => Source?.Card;

        [GameEventParameter("选择结果索引", "最终选择的选项序号列表。", Scope = GameEventParameterScope.Output)]
        public IReadOnlyList<int> SelectedOptionIds => _selectedOptionIndexes;

        public SequenceEvent(
            IEnumerable<Guid> optionIds,
            int selectionCount,
            bool loopSelection,
            SequenceOrder selectionOrder,
            CardInstance? sourceCard = null,
            Guid? eventId = null,
            DateTime? timestamp = null)
            : base(EventType.Sequence, eventId, timestamp)
        {
            SelectionCount = selectionCount;
            LoopSelection = loopSelection;
            SelectionOrder = selectionOrder;
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
            _selectedOptionIndexes = indexes?.ToList() ?? new List<int>();
        }
    }
}
