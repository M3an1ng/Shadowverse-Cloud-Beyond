using AutoMapper; // Added for IMapper
//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 战斗系统: 文档/05-游戏系统/02-战斗系统.md
// - 回合管理系统: 文档/05-游戏系统/03-回合管理系统.md
//
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.SignalR.Actions; // Added for action DTOs
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq; // Added for FirstOrDefault
using ShadowverseBeyond.Server.Domain.Buffs; // Add using for Buff
using ShadowverseBeyond.Server.Domain.Abilities; // Add using for Ability
using System; // Add using for ArgumentNullException

namespace ShadowverseBeyond.Server.Domain.Events
{
    /// <summary>
    /// Event triggered when a card is played from hand.
    /// This is the initial event that may trigger Fanfare abilities.
    /// </summary>
    public class PlayCardEvent : GameEventBase
    {
        public PlayCardEvent(PlayerStateDto player, CardInstance card, CardInstance? target = null)
            : base("play_card")
        {
            SourcePlayer = player;
            SourceCard = card;
            if (target != null) Parameters["target"] = target; // Assign only if not null
        }

        public override async Task<List<IGameEvent>> ExecuteAsync(GameContext context)
        {
            var events = new List<IGameEvent>();

            // Execute the card play logic based on card type
            if (SourceCard != null)
            {
                switch (SourceCard.CardType.ToLowerInvariant())
                {
                    case "follower":
                        // Follower cards: summon to battlefield
                        events.Add(new SummonEvent(SourcePlayer!, SourceCard));
                        break;
                    case "spell":
                        // Spell cards: execute effects then go to graveyard
                        events.Add(new CardDestroyedEvent(SourcePlayer!, SourceCard));
                        break;
                    case "amulet":
                        // Amulet cards: place on battlefield
                        events.Add(new SummonEvent(SourcePlayer!, SourceCard));
                        break;
                }
            }

            return await Task.FromResult(events);
        }
    }

    /// <summary>
    /// Event triggered when a card enters the battlefield (summoned).
    /// This is the natural trigger point for Fanfare abilities.
    /// </summary>
    public class SummonEvent : GameEventBase
    {
        public SummonEvent(PlayerStateDto player, CardInstance card, int? position = null)
            : base("summon")
        {
            SourcePlayer = player;
            SourceCard = card;
            if (position != null) Parameters["position"] = position; // Assign only if not null
        }

        public override async Task<List<IGameEvent>> ExecuteAsync(GameContext context)
        {
            var events = new List<IGameEvent>();

            // The actual summoning logic would be handled by GameService
            // This event just represents the fact that summoning occurred
            // Fanfare abilities will naturally listen to this event

            return await Task.FromResult(events);
        }
    }

    /// <summary>
    /// Event triggered when damage is dealt to a target.
    /// </summary>
    public class DamageEvent : GameEventBase
    {
        public DamageEvent(CardInstance target, int damage, CardInstance? source = null)
            : base("damage")
        {
            SourceCard = source;
            Parameters["target"] = target;
            Parameters["damage"] = damage;
        }

        public override async Task<List<IGameEvent>> ExecuteAsync(GameContext context)
        {
            var events = new List<IGameEvent>();
            var target = (CardInstance)Parameters["target"];
            var damage = (int)Parameters["damage"];

            // Apply damage logic would be handled by GameService
            // If target dies, generate death event
            if (target.CurrentHealth - damage <= 0)
            {
                // 使用GameContext的CreateSnapshot获取玩家信息
                var gameStateDto = context.CreateSnapshot();
                var targetPlayer = gameStateDto.Players?.FirstOrDefault(p => p.PlayerId == target.OwnerPlayerId);
                if (targetPlayer != null)
                {
                    events.Add(new CardDestroyedEvent(targetPlayer, target));
                }
            }

            return await Task.FromResult(events);
        }
    }

