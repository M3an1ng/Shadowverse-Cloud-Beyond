using System;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Dtos.Dsl;

public record DslParseRequest
{
    public string Expression { get; init; } = string.Empty;
}

public record DslParseResultDto
{
    public bool Success { get; init; }
    public int Version { get; init; } = 1;
    public string Expression { get; init; } = string.Empty;
    public DslAstNodeDto? Root { get; init; }
    public List<string> Errors { get; init; } = new();
    public long ElapsedMs { get; init; }
}

public record DslAstNodeDto
{
    public string NodeType { get; init; } = string.Empty;
    public string? DataType { get; init; }
    public object? Value { get; init; }
    public string? Name { get; init; }
    public List<string>? Path { get; init; }
    public string? Operator { get; init; }
    public bool? IsPrefix { get; init; }
    public bool? IsMethodCall { get; init; }
    public DslAstNodeDto? Left { get; init; }
    public DslAstNodeDto? Right { get; init; }
    public DslAstNodeDto? Operand { get; init; }
    public DslAstNodeDto? Inner { get; init; }
    public DslAstNodeDto? Receiver { get; init; }
    public DslAstNodeDto? Target { get; init; }
    public DslAstNodeDto? Member { get; init; }
    public DslAstNodeDto? Array { get; init; }
    public DslAstNodeDto? Index { get; init; }
    public DslAstNodeDto? Condition { get; init; }
    public DslAstNodeDto? TrueExpression { get; init; }
    public DslAstNodeDto? FalseExpression { get; init; }
    public List<DslAstNodeDto>? Arguments { get; init; }
    public List<DslAstNodeDto>? Statements { get; init; }
    public List<DslQueryOrderingDto>? QueryOrderings { get; init; }
    public string? QueryTakeStrategy { get; init; }
    public DslAstNodeDto? QueryTakeCount { get; init; }
}

public record DslQueryOrderingDto
{
    public DslAstNodeDto Expression { get; init; } = new();
    public string Direction { get; init; } = "asc";
}
