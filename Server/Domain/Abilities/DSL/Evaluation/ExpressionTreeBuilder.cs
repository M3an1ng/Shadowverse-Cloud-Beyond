using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Nodes;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.AST;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Parser; // For TokenType
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Domain.Events.Services;
using ShadowverseBeyond.Server.Domain.Entities; // Added for CardInstance casting
using ShadowverseBeyond.Server.Domain.Buffs;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
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
        private readonly ParameterExpression? _traceCollectorParam;

        private static readonly MethodInfo TraceCaptureMethod = typeof(DslTraceHelper)
            .GetMethod(nameof(DslTraceHelper.Capture))!;

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
            : this(selfParam, gameContextParam, null, dbContextParam, functionRegistry, null)
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
            IFunctionRegistry functionRegistry,
            ParameterExpression? traceCollectorParam = null)
        {
            _selfParam = selfParam ?? throw new ArgumentNullException(nameof(selfParam));
            _gameContextParam = gameContextParam ?? throw new ArgumentNullException(nameof(gameContextParam));
            _tscParam = tscParam; // Can be null
            _dbContextParam = dbContextParam ?? throw new ArgumentNullException(nameof(dbContextParam));
            _functionRegistry = functionRegistry ?? throw new ArgumentNullException(nameof(functionRegistry));
            _traceCollectorParam = traceCollectorParam;
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
            var expression = node switch
            {
                LiteralNode literal => VisitLiteral(literal),
                IdentifierNode identifier => VisitIdentifier(identifier),
                BinaryOperationNode binaryOp => VisitBinaryOperator(binaryOp),
                MemberAccessNode memberAccess => VisitMemberAccess(memberAccess),
                UnaryOperationNode unaryOp => VisitUnaryOperator(unaryOp),
                ParenthesizedExpressionNode parenthesized => VisitParenthesizedExpression(parenthesized),
                FunctionCallNode funcCall => VisitFunctionCall(funcCall),
                QueryFilterNode queryFilter => VisitQueryFilter(queryFilter),
                QueryOrderByNode queryOrderBy => VisitQueryOrderBy(queryOrderBy),
                QueryDistinctNode queryDistinct => VisitQueryDistinct(queryDistinct),
                QuerySelectNode querySelect => VisitQuerySelect(querySelect),
                QueryTakeNode queryTake => VisitQueryTake(queryTake),
                _ => throw new NotSupportedException($"AST node type '{node.GetType().Name}' is not supported for expression tree building.")
            };

            return WrapTrace(node, expression);
        }

        private Expression VisitParenthesizedExpression(ParenthesizedExpressionNode node)
        {
            return VisitNode(node.Inner);
        }

        private Expression VisitLiteral(LiteralNode node)
        {
            // Convert a DSL literal value into an Expression Tree constant.
            return Expression.Constant(node.Value);
        }

        private Expression WrapTrace(ExpressionNode node, Expression expression)
        {
            if (_traceCollectorParam == null || !ShouldTrace(node))
            {
                return expression;
            }

            var captureMethod = TraceCaptureMethod.MakeGenericMethod(expression.Type);
            var expressionText = node.ToString() ?? string.Empty;
            return Expression.Call(captureMethod, _traceCollectorParam, Expression.Constant(expressionText), expression);
        }

        private static bool ShouldTrace(ExpressionNode node)
        {
            return node is not LiteralNode
                && node is not BinaryOperationNode
                && node is not UnaryOperationNode
                && node is not ParenthesizedExpressionNode;
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
                    return Expression.Property(gameContextSource, nameof(GameContext.Owner));

                case "opponent":
                    return Expression.Property(gameContextSource, nameof(GameContext.Opponent));

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
                    var access = TryBuildMemberAccess(gameContextSource, rootName);
                    if (access != null)
                    {
                        return access;
                    }
                    return Expression.PropertyOrField(gameContextSource, rootName);
            }
        }

        private Expression BuildMemberAccess(Expression instance, string memberName)
        {
            // If the instance is 'self' of type object, avoid forcing a CardInstance cast.
            // Use runtime member access so CardInstanceSnapshot and other object-backed values work.
            Expression targetInstance = instance;
            if (targetInstance == _selfParam && targetInstance.Type == typeof(object))
            {
                return BuildRuntimeMemberAccess(targetInstance, memberName);
            }

            var directAccess = TryBuildMemberAccess(targetInstance, memberName);
            if (directAccess != null)
            {
                return directAccess;
            }

            var dictionaryAccess = TryBuildDictionaryAccess(targetInstance, memberName);
            if (dictionaryAccess != null)
            {
                return dictionaryAccess;
            }

            if (typeof(IGameEvent).IsAssignableFrom(targetInstance.Type))
            {
                return BuildEventParameterAccess(targetInstance, memberName);
            }

            if (targetInstance.Type == typeof(object))
            {
                return BuildRuntimeMemberAccess(targetInstance, memberName);
            }

            // Fallback logic specifically for CardInstance: if a property is not found on the instance,
            // try to find it on its definition. This is a common pattern in the game's domain model.
            if (typeof(CardInstance).IsAssignableFrom(targetInstance.Type))
            {
                var definitionProperty = Expression.Property(targetInstance, nameof(CardInstance.CurrentDefinition));
                var definitionAccess = TryBuildMemberAccess(definitionProperty, memberName);
                if (definitionAccess != null)
                {
                    return definitionAccess;
                }

                throw new ArgumentException($"'{memberName}' is not a member of type '{targetInstance.Type.Name}' or its '{nameof(CardInstance.CurrentDefinition)}' property.");
            }

            throw new ArgumentException($"'{memberName}' is not a member of type '{targetInstance.Type.Name}'.");
        }

        private Expression BuildEventParameterAccess(Expression instance, string memberName)
        {
            var method = typeof(EventParameterAccessor).GetMethod(
                nameof(EventParameterAccessor.Resolve),
                BindingFlags.Public | BindingFlags.Static);
            if (method == null)
            {
                throw new InvalidOperationException("Cannot resolve EventParameterAccessor.Resolve.");
            }

            var eventInstance = instance.Type == typeof(IGameEvent)
                ? instance
                : Expression.Convert(instance, typeof(IGameEvent));

            return Expression.Call(
                null,
                method,
                _gameContextParam,
                eventInstance,
                Expression.Constant(memberName));
        }

        private static Expression BuildRuntimeMemberAccess(Expression instance, string memberName)
        {
            var method = typeof(DslRuntimeMemberAccess).GetMethod(
                nameof(DslRuntimeMemberAccess.GetValue),
                BindingFlags.Public | BindingFlags.Static);
            if (method == null)
            {
                throw new InvalidOperationException("Cannot resolve DslRuntimeMemberAccess.GetValue.");
            }

            return Expression.Call(
                null,
                method,
                Expression.Convert(instance, typeof(object)),
                Expression.Constant(memberName));
        }

        private static Expression? TryBuildMemberAccess(Expression instance, string memberName)
        {
            var type = instance.Type;
            var property = type.GetProperty(memberName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (property != null)
            {
                return Expression.Property(instance, property);
            }

            var field = type.GetField(memberName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                return Expression.Field(instance, field);
            }

            return null;
        }

        private static Expression? TryBuildDictionaryAccess(Expression instance, string memberName)
        {
            if (string.IsNullOrWhiteSpace(memberName))
            {
                return null;
            }

            var dictionaryInterface = GetDictionaryInterface(instance.Type);
            if (dictionaryInterface == null)
            {
                return null;
            }

            var genericArguments = dictionaryInterface.GetGenericArguments();
            if (genericArguments.Length != 2 || genericArguments[0] != typeof(string))
            {
                return null;
            }

            var valueType = genericArguments[1];
            var accessor = FindDictionaryAccessor(dictionaryInterface);
            if (accessor == null)
            {
                return null;
            }

            var closedAccessor = accessor.MakeGenericMethod(valueType);
            var dictionaryExpression = instance.Type == dictionaryInterface
                ? instance
                : Expression.Convert(instance, dictionaryInterface);

            return Expression.Call(
                null,
                closedAccessor,
                dictionaryExpression,
                Expression.Constant(memberName));
        }

        private static Type? GetDictionaryInterface(Type type)
        {
            if (type.IsInterface && type.IsGenericType)
            {
                var definition = type.GetGenericTypeDefinition();
                if (definition == typeof(IReadOnlyDictionary<,>) || definition == typeof(IDictionary<,>))
                {
                    return type;
                }
            }

            var interfaces = type.GetInterfaces();
            var readOnly = interfaces.FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>));
            if (readOnly != null)
            {
                return readOnly;
            }

            return interfaces.FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));
        }

        private static MethodInfo? FindDictionaryAccessor(Type dictionaryInterface)
        {
            var genericDefinition = dictionaryInterface.GetGenericTypeDefinition();
            return typeof(DslDictionaryAccess).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method =>
                    method.Name == nameof(DslDictionaryAccess.GetValueOrDefault)
                    && method.IsGenericMethodDefinition
                    && method.GetParameters().Length == 2
                    && method.GetParameters()[0].ParameterType.IsGenericType
                    && method.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == genericDefinition);
        }

        private Expression VisitBinaryOperator(BinaryOperationNode node)
        {
            var left = VisitNode(node.Left);
            var right = VisitNode(node.Right);

            if (node.Operator == TokenType.DivideCeil)
            {
                return BuildDynamicBinaryOperation(left, right, node.Operator);
            }

            if (RequiresDynamicOperation(left, right))
            {
                return BuildDynamicBinaryOperation(left, right, node.Operator);
            }

            if (IsSetOperator(node.Operator, left.Type, right.Type))
            {
                return BuildSetOperation(left, right, node.Operator);
            }

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
                        TokenType.Modulo => Expression.Modulo(promotedLeft, promotedRight),
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

        private static bool IsSetOperator(TokenType operatorType, Type leftType, Type rightType)
        {
            if (operatorType == TokenType.SetUnion || operatorType == TokenType.SetIntersect || operatorType == TokenType.SetDifference)
            {
                return true;
            }

            return false;
        }

        private Expression BuildSetOperation(Expression left, Expression right, TokenType operatorType)
        {
            if (!TryResolveSetOperand(left.Type, out var leftElementType)
                || !TryResolveSetOperand(right.Type, out var rightElementType))
            {
                throw new InvalidOperationException($"Set operator '{operatorType}' requires collection operands.");
            }

            var elementType = ResolveCompatibleElementType(leftElementType, rightElementType, operatorType);
            var leftEnumerable = EnsureEnumerableOrEmpty(left, elementType);
            var rightEnumerable = EnsureEnumerableOrEmpty(right, elementType);

            var methodName = operatorType switch
            {
                TokenType.SetUnion => nameof(DslSetOperators.Union),
                TokenType.SetIntersect => nameof(DslSetOperators.Intersect),
                TokenType.SetDifference => nameof(DslSetOperators.Except),
                _ => throw new NotSupportedException($"Unsupported set operator '{operatorType}'.")
            };

            var method = typeof(DslSetOperators).GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            if (method == null)
            {
                throw new InvalidOperationException($"Cannot resolve set operator method '{methodName}'.");
            }

            var genericMethod = method.MakeGenericMethod(elementType);
            return Expression.Call(null, genericMethod, leftEnumerable, rightEnumerable);
        }

        private static Expression EnsureEnumerableOrEmpty(Expression source, Type elementType)
        {
            var emptyMethod = typeof(Enumerable).GetMethod(nameof(Enumerable.Empty), BindingFlags.Public | BindingFlags.Static);
            if (emptyMethod == null)
            {
                throw new InvalidOperationException("Cannot resolve Enumerable.Empty.");
            }

            var emptyCall = Expression.Call(null, emptyMethod.MakeGenericMethod(elementType));
            var nullCheck = Expression.Equal(Expression.Convert(source, typeof(object)), Expression.Constant(null));
            var converted = EnsureEnumerableOf(source, elementType);
            return Expression.Condition(nullCheck, emptyCall, converted);
        }

        private static Expression EnsureEnumerableOf(Expression source, Type elementType)
        {
            var targetType = typeof(IEnumerable<>).MakeGenericType(elementType);
            if (source.Type == typeof(string))
            {
                throw new InvalidOperationException("String values cannot be treated as collections.");
            }

            if (source.Type == typeof(object))
            {
                var coerceMethod = typeof(DslCollectionCoercion).GetMethod(
                    nameof(DslCollectionCoercion.CoerceToObjectEnumerableOrSingle),
                    BindingFlags.Public | BindingFlags.Static);
                if (coerceMethod == null)
                {
                    throw new InvalidOperationException("Cannot resolve DslCollectionCoercion.CoerceToObjectEnumerableOrSingle.");
                }

                var coerced = Expression.Call(null, coerceMethod, Expression.Convert(source, typeof(object)));
                if (targetType.IsAssignableFrom(coerced.Type))
                {
                    return Expression.Convert(coerced, targetType);
                }

                var castMethod = typeof(Enumerable).GetMethod(nameof(Enumerable.Cast), BindingFlags.Public | BindingFlags.Static);
                if (castMethod == null)
                {
                    throw new InvalidOperationException("Cannot resolve Enumerable.Cast.");
                }
                return Expression.Call(null, castMethod.MakeGenericMethod(elementType), coerced);
            }

            if (DslCollectionCoercion.TryGetEnumerableElementType(source.Type, out _))
            {
                if (targetType.IsAssignableFrom(source.Type))
                {
                    if (source.Type == targetType)
                    {
                        return source;
                    }
                    return Expression.Convert(source, targetType);
                }

                if (typeof(IEnumerable).IsAssignableFrom(source.Type))
                {
                    var castMethod = typeof(Enumerable).GetMethod(nameof(Enumerable.Cast), BindingFlags.Public | BindingFlags.Static);
                    if (castMethod == null)
                    {
                        throw new InvalidOperationException("Cannot resolve Enumerable.Cast.");
                    }
                    return Expression.Call(null, castMethod.MakeGenericMethod(elementType), source);
                }
            }

            var arrayExpression = Expression.NewArrayInit(elementType, Expression.Convert(source, elementType));
            return Expression.Convert(arrayExpression, targetType);
        }

        private static bool TryResolveSetOperand(Type operandType, out Type elementType)
        {
            if (operandType == typeof(string))
            {
                elementType = typeof(string);
                return false;
            }

            if (DslCollectionCoercion.TryGetEnumerableElementType(operandType, out elementType))
            {
                return true;
            }

            elementType = operandType;
            return true;
        }

        private static Type ResolveCompatibleElementType(Type leftElementType, Type rightElementType, TokenType operatorType)
        {
            if (leftElementType.IsAssignableFrom(rightElementType))
            {
                return leftElementType;
            }

            if (rightElementType.IsAssignableFrom(leftElementType))
            {
                return rightElementType;
            }

            throw new InvalidOperationException($"Set operator '{operatorType}' requires compatible collection element types.");
        }

        private Expression BuildEqualsExpression(Expression left, Expression right)
        {
            if (RequiresDynamicOperation(left, right))
            {
                return BuildDynamicBinaryOperation(left, right, TokenType.Equal);
            }

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

            // Numeric promotion (e.g., double vs int) to avoid invalid binary operators.
            if (IsNumericType(leftUnderlying ?? left.Type) && IsNumericType(rightUnderlying ?? right.Type))
            {
                var promotedType = GetNumericPromotionType(leftUnderlying ?? left.Type, rightUnderlying ?? right.Type);
                var targetType = (leftIsNullable || rightIsNullable)
                    ? typeof(Nullable<>).MakeGenericType(promotedType)
                    : promotedType;

                left = Expression.Convert(left, targetType);
                right = Expression.Convert(right, targetType);
                return (left, right);
            }

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

        private static bool IsNumericType(Type type)
        {
            return type == typeof(sbyte)
                || type == typeof(byte)
                || type == typeof(short)
                || type == typeof(ushort)
                || type == typeof(int)
                || type == typeof(uint)
                || type == typeof(long)
                || type == typeof(ulong)
                || type == typeof(float)
                || type == typeof(double)
                || type == typeof(decimal);
        }

        private static Type GetNumericPromotionType(Type leftType, Type rightType)
        {
            // Prefer double for mixed numeric comparisons (matches SumFunction return type).
            if (leftType == typeof(double) || rightType == typeof(double))
            {
                return typeof(double);
            }

            if (leftType == typeof(float) || rightType == typeof(float))
            {
                return typeof(double);
            }

            if (leftType == typeof(decimal) || rightType == typeof(decimal))
            {
                return typeof(double);
            }

            // Default to long for integral comparisons.
            if (leftType == typeof(long) || rightType == typeof(long)
                || leftType == typeof(ulong) || rightType == typeof(ulong))
            {
                return typeof(long);
            }

            return typeof(int);
        }

        private Expression VisitUnaryOperator(UnaryOperationNode node)
        {
            var operand = VisitNode(node.Operand);

            if (operand.Type == typeof(object))
            {
                return BuildDynamicUnaryOperation(operand, node.Operator);
            }

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

        private Expression VisitQueryFilter(QueryFilterNode node)
        {
            var sourceExpression = VisitNode(node.Source);
            var elementType = ResolveQueryableElementType(sourceExpression.Type);
            var enumerableSource = EnsureEnumerableOrEmpty(sourceExpression, elementType);

            var predicateLambda = BuildPredicateLambda(node.Filter, elementType);
            var whereMethod = GetEnumerableMethod(nameof(Enumerable.Where), 2).MakeGenericMethod(elementType);
            return Expression.Call(null, whereMethod, enumerableSource, predicateLambda);
        }

        private Expression VisitQueryOrderBy(QueryOrderByNode node)
        {
            var sourceExpression = VisitNode(node.Source);
            var elementType = ResolveQueryableElementType(sourceExpression.Type);
            var enumerableSource = EnsureEnumerableOrEmpty(sourceExpression, elementType);

            if (node.Orderings == null || node.Orderings.Count == 0)
            {
                return enumerableSource;
            }

            Expression? orderedExpression = null;
            for (var i = 0; i < node.Orderings.Count; i++)
            {
                var ordering = node.Orderings[i];
                var keySelector = BuildKeySelectorLambda(ordering.Expression, elementType);

                if (i == 0)
                {
                    var orderMethodName = ordering.Direction == OrderingDirection.Descending
                        ? nameof(Enumerable.OrderByDescending)
                        : nameof(Enumerable.OrderBy);
                    var orderByMethod = GetEnumerableMethod(orderMethodName, 2).MakeGenericMethod(elementType, typeof(object));
                    orderedExpression = Expression.Call(null, orderByMethod, enumerableSource, keySelector);
                }
                else
                {
                    var thenMethodName = ordering.Direction == OrderingDirection.Descending
                        ? nameof(Enumerable.ThenByDescending)
                        : nameof(Enumerable.ThenBy);
                    var thenByMethod = GetEnumerableMethod(thenMethodName, 2).MakeGenericMethod(elementType, typeof(object));
                    orderedExpression = Expression.Call(null, thenByMethod, orderedExpression!, keySelector);
                }
            }

            return orderedExpression ?? enumerableSource;
        }

        private Expression VisitQueryDistinct(QueryDistinctNode node)
        {
            var sourceExpression = VisitNode(node.Source);
            var elementType = ResolveQueryableElementType(sourceExpression.Type);
            var enumerableSource = EnsureEnumerableOrEmpty(sourceExpression, elementType);

            var keySelector = BuildKeySelectorLambda(node.KeySelector, elementType);
            var distinctMethod = typeof(DslQueryOperations).GetMethod(nameof(DslQueryOperations.DistinctBy), BindingFlags.Public | BindingFlags.Static);
            if (distinctMethod == null)
            {
                throw new InvalidOperationException("Cannot resolve query distinct method 'DistinctBy'.");
            }

            var genericMethod = distinctMethod.MakeGenericMethod(elementType);
            return Expression.Call(null, genericMethod, enumerableSource, keySelector);
        }

        private Expression VisitQuerySelect(QuerySelectNode node)
        {
            var sourceExpression = VisitNode(node.Source);
            var elementType = ResolveQueryableElementType(sourceExpression.Type);
            var enumerableSource = EnsureEnumerableOrEmpty(sourceExpression, elementType);

            var selector = BuildKeySelectorLambda(node.Selector, elementType);
            var selectMethod = GetEnumerableMethod(nameof(Enumerable.Select), 2).MakeGenericMethod(elementType, typeof(object));
            return Expression.Call(null, selectMethod, enumerableSource, selector);
        }


        private Expression VisitQueryTake(QueryTakeNode node)
        {
            var sourceExpression = VisitNode(node.Source);
            var elementType = ResolveQueryableElementType(sourceExpression.Type);
            var enumerableSource = EnsureEnumerableOrEmpty(sourceExpression, elementType);

            if (node.Selection == null || node.Selection.Strategy == SelectionStrategy.All)
            {
                return enumerableSource;
            }

            var countExpression = BuildCountExpression(node.Selection.CountExpression);
            var methodName = node.Selection.Strategy switch
            {
                SelectionStrategy.Random => nameof(DslQueryOperations.TakeRandom),
                SelectionStrategy.Last => nameof(DslQueryOperations.TakeLast),
                _ => nameof(DslQueryOperations.TakeFirst)
            };

            var takeMethod = typeof(DslQueryOperations).GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            if (takeMethod == null)
            {
                throw new InvalidOperationException($"Cannot resolve query take method '{methodName}'.");
            }

            var genericMethod = takeMethod.MakeGenericMethod(elementType);
            return Expression.Call(null, genericMethod, enumerableSource, countExpression);
        }

        private LambdaExpression BuildPredicateLambda(ExpressionNode filterExpression, Type elementType)
        {
            var elementParam = Expression.Parameter(elementType, "item");
            var builder = new ExpressionTreeBuilder(elementParam, _gameContextParam, _tscParam, _dbContextParam, _functionRegistry);
            var body = builder.Build(filterExpression);
            var booleanBody = EnsureBooleanExpression(body);
            return Expression.Lambda(booleanBody, elementParam);
        }

        private LambdaExpression BuildKeySelectorLambda(ExpressionNode keyExpression, Type elementType)
        {
            var elementParam = Expression.Parameter(elementType, "item");
            var builder = new ExpressionTreeBuilder(elementParam, _gameContextParam, _tscParam, _dbContextParam, _functionRegistry);
            var body = builder.Build(keyExpression);
            if (body.Type != typeof(object))
            {
                body = Expression.Convert(body, typeof(object));
            }
            return Expression.Lambda(body, elementParam);
        }

        private Expression BuildCountExpression(ExpressionNode? countExpression)
        {
            if (countExpression == null)
            {
                return Expression.Constant(0);
            }

            var rawCount = VisitNode(countExpression);
            if (rawCount.Type == typeof(int))
            {
                return rawCount;
            }

            var method = typeof(TypeConverter).GetMethod(nameof(TypeConverter.ConvertToInt), new[] { typeof(object), typeof(int) });
            if (method == null)
            {
                throw new InvalidOperationException("Cannot resolve TypeConverter.ConvertToInt.");
            }

            var converted = Expression.Convert(rawCount, typeof(object));
            return Expression.Call(null, method, converted, Expression.Constant(0));
        }

        private static Expression EnsureBooleanExpression(Expression expression)
        {
            if (expression.Type == typeof(bool))
            {
                return expression;
            }

            if (expression.Type == typeof(bool?))
            {
                return Expression.Coalesce(expression, Expression.Constant(false));
            }

            var method = typeof(TypeConverter).GetMethod(nameof(TypeConverter.ConvertToBool), new[] { typeof(object) });
            if (method == null)
            {
                throw new InvalidOperationException("Cannot resolve TypeConverter.ConvertToBool.");
            }

            return Expression.Call(null, method, Expression.Convert(expression, typeof(object)));
        }

        private static Type ResolveQueryableElementType(Type sourceType)
        {
            if (sourceType == typeof(string))
            {
                throw new InvalidOperationException("String values cannot be treated as collections.");
            }

            return DslCollectionCoercion.TryGetEnumerableElementType(sourceType, out var elementType)
                ? elementType
                : typeof(object);
        }

        private static MethodInfo GetEnumerableMethod(string name, int parameterCount)
        {
            var method = typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == name && m.GetParameters().Length == parameterCount);
            if (method == null)
            {
                throw new InvalidOperationException($"Cannot resolve Enumerable.{name}.");
            }

            return method;
        }

        private static bool RequiresDynamicOperation(Expression left, Expression right)
        {
            return left.Type == typeof(object) || right.Type == typeof(object);
        }

        private static Expression BuildDynamicBinaryOperation(Expression left, Expression right, TokenType operatorType)
        {
            var leftObj = Expression.Convert(left, typeof(object));
            var rightObj = Expression.Convert(right, typeof(object));

            MethodInfo method = operatorType switch
            {
                TokenType.Plus => GetDynamicMethod(nameof(DynamicOperations.Add)),
                TokenType.Minus => GetDynamicMethod(nameof(DslRuntimeBinaryOperations.ApplyMinus)),
                TokenType.Multiply => GetDynamicMethod(nameof(DynamicOperations.Multiply)),
                TokenType.Divide => GetDynamicMethod(nameof(DynamicOperations.Divide)),
                TokenType.DivideCeil => GetDynamicMethod(nameof(DynamicOperations.CeilingDivide)),
                TokenType.Modulo => GetDynamicMethod(nameof(DynamicOperations.Modulo)),
                TokenType.GreaterThan => GetDynamicMethod(nameof(DynamicOperations.IsGreaterThan)),
                TokenType.GreaterEqual => GetDynamicMethod(nameof(DynamicOperations.IsGreaterThanOrEqual)),
                TokenType.LessThan => GetDynamicMethod(nameof(DynamicOperations.IsLessThan)),
                TokenType.LessEqual => GetDynamicMethod(nameof(DynamicOperations.IsLessThanOrEqual)),
                TokenType.Equal => GetDynamicMethod(nameof(DynamicOperations.AreEqual)),
                TokenType.NotEqual => GetDynamicMethod(nameof(DynamicOperations.AreEqual)),
                TokenType.SetUnion => GetDynamicMethod(nameof(DynamicOperations.SetUnion)),
                TokenType.SetIntersect => GetDynamicMethod(nameof(DynamicOperations.SetIntersect)),
                TokenType.SetDifference => GetDynamicMethod(nameof(DynamicOperations.SetDifference)),
                TokenType.And => GetDynamicMethod(nameof(DynamicOperations.LogicalAnd)),
                TokenType.Or => GetDynamicMethod(nameof(DynamicOperations.LogicalOr)),
                _ => throw new NotSupportedException($"Binary operator '{operatorType}' is not supported for dynamic evaluation.")
            };

            var call = Expression.Call(null, method, leftObj, rightObj);
            return operatorType == TokenType.NotEqual ? Expression.Not(call) : call;
        }

        private static Expression BuildDynamicUnaryOperation(Expression operand, TokenType operatorType)
        {
            var operandObj = Expression.Convert(operand, typeof(object));
            MethodInfo method = operatorType switch
            {
                TokenType.Not => GetDynamicMethod(nameof(DynamicOperations.LogicalNot)),
                TokenType.Minus => GetDynamicMethod(nameof(DynamicOperations.Negate)),
                _ => throw new NotSupportedException($"Unary operator '{operatorType}' is not supported for dynamic evaluation.")
            };

            return Expression.Call(null, method, operandObj);
        }

        private static MethodInfo GetDynamicMethod(string name)
        {
            var method = typeof(DynamicOperations).GetMethod(name, BindingFlags.Public | BindingFlags.Static);
            if (method != null)
            {
                return method;
            }

            method = typeof(DslRuntimeBinaryOperations).GetMethod(name, BindingFlags.Public | BindingFlags.Static);
            if (method != null)
            {
                return method;
            }

            throw new InvalidOperationException($"Cannot resolve dynamic DSL operation '{name}'.");
        }
    }

    internal static class DslDictionaryAccess
    {
        public static TValue? GetValueOrDefault<TValue>(IReadOnlyDictionary<string, TValue> dictionary, string key)
        {
            if (dictionary == null || string.IsNullOrWhiteSpace(key))
            {
                return default;
            }

            if (dictionary.TryGetValue(key, out var value))
            {
                return value;
            }

            foreach (var entry in dictionary)
            {
                if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Value;
                }
            }

            return default;
        }

        public static TValue? GetValueOrDefault<TValue>(IDictionary<string, TValue> dictionary, string key)
        {
            if (dictionary == null || string.IsNullOrWhiteSpace(key))
            {
                return default;
            }

            if (dictionary.TryGetValue(key, out var value))
            {
                return value;
            }

            foreach (var entry in dictionary)
            {
                if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Value;
                }
            }

            return default;
        }
    }

    internal static class EventParameterAccessor
    {
        public static object? Resolve(GameContext context, IGameEvent? gameEvent, string name)
        {
            if (gameEvent == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var eventType = gameEvent.GetType();
            if (EventParameterCache.TryGetBinding(eventType, name, out var binding))
            {
                try
                {
                    return binding.Getter(gameEvent);
                }
                catch
                {
                    return null;
                }
            }

            if (gameEvent is IRawEventBackedGameEvent rawBacked
                && TryGetJsonValue(rawBacked.OutputPayload, name, out var node))
            {
                return node;
            }

            var prop = eventType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop == null || prop.GetIndexParameters().Length != 0)
            {
                return null;
            }

            try
            {
                return prop.GetValue(gameEvent);
            }
            catch
            {
                return null;
            }
        }

        private static bool TryGetJsonValue(JsonObject obj, string name, out JsonNode? node)
        {
            node = null;
            foreach (var kv in obj)
            {
                if (!string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                node = kv.Value;
                return true;
            }

            return false;
        }
    }

    internal static class DslRuntimeMemberAccess
    {
        private static readonly ConcurrentDictionary<(Type Type, string Member), Func<object, object?>> MemberAccessorCache = new();
        private static readonly ConcurrentDictionary<(Type Type, string Member), Func<object, object?>> DefinitionAccessorCache = new();

        public static object? GetValue(object? instance, string memberName)
        {
            if (instance == null || string.IsNullOrWhiteSpace(memberName))
            {
                return null;
            }

            if (memberName.Equals("Count", StringComparison.OrdinalIgnoreCase)
                && instance is ICollection collection)
            {
                return collection.Count;
            }

            if (memberName.Equals("IsEmpty", StringComparison.OrdinalIgnoreCase)
                && instance is ICollection collectionIsEmpty)
            {
                return collectionIsEmpty.Count == 0;
            }

            if (instance is JsonObject jsonObject)
            {
                foreach (var kv in jsonObject)
                {
                    if (string.Equals(kv.Key, memberName, StringComparison.OrdinalIgnoreCase))
                    {
                        return kv.Value;
                    }
                }
            }

            if (instance is IReadOnlyDictionary<string, object?> readOnlyDictionary)
            {
                if (readOnlyDictionary.TryGetValue(memberName, out var value))
                {
                    return value;
                }

                foreach (var entry in readOnlyDictionary)
                {
                    if (string.Equals(entry.Key, memberName, StringComparison.OrdinalIgnoreCase))
                    {
                        return entry.Value;
                    }
                }
            }

            if (instance is IReadOnlyDictionary<string, int> readOnlyIntDictionary)
            {
                if (readOnlyIntDictionary.TryGetValue(memberName, out var value))
                {
                    return value;
                }

                foreach (var entry in readOnlyIntDictionary)
                {
                    if (string.Equals(entry.Key, memberName, StringComparison.OrdinalIgnoreCase))
                    {
                        return entry.Value;
                    }
                }
            }

            if (instance is IDictionary dictionary)
            {
                if (dictionary.Contains(memberName))
                {
                    return dictionary[memberName];
                }

                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key is string key
                        && string.Equals(key, memberName, StringComparison.OrdinalIgnoreCase))
                    {
                        return entry.Value;
                    }
                }
            }

            var type = instance.GetType();
            var normalizedMember = memberName.ToUpperInvariant();

            var accessor = MemberAccessorCache.GetOrAdd(
                (type, normalizedMember),
                static key => BuildMemberAccessor(key.Type, key.Member));

            return accessor(instance);
        }

        private static Func<object, object?> BuildMemberAccessor(Type instanceType, string normalizedMember)
        {
            var flags = BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance;

            var property = instanceType.GetProperty(normalizedMember, flags);
            if (property != null && property.GetIndexParameters().Length == 0)
            {
                return CreatePropertyGetter(instanceType, property);
            }

            var field = instanceType.GetField(normalizedMember, flags);
            if (field != null)
            {
                return CreateFieldGetter(instanceType, field);
            }

            if (typeof(CardInstance).IsAssignableFrom(instanceType))
            {
                return instance =>
                {
                    var card = instance as CardInstance;
                    var definition = card?.CurrentDefinition;
                    if (definition == null)
                    {
                        return null;
                    }

                    return GetDefinitionMemberValue(definition, normalizedMember);
                };
            }

            return static _ => null;
        }

        private static object? GetDefinitionMemberValue(object definition, string normalizedMember)
        {
            var definitionType = definition.GetType();
            var accessor = DefinitionAccessorCache.GetOrAdd(
                (definitionType, normalizedMember),
                static key => BuildDefinitionAccessor(key.Type, key.Member));

            return accessor(definition);
        }

        private static Func<object, object?> BuildDefinitionAccessor(Type definitionType, string normalizedMember)
        {
            var flags = BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance;

            var property = definitionType.GetProperty(normalizedMember, flags);
            if (property != null && property.GetIndexParameters().Length == 0)
            {
                return CreatePropertyGetter(definitionType, property);
            }

            var field = definitionType.GetField(normalizedMember, flags);
            if (field != null)
            {
                return CreateFieldGetter(definitionType, field);
            }

            return static _ => null;
        }

        private static Func<object, object?> CreatePropertyGetter(Type declaringType, PropertyInfo propertyInfo)
        {
            var instanceParam = Expression.Parameter(typeof(object), "instance");
            var typedInstance = Expression.Convert(instanceParam, declaringType);
            var access = Expression.Property(typedInstance, propertyInfo);
            var boxed = Expression.Convert(access, typeof(object));
            return Expression.Lambda<Func<object, object?>>(boxed, instanceParam).Compile();
        }

        private static Func<object, object?> CreateFieldGetter(Type declaringType, FieldInfo fieldInfo)
        {
            var instanceParam = Expression.Parameter(typeof(object), "instance");
            var typedInstance = Expression.Convert(instanceParam, declaringType);
            var access = Expression.Field(typedInstance, fieldInfo);
            var boxed = Expression.Convert(access, typeof(object));
            return Expression.Lambda<Func<object, object?>>(boxed, instanceParam).Compile();
        }
    }

    internal static class DslRuntimeBinaryOperations
    {
        public static object? ApplyMinus(object? left, object? right)
        {
            return DynamicOperations.Subtract(left, right);
        }
    }
}
