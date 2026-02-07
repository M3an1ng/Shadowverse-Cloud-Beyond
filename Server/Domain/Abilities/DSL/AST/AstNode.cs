using System;
using System.Collections.Generic;
using System.Linq;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.AST
{
    /// <summary>
    /// AST节点基类
    /// 所有AST节点的基础接口
    /// </summary>
    public abstract class AstNode
    {
        /// <summary>
        /// 节点类型
        /// </summary>
        public abstract string NodeType { get; }

        /// <summary>
        /// 源代码位置信息
        /// </summary>
        public SourceLocation? Location { get; set; }

        /// <summary>
        /// 节点元数据
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// 父节点
        /// </summary>
        public AstNode? Parent { get; set; }

        /// <summary>
        /// 子节点列表
        /// </summary>
        public virtual List<AstNode> Children => new();

        /// <summary>
        /// 接受访问者模式
        /// </summary>
        /// <param name="visitor">访问者</param>
        /// <returns>访问结果</returns>
        public abstract T Accept<T>(IAstVisitor<T> visitor);

        /// <summary>
        /// 验证节点
        /// </summary>
        /// <returns>验证结果</returns>
        public virtual ValidationResult Validate()
        {
            var result = new ValidationResult();

            // 验证子节点
            foreach (var child in Children)
            {
                var childResult = child.Validate();
                result.Errors.AddRange(childResult.Errors);
                result.Warnings.AddRange(childResult.Warnings);
            }

            return result;
        }

        /// <summary>
        /// 克隆节点
        /// </summary>
        /// <returns>克隆的节点</returns>
        public abstract AstNode Clone();

        /// <summary>
        /// 转换为字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString()
        {
            return $"{NodeType}";
        }
    }

    /// <summary>
    /// 源代码位置信息
    /// </summary>
    public class SourceLocation
    {
        /// <summary>
        /// 起始行号
        /// </summary>
        public int StartLine { get; set; }

        /// <summary>
        /// 起始列号
        /// </summary>
        public int StartColumn { get; set; }

        /// <summary>
        /// 结束行号
        /// </summary>
        public int EndLine { get; set; }

        /// <summary>
        /// 结束列号
        /// </summary>
        public int EndColumn { get; set; }

        /// <summary>
        /// 源文件名
        /// </summary>
        public string? FileName { get; set; }

        public override string ToString()
        {
            return $"{FileName}({StartLine},{StartColumn})-({EndLine},{EndColumn})";
        }
    }

    /// <summary>
    /// AST访问者接口
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    public interface IAstVisitor<T>
    {
        T VisitAbility(AbilityNode node);
        T VisitTrigger(TriggerNode node);
        T VisitCondition(ConditionNode node);
        T VisitTarget(TargetNode node);
        T VisitEffect(EffectNode node);
        T VisitProperty(PropertyNode node);
        T VisitFilter(FilterNode node);
        T VisitParameter(ParameterNode node);
        T VisitExpression(ExpressionNode node);
        T VisitLiteral(LiteralNode node);
        T VisitIdentifier(IdentifierNode node);
        T VisitBinaryOperation(BinaryOperationNode node);
        T VisitUnaryOperation(UnaryOperationNode node);
        T VisitParenthesizedExpression(ParenthesizedExpressionNode node);
        T VisitFunctionCall(FunctionCallNode node);
        T VisitBlock(BlockNode node);
        T VisitConditionalExpression(ConditionalExpressionNode node);
        T VisitArrayAccess(ArrayAccessNode node);
        T VisitMemberAccess(MemberAccessNode node);
    }

    /// <summary>
    /// 基础AST访问者实现
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    public abstract class BaseAstVisitor<T> : IAstVisitor<T>
    {
        public virtual T VisitAbility(AbilityNode node) => DefaultVisit(node);
        public virtual T VisitTrigger(TriggerNode node) => DefaultVisit(node);
        public virtual T VisitCondition(ConditionNode node) => DefaultVisit(node);
        public virtual T VisitTarget(TargetNode node) => DefaultVisit(node);
        public virtual T VisitEffect(EffectNode node) => DefaultVisit(node);
        public virtual T VisitProperty(PropertyNode node) => DefaultVisit(node);
        public virtual T VisitFilter(FilterNode node) => DefaultVisit(node);
        public virtual T VisitParameter(ParameterNode node) => DefaultVisit(node);
        public virtual T VisitExpression(ExpressionNode node) => DefaultVisit(node);
        public virtual T VisitLiteral(LiteralNode node) => DefaultVisit(node);
        public virtual T VisitIdentifier(IdentifierNode node) => DefaultVisit(node);
        public virtual T VisitBinaryOperation(BinaryOperationNode node) => DefaultVisit(node);
        public virtual T VisitUnaryOperation(UnaryOperationNode node) => DefaultVisit(node);
        public virtual T VisitParenthesizedExpression(ParenthesizedExpressionNode node) => DefaultVisit(node);
        public virtual T VisitFunctionCall(FunctionCallNode node) => DefaultVisit(node);
        public virtual T VisitBlock(BlockNode node) => DefaultVisit(node);
        public virtual T VisitConditionalExpression(ConditionalExpressionNode node) => DefaultVisit(node);
        public virtual T VisitArrayAccess(ArrayAccessNode node) => DefaultVisit(node);
        public virtual T VisitMemberAccess(MemberAccessNode node) => DefaultVisit(node);

        protected abstract T DefaultVisit(AstNode node);
    }

    /// <summary>
    /// 验证结果类（重用之前定义的）
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// 是否验证通过
        /// </summary>
        public bool IsValid => Errors.Count == 0;

        /// <summary>
        /// 错误列表
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// 警告列表
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// 添加错误
        /// </summary>
        /// <param name="error">错误信息</param>
        public void AddError(string error)
        {
            Errors.Add(error);
        }

        /// <summary>
        /// 添加警告
        /// </summary>
        /// <param name="warning">警告信息</param>
        public void AddWarning(string warning)
        {
            Warnings.Add(warning);
        }

        /// <summary>
        /// 合并其他验证结果
        /// </summary>
        /// <param name="other">其他验证结果</param>
        public void Merge(ValidationResult other)
        {
            Errors.AddRange(other.Errors);
            Warnings.AddRange(other.Warnings);
        }
    }

    /// <summary>
    /// AST工具类
    /// </summary>
    public static class AstUtils
    {
        /// <summary>
        /// 查找指定类型的子节点
        /// </summary>
        /// <typeparam name="T">节点类型</typeparam>
        /// <param name="node">父节点</param>
        /// <returns>子节点列表</returns>
        public static List<T> FindChildren<T>(AstNode node) where T : AstNode
        {
            var result = new List<T>();
            FindChildrenRecursive(node, result);
            return result;
        }

        private static void FindChildrenRecursive<T>(AstNode node, List<T> result) where T : AstNode
        {
            if (node is T target)
            {
                result.Add(target);
            }

            foreach (var child in node.Children)
            {
                FindChildrenRecursive(child, result);
            }
        }

        /// <summary>
        /// 查找第一个指定类型的子节点
        /// </summary>
        /// <typeparam name="T">节点类型</typeparam>
        /// <param name="node">父节点</param>
        /// <returns>子节点或null</returns>
        public static T? FindFirstChild<T>(AstNode node) where T : AstNode
        {
            if (node is T target)
            {
                return target;
            }

            foreach (var child in node.Children)
            {
                var result = FindFirstChild<T>(child);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        /// <summary>
        /// 设置父子关系
        /// </summary>
        /// <param name="parent">父节点</param>
        /// <param name="children">子节点列表</param>
        public static void SetParentChildRelationship(AstNode parent, params AstNode[] children)
        {
            foreach (var child in children)
            {
                child.Parent = parent;
            }
        }
    }
}
