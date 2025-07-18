// File: Server/Domain/Entities/PlayerState.cs
using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.GameFlow.EffectQueue;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Interfaces;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Exceptions;
using ShadowverseBeyond.Server.Domain.Abilities.Effects;

namespace ShadowverseBeyond.Server.Domain.Entities
{
    public class PlayerState
    {
        public Guid PlayerId { get; set; }
        public int MaxPlayPoints { get; set; } = 0;
        public int CurrentPlayPoints { get; set; } = 0;
        public int MaxEvolutionPoints { get; set; } = 0;
        public int CurrentEvolutionPoints { get; set; } = 0;
        public bool IsAI { get; set; } = false;
        public bool IsFirst { get; set; } = false;
        public bool HasCompletedMulligan { get; set; } = false;
        public int TurnNumber { get; set; } = 0;
        public int PlayedCardCountThisTurn { get; set; } = 0;
        public bool HasTakenFirstTurn { get; set; } = false;
        public int CurrentHealth => Leader?.CurrentHealth ?? 0;
        public int MaxHealth => Leader?.MaxHealth ?? 0;
        public CardInstance Leader { get; set; } = null!;
        public List<CardInstance> Hand { get; set; } = new List<CardInstance>();
        public List<CardInstance> Deck { get; set; } = new List<CardInstance>();
        public List<CardInstance> Field { get; set; } = new List<CardInstance>();
        public List<CardInstance> Graveyard { get; set; } = new List<CardInstance>();
        public List<CardInstance> Extra { get; set; } = new List<CardInstance>();
        public int DeckCount => Deck?.Count ?? 0;
        public bool CanEvolve { get; set; } = false;

        public PlayerState(Guid playerId)
        {
            PlayerId = playerId;
            Hand = new List<CardInstance>();
            Deck = new List<CardInstance>();
            Field = new List<CardInstance>();
            Graveyard = new List<CardInstance>();
            Extra = new List<CardInstance>();
        }

        private PlayerState()
        {
            PlayerId = Guid.Empty;
            Hand = new List<CardInstance>();
            Deck = new List<CardInstance>();
            Field = new List<CardInstance>();
            Graveyard = new List<CardInstance>();
            Extra = new List<CardInstance>();
        }

        public DomainOperationResult ModifyPlayPoints(int amount)
        {
            var result = new DomainOperationResult();
            int originalPP = this.CurrentPlayPoints;
            int newPP = Math.Max(0, Math.Min(originalPP + amount, this.MaxPlayPoints));
            if (originalPP != newPP)
            {
                this.CurrentPlayPoints = newPP;
                result.GeneratedEvents.Add(new PpUpdateEvent(this, this.CurrentPlayPoints, this.MaxPlayPoints));
            }
            result.Success = true;
            return result;
        }

        public DomainOperationResult ModifyMaxPlayPoints(int amount)
        {
            var result = new DomainOperationResult();
            int originalMaxPP = this.MaxPlayPoints;
            int newMaxPP = Math.Max(0, Math.Min(originalMaxPP + amount, GameConstants.PlayPoints.MaxPlayPoints));
            if (originalMaxPP != newMaxPP)
            {
                this.MaxPlayPoints = newMaxPP;
                result.GeneratedEvents.Add(new PpUpdateEvent(this, this.CurrentPlayPoints, this.MaxPlayPoints));
            }
            result.Success = true;
            return result;
        }

        private List<CardInstance>? GetZoneList(CardZone zone)
        {
            return zone switch
            {
                CardZone.Hand => this.Hand,
                CardZone.Deck => this.Deck,
                CardZone.Field => this.Field,
                CardZone.Graveyard => this.Graveyard,
                CardZone.Extra => this.Extra,
                _ => null,
            };
        }

        private void UpdateZoneIndices(CardZone zone)
        {
            var zoneList = GetZoneList(zone);
            if (zoneList == null) return;

            for (int i = 0; i < zoneList.Count; i++)
            {
                zoneList[i].ZoneIndex = i;
            }
        }

        private bool RemoveCardFromCurrentZone(CardInstance card)
        {
            var zoneList = GetZoneList(card.CurrentZone);
            if (zoneList != null && zoneList.Remove(card))
            {
                UpdateZoneIndices(card.CurrentZone);
                return true;
            }
            return false;
        }

