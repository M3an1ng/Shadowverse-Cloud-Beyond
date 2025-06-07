using System.Collections.Generic;
using System.Linq;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.AST
{
    /// <summary>
    /// 能力节点
    /// 表示完整的能力定义
    /// </summary>
    public class AbilityNode : AstNode
    {
        public override string NodeType => "Ability";

        /// <summary>
        /// 能力名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 能力标签
        /// </summary>
        public string Tag { get; set; } = string.Empty;

        /// <summary>
        /// 能力类型
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// 触发器节点
        /// </summary>
        public TriggerNode? Trigger { get; set; }

        /// <summary>
        /// 条件节点列表
        /// </summary>
        public List<ConditionNode> Conditions { get; set; } = new();

        /// <summary>
        /// 目标节点列表
        /// </summary>
        public List<TargetNode> Targets { get; set; } = new();

        /// <summary>
        /// 效果节点列表
        /// </summary>
        public List<EffectNode> Effects { get; set; } = new();

        /// <summary>
        /// 属性节点列表
        /// </summary>
        public List<PropertyNode> Properties { get; set; } = new();

        public override List<AstNode> Children
        {
            get
            {
                var children = new List<AstNode>();
                if (Trigger != null) children.Add(Trigger);
                children.AddRange(Conditions);
                children.AddRange(Targets);
                children.AddRange(Effects);
                children.AddRange(Properties);
                return children;
            }
        }

        public override T Accept<T>(IAstVisitor<T> visitor)
        {
            return visitor.VisitAbility(this);
        }

        public override AstNode Clone()
        {
            return new AbilityNode
            {
                Name = this.Name,
                Tag = this.Tag,
                Type = this.Type,
                Trigger = this.Trigger?.Clone() as TriggerNode,
                Conditions = this.Conditions.Select(c => c.Clone() as ConditionNode).ToList()!,
                Targets = this.Targets.Select(t => t.Clone() as TargetNode).ToList()!,
                Effects = this.Effects.Select(e => e.Clone() as EffectNode).ToList()!,
                Properties = this.Properties.Select(p => p.Clone() as PropertyNode).ToList()!,
                Location = this.Location,
                Metadata = new Dictionary<string, object>(this.Metadata)
            };
        }
    }

    /// <summary>
    /// 触发器节点
    /// 表示能力的触发条件
    /// </summary>
    public class TriggerNode : AstNode
    {
        public override string NodeType => "Trigger";

        /// <summary>
        /// 触发事件类型
        /// </summary>
        public List<string> EventTypes { get; set; } = new();

        /// <summary>
        /// 触发范围
        /// </summary>
        public string Scope { get; set; } = "self";

        /// <summary>
        /// 触发时序
        /// </summary>
        public string Timing { get; set; } = "after";

        /// <summary>
        /// 触发条件表达式
        /// </summary>
        public ExpressionNode? Condition { get; set; }

        /// <summary>
        /// 是否可选
        /// </summary>
        public bool IsOptional { get; set; } = false;

        public override List<AstNode> Children
        {
            get
            {
                var children = new List<AstNode>();
                if (Condition != null) children.Add(Condition);
                return children;
            }
        }

        public override T Accept<T>(IAstVisitor<T> visitor)
        {
            return visitor.VisitTrigger(this);
        }

        public override AstNode Clone()
        {
            return new TriggerNode
            {
                EventTypes = new List<string>(this.EventTypes),
                Scope = this.Scope,
                Timing = this.Timing,
                Condition = this.Condition?.Clone() as ExpressionNode,
                IsOptional = this.IsOptional,
                Location = this.Location,
                Metadata = new Dictionary<string, object>(this.Metadata)
            };
        }
    }

    /// <summary>
    /// 条件节点
    /// 表示能力的执行条件
    /// </summary>
    public class ConditionNode : AstNode
    {
        public override string NodeType => "Condition";

        /// <summary>
        /// 条件表达式
        /// </summary>
        public ExpressionNode Expression { get; set; } = new LiteralNode { Value = true };

        /// <summary>
        /// 逻辑操作符（与下一个条件的关系）
        /// </summary>
        public string LogicOperator { get; set; } = "and";

        /// <summary>
        /// 是否取反
        /// </summary>
        public bool Negate { get; set; } = false;

        public override List<AstNode> Children => new() { Expression };

        public override T Accept<T>(IAstVisitor<T> visitor)
        {
            return visitor.VisitCondition(this);
        }

        public override AstNode Clone()
        {
            return new ConditionNode
            {
                Expression = this.Expression.Clone() as ExpressionNode ?? new LiteralNode { Value = true },
                LogicOperator = this.LogicOperator,
                Negate = this.Negate,
                Location = this.Location,
                Metadata = new Dictionary<string, object>(this.Metadata)
            };
        }
    }

    /// <summary>
    /// 目标节点
    /// 表示能力的目标选择
    /// </summary>
    public class TargetNode : AstNode
    {
        public override string NodeType => "Target";

        /// <summary>
        /// 目标类型
        /// </summary>
        public string Type { get; set; } = "self";

        /// <summary>
        /// 目标范围
        /// </summary>
        public string Scope { get; set; } = "field";

        /// <summary>
        /// 选择方法
        /// </summary>
        public string Selection { get; set; } = "all";

        /// <summary>
        /// 选择数量表达式
        /// </summary>
        public ExpressionNode Count { get; set; } = new LiteralNode { Value = 1 };

        /// <summary>
        /// 过滤条件列表
        /// </summary>
        public List<FilterNode> Filters { get; set; } = new();

        /// <summary>
        /// 是否需要玩家选择
        /// </summary>
        public bool RequiresPlayerChoice { get; set; } = false;

        public override List<AstNode> Children
        {
            get
            {
                var children = new List<AstNode> { Count };
                children.AddRange(Filters);
                return children;
            }
        }

        public override T Accept<T>(IAstVisitor<T> visitor)
        {
            return visitor.VisitTarget(this);
        }

        public override AstNode Clone()
        {
            return new TargetNode
            {
                Type = this.Type,
                Scope = this.Scope,
                Selection = this.Selection,
                Count = this.Count.Clone() as ExpressionNode ?? new LiteralNode { Value = 1 },
                Filters = this.Filters.Select(f => f.Clone() as FilterNode).ToList()!,
                RequiresPlayerChoice = this.RequiresPlayerChoice,
                Location = this.Location,
                Metadata = new Dictionary<string, object>(this.Metadata)
            };
        }
    }

    /// <summary>
    /// 效果节点
    /// 表示能力的具体效果
    /// </summary>
    public class EffectNode : AstNode
    {
        public override string NodeType => "Effect";

        /// <summary>
        /// 效果类型
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// 效果目标
        /// </summary>
        public TargetNode? Target { get; set; }

        /// <summary>
        /// 效果数值表达式
        /// </summary>
        public ExpressionNode? Amount { get; set; }

        /// <summary>
        /// 效果参数列表
        /// </summary>
        public List<ParameterNode> Parameters { get; set; } = new();

        /// <summary>
        /// 子效果列表（用于复合效果）
        /// </summary>
        public List<EffectNode> SubEffects { get; set; } = new();

        /// <summary>
        /// 执行顺序
        /// </summary>
        public int ExecutionOrder { get; set; } = 0;

        public override List<AstNode> Children
        {
            get
            {
                var children = new List<AstNode>();
                if (Target != null) children.Add(Target);
                if (Amount != null) children.Add(Amount);
                children.AddRange(Parameters);
                children.AddRange(SubEffects);
                return children;
            }
        }

        public override T Accept<T>(IAstVisitor<T> visitor)
        {
            return visitor.VisitEffect(this);
        }

        public override AstNode Clone()
        {
            return new EffectNode
            {
                Type = this.Type,
                Target = this.Target?.Clone() as TargetNode,
                Amount = this.Amount?.Clone() as ExpressionNode,
                Parameters = this.Parameters.Select(p => p.Clone() as ParameterNode).ToList()!,
                SubEffects = this.SubEffects.Select(e => e.Clone() as EffectNode).ToList()!,
                ExecutionOrder = this.ExecutionOrder,
                Location = this.Location,
                Metadata = new Dictionary<string, object>(this.Metadata)
            };
        }
    }

    /// <summary>
    /// 属性节点
    /// 表示能力的属性设置
    /// </summary>
    public class PropertyNode : AstNode
    {
        public override string NodeType => "Property";

        /// <summary>
        /// 属性名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 属性值表达式
        /// </summary>
        public ExpressionNode Value { get; set; } = new LiteralNode { Value = null };

        public override List<AstNode> Children => new() { Value };

        public override T Accept<T>(IAstVisitor<T> visitor)
        {
            return visitor.VisitProperty(this);
        }

        public override AstNode Clone()
        {
            return new PropertyNode
            {
                Name = this.Name,
                Value = this.Value.Clone() as ExpressionNode ?? new LiteralNode { Value = null },
                Location = this.Location,
                Metadata = new Dictionary<string, object>(this.Metadata)
            };
        }
    }

    /// <summary>
    /// 过滤器节点
    /// 表示目标过滤条件
    /// </summary>
    public class FilterNode : AstNode
    {
        public override string NodeType => "Filter";

        /// <summary>
        /// 过滤属性
        /// </summary>
        public string Property { get; set; } = string.Empty;

        /// <summary>
        /// 比较操作符
        /// </summary>
        public string Operator { get; set; } = "==";

        /// <summary>
        /// 比较值表达式
        /// </summary>
        public ExpressionNode Value { get; set; } = new LiteralNode { Value = null };

        /// <summary>
        /// 逻辑操作符
        /// </summary>
        public string LogicOperator { get; set; } = "and";

        public override List<AstNode> Children => new() { Value };

        public override T Accept<T>(IAstVisitor<T> visitor)
        {
            return visitor.VisitFilter(this);
        }

        public override AstNode Clone()
        {
            return new FilterNode
            {
                Property = this.Property,
                Operator = this.Operator,
                Value = this.Value.Clone() as ExpressionNode ?? new LiteralNode { Value = null },
                LogicOperator = this.LogicOperator,
                Location = this.Location,
                Metadata = new Dictionary<string, object>(this.Metadata)
            };
        }
    }

    /// <summary>
    /// 参数节点
    /// 表示效果参数
    /// </summary>
    public class ParameterNode : AstNode
    {
        public override string NodeType => "Parameter";

        /// <summary>
        /// 参数名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 参数值表达式
        /// </summary>
        public ExpressionNode Value { get; set; } = new LiteralNode { Value = null };

        public override List<AstNode> Children => new() { Value };

        public override T Accept<T>(IAstVisitor<T> visitor)
        {
            return visitor.VisitParameter(this);
        }

        public override AstNode Clone()
        {
            return new ParameterNode
            {
                Name = this.Name,
                Value = this.Value.Clone() as ExpressionNode ?? new LiteralNode { Value = null },
                Location = this.Location,
                Metadata = new Dictionary<string, object>(this.Metadata)
            };
        }
    }
}
