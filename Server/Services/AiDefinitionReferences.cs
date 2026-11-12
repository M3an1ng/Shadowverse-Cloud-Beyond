using System.Globalization;
using System.Text.Json;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.AST;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Parser;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Services;

// Only actual card-reference fields create edges. Never scrape arbitrary numbers or execute DSL.
internal static class AiDefinitionReferences
{
    internal sealed record Links(int[] CardIds,string[] UnresolvedParameters,int UnresolvedCount,bool Available=true);
    private static readonly HashSet<string> IdFields=new(StringComparer.OrdinalIgnoreCase) {
        "CardDefinitionId","CardDefinitionIds","TargetCardDefinitionId","PlayAsCardDefinitionId"
    };
    internal static AiRuleContext.ReferenceCatalog Collect(IGameDataProvider provider,IEnumerable<int> roots,
        IEnumerable<string> descriptions,int limit=40,int scanLimit=256,IEnumerable<int>? providedIds=null)=>
        AiRuleContext.CollectConfigured(descriptions,provider.GetAllCardDefinitions(),roots,id=>Read(provider,id),limit,scanLimit,providedIds);

    internal static Links Read(IGameDataProvider provider,int cardId)
    {
        var facts=AiAbilityFacts.Read(provider,[cardId],int.MaxValue);
        if(facts.Cards.Length!=1)return new([],facts.Omitted.Select(item=>item.Reason).ToArray(),facts.Omitted.Length,false);
        return Extract(facts.Cards[0]);
    }
    internal static Links Extract(JsonElement config)
    {
        var ids=new SortedSet<int>();var unresolved=new SortedSet<string>(StringComparer.Ordinal);
        void Visit(JsonElement value,string path) {
            if(value.ValueKind==JsonValueKind.Array) {
                var index=0;foreach(var item in value.EnumerateArray())Visit(item,path+"["+(index++)+"]");
            } else if(value.ValueKind==JsonValueKind.Object)foreach(var property in value.EnumerateObject()) {
                var next=path.Length==0?property.Name:path+"."+property.Name;
                if(IdFields.Contains(property.Name)) {
                    if(!ReadReference(property.Value,ids))unresolved.Add(next);
                } else Visit(property.Value,next);
            }
        }
        Visit(config,"");
        return new(ids.ToArray(),unresolved.Take(16).Select(path=>path.Length<=240?path:path[..240]).ToArray(),unresolved.Count);
    }
    private static bool ReadReference(JsonElement value,SortedSet<int> ids)
    {
        if(value.ValueKind==JsonValueKind.Object) {
            JsonElement Named(string name)=>value.EnumerateObject().FirstOrDefault(property=>property.Name.Equals(name,StringComparison.OrdinalIgnoreCase)).Value;
            var kind=Named("$type");
            // Match ValueConfigConverter: a declared formula wins over stale Value, and vice versa.
            if(kind.ValueKind==JsonValueKind.String&&kind.GetString()?.Equals("FormulaValueConfig",StringComparison.OrdinalIgnoreCase)==true) {
                var formula=Named("Formula");return formula.ValueKind==JsonValueKind.String&&ConstantExpression(formula.GetString()??"",ids);
            }
            if(kind.ValueKind==JsonValueKind.String&&kind.GetString()?.Equals("StaticValueConfig",StringComparison.OrdinalIgnoreCase)==true) {
                var literal=Named("Value");return literal.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null||Literal(literal,ids);
            }
            foreach(var property in value.EnumerateObject())
                if(property.Name.Equals("value",StringComparison.OrdinalIgnoreCase))return Literal(property.Value,ids);
            foreach(var property in value.EnumerateObject())
                if(property.Name.Equals("dsl",StringComparison.OrdinalIgnoreCase)||property.Name.Equals("formula",StringComparison.OrdinalIgnoreCase))
                    return property.Value.ValueKind==JsonValueKind.String&&ConstantExpression(property.Value.GetString()??"",ids);
            return false;
        }
        return Literal(value,ids);
    }
    private static bool Literal(JsonElement value,SortedSet<int> ids)
    {
        if(value.ValueKind==JsonValueKind.Array) {
            var complete=true;foreach(var item in value.EnumerateArray())complete=Literal(item,ids)&&complete;return complete;
        }
        if(value.ValueKind==JsonValueKind.Null)return true;
        if(value.ValueKind==JsonValueKind.Number&&value.TryGetInt32(out var id)) {if(id>0)ids.Add(id);return true;}
        if(value.ValueKind==JsonValueKind.String&&int.TryParse(value.GetString(),NumberStyles.Integer,CultureInfo.InvariantCulture,out id))
        {if(id>0)ids.Add(id);return true;}
        return false;
    }
    private static bool ConstantExpression(string text,SortedSet<int> ids)
    {
        if(text.Length>32768)return false;
        try {
            var lexer=new Lexer(text);var tokens=lexer.Tokenize();
            if(lexer.Errors.Count>0||tokens.Count>4096)return false;
            // Protect the recursive parser from pathological nesting and unary/operator chains.
            var depth=0;var operators=0;
            foreach(var token in tokens) {
                if(token.Type is TokenType.LeftParen or TokenType.LeftBracket or TokenType.LeftBrace) {
                    if(++depth>32)return false;
                } else if(token.Type is TokenType.RightParen or TokenType.RightBracket or TokenType.RightBrace)depth--;
                if(token.Type is not (TokenType.Number or TokenType.String or TokenType.Comma or TokenType.LeftParen
                    or TokenType.RightParen or TokenType.LeftBracket or TokenType.RightBracket or TokenType.EOF
                    or TokenType.Whitespace or TokenType.Newline or TokenType.Comment))if(++operators>32)return false;
            }
            var node=new Parser(tokens).ParseSingleExpression();
            while(node is ParenthesizedExpressionNode parentheses)node=parentheses.Inner;
            // No function, query, player property, arithmetic evaluation or random sampling here.
            return node is LiteralNode literal&&Literal(JsonSerializer.SerializeToElement(literal.Value),ids);
        }
        catch(Exception error) when(error is ArgumentException or InvalidOperationException or JsonException or FormatException)
        {return false;}
    }
}