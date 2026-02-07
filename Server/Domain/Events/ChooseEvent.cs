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
    [GameEventMetadata("抉择", "当抉择效果完成选择时触发的事件。", EventType.Choose, AbilityEditorCategories.SpecialId, AbilityEditorCategories.SpecialLabel)]
    public class ChooseEvent : GameEventBase, ICommittableEvent
    {
        private List<Guid> _optionIds;
        private List<int> _selectedOptionIndexes = new();

        [GameEventParameter("选择数量", "配置要求的选择数量。", Scope = GameEventParameterScope.Input)]
        public int SelectionCount { get; set; }

        [GameEventParameter("选项ID列表", "抉择可选项ID列表。", Scope = GameEventParameterScope.Input)]
        public IReadOnlyList<Guid> OptionIds => _optionIds;

        [GameEventParameter("来源卡牌", "发动抉择的卡牌。", DslDisplayTypeName = "事件源")]
        public CardInstance? SourceCard => Source?.Card;

        [GameEventParameter("选择结果索引", "最终选择的选项序号列表。", Scope = GameEventParameterScope.Output)]
        public IReadOnlyList<int> SelectedOptionIds => _selectedOptionIndexes;


        public ChooseEvent(
            IEnumerable<Guid> optionIds,
            int selectionCount,
            CardInstance? sourceCard = null,
            Guid? eventId = null,
            DateTime? timestamp = null)
            : base(EventType.Choose, eventId, timestamp)
        {
            SelectionCount = selectionCount;
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
