using System.Collections.Generic;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Parser; // Added using for TokenType

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.AST
{
    /// <summary>
    /// 表达式节点基类
    /// 所有表达式节点的基类
    /// </summary>
    public abstract class ExpressionNode : AstNode
    {
        /// <summary>
        /// 表达式的数据类型
        /// </summary>
        public string DataType { get; set; } = "unknown";

        /// <summary>
        /// 是否为常量表达式
        /// </summary>
        public virtual bool IsConstant => false;

        /// <summary>
        /// 获取常量值（如果是常量表达式）
        /// </summary>
        /// <returns>常量值</returns>
        public virtual object? GetConstantValue() => null;
    }

    /// <summary>
    /// 字面量节点
    /// 表示常量值
    /// </summary>
    public class LiteralNode : ExpressionNode
    {
        public override string NodeType => "Literal";

        /// <summary>
        /// 字面量值
        /// </summary>
        public object? Value { get; set; }

        public override bool IsConstant => true;

        public override object? GetConstantValue() => Value;

        public override T Accept<T>(IAstVisitor<T> visitor)
        {
            return visitor.VisitLiteral(this);
        }

        public override AstNode Clone()
        {
            return new LiteralNode
            {
                Value = this.Value,
                DataType = this.DataType,
                Location = this.Location,
                Metadata = new Dictionary<string, object>(this.Metadata)
            };
        }

        public override string ToString()
        {
            if (Value is IEnumerable<string> stringList)
            {
                return $"Literal([{string.Join(",", stringList)}])";
            }
            return $"Literal({Value})";
        }
    }

    /// <summary>
    /// 标识符节点
    /// 表示变量、属性或函数名
    /// </summary>
    public class IdentifierNode : ExpressionNode
    {
        public override string NodeType => "Identifier";

        /// <summary>
        /// 标识符名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 标识符路径（用于嵌套属性访问，如 "self.attack"）
        /// </summary>
        public List<string> Path { get; set; } = new();

        public override T Accept<T>(IAstVisitor<T> visitor)
        {
            return visitor.VisitIdentifier(this);
        }

        public override AstNode Clone()
        {
            return new IdentifierNode
            {
                Name = this.Name,
                Path = new List<string>(this.Path),
                DataType = this.DataType,
                Location = this.Location,
                Metadata = new Dictionary<string, object>(this.Metadata)
            };
        }

        public override string ToString()
        {
            return Path.Count > 0 ? string.Join(".", Path) : Name;
        }
    }

    /// <summary>
    /// 二元操作节点
    /// 表示二元运算表达式
    /// </summary>
    public class BinaryOperationNode : ExpressionNode
    {
        public override string NodeType => "BinaryOperation";

        /// <summary>
        /// 左操作数
        /// </summary>
        public ExpressionNode Left { get; set; } = new LiteralNode();

        /// <summary>
        /// 操作符
        /// </summary>
        public TokenType Operator { get; set; } // Changed type to TokenType

        /// <summary>
        /// 右操作数
        /// </summary>
        public ExpressionNode Right { get; set; } = new LiteralNode();

        public override bool IsConstant => Left.IsConstant && Right.IsConstant;

        public override List<AstNode> Children => new() { Left, Right };

        public override T Accept<T>(IAstVisitor<T> visitor)
        {
            return visitor.VisitBinaryOperation(this);
        }

        public override AstNode Clone()
        {
            return new BinaryOperationNode
            {
                Left = this.Left.Clone() as ExpressionNode ?? new LiteralNode(),
                Operator = this.Operator, // Type is already TokenType
                Right = this.Right.Clone() as ExpressionNode ?? new LiteralNode(),
                DataType = this.DataType,
                Location = this.Location,
                Metadata = new Dictionary<string, object>(this.Metadata)
            };
        }

        public override string ToString()
        {
            // Use default enum ToString() for now
            return $"({Left} {Operator} {Right})";
        }
    }

    /// <summary>
    /// 一元操作节点
    /// 表示一元运算表达式
    /// </summary>
    public class UnaryOperationNode : ExpressionNode
    {
        public override string NodeType => "UnaryOperation";

        /// <summary>
        /// 操作符
        /// </summary>
        public TokenType Operator { get; set; } // Changed type to TokenType

        /// <summary>
        /// 操作数
        /// </summary>
        public ExpressionNode Operand { get; set; } = new LiteralNode();

        /// <summary>
        /// 是否为前缀操作符
        /// </summary>
        public bool IsPrefix { get; set; } = true;

        public override bool IsConstant => Operand.IsConstant;

        public override List<AstNode> Children => new() { Operand };

        public override T Accept<T>(IAstVisitor<T> visitor)
        {
            return visitor.VisitUnaryOperation(this);
        }

        public override AstNode Clone()
        {
            return new UnaryOperationNode
            {
                Operator = this.Operator, // Type is already TokenType
                Operand = this.Operand.Clone() as ExpressionNode ?? new LiteralNode(),
                IsPrefix = this.IsPrefix,
                DataType = this.DataType,
                Location = this.Location,
                Metadata = new Dictionary<string, object>(this.Metadata)
            };
        }

        public override string ToString()
        {
            // Use default enum ToString() for now
            var opStr = Operator.ToString();
            return IsPrefix ? $"{opStr}{Operand}" : $"{Operand}{opStr}";
        }
    }

    /// <summary>
    /// 括号表达式节点
    /// 表示显式括号包裹的表达式
    /// </summary>
    public class ParenthesizedExpressionNode : ExpressionNode
    {
        public override string NodeType => "ParenthesizedExpression";

        /// <summary>
        /// 括号内的表达式
        /// </summary>
        public ExpressionNode Inner { get; set; } = new LiteralNode();

        public override bool IsConstant => Inner.IsConstant;

        public override List<AstNode> Children => new() { Inner };

        public override T Accept<T>(IAstVisitor<T> visitor)
        {
            return visitor.VisitParenthesizedExpression(this);
        }

        public override AstNode Clone()
        {
            return new ParenthesizedExpressionNode
            {
                Inner = this.Inner.Clone() as ExpressionNode ?? new LiteralNode(),
                DataType = this.DataType,
                Location = this.Location,
                Metadata = new Dictionary<string, object>(this.Metadata)
            };
        }

        public override string ToString()
        {
            return $"({Inner})";
        }
    }

    /// <summary>
    /// 函数调用节点
    /// 表示函数调用表达式
    /// </summary>
    public class FunctionCallNode : ExpressionNode
    {
        public override string NodeType => "FunctionCall";

        /// <summary>
        /// 函数名
        /// </summary>
        public string FunctionName { get; set; } = string.Empty;

        /// <summary>
        /// 参数列表
        /// </summary>
        public List<ExpressionNode> Arguments { get; set; } = new();

        /// <summary>
        /// 是否为方法调用（有接收者对象）
        /// </summary>
        public bool IsMethodCall { get; set; } = false;

        /// <summary>
        /// 接收者对象（用于方法调用）
        /// </summary>
        public ExpressionNode? Receiver { get; set; }

        public override List<AstNode> Children
        {
            get
            {
                var children = new List<AstNode>();
                if (Receiver != null) children.Add(Receiver);
                children.AddRange(Arguments);
                return children;
            }
        }

        public override T Accept<T>(IAstVisitor<T> visitor)
        {
            return visitor.VisitFunctionCall(this);
        }

        public override AstNode Clone()
        {
            return new FunctionCallNode
            {
                FunctionName = this.FunctionName,
                Arguments = this.Arguments.Select(a => a.Clone() as ExpressionNode).ToList()!,
                IsMethodCall = this.IsMethodCall,
                Receiver = this.Receiver?.Clone() as ExpressionNode,
                DataType = this.DataType,
                Location = this.Location,
                Metadata = new Dictionary<string, object>(this.Metadata)
            };
        }

        public override string ToString()
        {
            var args = string.Join(", ", Arguments.Select(a => a.ToString()));
            if (IsMethodCall && Receiver != null)
            {
                return $"{Receiver}.{FunctionName}({args})";
            }
            return $"{FunctionName}({args})";
        }
    }

    /// <summary>
    /// 块节点
    /// 表示代码块或语句序列
    /// </summary>
    public class BlockNode : ExpressionNode
    {
        public override string NodeType => "Block";

        /// <summary>
        /// 语句列表
        /// </summary>
        public List<ExpressionNode> Statements { get; set; } = new();

        public override List<AstNode> Children => Statements.Cast<AstNode>().ToList();

        public override T Accept<T>(IAstVisitor<T> visitor)
        {
            return visitor.VisitBlock(this);
        }

        public override AstNode Clone()
        {
            return new BlockNode
            {
                Statements = this.Statements.Select(s => s.Clone() as ExpressionNode).ToList()!,
                DataType = this.DataType,
                Location = this.Location,
                Metadata = new Dictionary<string, object>(this.Metadata)
            };
        }

        public override string ToString()
        {
            return $"Block({Statements.Count} statements)";
        }
    }

    /// <summary>
    /// 条件表达式节点
    /// 表示三元条件表达式 (condition ? trueExpr : falseExpr)
    /// </summary>
    public class ConditionalExpressionNode : ExpressionNode
    {
        public override string NodeType => "ConditionalExpression";

        /// <summary>
        /// 条件表达式
        /// </summary>
        public ExpressionNode Condition { get; set; } = new LiteralNode();

        /// <summary>
        /// 条件为真时的表达式
        /// </summary>
        public ExpressionNode TrueExpression { get; set; } = new LiteralNode();

        /// <summary>
        /// 条件为假时的表达式
        /// </summary>
        public ExpressionNode FalseExpression { get; set; } = new LiteralNode();

        public override bool IsConstant => Condition.IsConstant && TrueExpression.IsConstant && FalseExpression.IsConstant;

        public override List<AstNode> Children => new() { Condition, TrueExpression, FalseExpression };

        public override T Accept<T>(IAstVisitor<T> visitor)
        {
            return visitor.VisitConditionalExpression(this);
        }

        public override AstNode Clone()
        {
            return new ConditionalExpressionNode
            {
                Condition = this.Condition.Clone() as ExpressionNode ?? new LiteralNode(),
                TrueExpression = this.TrueExpression.Clone() as ExpressionNode ?? new LiteralNode(),
                FalseExpression = this.FalseExpression.Clone() as ExpressionNode ?? new LiteralNode(),
                DataType = this.DataType,
                Location = this.Location,
                Metadata = new Dictionary<string, object>(this.Metadata)
            };
        }

        public override string ToString()
        {
            return $"({Condition} ? {TrueExpression} : {FalseExpression})";
        }
    }

    /// <summary>
    /// 数组访问节点
    /// 表示数组或集合的索引访问
    /// </summary>
    public class ArrayAccessNode : ExpressionNode
    {
        public override string NodeType => "ArrayAccess";

        /// <summary>
        /// 数组表达式
        /// </summary>
        public ExpressionNode Array { get; set; } = new LiteralNode();

        /// <summary>
        /// 索引表达式
        /// </summary>
        public ExpressionNode Index { get; set; } = new LiteralNode();

        public override List<AstNode> Children => new() { Array, Index };

        public override T Accept<T>(IAstVisitor<T> visitor)
        {
            return visitor.VisitArrayAccess(this);
        }

        public override AstNode Clone()
        {
            return new ArrayAccessNode
            {
                Array = this.Array.Clone() as ExpressionNode ?? new LiteralNode(),
                Index = this.Index.Clone() as ExpressionNode ?? new LiteralNode(),
                DataType = this.DataType,
                Location = this.Location,
                Metadata = new Dictionary<string, object>(this.Metadata)
            };
        }

        public override string ToString()
        {
            return $"{Array}[{Index}]";
        }
    }

    /// <summary>
    /// Represents a member access expression, e.g., "self.Attack" or "context.Player.Hand".
    /// </summary>
    public class MemberAccessNode : ExpressionNode
    {
        public override string NodeType => "MemberAccess";

        /// <summary>
        /// The expression representing the object whose member is being accessed.
        /// </summary>
        public ExpressionNode Target { get; set; } = new IdentifierNode();

        /// <summary>
        /// The identifier of the member being accessed.
        /// </summary>
        public IdentifierNode Member { get; set; } = new IdentifierNode();

        public override List<AstNode> Children => new() { Target, Member };

        public override T Accept<T>(IAstVisitor<T> visitor)
        {
            return visitor.VisitMemberAccess(this);
        }

        public override AstNode Clone()
        {
            return new MemberAccessNode
            {
                Target = this.Target.Clone() as ExpressionNode ?? new IdentifierNode(),
                Member = this.Member.Clone() as IdentifierNode ?? new IdentifierNode(),
                DataType = this.DataType,
                Location = this.Location,
                Metadata = new Dictionary<string, object>(this.Metadata)
            };
        }

        public override string ToString()
        {
            return $"{Target}.{Member}";
        }
    }
}
