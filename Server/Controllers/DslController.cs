using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.AST;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Parser;
using ShadowverseBeyond.Server.Dtos.Dsl;

namespace ShadowverseBeyond.Server.Controllers
{
    [ApiController]
    [Route("api/dsl")]
    public class DslController : ControllerBase
    {
        private readonly ILogger<DslController> _logger;

        public DslController(ILogger<DslController> logger)
        {
            _logger = logger;
        }

        [HttpPost("parse")]
        public ActionResult<DslParseResultDto> Parse([FromBody] DslParseRequest request)
        {
            var expression = request?.Expression ?? string.Empty;
            var stopwatch = Stopwatch.StartNew();

            var lexer = new Lexer(expression);
            var tokens = lexer.Tokenize();
            var parser = new Parser(tokens);
            var ast = parser.ParseSingleExpression();

            var errors = new List<string>();
            errors.AddRange(lexer.Errors.Select(e => e.ToString()));
            errors.AddRange(parser.Errors.Select(e => e.ToString()));

            stopwatch.Stop();

            if (errors.Count > 0 || ast == null)
            {
                _logger.LogWarning("DSL parse failed in {ElapsedMs}ms. Errors={ErrorCount}", stopwatch.ElapsedMilliseconds, errors.Count);
                return Ok(new DslParseResultDto
                {
                    Success = false,
                    Version = 1,
                    Expression = expression,
                    Errors = errors,
                    ElapsedMs = stopwatch.ElapsedMilliseconds
                });
            }

            var root = MapExpressionNode(ast);
            _logger.LogInformation("DSL parse succeeded in {ElapsedMs}ms.", stopwatch.ElapsedMilliseconds);

            return Ok(new DslParseResultDto
            {
                Success = true,
                Version = 1,
                Expression = expression,
                Root = root,
                Errors = new List<string>(),
                ElapsedMs = stopwatch.ElapsedMilliseconds
            });
        }

        private static DslAstNodeDto MapExpressionNode(ExpressionNode node)
        {
            switch (node)
            {
                case LiteralNode literal:
                    return new DslAstNodeDto
                    {
                        NodeType = literal.NodeType,
                        DataType = literal.DataType,
                        Value = literal.Value
                    };
                case IdentifierNode identifier:
                    return new DslAstNodeDto
                    {
                        NodeType = identifier.NodeType,
                        DataType = identifier.DataType,
                        Name = identifier.Name,
                        Path = identifier.Path?.ToList()
                    };
                case BinaryOperationNode binary:
                    return new DslAstNodeDto
                    {
                        NodeType = binary.NodeType,
                        DataType = binary.DataType,
                        Operator = binary.Operator.ToString(),
                        Left = MapExpressionNode(binary.Left),
                        Right = MapExpressionNode(binary.Right)
                    };
                case UnaryOperationNode unary:
                    return new DslAstNodeDto
                    {
                        NodeType = unary.NodeType,
                        DataType = unary.DataType,
                        Operator = unary.Operator.ToString(),
                        IsPrefix = unary.IsPrefix,
                        Operand = MapExpressionNode(unary.Operand)
                    };
                case ParenthesizedExpressionNode parenthesized:
                    return new DslAstNodeDto
                    {
                        NodeType = parenthesized.NodeType,
                        DataType = parenthesized.DataType,
                        Inner = MapExpressionNode(parenthesized.Inner)
                    };
                case FunctionCallNode call:
                    return new DslAstNodeDto
                    {
                        NodeType = call.NodeType,
                        DataType = call.DataType,
                        Name = call.FunctionName,
                        IsMethodCall = call.IsMethodCall,
                        Receiver = call.Receiver != null ? MapExpressionNode(call.Receiver) : null,
                        Arguments = call.Arguments?.Select(MapExpressionNode).ToList()
                    };
                case QueryFilterNode queryFilter:
                    return new DslAstNodeDto
                    {
                        NodeType = "FunctionCall",
                        DataType = queryFilter.DataType,
                        Name = "query",
                        IsMethodCall = true,
                        Receiver = MapExpressionNode(queryFilter.Source),
                        Arguments = new List<DslAstNodeDto> { MapExpressionNode(queryFilter.Filter) }
                    };
                case QueryOrderByNode queryOrderBy:
                    var orderings = queryOrderBy.Orderings?
                        .Select(o => new DslQueryOrderingDto
                        {
                            Expression = MapExpressionNode(o.Expression),
                            Direction = o.Direction == OrderingDirection.Descending ? "desc" : "asc"
                        })
                        .ToList() ?? new List<DslQueryOrderingDto>();
                    var orderingArgs = orderings.Select(o => o.Expression).ToList();
                    return new DslAstNodeDto
                    {
                        NodeType = "FunctionCall",
                        DataType = queryOrderBy.DataType,
                        Name = "orderBy",
                        IsMethodCall = true,
                        Receiver = MapExpressionNode(queryOrderBy.Source),
                        Arguments = orderingArgs,
                        QueryOrderings = orderings
                    };
                case QueryTakeNode queryTake:
                    var takeCount = queryTake.Selection?.CountExpression != null
                        ? MapExpressionNode(queryTake.Selection.CountExpression)
                        : null;
                    var takeArgs = takeCount != null ? new List<DslAstNodeDto> { takeCount } : new List<DslAstNodeDto>();
                    return new DslAstNodeDto
                    {
                        NodeType = "FunctionCall",
                        DataType = queryTake.DataType,
                        Name = "take",
                        IsMethodCall = true,
                        Receiver = MapExpressionNode(queryTake.Source),
                        Arguments = takeArgs,
                        QueryTakeStrategy = queryTake.Selection?.Strategy.ToString().ToLowerInvariant(),
                        QueryTakeCount = takeCount
                    };
                case QueryDistinctNode queryDistinct:
                    var distinctKey = queryDistinct.KeySelector != null
                        ? MapExpressionNode(queryDistinct.KeySelector)
                        : null;
                    var distinctArgs = distinctKey != null ? new List<DslAstNodeDto> { distinctKey } : new List<DslAstNodeDto>();
                    return new DslAstNodeDto
                    {
                        NodeType = "FunctionCall",
                        DataType = queryDistinct.DataType,
                        Name = "distinct",
                        IsMethodCall = true,
                        Receiver = MapExpressionNode(queryDistinct.Source),
                        Arguments = distinctArgs
                    };
                case QuerySelectNode querySelect:
                    var selectExpr = querySelect.Selector != null
                        ? MapExpressionNode(querySelect.Selector)
                        : null;
                    var selectArgs = selectExpr != null ? new List<DslAstNodeDto> { selectExpr } : new List<DslAstNodeDto>();
                    return new DslAstNodeDto
                    {
                        NodeType = "FunctionCall",
                        DataType = querySelect.DataType,
                        Name = "select",
                        IsMethodCall = true,
                        Receiver = MapExpressionNode(querySelect.Source),
                        Arguments = selectArgs
                    };
                case MemberAccessNode memberAccess:
                    return new DslAstNodeDto
                    {
                        NodeType = memberAccess.NodeType,
                        DataType = memberAccess.DataType,
                        Target = MapExpressionNode(memberAccess.Target),
                        Member = MapExpressionNode(memberAccess.Member)
                    };
                case BlockNode block:
                    return new DslAstNodeDto
                    {
                        NodeType = block.NodeType,
                        DataType = block.DataType,
                        Statements = block.Statements?.Select(MapExpressionNode).ToList()
                    };
                case ConditionalExpressionNode conditional:
                    return new DslAstNodeDto
                    {
                        NodeType = conditional.NodeType,
                        DataType = conditional.DataType,
                        Condition = MapExpressionNode(conditional.Condition),
                        TrueExpression = MapExpressionNode(conditional.TrueExpression),
                        FalseExpression = MapExpressionNode(conditional.FalseExpression)
                    };
                case ArrayAccessNode arrayAccess:
                    return new DslAstNodeDto
                    {
                        NodeType = arrayAccess.NodeType,
                        DataType = arrayAccess.DataType,
                        Array = MapExpressionNode(arrayAccess.Array),
                        Index = MapExpressionNode(arrayAccess.Index)
                    };
                default:
                    return new DslAstNodeDto
                    {
                        NodeType = node.NodeType,
                        DataType = node.DataType
                    };
            }
        }