        private DomainOperationResult MoveCard(CardInstance card, CardZone destination, int? index = null)
        {
            var result = new DomainOperationResult();
            var sourceZone = card.CurrentZone;

            if (sourceZone == destination)
            {
                result.Success = true;
                return result;
            }

            if (!RemoveCardFromCurrentZone(card))
            {
                result.Success = false;
                result.ErrorMessage = $"Card {card.InstanceId} not found in its declared source zone '{sourceZone}'.";
                return result;
            }

            var destinationList = GetZoneList(destination);
            if (destinationList == null && destination != CardZone.Extra && destination != CardZone.None)
            {
                result.Success = false;
                result.ErrorMessage = $"Cannot add card to an invalid or unsupported target zone: {destination}";
                // Attempt to return the card to its original zone to prevent card loss
                GetZoneList(sourceZone)?.Add(card);
                UpdateZoneIndices(sourceZone);
                return result;
            }

            card.CurrentZone = destination;

            if (destinationList != null)
            {
                if (index.HasValue && index.Value >= 0 && index.Value <= destinationList.Count)
                {
                    destinationList.Insert(index.Value, card);
                }
                else
                {
                    destinationList.Add(card);
                }
                UpdateZoneIndices(destination);
            }

            result.GeneratedEvents.Add(new CardMovedEvent(card, sourceZone, destination));
            if (destination == CardZone.Hand || sourceZone == CardZone.Hand)
            {
                result.GeneratedEvents.Add(new HandUpdatedEvent(this, this.Hand.ToList()));
            }
            if (destination == CardZone.Deck || sourceZone == CardZone.Deck)
            {
                result.GeneratedEvents.Add(new DeckCountChangedEvent(this, this.Deck.Count));
            }

            result.Success = true;
            return result;
        }

