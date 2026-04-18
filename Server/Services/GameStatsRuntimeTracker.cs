using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services;

/// <summary>
/// In-memory tracker of draw/play facts by game.
/// </summary>
public sealed class GameStatsRuntimeTracker : IGameStatsRuntimeTracker
{
    private readonly ConcurrentDictionary<string, GameRuntimeState> _games = new();

    public void TrackCommittedEvents(GameState gameState, IReadOnlyList<IGameEvent> processedEvents)
    {
        if (gameState == null || string.IsNullOrWhiteSpace(gameState.GameId))
        {
            return;
        }

        var state = _games.GetOrAdd(gameState.GameId, _ => CreateState(gameState));
        lock (state.SyncRoot)
        {
            if (state.Participants.Count == 0)
            {
                state = CreateState(gameState);
                _games[gameState.GameId] = state;
            }

            foreach (var gameEvent in processedEvents)
            {
                switch (gameEvent)
                {
                    case DrawEvent drawEvent:
                        TrackDraw(state, drawEvent);
                        break;
                    case PlayCardEvent playCardEvent:
                        TrackPlay(state, playCardEvent);
                        break;
                }
            }
        }
    }

    public GameRuntimeStatsSnapshot? GetSnapshot(string gameId)
    {
        if (!_games.TryGetValue(gameId, out var state))
        {
            return null;
        }

        lock (state.SyncRoot)
        {
            var participants = state.Participants.ToDictionary(
                pair => pair.Key,
                pair => new ParticipantRuntimeStatsSnapshot(
                    pair.Value.PlayerId,
                    pair.Value.IsFirstPlayer,
                    pair.Value.DeckClass,
                    pair.Value.Environment,
                    pair.Value.Rule,
                    pair.Value.UserDeckId,
                    pair.Value.DeckShapeHash,
                    new Dictionary<int, int>(pair.Value.InitialDeckCardCounts),
                    pair.Value.DrawnCardDefinitionIds.ToHashSet(),
                    pair.Value.PlayedCardDefinitionIds.ToHashSet()));

            return new GameRuntimeStatsSnapshot(gameId, participants);
        }
    }

    public void Clear(string gameId)
    {
        _games.TryRemove(gameId, out _);
    }

    private static GameRuntimeState CreateState(GameState gameState)
    {
        var participants = new Dictionary<Guid, ParticipantRuntimeState>();
        AddParticipant(participants, gameState.Player1);
        AddParticipant(participants, gameState.Player2);
        return new GameRuntimeState(participants);
    }

    private static void AddParticipant(IDictionary<Guid, ParticipantRuntimeState> participants, PlayerState player)
    {
        if (player == null || player.PlayerId == Guid.Empty)
        {
            return;
        }

        var definitionIds = player.InitialDeckDefinitionIds?.ToList() ?? new List<int>();
        if (definitionIds.Count == 0)
        {
            definitionIds = player.InitialDeck.Select(card => card.BaseDefinitionId).ToList();
        }

        var counts = definitionIds
            .GroupBy(cardId => cardId)
            .ToDictionary(group => group.Key, group => group.Count());

        var initialDefinitionByInstance = new Dictionary<Guid, int>(player.InitialDeck.Count);
        for (int i = 0; i < player.InitialDeck.Count; i++)
        {
            var card = player.InitialDeck[i];
            if (card == null || card.InstanceId == Guid.Empty)
            {
                continue;
            }

            var definitionId = i < definitionIds.Count ? definitionIds[i] : card.BaseDefinitionId;
            initialDefinitionByInstance[card.InstanceId] = definitionId;
        }

        var drawn = new HashSet<int>();
        foreach (var handCard in player.Hand)
        {
            if (handCard == null)
            {
                continue;
            }

            if (initialDefinitionByInstance.TryGetValue(handCard.InstanceId, out var definitionId))
            {
                drawn.Add(definitionId);
            }
        }

        participants[player.PlayerId] = new ParticipantRuntimeState(
            player.PlayerId,
            player.IsFirst,
            player.Class,
            player.Environment,
            player.Rule,
            player.UserDeckId,
            BuildDeckShapeHash(player.Class, counts),
            counts,
            initialDefinitionByInstance,
            drawn,
            new HashSet<int>());
    }

