using System.Linq;
using Xunit;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Parser;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.AST;

namespace ShadowverseBeyond.Tests.Domain.Abilities.DSL
{
    /// <summary>
    /// DSL集成测试
    /// 测试从源代码到AST的完整流程
    /// </summary>
    public class DslIntegrationTests
    {
        private AbilityNode ParseAbilityFromSource(string source)
        {
            var lexer = new Lexer(source);
            var tokens = lexer.Tokenize();

            if (lexer.Errors.Any())
            {
                throw new System.Exception($"Lexer errors: {string.Join(", ", lexer.Errors.Select(e => e.Message))}");
            }

            var parser = new Parser(tokens);
            var ability = parser.ParseAbility();

            if (parser.Errors.Any())
            {
                throw new System.Exception($"Parser errors: {string.Join(", ", parser.Errors.Select(e => e.Message))}");
            }

            return ability;
        }

        [Fact]
        public void DslIntegration_ShouldParseSimpleFanfareAbility()
        {
            // Arrange
            var source = @"
                ability Fanfare : triggered {
                    trigger on play_card
                    target enemy
                    effect damage(target, 2)
                }";

            // Act
            var ability = ParseAbilityFromSource(source);

            // Assert
            Assert.Equal("Fanfare", ability.Name);
            Assert.Equal("triggered", ability.Type);
            Assert.NotNull(ability.Trigger);
            Assert.Single(ability.Trigger.EventTypes);
            Assert.Equal("play_card", ability.Trigger.EventTypes[0]);
            Assert.Single(ability.Targets);
            Assert.Equal("enemy", ability.Targets[0].Type);
            Assert.Single(ability.Effects);
            Assert.Equal("damage", ability.Effects[0].Type);
        }

        [Fact]
        public void DslIntegration_ShouldParseConditionalAbility()
        {
            // Arrange
            var source = @"
                ability ConditionalDamage {
                    trigger on play_card when self.zone == ""hand""
                    condition target.attack > 3 and target.health <= 5
                    target enemy.units where attack > 2
                    effect damage(target, self.attack)
                    priority = 100
                }";

            // Act
            var ability = ParseAbilityFromSource(source);

            // Assert
            Assert.Equal("ConditionalDamage", ability.Name);
            Assert.NotNull(ability.Trigger);
            Assert.NotNull(ability.Trigger.Condition);
            Assert.Single(ability.Conditions);
            Assert.Single(ability.Targets);
            Assert.Single(ability.Targets[0].Filters);
            Assert.Single(ability.Effects);
            Assert.Single(ability.Properties);
            Assert.Equal("priority", ability.Properties[0].Name);
        }

        [Fact]
        public void DslIntegration_ShouldParseLastWordsAbility()
        {
            // Arrange
            var source = @"
                ability LastWords : triggered {
                    trigger on card_destroyed when source == self
                    target ally.hand
                    effect summon(""Fairy"", 2)
                }";

            // Act
            var ability = ParseAbilityFromSource(source);

            // Assert
            Assert.Equal("LastWords", ability.Name);
            Assert.Equal("triggered", ability.Type);
            Assert.NotNull(ability.Trigger);
            Assert.Equal("card_destroyed", ability.Trigger.EventTypes[0]);
            Assert.NotNull(ability.Trigger.Condition);
            Assert.Single(ability.Targets);
            Assert.Equal("ally", ability.Targets[0].Type);
            Assert.Single(ability.Effects);
            Assert.Equal("summon", ability.Effects[0].Type);
            Assert.Equal(2, ability.Effects[0].Parameters.Count);
        }

        [Fact]
        public void DslIntegration_ShouldParseContinuousAbility()
        {
            // Arrange
            var source = @"
                ability Aura : continuous {
                    target ally.units where position.adjacent(self)
                    effect modify_attack(target, +1)
                    effect modify_health(target, +1)
                }";

            // Act
            var ability = ParseAbilityFromSource(source);

            // Assert
            Assert.Equal("Aura", ability.Name);
            Assert.Equal("continuous", ability.Type);
            Assert.Null(ability.Trigger); // Continuous abilities don't need triggers
            Assert.Single(ability.Targets);
            Assert.Equal("ally", ability.Targets[0].Type);
            Assert.Equal(2, ability.Effects.Count);
            Assert.All(ability.Effects, effect => Assert.StartsWith("modify_", effect.Type));
        }

        [Fact]
        public void DslIntegration_ShouldParseComplexExpression()
        {
            // Arrange
            var source = @"
                ability ComplexCondition {
                    condition (self.attack + self.health) > (enemy.leader.health / 2) and not target.keywords.contains(""Ward"")
                    target random.enemy.units where health <= self.attack * 2
                    effect damage(target, self.attack + 1)
                }";

            // Act
            var ability = ParseAbilityFromSource(source);

            // Assert
            Assert.Equal("ComplexCondition", ability.Name);
            Assert.Single(ability.Conditions);

            // Verify complex condition expression
            var condition = ability.Conditions[0];
            Assert.IsType<BinaryOperationNode>(condition.Expression);
            var logicalAnd = (BinaryOperationNode)condition.Expression;
            Assert.Equal(TokenType.And, logicalAnd.Operator);

            // Verify complex target filter
            Assert.Single(ability.Targets);
            Assert.Equal("random", ability.Targets[0].Type);
            Assert.Single(ability.Targets[0].Filters);

            // Verify complex effect parameter
            Assert.Single(ability.Effects);
            Assert.Equal("damage", ability.Effects[0].Type);
        }

