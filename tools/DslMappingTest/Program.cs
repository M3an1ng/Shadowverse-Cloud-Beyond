using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.AST;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Parser;

var repoRoot = FindRepoRoot();
if (repoRoot == null)
{
    Console.Error.WriteLine("Failed to locate repo root (missing Shadowverse-beyond.sln or data/card_data.json).");
    return;
}

var cardDataPath = Path.Combine(repoRoot, "data", "card_data.json");
if (!File.Exists(cardDataPath))
{
    Console.Error.WriteLine($"card_data.json not found at {cardDataPath}");
    return;
}

const string ParseErrorLabel = "ParseError";

var rewriteParentheses = args.Any(a => string.Equals(a, "--rewrite-parentheses", StringComparison.OrdinalIgnoreCase));
if (rewriteParentheses)
{
    var (updated, skipped) = RewriteCardDataExpressions(cardDataPath);
    Console.WriteLine($"Rewrote expressions: {updated}");
    Console.WriteLine($"Skipped expressions (parse errors): {skipped}");
    return;
}

var expressions = ExtractDslExpressions(cardDataPath);
var issues = new List<MappingIssue>();

foreach (var info in expressions)
{
    var parse = ParseExpression(info.Expression);
    if (parse.Errors.Count > 0 || parse.Ast == null)
    {
        issues.Add(new MappingIssue(info, "ParseError", parse.Errors.Count > 0 ? parse.Errors : new List<string> { "AST is null" }));
        continue;
    }

    var analysis = AnalyzeMapping(parse.Ast);
    if (analysis.Severity != MappingSeverity.Ok)
    {
        issues.Add(new MappingIssue(info, analysis.Severity.ToString(), analysis.Reasons));
    }
}

Console.WriteLine($"Total DSL expressions: {expressions.Count}");
Console.WriteLine($"Mapping issues: {issues.Count}");
Console.WriteLine($"- ParseError: {issues.Count(i => i.IssueType == ParseErrorLabel)}");
Console.WriteLine($"- RawText: {issues.Count(i => i.IssueType == MappingSeverity.RawText.ToString())}");
Console.WriteLine($"- Lossy: {issues.Count(i => i.IssueType == MappingSeverity.Lossy.ToString())}");
Console.WriteLine();

foreach (var issue in issues.OrderBy(i => i.Info.CardId).ThenBy(i => i.Info.AbilityName).ThenBy(i => i.Info.Path))
{
    Console.WriteLine($"[{issue.IssueType}] Card {issue.Info.CardId} {issue.Info.CardName}");
    Console.WriteLine($"  Ability: {issue.Info.AbilityName} ({issue.Info.AbilityType})");
    var effectSuffix = string.IsNullOrEmpty(issue.Info.EffectType)
        ? string.Empty
        : $" ({issue.Info.EffectType})";
    Console.WriteLine($"  Category: {issue.Info.Category}{effectSuffix}");
    Console.WriteLine($"  Path: {issue.Info.Path}");
    Console.WriteLine($"  Expression: {issue.Info.Expression}");
    foreach (var reason in issue.Reasons)
    {
        Console.WriteLine($"  Reason: {reason}");
    }
    Console.WriteLine();
}

return;

static string? FindRepoRoot()
{
    var current = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (current != null)
    {
        var sln = Path.Combine(current.FullName, "Shadowverse-beyond.sln");
        var cardData = Path.Combine(current.FullName, "data", "card_data.json");
        if (File.Exists(sln) || File.Exists(cardData))
        {
            return current.FullName;
        }
        current = current.Parent;
    }
    return null;
}

