using System;
using System.Collections.Generic;
using Xunit;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.AST;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Parser;

namespace ShadowverseBeyond.Tests.Domain.Abilities.DSL
{
    /// <summary>
    /// AST节点单元测试
    /// </summary>
    public class AstNodeTests
    {
        [Fact]
        public void LiteralNode_ShouldCreateCorrectly()
        {
            // Arrange & Act
            var node = new LiteralNode
            {
                Value = 42,
                DataType = "int"
            };

            // Assert
            Assert.Equal("Literal", node.NodeType);
            Assert.Equal(42, node.Value);
            Assert.Equal("int", node.DataType);
            Assert.True(node.IsConstant);
            Assert.Equal(42, node.GetConstantValue());
        }

        [Fact]
        public void IdentifierNode_ShouldCreateCorrectly()
        {
            // Arrange & Act
            var node = new IdentifierNode
            {
                Name = "self",
                Path = new List<string> { "self", "attack" }
            };

            // Assert
            Assert.Equal("Identifier", node.NodeType);
            Assert.Equal("self", node.Name);
            Assert.Equal(2, node.Path.Count);
            Assert.Equal("self.attack", node.ToString());
        }

        [Fact]
        public void BinaryOperationNode_ShouldCreateCorrectly()
        {
            // Arrange
            var left = new LiteralNode { Value = 5 };
            var right = new LiteralNode { Value = 3 };

            // Act
            var node = new BinaryOperationNode
            {
                Left = left,
                Operator = TokenType.Plus,
                Right = right
            };

            // Assert
            Assert.Equal("BinaryOperation", node.NodeType);
            Assert.Equal(TokenType.Plus, node.Operator);
            Assert.Equal(2, node.Children.Count);
            Assert.Contains(left, node.Children);
            Assert.Contains(right, node.Children);
            Assert.True(node.IsConstant);
        }

        [Fact]
        public void UnaryOperationNode_ShouldCreateCorrectly()
        {
            // Arrange
            var operand = new LiteralNode { Value = 5 };

            // Act
            var node = new UnaryOperationNode
            {
                Operator = TokenType.Minus,
                Operand = operand,
                IsPrefix = true
            };

            // Assert
            Assert.Equal("UnaryOperation", node.NodeType);
            Assert.Equal(TokenType.Minus, node.Operator);
            Assert.True(node.IsPrefix);
            Assert.Single(node.Children);
            Assert.Contains(operand, node.Children);
            Assert.True(node.IsConstant);
        }

        [Fact]
        public void FunctionCallNode_ShouldCreateCorrectly()
        {
            // Arrange
            var arg1 = new LiteralNode { Value = "target" };
            var arg2 = new LiteralNode { Value = 3 };

            // Act
            var node = new FunctionCallNode
            {
                FunctionName = "damage",
                Arguments = new List<ExpressionNode> { arg1, arg2 }
            };

            // Assert
            Assert.Equal("FunctionCall", node.NodeType);
            Assert.Equal("damage", node.FunctionName);
            Assert.Equal(2, node.Arguments.Count);
            Assert.Equal(2, node.Children.Count);
            Assert.False(node.IsMethodCall);
        }

        [Fact]
        public void FunctionCallNode_WithReceiver_ShouldCreateCorrectly()
        {
            // Arrange
            var receiver = new IdentifierNode { Name = "self" };
            var arg = new LiteralNode { Value = 2 };

            // Act
            var node = new FunctionCallNode
            {
                FunctionName = "heal",
                Receiver = receiver,
                Arguments = new List<ExpressionNode> { arg },
                IsMethodCall = true
            };

            // Assert
            Assert.Equal("FunctionCall", node.NodeType);
            Assert.Equal("heal", node.FunctionName);
            Assert.True(node.IsMethodCall);
            Assert.Equal(2, node.Children.Count); // receiver + 1 argument
            Assert.Contains(receiver, node.Children);
        }

        [Fact]
        public void AbilityNode_ShouldCreateCorrectly()
        {
            // Arrange & Act
            var node = new AbilityNode
            {
                Name = "TestAbility",
                Type = "triggered",
                Tag = "Fanfare"
            };

            // Assert
            Assert.Equal("Ability", node.NodeType);
            Assert.Equal("TestAbility", node.Name);
            Assert.Equal("triggered", node.Type);
            Assert.Equal("Fanfare", node.Tag);
            Assert.Empty(node.Children); // No children added yet
        }

        [Fact]
        public void TriggerNode_ShouldCreateCorrectly()
        {
            // Arrange
            var condition = new BinaryOperationNode
            {
                Left = new IdentifierNode { Name = "self" },
                Operator = TokenType.Equal,
                Right = new LiteralNode { Value = "hand" }
            };

            // Act
            var node = new TriggerNode
            {
                EventTypes = new List<string> { "play_card" },
                Scope = "self",
                Timing = "after",
                Condition = condition
            };

            // Assert
            Assert.Equal("Trigger", node.NodeType);
            Assert.Single(node.EventTypes);
            Assert.Equal("play_card", node.EventTypes[0]);
            Assert.Equal("self", node.Scope);
            Assert.Equal("after", node.Timing);
            Assert.Single(node.Children);
            Assert.Contains(condition, node.Children);
        }

