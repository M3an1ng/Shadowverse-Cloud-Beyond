using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.AST;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Parser; // For TokenType
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Entities; // Added for CardInstance casting
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation
{
    /// <summary>
    /// Traverses a DSL Abstract Syntax Tree (AST) and builds an equivalent System.Linq.Expressions Expression Tree.
    /// This is the core component of the DslCompiler.
    /// </summary>
    public class ExpressionTreeBuilder
    {
        private readonly ParameterExpression _selfParam;
        private readonly ParameterExpression _gameContextParam;
        private readonly ParameterExpression? _tscParam; // Nullable TargetSelectionContext parameter
        private readonly ParameterExpression _dbContextParam;
        private readonly IFunctionRegistry _functionRegistry;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExpressionTreeBuilder"/> class with a unified context.
        /// </summary>
        /// <param name="selfParam">The parameter expression representing 'self' (the item being evaluated).</param>
        /// <param name="gameContextParam">The parameter expression representing the global game context (e.g., state, player).</param>
        /// <param name="dbContextParam">The parameter expression representing the database context (e.g., all card definitions).</param>
        /// <param name="functionRegistry">The registry to look up function definitions.</param>
        public ExpressionTreeBuilder(
            ParameterExpression selfParam,
            ParameterExpression gameContextParam,
            ParameterExpression dbContextParam,
            IFunctionRegistry functionRegistry)
            : this(selfParam, gameContextParam, null, dbContextParam, functionRegistry)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExpressionTreeBuilder"/> class with an optional TargetSelectionContext.
        /// </summary>
        public ExpressionTreeBuilder(
            ParameterExpression selfParam,
            ParameterExpression gameContextParam,
            ParameterExpression? tscParam, // Nullable
            ParameterExpression dbContextParam,
            IFunctionRegistry functionRegistry)
        {
            _selfParam = selfParam ?? throw new ArgumentNullException(nameof(selfParam));
            _gameContextParam = gameContextParam ?? throw new ArgumentNullException(nameof(gameContextParam));
            _tscParam = tscParam; // Can be null
            _dbContextParam = dbContextParam ?? throw new ArgumentNullException(nameof(dbContextParam));
            _functionRegistry = functionRegistry ?? throw new ArgumentNullException(nameof(functionRegistry));
        }

        /// <summary>
        /// Builds an Expression tree from an AST node.
        /// </summary>
        /// <param name="astNode">The root of the Abstract Syntax Tree.</param>
        /// <returns>The root of the generated Expression Tree.</returns>
        public Expression Build(ExpressionNode astNode)
        {
            return VisitNode(astNode);
        }

        private Expression VisitNode(ExpressionNode node)
        {
            return node switch
            {
                LiteralNode literal => VisitLiteral(literal),
                IdentifierNode identifier => VisitIdentifier(identifier),
                BinaryOperationNode binaryOp => VisitBinaryOperator(binaryOp),
                MemberAccessNode memberAccess => VisitMemberAccess(memberAccess),
                UnaryOperationNode unaryOp => VisitUnaryOperator(unaryOp),
                FunctionCallNode funcCall => VisitFunctionCall(funcCall),
                _ => throw new NotSupportedException($"AST node type '{node.GetType().Name}' is not supported for expression tree building.")
            };
        }

        private Expression VisitLiteral(LiteralNode node)
        {
            // Convert a DSL literal value into an Expression Tree constant.
            return Expression.Constant(node.Value);
        }

        private Expression VisitIdentifier(IdentifierNode node)
        {
            // With MemberAccessNode, IdentifierNode should only represent root objects.
            // The path should contain only one segment.
            if (node.Path.Count > 1)
            {
                // This case should ideally not be hit if the parser is correct.
                // We can fall back to the old logic for safety, but log a warning.
                // For now, we'll assume the parser change is correct and only handle the first segment.
            }

            if (!node.Path.Any())
            {
                throw new InvalidOperationException("Identifier node path cannot be empty.");
            }

            var rootName = node.Path.First();
            return ResolveRootExpression(rootName);
        }

        private Expression VisitMemberAccess(MemberAccessNode node)
        {
            // 1. Visit the target expression to get the instance.
            Expression targetExpression = VisitNode(node.Target);

            // 2. Get the member name.
            var memberName = node.Member.Name;

            // 3. Build the member access on the target expression.
            return BuildMemberAccess(targetExpression, memberName);
        }

        private Expression ResolveRootExpression(string rootName)
        {
            // Step 1: Prioritize TSC-specific identifiers.
            if (_tscParam != null && rootName.Equals("AbilityTargets", StringComparison.OrdinalIgnoreCase))
            {
                return Expression.Property(_tscParam, nameof(TargetSelectionContext.ResolvedTargets));
            }

            // Step 2: Determine the unified source for the GameContext.
            Expression gameContextSource = (_tscParam != null)
                ? Expression.Property(_tscParam, nameof(TargetSelectionContext.GameContext))
                : _gameContextParam;

            // Step 3: Use the unified source to resolve all common identifiers.
            switch (rootName.ToLowerInvariant())
            {
                case "context":
                case "game":
                    return gameContextSource;

                case "self":
                case "this":
                case "current":
                    return _selfParam;

                case "db":
                    return _dbContextParam;

                case "player":
                    var getPlayerMethod = typeof(GameContextExtensions).GetMethod(
                        "GetRequestingPlayer",
                        new[] { typeof(GameContext) }
                    )!;
                    return Expression.Call(null, getPlayerMethod, gameContextSource);

                case "opponent":
                    var getOpponentMethod = typeof(GameContextExtensions).GetMethod(
                        "GetOpponentPlayer",
                        new[] { typeof(GameContext) }
                    )!;
                    return Expression.Call(null, getOpponentMethod, gameContextSource);

                case "state":
                    return Expression.Property(gameContextSource, nameof(GameContext.CurrentGameState));

                case "owner":
                    {
                        var gameStateExpr = Expression.Property(gameContextSource, nameof(GameContext.CurrentGameState));
                        var sourceCardExpr = Expression.Property(gameContextSource, nameof(GameContext.SourceCard));
                        var ownerIdExpr = Expression.Property(sourceCardExpr, nameof(CardInstance.OwnerPlayerId));
                        var getPlayerStateMethod = typeof(GameState).GetMethod(nameof(GameState.GetPlayerState), new[] { typeof(Guid) });
                        if (getPlayerStateMethod == null)
                        {
                            throw new InvalidOperationException($"Cannot find GetPlayerState(Guid) method on GameState.");
                        }
                        return Expression.Call(gameStateExpr, getPlayerStateMethod, ownerIdExpr);
                    }

                default:
                    // Fallback for direct properties on the GameContext (e.g., TriggeringEvent).
                    return Expression.PropertyOrField(gameContextSource, rootName);
            }
        }

        private Expression BuildMemberAccess(Expression instance, string memberName)
        {
            // If the instance is 'self' of type object, cast it to CardInstance for property access.
            // This is crucial for the generic CompilePredicate<object> to work with strongly-typed members.
            Expression targetInstance = instance;
            if (targetInstance == _selfParam && targetInstance.Type == typeof(object))
            {
                targetInstance = Expression.Convert(targetInstance, typeof(CardInstance));
            }

            try
            {
                // The primary, universal approach: directly access the property or field.
                // This automatically handles computed properties (like CurrentAttack) by invoking their 'get' accessor.
                // No special handling or hardcoding is needed.
                return Expression.PropertyOrField(targetInstance, memberName);
            }
            catch (ArgumentException) // This exception is thrown if the member does not exist on the target type.
            {
                // Fallback logic specifically for CardInstance: if a property is not found on the instance,
                // try to find it on its definition. This is a common pattern in the game's domain model.
                if (typeof(CardInstance).IsAssignableFrom(targetInstance.Type))
                {
                    try
                    {
                        var definitionProperty = Expression.Property(targetInstance, nameof(CardInstance.CurrentDefinition));
                        return Expression.PropertyOrField(definitionProperty, memberName);
                    }
                    catch (ArgumentException innerEx)
                    {
                        // If it's not on the definition either, throw a more informative exception.
                        throw new ArgumentException($"'{memberName}' is not a member of type '{targetInstance.Type.Name}' or its '{nameof(CardInstance.CurrentDefinition)}' property.", innerEx);
                    }
                }

                // If it's not a CardInstance or the fallback failed, re-throw the original exception.
                throw;
            }
        }

        private Expression VisitBinaryOperator(BinaryOperationNode node)
        {
            var left = VisitNode(node.Left);
            var right = VisitNode(node.Right);

            // Do not promote yet, as we need the original types for special handling.

            // Map TokenType to ExpressionType
            switch (node.Operator)
            {
                case TokenType.Equal:
                    return BuildEqualsExpression(left, right);

                case TokenType.NotEqual:
                    return Expression.Not(BuildEqualsExpression(left, right));

                // For other operators, use standard promotion and creation
                default:
                    var (promotedLeft, promotedRight) = PromoteExpressions(left, right);
                    return node.Operator switch
                    {
                        TokenType.Plus => Expression.Add(promotedLeft, promotedRight),
                        TokenType.Minus => Expression.Subtract(promotedLeft, promotedRight),
                        TokenType.Multiply => Expression.Multiply(promotedLeft, promotedRight),
                        TokenType.Divide => Expression.Divide(promotedLeft, promotedRight),
                        TokenType.GreaterThan => Expression.GreaterThan(promotedLeft, promotedRight),
                        TokenType.GreaterEqual => Expression.GreaterThanOrEqual(promotedLeft, promotedRight),
                        TokenType.LessThan => Expression.LessThan(promotedLeft, promotedRight),
                        TokenType.LessEqual => Expression.LessThanOrEqual(promotedLeft, promotedRight),
                        TokenType.And => Expression.AndAlso(promotedLeft, promotedRight),
                        TokenType.Or => Expression.OrElse(promotedLeft, promotedRight),
                        _ => throw new NotSupportedException($"Binary operator '{node.Operator}' is not supported for expression tree building.")
                    };
            }
        }

        private Expression BuildEqualsExpression(Expression left, Expression right)
        {
            // Case 1: Enum comparison (e.g., self.CardType == "Follower")
            if (left.Type.IsEnum && right is ConstantExpression rightConst && rightConst.Value is string rightString)
            {
                try
                {
                    var enumValue = Enum.Parse(left.Type, rightString, true);
                    var enumConst = Expression.Constant(enumValue, left.Type);
                    return Expression.Equal(left, enumConst);
                }
                catch (ArgumentException)
                {
                    throw new InvalidOperationException($"'{rightString}' is not a valid value for enum type '{left.Type.Name}'.");
                }
            }

            // Case 2: Collection Contains (e.g., self.Tribe == "Officer")
            if (typeof(IEnumerable<string>).IsAssignableFrom(left.Type) && right is ConstantExpression rightConstStr && rightConstStr.Value is string strValue)
            {
                var containsMethod = typeof(Enumerable).GetMethods()
                    .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
                    .MakeGenericMethod(typeof(string));

                // Add a null check to prevent NullReferenceException on nullable collections
                var notNullCheck = Expression.NotEqual(left, Expression.Constant(null, left.Type));
                var containsCall = Expression.Call(null, containsMethod, left, right);
                return Expression.AndAlso(notNullCheck, containsCall);
            }

            // Default Case: Standard equality check
            var (promotedLeft, promotedRight) = PromoteExpressions(left, right);
            return Expression.Equal(promotedLeft, promotedRight);
        }

        /// <summary>
        /// Promotes numeric expressions to a common type (e.g., int to int?) to allow for comparison.
        /// </summary>
        private (Expression, Expression) PromoteExpressions(Expression left, Expression right)
        {
            if (left.Type == right.Type)
                return (left, right);

            var leftIsNullable = IsNullable(left.Type);
            var rightIsNullable = IsNullable(right.Type);
            var leftUnderlying = Nullable.GetUnderlyingType(left.Type);
            var rightUnderlying = Nullable.GetUnderlyingType(right.Type);

            if (leftIsNullable && !rightIsNullable && leftUnderlying == right.Type)
            {
                right = Expression.Convert(right, left.Type);
            }
            else if (!leftIsNullable && rightIsNullable && rightUnderlying == left.Type)
            {
                left = Expression.Convert(left, right.Type);
            }

            return (left, right);
        }

        private bool IsNullable(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        private Expression VisitUnaryOperator(UnaryOperationNode node)
        {
            var operand = VisitNode(node.Operand);

            return node.Operator switch
            {
                TokenType.Not => Expression.Not(operand),
                TokenType.Minus => Expression.Negate(operand),
                _ => throw new NotSupportedException($"Unary operator '{node.Operator}' is not supported for expression tree building.")
            };
        }

        private Expression VisitFunctionCall(FunctionCallNode node)
        {
            var function = _functionRegistry.GetFunction(node.FunctionName);
            if (function == null)
            {
                throw new NotSupportedException($"Function '{node.FunctionName}' is not registered.");
            }

            // 1. Build expressions for all arguments
            var argumentExpressions = node.Arguments.Select(VisitNode).ToList();

            // 2. Find the 'Execute' method on the function definition instance
            var executeMethodInfo = function.GetType().GetMethod("Execute");
            if (executeMethodInfo == null)
            {
                throw new InvalidOperationException($"Function definition for '{node.FunctionName}' does not have an 'Execute' method.");
            }

            // 3. Prepare arguments for the Expression.Call
            // The 'Execute' method expects two arguments: List<object> and GameContext
            // We need to convert our strongly-typed argument expressions into a List<object>

            // Create a new List<object>
            var listConstructor = typeof(List<object>).GetConstructor(Type.EmptyTypes)!;
            var newList = Expression.New(listConstructor);

            // Create an expression for each argument, converting it to object if it's a value type
            var addMethod = typeof(List<object>).GetMethod("Add")!;
            var addExpressions = argumentExpressions.Select(argExpr =>
            {
                var convertedArg = argExpr.Type.IsValueType ? Expression.Convert(argExpr, typeof(object)) : argExpr;
                return Expression.Call(newList, addMethod, convertedArg);
            });

            // We need to create a block to initialize the list and add all items
            var listVar = Expression.Variable(typeof(List<object>), "argList");
            var listInit = Expression.Assign(listVar, Expression.New(typeof(List<object>)));

            var blockExpressions = new List<Expression> { listInit };
            blockExpressions.AddRange(argumentExpressions.Select(argExpr =>
            {
                var convertedArg = argExpr.Type.IsValueType ? Expression.Convert(argExpr, typeof(object)) : argExpr;
                return Expression.Call(listVar, typeof(List<object>).GetMethod("Add")!, convertedArg);
            }));

            // The final expression in the block is the list itself, which becomes the return value of the block.
            blockExpressions.Add(listVar);

            var listInitializationBlock = Expression.Block(new[] { listVar }, blockExpressions);

            // 4. Create the call to the function's 'Execute' method
            var functionInstance = Expression.Constant(function);
            var callExpression = Expression.Call(functionInstance, executeMethodInfo, listInitializationBlock, _gameContextParam);

            // 5. Convert the result from object to the function's actual return type
            // This is crucial for subsequent operations (like comparisons) to have correct type information.
            var returnType = function.ReturnType;
            var convertedCall = Expression.Convert(callExpression, returnType);

            return convertedCall;
        }
    }
}
