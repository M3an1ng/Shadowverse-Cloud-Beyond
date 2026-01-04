using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Metadata;
using ShadowverseBeyond.Server.Dtos.Dsl;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Services
{
    public class DslMetadataService : IDslMetadataService
    {
        private readonly DslMetadataDto _cachedMetadata;

        public DslMetadataService(IFunctionRegistry functionRegistry)
        {
            _cachedMetadata = BuildMetadata(functionRegistry);
        }

        public DslMetadataDto GetDslMetadata()
        {
            return _cachedMetadata;
        }

        private DslMetadataDto BuildMetadata(IFunctionRegistry functionRegistry)
        {
            var metadata = new DslMetadataDto
            {
                Functions = BuildFunctionMetadata(functionRegistry),
                ContextObjects = BuildContextObjectMetadata(),
                TypeMetadata = BuildAllTypeMetadata() // 添加所有类型的元数据
            };
            return metadata;
        }

        private List<DslFunctionDto> BuildFunctionMetadata(IFunctionRegistry functionRegistry)
        {
            var functionDtos = new List<DslFunctionDto>();
            var functionNames = functionRegistry.GetRegisteredFunctionNames();

            foreach (var name in functionNames)
            {
                var functionDef = functionRegistry.GetFunction(name);
                if (functionDef != null)
                {
                    // Event parameter access functions are an internal mechanism and should not be surfaced to designers/players in the UI.
                    // They can still be used when loading existing DSL strings.
                    if (string.Equals(functionDef.Name, "eventInt", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(functionDef.Name, "eventBool", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(functionDef.Name, "eventString", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(functionDef.Name, "eventGuid", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    functionDtos.Add(new DslFunctionDto
                    {
                        Name = functionDef.Name,
                        Description = functionDef.Description,
                        ReturnTypeName = functionDef.ReturnType.Name,
                        Parameters = functionDef.Parameters.Select(p => new DslParameterDto
                        {
                            Name = p.Name,
                            Description = p.Description,
                            TypeName = p.Type.Name,
                            IsOptional = p.IsOptional
                        }).ToList()
                    });
                }
            }
            return functionDtos;
        }

        private List<DslContextObjectDto> BuildContextObjectMetadata()
        {
            var contextObjectDtos = new List<DslContextObjectDto>();
            var assembly = Assembly.GetExecutingAssembly();

            // 只返回GameContext作为根对象
            var gameContextType = typeof(GameContext);
            var gameContextAttr = gameContextType.GetCustomAttribute<DslExposableAttribute>();

            if (gameContextAttr != null)
            {
                var contextObjectDto = new DslContextObjectDto
                {
                    Name = "context",
                    DisplayName = gameContextAttr.DisplayName,
                    Description = gameContextAttr.Description,
                    TypeName = gameContextType.Name,
                    Properties = new List<DslPropertyDto>()
                };

                var exposableProperties = gameContextType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.GetCustomAttribute<DslExposableAttribute>() != null)
                    .ToList();

                foreach (var prop in exposableProperties)
                {
                    var propAttr = prop.GetCustomAttribute<DslExposableAttribute>();
                    if (propAttr != null)
                    {
                        contextObjectDto.Properties.Add(new DslPropertyDto
                        {
                            Name = GetDslPropertyName(prop),
                            DisplayName = propAttr.DisplayName,
                            Description = propAttr.Description,
                            TypeName = prop.PropertyType.Name
                        });
                    }
                }
                contextObjectDtos.Add(contextObjectDto);
            }

            return contextObjectDtos;
        }

        /// <summary>
        /// 构建所有DslExposable类型的元数据，用于类型推断和属性解析
        /// </summary>
        private Dictionary<string, DslTypeDto> BuildAllTypeMetadata()
        {
            var typeMetadata = new Dictionary<string, DslTypeDto>();
            var assembly = Assembly.GetExecutingAssembly();
            var exposableTypes = assembly.GetTypes()
                .Where(t => t.IsClass && t.GetCustomAttribute<DslExposableAttribute>() != null)
                .ToList();
            var baseEventFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "EventType",
                "EventId",
                "Timestamp",
                "Source",
                "Target"
            };

            foreach (var type in exposableTypes)
            {
                var classAttr = type.GetCustomAttribute<DslExposableAttribute>();
                if (classAttr != null)
                {
                    var typeDto = new DslTypeDto
                    {
                        TypeName = type.Name,
                        DisplayName = classAttr.DisplayName,
                        Description = classAttr.Description,
                        Properties = new List<DslPropertyDto>()
                    };

                    var exposableProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Where(p => p.GetCustomAttribute<DslExposableAttribute>() != null)
                        .ToList();
                    if (typeof(GameEventBase).IsAssignableFrom(type))
                    {
                        exposableProperties = exposableProperties
                            .Where(p => !baseEventFields.Contains(p.Name))
                            .ToList();
                    }

                    foreach (var prop in exposableProperties)
                    {
                        var propAttr = prop.GetCustomAttribute<DslExposableAttribute>();
                        if (propAttr != null)
                        {
                            typeDto.Properties.Add(new DslPropertyDto
                            {
                                Name = GetDslPropertyName(prop),
                                DisplayName = propAttr.DisplayName,
                                Description = propAttr.Description,
                                TypeName = GetCleanTypeName(prop.PropertyType)
                            });
                        }
                    }

                    typeMetadata[type.Name] = typeDto;
                }
            }

            return typeMetadata;
        }

        private string GetCleanTypeName(Type type) => TypeNameHelper.GetCleanTypeName(type);

        private string GetDslPath(Type type)
        {
            if (type == typeof(PlayerState))
                return "player";
            if (type == typeof(CardInstance))
                return "self";
            if (type.Name.Contains("GameState"))
                return "game";

            return type.Name.ToLowerInvariant();
        }

        private string GetDslPropertyName(PropertyInfo property)
        {
            return char.ToLowerInvariant(property.Name[0]) + property.Name.Substring(1);
        }
    }
}
