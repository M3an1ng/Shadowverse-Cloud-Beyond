using System;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Metadata
{
    /// <summary>
    /// DSL元数据的根对象，包含所有可用的上下文对象和函数
    /// </summary>
    public class DslMetadata
    {
        /// <summary>
        /// 顶级上下文对象（如player、opponent、self等）
        /// </summary>
        public Dictionary<string, DslObjectMetadata> TopLevelObjects { get; set; } = new();

        /// <summary>
        /// 可用的DSL函数
        /// </summary>
        public Dictionary<string, DslFunctionMetadata> Functions { get; set; } = new();

        /// <summary>
        /// 所有类型的元数据（用于属性类型解析）
        /// </summary>
        public Dictionary<string, DslObjectMetadata> Types { get; set; } = new();
    }

    /// <summary>
    /// DSL对象元数据，描述一个类型及其可访问的属性和方法
    /// </summary>
    public class DslObjectMetadata
    {
        /// <summary>
        /// 对象的显示名称
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 对象的描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// C#类型名称
        /// </summary>
        public string TypeName { get; set; } = string.Empty;

        /// <summary>
        /// 在DSL中的访问路径
        /// </summary>
        public string DslPath { get; set; } = string.Empty;

        /// <summary>
        /// 是否为顶级关键字
        /// </summary>
        public bool IsTopLevel { get; set; } = false;

        /// <summary>
        /// 可访问的属性
        /// </summary>
        public Dictionary<string, DslPropertyMetadata> Properties { get; set; } = new();

        /// <summary>
        /// 可访问的方法
        /// </summary>
        public Dictionary<string, DslMethodMetadata> Methods { get; set; } = new();
    }

    /// <summary>
    /// DSL属性元数据
    /// </summary>
    public class DslPropertyMetadata
    {
        /// <summary>
        /// 属性的显示名称
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 属性的描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 属性的C#类型名称
        /// </summary>
        public string TypeName { get; set; } = string.Empty;

        /// <summary>
        /// 在DSL中的访问名称
        /// </summary>
        public string DslName { get; set; } = string.Empty;

        /// <summary>
        /// 是否为集合类型
        /// </summary>
        public bool IsCollection { get; set; } = false;

        /// <summary>
        /// 如果是集合，集合元素的类型名称
        /// </summary>
        public string? ElementTypeName { get; set; }
    }

    /// <summary>
    /// DSL方法元数据
    /// </summary>
    public class DslMethodMetadata
    {
        /// <summary>
        /// 方法的显示名称
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 方法的描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 返回类型名称
        /// </summary>
        public string ReturnTypeName { get; set; } = string.Empty;

        /// <summary>
        /// 在DSL中的访问名称
        /// </summary>
        public string DslName { get; set; } = string.Empty;

        /// <summary>
        /// 方法参数
        /// </summary>
        public List<DslParameterMetadata> Parameters { get; set; } = new();
    }

    /// <summary>
    /// DSL函数元数据
    /// </summary>
    public class DslFunctionMetadata
    {
        /// <summary>
        /// 函数名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 函数的显示名称
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 函数的描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 返回类型名称
        /// </summary>
        public string ReturnTypeName { get; set; } = string.Empty;

        /// <summary>
        /// 函数参数
        /// </summary>
        public List<DslParameterMetadata> Parameters { get; set; } = new();
    }

    /// <summary>
    /// DSL参数元数据
    /// </summary>
    public class DslParameterMetadata
    {
        /// <summary>
        /// 参数名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 参数的显示名称
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 参数的描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 参数类型名称
        /// </summary>
        public string TypeName { get; set; } = string.Empty;

        /// <summary>
        /// 是否为可选参数
        /// </summary>
        public bool IsOptional { get; set; } = false;

        /// <summary>
        /// 默认值（序列化为字符串）
        /// </summary>
        public string? DefaultValue { get; set; }
    }
}