        [HttpPost("generate-from-card")]
        public ActionResult<DslGenerateFromCardResponse> GenerateFromCard([FromBody] DslGenerateFromCardRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { error = "请求体不能为空" });
            }

            var cardName = string.IsNullOrWhiteSpace(request.Name) ? "未命名卡牌" : request.Name.Trim();
            var cardType = string.IsNullOrWhiteSpace(request.CardType) ? "Follower" : request.CardType.Trim();
            var sections = request.Sections?.Where(s => s != null && !string.Equals(s.Style, "none", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(s.Text)).ToList()
                           ?? new List<CardSectionInput>();

            if (sections.Count == 0 && !string.IsNullOrWhiteSpace(request.Description))
            {
                sections.Add(new CardSectionInput { Style = "normal", Text = request.Description });
            }

            if (sections.Count == 0)
            {
                return BadRequest(new { error = "请先在卡面或描述中输入规则文本，再生成能力配置。" });
            }

            var drafts = new List<GeneratedAbilityDraftDto>();
            var detectedKeywords = new List<string>();
            var unrecognizedClauses = new List<string>();
            int seq = 1;

            foreach (var section in sections)
            {
                var style = (section.Style ?? "normal").Trim().ToLowerInvariant() switch
                {
                    "进化" or "進化" or "evolve" => "evolve",
                    "超进化" or "超進化" or "superevolve" => "superevolve",
                    "纹章" or "紋章" or "crest" => "crest",
                    "信仰" or "faith" => "faith",
                    "普通（分割线）" or "divider" => "divider",
                    _ => "normal"
                };
                var defaultTrigger = style switch
                {
                    "evolve" => "Evolve",
                    "superevolve" => "SuperEvolve",
                    _ => "PlayCard"
                };
                var activeSectionTrigger = defaultTrigger;
                string? pendingTriggerOnlyLine = null;
                var rawLines = (section.Text ?? string.Empty)
                    .Replace("\r\n", "\n")
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                foreach (var rawLine in rawLines)
                {
                    if (string.IsNullOrWhiteSpace(rawLine)) continue;
                    // Only leading labels describe the card itself; granted keywords belong to the effect.
                    var clause = Regex.Replace(rawLine, @"^(?:【(?:守护|守護|疾驰|疾馳|突进|突進|必杀|必殺|吸血|潜行|潛行|威慑|威懾|屏障|光纹|光紋)】\s*)+", match =>
                    {
                        foreach (Match label in Regex.Matches(match.Value, @"【([^】]+)】"))
                        {
                            var keyword = label.Groups[1].Value switch
                            {
                                "守护" or "守護" => "Ward",
                                "疾驰" or "疾馳" => "Storm",
                                "突进" or "突進" => "Rush",
                                "必杀" or "必殺" => "Bane",
                                "吸血" => "Drain",
                                "潜行" or "潛行" => "Ambush",
                                "威慑" or "威懾" => "Intimidate",
                                "屏障" => "Barrier",
                                "光纹" or "光紋" => "Aura",
                                _ => throw new InvalidOperationException("未知卡面关键字")
                            };
                            if (!detectedKeywords.Contains(keyword)) detectedKeywords.Add(keyword);
                        }
                        return string.Empty;
                    }).Trim();
                    if (string.IsNullOrWhiteSpace(clause)
                        || Regex.IsMatch(clause, @"^(?:[-—─]{2,}|==)$")
                        || Regex.IsMatch(clause, @"^\{[^{}]+\}$")) continue;

                    var (detectedTrigger, remainder) = DetectTriggerAndRemainder(clause);
                    if (detectedTrigger != null)
                    {
                        if (pendingTriggerOnlyLine != null)
                        {
                            unrecognizedClauses.Add(pendingTriggerOnlyLine);
                        }
                        activeSectionTrigger = detectedTrigger;
                    }
                    if (string.IsNullOrWhiteSpace(remainder))
                    {
                        pendingTriggerOnlyLine = clause;
                        continue;
                    }
                    pendingTriggerOnlyLine = null;

                    var draft = CompileClauseToAbilityDraft(cardName, cardType, style, activeSectionTrigger, clause, seq, unrecognizedClauses);
                    if (draft.HasEffects)
                    {
                        drafts.Add(draft);
                        seq++;
                    }
                }

                if (pendingTriggerOnlyLine != null)
                {
                    unrecognizedClauses.Add(pendingTriggerOnlyLine);
                }
            }

            bool fullyCompiled = unrecognizedClauses.Count == 0
                                 && drafts.All(d => d.DslValid && d.HasEffects)
                                 && (drafts.Count > 0 || detectedKeywords.Count > 0);

            return Ok(new DslGenerateFromCardResponse
            {
                CardName = cardName,
                Abilities = drafts,
                DetectedKeywords = detectedKeywords,
                UnrecognizedClauses = unrecognizedClauses,
                FullyCompiled = fullyCompiled
            });
        }

