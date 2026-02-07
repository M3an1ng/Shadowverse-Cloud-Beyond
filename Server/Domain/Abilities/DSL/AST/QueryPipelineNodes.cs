using System.Collections.Generic;
using System.Linq;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.AST
{
    /// <summary>
    /// Represents a collection filter pipeline stage: source.query(filter).
    /// </summary>
    public class QueryFilterNode : ExpressionNode
    {
        public override string NodeType => "QueryFilter";

        public ExpressionNode Source { get; set; } = new LiteralNode();

        public ExpressionNode Filter { get; set; } = new LiteralNode();

        public override List<AstNode> Children => new() { Source, Filter };

        public override T Accept<T>(IAstVisitor<T> visitor)
        {
            return default!;
        }

        public override AstNode Clone()
        {
            return new QueryFilterNode
            {
                Source = Source.Clone() as ExpressionNode ?? new LiteralNode(),
                Filter = Filter.Clone() as ExpressionNode ?? new LiteralNode(),
                DataType = DataType,
                Location = Location,
                Metadata = new Dictionary<string, object>(Metadata)
            };
        }

        public override string ToString()
        {
            return $"{Source}.query({Filter})";
        }
    }

    /// <summary>
    /// Represents a collection ordering pipeline stage: source.orderBy(...).
    /// </summary>
    public class QueryOrderByNode : ExpressionNode
    {
        public override string NodeType => "QueryOrderBy";

        public ExpressionNode Source { get; set; } = new LiteralNode();

        public List<OrderingTerm> Orderings { get; set; } = new();

        public override List<AstNode> Children
        {
            get
            {
                var children = new List<AstNode> { Source };
                children.AddRange(Orderings);
                return children;
            }
        }

        public override T Accept<T>(IAstVisitor<T> visitor)
        {
            return default!;
        }

        public override AstNode Clone()
        {
            return new QueryOrderByNode
            {
                Source = Source.Clone() as ExpressionNode ?? new LiteralNode(),
                Orderings = Orderings.Select(o => o.Clone() as OrderingTerm).ToList()!,
                DataType = DataType,
                Location = Location,
                Metadata = new Dictionary<string, object>(Metadata)
            };
        }

        public override string ToString()
        {
            var orderingText = Orderings.Any()
                ? string.Join(", ", Orderings)
                : string.Empty;
            return $"{Source}.orderBy({orderingText})";
        }
    }

    /// <summary>
    /// Represents a collection distinct pipeline stage: source.distinct(key).
    /// </summary>
    public class QueryDistinctNode : ExpressionNode
    {
        public override string NodeType => "QueryDistinct";

        public ExpressionNode Source { get; set; } = new LiteralNode();

        public ExpressionNode KeySelector { get; set; } = new LiteralNode();

        public override List<AstNode> Children => new() { Source, KeySelector };

        public override T Accept<T>(IAstVisitor<T> visitor)
        {
            return default!;
        }

        public override AstNode Clone()
        {
            return new QueryDistinctNode
            {
                Source = Source.Clone() as ExpressionNode ?? new LiteralNode(),
                KeySelector = KeySelector.Clone() as ExpressionNode ?? new LiteralNode(),
                DataType = DataType,
                Location = Location,
                Metadata = new Dictionary<string, object>(Metadata)
            };
        }

        public override string ToString()
        {
            return $"{Source}.distinct({KeySelector})";
        }
    }

    /// <summary>
    /// Represents a collection projection pipeline stage: source.select(expr).
    /// </summary>
    public class QuerySelectNode : ExpressionNode
    {
        public override string NodeType => "QuerySelect";

        public ExpressionNode Source { get; set; } = new LiteralNode();

        public ExpressionNode Selector { get; set; } = new LiteralNode();

        public override List<AstNode> Children => new() { Source, Selector };

        public override T Accept<T>(IAstVisitor<T> visitor)
        {
            return default!;
        }

        public override AstNode Clone()
        {
            return new QuerySelectNode
            {
                Source = Source.Clone() as ExpressionNode ?? new LiteralNode(),
                Selector = Selector.Clone() as ExpressionNode ?? new LiteralNode(),
                DataType = DataType,
                Location = Location,
                Metadata = new Dictionary<string, object>(Metadata)
            };
        }

        public override string ToString()
        {
            return $"{Source}.select({Selector})";
        }
    }


    /// <summary>
    /// Represents a collection selection pipeline stage: source.take(...).
    /// </summary>
    public class QueryTakeNode : ExpressionNode
    {
        public override string NodeType => "QueryTake";

        public ExpressionNode Source { get; set; } = new LiteralNode();

        public SelectionTerm Selection { get; set; } = new();

        public override List<AstNode> Children => new() { Source, Selection };

        public override T Accept<T>(IAstVisitor<T> visitor)
        {
            return default!;
        }

        public override AstNode Clone()
        {
            return new QueryTakeNode
            {
                Source = Source.Clone() as ExpressionNode ?? new LiteralNode(),
                Selection = Selection.Clone() as SelectionTerm ?? new SelectionTerm(),
                DataType = DataType,
                Location = Location,
                Metadata = new Dictionary<string, object>(Metadata)
            };
        }

        public override string ToString()
        {
            return $"{Source}.take({Selection})";
        }
    }
}