    /// <summary>
    /// Event triggered when a card is destroyed/dies.
    /// This is the natural trigger point for Last Words abilities.
    /// </summary>
    public class CardDestroyedEvent : GameEventBase
    {
        public CardDestroyedEvent(PlayerStateDto player, CardInstance card)
            : base("card_destroyed")
        {
            SourcePlayer = player;
            SourceCard = card;
        }

        public override async Task<List<IGameEvent>> ExecuteAsync(GameContext context)
        {
            var events = new List<IGameEvent>();

            // The actual destruction logic would be handled by GameService
            // Last Words abilities will naturally listen to this event

            return await Task.FromResult(events);
        }
    }

    /// <summary>
    /// Event triggered when a card evolves.
    /// This is the natural trigger point for Evolve Effect abilities.
    /// </summary>
    public class EvolveEvent : GameEventBase
    {
        public EvolveEvent(PlayerStateDto player, CardInstance card)
            : base("evolve")
        {
            SourcePlayer = player;
            SourceCard = card;
        }

        public override async Task<List<IGameEvent>> ExecuteAsync(GameContext context)
        {
            var events = new List<IGameEvent>();

            // The actual evolution logic would be handled by GameService
            // Evolve Effect abilities will naturally listen to this event

            return await Task.FromResult(events);
        }
    }

    /// <summary>
    /// Event triggered when an attack is declared.
    /// </summary>
    public class AttackEvent : GameEventBase
    {
        public AttackEvent(CardInstance attacker, CardInstance target)
            : base("attack")
        {
            SourceCard = attacker;
            Parameters["target"] = target;
        }

        public override async Task<List<IGameEvent>> ExecuteAsync(GameContext context)
        {
            var events = new List<IGameEvent>();
            var target = (CardInstance)Parameters["target"];

            // Calculate damage and generate damage events
            if (SourceCard != null)
            {
                // Attacker deals damage to target
                events.Add(new DamageEvent(target, SourceCard.CurrentAttack, SourceCard));

                // If target can deal damage back (unit combat)
                if (target.CurrentAttack > 0)
                {
                    events.Add(new DamageEvent(SourceCard, target.CurrentAttack, target));
                }
            }

            return await Task.FromResult(events);
        }
    }

    /// <summary>
    /// Event triggered when two units engage in combat (clash).
    /// This event is fired before damage calculation and allows abilities to trigger.
    /// </summary>
    public class ClashEvent : GameEventBase
    {
        public ClashEvent(CardInstance attacker, CardInstance defender)
            : base("clash")
        {
            SourceCard = attacker;
            Parameters["attacker"] = attacker;
            Parameters["defender"] = defender;
            Parameters["clash_type"] = "unit_combat";
        }

        public override async Task<List<IGameEvent>> ExecuteAsync(GameContext context)
        {
            var events = new List<IGameEvent>();

            // Clash event itself doesn't generate additional events
            // It serves as a trigger point for abilities that respond to combat
            // The actual damage will be handled by subsequent AttackEvent

            return await Task.FromResult(events);
        }
    }

    /// <summary>
    /// Event triggered when healing is applied to a target.
    /// </summary>
    public class HealEvent : GameEventBase
    {
        public HealEvent(CardInstance target, int healAmount, CardInstance? source = null)
            : base("heal")
        {
            SourceCard = source;
            Parameters["target"] = target;
            Parameters["healAmount"] = healAmount;
        }

        public override async Task<List<IGameEvent>> ExecuteAsync(GameContext context)
        {
            var events = new List<IGameEvent>();
            var target = (CardInstance)Parameters["target"];
            var healAmount = (int)Parameters["healAmount"];

            // Apply healing logic would be handled by GameService
            // This event represents the fact that healing occurred

            return await Task.FromResult(events);
        }
    }