        [Fact]
        public void ConditionNode_ShouldCreateCorrectly()
        {
            // Arrange
            var expression = new BinaryOperationNode
            {
                Left = new IdentifierNode { Name = "target", Path = new List<string> { "target", "attack" } },
                Operator = TokenType.GreaterThan,
                Right = new LiteralNode { Value = 3 }
            };

            // Act
            var node = new ConditionNode
            {
                Expression = expression,
                LogicOperator = "and",
                Negate = false
            };

            // Assert
            Assert.Equal("Condition", node.NodeType);
            Assert.Equal("and", node.LogicOperator);
            Assert.False(node.Negate);
            Assert.Single(node.Children);
            Assert.Contains(expression, node.Children);
        }

        [Fact]
        public void TargetNode_ShouldCreateCorrectly()
        {
            // Arrange
            var filter = new FilterNode
            {
                Property = "attack",
                Operator = ">",
                Value = new LiteralNode { Value = 3 }
            };

            // Act
            var node = new TargetNode
            {
                Type = "enemy",
                Scope = "units",
                Selection = "random",
                Count = new LiteralNode { Value = 1 },
                Filters = new List<FilterNode> { filter }
            };

            // Assert
            Assert.Equal("Target", node.NodeType);
            Assert.Equal("enemy", node.Type);
            Assert.Equal("units", node.Scope);
            Assert.Equal("random", node.Selection);
            Assert.Single(node.Filters);
            Assert.Equal(2, node.Children.Count); // Count + 1 filter
        }

        [Fact]
        public void EffectNode_ShouldCreateCorrectly()
        {
            // Arrange
            var target = new TargetNode { Type = "enemy" };
            var amount = new LiteralNode { Value = 2 };
            var parameter = new ParameterNode
            {
                Name = "type",
                Value = new LiteralNode { Value = "fire" }
            };

            // Act
            var node = new EffectNode
            {
                Type = "damage",
                Target = target,
                Amount = amount,
                Parameters = new List<ParameterNode> { parameter },
                ExecutionOrder = 1
            };

            // Assert
            Assert.Equal("Effect", node.NodeType);
            Assert.Equal("damage", node.Type);
            Assert.Equal(1, node.ExecutionOrder);
            Assert.Equal(3, node.Children.Count); // target + amount + 1 parameter
        }

        [Fact]
        public void PropertyNode_ShouldCreateCorrectly()
        {
            // Arrange & Act
            var node = new PropertyNode
            {
                Name = "priority",
                Value = new LiteralNode { Value = 100 }
            };

            // Assert
            Assert.Equal("Property", node.NodeType);
            Assert.Equal("priority", node.Name);
            Assert.Single(node.Children);
        }

        [Fact]
        public void FilterNode_ShouldCreateCorrectly()
        {
            // Arrange & Act
            var node = new FilterNode
            {
                Property = "health",
                Operator = "<=",
                Value = new LiteralNode { Value = 2 },
                LogicOperator = "and"
            };

            // Assert
            Assert.Equal("Filter", node.NodeType);
            Assert.Equal("health", node.Property);
            Assert.Equal("<=", node.Operator);
            Assert.Equal("and", node.LogicOperator);
            Assert.Single(node.Children);
        }

        [Fact]
        public void ParameterNode_ShouldCreateCorrectly()
        {
            // Arrange & Act
            var node = new ParameterNode
            {
                Name = "cardId",
                Value = new LiteralNode { Value = 12345 }
            };

            // Assert
            Assert.Equal("Parameter", node.NodeType);
            Assert.Equal("cardId", node.Name);
            Assert.Single(node.Children);
        }

        [Fact]
        public void ConditionalExpressionNode_ShouldCreateCorrectly()
        {
            // Arrange
            var condition = new BinaryOperationNode
            {
                Left = new IdentifierNode { Name = "x" },
                Operator = TokenType.GreaterThan,
                Right = new LiteralNode { Value = 0 }
            };
            var trueExpr = new LiteralNode { Value = "positive" };
            var falseExpr = new LiteralNode { Value = "negative" };

            // Act
            var node = new ConditionalExpressionNode
            {
                Condition = condition,
                TrueExpression = trueExpr,
                FalseExpression = falseExpr
            };

            // Assert
            Assert.Equal("ConditionalExpression", node.NodeType);
            Assert.Equal(3, node.Children.Count);
            Assert.Contains(condition, node.Children);
            Assert.Contains(trueExpr, node.Children);
            Assert.Contains(falseExpr, node.Children);
        }

        [Fact]
        public void ArrayAccessNode_ShouldCreateCorrectly()
        {
            // Arrange
            var array = new IdentifierNode { Name = "units" };
            var index = new LiteralNode { Value = 0 };

            // Act
            var node = new ArrayAccessNode
            {
                Array = array,
                Index = index
            };

            // Assert
            Assert.Equal("ArrayAccess", node.NodeType);
            Assert.Equal(2, node.Children.Count);
            Assert.Contains(array, node.Children);
            Assert.Contains(index, node.Children);
            Assert.Equal("units[0]", node.ToString());
        }

        [Fact]
        public void BlockNode_ShouldCreateCorrectly()
        {
            // Arrange
            var stmt1 = new LiteralNode { Value = 1 };
            var stmt2 = new LiteralNode { Value = 2 };

            // Act
            var node = new BlockNode
            {
                Statements = new List<ExpressionNode> { stmt1, stmt2 }
            };

            // Assert
            Assert.Equal("Block", node.NodeType);
            Assert.Equal(2, node.Statements.Count);
            Assert.Equal(2, node.Children.Count);
        }
    }
}
