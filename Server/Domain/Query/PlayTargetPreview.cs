using System;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.AST;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Parser;

namespace ShadowverseBeyond.Server.Domain.Query
{
    internal static class PlayTargetPreview
    {
        public static bool CanPreviewCondition(string expression, bool allowBloodPool = false)
            => IsKnownValue(Parse(expression), cardFilter: false, allowBloodPool);

        public static bool CanPreviewQuery(QueryConfig query, bool entersField)
            => query.Operations is not { Count: > 0 }
                && IsVisiblePool(Parse(query.SourcePoolExpression), entersField);

        private static ExpressionNode? Parse(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression)) return null;
            var lexer = new Lexer(expression);
            return new Parser(lexer.Tokenize()).ParseSingleExpression();
        }

        private static bool IsVisiblePool(ExpressionNode? expression, bool entersField) => expression switch
        {
            ParenthesizedExpressionNode group => IsVisiblePool(group.Inner, entersField),
            QueryFilterNode filter => IsVisiblePool(filter.Source, entersField) && IsKnownValue(filter.Filter, cardFilter: true),
            MemberAccessNode member => member.ToString().ToLowerInvariant() switch
            {
                "context.owner.hand" or "context.opponent.field" => true,
                "context.owner.field" or "context.currentgamestate.allfield" => !entersField,
                _ => false
            },
            _ => false
        };

        // Candidate filters may read only the current visible card and the declared cost.
        // Conditions that depend on zones, counters, event history or a random result wait for settlement.
        private static bool IsKnownValue(ExpressionNode? expression, bool cardFilter, bool allowBloodPool = false) => expression switch
        {
            LiteralNode => true,
            ParenthesizedExpressionNode group => IsKnownValue(group.Inner, cardFilter, allowBloodPool),
            BinaryOperationNode binary => IsKnownValue(binary.Left, cardFilter, allowBloodPool)
                && IsKnownValue(binary.Right, cardFilter, allowBloodPool),
            UnaryOperationNode unary => IsKnownValue(unary.Operand, cardFilter, allowBloodPool),
            FunctionCallNode { IsMethodCall: false } call when call.FunctionName.Equals("contains", StringComparison.OrdinalIgnoreCase)
                => call.Arguments.All(argument => IsKnownValue(argument, cardFilter, allowBloodPool)),
            MemberAccessNode member => member.ToString().ToLowerInvariant() switch
            {
                "context.triggeringevent.paidcost" => true,
                "context.owner.bloodpool" => allowBloodPool,
                "self.type" or "self.cardtype" or "self.tribe" or "self.instanceid" or "self.currentdefinition.id"
                    or "self.currentattack" or "self.currenthealth" or "self.currentcost" or "self.keywords" => cardFilter,
                _ => false
            },
            _ => false
        };
    }
}