    /// <summary>
    /// Event triggered when an amulet's countdown reaches zero.
    /// </summary>
    public class CountdownFinishedEvent : GameEventBase
    {
        public CountdownFinishedEvent(PlayerStateDto player, CardInstance amulet)
            : base("countdown_finished")
        {
            SourcePlayer = player;
            SourceCard = amulet;
        }

        public override async Task<List<IGameEvent>> ExecuteAsync(GameContext context)
        {
            var events = new List<IGameEvent>();

            // The actual countdown finish logic would be handled by GameService
            // This typically triggers the amulet's effect and then destroys it
            if (SourceCard != null && SourcePlayer != null)
            {
                events.Add(new CardDestroyedEvent(SourcePlayer, SourceCard));
            }

            return await Task.FromResult(events);
        }
    }

    /// <summary>
    /// Event triggered when a player draws a card.
    /// </summary>
    public class DrawEvent : GameEventBase
    {
        public DrawEvent(PlayerStateDto player, int count = 1)
            : base("draw")
        {
            SourcePlayer = player;
            Parameters["count"] = count;
        }

        public override async Task<List<IGameEvent>> ExecuteAsync(GameContext context)
        {
            var events = new List<IGameEvent>();
            // The actual draw logic would be handled by GameService
            return await Task.FromResult(events);
        }
    }

    /// <summary>
    /// Event triggered at the start of a player's turn.
    /// </summary>
    public class TurnStartEvent : GameEventBase
    {
        public TurnStartEvent(PlayerStateDto player)
            : base("turn_start")
        {
            SourcePlayer = player;
        }

        public override async Task<List<IGameEvent>> ExecuteAsync(GameContext context)
        {
            var events = new List<IGameEvent>();
            // Turn start logic would be handled by GameService
            return await Task.FromResult(events);
        }
    }

    /// <summary>
    /// Event triggered at the end of a player's turn.
    /// </summary>
    public class TurnEndEvent : GameEventBase
    {
        public TurnEndEvent(PlayerStateDto player)
            : base("turn_end")
        {
            SourcePlayer = player;
        }

        public override async Task<List<IGameEvent>> ExecuteAsync(GameContext context)
        {
            var events = new List<IGameEvent>();
            // Turn end logic would be handled by GameService
            return await Task.FromResult(events);
        }
    }

    /// <summary>
    /// Event triggered when a Buff is successfully applied (registered) to a target.
    /// </summary>
    public class BuffAppliedEvent : GameEventBase
    {
        /// <summary>
        /// The Buff instance that was applied.
        /// Contains details about the source, target, duration, and effects.
        /// </summary>
        public Buff AppliedBuff { get; }

        public BuffAppliedEvent(Buff appliedBuff)
            : base("buff_applied") // Define a unique event type string
        {
            AppliedBuff = appliedBuff ?? throw new ArgumentNullException(nameof(appliedBuff));
            // Populate base properties if needed (SourcePlayer, SourceCard might come from the buff's source ability)
            SourceCard = appliedBuff.SourceAbility?.SourceCard;
            // Finding the SourcePlayer might require looking up the SourceCard's controller in the GameState
            // Parameters can hold the BuffId or other relevant info
            Parameters["buffId"] = appliedBuff.Id;
            Parameters["targetCardInstanceId"] = appliedBuff.TargetCardInstanceId;
        }

        public override Task<List<IGameEvent>> ExecuteAsync(GameContext context) // Corrected angle brackets
        {
            // This event typically doesn't execute further logic itself,
            // it serves as a notification for other systems (like UI or state calculators).
            return Task.FromResult(new List<IGameEvent>()); // Corrected angle brackets
        }
    }

    /// <summary>
    /// Event triggered when a Buff is removed from a target.
    /// </summary>
    public class BuffRemovedEvent : GameEventBase
    {
        /// <summary>
        /// The Buff instance that was removed.
        /// </summary>
        public Buff RemovedBuff { get; }

        /// <summary>
        /// The reason for buff removal (e.g., "expired", "silenced", "replaced", "manual").
        /// </summary>
        public string RemovalReason { get; }