static List<ExpressionInfo> ExtractDslExpressions(string cardDataPath)
{
    var json = File.ReadAllText(cardDataPath);
    using var doc = JsonDocument.Parse(json);
    var root = doc.RootElement;
    var list = new List<ExpressionInfo>();

    if (root.ValueKind != JsonValueKind.Array)
    {
        return list;
    }

    for (var cardIndex = 0; cardIndex < root.GetArrayLength(); cardIndex++)
    {
        var card = root[cardIndex];
        var cardId = TryGetInt(card, "id");
        var cardName = TryGetString(card, "name");

        if (!card.TryGetProperty("abilities", out var abilities) || abilities.ValueKind != JsonValueKind.Array)
        {
            continue;
        }

        for (var abilityIndex = 0; abilityIndex < abilities.GetArrayLength(); abilityIndex++)
        {
            var ability = abilities[abilityIndex];
            var abilityPath = $"[{cardIndex}].abilities[{abilityIndex}]";
            ExtractFromAbility(ability, abilityPath, cardId, cardName, list, null, null);
        }
    }

    return list;
}

static void ExtractFromAbility(
    JsonElement ability,
    string abilityPath,
    int cardId,
    string cardName,
    List<ExpressionInfo> list,
    string? parentAbilityName,
    string? parentAbilityType)
{
    var abilityName = TryGetString(ability, "name");
    var abilityType = TryGetString(ability, "type");

    if (string.IsNullOrWhiteSpace(abilityName))
    {
        abilityName = string.IsNullOrWhiteSpace(parentAbilityName) ? "(Unnamed Ability)" : $"{parentAbilityName} (chained)";
    }

    if (string.IsNullOrWhiteSpace(abilityType) && !string.IsNullOrWhiteSpace(parentAbilityType))
    {
        abilityType = parentAbilityType;
    }

    if (ability.TryGetProperty("conditions", out var conditions) && conditions.ValueKind == JsonValueKind.Array)
    {
        for (var conditionIndex = 0; conditionIndex < conditions.GetArrayLength(); conditionIndex++)
        {
            var condition = conditions[conditionIndex];
            if (TryGetString(condition, "expression") is { Length: > 0 } expr)
            {
                list.Add(new ExpressionInfo(
                    cardId,
                    cardName,
                    abilityName,
                    abilityType,
                    "Condition",
                    $"{abilityPath}.conditions[{conditionIndex}].expression",
                    expr,
                    null));
            }
        }
    }

    if (ability.TryGetProperty("targets", out var targets) && targets.ValueKind == JsonValueKind.Array)
    {
        for (var targetIndex = 0; targetIndex < targets.GetArrayLength(); targetIndex++)
        {
            var target = targets[targetIndex];
            if (!target.TryGetProperty("query", out var query) || query.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (TryGetString(query, "sourcePoolExpression") is { Length: > 0 } expr)
            {
                list.Add(new ExpressionInfo(
                    cardId,
                    cardName,
                    abilityName,
                    abilityType,
                    "Target",
                    $"{abilityPath}.targets[{targetIndex}].query.sourcePoolExpression",
                    expr,
                    null));
            }
        }
    }

    if (ability.TryGetProperty("effects", out var effects) && effects.ValueKind == JsonValueKind.Array)
    {
        for (var effectIndex = 0; effectIndex < effects.GetArrayLength(); effectIndex++)
        {
            var effect = effects[effectIndex];
            var effectType = TryGetString(effect, "type");
            var effectPath = $"{abilityPath}.effects[{effectIndex}]";

            if (effect.TryGetProperty("parameters", out var parameters) && parameters.ValueKind == JsonValueKind.Object)
            {
                var basePath = $"{effectPath}.parameters";
                CollectDslFields(parameters, basePath, (path, expr) =>
                {
                    list.Add(new ExpressionInfo(
                        cardId,
                        cardName,
                        abilityName,
                        abilityType,
                        "Effect",
                        path,
                        expr,
                        effectType));
                });
            }

            if (effect.TryGetProperty("chainedAbilities", out var chained) && chained.ValueKind == JsonValueKind.Array)
            {
                for (var chainedIndex = 0; chainedIndex < chained.GetArrayLength(); chainedIndex++)
                {
                    var chainedAbility = chained[chainedIndex];
                    var chainedPath = $"{effectPath}.chainedAbilities[{chainedIndex}]";
                    ExtractFromAbility(chainedAbility, chainedPath, cardId, cardName, list, abilityName, abilityType);
                }
            }
        }
    }
}

static void CollectDslFields(JsonElement element, string path, Action<string, string> onFound)
{
    if (element.ValueKind == JsonValueKind.Object)
    {
        foreach (var prop in element.EnumerateObject())
        {
            var nextPath = $"{path}.{prop.Name}";
            if ((prop.Name == "formula" || prop.Name == "expression") && prop.Value.ValueKind == JsonValueKind.String)
            {
                var expr = prop.Value.GetString();
                if (!string.IsNullOrWhiteSpace(expr))
                {
                    onFound(nextPath, expr.Trim());
                }
            }
            CollectDslFields(prop.Value, nextPath, onFound);
        }
    }
    else if (element.ValueKind == JsonValueKind.Array)
    {
        var index = 0;
        foreach (var item in element.EnumerateArray())
        {
            CollectDslFields(item, $"{path}[{index}]", onFound);
            index++;
        }
    }
}

static string TryGetString(JsonElement element, string propertyName)
{
    return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
        ? prop.GetString() ?? string.Empty
        : string.Empty;
}

static int TryGetInt(JsonElement element, string propertyName)
{
    return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var value)
        ? value
        : 0;
}

