using System;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.AST;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Parser;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation
{
    public static class DslTraceFormatter
    {
        public static string Format(string expression, IDslTraceCollector? collector)
        {
            if (string.IsNullOrWhiteSpace(expression) || collector == null || collector.Values.Count == 0)
            {
                return expression;
            }

            try
            {
                var lexer = new Lexer(expression);
                var tokens = lexer.Tokenize();
                var parser = new ShadowverseBeyond.Server.Domain.Abilities.DSL.Parser.Parser(tokens);
                var ast = parser.ParseSingleExpression();
                if (ast == null)
                {
                    return expression;
                }

                return FormatNode(ast, collector);
            }
            catch
            {
                return expression;
            }
        }

        private static string FormatNode(ExpressionNode node, IDslTraceCollector collector)
        {
            switch (node)
            {
                case ParenthesizedExpressionNode parenthesized:
                    return $"({FormatNode(parenthesized.Inner, collector)})";
                case BinaryOperationNode binary:
                    return $"{FormatNode(binary.Left, collector)} {FormatOperator(binary.Operator)} {FormatNode(binary.Right, collector)}";
                case UnaryOperationNode unary:
                    return unary.IsPrefix
                        ? $"{FormatOperator(unary.Operator)}{FormatNode(unary.Operand, collector)}"
                        : $"{FormatNode(unary.Operand, collector)}{FormatOperator(unary.Operator)}";
                case FunctionCallNode func:
                    {
                        var args = string.Join(", ", func.Arguments.Select(arg => FormatNode(arg, collector)));
                        if (func.IsMethodCall && func.Receiver != null)
                        {
                            var receiver = FormatNode(func.Receiver, collector);
                            return $"{receiver}.{func.FunctionName}({args})";
                        }
                        return $"{func.FunctionName}({args})";
                    }
                case MemberAccessNode member:
                    return FormatValueOrFallback(member.ToString(), collector);
                case IdentifierNode identifier:
                    return FormatValueOrFallback(identifier.ToString(), collector);
                case LiteralNode literal:
                    return literal.Value is string str ? $"\"{str}\"" : literal.Value?.ToString() ?? "null";
                default:
                    return FormatValueOrFallback(node.ToString(), collector);
            }
        }

        private static string FormatValueOrFallback(string expression, IDslTraceCollector collector)
        {
            if (collector.TryGetValue(expression, out var value))
            {
                return $"{expression}->({value.Display})";
            }

            return expression;
        }

        private static string FormatOperator(TokenType op)
        {
            return op switch
            {
                TokenType.Plus => "+",
                TokenType.Minus => "-",
                TokenType.Multiply => "*",
                TokenType.Divide => "/",
                TokenType.DivideCeil => "/^",
                TokenType.Modulo => "%",
                TokenType.Equal => "==",
                TokenType.NotEqual => "!=",
                TokenType.GreaterThan => ">",
                TokenType.GreaterEqual => ">=",
                TokenType.LessThan => "<",
                TokenType.LessEqual => "<=",
                TokenType.And => "&&",
                TokenType.Or => "||",
                TokenType.Not => "!",
                TokenType.SetUnion => "UNION",
                TokenType.SetIntersect => "INTERSECT",
                TokenType.SetDifference => "\\",
                _ => op.ToString()
            };
        }
    }
}
