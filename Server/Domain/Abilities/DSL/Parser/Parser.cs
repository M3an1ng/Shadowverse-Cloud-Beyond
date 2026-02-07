using ShadowverseBeyond.Server.Domain.Abilities.DSL.AST;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Parser
{
    /// <summary>
    /// 语法分析器
    /// 将词法单元序列转换为抽象语法树(AST)
    /// </summary>
    public class Parser
    {
        private readonly List<Token> _tokens;
        private int _current;
        public List<ParseError> Errors { get; } = new List<ParseError>();

        /// <summary>
        /// 当前词法单元
        /// </summary>
        private Token? CurrentToken => _current < _tokens.Count ? _tokens[_current] : null;

        /// <summary>
        /// 下一个词法单元
        /// </summary>
        private Token PeekToken => _current + 1 < _tokens.Count ? _tokens[_current + 1] : _tokens.Last();

        /// <summary>
        /// 是否到达末尾
        /// </summary>
        private bool IsAtEnd => CurrentToken == null || CurrentToken.Type == TokenType.EOF;


        public Parser(List<Token> tokens)
        {
            _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
            _current = 0;
            // Errors list is initialized inline now

            // 过滤掉空白字符和注释
            _tokens = _tokens.Where(t => t.Type != TokenType.Whitespace
                && t.Type != TokenType.Comment
                && t.Type != TokenType.Newline).ToList();
        }

        // --- Public Entry Points ---



        /// <summary>
        /// 解析单个核心表达式 (用于 ConditionConfig.Expression 或 ValueConfig.Expression)
        /// </summary>
        /// <returns>表达式的AST根节点，如果解析失败则返回 null</returns>
        public ExpressionNode? ParseSingleExpression()
        {
            try
            {
                // 确保我们从头开始解析，并且只解析一个表达式
                _current = 0; // Reset position
                var expression = ParseExpression();

                // 检查解析后是否还有未处理的非EOF Token，这表示输入不仅仅是一个表达式
                if (!IsAtEnd)
                {
                    AddError($"Unexpected token '{CurrentToken!.Type}' after expression. Expected EOF.", CurrentToken!.Position);
                }

                if (Errors.Any())
                {
                    return null; // 返回 null 表示解析失败
                }

                return expression;
            }
            catch (ParseException ex)
            {
                AddError(ex.Message, CurrentToken?.Position ?? new TokenPosition(0, 0, 0));
                return null; // 返回 null 表示解析失败
            }
            catch (Exception ex) // Catch unexpected errors during parsing
            {
                AddError($"An unexpected error occurred during expression parsing: {ex.Message}", CurrentToken?.Position ?? new TokenPosition(0, 0, 0));
                return null;
            }
        }

        /*
        /// <summary>
        /// 解析目标表达式 (用于 TargetConfig.SourcePoolExpression 等)
        /// 语法: source_pool_expression [where filter_expression] [order by ordering_clause] [take selection_clause]
        /// </summary>
        /// <returns>目标表达式的AST根节点，如果解析失败则返回 null</returns>
        public TargetExpressionNode? ParseTargetExpression()
        {
            Errors.Clear(); // Clear errors for new parse run
            _current = 0; // Reset position

            try
            {
                var sourcePoolStartPosition = CurrentToken?.Position ?? new TokenPosition(0, 0, 0); // Capture start position specifically for source pool

                // 1. 解析源池表达式 (source_pool)
                var sourcePoolExpr = ParseExpression();

                // --- Type Check for Source Pool ---
                if (sourcePoolExpr is not FunctionCallNode && sourcePoolExpr is not IdentifierNode)
                {
                    AddError("Target expression source pool must start with a function call (e.g., UnitsInZone(...)) or an identifier (e.g., Event.Targets).",
                             sourcePoolStartPosition); // Use the captured start position for error reporting
                    // Even with error, proceed with the parsed node to potentially find more errors,
                    // but the resulting TargetExpressionNode might be invalid.
                }
                // --- End Type Check ---

                var targetNode = new TargetExpressionNode
                {
                    SourcePool = sourcePoolExpr,
                    Location = sourcePoolExpr.Location // Initial location from source pool
                };

                // 2. Parse optional 'where' clause
                if (Match(TokenType.Where))
                {
                    targetNode.Filter = ParseExpression(); // Placeholder
                }

                // 3. Parse optional 'order by' clause
                if (Match(TokenType.Order))
                {
                    Expect(TokenType.By);
                    targetNode.Orderings = ParseOrderingClause(); // Call helper
                }

                // 4. Parse optional 'take' clause
                if (Match(TokenType.Take))
                {
                    targetNode.Selection = ParseSelectionClause(); // Call helper
                }

                // Ensure the entire input was consumed
                if (!IsAtEnd && CurrentToken?.Type != TokenType.EOF)
                {
                    AddError($"Unexpected token '{CurrentToken?.Type}' after target expression. Expected end of input.", CurrentToken?.Position ?? new TokenPosition(0, 0, 0));
                }

                if (Errors.Any())
                {
                    return null; // Return null on error
                }

                // TODO: Update end location accurately
                // var endToken = Previous();
                // if (targetNode.Location != null && endToken != null) { ... }

                // Update the overall location of the TargetExpressionNode
                if (!Errors.Any()) // Only update location if parsing was generally successful
                {
                    // It should span from the start of the source pool to the end of the last parsed token
                    // Note: Previous() might point to EOF if the expression ends exactly at EOF.
                    var endToken = _current > 0 ? Previous() : _tokens.FirstOrDefault(); // Get last consumed token, handle edge case of empty input
                    if (endToken != null)
                    {
                        var startLoc = ConvertPosition(sourcePoolStartPosition);
                        var endLoc = ConvertPosition(endToken.Position);
                        targetNode.Location = MergeLocations(startLoc, endLoc);
                    }
                    // If endToken is null (empty input), the initial location from sourcePoolExpr remains.
                }
                // If there were errors, keep the initial location from sourcePoolExpr as a fallback.
                // The comment below still needs fixing separately:
                // if (targetNode.Location != null && endToken != null) { ... }

                return targetNode;
            }
            catch (ParseException ex)
            {
                // Error already added if thrown by helper Expect methods
                if (!Errors.Any(e => e.Message == ex.Message)) // Avoid duplicate errors
                {
                    AddError(ex.Message, CurrentToken?.Position ?? new TokenPosition(0, 0, 0));
                }
                return null; // Return null on error
            }
            catch (Exception ex) // Catch unexpected errors
            {
                AddError($"An unexpected error occurred during target expression parsing: {ex.Message}", CurrentToken?.Position ?? new TokenPosition(0, 0, 0));
                return null;
            }
        }

        /// <summary>
        /// 解析完整的能力定义 (顶级入口)
        /// 语法: ABILITY <name> [TAG <tag>] [TYPE <type>] [TRIGGER ...] [CONDITION ...] [TARGET ...] [EFFECT ...] [AURA ...] [PROPERTY ...] END_ABILITY
        /// </summary>
        /// <returns>能力的AST根节点，如果解析失败则返回 null</returns>
        public AbilityNode? ParseAbility()
        {
            Errors.Clear(); // Clear errors for new parse run
            _current = 0; // Reset position

            try
            {
                ExpectIdentifier("ability"); // Expect ABILITY keyword
                var abilityNameToken = ExpectIdentifierWithMessage("Expected ability name after ABILITY keyword.");
                var abilityNode = new AbilityNode { Name = abilityNameToken.Value };
                abilityNode.Location = ConvertPosition(abilityNameToken.Position); // Initial location

                // Optional TAG
                if (MatchIdentifier("tag"))
                {
                    var tagToken = ExpectIdentifierWithMessage("Expected tag name after TAG keyword.");
                    abilityNode.Tag = tagToken.Value;
                    // TODO: Update end location
                }

                // Optional TYPE
                if (MatchIdentifier("type"))
                {
                    var typeToken = ExpectIdentifierWithMessage("Expected type name after TYPE keyword.");
                    abilityNode.Type = typeToken.Value;
                    // TODO: Update end location
                }

                // 解析能力的各个块：TRIGGER, CONDITION, TARGET, EFFECT, PROPERTY
                while (!CheckIdentifier("end_ability") && !IsAtEnd)
                {
                    if (MatchIdentifier("trigger"))
                    {
                        abilityNode.Trigger = ParseTriggerBlock();
                        ExpectIdentifier("end_trigger");
                    }
                    else if (MatchIdentifier("condition"))
                    {
                        abilityNode.Conditions.Add(ParseConditionBlock());
                        ExpectIdentifier("end_condition");
                    }
                    else if (MatchIdentifier("target"))
                    {
                        abilityNode.Targets.Add(ParseTargetBlock());
                        ExpectIdentifier("end_target");
                    }
                    else if (MatchIdentifier("effect"))
                    {
                        abilityNode.Effects.Add(ParseEffectBlock());
                        ExpectIdentifier("end_effect");
                    }
                    else if (MatchIdentifier("property"))
                    {
                        abilityNode.Properties.Add(ParsePropertyBlock());
                        ExpectIdentifier("end_property");
                    }
                    else if (Match(TokenType.Semicolon))
                    {
                        continue;
                    }
                    else
                    {
                        AddError($"Unexpected token '{CurrentToken!.Type}' in ability definition. Expected TRIGGER, CONDITION, TARGET, EFFECT, PROPERTY, or END_ABILITY.", CurrentToken!.Position);
                        Advance(); // Skip unexpected token to avoid infinite loop
                    }
                }

                ExpectIdentifier("end_ability"); // Expect END_ABILITY keyword
                // TODO: Update final end location accurately

                if (!IsAtEnd)
                {
                    AddError($"Unexpected token '{CurrentToken!.Type}' after END_ABILITY.", CurrentToken!.Position);
                }

                if (Errors.Any()) return null;

                return abilityNode;
            }
            catch (ParseException ex)
            {
                if (!Errors.Any(e => e.Message == ex.Message))
                {
                    AddError(ex.Message, CurrentToken?.Position ?? new TokenPosition(0, 0, 0));
                }
                return null;
            }
            catch (Exception ex)
            {
                AddError($"An unexpected error occurred during ability parsing: {ex.Message}", CurrentToken?.Position ?? new TokenPosition(0, 0, 0));
                return null;
            }
        }

        /// <summary>
        /// 解析条件块的内容
        /// 语法: expression [AND|OR expression]*
        /// </summary>
        private ConditionNode ParseConditionBlock()
        {
            var conditionNode = new ConditionNode();

            // 解析主要条件表达式
            conditionNode.Expression = ParseExpression();

            // 检查是否有逻辑操作符
            if (Check(TokenType.And) || Check(TokenType.Or))
            {
                var logicOp = Advance();
                conditionNode.LogicOperator = logicOp.Type == TokenType.And ? "and" : "or";
            }

            return conditionNode;
        }

        /// <summary>
        /// 解析目标块的内容
        /// 语法: TYPE type [SCOPE scope] [SELECTION selection] [COUNT count] [FILTER filter]*
        /// </summary>
        private TargetNode ParseTargetBlock()
        {
            var targetNode = new TargetNode();

            // 解析目标类型
            if (Match(TokenType.Identifier))
            {
                targetNode.Type = Previous().Value;
            }

            // 解析可选的范围、选择方法等
            while (!CheckIdentifier("end_target") && !IsAtEnd)
            {
                if (Match(TokenType.Identifier))
                {
                    var keyword = Previous().Value.ToLowerInvariant();
                    switch (keyword)
                    {
                        case "scope":
                            if (Match(TokenType.Identifier))
                                targetNode.Scope = Previous().Value;
                            break;
                        case "selection":
                            if (Match(TokenType.Identifier))
                                targetNode.Selection = Previous().Value;
                            break;
                        case "count":
                            targetNode.Count = ParseExpression();
                            break;
                        default:
                            AddError($"Unknown target keyword: {keyword}", Previous().Position);
                            break;
                    }
                }
                else
                {
                    break; // 退出循环，让外层处理
                }
            }

            return targetNode;
        }

        /// <summary>
        /// 解析效果块的内容
        /// 语法: TYPE type [TARGET target] [AMOUNT amount] [PARAMETER param]*
        /// </summary>
        private EffectNode ParseEffectBlock()
        {
            var effectNode = new EffectNode();

            // 解析效果类型
            if (Match(TokenType.Identifier))
            {
                effectNode.Type = Previous().Value;
            }

            // 解析可选的目标、数量等
            while (!CheckIdentifier("end_effect") && !IsAtEnd)
            {
                if (Match(TokenType.Identifier))
                {
                    var keyword = Previous().Value.ToLowerInvariant();
                    switch (keyword)
                    {
                        case "target":
                            // 这里可以解析内联目标或引用
                            break;
                        case "amount":
                            effectNode.Amount = ParseExpression();
                            break;
                        default:
                            AddError($"Unknown effect keyword: {keyword}", Previous().Position);
                            break;
                    }
                }
                else
                {
                    break; // 退出循环，让外层处理
                }
            }

            return effectNode;
        }

        /// <summary>
        /// 解析属性块的内容
        /// 语法: NAME name VALUE value
        /// </summary>
        private PropertyNode ParsePropertyBlock()
        {
            var propertyNode = new PropertyNode();

            // 解析属性名
            if (Match(TokenType.Identifier))
            {
                propertyNode.Name = Previous().Value;
            }

            // 解析属性值
            if (Match(TokenType.Identifier) && Previous().Value.ToLowerInvariant() == "value")
            {
                propertyNode.Value = ParseExpression();
            }

            return propertyNode;
        }

        private TriggerNode ParseTriggerBlock()
        {
            var triggerNode = new TriggerNode();

            if (MatchIdentifier("event") || MatchIdentifier("on"))
            {
                ParseEventTypes(triggerNode, "Expected event type after EVENT keyword.");
            }
            else if (Check(TokenType.Identifier) && !IsTriggerOptionKeyword(CurrentToken!.Value))
            {
                ParseEventTypes(triggerNode, "Expected event type in trigger block.");
            }
            else
            {
                AddError("Expected event type in trigger block.", CurrentToken?.Position ?? new TokenPosition(0, 0, 0));
            }

            var stopKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "end_trigger"
            };
            ParseTriggerOptions(triggerNode, stopKeywords, allowEventKeyword: true);

            return triggerNode;
        }

        private void ParseEventTypes(TriggerNode triggerNode, string errorMessage)
        {
            if (!Match(TokenType.Identifier))
            {
                AddError(errorMessage, CurrentToken?.Position ?? new TokenPosition(0, 0, 0));
                return;
            }

            triggerNode.EventTypes.Add(Previous().Value);
            while (Match(TokenType.Comma))
            {
                if (Match(TokenType.Identifier))
                {
                    triggerNode.EventTypes.Add(Previous().Value);
                }
                else
                {
                    AddError("Expected event type after ','.", CurrentToken?.Position ?? new TokenPosition(0, 0, 0));
                    break;
                }
            }
        }

        private void ParseTriggerOptions(TriggerNode triggerNode, HashSet<string> stopKeywords, bool allowEventKeyword)
        {
            while (!IsAtEnd)
            {
                if (Match(TokenType.Semicolon))
                {
                    break;
                }

                if (Check(TokenType.Identifier) && stopKeywords.Contains(CurrentToken!.Value))
                {
                    break;
                }

                if (allowEventKeyword && (MatchIdentifier("event") || MatchIdentifier("on")))
                {
                    ParseEventTypes(triggerNode, "Expected event type after EVENT keyword.");
                    continue;
                }

                if (Match(TokenType.Identifier))
                {
                    var keyword = Previous().Value.ToLowerInvariant();
                    switch (keyword)
                    {
                        case "scope":
                            if (Match(TokenType.Identifier))
                                triggerNode.Scope = Previous().Value;
                            break;
                        case "relation":
                            if (Match(TokenType.Identifier))
                                triggerNode.Relation = Previous().Value;
                            break;
                        case "timing":
                            if (Match(TokenType.Identifier))
                                triggerNode.Timing = Previous().Value;
                            break;
                        case "zone":
                        case "zones":
                            ParseZoneList(triggerNode);
                            break;
                        case "if":
                            triggerNode.Condition = ParseExpression();
                            break;
                        case "optional":
                            triggerNode.IsOptional = true;
                            break;
                        default:
                            AddError($"Unknown trigger keyword: {keyword}", Previous().Position);
                            break;
                    }
                }
                else
                {
                    break;
                }
            }
        }

        private void ParseZoneList(TriggerNode triggerNode)
        {
            if (!Match(TokenType.Identifier))
            {
                AddError("Expected zone name after ZONE keyword.", CurrentToken?.Position ?? new TokenPosition(0, 0, 0));
                return;
            }

            triggerNode.Zones.Add(Previous().Value);
            while (Match(TokenType.Comma))
            {
                if (Match(TokenType.Identifier))
                {
                    triggerNode.Zones.Add(Previous().Value);
                }
                else
                {
                    AddError("Expected zone name after ','.", CurrentToken?.Position ?? new TokenPosition(0, 0, 0));
                    break;
                }
            }
        }

        private static bool IsTriggerOptionKeyword(string value)
        {
            switch (value.ToLowerInvariant())
            {
                case "scope":
                case "relation":
                case "timing":
                case "zone":
                case "zones":
                case "if":
                case "optional":
                    return true;
                default:
                    return false;
            }
        }

        // --- Clause Parsing Helpers (New for Target Expressions) ---

        /// <summary>
        /// 解析 order by 子句的内容
        /// 语法: expression [asc|desc] [, expression [asc|desc]]*
        /// </summary>
        private List<OrderingTerm> ParseOrderingClause()
        {
            var orderings = new List<OrderingTerm>();

            // 解析排序项，支持逗号分隔的多个排序条件
            do
            {
                var startPosition = CurrentToken?.Position ?? new TokenPosition(0, 0, 0);
                var expression = ParseExpression(); // The expression to sort by
                var direction = OrderingDirection.Ascending; // Default direction
                var endToken = Previous(); // Token at the end of the expression

                // Optional direction specifier
                if (Match(TokenType.Asc))
                {
                    direction = OrderingDirection.Ascending;
                    endToken = Previous(); // Update end token
                }
                else if (Match(TokenType.Desc))
                {
                    direction = OrderingDirection.Descending;
                    endToken = Previous(); // Update end token
                }

                var term = new OrderingTerm
                {
                    Expression = expression,
                    Direction = direction,
                    Location = MergeLocations(expression.Location, ConvertPosition(endToken.Position)) // Span from expression start to direction end (or expression end if no direction)
                };
                orderings.Add(term);

            } while (Match(TokenType.Comma)); // Allow multiple ordering terms separated by comma

            if (!orderings.Any())
            {
                // This shouldn't happen if called after 'order by', but good practice to check
                AddError("Expected at least one ordering expression after 'order by'.", CurrentToken?.Position ?? new TokenPosition(0, 0, 0));
            }

            return orderings; // Add the return statement
        }


        private SelectionTerm ParseSelectionClause()
        {
            var startPosition = CurrentToken?.Position ?? new TokenPosition(1, 1, 0);
            var strategy = SelectionStrategy.All;
            ExpressionNode? countExpr = null;

            var strategyToken = ExpectIdentifierWithMessage("Expected selection strategy (first, last, random, all) after 'take'.");
            var strategyWord = strategyToken.Value.ToLowerInvariant();

            switch (strategyWord)
            {
                case "first":
                    strategy = SelectionStrategy.First;
                    countExpr = ParseExpression();
                    break;
                case "last":
                    strategy = SelectionStrategy.Last;
                    countExpr = ParseExpression();
                    break;
                case "random":
                    strategy = SelectionStrategy.Random;
                    countExpr = ParseExpression();
                    break;
                case "all":
                    strategy = SelectionStrategy.All;
                    if (!IsAtEnd && !IsNextTokenAClauseKeyword())
                    {
                        var currentPosBeforeAttempt = _current;
                        try
                        {
                            ParseExpression();
                            AddError("Unexpected expression after 'take all'. 'all' does not take a count.", Previous().Position);
                        }
                        catch (ParseException)
                        {
                            _current = currentPosBeforeAttempt;
                        }
                    }
                    break;
                default:
                    AddError($"Unknown selection strategy '{strategyWord}'. Expected first, last, random, or all.", strategyToken.Position);
                    strategy = SelectionStrategy.All;
                    break;
            }

            if (strategy != SelectionStrategy.All && countExpr == null)
            {
                AddError($"Selection strategy '{strategy.ToString().ToLower()}' requires a count expression.", strategyToken.Position);
                countExpr = new LiteralNode { Value = 1, DataType = "number", Location = ConvertPosition(strategyToken.Position) };
            }

            var endLocation = countExpr?.Location ?? ConvertPosition(strategyToken.Position);

            return new SelectionTerm
            {
                Strategy = strategy,
                CountExpression = countExpr,
                Location = MergeLocations(ConvertPosition(startPosition), endLocation)
            };
        }

        // --- Helper for Merging Source Locations ---
        private SourceLocation? MergeLocations(SourceLocation? startLoc, SourceLocation? endLoc)
        {
            if (startLoc == null || endLoc == null) return startLoc ?? endLoc; // Return non-null one if possible

            // Basic merge: assumes endLoc comes after startLoc
            return new SourceLocation
            {
                StartLine = startLoc.StartLine,
                StartColumn = startLoc.StartColumn,
                EndLine = endLoc.EndLine,
                EndColumn = endLoc.EndColumn
            };
        }
        /// <summary>
        /// Checks if the next token signifies the start of a known clause (where, order, take).
        /// Used to determine if unexpected tokens follow 'take all'.
        /// </summary>
        private bool IsNextTokenAClauseKeyword()
        {
            if (IsAtEnd) return false; // No token means no clause keyword
            return Check(TokenType.Where, TokenType.Order, TokenType.Take);
        }
        */
        /// <summary>
        /// 解析表达式
        /// </summary>
        public ExpressionNode ParseExpression()
        {
            return ParseLogicalOr();
        }

        /// <summary>
        /// 解析逻辑或表达式
        /// </summary>
        private ExpressionNode ParseLogicalOr()
        {
            var expr = ParseLogicalAnd();

            while (Match(TokenType.Or))
            {
                var opToken = Previous(); // Get the token
                var right = ParseLogicalAnd();
                expr = new BinaryOperationNode
                {
                    Left = expr,
                    Operator = opToken.Type, // Assign TokenType
                    Right = right,

                };
            }

            return expr;
        }

        /// <summary>
        /// 解析逻辑与表达式
        /// </summary>
        private ExpressionNode ParseLogicalAnd()
        {
            var expr = ParseEquality();

            while (Match(TokenType.And))
            {
                var opToken = Previous(); // Get the token
                var right = ParseEquality();
                expr = new BinaryOperationNode
                {
                    Left = expr,
                    Operator = opToken.Type, // Assign TokenType
                    Right = right,

                };
            }

            return expr;
        }

        /// <summary>
        /// 解析相等性表达式
        /// </summary>
        private ExpressionNode ParseEquality()
        {
            var expr = ParseComparison();

            while (Match(TokenType.Equal, TokenType.NotEqual))
            {
                var opToken = Previous(); // Get the token
                var right = ParseComparison();
                expr = new BinaryOperationNode
                {
                    Left = expr,
                    Operator = opToken.Type, // Assign TokenType
                    Right = right,

                };
            }

            return expr;
        }

        /// <summary>
        /// 解析比较表达式
        /// </summary>
        private ExpressionNode ParseComparison()
        {
            var expr = ParseSetUnion();

            while (Match(TokenType.GreaterThan, TokenType.GreaterEqual,
                        TokenType.LessThan, TokenType.LessEqual))
            {
                var opToken = Previous(); // Get the token
                var right = ParseSetUnion();
                expr = new BinaryOperationNode
                {
                    Left = expr,
                    Operator = opToken.Type, // Assign TokenType
                    Right = right,

                };
            }

            return expr;
        }

        /// <summary>
        /// 解析集合并集表达式
        /// </summary>
        private ExpressionNode ParseSetUnion()
        {
            var expr = ParseSetIntersect();

            while (Match(TokenType.SetUnion))
            {
                var opToken = Previous();
                var right = ParseSetIntersect();
                expr = new BinaryOperationNode
                {
                    Left = expr,
                    Operator = opToken.Type,
                    Right = right,
                };
            }

            return expr;
        }

        /// <summary>
        /// 解析集合交集表达式
        /// </summary>
        private ExpressionNode ParseSetIntersect()
        {
            var expr = ParseTerm();

            while (Match(TokenType.SetIntersect, TokenType.SetDifference))
            {
                var opToken = Previous();
                var right = ParseTerm();
                expr = new BinaryOperationNode
                {
                    Left = expr,
                    Operator = opToken.Type,
                    Right = right,
                };
            }

            return expr;
        }

        /// <summary>
        /// 解析项表达式
        /// </summary>
        private ExpressionNode ParseTerm()
        {
            var expr = ParseFactor();

            while (Match(TokenType.Plus, TokenType.Minus))
            {
                var opToken = Previous(); // Get the token
                var right = ParseFactor();
                expr = new BinaryOperationNode
                {
                    Left = expr,
                    Operator = opToken.Type, // Assign TokenType
                    Right = right,

                };
            }

            return expr;
        }

        /// <summary>
        /// 解析因子表达式
        /// </summary>
        private ExpressionNode ParseFactor()
        {
            var expr = ParseUnary();

            while (Match(TokenType.Multiply, TokenType.Divide, TokenType.DivideCeil, TokenType.Modulo))
            {
                var opToken = Previous(); // Get the token
                var right = ParseUnary();
                expr = new BinaryOperationNode
                {
                    Left = expr,
                    Operator = opToken.Type, // Assign TokenType
                    Right = right,

                };
            }

            return expr;
        }

        /// <summary>
        /// 解析一元表达式
        /// </summary>
        private ExpressionNode ParseUnary()
        {
            if (Match(TokenType.Not, TokenType.Minus))
            {
                var opToken = Previous(); // Get the token
                var right = ParseUnary();
                return new UnaryOperationNode
                {
                    Operator = opToken.Type, // Assign TokenType
                    Operand = right,
                    IsPrefix = true,

                };
            }

            return ParseCall();
        }

        /// <summary>
        /// 解析函数调用表达式
        /// </summary>
        private ExpressionNode ParseCall()
        {
            var expr = ParsePrimary();

            while (true)
            {
                if (Match(TokenType.LeftParen))
                {
                    expr = FinishCall(expr);
                }
                else if (Match(TokenType.Dot))
                {
                    var nameToken = ExpectIdentifierWithMessage("Expected property name after '.'");
                    var memberIdentifier = new IdentifierNode { Name = nameToken.Value, Path = new List<string>(), Location = ConvertPosition(nameToken.Position) };
                    memberIdentifier.Path.Add(nameToken.Value); // Keep the path consistent for now, although it's just one segment.

                    expr = new MemberAccessNode
                    {
                        Target = expr,
                        Member = memberIdentifier,
                    };
                }
                else
                {
                    break;
                }
            }

            return expr;
        }

        /// <summary>
        /// 完成函数调用解析
        /// </summary>
        private ExpressionNode FinishCall(ExpressionNode callee)
        {
            if (callee is MemberAccessNode memberAccess && IsQueryMethod(memberAccess.Member.Name))
            {
                return ParseQueryMethodCall(memberAccess);
            }

            var arguments = new List<ExpressionNode>();

            if (!Check(TokenType.RightParen))
            {
                do
                {
                    arguments.Add(ParseExpression());
                } while (Match(TokenType.Comma));
            }

            Expect(TokenType.RightParen);

            if (callee is IdentifierNode identifier)
            {
                return new FunctionCallNode
                {
                    FunctionName = identifier.Name,
                    Arguments = arguments
                };
            }

            if (callee is MemberAccessNode memberAccessCall)
            {
                // Treat member calls like list.find(x) as find(list, x)
                var callArguments = new List<ExpressionNode> { memberAccessCall.Target };
                callArguments.AddRange(arguments);
                return new FunctionCallNode
                {
                    FunctionName = memberAccessCall.Member.Name,
                    Arguments = callArguments
                };
            }

            throw new ParseException("Invalid function call");
        }

        private static bool IsQueryMethod(string name)
        {
            return name.Equals("query", StringComparison.OrdinalIgnoreCase)
                || name.Equals("orderby", StringComparison.OrdinalIgnoreCase)
                || name.Equals("take", StringComparison.OrdinalIgnoreCase)
                || name.Equals("distinct", StringComparison.OrdinalIgnoreCase)
                || name.Equals("select", StringComparison.OrdinalIgnoreCase);
        }

        private ExpressionNode ParseQueryMethodCall(MemberAccessNode memberAccess)
        {
            var methodName = memberAccess.Member.Name;
            var source = memberAccess.Target;

            if (methodName.Equals("query", StringComparison.OrdinalIgnoreCase))
            {
                if (Check(TokenType.RightParen))
                {
                    AddError("Query operation requires a filter expression.", CurrentToken?.Position ?? new TokenPosition(0, 0, 0));
                    Expect(TokenType.RightParen);
                    return new QueryFilterNode
                    {
                        Source = source,
                        Filter = new LiteralNode { Value = true, DataType = "boolean" }
                    };
                }

                var filterExpression = ParseExpression();
                Expect(TokenType.RightParen);
                return new QueryFilterNode
                {
                    Source = source,
                    Filter = filterExpression
                };
            }

            if (methodName.Equals("orderby", StringComparison.OrdinalIgnoreCase))
            {
                var orderings = ParseOrderByArguments();
                Expect(TokenType.RightParen);
                return new QueryOrderByNode
                {
                    Source = source,
                    Orderings = orderings
                };
            }

            if (methodName.Equals("take", StringComparison.OrdinalIgnoreCase))
            {
                var selection = ParseTakeArguments();
                Expect(TokenType.RightParen);
                return new QueryTakeNode
                {
                    Source = source,
                    Selection = selection
                };
            }

            if (methodName.Equals("distinct", StringComparison.OrdinalIgnoreCase))
            {
                if (Check(TokenType.RightParen))
                {
                    AddError("Distinct operation requires a key expression.", CurrentToken?.Position ?? new TokenPosition(0, 0, 0));
                    Expect(TokenType.RightParen);
                    return new QueryDistinctNode
                    {
                        Source = source,
                        KeySelector = new LiteralNode { Value = null, DataType = "null" }
                    };
                }

                var keyExpression = ParseExpression();
                Expect(TokenType.RightParen);
                return new QueryDistinctNode
                {
                    Source = source,
                    KeySelector = keyExpression
                };
            }

            if (methodName.Equals("select", StringComparison.OrdinalIgnoreCase))
            {
                if (Check(TokenType.RightParen))
                {
                    AddError("Select operation requires a projection expression.", CurrentToken?.Position ?? new TokenPosition(0, 0, 0));
                    Expect(TokenType.RightParen);
                    return new QuerySelectNode
                    {
                        Source = source,
                        Selector = new LiteralNode { Value = null, DataType = "null" }
                    };
                }

                var selectorExpression = ParseExpression();
                Expect(TokenType.RightParen);
                return new QuerySelectNode
                {
                    Source = source,
                    Selector = selectorExpression
                };
            }


            throw new ParseException($"Unsupported query method: {methodName}");
        }

        private List<OrderingTerm> ParseOrderByArguments()
        {
            var orderings = new List<OrderingTerm>();

            if (Check(TokenType.RightParen))
            {
                AddError("OrderBy operation requires at least one ordering expression.", CurrentToken?.Position ?? new TokenPosition(0, 0, 0));
                return orderings;
            }

            do
            {
                var expression = ParseExpression();
                var direction = OrderingDirection.Ascending;

                if (Match(TokenType.Identifier, "desc"))
                {
                    direction = OrderingDirection.Descending;
                }
                else if (Match(TokenType.Identifier, "asc"))
                {
                    direction = OrderingDirection.Ascending;
                }

                orderings.Add(new OrderingTerm
                {
                    Expression = expression,
                    Direction = direction
                });
            } while (Match(TokenType.Comma));

            return orderings;
        }

        private SelectionTerm ParseTakeArguments()
        {
            if (Check(TokenType.RightParen))
            {
                AddError("Take operation requires a count expression.", CurrentToken?.Position ?? new TokenPosition(0, 0, 0));
                return new SelectionTerm
                {
                    Strategy = SelectionStrategy.First,
                    CountExpression = new LiteralNode { Value = 1, DataType = "number" }
                };
            }

            var strategy = SelectionStrategy.First;
            ExpressionNode? countExpression = null;

            if (Match(TokenType.Identifier, "random"))
            {
                strategy = SelectionStrategy.Random;
            }
            else if (Match(TokenType.Identifier, "last"))
            {
                strategy = SelectionStrategy.Last;
            }
            else if (Match(TokenType.Identifier, "first"))
            {
                strategy = SelectionStrategy.First;
            }
            else if (Match(TokenType.Identifier, "all"))
            {
                strategy = SelectionStrategy.All;
            }

            if (strategy != SelectionStrategy.All)
            {
                countExpression = ParseExpression();
                if (countExpression == null)
                {
                    AddError("Take operation requires a count expression.", CurrentToken?.Position ?? new TokenPosition(0, 0, 0));
                    countExpression = new LiteralNode { Value = 1, DataType = "number" };
                }
            }

            return new SelectionTerm
            {
                Strategy = strategy,
                CountExpression = countExpression
            };
        }

        /// <summary>
        /// 解析基础表达式
        /// </summary>
        private ExpressionNode ParsePrimary()
        {
            if (Match(TokenType.Boolean))
            {
                var token = Previous();
                return new LiteralNode
                {
                    Value = token.ParsedValue,
                    DataType = "boolean"
                };
            }

            if (Match(TokenType.Null))
            {
                return new LiteralNode
                {
                    Value = null,
                    DataType = "null"
                };
            }

            if (Match(TokenType.Number))
            {
                var token = Previous();
                return new LiteralNode
                {
                    Value = token.ParsedValue,
                    DataType = token.DataType?.Name.ToLowerInvariant() ?? "number"
                };
            }

            if (Match(TokenType.String))
            {
                var token = Previous();
                return new LiteralNode
                {
                    Value = token.ParsedValue,
                    DataType = "string"
                };
            }

            if (Match(TokenType.LeftBracket))
            {
                return ParseListLiteralNode();
            }

            if (Match(TokenType.Identifier))
            {
                var token = Previous();
                return new IdentifierNode
                {
                    Name = token.Value,
                    Path = new List<string> { token.Value }
                };
            }

            if (Match(TokenType.LeftParen))
            {
                var expr = ParseExpression();
                Expect(TokenType.RightParen);
                return new ParenthesizedExpressionNode
                {
                    Inner = expr,
                    DataType = expr.DataType
                };
            }

            throw new ParseException($"Unexpected token: {CurrentToken?.Type.ToString() ?? "EOF"}");
        }

        private LiteralNode ParseListLiteralNode()
        {
            var elements = new List<string>();

            if (!Check(TokenType.RightBracket))
            {
                while (true)
                {
                    var elementTokens = new List<Token>();
                    while (!Check(TokenType.Comma) && !Check(TokenType.RightBracket) && !IsAtEnd)
                    {
                        elementTokens.Add(CurrentToken!);
                        Advance();
                    }

                    elements.Add(BuildListLiteralElement(elementTokens));

                    if (Match(TokenType.Comma))
                    {
                        continue;
                    }

                    break;
                }
            }

            Expect(TokenType.RightBracket);

            var allInt = elements.Count > 0 && elements.All(e => int.TryParse(e, out _));
            if (allInt)
            {
                var ints = elements.Select(int.Parse).ToList();
                return new LiteralNode
                {
                    Value = ints,
                    DataType = "list<int32>"
                };
            }

            return new LiteralNode
            {
                Value = elements,
                DataType = "list<string>"
            };
        }

        private static string BuildListLiteralElement(List<Token> tokens)
        {
            if (tokens.Count == 0) return string.Empty;

            var builder = new StringBuilder();
            foreach (var token in tokens)
            {
                if (token.Type == TokenType.String)
                {
                    builder.Append(token.ParsedValue?.ToString() ?? string.Empty);
                }
                else
                {
                    builder.Append(token.Value);
                }
            }

            return builder.ToString().Trim();
        }

        // 辅助方法
        private bool Match(TokenType type, string value)
        {
            if (Check(type) && CurrentToken!.Value.Equals(value, StringComparison.InvariantCultureIgnoreCase))
            {
                Advance();
                return true;
            }
            return false;
        }

        private bool MatchIdentifier(string value)
        {
            return Match(TokenType.Identifier, value);
        }

        private bool CheckIdentifier(string value)
        {
            if (IsAtEnd) return false;
            return CurrentToken!.Type == TokenType.Identifier && CurrentToken.Value.Equals(value, StringComparison.InvariantCultureIgnoreCase);
        }

        private Token ExpectIdentifier(string value)
        {
            if (CheckIdentifier(value))
            {
                return Advance();
            }

            throw new ParseException($"Expected identifier '{value}', but got '{CurrentToken?.Value ?? "EOF"}'");
        }
        private bool Match(params TokenType[] types)
        {
            foreach (var type in types)
            {
                if (Check(type))
                {
                    Advance();
                    return true;
                }
            }
            return false;
        }

        private bool Check(params TokenType[] types)
        {
            if (IsAtEnd) return false;
            return types.Contains(CurrentToken!.Type);
        }

        private Token Advance()
        {
            if (!IsAtEnd) _current++;
            return Previous();
        }

        private Token Previous()
        {
            return _tokens[_current - 1];
        }

        private Token Expect(TokenType type)
        {
            if (Check(type))
            {
                return Advance();
            }

            throw new ParseException($"Expected {type}, but got {CurrentToken?.Type.ToString() ?? "EOF"}");
        }

        private Token ExpectIdentifierWithMessage(string message)
        {
            if (Check(TokenType.Identifier))
            {
                return Advance();
            }

            throw new ParseException(message);
        }

        private SourceLocation ConvertPosition(TokenPosition position)
        {
            return new SourceLocation
            {
                StartLine = position.Line,
                StartColumn = position.Column,
                EndLine = position.Line,
                EndColumn = position.Column + position.Length
            };
        }

        private void AddError(string message, TokenPosition position)
        {
            Errors.Add(new ParseError(message, position));
        }
    }

    /// <summary>
    /// 语法分析异常
    /// </summary>
    public class ParseException : Exception
    {
        public ParseException(string message) : base(message) { }
        public ParseException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// 语法分析错误
    /// </summary>
    public class ParseError
    {
        /// <summary>
        /// 错误消息
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 错误位置
        /// </summary>
        public TokenPosition Position { get; }

        public ParseError(string message, TokenPosition position)
        {
            Message = message;
            Position = position;
        }

        public override string ToString()
        {
            return $"Parse Error at {Position}: {Message}";
        }
    }
}