static ParseResult ParseExpression(string expression)
{
    var lexer = new Lexer(expression);
    var tokens = lexer.Tokenize();
    var parser = new Parser(tokens);
    var ast = parser.ParseSingleExpression();

    var errors = new List<string>();
    errors.AddRange(lexer.Errors.Select(e => e.ToString()));
    errors.AddRange(parser.Errors.Select(e => e.ToString()));

    return new ParseResult(ast, errors);
}

static MappingAnalysis AnalyzeMapping(ExpressionNode node)
{
    var analysis = new MappingAnalysis();
    AnalyzeNode(node, analysis);
    return analysis;
}

static bool AnalyzeNode(ExpressionNode? node, MappingAnalysis analysis)
{
    if (node == null)
    {
        analysis.Add(MappingSeverity.RawText, "AST node is null");
        return false;
    }

    switch (node)
    {
        case ParenthesizedExpressionNode parenthesized:
            return AnalyzeNode(parenthesized.Inner, analysis);
        case LiteralNode:
        case IdentifierNode:
            return true;
        case MemberAccessNode memberAccess:
            if (FlattenMemberAccessPath(memberAccess) == null)
            {
                analysis.Add(MappingSeverity.RawText, "Member access base is not an identifier chain (e.g., function call result)");
                return false;
            }
            return true;
        case BinaryOperationNode binary:
            if (!IsSupportedBinaryOperator(binary.Operator))
            {
                analysis.Add(MappingSeverity.RawText, $"Unsupported binary operator: {binary.Operator}");
                return false;
            }
            return AnalyzeNode(binary.Left, analysis) && AnalyzeNode(binary.Right, analysis);
        case UnaryOperationNode unary:
            if (unary.Operator != TokenType.Minus)
            {
                analysis.Add(MappingSeverity.RawText, $"Unsupported unary operator: {unary.Operator} (only '-' with numeric literal is supported)");
                return false;
            }
            if (unary.Operand is LiteralNode literal && IsNumericLiteral(literal))
            {
                return true;
            }
            analysis.Add(MappingSeverity.RawText, "Unary '-' only supported for numeric literal");
            return false;
        case FunctionCallNode call:
            return AnalyzeFunctionCall(call, analysis);
        case ConditionalExpressionNode:
            analysis.Add(MappingSeverity.RawText, "Conditional (ternary) expressions are not supported");
            return false;
        case ArrayAccessNode:
            analysis.Add(MappingSeverity.RawText, "Array access expressions are not supported");
            return false;
        case BlockNode:
            analysis.Add(MappingSeverity.RawText, "Block expressions are not supported");
            return false;
        default:
            analysis.Add(MappingSeverity.RawText, $"Unsupported node type: {node.NodeType}");
            return false;
    }
}

