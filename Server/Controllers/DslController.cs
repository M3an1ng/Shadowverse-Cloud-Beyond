using System.Diagnostics;
using System.Linq;
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
    }
}
