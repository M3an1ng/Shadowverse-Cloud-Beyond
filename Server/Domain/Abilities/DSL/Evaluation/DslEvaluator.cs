using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging; // Assuming logging will be added
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.AST;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Parser; // For TokenType
using ShadowverseBeyond.Server.Domain.Events; // Added for GameContext

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation
{
    /// <summary>
    /// Evaluates a DSL Abstract Syntax Tree (AST) within a given context.
    /// Traverses the AST and computes the expression's value based on runtime data
    /// provided by the EvaluationContext.
    /// </summary>
    public class DslEvaluator
    {
        private readonly ILogger<DslEvaluator> _logger;

        // Consider injecting dependencies like FunctionRegistry if needed
        public DslEvaluator(ILogger<DslEvaluator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Asynchronously evaluates the given AST node within the specified context.
        /// </summary>
        /// <param name="astNode">The root node of the AST to evaluate.</param>
        /// <param name="context">The evaluation context containing runtime data.</param>
        /// <returns>An EvaluationResult containing the computed value or errors.</returns>
        public async Task<EvaluationResult> EvaluateAsync(ExpressionNode astNode, EvaluationContext context)
        {
            if (astNode == null)
            {
                _logger.LogWarning("Attempted to evaluate a null AST node.");
                return EvaluationResult.Failure("Cannot evaluate a null expression.");
            }
            if (context == null)
            {
                _logger.LogError("EvaluationContext cannot be null.");
                return EvaluationResult.Failure("EvaluationContext is required.");
            }


            _logger.LogTrace("Starting evaluation for AST node type: {NodeType}", astNode.GetType().Name);
            try
            {
                // Use a dispatcher method based on node type
                return await VisitNodeAsync(astNode, context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception during DSL evaluation for node type {NodeType}.", astNode.GetType().Name);
                return EvaluationResult.Failure($"Unhandled exception during evaluation: {ex.Message}");
            }
        }

        /// <summary>
        /// Asynchronously evaluates the given AST node within the specified GameContext.
        /// This method creates a temporary EvaluationContext from GameContext for compatibility.
        /// </summary>
        /// <param name="astNode">The root node of the AST to evaluate.</param>
        /// <param name="context">The game context containing runtime data.</param>
        /// <returns>An EvaluationResult containing the computed value or errors.</returns>
        public async Task<EvaluationResult> EvaluateAsync(ExpressionNode astNode, GameContext context)
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

            _logger.LogTrace("Starting evaluation for AST node type: {NodeType} using GameContext", astNode.GetType().Name);

            // Create temporary EvaluationContext from GameContext
            var evaluationContext = CreateEvaluationContextFromGameContext(context);

            // Delegate to the existing EvaluationContext method
            return await EvaluateAsync(astNode, evaluationContext);
        }

        /// <summary>
        /// Creates an EvaluationContext from a GameContext for compatibility with existing DSL evaluation logic.
        /// </summary>
        /// <param name="context">The GameContext to convert.</param>
        /// <returns>An EvaluationContext with equivalent data.</returns>
        private EvaluationContext CreateEvaluationContextFromGameContext(GameContext context)
        {
            // Create snapshot of game state
            var gameStateDto = context.CreateSnapshot();
            var requestingPlayerDto = gameStateDto.Players?.FirstOrDefault(p => p.PlayerId == context.RequestingPlayerId);
            var opponentPlayerDto = gameStateDto.Players?.FirstOrDefault(p => p.PlayerId != context.RequestingPlayerId);

            return new EvaluationContext(
                cardRepository: context.CardRepository ?? throw new InvalidOperationException("CardRepository is required in GameContext for DSL evaluation"),
                functionRegistry: context.FunctionRegistry ?? throw new InvalidOperationException("FunctionRegistry is required in GameContext for DSL evaluation"),
                gameState: gameStateDto,
                player: requestingPlayerDto,
                opponent: opponentPlayerDto,
                currentItem: context.CurrentItem,
                temporaryVariables: new Dictionary<string, object>(context.TemporaryVariables)
            );
        }

        /// <summary>
        /// Dispatches evaluation to the appropriate Visit method based on the node type.
        /// </summary>
        private async Task<EvaluationResult> VisitNodeAsync(ExpressionNode node, EvaluationContext context)
        {
            // Using a switch expression for type-based dispatching
            return node switch
            {
                LiteralNode literal => VisitLiteral(literal, context),
                IdentifierNode identifier => await VisitIdentifierAsync(identifier, context),
                BinaryOperationNode binaryOp => await VisitBinaryOperatorAsync(binaryOp, context), // Corrected name
                UnaryOperationNode unaryOp => await VisitUnaryOperatorAsync(unaryOp, context), // Corrected name
                FunctionCallNode funcCall => await VisitFunctionCallAsync(funcCall, context),
                // Add cases for other node types (e.g., TargetExpressionNode if evaluated here)
                _ => HandleUnsupportedNode(node)
            };
        }

        /// <summary>
        /// Handles literal values (numbers, strings, booleans, null).
        /// </summary>
        private EvaluationResult VisitLiteral(LiteralNode node, EvaluationContext context)
        {
            _logger.LogTrace("Visiting LiteralNode. Value: {Value}, Type: {Type}", node.Value ?? "null", node.Value?.GetType().Name ?? "null");
            // Literals directly return their value.
            return EvaluationResult.Success(node.Value);
        }

        /// <summary>
        /// Handles identifiers (variable names, property paths).
        /// </summary>
        private async Task<EvaluationResult> VisitIdentifierAsync(IdentifierNode node, EvaluationContext context)
        {
            _logger.LogTrace("Visiting IdentifierNode. Path: {Path}", string.Join(".", node.Path));
            // Delegate resolution to the context
            object? resolvedValue = await context.ResolveIdentifierAsync(node.Path);

            if (resolvedValue != null) // TODO: Refine success condition if null is a valid resolved value in some cases
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


        /// <summary>
        /// Handles binary operations (+, -, *, /, ==, !=, <, >, etc.).
        /// </summary>
        private async Task<EvaluationResult> VisitBinaryOperatorAsync(BinaryOperationNode node, EvaluationContext context) // Corrected type
        {
            _logger.LogTrace("Visiting BinaryOperationNode. Operator: {Operator}", node.Operator); // Corrected log message type
            // TODO: Implement binary operation logic
            // 1. Evaluate left operand
            var leftResult = await VisitNodeAsync(node.Left, context);
            if (!leftResult.IsSuccess) return leftResult;

            // 2. Evaluate right operand
            var rightResult = await VisitNodeAsync(node.Right, context);
            if (!rightResult.IsSuccess) return rightResult;

            // 3. Perform operation (implement PerformBinaryOperation helper)
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
            // Let other unexpected exceptions propagate to the main handler
        }

        /// <summary>
        /// Handles unary operations (-, !).
        /// </summary>
        private async Task<EvaluationResult> VisitUnaryOperatorAsync(UnaryOperationNode node, EvaluationContext context) // Corrected type
        {
            _logger.LogTrace("Visiting UnaryOperationNode. Operator: {Operator}", node.Operator); // Corrected log message type
            // TODO: Implement unary operation logic
            var operandResult = await VisitNodeAsync(node.Operand, context);
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
            // Let other unexpected exceptions propagate
        }

        /// <summary>
        /// Handles function calls.
        /// </summary>
        private async Task<EvaluationResult> VisitFunctionCallAsync(FunctionCallNode node, EvaluationContext context)
        {
            _logger.LogTrace("Visiting FunctionCallNode. Function: {FunctionName}, Args: {ArgCount}", node.FunctionName, node.Arguments.Count);
            // TODO: Implement function call logic
            // 1. Find function
            var function = context.FindFunction(node.FunctionName);
            if (function == null)
            {
                _logger.LogWarning("Function not found: {FunctionName}", node.FunctionName);
                return EvaluationResult.Failure($"Function not found: {node.FunctionName}");
            }

            // 2. Evaluate arguments
            var evaluatedArgs = new List<object?>();
            foreach (var argNode in node.Arguments)
            {
                var argResult = await VisitNodeAsync(argNode, context);
                if (!argResult.IsSuccess)
                {
                    _logger.LogWarning("Argument evaluation failed for function '{FunctionName}'. Error: {Error}", node.FunctionName, string.Join("; ", argResult.Errors));
                    // Return the argument evaluation error
                    return EvaluationResult.Failure($"Argument evaluation failed for function '{node.FunctionName}': {string.Join("; ", argResult.Errors)}");
                }
                evaluatedArgs.Add(argResult.Value);
            }
            _logger.LogTrace("Arguments evaluated for function '{FunctionName}'. Count: {Count}", node.FunctionName, evaluatedArgs.Count);


            // 3. Validate arguments (using function definition)
            if (!function.ValidateArguments(evaluatedArgs))
            {
                _logger.LogWarning("Invalid arguments provided for function: {FunctionName}", node.FunctionName);
                // TODO: Get more specific error message from ValidateArguments if possible
                return EvaluationResult.Failure($"Invalid arguments for function: {node.FunctionName}");
            }

            // 4. Execute function
            try
            {
                object? resultValue = await function.ExecuteAsync(evaluatedArgs, context);
                _logger.LogTrace("Function '{FunctionName}' executed successfully. Result: {Result}", node.FunctionName, resultValue ?? "null");
                return EvaluationResult.Success(resultValue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing function '{FunctionName}'.", node.FunctionName);
                return EvaluationResult.Failure($"Error executing function '{node.FunctionName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Handles unsupported AST node types.
        /// </summary>
        private EvaluationResult HandleUnsupportedNode(ExpressionNode node)
        {
            string errorMsg = $"Unsupported AST node type encountered during evaluation: {node.GetType().Name}";
            _logger.LogError(errorMsg);
            return EvaluationResult.Failure(errorMsg);
        }

        // --- Private Helper Methods for Operations ---

        /// <summary>
        /// Performs the actual binary operation logic, including type checking and coercion.
        /// </summary>
        private object? PerformBinaryOperation(object? left, object? right, TokenType operatorType)
        {
            _logger.LogTrace("Performing binary operation {Operator} on '{Left}' ({LeftType}) and '{Right}' ({RightType})",
                operatorType, left ?? "null", left?.GetType().Name ?? "null", right ?? "null", right?.GetType().Name ?? "null");

            // TODO: Implement detailed logic for each operator (+, -, *, /, %, ==, !=, <, >, <=, >=, &&, ||)
            // This needs careful handling of nulls, type compatibility, and potential conversions.
            // Example structure:
            switch (operatorType)
            {
                case TokenType.Plus:
                    return DynamicOperations.Add(left, right);
                case TokenType.Minus:
                    return DynamicOperations.Subtract(left, right);
                case TokenType.Multiply: // Corrected from Asterisk
                    return DynamicOperations.Multiply(left, right);
                case TokenType.Divide: // Corrected from Slash
                    return DynamicOperations.Divide(left, right);
                case TokenType.Modulo: // Corrected from Percent
                    return DynamicOperations.Modulo(left, right);
                case TokenType.Equal: // Corrected from EqualEqual
                    return DynamicOperations.AreEqual(left, right);
                case TokenType.NotEqual: // Corrected from BangEqual
                    return !DynamicOperations.AreEqual(left, right);
                case TokenType.GreaterThan: // Corrected from Greater
                    return DynamicOperations.IsGreaterThan(left, right);
                case TokenType.GreaterEqual:
                    return DynamicOperations.IsGreaterThanOrEqual(left, right);
                case TokenType.LessThan: // Corrected from Less
                    return DynamicOperations.IsLessThan(left, right);
                case TokenType.LessEqual:
                    return DynamicOperations.IsLessThanOrEqual(left, right);
                case TokenType.And: // Corrected from AmpersandAmpersand/Keyword_and
                    return DynamicOperations.LogicalAnd(left, right);
                case TokenType.Or: // Corrected from PipePipe/Keyword_or
                    return DynamicOperations.LogicalOr(left, right);
                // TODO: Add other operators if needed (e.g., Power)
                default:
                    throw new InvalidOperationException($"Unsupported binary operator: {operatorType}");
            }
        }

        /// <summary>
        /// Performs the actual unary operation logic.
        /// </summary>
        private object? PerformUnaryOperation(object? operand, TokenType operatorType)
        {
            _logger.LogTrace("Performing unary operation {Operator} on '{Operand}' ({OperandType})",
                operatorType, operand ?? "null", operand?.GetType().Name ?? "null");

            // TODO: Implement logic for unary operators (-, !)
            switch (operatorType)
            {
                case TokenType.Minus: // Negation - Stays Minus
                    return DynamicOperations.Negate(operand);
                case TokenType.Not: // Corrected from Bang/Keyword_not
                    return DynamicOperations.LogicalNot(operand);
                default:
                    throw new InvalidOperationException($"Unsupported unary operator: {operatorType}");
            }
        }
    }

    // Helper class for dynamic operations (Consider moving to a separate utility file)
    internal static class DynamicOperations
    {
        public static object? Add(object? left, object? right)
        {
            if (left is string || right is string)
                return (left?.ToString() ?? "") + (right?.ToString() ?? "");
            if (left is decimal || right is decimal)
                return Convert.ToDecimal(left ?? 0m) + Convert.ToDecimal(right ?? 0m); // Assume 0 for null
            if (left is double || right is double || left is float || right is float)
                return Convert.ToDouble(left ?? 0.0) + Convert.ToDouble(right ?? 0.0); // Assume 0.0 for null
            if (left is long || right is long)
                return Convert.ToInt64(left ?? 0L) + Convert.ToInt64(right ?? 0L); // Assume 0L for null
            if (left is int || right is int)
                return Convert.ToInt32(left ?? 0) + Convert.ToInt32(right ?? 0); // Assume 0 for null

            // Add other numeric types if necessary
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
            // Handle division by zero explicitly
            if (TypeConverter.IsZero(right)) throw new DivideByZeroException();

            // Prefer decimal for division to maintain precision
            if (left is decimal || right is decimal || left is double || right is double || left is float || right is float)
                // Use decimal for division to avoid potential precision issues with double
                return Convert.ToDecimal(left ?? 0m) / Convert.ToDecimal(right ?? 1m); // Avoid div by zero conceptually here, handled above
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
            // Standard equality check, handles nulls correctly
            // Consider numeric type coercion for equality? E.g., 1 == 1.0m
            if (left is System.ValueType && right is System.ValueType && left.GetType() != right.GetType())
            {
                try { return Convert.ToDecimal(left).Equals(Convert.ToDecimal(right)); } catch { /* Fall through */ }
            }
            return Equals(left, right);
        }

        public static bool IsGreaterThan(object? left, object? right)
        {
            if (left == null || right == null) return false; // Cannot compare nulls
            if (TryCompare(left, right, out int result)) return result > 0;
            throw new InvalidOperationException($"Cannot compare '>' on types {left.GetType().Name} and {right.GetType().Name}");
        }

        public static bool IsGreaterThanOrEqual(object? left, object? right)
        {
            if (left == null && right == null) return true; // null == null
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
            // Standard C# truthiness: null is false, non-zero numbers are true? No, stick to bool.
            bool leftBool = TypeConverter.ConvertToBool(left);
            // Short-circuit evaluation
            if (!leftBool) return false;
            return TypeConverter.ConvertToBool(right);
        }

        public static bool LogicalOr(object? left, object? right)
        {
            bool leftBool = TypeConverter.ConvertToBool(left);
            // Short-circuit evaluation
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
            // Add short, byte etc. if needed
            throw new InvalidOperationException($"Cannot negate type {operand?.GetType().Name ?? "null"}");
        }

        public static bool LogicalNot(object? operand)
        {
            return !TypeConverter.ConvertToBool(operand);
        }


        // --- Private Helpers for DynamicOperations ---



        private static bool TryCompare(object left, object right, out int comparisonResult)
        {
            comparisonResult = 0;
            try
            {
                // Promote to common types for comparison (decimal for best precision)
                if (IsNumericType(left) && IsNumericType(right))
                {
                    comparisonResult = Convert.ToDecimal(left).CompareTo(Convert.ToDecimal(right));
                    return true;
                }
                // Add string comparison if needed:
                if (left is string sLeft && right is string sRight)
                {
                    comparisonResult = string.Compare(sLeft, sRight, StringComparison.Ordinal); // Or other comparison type
                    return true;
                }

                // If types are not directly comparable numerically or as strings
                if (left is IComparable compLeft)
                {
                    // This might throw ArgumentException if types are incompatible (e.g., int.CompareTo(string))
                    comparisonResult = compLeft.CompareTo(right);
                    return true;
                }
            }
            catch (Exception ex) when (ex is InvalidCastException || ex is FormatException || ex is ArgumentException || ex is OverflowException)
            {
                // Comparison failed due to type incompatibility or conversion error
                return false;
            }
            return false; // Could not compare
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