static (int Updated, int Skipped) RewriteCardDataExpressions(string cardDataPath)
{
    var json = File.ReadAllText(cardDataPath);
    var root = JsonNode.Parse(json);
    if (root == null)
    {
        Console.Error.WriteLine("Failed to parse card_data.json");
        return (0, 0);
    }

    var updated = 0;
    var skipped = 0;
    RewriteDslFields(root, ref updated, ref skipped);

    var options = new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    File.WriteAllText(cardDataPath, root.ToJsonString(options) + Environment.NewLine);
    return (updated, skipped);
}

static void RewriteDslFields(JsonNode? node, ref int updated, ref int skipped)
{
    if (node is JsonObject obj)
    {
        foreach (var entry in obj.ToList())
        {
            var key = entry.Key;
            var value = entry.Value;

            if (value is JsonValue jsonValue && (key == "expression" || key == "formula" || key == "sourcePoolExpression"))
            {
                var expr = jsonValue.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(expr))
                {
                    if (TryFormatExpression(expr, out var formatted))
                    {
                        if (!string.Equals(expr, formatted, StringComparison.Ordinal))
                        {
                            obj[key] = formatted;
                            updated++;
                        }
                    }
                    else
                    {
                        skipped++;
                    }
                }
            }
            else
            {
                RewriteDslFields(value, ref updated, ref skipped);
            }
        }
        return;
    }

    if (node is JsonArray array)
    {
        foreach (var item in array)
        {
            RewriteDslFields(item, ref updated, ref skipped);
        }
    }
}

static bool TryFormatExpression(string expression, out string formatted)
{
    formatted = expression;
    var raw = expression.Trim();
    if (string.IsNullOrEmpty(raw))
    {
        return false;
    }

    var lexer = new Lexer(raw);
    var tokens = lexer.Tokenize();
    var parser = new Parser(tokens);
    var ast = parser.ParseSingleExpression();

    var errors = new List<string>();
    errors.AddRange(lexer.Errors.Select(e => e.ToString()));
    errors.AddRange(parser.Errors.Select(e => e.ToString()));

    if (errors.Count > 0 || ast == null)
    {
        return false;
    }

    var style = DetermineOperatorStyle(raw);
    formatted = FormatExpression(ast, style);
    return true;
}

static OperatorStyle DetermineOperatorStyle(string expression)
{
    if (Regex.IsMatch(expression, @"(&&)|(\|\|)"))
    {
        return OperatorStyle.Symbol;
    }

    if (Regex.IsMatch(expression, @"\b(and|or|not)\b", RegexOptions.IgnoreCase))
    {
        return OperatorStyle.Keyword;
    }

    return OperatorStyle.Symbol;
}

static string FormatExpression(ExpressionNode node, OperatorStyle style)
{
    switch (node)
    {
        case ParenthesizedExpressionNode parenthesized:
            return $"({FormatExpression(parenthesized.Inner, style)})";
        case LiteralNode literal:
            return FormatLiteral(literal);
        case IdentifierNode identifier:
            return FormatIdentifier(identifier);
        case MemberAccessNode memberAccess:
            return $"{FormatExpression(memberAccess.Target, style)}.{FormatIdentifier(memberAccess.Member)}";
        case UnaryOperationNode unary:
            return $"{OperatorToString(unary.Operator, style)}({FormatExpression(unary.Operand, style)})";
        case BinaryOperationNode binary:
            return $"({FormatExpression(binary.Left, style)}) {OperatorToString(binary.Operator, style)} ({FormatExpression(binary.Right, style)})";
        case FunctionCallNode call:
            return FormatFunctionCall(call, style);
        case ArrayAccessNode arrayAccess:
            return $"{FormatExpression(arrayAccess.Array, style)}[{FormatExpression(arrayAccess.Index, style)}]";
        case ConditionalExpressionNode conditional:
            return $"({FormatExpression(conditional.Condition, style)}) ? ({FormatExpression(conditional.TrueExpression, style)}) : ({FormatExpression(conditional.FalseExpression, style)})";
        case BlockNode block:
            return string.Join("; ", block.Statements.Select(stmt => FormatExpression(stmt, style)));
        default:
            return node.ToString() ?? string.Empty;
    }
}