        [Fact]
        public void DslIntegration_ShouldParseMultipleTargetsAndEffects()
        {
            // Arrange
            var source = @"
                ability MultiTarget {
                    trigger on evolve when source == self
                    target enemy.leader
                    effect damage(target, 2)
                    target ally.hand where type == ""follower""
                    effect modify_cost(target, -1)
                    target self
                    effect modify_attack(target, +2)
                    effect modify_health(target, +2)
                }";

            // Act
            var ability = ParseAbilityFromSource(source);

            // Assert
            Assert.Equal("MultiTarget", ability.Name);
            Assert.NotNull(ability.Trigger);
            Assert.Equal("evolve", ability.Trigger.EventTypes[0]);
            Assert.Equal(3, ability.Targets.Count);
            Assert.Equal(4, ability.Effects.Count);

            // Verify different target types
            Assert.Equal("enemy", ability.Targets[0].Type);
            Assert.Equal("ally", ability.Targets[1].Type);
            Assert.Equal("self", ability.Targets[2].Type);
        }

        [Fact]
        public void DslIntegration_ShouldParseAbilityWithComments()
        {
            // Arrange
            var source = @"
                // This is a Fanfare ability that deals damage
                ability CommentedAbility {
                    // Trigger when played from hand
                    trigger on play_card when self.zone == ""hand""
                    
                    /* Multi-line comment
                       explaining the target selection */
                    target enemy.units where attack > 3
                    
                    // Deal damage equal to self attack
                    effect damage(target, self.attack)
                }";

            // Act
            var ability = ParseAbilityFromSource(source);

            // Assert
            Assert.Equal("CommentedAbility", ability.Name);
            Assert.NotNull(ability.Trigger);
            Assert.Single(ability.Targets);
            Assert.Single(ability.Effects);
        }

        [Fact]
        public void DslIntegration_ShouldParseAbilityWithPropertyAccess()
        {
            // Arrange
            var source = @"
                ability PropertyAccess {
                    condition self.keywords.contains(""Rush"") and target.controller != self.controller
                    target enemy.units where health <= self.attack
                    effect damage(target, self.attack)
                }";

            // Act
            var ability = ParseAbilityFromSource(source);

            // Assert
            Assert.Equal("PropertyAccess", ability.Name);
            Assert.Single(ability.Conditions);

            // Verify property access in condition
            var condition = ability.Conditions[0].Expression;
            Assert.IsType<BinaryOperationNode>(condition);

            Assert.Single(ability.Targets);
            Assert.Single(ability.Effects);
        }

        [Fact]
        public void DslIntegration_ShouldParseAbilityWithFunctionCalls()
        {
            // Arrange
            var source = @"
                ability FunctionCalls {
                    condition enemy.units.count() >= 2 and ally.hand.count() <= 5
                    target random.enemy.units where not keywords.contains(""Ward"")
                    effect damage(target, min(self.attack, target.health))
                }";

            // Act
            var ability = ParseAbilityFromSource(source);

            // Assert
            Assert.Equal("FunctionCalls", ability.Name);
            Assert.Single(ability.Conditions);

            // Verify function calls in condition
            var condition = ability.Conditions[0].Expression;
            Assert.IsType<BinaryOperationNode>(condition);

            Assert.Single(ability.Targets);
            Assert.Single(ability.Effects);
        }

        [Fact]
        public void DslIntegration_ShouldHandleComplexNesting()
        {
            // Arrange
            var source = @"
                ability NestedLogic {
                    condition (self.attack > 5 and self.health > 5) or (enemy.units.count() == 0)
                    target enemy.units where (attack + health) <= (self.attack * 2)
                    effect damage(target, max(1, self.attack - target.health))
                }";

            // Act
            var ability = ParseAbilityFromSource(source);

            // Assert
            Assert.Equal("NestedLogic", ability.Name);
            Assert.Single(ability.Conditions);
            Assert.Single(ability.Targets);
            Assert.Single(ability.Effects);

            // Verify the structure was parsed correctly
            var condition = ability.Conditions[0].Expression;
            Assert.IsType<BinaryOperationNode>(condition);
            var orExpression = (BinaryOperationNode)condition;
            Assert.Equal(TokenType.Or, orExpression.Operator);
        }

        [Fact]
        public void DslIntegration_ShouldParseMinimalAbility()
        {
            // Arrange
            var source = @"
                ability Minimal {
                    effect draw(1)
                }";

            // Act
            var ability = ParseAbilityFromSource(source);

            // Assert
            Assert.Equal("Minimal", ability.Name);
            Assert.Equal("triggered", ability.Type); // default
            Assert.Null(ability.Trigger);
            Assert.Empty(ability.Conditions);
            Assert.Empty(ability.Targets);
            Assert.Single(ability.Effects);
            Assert.Equal("draw", ability.Effects[0].Type);
        }
    }
}
