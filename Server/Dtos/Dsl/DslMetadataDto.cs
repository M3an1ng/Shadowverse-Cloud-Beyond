using System;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Dtos.Dsl
{
    /// <summary>
    /// The root DTO for all DSL metadata exposed to the UI editor.
    /// It contains lists of all available context objects and functions.
    /// </summary>
    public class DslMetadataDto
    {
        public List<DslContextObjectDto> ContextObjects { get; set; } = new List<DslContextObjectDto>();
        public List<DslFunctionDto> Functions { get; set; } = new List<DslFunctionDto>();
        public Dictionary<string, DslTypeDto> TypeMetadata { get; set; } = new Dictionary<string, DslTypeDto>();
    }

    /// <summary>
    /// DTO for an exposable context object (e.g., "Player", "Self").
    /// </summary>
    public class DslContextObjectDto
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public List<DslPropertyDto> Properties { get; set; } = new List<DslPropertyDto>();
    }

    /// <summary>
    /// DTO for an exposable property of a context object.
    /// </summary>
    public class DslPropertyDto
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for type metadata, containing all properties of a specific type.
    /// </summary>
    public class DslTypeDto
    {
        public string TypeName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<DslPropertyDto> Properties { get; set; } = new List<DslPropertyDto>();
    }

    /// <summary>
    /// DTO for a registered DSL function (e.g., "if", "count").
    /// </summary>
    public class DslFunctionDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ReturnTypeName { get; set; } = string.Empty;
        public List<DslParameterDto> Parameters { get; set; } = new List<DslParameterDto>();
    }

    /// <summary>
    /// DTO for a parameter of a DSL function.
    /// </summary>
    public class DslParameterDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public bool IsOptional { get; set; }
    }
}