static string FormatFunctionCall(FunctionCallNode call, OperatorStyle style)
{
    var name = call.FunctionName ?? string.Empty;
    var lower = name.ToLowerInvariant();
    if ((lower is "query" or "orderby" or "take") && call.Arguments.Count > 0)
    {
        var receiver = FormatExpression(call.Arguments[0], style);
        var args = call.Arguments.Skip(1).Select(arg => $"({FormatExpression(arg, style)})");
        return $"{receiver}.{name}({string.Join(", ", args)})";
    }

    var formattedArgs = call.Arguments.Select(arg => $"({FormatExpression(arg, style)})");
    return $"{name}({string.Join(", ", formattedArgs)})";
}

static string FormatIdentifier(IdentifierNode identifier)
{
    if (identifier.Path != null && identifier.Path.Count > 0)
    {
        return string.Join(".", identifier.Path);
    }
    return identifier.Name ?? string.Empty;
}

static string FormatLiteral(LiteralNode literal)
{
    if (literal.Value == null)
    {
        return "null";
    }

    return literal.Value switch
    {
        bool b => b ? "true" : "false",
        string s => JsonSerializer.Serialize(s),
        float f => f.ToString(CultureInfo.InvariantCulture),
        double d => d.ToString(CultureInfo.InvariantCulture),
        decimal m => m.ToString(CultureInfo.InvariantCulture),
        sbyte or byte or short or ushort or int or uint or long or ulong
            => Convert.ToString(literal.Value, CultureInfo.InvariantCulture) ?? "0",
        _ => Convert.ToString(literal.Value, CultureInfo.InvariantCulture) ?? literal.Value.ToString() ?? string.Empty
    };
}

static string OperatorToString(TokenType op, OperatorStyle style)
{
    return op switch
    {
        TokenType.Plus => "+",
        TokenType.Minus => "-",
        TokenType.Multiply => "*",
        TokenType.Divide => "/",
        TokenType.Modulo => "%",
        TokenType.Power => "**",
        TokenType.Equal => "==",
        TokenType.NotEqual => "!=",
        TokenType.GreaterThan => ">",
        TokenType.LessThan => "<",
        TokenType.GreaterEqual => ">=",
        TokenType.LessEqual => "<=",
        TokenType.And => style == OperatorStyle.Keyword ? "and" : "&&",
        TokenType.Or => style == OperatorStyle.Keyword ? "or" : "||",
        TokenType.Not => style == OperatorStyle.Keyword ? "not" : "!",
        TokenType.SetUnion => "|",
        TokenType.SetIntersect => "&",
        TokenType.Assign => "=",
        TokenType.PlusAssign => "+=",
        TokenType.MinusAssign => "-=",
        TokenType.Arrow => "->",
        TokenType.Question => "?",
        _ => op.ToString()
    };
}