        public BuffRemovedEvent(Buff removedBuff, string removalReason = "manual")
            : base("buff_removed")
        {
            RemovedBuff = removedBuff ?? throw new ArgumentNullException(nameof(removedBuff));
            RemovalReason = removalReason ?? "unknown";

            // Populate base properties
            SourceCard = removedBuff.SourceAbility?.SourceCard;
            Parameters["buffId"] = removedBuff.Id;
            Parameters["targetCardInstanceId"] = removedBuff.TargetCardInstanceId;
            Parameters["removalReason"] = removalReason;
        }

        public override Task<List<IGameEvent>> ExecuteAsync(GameContext context)
        {
            // This event typically doesn't execute further logic itself,
            // it serves as a notification for other systems (like UI or state calculators).
            return Task.FromResult(new List<IGameEvent>());
        }
    }

    /// <summary>
    /// Event triggered when silence is applied to a target.
    /// </summary>
    public class SilenceAppliedEvent : GameEventBase
    {
        /// <summary>
        /// The target card instance that was silenced.
        /// </summary>
        public CardInstance Target { get; }

        /// <summary>
        /// The source ability that applied the silence (if any).
        /// </summary>
        public Ability? SourceAbility { get; }

        public SilenceAppliedEvent(CardInstance target, CardInstance? sourceCard = null, Ability? sourceAbility = null)
            : base("silence_applied")
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            SourceCard = sourceCard; // Use inherited property
            SourceAbility = sourceAbility;

            // Populate base properties
            Parameters["targetCardInstanceId"] = target.InstanceId;
            Parameters["sourceCardInstanceId"] = sourceCard?.InstanceId;
            Parameters["sourceAbilityId"] = sourceAbility?.AbilityId;
        }

