using System;
using System;
using System.Linq;
using Xunit;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Effects;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Tests.Scenarios
{
    public sealed class ExpectCardZone : IScenarioExpectation
    {
        private readonly string _cardKey;
        private readonly CardZone _expectedZone;

        public ExpectCardZone(string cardKey, CardZone expectedZone)
        {
            _cardKey = cardKey;
            _expectedZone = expectedZone;
        }

        public void Assert(ScenarioContext context)
        {
            var card = context.GetCard(_cardKey);
            Xunit.Assert.Equal(_expectedZone, card.CurrentZone);
        }
    }

    public sealed class ExpectCardEvolved : IScenarioExpectation
    {
        private readonly string _cardKey;
        private readonly bool _expected;

        public ExpectCardEvolved(string cardKey, bool expected)
        {
            _cardKey = cardKey;
            _expected = expected;
        }

        public void Assert(ScenarioContext context)
        {
            var card = context.GetCard(_cardKey);
            Xunit.Assert.Equal(_expected, card.IsEvolved);
        }
    }

    public sealed class ExpectCardStats : IScenarioExpectation
    {
        private readonly string _cardKey;
        private readonly int? _attack;
        private readonly int? _maxHealth;
        private readonly int? _currentHealth;
        private readonly int? _cost;

        public ExpectCardStats(string cardKey, int? attack = null, int? maxHealth = null, int? currentHealth = null, int? cost = null)
        {
            _cardKey = cardKey;
            _attack = attack;
            _maxHealth = maxHealth;
            _currentHealth = currentHealth;
            _cost = cost;
        }

        public void Assert(ScenarioContext context)
        {
            var card = context.GetCard(_cardKey);

            if (_attack.HasValue)
            {
                Xunit.Assert.Equal(_attack.Value, card.CurrentAttack);
            }

            if (_maxHealth.HasValue)
            {
                Xunit.Assert.Equal(_maxHealth.Value, card.MaxHealth);
            }

            if (_currentHealth.HasValue)
            {
                Xunit.Assert.Equal(_currentHealth.Value, card.CurrentHealth);
            }

            if (_cost.HasValue)
            {
                Xunit.Assert.Equal(_cost.Value, card.CurrentCost);
            }
        }
    }

    public sealed class ExpectCardKeyword : IScenarioExpectation
    {
        private readonly string _cardKey;
        private readonly CardKeyword _keyword;
        private readonly bool _shouldHave;

        public ExpectCardKeyword(string cardKey, CardKeyword keyword, bool shouldHave = true)
        {
            _cardKey = cardKey;
            _keyword = keyword;
            _shouldHave = shouldHave;
        }

        public void Assert(ScenarioContext context)
        {
            var card = context.GetCard(_cardKey);
            var hasKeyword = card.HasKeyword(_keyword);
            Xunit.Assert.Equal(_shouldHave, hasKeyword);
        }
    }

    public sealed class ExpectCardCountdown : IScenarioExpectation
    {
        private readonly string _cardKey;
        private readonly int? _expectedCountdown;

        public ExpectCardCountdown(string cardKey, int? expectedCountdown)
        {
            _cardKey = cardKey;
            _expectedCountdown = expectedCountdown;
        }

        public void Assert(ScenarioContext context)
        {
            var card = context.GetCard(_cardKey);
            Xunit.Assert.Equal(_expectedCountdown, card.Countdown);
        }
    }

    public sealed class ExpectPlayerHealth : IScenarioExpectation
    {
        private readonly ScenarioPlayer _player;
        private readonly int _expectedHealth;

        public ExpectPlayerHealth(ScenarioPlayer player, int expectedHealth)
        {
            _player = player;
            _expectedHealth = expectedHealth;
        }

        public void Assert(ScenarioContext context)
        {
            var player = context.GetPlayer(_player);
            Xunit.Assert.Equal(_expectedHealth, player.CurrentHealth);
        }
    }

    public sealed class ExpectPlayerZoneCount : IScenarioExpectation
    {
        private readonly ScenarioPlayer _player;
        private readonly CardZone _zone;
        private readonly int _expectedCount;

        public ExpectPlayerZoneCount(ScenarioPlayer player, CardZone zone, int expectedCount)
        {
            _player = player;
            _zone = zone;
            _expectedCount = expectedCount;
        }

        public void Assert(ScenarioContext context)
        {
            var count = context.CountCardsInZone(_player, _zone);
            Xunit.Assert.Equal(_expectedCount, count);
        }
    }

    public sealed class ExpectPlayerZoneCardIdCount : IScenarioExpectation
    {
        private readonly ScenarioPlayer _player;
        private readonly CardZone _zone;
        private readonly int _cardId;
        private readonly int _expectedCount;

        public ExpectPlayerZoneCardIdCount(ScenarioPlayer player, CardZone zone, int cardId, int expectedCount)
        {
            _player = player;
            _zone = zone;
            _cardId = cardId;
            _expectedCount = expectedCount;
        }

        public void Assert(ScenarioContext context)
        {
            var count = context.FindCardsById(_cardId, _player, _zone).Count();
            Xunit.Assert.Equal(_expectedCount, count);
        }
    }

    public sealed class ExpectPlayerZoneCardIdCost : IScenarioExpectation
    {
        private readonly ScenarioPlayer _player;
        private readonly CardZone _zone;
        private readonly int _cardId;
        private readonly int _expectedCost;
        private readonly bool _requireAll;

        public ExpectPlayerZoneCardIdCost(ScenarioPlayer player, CardZone zone, int cardId, int expectedCost, bool requireAll = false)
        {
            _player = player;
            _zone = zone;
            _cardId = cardId;
            _expectedCost = expectedCost;
            _requireAll = requireAll;
        }

        public void Assert(ScenarioContext context)
        {
            var cards = context.FindCardsById(_cardId, _player, _zone).ToList();
            Xunit.Assert.NotEmpty(cards);

            if (_requireAll)
            {
                Xunit.Assert.All(cards, card => Xunit.Assert.Equal(_expectedCost, card.CurrentCost));
                return;
            }

            var anyMatch = cards.Any(card => card.CurrentCost == _expectedCost);
            Xunit.Assert.True(anyMatch, $"Expected at least one card {_cardId} in {_zone} to have cost {_expectedCost}.");
        }
    }

    public sealed class ExpectCardAbilityEffect : IScenarioExpectation
    {
        private readonly string _cardKey;
        private readonly EventType? _triggerEvent;
        private readonly EffectType _effectType;
        private readonly int _minimumCount;

        public ExpectCardAbilityEffect(string cardKey, EventType? triggerEvent, EffectType effectType, int minimumCount = 1)
        {
            _cardKey = cardKey;
            _triggerEvent = triggerEvent;
            _effectType = effectType;
            _minimumCount = minimumCount;
        }

        public void Assert(ScenarioContext context)
        {
            var card = context.GetCard(_cardKey);
            var count = card.Abilities.Count(ability =>
                (_triggerEvent == null || ability.TriggerConfig.EventTypes.Contains(_triggerEvent.Value))
                && ability.Config.Effects.Any(effect => effect.Type == _effectType));

            Xunit.Assert.True(count >= _minimumCount, $"Expected card '{_cardKey}' to have at least {_minimumCount} abilities with effect {_effectType}.");
        }
    }

    public sealed class ExpectZoneCardAbilityEffect : IScenarioExpectation
    {
        private readonly ScenarioPlayer _player;
        private readonly CardZone _zone;
        private readonly int _cardId;
        private readonly EffectType _effectType;
        private readonly EventType? _triggerEvent;
        private readonly int _minimumCount;
        private readonly bool _requireAll;

        public ExpectZoneCardAbilityEffect(
            ScenarioPlayer player,
            CardZone zone,
            int cardId,
            EffectType effectType,
            EventType? triggerEvent = null,
            int minimumCount = 1,
            bool requireAll = false)
        {
            _player = player;
            _zone = zone;
            _cardId = cardId;
            _effectType = effectType;
            _triggerEvent = triggerEvent;
            _minimumCount = minimumCount;
            _requireAll = requireAll;
        }

        public void Assert(ScenarioContext context)
        {
            var cards = context.FindCardsById(_cardId, _player, _zone).ToList();
            Xunit.Assert.NotEmpty(cards);

            bool HasAbility(CardInstance card)
            {
                return card.Abilities.Any(ability =>
                    (_triggerEvent == null || ability.TriggerConfig.EventTypes.Contains(_triggerEvent.Value))
                    && ability.Config.Effects.Any(effect => effect.Type == _effectType));
            }

            if (_requireAll)
            {
                Xunit.Assert.All(cards, card => Xunit.Assert.True(HasAbility(card), $"Expected card {_cardId} in {_zone} to have ability with effect {_effectType}."));
                return;
            }

            var count = cards.Count(HasAbility);
            Xunit.Assert.True(count >= _minimumCount, $"Expected at least {_minimumCount} cards {_cardId} in {_zone} to have effect {_effectType}.");
        }
    }

    public sealed class ExpectZoneCardKeyword : IScenarioExpectation
    {
        private readonly ScenarioPlayer _player;
        private readonly CardZone _zone;
        private readonly int _cardId;
        private readonly CardKeyword _keyword;
        private readonly int _minimumCount;
        private readonly bool _requireAll;

        public ExpectZoneCardKeyword(
            ScenarioPlayer player,
            CardZone zone,
            int cardId,
            CardKeyword keyword,
            int minimumCount = 1,
            bool requireAll = false)
        {
            _player = player;
            _zone = zone;
            _cardId = cardId;
            _keyword = keyword;
            _minimumCount = minimumCount;
            _requireAll = requireAll;
        }

        public void Assert(ScenarioContext context)
        {
            var cards = context.FindCardsById(_cardId, _player, _zone).ToList();
            Xunit.Assert.NotEmpty(cards);

            bool HasKeyword(CardInstance card) => card.HasKeyword(_keyword);

            if (_requireAll)
            {
                Xunit.Assert.All(cards, card => Xunit.Assert.True(HasKeyword(card), $"Expected card {_cardId} in {_zone} to have keyword {_keyword}."));
                return;
            }

            var count = cards.Count(HasKeyword);
            Xunit.Assert.True(count >= _minimumCount, $"Expected at least {_minimumCount} cards {_cardId} in {_zone} to have keyword {_keyword}.");
        }
    }

    public sealed class ExpectPlayerZoneContainsCard : IScenarioExpectation
    {
        private readonly ScenarioPlayer _player;
        private readonly CardZone _zone;
        private readonly int _cardId;

        public ExpectPlayerZoneContainsCard(ScenarioPlayer player, CardZone zone, int cardId)
        {
            _player = player;
            _zone = zone;
            _cardId = cardId;
        }

        public void Assert(ScenarioContext context)
        {
            var found = context.FindCardsById(_cardId, _player, _zone).Any();
            Xunit.Assert.True(found);
        }
    }

    public sealed class ExpectEventTypeCount : IScenarioExpectation
    {
        private readonly EventType _eventType;
        private readonly int _expectedCount;

        public ExpectEventTypeCount(EventType eventType, int expectedCount)
        {
            _eventType = eventType;
            _expectedCount = expectedCount;
        }

        public void Assert(ScenarioContext context)
        {
            var count = context.ProcessedEvents.Count(evt => evt.EventType == _eventType);
            Xunit.Assert.Equal(_expectedCount, count);
        }
    }

    public sealed class ExpectEventTypeOccurred : IScenarioExpectation
    {
        private readonly EventType _eventType;
        private readonly int _minimumCount;

        public ExpectEventTypeOccurred(EventType eventType, int minimumCount = 1)
        {
            _eventType = eventType;
            _minimumCount = minimumCount;
        }

        public void Assert(ScenarioContext context)
        {
            var count = context.ProcessedEvents.Count(evt => evt.EventType == _eventType);
            Xunit.Assert.True(count >= _minimumCount, $"Expected {_eventType} at least {_minimumCount} times, but saw {count}.");
        }
    }

    public sealed class LogKeywordRemovedEvents : IScenarioExpectation
    {
        private readonly string? _cardKey;
        private readonly string _label;

        public LogKeywordRemovedEvents(string? cardKey = null, string? label = null)
        {
            _cardKey = string.IsNullOrWhiteSpace(cardKey) ? null : cardKey;
            _label = string.IsNullOrWhiteSpace(label) ? "keyword-removed" : label!;
        }

        public void Assert(ScenarioContext context)
        {
            Guid? filterId = null;
            if (_cardKey != null)
            {
                filterId = context.GetCard(_cardKey).InstanceId;
            }

            foreach (var evt in context.ProcessedEvents.OfType<KeywordRemovedEvent>())
            {
                var targetId = evt.TargetCard?.InstanceId;
                if (filterId.HasValue && targetId != filterId.Value)
                {
                    continue;
                }

                Console.WriteLine(
                    "[ScenarioLog] {0} target={1} keyword={2} effectKey={3} canStack={4}",
                    _label,
                    targetId,
                    evt.Keyword,
                    evt.EffectKey ?? "<null>",
                    evt.CanStack);
            }
        }
    }

    public sealed class LogCardBuffSummary : IScenarioExpectation
    {
        private readonly string _cardKey;
        private readonly string _label;

        public LogCardBuffSummary(string cardKey, string? label = null)
        {
            _cardKey = cardKey;
            _label = string.IsNullOrWhiteSpace(label) ? cardKey : label!;
        }

        public void Assert(ScenarioContext context)
        {
            var card = context.GetCard(_cardKey);
            var buffManager = context.GameState.BuffManager;
            var buffs = buffManager.GetBuffsForCard(card.InstanceId);
            var dto = context.Mapper.Map<CardInstanceDto>(card);
            var summaryCount = dto.BuffSummaries?.Count ?? 0;
            var statSummary = dto.BuffStatSummary;

            Console.WriteLine(
                "[ScenarioLog] {0} cardId={1} instance={2} zone={3} buffs={4} summaries={5} statSummary={6}",
                _label,
                card.CurrentDefinitionId,
                card.InstanceId,
                card.CurrentZone,
                buffs.Count,
                summaryCount,
                statSummary != null);

            foreach (var buff in buffs)
            {
                var effects = string.Join(",", buff.Effects.Select(effect => effect.GetType().Name));
                Console.WriteLine(
                    "[ScenarioLog]  buff id={0} effectKey={1} sourceAbilityId={2} effects={3}",
                    buff.Id,
                    buff.EffectKey ?? "<null>",
                    buff.SourceAbilityId,
                    effects);
            }

            if (statSummary != null)
            {
                Console.WriteLine(
                    "[ScenarioLog]  statSummary atk={0} hp={1} cost={2}",
                    statSummary.AttackDelta,
                    statSummary.HealthDelta,
                    statSummary.CostDelta);
            }

            if (dto.BuffSummaries == null)
            {
                return;
            }

            foreach (var summary in dto.BuffSummaries)
            {
                Console.WriteLine(
                    "[ScenarioLog]  summary effectKey={0} sourceCard={1} sourceAbility={2}",
                    summary.EffectKey ?? "<null>",
                    summary.SourceCardName ?? "<null>",
                    summary.SourceAbilityName ?? "<null>");
            }
        }
    }
}