static bool AnalyzeFunctionCall(FunctionCallNode call, MappingAnalysis analysis)
{
    var name = call.FunctionName ?? string.Empty;
    var lower = name.ToLowerInvariant();

    if ((lower is "query" or "orderby" or "take") && call.Receiver == null && !call.IsMethodCall)
    {
        if (call.Arguments.Count == 0)
        {
            analysis.Add(MappingSeverity.RawText, $"'{name}' call is missing a source list argument");
            return false;
        }

        var receiverOk = AnalyzeNode(call.Arguments[0], analysis);
        var args = call.Arguments.Skip(1).ToList();
        if (args.Count > 0)
        {
            receiverOk &= AnalyzeNode(args[0], analysis);
        }
        if (args.Count > 1)
        {
            analysis.Add(MappingSeverity.Lossy, $"'{name}' call has {args.Count + 1} args; UI maps only the first filter/sort/take argument");
        }

        return receiverOk;
    }

    if (call.Receiver != null || call.IsMethodCall)
    {
        if (lower is "query" or "orderby" or "take")
        {
            var ok = AnalyzeNode(call.Receiver, analysis);
            if (call.Arguments.Count > 0)
            {
                ok &= AnalyzeNode(call.Arguments[0], analysis);
            }
            if (call.Arguments.Count > 1)
            {
                analysis.Add(MappingSeverity.Lossy, $"Method call '{name}' has {call.Arguments.Count} args; UI maps only the first argument");
            }
            return ok;
        }

        analysis.Add(MappingSeverity.Lossy, $"Method call '{name}' with receiver will be mapped without receiver");
        foreach (var arg in call.Arguments)
        {
            AnalyzeNode(arg, analysis);
        }
        return true;
    }

    if (lower is "query" or "orderby" or "take")
    {
        if (call.Arguments.Count >= 2)
        {
            analysis.Add(MappingSeverity.Lossy, $"'{name}' parsed as a normal function call; UI cannot map it to the list pipeline");
        }
    }

    if (IsEventAccessor(lower))
    {
        if (call.Arguments.Count == 0)
        {
            analysis.Add(MappingSeverity.RawText, $"Event accessor '{name}' requires a string literal argument");
            return false;
        }

        if (call.Arguments[0] is LiteralNode literal && literal.Value is string)
        {
            return true;
        }

        analysis.Add(MappingSeverity.Lossy, $"Event accessor '{name}' argument is not a string literal; UI will not resolve event field label");
    }

    var allOk = true;
    foreach (var arg in call.Arguments)
    {
        allOk &= AnalyzeNode(arg, analysis);
    }

    return allOk;
}

static List<string>? FlattenMemberAccessPath(ExpressionNode node)
{
    if (node is MemberAccessNode member)
    {
        var left = FlattenMemberAccessPath(member.Target);
        var right = FlattenMemberAccessPath(member.Member);
        if (left == null || right == null)
        {
            return null;
        }
        left.AddRange(right);
        return left;
    }

    if (node is IdentifierNode identifier)
    {
        if (identifier.Path != null && identifier.Path.Count > 0)
        {
            return new List<string>(identifier.Path);
        }
        if (!string.IsNullOrWhiteSpace(identifier.Name))
        {
            return new List<string> { identifier.Name };
        }
    }

    return null;
}

static bool IsSupportedBinaryOperator(TokenType op)
{
    return op is TokenType.Equal
        or TokenType.NotEqual
        or TokenType.GreaterThan
        or TokenType.GreaterEqual
        or TokenType.LessThan
        or TokenType.LessEqual
        or TokenType.And
        or TokenType.Or
        or TokenType.SetUnion
        or TokenType.SetIntersect
        or TokenType.Plus
        or TokenType.Minus
        or TokenType.Multiply
        or TokenType.Divide;
}

static bool IsNumericLiteral(LiteralNode literal)
{
    return literal.Value is sbyte
        or byte
        or short
        or ushort
        or int
        or uint
        or long
        or ulong
        or float
        or double
        or decimal;
}

static bool IsEventAccessor(string name)
{
    return name is "eventbool"
        or "eventstring"
        or "eventguid"
        or "eventint"
        or "eventcard"
        or "eventcards"
        or "eventguidlist";
}

record ParseResult(ExpressionNode? Ast, List<string> Errors);

record ExpressionInfo(
    int CardId,
    string CardName,
    string AbilityName,
    string AbilityType,
    string Category,
    string Path,
    string Expression,
    string? EffectType);

record MappingIssue(ExpressionInfo Info, string IssueType, List<string> Reasons);

enum MappingSeverity
{
    Ok = 0,
    Lossy = 1,
    RawText = 2
}

enum OperatorStyle
{
    Symbol,
    Keyword
}

sealed class MappingAnalysis
{
    public MappingSeverity Severity { get; private set; } = MappingSeverity.Ok;
    public List<string> Reasons { get; } = new();

    public void Add(MappingSeverity severity, string reason)
    {
        if (!string.IsNullOrWhiteSpace(reason))
        {
            Reasons.Add(reason);
        }
        if (severity > Severity)
        {
            Severity = severity;
        }
    }
}
