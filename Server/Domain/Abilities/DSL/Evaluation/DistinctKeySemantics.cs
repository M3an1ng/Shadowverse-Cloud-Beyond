using System;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.AST;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation
{
    public enum DistinctKeySemantics
    {
        Auto = 0,
        Scalar = 1,
        ListExpanded = 2,
        TribeExpanded = 3
    }

    internal static class DistinctKeySemanticsClassifier
    {
        public static DistinctKeySemantics Classify(ExpressionNode keyExpression, Type? staticKeyType = null)
        {
            if (IsTribeKeyExpression(keyExpression))
            {
                return DistinctKeySemantics.TribeExpanded;
            }

            if (staticKeyType == null || staticKeyType == typeof(object))
            {
                return DistinctKeySemantics.Auto;
            }

            return DslCollectionCoercion.IsCollectionType(staticKeyType)
                ? DistinctKeySemantics.ListExpanded
                : DistinctKeySemantics.Scalar;
        }

        private static bool IsTribeKeyExpression(ExpressionNode? expression)
        {
            return expression switch
            {
                IdentifierNode identifier => IsTribeName(identifier.Name) || IsTribeName(identifier.Path.Count > 0 ? identifier.Path[^1] : null),
                MemberAccessNode memberAccess => IsTribeName(memberAccess.Member?.Name),
                ParenthesizedExpressionNode parenthesized => IsTribeKeyExpression(parenthesized.Inner),
                _ => false
            };
        }

        private static bool IsTribeName(string? name)
        {
            return string.Equals(name, "Tribe", StringComparison.OrdinalIgnoreCase);
        }
    }
}
