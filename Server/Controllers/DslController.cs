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
                    if (string.IsNullOrWhiteSpace(clause)) continue;

                    var (detectedTrigger, remainder) = DetectTriggerAndRemainder(clause);
                    if (detectedTrigger != null) activeSectionTrigger = detectedTrigger;
                    if (string.IsNullOrWhiteSpace(remainder)) continue;

                    var draft = CompileClauseToAbilityDraft(cardName, cardType, style, activeSectionTrigger, clause, seq++);
                    drafts.Add(draft);
                }
            }

            return Ok(new DslGenerateFromCardResponse
            {
                CardName = cardName,
                Abilities = drafts,
                DetectedKeywords = detectedKeywords
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
            int sequence)
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
            var targets = new Dictionary<string, object>();
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
                conditions.Add(new { expression = $"SourcePlayer.Combo >= {val}", isEnabled = true });
            }

            var rallyMatch = Regex.Match(text, @"【?协作】?\s*(\d+)");
            if (rallyMatch.Success)
            {
                var val = rallyMatch.Groups[1].Value;
                abilityKeywords.Add(new { keyword = "Rally", value = int.Parse(val) });
                conditions.Add(new { expression = $"SourcePlayer.Rally >= {val}", isEnabled = true });
            }

            if (Regex.IsMatch(text, @"【?觉醒】?"))
            {
                abilityKeywords.Add(new { keyword = "Awake", value = (int?)null });
                conditions.Add(new { expression = "SourcePlayer.MaxPlayPoints >= 7", isEnabled = true });
            }

            // 3. Effects & Targets resolution
            int order = 0;

            // 3a. Necromancy cost
            var necroMatch = Regex.Match(text, @"【?死灵术】?\s*(\d+)");
            if (necroMatch.Success && int.TryParse(necroMatch.Groups[1].Value, out var necroCost))
            {
                abilityKeywords.Add(new { keyword = "Necromance", value = necroCost });
                effects.Add(new
                {
                    type = "Necromancy",
                    executionOrder = order++,
                    isEnabled = true,
                    parameters = new Dictionary<string, object>
                    {
                        ["Amount"] = StaticVal(necroCost)
                    },
                    targetSelector = Array.Empty<string>()
                });
            }

            // 3b. Draw cards
            var drawMatch = Regex.Match(text, @"抽取?\s*(\d+)\s*(?:张(?:卡牌|牌)?|卡牌|牌)");
            if (drawMatch.Success && int.TryParse(drawMatch.Groups[1].Value, out var drawCount))
            {
                effects.Add(new
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
            }

            // 3c. Heal leader
            var healLeaderMatch = Regex.Match(text, @"回复(?:自己的)?主战者\s*(\d+)\s*点|(?:使)?(?:自己的)?主战者回复\s*(\d+)\s*点");
            if (healLeaderMatch.Success)
            {
                var amtStr = healLeaderMatch.Groups[1].Success ? healLeaderMatch.Groups[1].Value : healLeaderMatch.Groups[2].Value;
                if (int.TryParse(amtStr, out var healAmt))
                {
                    const string targetKey = "selfLeader";
                    targets[targetKey] = BuildTarget("[SourcePlayer.Leader]", requireChoice: false);
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
                }
            }

            // 3d. Damage to enemy leader
            var dmgLeaderMatch = Regex.Match(text, @"(?:对|给予)(?:敌方|对手的?)主战者(?:造成)?\s*(\d+)\s*点伤害");
            if (dmgLeaderMatch.Success && int.TryParse(dmgLeaderMatch.Groups[1].Value, out var dmgLeaderAmt))
            {
                const string targetKey = "enemyLeader";
                targets[targetKey] = BuildTarget("[OpponentPlayer.Leader]", requireChoice: false);
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
            }

            // 3e. AoE damage to enemy followers
            var dmgAoeMatch = Regex.Match(text, @"(?:对|给予)(?:所有|全体)敌方随从(?:造成)?\s*(\d+)\s*点伤害|(?:对|给予)敌方(?:所有|全体)随从(?:造成)?\s*(\d+)\s*点伤害");
            if (dmgAoeMatch.Success)
            {
                var amtStr = dmgAoeMatch.Groups[1].Success ? dmgAoeMatch.Groups[1].Value : dmgAoeMatch.Groups[2].Value;
                if (int.TryParse(amtStr, out var dmgAoeAmt))
                {
                    const string targetKey = "enemyFollowersAoe";
                    targets[targetKey] = BuildTarget("OpponentPlayer.Field", requireChoice: false);
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
                }
            }
            else
            {
                // Single target enemy follower damage
                var dmgSingleMatch = Regex.Match(text, @"(?:对|给予|选择)\s*1\s*个敌方随从(?:造成|，对其造成)?\s*(\d+)\s*点伤害");
                if (dmgSingleMatch.Success && int.TryParse(dmgSingleMatch.Groups[1].Value, out var dmgSingleAmt))
                {
                    bool isRandom = text.Contains("随机");
                    const string targetKey = "enemyFollowerTarget";
                    targets[targetKey] = BuildTarget("OpponentPlayer.Field", requireChoice: !isRandom, randomCount: isRandom ? 1 : null);
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
                }
            }

            // 3f. Destroy enemy follower(s)
            if (Regex.IsMatch(text, @"破坏(?:所有|全体)敌方随从|破坏敌方(?:所有|全体)随从"))
            {
                const string targetKey = "destroyAllEnemies";
                targets[targetKey] = BuildTarget("OpponentPlayer.Field", requireChoice: false);
                effects.Add(new
                {
                    type = "Destroy",
                    executionOrder = order++,
                    isEnabled = true,
                    parameters = new Dictionary<string, object>(),
                    targetSelector = new[] { targetKey }
                });
            }
            else if (Regex.IsMatch(text, @"破坏\s*1\s*个敌方随从|选择\s*1\s*个敌方随从.*破坏"))
            {
                bool isRandom = text.Contains("随机");
                const string targetKey = "destroyEnemyTarget";
                targets[targetKey] = BuildTarget("OpponentPlayer.Field", requireChoice: !isRandom, randomCount: isRandom ? 1 : null);
                effects.Add(new
                {
                    type = "Destroy",
                    executionOrder = order++,
                    isEnabled = true,
                    parameters = new Dictionary<string, object>(),
                    targetSelector = new[] { targetKey }
                });
            }

            // 3g. Stat Buff (+X/+Y)
            var buffMatch = Regex.Match(text, @"获得\s*([+-]?\d+)\s*/\s*([+-]?\d+)");
            if (buffMatch.Success
                && int.TryParse(buffMatch.Groups[1].Value, out var atkMod)
                && int.TryParse(buffMatch.Groups[2].Value, out var hpMod))
            {
                bool allFriendly = Regex.IsMatch(text, @"自己的(?:所有|全体)随从");
                bool chooseFriendly = !allFriendly && Regex.IsMatch(text, @"1\s*个自己的(?:其他)?随从");
                string targetKey = allFriendly ? "friendlyFollowers" : (chooseFriendly ? "chosenFriendlyFollower" : "selfCard");
                string poolExpr = (allFriendly || chooseFriendly) ? "SourcePlayer.Field" : "[SourceCard]";
                targets[targetKey] = BuildTarget(poolExpr, requireChoice: chooseFriendly);
                effects.Add(new
                {
                    type = "ModifyStatus",
                    executionOrder = order++,
                    isEnabled = true,
                    parameters = new Dictionary<string, object>
                    {
                        ["DurationType"] = StaticVal("Permanent"),
                        ["DurationParameter"] = StaticVal(0),
                        ["AttackModifier"] = StaticVal(atkMod),
                        ["HealthModifier"] = StaticVal(hpMod),
                        ["ModifierLayer"] = StaticVal("Additive")
                    },
                    targetSelector = new[] { targetKey }
                });
            }

            // 3h. Ramp (MaxPP + X)
            var rampMatch = Regex.Match(text, @"最大\s*PP\s*\+\s*(\d+)|增加\s*(\d+)\s*点最大\s*PP|获得\s*(\d+)\s*个空的\s*PP");
            if (rampMatch.Success)
            {
                var rampStr = rampMatch.Groups[1].Success ? rampMatch.Groups[1].Value
                    : (rampMatch.Groups[2].Success ? rampMatch.Groups[2].Value : rampMatch.Groups[3].Value);
                if (int.TryParse(rampStr, out var rampAmt))
                {
                    effects.Add(new
                    {
                        type = "Ramp",
                        executionOrder = order++,
                        isEnabled = true,
                        parameters = new Dictionary<string, object>
                        {
                            ["PlayerScope"] = StaticVal("Self"),
                            ["Amount"] = StaticVal(rampAmt)
                        },
                        targetSelector = Array.Empty<string>()
                    });
                }
            }

            // 3i. Spellboost hand
            var sbMatch = Regex.Match(text, @"魔力增幅\s*(\d+)\s*次");
            if (sbMatch.Success && int.TryParse(sbMatch.Groups[1].Value, out var sbAmt))
            {
                const string targetKey = "selfHand";
                targets[targetKey] = BuildTarget("SourcePlayer.Hand", requireChoice: false);
                effects.Add(new
                {
                    type = "SpellBoost",
                    executionOrder = order++,
                    isEnabled = true,
                    parameters = new Dictionary<string, object>
                    {
                        ["Amount"] = StaticVal(sbAmt)
                    },
                    targetSelector = new[] { targetKey }
                });
            }

            if (effects.Count == 0)
            {
                notes.Add("已生成触发时机骨架，具体复合效果可在向导第 5 步微调。");
            }
            else
            {
                notes.Add($"已自动识别 {effects.Count} 个效果节点与 {targets.Count} 个目标池。");
            }

            // Validate all generated DSL expressions via Lexer + Parser
            bool dslValid = true;
            foreach (var expr in targets.Values.Select(ExtractSourcePoolExpression).Where(s => !string.IsNullOrWhiteSpace(s)))
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
                isActive = true,
                version = "1.0"
            };

            return new GeneratedAbilityDraftDto
            {
                Name = abilityName,
                Description = clauseText.Trim(),
                AbilityType = 1,
                Priority = 0,
                IsActive = true,
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
            source = "Static",
            value = val
        };

        private static object BuildTarget(string sourcePoolExpression, bool requireChoice, int? randomCount = null)
        {
            var operations = new List<object>();
            if (randomCount.HasValue && randomCount.Value > 0)
            {
                operations.Add(new
                {
                    type = "Take",
                    strategy = "Random",
                    count = StaticVal(randomCount.Value)
                });
            }

            return new
            {
                isEnabled = true,
                requireValidTarget = false,
                targetHandlingStrategy = "DoNotIntercept",
                query = new
                {
                    sourcePoolExpression,
                    operations
                },
                playerChoiceOptions = requireChoice
                    ? new
                    {
                        isRequired = true,
                        minSelections = 1,
                        maxSelections = 1,
                        selectionTimeoutSeconds = 30
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
        }

        public class GeneratedAbilityDraftDto
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public int AbilityType { get; set; } = 1;
            public int Priority { get; set; } = 0;
            public bool IsActive { get; set; } = true;
            public bool DslValid { get; set; } = true;
            public string Notes { get; set; } = string.Empty;
            public string JsonConfig { get; set; } = string.Empty;
        }
    }
}