        private static (string? Trigger, string Remainder) DetectTriggerAndRemainder(string text)
        {
            const string triggerPattern = "超进化时|超進化時|进化时|進化時|入场曲|入場曲|谢幕曲|謝幕曲|攻击时|攻擊時|交战时|交戰時|(?:自己的)?回合(?:开始时|開始時|结束时|結束時)|启动|起动|起動";
            var match = Regex.Match(text, $@"^\s*(?:【(?<trigger>{triggerPattern})】|(?<trigger>{triggerPattern}))\s*[：:]?\s*");
            if (!match.Success) return (null, text);

            var trigger = match.Groups["trigger"].Value switch
            {
                "超进化时" or "超進化時" => "SuperEvolve",
                "进化时" or "進化時" => "Evolve",
                "入场曲" or "入場曲" => "Fanfare",
                "谢幕曲" or "謝幕曲" => "Destroy",
                "攻击时" or "攻擊時" => "AttackDeclared",
                "交战时" or "交戰時" => "Attack",
                "回合开始时" or "回合開始時" or "自己的回合开始时" or "自己的回合開始時" => "TurnStart",
                "回合结束时" or "回合結束時" or "自己的回合结束时" or "自己的回合結束時" => "TurnEnd",
                "启动" or "起动" or "起動" => "Act",
                _ => throw new InvalidOperationException("未知触发器")
            };
            return (trigger, text[match.Length..]);
        }

