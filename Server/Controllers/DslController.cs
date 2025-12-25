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
