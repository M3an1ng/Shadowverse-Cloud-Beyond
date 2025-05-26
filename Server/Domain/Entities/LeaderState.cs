// File: Server/Domain/Entities/LeaderState.cs
using System; // For Math.Min

namespace ShadowverseBeyond.Server.Domain.Entities
{
    /// <summary>
    /// Represents the state of a Player's Leader on the board.
    /// Treated as a special type of BoardEntityState.
    /// </summary>
    public class LeaderState : BoardEntityState
    {
        // Leader-specific properties (Health)
        public int CurrentHealth { get; set; }
        public int MaxHealth { get; set; } // Can potentially be modified by effects

        // Constructor
        public LeaderState(string instanceId, string ownerPlayerId, int definitionId, int initialHealth)
            : base(instanceId, ownerPlayerId, definitionId)
        {
            if (initialHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialHealth), "Initial health must be positive.");
            }
            MaxHealth = initialHealth;
            CurrentHealth = initialHealth;
        }

        // Parameterless constructor for serialization/ORM
        // Make it private or protected if direct instantiation isn't desired
        protected LeaderState() : base() { }

        // Leader-specific methods
        public void TakeDamage(int amount)
        {
            if (amount < 0) return; // Damage shouldn't be negative
            CurrentHealth -= amount;

            // Check for defeat immediately after taking damage
            if (CurrentHealth <= 0)
            {
                CurrentHealth = 0; // Prevent negative health values visually
                                   // TODO: Raise a domain event indicating the leader is defeated.
                                   // Example: RaiseDomainEvent(new LeaderDefeatedEvent(this.InstanceId, this.OwnerPlayerId));
            }
            // TODO: Raise an event for taking damage? (e.g., for Vengeance effects)
            // Example: RaiseDomainEvent(new LeaderTookDamageEvent(this.InstanceId, amount));
        }

        public void Heal(int amount)
        {
            if (amount < 0 || CurrentHealth <= 0) return; // Healing shouldn't be negative, and can't heal if already defeated
            int previousHealth = CurrentHealth;
            CurrentHealth = Math.Min(CurrentHealth + amount, MaxHealth);
            int actualHealAmount = CurrentHealth - previousHealth;

            if (actualHealAmount > 0)
            {
                // TODO: Raise an event for healing?
                // Example: RaiseDomainEvent(new LeaderHealedEvent(this.InstanceId, actualHealAmount));
            }
        }

        // Example: Method to change max health (e.g., from a card effect)
        public void SetMaxHealth(int newMaxHealth, bool restoreToFull = false)
        {
            if (newMaxHealth <= 0) return; // Max health should be positive

            int oldMaxHealth = MaxHealth;
            MaxHealth = newMaxHealth;

            if (restoreToFull)
            {
                CurrentHealth = MaxHealth;
            }
            else
            {
                // Ensure current health doesn't exceed new max health
                CurrentHealth = Math.Min(CurrentHealth, MaxHealth);
            }

            if (oldMaxHealth != MaxHealth)
            {
                // TODO: Raise an event if needed (e.g., MaxHealthChangedEvent)
                // Example: RaiseDomainEvent(new LeaderMaxHealthChangedEvent(this.InstanceId, oldMaxHealth, MaxHealth));
            }
        }
    }
}