        private static GeneratedAbilityDraftDto CompileClauseToAbilityDraft(
            string cardName,
            string cardType,
            string sectionStyle,
            string trigger,
            string clauseText,
            int sequence,
            List<string> unrecognizedClauses)
        {
            var (_, remainder) = DetectTriggerAndRemainder(clauseText.Trim());
            var text = NormalizeChineseNumbers(remainder)
                .Replace("從者", "随从").Replace("从者", "随从").Replace("隨從", "随从")
                .Replace("主戰者", "主战者").Replace("傷害", "伤害")
                .Replace("恢復", "回复").Replace("恢复", "回复").Replace("回復", "回复")
                .Replace("卡片", "卡牌").Replace("破壞", "破坏").Replace("召喚", "召唤")
                .Replace("對", "对").Replace("給予", "给予").Replace("敵方", "敌方")
                .Replace("選擇", "选择").Replace("隨機", "随机").Replace("全體", "全体")
                .Replace("獲得", "获得").Replace("爆能強化", "爆能强化")
                .Replace("連擊", "连击").Replace("協作", "协作").Replace("覺醒", "觉醒")
                .Replace("死靈術", "死灵术");
            var eventTypes = new List<string>();
            var validZones = new List<string>();
            var relationType = "Self";
            var abilityKeywords = new List<object>();
            var conditions = new List<object>();
            var targets = new List<object>();
            var effects = new List<object>();
            var notes = new List<string>();
            int? activateCost = null;

            bool isSpell = string.Equals(cardType, "Spell", StringComparison.OrdinalIgnoreCase);
            bool isCrest = string.Equals(cardType, "Crest", StringComparison.OrdinalIgnoreCase)
                           || string.Equals(cardType, "Faith", StringComparison.OrdinalIgnoreCase)
                           || sectionStyle is "crest" or "faith";

            // 1. Trigger & AbilityKeyword resolution
            var enhanceMatch = Regex.Match(text, @"【?爆能强化】?\s*(\d+)");
            if (enhanceMatch.Success && int.TryParse(enhanceMatch.Groups[1].Value, out var enhanceVal))
            {
                activateCost = enhanceVal;
                abilityKeywords.Add(new { keyword = "Enhance", value = enhanceVal });
                eventTypes.Add("PlayCard");
            }

            if (trigger == "SuperEvolve")
            {
                eventTypes.Add("SuperEvolve");
                validZones.Add("Field");
            }
            else if (trigger == "Evolve")
            {
                if (!eventTypes.Contains("Evolve")) eventTypes.Add("Evolve");
                validZones.Add("Field");
                abilityKeywords.Add(new { keyword = "EvolveEffect", value = (int?)null });
            }
            else if (trigger == "Destroy")
            {
                eventTypes.Add("Destroy");
                abilityKeywords.Add(new { keyword = "Lastword", value = (int?)null });
            }
            else if (trigger == "AttackDeclared")
            {
                eventTypes.Add("AttackDeclared");
                validZones.Add("Field");
                abilityKeywords.Add(new { keyword = "AttackEffect", value = (int?)null });
            }
            else if (trigger == "Attack")
            {
                eventTypes.Add("Attack");
                validZones.Add("Field");
                abilityKeywords.Add(new { keyword = "FightEffect", value = (int?)null });
            }
            else if (trigger == "TurnEnd")
            {
                eventTypes.Add("TurnEnd");
                relationType = "Friendly";
                validZones.Add(isCrest ? "Emblem" : "Field");
            }
            else if (trigger == "TurnStart")
            {
                eventTypes.Add("TurnStart");
                relationType = "Friendly";
                validZones.Add(isCrest ? "Emblem" : "Field");
            }
            else if (trigger == "Act")
            {
                eventTypes.Add("Act");
                validZones.Add("Field");
                abilityKeywords.Add(new { keyword = "Act", value = (int?)null });
            }
            else if (trigger is "PlayCard" or "Fanfare")
            {
                if (!eventTypes.Contains("PlayCard")) eventTypes.Add("PlayCard");
                if (!isSpell && trigger == "Fanfare")
                {
                    abilityKeywords.Add(new { keyword = "Fanfare", value = (int?)null });
                }
            }

            // 2. Conditions resolution
            var comboMatch = Regex.Match(text, @"(?:【?连击】?\s*(\d+)|本回合已使用(?:卡牌|牌)数(?:大于等于|>=|至少为)\s*(\d+))");
            if (comboMatch.Success)
            {
                var val = comboMatch.Groups[1].Success ? comboMatch.Groups[1].Value : comboMatch.Groups[2].Value;
                conditions.Add(new { expression = $"context.owner.combo >= {val}", isEnabled = true });
            }

            var rallyMatch = Regex.Match(text, @"【?协作】?\s*(\d+)");
            if (rallyMatch.Success)
            {
                var val = rallyMatch.Groups[1].Value;
                abilityKeywords.Add(new { keyword = "Rally", value = int.Parse(val) });
                conditions.Add(new { expression = $"context.owner.rally >= {val}", isEnabled = true });
            }

            if (Regex.IsMatch(text, @"【?觉醒】?"))
            {
                abilityKeywords.Add(new { keyword = "Awake", value = (int?)null });
                conditions.Add(new { expression = "context.owner.maxPlayPoints >= 7", isEnabled = true });
            }

            // 3. Effects & Targets resolution (左起顺序词法扫描，支持句内逗号串联、同类效果多次出现与未知片段精确拦截)
            int order = 0;
            int realActionCount = 0;
            var clauseUnrecognized = new List<string>();
            var sentences = text
                .Split(new[] { '。', '；', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var rawSentence in sentences)
            {
                var s = rawSentence.Trim();
                if (string.IsNullOrWhiteSpace(s)
                    || Regex.IsMatch(s, @"^(?:[-—─]{2,}|==)$")
                    || Regex.IsMatch(s, @"^\{[^{}]+\}$")) continue;

                // 剥离句首条件/费用标签（已在第 1、2 步提取）
                var afterCond = Regex.Replace(
                    s,
                    @"^(?:\s*(?:【?(?:爆能强化|连击|协作)】?\s*\d+|【?觉醒】?|消费\s*\d+)\s*[：:，,]?\s*)+",
                    string.Empty
                ).Trim();

                if (string.IsNullOrWhiteSpace(afterCond))
                {
                    continue;
                }

                // 提取可选的句首【死灵术】N 前缀（仅当后续解析出有效实际动作时才落入消耗效果）
                int? pendingNecroCost = null;
                var actionBody = afterCond;
                var necroMatch = Regex.Match(afterCond, @"^【?死灵术】?\s*(\d+)\s*[：:，,]?\s*");
                if (necroMatch.Success && int.TryParse(necroMatch.Groups[1].Value, out var necroCost))
                {
                    pendingNecroCost = necroCost;
                    actionBody = afterCond[necroMatch.Length..].Trim();
                }

                if (string.IsNullOrWhiteSpace(actionBody))
                {
                    clauseUnrecognized.Add(s);
                    unrecognizedClauses.Add(s);
                    continue;
                }

                var sentenceActionBuilders = new List<Action>();
                int pos = 0;
                while (pos < actionBody.Length)
                {
                    // 跳过分隔标点与连接词（如“，”、“，并”、“随后”、“然后”）
                    var sepMatch = Regex.Match(actionBody[pos..], @"^[，,、：:\s]*");
                    pos += sepMatch.Length;
                    if (pos >= actionBody.Length) break;

                    var conjMatch = Regex.Match(actionBody[pos..], @"^(?:并(?:且)?|随后|然后|同时|再)(?=[，,、：:\s]|随机|对|给予|选择|自己|抽|回复|使|令|让|破坏|获得|最大|增加|进行|魔力|\d)[，,、：:\s]*");
                    if (conjMatch.Success)
                    {
                        pos += conjMatch.Length;
                    }
                    if (pos >= actionBody.Length) break;

                    var slice = actionBody[pos..];
                    if (TryConsumeLeadingAction(slice, out var consumedLen, out var appendAction))
                    {
                        sentenceActionBuilders.Add(appendAction);
                        pos += consumedLen;
                    }
                    else
                    {
                        int nextDelim = slice.IndexOfAny(new[] { '，', ',', '、' });
                        var unknownSeg = (nextDelim >= 0 ? slice[..nextDelim] : slice).Trim();
                        if (!string.IsNullOrWhiteSpace(unknownSeg))
                        {
                            clauseUnrecognized.Add(unknownSeg);
                            unrecognizedClauses.Add(unknownSeg);
                        }
                        pos += nextDelim >= 0 ? nextDelim + 1 : slice.Length;
                    }
                }

                if (sentenceActionBuilders.Count == 0)
                {
                    if (clauseUnrecognized.Count == 0)
                    {
                        clauseUnrecognized.Add(s);
                        unrecognizedClauses.Add(s);
                    }
                    continue;
                }

                if (pendingNecroCost.HasValue)
                {
                    var costVal = pendingNecroCost.Value;
                    var necroKw = new { keyword = "Necromance", value = costVal };
                    if (!abilityKeywords.Any(k => JsonSerializer.Serialize(k) == JsonSerializer.Serialize(necroKw)))
                    {
                        abilityKeywords.Add(necroKw);
                    }
                    effects.Add(new
                    {
                        type = "Necromancy",
                        executionOrder = order++,
                        isEnabled = true,
                        parameters = new Dictionary<string, object>
                        {
                            ["Amount"] = StaticVal(costVal)
                        },
                        targetSelector = Array.Empty<string>()
                    });
                }

                foreach (var buildAction in sentenceActionBuilders)
                {
                    buildAction();
                    realActionCount++;
                }
            }

            bool TryConsumeLeadingAction(string slice, out int consumedLength, out Action appendEffect)
            {
                consumedLength = 0;
                appendEffect = static () => { };

                // 3a. Draw cards
                var drawMatch = Regex.Match(slice, @"^(?:自己)?抽取?\s*(\d+)\s*(?:张(?:卡牌|牌)?|卡牌|牌)");
                if (drawMatch.Success && int.TryParse(drawMatch.Groups[1].Value, out var drawCount))
                {
                    consumedLength = drawMatch.Length;
                    appendEffect = () => effects.Add(new
                    {
                        type = "Draw",
                        executionOrder = order++,
                        isEnabled = true,
                        parameters = new Dictionary<string, object>
                        {
                            ["PlayerScope"] = StaticVal("Self"),
                            ["UseSearchMode"] = StaticVal(false),
                            ["DrawAmount"] = StaticVal(drawCount)
                        },
                        targetSelector = Array.Empty<string>()
                    });
                    return true;
                }

                // 3b. Heal leader
                var healLeaderMatch = Regex.Match(slice, @"^(?:回复(?:自己的)?主战者\s*(\d+)\s*点(?:生命(?:值)?)?|(?:使|令)?(?:自己的)?主战者回复\s*(\d+)\s*点(?:生命(?:值)?)?)");
                if (healLeaderMatch.Success)
                {
                    var amtStr = healLeaderMatch.Groups[1].Success ? healLeaderMatch.Groups[1].Value : healLeaderMatch.Groups[2].Value;
                    if (int.TryParse(amtStr, out var healAmt))
                    {
                        consumedLength = healLeaderMatch.Length;
                        appendEffect = () =>
                        {
                            var targetKey = AddTarget(targets, BuildTarget("context.owner.leader", requireChoice: false));
                            effects.Add(new
                            {
                                type = "Heal",
                                executionOrder = order++,
                                isEnabled = true,
                                parameters = new Dictionary<string, object>
                                {
                                    ["HealAmount"] = StaticVal(healAmt)
                                },
                                targetSelector = new[] { targetKey }
                            });
                        };
                        return true;
                    }
                }

                // 3c. Damage to enemy leader
                var dmgLeaderMatch = Regex.Match(slice, @"^(?:对|给予)(?:敌方|对手的?)主战者(?:造成)?\s*(\d+)\s*点伤害");
                if (dmgLeaderMatch.Success && int.TryParse(dmgLeaderMatch.Groups[1].Value, out var dmgLeaderAmt))
                {
                    consumedLength = dmgLeaderMatch.Length;
                    appendEffect = () =>
                    {
                        var targetKey = AddTarget(targets, BuildTarget("context.opponent.leader", requireChoice: false));
                        effects.Add(new
                        {
                            type = "Damage",
                            executionOrder = order++,
                            isEnabled = true,
                            parameters = new Dictionary<string, object>
                            {
                                ["DamageAmount"] = StaticVal(dmgLeaderAmt),
                                ["DistributionMode"] = StaticVal("PerTarget")
                            },
                            targetSelector = new[] { targetKey }
                        });
                    };
                    return true;
                }

                // 3d. AoE damage to enemy followers
                var dmgAoeMatch = Regex.Match(slice, @"^(?:(?:对|给予)(?:所有|全体)敌方随从(?:造成)?\s*(\d+)\s*点伤害|(?:对|给予)敌方(?:所有|全体)随从(?:造成)?\s*(\d+)\s*点伤害)");
                if (dmgAoeMatch.Success)
                {
                    var amtStr = dmgAoeMatch.Groups[1].Success ? dmgAoeMatch.Groups[1].Value : dmgAoeMatch.Groups[2].Value;
                    if (int.TryParse(amtStr, out var dmgAoeAmt))
                    {
                        consumedLength = dmgAoeMatch.Length;
                        appendEffect = () =>
                        {
                            var targetKey = AddTarget(targets, BuildTarget("context.opponent.field.query(self.type == \"Follower\")", requireChoice: false));
                            effects.Add(new
                            {
                                type = "Damage",
                                executionOrder = order++,
                                isEnabled = true,
                                parameters = new Dictionary<string, object>
                                {
                                    ["DamageAmount"] = StaticVal(dmgAoeAmt),
                                    ["DistributionMode"] = StaticVal("PerTarget")
                                },
                                targetSelector = new[] { targetKey }
                            });
                        };
                        return true;
                    }
                }

                // 3e. Single-target damage to enemy follower (choice or random)
                var dmgSingleMatch = Regex.Match(slice, @"^(?<rnd>随机)?(?:(?:对|给予)\s*1\s*个(?<rnd2>随机)?敌方随从(?:造成)?|选择\s*1\s*个敌方随从(?:[，,]\s*(?:对其)?)?造成)\s*(\d+)\s*点伤害");
                if (dmgSingleMatch.Success && int.TryParse(dmgSingleMatch.Groups[1].Value, out var dmgSingleAmt))
                {
                    bool isRandom = dmgSingleMatch.Groups["rnd"].Success || dmgSingleMatch.Groups["rnd2"].Success;
                    consumedLength = dmgSingleMatch.Length;
                    appendEffect = () =>
                    {
                        var targetKey = AddTarget(targets, BuildTarget("context.opponent.field.query(self.type == \"Follower\")", requireChoice: !isRandom, randomCount: isRandom ? 1 : null));
                        effects.Add(new
                        {
                            type = "Damage",
                            executionOrder = order++,
                            isEnabled = true,
                            parameters = new Dictionary<string, object>
                            {
                                ["DamageAmount"] = StaticVal(dmgSingleAmt),
                                ["DistributionMode"] = StaticVal("PerTarget")
                            },
                            targetSelector = new[] { targetKey }
                        });
                    };
                    return true;
                }

                // 3f. Destroy enemy follower(s)
                var destroyAoeMatch = Regex.Match(slice, @"^(?:破坏(?:所有|全体)敌方随从|破坏敌方(?:所有|全体)随从)");
                if (destroyAoeMatch.Success)
                {
                    consumedLength = destroyAoeMatch.Length;
                    appendEffect = () =>
                    {
                        var targetKey = AddTarget(targets, BuildTarget("context.opponent.field.query(self.type == \"Follower\")", requireChoice: false));
                        effects.Add(new
                        {
                            type = "Destroy",
                            executionOrder = order++,
                            isEnabled = true,
                            parameters = new Dictionary<string, object>(),
                            targetSelector = new[] { targetKey }
                        });
                    };
                    return true;
                }

                var destroySingleMatch = Regex.Match(slice, @"^(?:(?<rnd>随机)?破坏\s*1\s*个(?<rnd2>随机)?敌方随从|选择\s*1\s*个敌方随从(?:[，,]\s*(?:将其|把其|使其|使它)?)?破坏(?:该随从|它)?)");
                if (destroySingleMatch.Success)
                {
                    bool isRandom = destroySingleMatch.Groups["rnd"].Success || destroySingleMatch.Groups["rnd2"].Success;
                    consumedLength = destroySingleMatch.Length;
                    appendEffect = () =>
                    {
                        var targetKey = AddTarget(targets, BuildTarget("context.opponent.field.query(self.type == \"Follower\")", requireChoice: !isRandom, randomCount: isRandom ? 1 : null));
                        effects.Add(new
                        {
                            type = "Destroy",
                            executionOrder = order++,
                            isEnabled = true,
                            parameters = new Dictionary<string, object>(),
                            targetSelector = new[] { targetKey }
                        });
                    };
                    return true;
                }

                // 3g. Stat Buff (+X/+Y)
                var buffAllMatch = Regex.Match(slice, @"^(?:使|令|让)?(?:自己的(?:所有|全体)随从|随从全体)获得\s*([+-]?\d+)\s*/\s*([+-]?\d+)(?:效果)?");
                if (buffAllMatch.Success
                    && int.TryParse(buffAllMatch.Groups[1].Value, out var allAtk)
                    && int.TryParse(buffAllMatch.Groups[2].Value, out var allHp))
                {
                    consumedLength = buffAllMatch.Length;
                    appendEffect = () =>
                    {
                        var targetKey = AddTarget(targets, BuildTarget("context.owner.field.query(self.type == \"Follower\")", requireChoice: false));
                        effects.Add(new
                        {
                            type = "ModifyStatus",
                            executionOrder = order++,
                            isEnabled = true,
                            parameters = new Dictionary<string, object>
                            {
                                ["DurationType"] = StaticVal("Permanent"),
                                ["DurationParameter"] = StaticVal(0),
                                ["AttackModifier"] = StaticVal(allAtk),
                                ["HealthModifier"] = StaticVal(allHp),
                                ["ModifierLayer"] = StaticVal("Additive")
                            },
                            targetSelector = new[] { targetKey }
                        });
                    };
                    return true;
                }

                var buffSingleMatch = Regex.Match(slice, @"^(?<rnd>随机)?(?:(?:使|令|让)\s*|选择\s*)?1\s*个(?<rnd2>随机)?自己的(?<other>其他)?随从(?:[，,]\s*(?:使其|使它)?)?获得\s*([+-]?\d+)\s*/\s*([+-]?\d+)(?:效果)?");
                if (buffSingleMatch.Success
                    && int.TryParse(buffSingleMatch.Groups[1].Value, out var singleAtk)
                    && int.TryParse(buffSingleMatch.Groups[2].Value, out var singleHp))
                {
                    bool isOther = buffSingleMatch.Groups["other"].Success;
                    bool isRandom = buffSingleMatch.Groups["rnd"].Success || buffSingleMatch.Groups["rnd2"].Success;
                    string poolExpr = isOther
                        ? "context.owner.field.query(self.type == \"Follower\" && self != context.sourceCard)"
                        : "context.owner.field.query(self.type == \"Follower\")";
                    consumedLength = buffSingleMatch.Length;
                    appendEffect = () =>
                    {
                        var targetKey = AddTarget(targets, BuildTarget(poolExpr, requireChoice: !isRandom, randomCount: isRandom ? 1 : null));
                        effects.Add(new
                        {
                            type = "ModifyStatus",
                            executionOrder = order++,
                            isEnabled = true,
                            parameters = new Dictionary<string, object>
                            {
                                ["DurationType"] = StaticVal("Permanent"),
                                ["DurationParameter"] = StaticVal(0),
                                ["AttackModifier"] = StaticVal(singleAtk),
                                ["HealthModifier"] = StaticVal(singleHp),
                                ["ModifierLayer"] = StaticVal("Additive")
                            },
                            targetSelector = new[] { targetKey }
                        });
                    };
                    return true;
                }

                var buffSelfMatch = Regex.Match(slice, @"^(?:(?:使|令|让)?(?:这个随从|该随从|本随从|自身))?获得\s*([+-]?\d+)\s*/\s*([+-]?\d+)(?:效果)?");
                if (buffSelfMatch.Success
                    && int.TryParse(buffSelfMatch.Groups[1].Value, out var selfAtk)
                    && int.TryParse(buffSelfMatch.Groups[2].Value, out var selfHp))
                {
                    consumedLength = buffSelfMatch.Length;
                    appendEffect = () =>
                    {
                        var targetKey = AddTarget(targets, BuildTarget("context.sourceCard", requireChoice: false));
                        effects.Add(new
                        {
                            type = "ModifyStatus",
                            executionOrder = order++,
                            isEnabled = true,
                            parameters = new Dictionary<string, object>
                            {
                                ["DurationType"] = StaticVal("Permanent"),
                                ["DurationParameter"] = StaticVal(0),
                                ["AttackModifier"] = StaticVal(selfAtk),
                                ["HealthModifier"] = StaticVal(selfHp),
                                ["ModifierLayer"] = StaticVal("Additive")
                            },
                            targetSelector = new[] { targetKey }
                        });
                    };
                    return true;
                }

                // 3h. Ramp (ModifyPPMax -> ModifyAmount)
                var rampMatch = Regex.Match(slice, @"^(?:(?:使自己的)?最大\s*PP\s*\+\s*(\d+)|增加\s*(\d+)\s*点最大\s*PP|获得\s*(\d+)\s*个空的\s*PP)");
                if (rampMatch.Success)
                {
                    var rampStr = rampMatch.Groups[1].Success ? rampMatch.Groups[1].Value
                        : (rampMatch.Groups[2].Success ? rampMatch.Groups[2].Value : rampMatch.Groups[3].Value);
                    if (int.TryParse(rampStr, out var rampAmt))
                    {
                        consumedLength = rampMatch.Length;
                        appendEffect = () => effects.Add(new
                        {
                            type = "Ramp",
                            executionOrder = order++,
                            isEnabled = true,
                            parameters = new Dictionary<string, object>
                            {
                                ["PlayerScope"] = StaticVal("Self"),
                                ["ModifyAmount"] = StaticVal(rampAmt)
                            },
                            targetSelector = Array.Empty<string>()
                        });
                        return true;
                    }
                }

                // 3i. Spellboost hand
                var sbMatch = Regex.Match(slice, @"^(?:(?:使自己的手牌)?(?:进行\s*)?魔力增幅\s*(\d+)\s*次|(?:对自己的手牌进行\s*)?(\d+)\s*次魔力增幅)");
                if (sbMatch.Success)
                {
                    var sbStr = sbMatch.Groups[1].Success ? sbMatch.Groups[1].Value : sbMatch.Groups[2].Value;
                    if (int.TryParse(sbStr, out var sbAmt) && sbAmt > 0)
                    {
                        consumedLength = sbMatch.Length;
                        appendEffect = () =>
                        {
                            var targetKey = AddTarget(targets, BuildTarget("context.owner.hand", requireChoice: false));
                            effects.Add(new
                            {
                                type = "SpellBoost",
                                executionOrder = order++,
                                repeatCount = Math.Max(0, sbAmt - 1),
                                isEnabled = true,
                                parameters = new Dictionary<string, object>(),
                                targetSelector = new[] { targetKey }
                            });
                        };
                        return true;
                    }
                }

                return false;
            }

            if (realActionCount == 0 && clauseUnrecognized.Count == 0)
            {
                clauseUnrecognized.Add(clauseText.Trim());
                unrecognizedClauses.Add(clauseText.Trim());
            }

            bool hasEffects = realActionCount > 0;
            bool clauseComplete = hasEffects && clauseUnrecognized.Count == 0;
            if (!hasEffects)
            {
                notes.Add("存在暂未支持自动编译的复杂规则描述，需在能力编辑器中手动配置。");
            }
            else if (!clauseComplete)
            {
                notes.Add($"已部分识别 {effects.Count} 个效果节点，存在未识别片段（已保存为未启用草稿，请前往能力编辑器补全）。");
            }
            else
            {
                notes.Add($"已自动识别 {effects.Count} 个效果节点与 {targets.Count} 个目标池。");
            }

            // Validate all generated DSL expressions via Lexer + Parser
            bool dslValid = true;
            foreach (var expr in targets.Select(ExtractSourcePoolExpression).Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                if (!ValidateSingleDslExpression(expr!))
                {
                    dslValid = false;
                    notes.Add($"目标表达式校验告警: {expr}");
                }
            }

            var triggerLabel = sectionStyle switch
            {
                "crest" => "纹章效果",
                "faith" => "信仰效果",
                _ => eventTypes.FirstOrDefault() switch
                {
                    "Destroy" => "谢幕曲",
                    "Evolve" => "进化时",
                    "SuperEvolve" => "超进化时",
                    "TurnEnd" => "回合结束",
                    "TurnStart" => "回合开始",
                    "AttackDeclared" => "攻击时",
                    "Attack" => "交战时",
                    "Act" => "起动",
                    _ => isSpell ? "法术效果" : "入场曲"
                }
            };

            var abilityName = $"{cardName}·{triggerLabel}{sequence}";

            var jsonConfigObj = new
            {
                name = abilityName,
                description = clauseText.Trim(),
                generatedBy = "mse-compiler",
                abilityKeywords,
                activateCost,
                type = "Triggered",
                trigger = new
                {
                    eventTypes,
                    relationType,
                    timing = "On",
                    validZones,
                    revalidateZoneBeforeExecution = false,
                    isEnabled = true
                },
                conditions,
                targets,
                effects,
                auras = Array.Empty<object>(),
                counterRegistrations = Array.Empty<object>(),
                playerCounterRegistrations = Array.Empty<object>(),
                broadcastOnTrigger = true,
                buffDisplay = true,
                isActive = clauseComplete,
                version = "1.0"
            };

            return new GeneratedAbilityDraftDto
            {
                Name = abilityName,
                Description = clauseText.Trim(),
                AbilityType = 1,
                Priority = 0,
                IsActive = clauseComplete,
                HasEffects = hasEffects,
                DslValid = dslValid,
                Notes = string.Join(" ", notes),
                JsonConfig = JsonSerializer.Serialize(jsonConfigObj)
            };
        }

        private static string NormalizeChineseNumbers(string input)
        {
            return input
                .Replace("張", "张").Replace("個", "个").Replace("點", "点").Replace("兩", "两")
                .Replace("一张", "1张")
                .Replace("两张", "2张")
                .Replace("三张", "3张")
                .Replace("一个", "1个")
                .Replace("两个", "2个")
                .Replace("三个", "3个")
                .Replace("一点", "1点")
                .Replace("两点", "2点")
                .Replace("三点", "3点")
                .Replace("四点", "4点")
                .Replace("五点", "5点");
        }

        private static object StaticVal(object val) => new
        {
            value = val
        };

        private static string AddTarget(List<object> targets, object targetObj)
        {
            targets.Add(targetObj);
            return $"target{targets.Count}";
        }

        private static object BuildTarget(string sourcePoolExpression, bool requireChoice, int? randomCount = null)
        {
            var finalExpr = (randomCount.HasValue && randomCount.Value > 0)
                ? $"{sourcePoolExpression}.take(random {randomCount.Value})"
                : sourcePoolExpression;

            return new
            {
                isEnabled = true,
                requireValidTarget = false,
                targetHandlingStrategy = 0,
                query = new
                {
                    sourcePoolExpression = finalExpr,
                    operations = Array.Empty<object>()
                },
                playerChoiceOptions = requireChoice
                    ? new
                    {
                        isRequired = true,
                        canCancel = true,
                        minCount = 1,
                        maxCount = 1
                    }
                    : null
            };
        }

        private static string? ExtractSourcePoolExpression(object targetObj)
        {
            try
            {
                var json = JsonSerializer.SerializeToElement(targetObj);
                if (json.TryGetProperty("query", out var q) && q.TryGetProperty("sourcePoolExpression", out var expr))
                {
                    return expr.GetString();
                }
            }
            catch
            {
                // ignore
            }
            return null;
        }

        private static bool ValidateSingleDslExpression(string expression)
        {
            var lexer = new Lexer(expression);
            var tokens = lexer.Tokenize();
            var parser = new Parser(tokens);
            var ast = parser.ParseSingleExpression();
            return ast != null && lexer.Errors.Count == 0 && parser.Errors.Count == 0;
        }

        public class DslGenerateFromCardRequest
        {
            public int? CardId { get; set; }
            public string? Name { get; set; }
            public string? CardType { get; set; }
            public string? CardClass { get; set; }
            public int? Cost { get; set; }
            public string? Description { get; set; }
            public List<CardSectionInput>? Sections { get; set; }
        }

        public class CardSectionInput
        {
            public string? Style { get; set; }
            public string? Text { get; set; }
        }

        public class DslGenerateFromCardResponse
        {
            public string CardName { get; set; } = string.Empty;
            public List<GeneratedAbilityDraftDto> Abilities { get; set; } = new();
            public List<string> DetectedKeywords { get; set; } = new();
            public List<string> UnrecognizedClauses { get; set; } = new();
            public bool FullyCompiled { get; set; }
        }

        public class GeneratedAbilityDraftDto
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public int AbilityType { get; set; } = 1;
            public int Priority { get; set; } = 0;
            public bool IsActive { get; set; } = true;
            public bool HasEffects { get; set; } = true;
            public bool DslValid { get; set; } = true;
            public string Notes { get; set; } = string.Empty;
            public string JsonConfig { get; set; } = string.Empty;
        }
    }
}
