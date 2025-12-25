using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ValueType = System.ValueType;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.AST;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Parser;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation
{
    public class DslEvaluator
    {
        private readonly ILogger<DslEvaluator> _logger;
        private readonly IFunctionRegistry _functionRegistry;

        public DslEvaluator(ILogger<DslEvaluator> logger, IFunctionRegistry functionRegistry)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _functionRegistry = functionRegistry ?? throw new ArgumentNullException(nameof(functionRegistry));
        }

        public EvaluationResult Evaluate(ExpressionNode astNode, GameContext context)
        {
            if (astNode == null)
            {
                _logger.LogWarning("Attempted to evaluate a null AST node.");
                return EvaluationResult.Failure("Cannot evaluate a null expression.");
            }
            if (context == null)
            {
                _logger.LogError("GameContext cannot be null.");
                return EvaluationResult.Failure("GameContext is required.");
            }

            _logger.LogTrace("Starting evaluation for AST node type: {NodeType}", astNode.GetType().Name);
            try
            {
                return VisitNode(astNode, context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception during DSL evaluation for node type {NodeType}.", astNode.GetType().Name);
                return EvaluationResult.Failure($"Unhandled exception during evaluation: {ex.Message}");
            }
        }

        private EvaluationResult VisitNode(ExpressionNode node, GameContext context)
        {
            return node switch
            {
                LiteralNode literal => VisitLiteral(literal, context),
                IdentifierNode identifier => VisitIdentifier(identifier, context),
                BinaryOperationNode binaryOp => VisitBinaryOperator(binaryOp, context),
                UnaryOperationNode unaryOp => VisitUnaryOperator(unaryOp, context),
                FunctionCallNode funcCall => VisitFunctionCall(funcCall, context),
                MemberAccessNode memberAccess => VisitMemberAccess(memberAccess, context),
                _ => HandleUnsupportedNode(node)
            };
        }

        private EvaluationResult VisitMemberAccess(MemberAccessNode node, GameContext context)
        {
            _logger.LogTrace("Visiting MemberAccessNode. Target: {Target}, Member: {Member}", node.Target, node.Member.Name);

            // 1. Evaluate the target object first
            var targetResult = VisitNode(node.Target, context);
            if (!targetResult.IsSuccess)
            {
                _logger.LogWarning("Failed to evaluate target of member access: {Target}", node.Target);
                return targetResult; // Propagate failure
            }

            object? targetObject = targetResult.Value;
            if (targetObject == null)
            {
                _logger.LogWarning("Target of member access '{Member}' evaluated to null.", node.Member.Name);
                return EvaluationResult.Failure($"Cannot access member '{node.Member.Name}' on a null object.");
            }

            // 2. Resolve the member on the target object using the existing reflection logic
            var memberName = node.Member.Name;
            object? resolvedValue = ResolvePathSegments(targetObject, new List<string> { memberName });

            if (resolvedValue != null)
            {
                _logger.LogTrace("Member '{Member}' on target '{Target}' resolved successfully. Value: {Value}", memberName, node.Target, resolvedValue);
                return EvaluationResult.Success(resolvedValue);
            }

            // Special handling for CardInstance.AbilityState, mimicking logic from ResolveIdentifier
            if (targetObject is CardInstance sourceCard &&
                memberName.Equals("state", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Accessing '.state' directly is ambiguous. Use state variables directly on 'self', e.g., 'self.myVar'.");
                return EvaluationResult.Failure("Direct access to '.state' is not recommended. Access state variables directly on the card.");
            }

            _logger.LogWarning("Member '{Member}' not found on target type '{TargetType}'.", memberName, targetObject.GetType().Name);
            return EvaluationResult.Failure($"Member '{memberName}' not found on target of type '{targetObject.GetType().Name}'.");
        }

        private EvaluationResult VisitLiteral(LiteralNode node, GameContext context)
        {
            _logger.LogTrace("Visiting LiteralNode. Value: {Value}, Type: {Type}", node.Value ?? "null", node.Value?.GetType().Name ?? "null");
            return EvaluationResult.Success(node.Value);
        }

        private EvaluationResult VisitIdentifier(IdentifierNode node, GameContext context)
        {
            _logger.LogTrace("Visiting IdentifierNode. Path: {Path}", string.Join(".", node.Path));
            object? resolvedValue = ResolveIdentifier(node.Path, context);

            if (resolvedValue != null)
            {
                _logger.LogTrace("Identifier '{Path}' resolved successfully. Value: {Value}", string.Join(".", node.Path), resolvedValue);
                return EvaluationResult.Success(resolvedValue);
            }
            else
            {
                _logger.LogWarning("Identifier not found or path invalid: {Path}", string.Join(".", node.Path));
                return EvaluationResult.Failure($"Identifier not found or path invalid: {string.Join(".", node.Path)}");
            }
        }

        private EvaluationResult VisitBinaryOperator(BinaryOperationNode node, GameContext context)
        {
            _logger.LogTrace("Visiting BinaryOperationNode. Operator: {Operator}", node.Operator);
            var leftResult = VisitNode(node.Left, context);
            if (!leftResult.IsSuccess) return leftResult;

            var rightResult = VisitNode(node.Right, context);
            if (!rightResult.IsSuccess) return rightResult;

            try
            {
                object? resultValue = PerformBinaryOperation(leftResult.Value, rightResult.Value, node.Operator);
                _logger.LogTrace("Binary operation {Operator} successful. Result: {Result}", node.Operator, resultValue ?? "null");
                return EvaluationResult.Success(resultValue);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is DivideByZeroException || ex is ArgumentException || ex is OverflowException)
            {
                _logger.LogWarning("Binary operation failed ({Operator}) between '{LeftValue}' ({LeftType}) and '{RightValue}' ({RightType}). Error: {Error}",
                    node.Operator, leftResult.Value ?? "null", leftResult.Value?.GetType().Name ?? "null",
                    rightResult.Value ?? "null", rightResult.Value?.GetType().Name ?? "null", ex.Message);
                return EvaluationResult.Failure($"Binary operation failed: {ex.Message}");
            }
        }

        private EvaluationResult VisitUnaryOperator(UnaryOperationNode node, GameContext context)
        {
            _logger.LogTrace("Visiting UnaryOperationNode. Operator: {Operator}", node.Operator);
            var operandResult = VisitNode(node.Operand, context);
            if (!operandResult.IsSuccess) return operandResult;

            try
            {
                object? resultValue = PerformUnaryOperation(operandResult.Value, node.Operator);
                _logger.LogTrace("Unary operation {Operator} successful. Result: {Result}", node.Operator, resultValue ?? "null");
                return EvaluationResult.Success(resultValue);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Unary operation failed ({Operator}) for value '{Value}' ({Type}). Error: {Error}",
                    node.Operator, operandResult.Value ?? "null", operandResult.Value?.GetType().Name ?? "null", ex.Message);
                return EvaluationResult.Failure($"Unary operation failed: {ex.Message}");
            }
        }

        private EvaluationResult VisitFunctionCall(FunctionCallNode node, GameContext context)
        {
            _logger.LogTrace("Visiting FunctionCallNode. Function: {FunctionName}, Args: {ArgCount}", node.FunctionName, node.Arguments.Count);
            var function = FindFunction(node.FunctionName, context);
            if (function == null)
            {
                _logger.LogWarning("Function not found: {FunctionName}", node.FunctionName);
                return EvaluationResult.Failure($"Function not found: {node.FunctionName}");
            }

            var evaluatedArgs = new List<object?>();
            foreach (var argNode in node.Arguments)
            {
                var argResult = VisitNode(argNode, context);
                if (!argResult.IsSuccess)
                {
                    _logger.LogWarning("Argument evaluation failed for function '{FunctionName}'. Error: {Error}", node.FunctionName, string.Join("; ", argResult.Errors));
                    return EvaluationResult.Failure($"Argument evaluation failed for function '{node.FunctionName}': {string.Join("; ", argResult.Errors)}");
                }
                evaluatedArgs.Add(argResult.Value);
            }
            _logger.LogTrace("Arguments evaluated for function '{FunctionName}'. Count: {Count}", node.FunctionName, evaluatedArgs.Count);

            if (!function.ValidateArguments(evaluatedArgs))
            {
                _logger.LogWarning("Invalid arguments provided for function: {FunctionName}", node.FunctionName);
                return EvaluationResult.Failure($"Invalid arguments for function: {node.FunctionName}");
            }

            try
            {
                object? resultValue = function.Execute(evaluatedArgs, context);
                _logger.LogTrace("Function '{FunctionName}' executed successfully. Result: {Result}", node.FunctionName, resultValue ?? "null");
                return EvaluationResult.Success(resultValue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing function '{FunctionName}'.", node.FunctionName);
                return EvaluationResult.Failure($"Error executing function '{node.FunctionName}': {ex.Message}");
            }
        }

        private EvaluationResult HandleUnsupportedNode(ExpressionNode node)
        {
            string errorMsg = $"Unsupported AST node type encountered during evaluation: {node.GetType().Name}";
            _logger.LogError(errorMsg);
            return EvaluationResult.Failure(errorMsg);
        }

        private object? PerformBinaryOperation(object? left, object? right, TokenType operatorType)
        {
            _logger.LogTrace("Performing binary operation {Operator} on '{Left}' ({LeftType}) and '{Right}' ({RightType})",
                operatorType, left ?? "null", left?.GetType().Name ?? "null", right ?? "null", right?.GetType().Name ?? "null");

            switch (operatorType)
            {
                case TokenType.Plus:
                    return DynamicOperations.Add(left, right);
                case TokenType.Minus:
                    return DynamicOperations.Subtract(left, right);
                case TokenType.Multiply:
                    return DynamicOperations.Multiply(left, right);
                case TokenType.Divide:
                    return DynamicOperations.Divide(left, right);
                case TokenType.Modulo:
                    return DynamicOperations.Modulo(left, right);
                case TokenType.Equal:
                    return DynamicOperations.AreEqual(left, right);
                case TokenType.NotEqual:
                    return !DynamicOperations.AreEqual(left, right);
                case TokenType.GreaterThan:
                    return DynamicOperations.IsGreaterThan(left, right);
                case TokenType.GreaterEqual:
                    return DynamicOperations.IsGreaterThanOrEqual(left, right);
                case TokenType.LessThan:
                    return DynamicOperations.IsLessThan(left, right);
                case TokenType.LessEqual:
                    return DynamicOperations.IsLessThanOrEqual(left, right);
                case TokenType.And:
                    return DynamicOperations.LogicalAnd(left, right);
                case TokenType.Or:
                    return DynamicOperations.LogicalOr(left, right);
                default:
                    throw new InvalidOperationException($"Unsupported binary operator: {operatorType}");
            }
        }

        private object? PerformUnaryOperation(object? operand, TokenType operatorType)
        {
            _logger.LogTrace("Performing unary operation {Operator} on '{Operand}' ({OperandType})",
                operatorType, operand ?? "null", operand?.GetType().Name ?? "null");

            switch (operatorType)
            {
                case TokenType.Minus:
                    return DynamicOperations.Negate(operand);
                case TokenType.Not:
                    return DynamicOperations.LogicalNot(operand);
                default:
                    throw new InvalidOperationException($"Unsupported unary operator: {operatorType}");
            }
        }

        private object? ResolveIdentifier(List<string> path, GameContext context)
        {
            if (path == null || !path.Any())
            {
                _logger.LogWarning("ResolveIdentifier called with null or empty path.");
                return null;
            }

            string firstSegment = path.First();
            string firstSegmentLower = firstSegment.ToLowerInvariant();
            object? baseObjectForPathResolution = null;
            List<string> pathSegmentsToResolve;

            // Priority 1: Explicit top-level keywords
            switch (firstSegmentLower)
            {
                case "player":
                    baseObjectForPathResolution = context.GetRequestingPlayer();
                    pathSegmentsToResolve = path.Skip(1).ToList();
                    _logger.LogTrace("Resolved '{Segment}' to RequestingPlayer.", firstSegment);
                    break;
                case "opponent":
                    baseObjectForPathResolution = context.GetOpponentPlayer();
                    pathSegmentsToResolve = path.Skip(1).ToList();
                    _logger.LogTrace("Resolved '{Segment}' to OpponentPlayer.", firstSegment);
                    break;
                case "gamestate":
                    baseObjectForPathResolution = context.CurrentGameState;
                    pathSegmentsToResolve = path.Skip(1).ToList();
                    _logger.LogTrace("Resolved '{Segment}' to CurrentGameState.", firstSegment);
                    break;
                case "self":
                case "this":
                case "current":
                    baseObjectForPathResolution = context.SourceCard; // Replaced CurrentItem with SourceCard
                    pathSegmentsToResolve = path.Skip(1).ToList();
                    _logger.LogTrace("Resolved '{Segment}' to CurrentItem.", firstSegment);
                    if (!pathSegmentsToResolve.Any() && path.Count == 1) // If path is just "self", "this", or "current"
                    {
                        return baseObjectForPathResolution;
                    }
                    break;
                default:
                    // Path does not start with a known top-level keyword
                    // Priority 2: Temporary variables (if path is a single segment)
                    // TODO: Refactor TemporaryVariables
                    // if (path.Count == 1 && context.TemporaryVariables.TryGetValue(firstSegment, out var tempValue))
                    // {
                    //     _logger.LogTrace("Resolved '{Segment}' from TemporaryVariables.", firstSegment);
                    //     return tempValue;
                    // }

                    // Priority 3: Assume the path starts with a property of GameContext itself (implicit root)
                    // For example, "TriggeringEvent.SourceCard" or "GeneratedEvents.Count"
                    baseObjectForPathResolution = context;
                    pathSegmentsToResolve = path; // The entire path will be resolved on GameContext
                    _logger.LogTrace("Identifier '{Segment}' not a known keyword or temp var; attempting to resolve full path on GameContext.", firstSegment);
                    break;
            }

            if (baseObjectForPathResolution != null)
            {
                return ResolvePathSegments(baseObjectForPathResolution, pathSegmentsToResolve);
            }

            // Fallback for "self.state.variable" if CurrentItem is CardInstance and path starts with "self", "this", or "current"
            // This specific structure was handled separately in the original code.
            if ((firstSegmentLower is "self" or "this" or "current") &&
                path.Count > 2 && path[1].ToLowerInvariant() == "state" &&
                context.SourceCard is CardInstance sourceCard) // Replaced CurrentItem with SourceCard
            {
                string variableName = path[2];
                if (sourceCard.AbilityState.TryGetValue(variableName, out var stateValue))
                {
                    _logger.LogTrace("Resolved '{Path}' from CardInstance.AbilityState.", string.Join(".", path));
                    if (path.Count == 3) return stateValue;
                    return ResolvePathSegments(stateValue, path.Skip(3).ToList());
                }
            }

            _logger.LogWarning("Identifier could not be resolved: {Path}", string.Join(".", path));
            return null;
        }

        private object? ResolvePathSegments(object? target, List<string> segments)
        {
            object? currentTarget = target;
            foreach (var segment in segments)
            {
                if (currentTarget == null)
                {
                    _logger.LogDebug("ResolvePathSegments: currentTarget is null, cannot resolve segment '{Segment}'.", segment);
                    return null;
                }

                // Handle special collection properties like Count, IsEmpty first
                if (segment.Equals("Count", StringComparison.OrdinalIgnoreCase) && currentTarget is ICollection collection)
                {
                    currentTarget = collection.Count;
                    _logger.LogTrace("Resolved segment '{Segment}' to collection Count: {Count}", segment, currentTarget);
                    continue;
                }
                if (segment.Equals("IsEmpty", StringComparison.OrdinalIgnoreCase) && currentTarget is ICollection collectionIsEmpty)
                {
                    currentTarget = collectionIsEmpty.Count == 0;
                    _logger.LogTrace("Resolved segment '{Segment}' to collection IsEmpty: {IsEmpty}", segment, currentTarget);
                    continue;
                }

                // Handle IReadOnlyDictionary<string, object?> access (e.g., for IGameEvent.Parameters)
                if (currentTarget is IReadOnlyDictionary<string, object?> dictionary && dictionary.TryGetValue(segment, out var dictValue))
                {
                    currentTarget = dictValue;
                    _logger.LogTrace("Resolved segment '{Segment}' from dictionary. Value: {Value}", segment, currentTarget ?? "null");
                    continue;
                }

                // Fallback to property reflection
                var propertyInfo = currentTarget.GetType().GetProperty(segment, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

                if (propertyInfo == null)
                {
                    _logger.LogWarning("Property '{Segment}' not found on type '{TargetType}'. Path segment resolution failed.", segment, currentTarget.GetType().Name);
                    return null;
                }
                currentTarget = propertyInfo.GetValue(currentTarget);
                _logger.LogTrace("Resolved segment '{Segment}' to property value: {Value}", segment, currentTarget ?? "null");
            }
            return currentTarget;
        }

        private IFunctionDefinition? FindFunction(string functionName, GameContext context)
        {
            if (string.IsNullOrWhiteSpace(functionName))
            {
                return null;
            }

            return _functionRegistry.GetFunction(functionName);
        }
    }

    internal static class DynamicOperations
    {
        public static object? Add(object? left, object? right)
        {
            if (left is string || right is string)
                return (left?.ToString() ?? "") + (right?.ToString() ?? "");
            if (left is decimal || right is decimal)
                return Convert.ToDecimal(left ?? 0m) + Convert.ToDecimal(right ?? 0m);
            if (left is double || right is double || left is float || right is float)
                return Convert.ToDouble(left ?? 0.0) + Convert.ToDouble(right ?? 0.0);
            if (left is long || right is long)
                return Convert.ToInt64(left ?? 0L) + Convert.ToInt64(right ?? 0L);
            if (left is int || right is int)
                return Convert.ToInt32(left ?? 0) + Convert.ToInt32(right ?? 0);

            throw new InvalidOperationException($"Cannot add types {left?.GetType().Name ?? "null"} and {right?.GetType().Name ?? "null"}");
        }

        public static object? Subtract(object? left, object? right)
        {
            if (left is decimal || right is decimal)
                return Convert.ToDecimal(left ?? 0m) - Convert.ToDecimal(right ?? 0m);
            if (left is double || right is double || left is float || right is float)
                return Convert.ToDouble(left ?? 0.0) - Convert.ToDouble(right ?? 0.0);
            if (left is long || right is long)
                return Convert.ToInt64(left ?? 0L) - Convert.ToInt64(right ?? 0L);
            if (left is int || right is int)
                return Convert.ToInt32(left ?? 0) - Convert.ToInt32(right ?? 0);
            throw new InvalidOperationException($"Cannot subtract types {left?.GetType().Name ?? "null"} and {right?.GetType().Name ?? "null"}");
        }

        public static object? Multiply(object? left, object? right)
        {
            if (left is decimal || right is decimal)
                return Convert.ToDecimal(left ?? 0m) * Convert.ToDecimal(right ?? 0m);
            if (left is double || right is double || left is float || right is float)
                return Convert.ToDouble(left ?? 0.0) * Convert.ToDouble(right ?? 0.0);
            if (left is long || right is long)
                return Convert.ToInt64(left ?? 0L) * Convert.ToInt64(right ?? 0L);
            if (left is int || right is int)
                return Convert.ToInt32(left ?? 0) * Convert.ToInt32(right ?? 0);
            throw new InvalidOperationException($"Cannot multiply types {left?.GetType().Name ?? "null"} and {right?.GetType().Name ?? "null"}");
        }

        public static object? Divide(object? left, object? right)
        {
            if (TypeConverter.IsZero(right)) throw new DivideByZeroException();

            if (left is decimal || right is decimal || left is double || right is double || left is float || right is float)
                return Convert.ToDecimal(left ?? 0m) / Convert.ToDecimal(right ?? 1m);
            if (left is long || right is long)
                return Convert.ToInt64(left ?? 0L) / Convert.ToInt64(right ?? 1L);
            if (left is int || right is int)
                return Convert.ToInt32(left ?? 0) / Convert.ToInt32(right ?? 1);
            throw new InvalidOperationException($"Cannot divide types {left?.GetType().Name ?? "null"} and {right?.GetType().Name ?? "null"}");
        }

        public static object? Modulo(object? left, object? right)
        {
            if (TypeConverter.IsZero(right)) throw new DivideByZeroException("Attempted modulo by zero.");

            if (left is decimal || right is decimal)
                return Convert.ToDecimal(left ?? 0m) % Convert.ToDecimal(right ?? 1m);
            if (left is double || right is double || left is float || right is float)
                return Convert.ToDouble(left ?? 0.0) % Convert.ToDouble(right ?? 1.0);
            if (left is long || right is long)
                return Convert.ToInt64(left ?? 0L) % Convert.ToInt64(right ?? 1L);
            if (left is int || right is int)
                return Convert.ToInt32(left ?? 0) % Convert.ToInt32(right ?? 1);
            throw new InvalidOperationException($"Cannot perform modulo on types {left?.GetType().Name ?? "null"} and {right?.GetType().Name ?? "null"}");
        }

        public static bool AreEqual(object? left, object? right)
        {
            if (left is ValueType && right is ValueType && left.GetType() != right.GetType())
            {
                try { return Convert.ToDecimal(left).Equals(Convert.ToDecimal(right)); } catch { /* Fall through */ }
            }
            return Equals(left, right);
        }

        public static bool IsGreaterThan(object? left, object? right)
        {
            if (left == null || right == null) return false;
            if (TryCompare(left, right, out int result)) return result > 0;
            throw new InvalidOperationException($"Cannot compare '>' on types {left.GetType().Name} and {right.GetType().Name}");
        }

        public static bool IsGreaterThanOrEqual(object? left, object? right)
        {
            if (left == null && right == null) return true;
            if (left == null || right == null) return false;
            if (TryCompare(left, right, out int result)) return result >= 0;
            throw new InvalidOperationException($"Cannot compare '>=' on types {left.GetType().Name} and {right.GetType().Name}");
        }

        public static bool IsLessThan(object? left, object? right)
        {
            if (left == null || right == null) return false;
            if (TryCompare(left, right, out int result)) return result < 0;
            throw new InvalidOperationException($"Cannot compare '<' on types {left.GetType().Name} and {right.GetType().Name}");
        }

        public static bool IsLessThanOrEqual(object? left, object? right)
        {
            if (left == null && right == null) return true;
            if (left == null || right == null) return false;
            if (TryCompare(left, right, out int result)) return result <= 0;
            throw new InvalidOperationException($"Cannot compare '<=' on types {left.GetType().Name} and {right.GetType().Name}");
        }

        public static bool LogicalAnd(object? left, object? right)
        {
            bool leftBool = TypeConverter.ConvertToBool(left);
            if (!leftBool) return false;
            return TypeConverter.ConvertToBool(right);
        }

        public static bool LogicalOr(object? left, object? right)
        {
            bool leftBool = TypeConverter.ConvertToBool(left);
            if (leftBool) return true;
            return TypeConverter.ConvertToBool(right);
        }

        public static object? Negate(object? operand)
        {
            if (operand is decimal d) return -d;
            if (operand is double db) return -db;
            if (operand is float f) return -f;
            if (operand is long l) return -l;
            if (operand is int i) return -i;
            throw new InvalidOperationException($"Cannot negate type {operand?.GetType().Name ?? "null"}");
        }

        public static bool LogicalNot(object? operand)
        {
            return !TypeConverter.ConvertToBool(operand);
        }

        private static bool TryCompare(object left, object right, out int comparisonResult)
        {
            comparisonResult = 0;
            try
            {
                if (IsNumericType(left) && IsNumericType(right))
                {
                    comparisonResult = Convert.ToDecimal(left).CompareTo(Convert.ToDecimal(right));
                    return true;
                }
                if (left is string sLeft && right is string sRight)
                {
                    comparisonResult = string.Compare(sLeft, sRight, StringComparison.Ordinal);
                    return true;
                }
                if (left is IComparable compLeft)
                {
                    comparisonResult = compLeft.CompareTo(right);
                    return true;
                }
            }
            catch (Exception ex) when (ex is InvalidCastException || ex is FormatException || ex is ArgumentException || ex is OverflowException)
            {
                return false;
            }
            return false;
        }

        private static bool IsNumericType(object? obj)
        {
            if (obj == null) return false;
            return obj is byte || obj is sbyte ||
                   obj is short || obj is ushort ||
                   obj is int || obj is uint ||
                   obj is long || obj is ulong ||
                   obj is float || obj is double || obj is decimal;
        }
    }
}