        public override Task<List<IGameEvent>> ExecuteAsync(GameContext context)
        {
            // This event typically doesn't execute further logic itself,
            // it serves as a notification for other systems (like UI or state calculators).
            return Task.FromResult(new List<IGameEvent>());
        }
    }

    /// <summary>
    /// Event triggered when silence is removed from a target.
    /// </summary>
    public class SilenceRemovedEvent : GameEventBase
    {
        /// <summary>
        /// The target card instance that had
        /// The target card instance that had silence removed.
        /// </summary>
        public CardInstance Target { get; }

        public SilenceRemovedEvent(CardInstance target)
            : base("silence_removed")
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));

            // Populate base properties
            Parameters["targetCardInstanceId"] = target.InstanceId;
        }

        public override Task<List<IGameEvent>> ExecuteAsync(GameContext context)
        {
            // This event typically doesn't execute further logic itself,
            // it serves as a notification for other systems (like UI or state calculators).
            return Task.FromResult(new List<IGameEvent>());
        }
    }
    /// <summary>
    /// Represents the reason why a card was discarded.
    /// </summary>
    public enum DiscardReason
    {
        /// <summary>Discarded due to a card effect.</summary>
        Effect,
        /// <summary>Discarded due to having a full hand when drawing.</summary>
        Overdraw,
        /// <summary>Discarded during the mulligan phase.</summary>
        Mulligan,
        /// <summary>Discarded for other or unknown reasons.</summary>
        Other
    }

    /// <summary>
    /// Event triggered when a single, specific card is drawn by a player.
    /// Contrasts with DrawEvent which signifies the intent to draw N cards.
    /// </summary>
    public class SingleCardDrawnEvent : GameEventBase
    {
        /// <summary>
        /// The specific card instance that was drawn.
        /// </summary>
        public CardInstance DrawnCard { get; }

        // Constructor needs IMapper to convert PlayerState to PlayerStateDto for base class
        // IMPORTANT: Ensure 'using AutoMapper;' exists at the top of CoreGameEvents.cs
        public SingleCardDrawnEvent(PlayerState player, CardInstance drawnCard, IMapper mapper)
            : base("single_card_drawn")
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (mapper == null) throw new ArgumentNullException(nameof(mapper));

            // Use base.SourcePlayer (PlayerStateDto)
            SourcePlayer = mapper.Map<PlayerStateDto>(player);
            DrawnCard = drawnCard ?? throw new ArgumentNullException(nameof(drawnCard));
            SourceCard = drawnCard; // The drawn card is the "source" of this specific event context
            Parameters["drawnCardInstanceId"] = drawnCard.InstanceId;
            Parameters["drawnCardDefinitionId"] = drawnCard.DefinitionId;
        }

        // Corrected ExecuteAsync signature and implementation
        public override Task<List<IGameEvent>> ExecuteAsync(GameContext context)
        {
            // This event typically doesn't execute further logic itself.
            return Task.FromResult(new List<IGameEvent>());
        }
    }

    /// <summary>
    /// Event triggered when a specific card is discarded from a player's hand.
    /// </summary>
    public class CardDiscardedEvent : GameEventBase
    {
        /// <summary>
        /// The specific card instance that was discarded.
        /// </summary>
        public CardInstance DiscardedCard { get; }

        /// <summary>
        /// The reason the card was discarded.
        /// </summary>
        public DiscardReason Reason { get; }

        // Constructor needs IMapper to convert PlayerState to PlayerStateDto for base class
        // IMPORTANT: Ensure 'using AutoMapper;' exists at the top of CoreGameEvents.cs
        public CardDiscardedEvent(PlayerState player, CardInstance discardedCard, DiscardReason reason, IMapper mapper)
            : base("card_discarded")
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (mapper == null) throw new ArgumentNullException(nameof(mapper));

            // Use base.SourcePlayer (PlayerStateDto)
            SourcePlayer = mapper.Map<PlayerStateDto>(player);
            DiscardedCard = discardedCard ?? throw new ArgumentNullException(nameof(discardedCard));
            Reason = reason;
            SourceCard = discardedCard; // The discarded card is the "source" of this specific event context
            Parameters["discardedCardInstanceId"] = discardedCard.InstanceId;
            Parameters["discardedCardDefinitionId"] = discardedCard.DefinitionId;
            Parameters["reason"] = reason.ToString();
        }

        // Corrected ExecuteAsync signature and implementation
        public override Task<List<IGameEvent>> ExecuteAsync(GameContext context)
        {
            // This event typically doesn't execute further logic itself.
            return Task.FromResult(new List<IGameEvent>());
        }
    }

    /// <summary>
    /// Event triggered when a card instance's stats (Attack, Health, MaxHealth) change.
    /// </summary>
    public class StatChangedEvent : GameEventBase
    {
        public Guid TargetCardInstanceId { get; }
        public int NewAttack { get; }
        public int NewHealth { get; }
        public int NewMaxHealth { get; }

        // Constructor needs IMapper if SourcePlayer is required by base or logic
        // For now, let's assume the source is implicitly the ability/effect source
        public StatChangedEvent(Guid sourcePlayerId, Guid targetCardInstanceId, int newAttack, int newHealth, int newMaxHealth)
            : base("stat_changed") // EventType string
        {
            // If GameEventBase requires PlayerStateDto SourcePlayer:
            // SourcePlayer = mapper.Map<PlayerStateDto>(sourcePlayerState); 
            // Or maybe just store the ID if the full DTO isn't needed by subscribers?
            // For simplicity now, let's assume base doesn't strictly require SourcePlayer DTO for this event type
            // or it's handled differently. We might need to revisit GameEventBase.

            // Store relevant IDs and values in Parameters for generic subscribers
            Parameters["sourcePlayerId"] = sourcePlayerId; // ID of the player whose action caused the change
            Parameters["targetCardInstanceId"] = targetCardInstanceId;
            Parameters["newAttack"] = newAttack;
            Parameters["newHealth"] = newHealth;
            Parameters["newMaxHealth"] = newMaxHealth;

            // Also store strongly-typed properties for specific subscribers
            TargetCardInstanceId = targetCardInstanceId;
            NewAttack = newAttack;
            NewHealth = newHealth;
            NewMaxHealth = newMaxHealth;

            // Set TargetCard if applicable (the card whose stats changed)
            // This might require fetching the CardInstance from context if not readily available
            // TargetCard = targetCardInstance; // Assuming targetCardInstance is available
        }

        public override Task<List<IGameEvent>> ExecuteAsync(GameContext context)
        {
            // This event typically doesn't execute further logic itself.
            return Task.FromResult(new List<IGameEvent>());
        }
    }
    /// <summary>
    /// Event triggered when a specific card instance is summoned to a zone (typically the field).
    /// </summary>
    public class CardSummonedEvent : GameEventBase
    {
        /// <summary>
        /// The specific card instance that was summoned.
        /// </summary>
        public CardInstance SummonedCard { get; }

        /// <summary>
        /// The zone the card was summoned to.
        /// </summary>
        public CardZone TargetZone { get; }

        /// <summary>
        /// The player who owns the summoned card.
        /// </summary>
        public Guid OwnerPlayerId { get; }

        /// <summary>
        /// The player whose action caused the summon (might be different from the owner).
        /// </summary>
        public Guid SourcePlayerId { get; }

        // Constructor might need IMapper if base class requires PlayerStateDto
        public CardSummonedEvent(Guid sourcePlayerId, CardInstance summonedCard, CardZone targetZone)
            : base("card_summoned") // Define event type string
        {
            SummonedCard = summonedCard ?? throw new ArgumentNullException(nameof(summonedCard));
            TargetZone = targetZone;
            OwnerPlayerId = summonedCard.OwnerPlayerId;
            SourcePlayerId = sourcePlayerId;

            // Populate base properties if needed
            SourceCard = summonedCard; // The summoned card is the context
            // SourcePlayer might need mapping if base requires DTO

            // Populate Parameters dictionary
            Parameters["sourcePlayerId"] = sourcePlayerId;
            Parameters["summonedCardInstanceId"] = summonedCard.InstanceId;
            Parameters["summonedCardDefinitionId"] = summonedCard.DefinitionId;
            Parameters["ownerPlayerId"] = summonedCard.OwnerPlayerId;
            Parameters["targetZone"] = targetZone.ToString();
        }

        public override Task<List<IGameEvent>> ExecuteAsync(GameContext context)
        {
            // This event typically doesn't execute further logic itself.
            return Task.FromResult(new List<IGameEvent>());
        }
    }
    /// &lt;summary&gt;
    /// Event triggered when a player's Evolution Points (EP) change.
    /// &lt;/summary&gt;
    public class EpUpdateEvent : GameEventBase
    {
        public Guid PlayerId { get; }
        public int CurrentEp { get; }
        public int MaxEp { get; }

        // Constructor might need IMapper if base class requires PlayerStateDto
        public EpUpdateEvent(Guid playerId, int currentEp, int maxEp)
            : base("ep_update") // Define event type string
        {
            PlayerId = playerId;
            CurrentEp = currentEp;
            MaxEp = maxEp;

            // Populate Parameters dictionary
            Parameters["playerId"] = playerId;
            Parameters["currentEp"] = currentEp;
            Parameters["maxEp"] = maxEp;

            // If GameEventBase requires SourcePlayer DTO, we might need to fetch and map it here
            // For now, assume it's not strictly required or handled differently for this event type.
        }

        public override Task<List<IGameEvent>> ExecuteAsync(GameContext context)
        {
            // This event typically doesn't execute further logic itself.
            return Task.FromResult(new List<IGameEvent>());
        }
    }


} // Move closing brace of namespace after the SilenceRemovedEvent class
