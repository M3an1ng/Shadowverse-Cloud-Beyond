using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using System.Collections.Generic;
using System.Threading.Tasks;

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
            Parameters["target"] = target;
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
            Parameters["position"] = position;
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
                var targetPlayer = context.GetPlayer(target.OwnerPlayerId);
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
}
