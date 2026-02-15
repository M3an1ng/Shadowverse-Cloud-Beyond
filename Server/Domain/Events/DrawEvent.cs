using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Identifiers;

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Event triggered when a player draws one or more cards.
    /// The concrete drawn cards are resolved and backfilled during Commit.
    /// </summary>
    [GameEventMetadata("抽牌", "当玩家抽取一张或多张卡牌时触发的事件。", EventType.Draw)]
    public class DrawEvent : DelayedEventBase, ICommittableEvent, ILogAnnounceableEvent
    {
        [GameEventParameter("抽牌玩家", "执行抽牌的玩家。", Scope = GameEventParameterScope.Input, DslDisplayTypeName = "事件源")]
        public PlayerState? DrawingPlayer => Source?.Player;

        [GameEventParameter("抽牌数量", "本事件请求抽取的卡牌数量。", Scope = GameEventParameterScope.Input)]
        public int Count { get; }

        [GameEventParameter("抽取的卡牌", "被抽取的卡牌列表。", Scope = GameEventParameterScope.Output, DslDisplayTypeName = "事件目标")]
        public List<CardInstance> DrawnCards => Target.Where(t => t.Card != null).Select(t => t.Card!).ToList();

        public object? SearchTargets { get; }

        public Guid? SearchSourceCardInstanceId { get; }

        public DrawEvent(
            PlayerState player,
            int count,
            DelayConfig? delayConfig = null,
            Guid? eventId = null,
            DateTime? timestamp = null)
            : base(EventType.Draw, eventId, timestamp)
        {
            Source = TargetInfo.ForPlayer(player);
            Count = Math.Max(0, count);
            SearchTargets = null;
            SearchSourceCardInstanceId = null;
            ConfigureDelay(delayConfig ?? DelayConfig.Disabled);
        }

        public DrawEvent(
            PlayerState player,
            object searchTargets,
            CardInstance? sourceCard = null,
            DelayConfig? delayConfig = null,
            Guid? eventId = null,
            DateTime? timestamp = null)
            : base(EventType.Draw, eventId, timestamp)
        {
            Source = TargetInfo.ForPlayer(player);
            Count = 0;
            SearchTargets = searchTargets;
            SearchSourceCardInstanceId = sourceCard?.InstanceId;
            ConfigureDelay(delayConfig ?? DelayConfig.Disabled);
        }

        public DomainOperationResult Commit(GameContext context)
        {
            if (DrawingPlayer == null)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "DrawEvent source player is null." };
            }

            var player = DrawingPlayer;
            if (SearchTargets != null)
            {
                var searchContext = BuildSearchContext(context);
                if (!TryResolveSearchTargets(SearchTargets, searchContext, out var cardsToDraw, out var errorMessage))
                {
                    return new DomainOperationResult { Success = false, ErrorMessage = errorMessage ?? "SearchTargets are invalid." };
                }

                var selectedCards = new List<CardInstance>();
                foreach (var card in cardsToDraw)
                {
                    if (card.OwnerPlayerId != player.PlayerId)
                    {
                        continue;
                    }

                    if (!player.Deck.Contains(card))
                    {
                        continue;
                    }

                    if (selectedCards.Any(selected => selected.InstanceId == card.InstanceId))
                    {
                        continue;
                    }

                    selectedCards.Add(card);
                }

                Target.Clear();
                foreach (var card in selectedCards)
                {
                    Target.Add(TargetInfo.ForCard(card));
                }

                if (selectedCards.Count == 0)
                {
                    return new DomainOperationResult { Success = true };
                }

                return player.DrawCards(selectedCards.Select(card => card.InstanceId).ToList());
            }

            if (Count <= 0)
            {
                Target.Clear();
                return new DomainOperationResult { Success = true };
            }

            var selectedCardsByCount = player.Deck.Take(Math.Min(Count, player.Deck.Count)).ToList();
            Target.Clear();
            foreach (var card in selectedCardsByCount)
            {
                Target.Add(TargetInfo.ForCard(card));
            }

            var result = new DomainOperationResult { Success = true };
            if (selectedCardsByCount.Count < Count)
            {
                result.GeneratedEvents.Add(new DeckEmptyOnDrawEvent(player));
            }

            if (selectedCardsByCount.Count == 0)
            {
                return result;
            }

            var drawResult = player.DrawCards(selectedCardsByCount.Select(card => card.InstanceId).ToList());
            if (!drawResult.Success)
            {
                return drawResult;
            }

            result.GeneratedEvents.AddRange(drawResult.GeneratedEvents);
            return result;
        }

        public string GetAnnouncement(GameState gameState)
        {
            var player = DrawingPlayer;
            var count = DrawnCards.Count;
            if (player == null || count <= 0)
            {
                return string.Empty;
            }

            return $"{AnnouncementTokens.Player(player)}抽了 {AnnouncementTokens.Number(count)} 张牌。";
        }

        private GameContext BuildSearchContext(GameContext context)
        {
            if (!SearchSourceCardInstanceId.HasValue)
            {
                return context;
            }

            var sourceCard = context.CurrentGameState.FindCardInstance(SearchSourceCardInstanceId.Value);
            return context with { SourceCard = sourceCard };
        }

        private static bool TryResolveSearchTargets(object? value, GameContext context, out List<CardInstance> cards, out string? errorMessage)
        {
            var resolvedCards = new List<CardInstance>();
            errorMessage = null;

            if (value == null)
            {
                errorMessage = "SearchTargets is null.";
                cards = resolvedCards;
                return false;
            }

            if (value is ValueConfig valueConfig)
            {
                var resolver = context.ServiceProvider.GetRequiredService<IValueSourceResolver>();
                value = valueConfig.Resolve(context, resolver);
            }

            var hadUnsupported = false;

            void AddCard(CardInstance? card)
            {
                if (card != null)
                {
                    resolvedCards.Add(card);
                }
            }

            void HandleObject(object? obj)
            {
                switch (obj)
                {
                    case null:
                        return;
                    case CardInstance card:
                        AddCard(card);
                        return;
                    case TargetInfo target when target.Card != null:
                        AddCard(target.Card);
                        return;
                    case Guid guid:
                        AddCard(context.CurrentGameState.FindCardInstance(guid));
                        return;
                    case string str when Guid.TryParse(str, out var parsedGuid):
                        AddCard(context.CurrentGameState.FindCardInstance(parsedGuid));
                        return;
                    case JsonElement jsonElement:
                        if (jsonElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in jsonElement.EnumerateArray())
                            {
                                HandleObject(item);
                            }
                            return;
                        }
                        if (jsonElement.ValueKind == JsonValueKind.String && Guid.TryParse(jsonElement.GetString(), out var jsonGuid))
                        {
                            AddCard(context.CurrentGameState.FindCardInstance(jsonGuid));
                            return;
                        }
                        hadUnsupported = true;
                        return;
                    case IEnumerable enumerable when obj is not string:
                        foreach (var item in enumerable)
                        {
                            HandleObject(item);
                        }
                        return;
                    default:
                        hadUnsupported = true;
                        return;
                }
            }

            HandleObject(value);

            if (resolvedCards.Count == 0 && hadUnsupported)
            {
                errorMessage = "SearchTargets contains unsupported values.";
                cards = resolvedCards;
                return false;
            }

            cards = resolvedCards;
            return true;
        }

    }
}
