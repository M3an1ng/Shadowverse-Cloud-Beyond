using ShadowverseBeyond.Server.Domain.Abilities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.AST;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Parser;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services;

// Only called for recovery routes whose enabled effects have already been checked.
internal static class AiRecoveryReactionScope
{
    internal static bool CannotReact(AbilityConfig ability, CardInstance source, bool evolves, bool plays,
        bool stableField)
    {
        var trigger = ability.Trigger;
        return trigger.EventTypes.Count > 0 && trigger.EventTypes.All(type => type switch
        {
            EventType.Attack or EventType.SuperEvolve or EventType.Act => true,
            EventType.Evolve => !evolves || trigger.RelationType is TriggerRelationType.TargetSelf or TriggerRelationType.SourceSelf,
            EventType.Summon => !evolves || !plays || trigger.RelationType == TriggerRelationType.TargetSelf,
            EventType.Destroy => !evolves && stableField,
            EventType.PlayCard => !plays || trigger.RelationType is TriggerRelationType.TargetSelf or TriggerRelationType.SourceSelf
                || ability.Conditions.Any(condition => condition.IsEnabled && ExcludesPlayedId(condition.Expression, source.CurrentDefinitionId)),
            _ => false
        });
    }

    private static bool ExcludesPlayedId(string expression, int cardId)
    {
        // Accept only an equality on the event's known definition ID, never execute an arbitrary formula.
        if (string.IsNullOrWhiteSpace(expression) || expression.Length > 240) return false;
        var lexer = new Lexer(expression);
        var tokens = lexer.Tokenize();
        if (lexer.Errors.Count != 0) return false;
        var node = Unwrap(new Parser(tokens).ParseSingleExpression());
        return node is BinaryOperationNode { Operator: TokenType.Equal } equal
            && (DifferentId(equal.Left, equal.Right, cardId) || DifferentId(equal.Right, equal.Left, cardId));
    }

    private static bool DifferentId(ExpressionNode left, ExpressionNode right, int cardId) =>
        Unwrap(left) is MemberAccessNode member
        && string.Equals(member.ToString(), "context.triggeringEvent.PlayedCard.currentDefinition.id", StringComparison.OrdinalIgnoreCase)
        && Unwrap(right) is LiteralNode { Value: int expected } && expected > 0 && expected != cardId;

    private static ExpressionNode? Unwrap(ExpressionNode? node)
    {
        while (node is ParenthesizedExpressionNode parentheses) node = parentheses.Inner;
        return node;
    }
}
