using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.AST
{
    /// <summary>
    /// 表示完整的目标选择表达式的AST节点
    /// source_pool [where filter] [order by ordering] [take selection]
    /// </summary>
    public class TargetExpressionNode : AstNode
    {
        public override string NodeType => "TargetExpression";

        /// <summary>
        /// 定义初始目标集合的表达式 (函数调用或标识符路径)
        /// </summary>
        public ExpressionNode SourcePool { get; set; } = new IdentifierNode { Name = "UnknownSource" };

        /// <summary>
        /// 可选的过滤条件表达式 (where 子句)
        /// </summary>
        public ExpressionNode? Filter { get; set; }

        /// <summary>
        /// 可选的排序规则列表 (order by 子句)
        /// </summary>
        public List<OrderingTerm> Orderings { get; set; } = new();

        /// <summary>
        /// 可选的选择策略 (take 子句)
        /// </summary>
        public SelectionTerm? Selection { get; set; }

        public override List<AstNode> Children
        {
            get
            {
                var children = new List<AstNode> { SourcePool };
                if (Filter != null) children.Add(Filter);
                children.AddRange(Orderings);
                if (Selection != null) children.Add(Selection);
                return children;
            }
        }

        public override T Accept<T>(IAstVisitor<T> visitor)
        {
            // TODO: Add VisitTargetExpression to IAstVisitor if needed
            // return visitor.VisitTargetExpression(this);
            return default!; // Placeholder
        }

        public override AstNode Clone()
        {
            return new TargetExpressionNode
            {
                SourcePool = this.SourcePool.Clone() as ExpressionNode ?? new IdentifierNode { Name = "UnknownSource" },
                Filter = this.Filter?.Clone() as ExpressionNode,
                Orderings = this.Orderings.Select(o => o.Clone() as OrderingTerm).ToList()!,
                Selection = this.Selection?.Clone() as SelectionTerm,
                Location = this.Location,
                Metadata = new Dictionary<string, object>(this.Metadata)
            };
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(SourcePool);
            if (Filter != null) sb.Append($" where {Filter}");
            if (Orderings.Any()) sb.Append($" order by {string.Join(", ", Orderings)}");
            if (Selection != null) sb.Append($" take {Selection}");
            return sb.ToString();
        }
    }

    /// <summary>
    /// 表示 order by 子句中的一个排序项
    /// </summary>
    public class OrderingTerm : AstNode
    {
        public override string NodeType => "OrderingTerm";

        /// <summary>
        /// 用于排序的表达式
        /// </summary>
        public ExpressionNode Expression { get; set; } = new LiteralNode();

        /// <summary>
        /// 排序方向
        /// </summary>
        public OrderingDirection Direction { get; set; } = OrderingDirection.Ascending;

        public override List<AstNode> Children => new() { Expression };

        public override T Accept<T>(IAstVisitor<T> visitor)
        {
            // visitor.VisitOrderingTerm(this);
            return default!; // Placeholder
        }

        public override AstNode Clone()
        {
            return new OrderingTerm
            {
                Expression = this.Expression.Clone() as ExpressionNode ?? new LiteralNode(),
                Direction = this.Direction,
                Location = this.Location,
                Metadata = new Dictionary<string, object>(this.Metadata)
            };
        }

        public override string ToString()
        {
            return $"{Expression} {(Direction == OrderingDirection.Descending ? "desc" : "asc")}";
        }
    }

    /// <summary>
    /// 排序方向
    /// </summary>
    public enum OrderingDirection
    {
        Ascending,
        Descending
    }

    /// <summary>
    /// 表示 take 子句的选择策略和数量
    /// </summary>
    public class SelectionTerm : AstNode
    {
        public override string NodeType => "SelectionTerm";

        /// <summary>
        /// 选择策略
        /// </summary>
        public SelectionStrategy Strategy { get; set; } = SelectionStrategy.All;

        /// <summary>
        /// 选择数量的表达式 (对于 All 策略，此项可能为 null)
        /// </summary>
        public ExpressionNode? CountExpression { get; set; }

        public override List<AstNode> Children
        {
            get
            {
                var children = new List<AstNode>();
                if (CountExpression != null) children.Add(CountExpression);
                return children;
            }
        }

        public override T Accept<T>(IAstVisitor<T> visitor)
        {
            // visitor.VisitSelectionTerm(this);
            return default!; // Placeholder
        }

        public override AstNode Clone()
        {
            return new SelectionTerm
            {
                Strategy = this.Strategy,
                CountExpression = this.CountExpression?.Clone() as ExpressionNode,
                Location = this.Location,
                Metadata = new Dictionary<string, object>(this.Metadata)
            };
        }

        public override string ToString()
        {
            if (Strategy == SelectionStrategy.All) return "all";
            return $"{Strategy.ToString().ToLowerInvariant()} {CountExpression}";
        }
    }

    /// <summary>
    /// 选择策略
    /// </summary>
    public enum SelectionStrategy
    {
        All,
        First,
        Last,
        Random
    }
}