        public DomainOperationResult SummonCardToField(CardInstance card, int? index = null)
        {
            if (Field.Count >= GameConstants.Field.MaxFieldSize)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = "Field is full." };
            }
            var result = MoveCard(card, CardZone.Field, index);
            if (result.Success)
            {
                result.GeneratedEvents.Add(new SummonEvent(this, card, index));
            }
            return result;
        }

        public DomainOperationResult MoveCardToGraveyard(CardInstance card)
        {
            var result = MoveCard(card, CardZone.Graveyard, null);
            if (result.Success)
            {
                // 【BUG修复】当卡牌成功移动到墓地时，生成GraveyardUpdatedEvent
                result.GeneratedEvents.Add(new GraveyardUpdatedEvent(this, this.Graveyard.Count));
            }
            return result;
        }

        public DomainOperationResult DestroyCard(CardInstance cardToDestroy, CardInstance? cause, string reason = "Effect")
        {
            var moveResult = MoveCardToGraveyard(cardToDestroy);
            if (moveResult.Success)
            {
                // The 'cause' is the source of the destruction, 'cardToDestroy' is the target.
                // This now correctly aligns with the CardDestroyedEvent constructor.
                moveResult.GeneratedEvents.Add(new CardDestroyedEvent(cause, cardToDestroy, reason));
            }
            return moveResult;
        }

        public DomainOperationResult PlayCard(CardInstance cardInHand, List<Guid> targetIds)
        {
            var result = new DomainOperationResult();
            var ppResult = ModifyPlayPoints(-cardInHand.CurrentCost);
            result.GeneratedEvents.AddRange(ppResult.GeneratedEvents);
            this.PlayedCardCountThisTurn++;
            return result;
        }

        public DomainOperationResult ConsumeEvolutionPoints(int amount)
        {
            if (amount <= 0)
            {
                return new DomainOperationResult { Success = true };
            }
            return ModifyEvolutionPoints(-amount);
        }

        public DomainOperationResult ModifyEvolutionPoints(int amount)
        {
            var result = new DomainOperationResult();
            int originalEP = this.CurrentEvolutionPoints;
            int newEP = Math.Max(0, Math.Min(originalEP + amount, this.MaxEvolutionPoints));
            if (originalEP != newEP)
            {
                this.CurrentEvolutionPoints = newEP;
                result.GeneratedEvents.Add(new EpUpdateEvent(this, this.CurrentEvolutionPoints, this.MaxEvolutionPoints));
            }
            result.Success = true;
            return result;
        }

        public DomainOperationResult EvolveCard(CardInstance cardToEvolve, CardDefinition evolvedDefinition)
        {
            var result = new DomainOperationResult();

            // Preconditions (EP, CanEvolve flag, etc.) are now expected to be validated by the calling service/handler.
            // This method now only executes the state change.

            // 1. Consume Evolution Point
            var epResult = ConsumeEvolutionPoints(GameConstants.Evolution.EvolutionPointCost);
            if (!epResult.Success)
            {
                // This check is a safeguard; the caller should have already verified EP.
                return epResult;
            }
            result.GeneratedEvents.AddRange(epResult.GeneratedEvents);

            // 2. Apply the transformation by calling the method on the card instance
            var evolveResult = cardToEvolve.ApplyEvolution(evolvedDefinition);
            result.GeneratedEvents.AddRange(evolveResult.GeneratedEvents);

            // 3. Finalize player state
            this.CanEvolve = false; // Consume the turn's evolution right

            result.Success = true;
            return result;
        }

        private DomainOperationResult CheckAndUnlockEvolutionPoints()
        {
            var result = new DomainOperationResult();

            // Don't do anything if EP is already unlocked.
            if (this.MaxEvolutionPoints > 0)
            {
                result.Success = true;
                return result;
            }

            bool isEligibleForUnlock =
                (this.IsFirst && this.TurnNumber == GameConstants.Evolution.FirstPlayerEvolutionStartTurn) ||
                (!this.IsFirst && this.TurnNumber == GameConstants.Evolution.SecondPlayerEvolutionStartTurn);

            if (isEligibleForUnlock)
            {
                int newEp = this.IsFirst
                    ? GameConstants.Evolution.FirstPlayerEvolutionPoints
                    : GameConstants.Evolution.SecondPlayerEvolutionPoints;

                this.MaxEvolutionPoints = newEp;
                this.CurrentEvolutionPoints = newEp;
                this.CanEvolve = true; // Grant evolution right for the turn
                result.GeneratedEvents.Add(new EpUpdateEvent(this, this.CurrentEvolutionPoints, this.MaxEvolutionPoints));
            }

            result.Success = true;
            return result;
        }

        public DomainOperationResult EndTurn()
        {
            var result = new DomainOperationResult();
            result.GeneratedEvents.Add(new TurnEndEvent(this));
            this.PlayedCardCountThisTurn = 0;
            result.Success = true;
            return result;
        }

        public MulliganResult PerformMulligan(List<Guid> cardUidsToReplace, GameState gameState)
        {
            if (gameState.Phase != GamePhase.Mulligan)
                throw new GameLogicException("Mulligan can only be performed during mulligan phase.");
            if (this.HasCompletedMulligan)
                throw new GameLogicException($"Player {this.PlayerId} has already completed mulligan.");
            if (cardUidsToReplace.Count > this.Hand.Count)
                throw new GameLogicException("Cannot replace more cards than in hand.");

            var result = new MulliganResult();
            var cardsToReplace = this.Hand.Where(c => cardUidsToReplace.Contains(c.InstanceId)).ToList();

            foreach (var card in cardsToReplace)
            {
                MoveCard(card, CardZone.Deck, null);
            }

            ShuffleDeck();

            int cardsToDrawCount = cardsToReplace.Count;
            for (int i = 0; i < cardsToDrawCount && Deck.Count > 0; i++)
            {
                if (Hand.Count >= GameConstants.Hand.MaxHandSize) break;
                var drawnCard = Deck[0];
                MoveCard(drawnCard, CardZone.Hand, null);
            }

            HasCompletedMulligan = true;
            result.GeneratedEvents.Add(new HandUpdatedEvent(this, Hand.ToList()));

            if (gameState.AreBothMulligansCompleted())
            {
                var transitionEvents = gameState.TransitionFromMulliganToMain();
                result.GeneratedEvents.AddRange(transitionEvents);
                result.PhaseTransitionTriggered = true;
                result.FirstTurnPlayerId = gameState.FirstPlayerId;
            }

            result.GeneratedEvents.Add(new MulliganCompletedEvent(this, cardUidsToReplace));
            result.Success = true;
            return result;
        }

        private void ShuffleDeck()
        {
            var random = new Random();
            for (int i = Deck.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (Deck[i], Deck[j]) = (Deck[j], Deck[i]);
            }
        }

        public DomainOperationResult StartTurn(GameState gameState)
        {
            if (gameState.IsGameOver)
                throw new GameLogicException("Game is already over.");
            if (gameState.Phase == GamePhase.Mulligan)
                throw new GameLogicException("Cannot start turn during mulligan phase.");

            var result = new DomainOperationResult();
            this.CanEvolve = this.CurrentEvolutionPoints > 0;
            TurnNumber += 1;
            this.HasTakenFirstTurn = true;
            gameState.CurrentPlayerId = this.PlayerId;

            var epResult = CheckAndUnlockEvolutionPoints();
            result.GeneratedEvents.AddRange(epResult.GeneratedEvents);

            var ppResult = RestorePlayPoints();
            result.GeneratedEvents.AddRange(ppResult.GeneratedEvents);

            int cardsToDraw = (this.TurnNumber == 1 && !this.IsFirst) ? 2 : 1;
            var drawResult = DrawCards(cardsToDraw);
            result.GeneratedEvents.AddRange(drawResult.GeneratedEvents);

            var unitResetResult = ResetUnitsForTurnStart();
            result.GeneratedEvents.AddRange(unitResetResult.GeneratedEvents);

            result.GeneratedEvents.Add(new TurnStartEvent(this));

            result.Success = true;
            return result;
        }

        public DomainOperationResult RestorePlayPoints()
        {
            var result = new DomainOperationResult();
            int originalPP = CurrentPlayPoints;
            int originalMaxPP = MaxPlayPoints;
            MaxPlayPoints = Math.Min(10, MaxPlayPoints + 1);
            CurrentPlayPoints = MaxPlayPoints;
            if (originalPP != CurrentPlayPoints || originalMaxPP != MaxPlayPoints)
            {
                result.GeneratedEvents.Add(new PpUpdateEvent(this, CurrentPlayPoints, MaxPlayPoints));
            }
            return result;
        }

        public DomainOperationResult DrawCards(int drawCount)
        {
            var result = new DomainOperationResult();
            var drawnCardIds = new List<Guid>();

            for (int i = 0; i < drawCount; i++)
            {
                if (this.Deck.Count == 0)
                {
                    result.GeneratedEvents.Add(new DeckEmptyOnDrawEvent(this));
                    break;
                }
                var drawnCard = this.Deck[0];
                if (this.Hand.Count >= GameConstants.Hand.MaxHandSize)
                {
                    var burnOpResult = MoveCard(drawnCard, CardZone.Graveyard, null);
                    result.GeneratedEvents.AddRange(burnOpResult.GeneratedEvents);
                    if (burnOpResult.Success)
                    {
                        result.GeneratedEvents.Add(new CardBurnedEvent(drawnCard, "HandFull"));
                    }
                }
                else
                {
                    var addOpResult = MoveCard(drawnCard, CardZone.Hand, null);
                    if (addOpResult.Success)
                    {
                        drawnCardIds.Add(drawnCard.InstanceId);
                    }
                    result.GeneratedEvents.AddRange(addOpResult.GeneratedEvents);
                }
            }

            if (drawnCardIds.Any())
            {
                result.GeneratedEvents.Add(new DrawEvent(this, drawnCardIds));
            }

            result.Success = true;
            return result;
        }

        public DomainOperationResult AddCardsToHand(List<CardInstance> cardInstancesToAdd)
        {
            var result = new DomainOperationResult();
            if (cardInstancesToAdd == null)
            {
                result.Success = false;
                result.ErrorMessage = "cardInstancesToAdd cannot be null.";
                return result;
            }
            foreach (var cardInstance in cardInstancesToAdd)
            {
                if (this.Hand.Count >= GameConstants.Hand.MaxHandSize)
                {
                    break;
                }
                var addResult = MoveCard(cardInstance, CardZone.Hand, null);
                result.GeneratedEvents.AddRange(addResult.GeneratedEvents);
            }
            result.Success = true;
            return result;
        }

        private DomainOperationResult ResetUnitsForTurnStart()
        {
            var result = new DomainOperationResult();
            // The responsibility of cleaning up dead units is now with the handlers
            // that cause the damage/destruction (AttackEffectHandler, DamageEffectHandler, etc.).
            // This method should only reset turn-based states.
            foreach (var unit in Field)
            {
                unit.AttacksThisTurn = 0;
                if (!unit.CanAttack)
                {
                    unit.CanAttack = true;
                }
            }
            result.Success = true;
            return result;
        }

        public DomainOperationResult ExecutePlayCard(CardInstance cardInHand)
        {
            var result = new DomainOperationResult();
            if (!this.Hand.Contains(cardInHand))
            {
                result.Success = false;
                result.ErrorMessage = $"Card {cardInHand.InstanceId} not found in hand.";
                return result;
            }
            if (this.CurrentPlayPoints < cardInHand.CurrentCost)
            {
                result.Success = false;
                result.ErrorMessage = $"Not enough PP. Required: {cardInHand.CurrentCost}, Available: {this.CurrentPlayPoints}";
                return result;
            }

            var ppResult = ModifyPlayPoints(-cardInHand.CurrentCost);
            result.GeneratedEvents.AddRange(ppResult.GeneratedEvents);
            this.PlayedCardCountThisTurn++;

            switch (cardInHand.CardType)
            {
                case CardType.Follower:
                case CardType.Amulet:
                    cardInHand.CanAttack = cardInHand.HasKeyword(CardKeyword.Rush) || cardInHand.HasKeyword(CardKeyword.Storm);
                    cardInHand.AttacksThisTurn = 0;
                    var summonResult = SummonCardToField(cardInHand);
                    result.GeneratedEvents.AddRange(summonResult.GeneratedEvents);
                    break;
                case CardType.Spell:
                    var graveyardResult = MoveCardToGraveyard(cardInHand);
                    result.GeneratedEvents.AddRange(graveyardResult.GeneratedEvents);
                    break;
                default:
                    result.Success = false;
                    result.ErrorMessage = $"Unknown card type: {cardInHand.CardType}";
                    return result;
            }
            result.Success = true;
            return result;
        }

        public DomainOperationResult DiscardCard(CardInstance cardToDiscard, string reason)
        {
            if (!this.Hand.Contains(cardToDiscard))
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Card {cardToDiscard.InstanceId} not found in hand." };
            }
            var result = MoveCardToGraveyard(cardToDiscard);
            if (result.Success)
            {
                result.GeneratedEvents.Add(new CardDiscardedEvent(this, cardToDiscard.InstanceId, reason));
            }
            return result;
        }

        public DomainOperationResult ReturnCardToHand(CardInstance cardToReturn)
        {
            if (cardToReturn.CurrentZone != CardZone.Field)
            {
                return new DomainOperationResult { Success = false, ErrorMessage = $"Card {cardToReturn.InstanceId} is not on the field." };
            }
            if (this.Hand.Count >= GameConstants.Hand.MaxHandSize)
            {
                var graveyardResult = MoveCardToGraveyard(cardToReturn);
                graveyardResult.ErrorMessage = "Hand is full, card was moved to graveyard instead.";
                return graveyardResult;
            }
            var result = MoveCard(cardToReturn, CardZone.Hand, null);
            if (result.Success)
            {
                result.GeneratedEvents.Add(new CardReturnedToHandEvent(cardToReturn));
            }
            return result;
        }

        public DomainOperationResult SummonTokens(List<CardInstance> tokenInstancesToSummon)
        {
            var result = new DomainOperationResult { Success = true };
            if (tokenInstancesToSummon == null)
            {
                result.Success = false;
                result.ErrorMessage = "tokenInstancesToSummon cannot be null.";
                return result;
            }
            foreach (var tokenInstance in tokenInstancesToSummon)
            {
                if (this.Field.Count >= GameConstants.Field.MaxFieldSize)
                {
                    break;
                }
                var summonResult = SummonCardToField(tokenInstance);
                result.GeneratedEvents.AddRange(summonResult.GeneratedEvents);
            }
            return result;
        }

        public DomainOperationResult VanishCard(CardInstance cardToVanish)
        {
            var originalZone = cardToVanish.CurrentZone;
            var result = MoveCard(cardToVanish, CardZone.Extra, null);
            if (result.Success)
            {
                result.GeneratedEvents.Add(new CardVanishedEvent(cardToVanish, originalZone));
            }
            return result;
        }

        public List<CardInstance> GetCardsInZone(CardZone zone)
        {
            var zoneList = GetZoneList(zone);
            return zoneList != null ? new List<CardInstance>(zoneList) : new List<CardInstance>();
        }

    }
}