    private static void TrackDraw(GameRuntimeState state, DrawEvent drawEvent)
    {
        var playerId = drawEvent.DrawingPlayer?.PlayerId ?? Guid.Empty;
        if (playerId == Guid.Empty || !state.Participants.TryGetValue(playerId, out var participant))
        {
            return;
        }

        foreach (var card in drawEvent.DrawnCards)
        {
            if (card == null)
            {
                continue;
            }

            if (participant.InitialDefinitionByInstance.TryGetValue(card.InstanceId, out var definitionId))
            {
                participant.DrawnCardDefinitionIds.Add(definitionId);
            }
        }
    }

    private static void TrackPlay(GameRuntimeState state, PlayCardEvent playCardEvent)
    {
        var playerId = playCardEvent.PlayingPlayer?.PlayerId ?? Guid.Empty;
        if (playerId == Guid.Empty || !state.Participants.TryGetValue(playerId, out var participant))
        {
            return;
        }

        var playedCard = playCardEvent.PlayedCard;
        if (playedCard == null)
        {
            return;
        }

        if (participant.InitialDefinitionByInstance.TryGetValue(playedCard.InstanceId, out var definitionId))
        {
            participant.PlayedCardDefinitionIds.Add(definitionId);
        }
    }

    private static string BuildDeckShapeHash(CardClass deckClass, IReadOnlyDictionary<int, int> deckCounts)
    {
        var payload = string.Join(",",
            deckCounts
                .OrderBy(pair => pair.Key)
                .Select(pair => $"{pair.Key}:{pair.Value}"));

        var raw = $"{deckClass}|{payload}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }

    private sealed class GameRuntimeState
    {
        public GameRuntimeState(Dictionary<Guid, ParticipantRuntimeState> participants)
        {
            Participants = participants;
        }

        public object SyncRoot { get; } = new();
        public Dictionary<Guid, ParticipantRuntimeState> Participants { get; }
    }

    private sealed class ParticipantRuntimeState
    {
        public ParticipantRuntimeState(
            Guid playerId,
            bool isFirstPlayer,
            CardClass deckClass,
            DeckEnvironment environment,
            DeckRule rule,
            Guid? userDeckId,
            string deckShapeHash,
            Dictionary<int, int> initialDeckCardCounts,
            Dictionary<Guid, int> initialDefinitionByInstance,
            HashSet<int> drawnCardDefinitionIds,
            HashSet<int> playedCardDefinitionIds)
        {
            PlayerId = playerId;
            IsFirstPlayer = isFirstPlayer;
            DeckClass = deckClass;
            Environment = environment;
            Rule = rule;
            UserDeckId = userDeckId;
            DeckShapeHash = deckShapeHash;
            InitialDeckCardCounts = initialDeckCardCounts;
            InitialDefinitionByInstance = initialDefinitionByInstance;
            DrawnCardDefinitionIds = drawnCardDefinitionIds;
            PlayedCardDefinitionIds = playedCardDefinitionIds;
        }

        public Guid PlayerId { get; }
        public bool IsFirstPlayer { get; }
        public CardClass DeckClass { get; }
        public DeckEnvironment Environment { get; }
        public DeckRule Rule { get; }
        public Guid? UserDeckId { get; }
        public string DeckShapeHash { get; }
        public Dictionary<int, int> InitialDeckCardCounts { get; }
        public Dictionary<Guid, int> InitialDefinitionByInstance { get; }
        public HashSet<int> DrawnCardDefinitionIds { get; }
        public HashSet<int> PlayedCardDefinitionIds { get; }
    }
}



