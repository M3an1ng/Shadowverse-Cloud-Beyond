using ShadowverseBeyond.Server.Domain.Entities;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Domain.Abilities
{
    /// <summary>
    /// Test card definitions with abilities for testing the card ability management system.
    /// These can be used to verify that the ability system works correctly.
    /// </summary>
    public static class AbilityTestCards
    {
        /// <summary>
        /// Creates a test card with Rush ability.
        /// </summary>
        public static CardDefinition CreateRushTestCard()
        {
            return new CardDefinition
            {
                Id = 9001,
                Name = "Rush Goblin",
                Type = "Follower",
                Cost = 2,
                Attack = 2,
                Health = 1,
                Description = "A quick goblin that can attack followers immediately.",
                Keywords = new List<string> { "Rush" },
                IsCollectible = true,
                IsEvolved = false
            };
        }

        /// <summary>
        /// Creates a test card with Storm ability.
        /// </summary>
        public static CardDefinition CreateStormTestCard()
        {
            return new CardDefinition
            {
                Id = 9002,
                Name = "Storm Dragon",
                Type = "Follower",
                Cost = 5,
                Attack = 4,
                Health = 4,
                Description = "A mighty dragon that can attack any target immediately.",
                Keywords = new List<string> { "Storm" },
                IsCollectible = true,
                IsEvolved = false
            };
        }

        /// <summary>
        /// Creates a test card with Ward ability.
        /// </summary>
        public static CardDefinition CreateWardTestCard()
        {
            return new CardDefinition
            {
                Id = 9003,
                Name = "Guardian Knight",
                Type = "Follower",
                Cost = 3,
                Attack = 2,
                Health = 4,
                Description = "A protective knight that must be attacked first.",
                Keywords = new List<string> { "Ward" },
                IsCollectible = true,
                IsEvolved = false
            };
        }

        /// <summary>
        /// Creates a test card with Drain ability.
        /// </summary>
        public static CardDefinition CreateDrainTestCard()
        {
            return new CardDefinition
            {
                Id = 9004,
                Name = "Vampire Lord",
                Type = "Follower",
                Cost = 4,
                Attack = 3,
                Health = 3,
                Description = "A vampire that heals its controller when dealing damage.",
                Keywords = new List<string> { "Drain" },
                IsCollectible = true,
                IsEvolved = false
            };
        }

        /// <summary>
        /// Creates a test card with Fanfare ability.
        /// </summary>
        public static CardDefinition CreateFanfareTestCard()
        {
            return new CardDefinition
            {
                Id = 9005,
                Name = "Fire Mage",
                Type = "Follower",
                Cost = 3,
                Attack = 2,
                Health = 2,
                Description = "Fanfare: Deal 2 damage to the enemy leader.",
                Keywords = new List<string>(),
                IsCollectible = true,
                IsEvolved = false
            };
        }

        /// <summary>
        /// Creates a test card with Last Words ability.
        /// </summary>
        public static CardDefinition CreateLastWordsTestCard()
        {
            return new CardDefinition
            {
                Id = 9006,
                Name = "Explosive Goblin",
                Type = "Follower",
                Cost = 2,
                Attack = 1,
                Health = 1,
                Description = "Last Words: Deal 1 damage to all enemies.",
                Keywords = new List<string>(),
                IsCollectible = true,
                IsEvolved = false
            };
        }

        /// <summary>
        /// Creates a test card with multiple abilities.
        /// </summary>
        public static CardDefinition CreateMultiAbilityTestCard()
        {
            return new CardDefinition
            {
                Id = 9007,
                Name = "Elite Warrior",
                Type = "Follower",
                Cost = 4,
                Attack = 3,
                Health = 3,
                Description = "Fanfare: Draw a card. A versatile warrior with protective training.",
                Keywords = new List<string> { "Ward", "Drain" },
                IsCollectible = true,
                IsEvolved = false
            };
        }

        /// <summary>
        /// Creates a test spell card with immediate effect.
        /// </summary>
        public static CardDefinition CreateSpellTestCard()
        {
            return new CardDefinition
            {
                Id = 9008,
                Name = "Lightning Bolt",
                Type = "Spell",
                Cost = 2,
                Attack = null,
                Health = null,
                Description = "Deal 3 damage to any target.",
                Keywords = new List<string>(),
                IsCollectible = true,
                IsEvolved = false
            };
        }

        /// <summary>
        /// Gets all test cards for easy registration in tests.
        /// </summary>
        public static List<CardDefinition> GetAllTestCards()
        {
            return new List<CardDefinition>
            {
                CreateRushTestCard(),
                CreateStormTestCard(),
                CreateWardTestCard(),
                CreateDrainTestCard(),
                CreateFanfareTestCard(),
                CreateLastWordsTestCard(),
                CreateMultiAbilityTestCard(),
                CreateSpellTestCard()
            };
        }
    }
}
