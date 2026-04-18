using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ComponentModel;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Metadata;
using ShadowverseBeyond.Server.Dtos.Dsl;
using ShadowverseBeyond.Server.Services.Abstractions;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Events.Metadata;
using ShadowverseBeyond.Server.Dtos.Enums;

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
                TypeMetadata = BuildAllTypeMetadata(), // 添加所有类型的元数据
                EnumTypes = BuildEnumMetadata()
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
                        ReturnTypeName = GetCleanTypeName(functionDef.ReturnType),
                        Parameters = functionDef.Parameters.Select(p => new DslParameterDto
                        {
                            Name = p.Name,
                            Description = p.Description,
                            TypeName = GetCleanTypeName(p.Type),
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
                            TypeName = GetCleanTypeName(prop.PropertyType)
                        });
                    }
                }
                contextObjectDtos.Add(contextObjectDto);
            }

            contextObjectDtos.Add(new DslContextObjectDto
            {
                Name = "db",
                DisplayName = "卡牌数据集",
                Description = "按构筑环境拆分后的静态卡牌定义集合",
                TypeName = "CardDefinitionDatabaseRoot",
                Properties = BuildEnvironmentScopedDatabaseProperties()
            });

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

            MergeEventParameterTypeMetadata(typeMetadata, assembly);

            typeMetadata["CardDefinitionDatabaseRoot"] = new DslTypeDto
            {
                TypeName = "CardDefinitionDatabaseRoot",
                DisplayName = "卡牌数据集",
                Description = "按构筑环境拆分后的静态卡牌定义集合",
                Properties = BuildEnvironmentScopedDatabaseProperties()
            };

            return typeMetadata;
        }

        private void MergeEventParameterTypeMetadata(Dictionary<string, DslTypeDto> typeMetadata, Assembly assembly)
        {
            MergeEventParameterDescriptor(typeMetadata, typeof(IGameEvent), typeof(GameEventBase));

            var eventTypes = assembly.GetTypes()
                .Where(t => typeof(IGameEvent).IsAssignableFrom(t) && t.IsClass)
                .ToList();

            foreach (var eventType in eventTypes)
            {
                MergeEventParameterDescriptor(typeMetadata, eventType, eventType);
            }
        }

        private void MergeEventParameterDescriptor(
            Dictionary<string, DslTypeDto> typeMetadata,
            Type metadataType,
            Type descriptorType)
        {
            var typeName = GetCleanTypeName(metadataType);
            if (!typeMetadata.TryGetValue(typeName, out var typeDto))
            {
                typeDto = new DslTypeDto
                {
                    TypeName = typeName,
                    DisplayName = metadataType.Name,
                    Description = string.Empty,
                    Properties = new List<DslPropertyDto>()
                };
                typeMetadata[typeName] = typeDto;
            }

            var existingPropertyNames = new HashSet<string>(
                typeDto.Properties.Select(prop => prop.Name),
                StringComparer.OrdinalIgnoreCase);

            var descriptor = EventParameterCache.GetDescriptor(descriptorType);
            foreach (var parameter in descriptor.Parameters)
            {
                var propertyName = ToDslPropertyName(parameter.PropertyName);
                if (!existingPropertyNames.Add(propertyName))
                {
                    continue;
                }

                typeDto.Properties.Add(new DslPropertyDto
                {
                    Name = propertyName,
                    DisplayName = parameter.DisplayName,
                    Description = parameter.Description,
                    TypeName = GetCleanTypeName(parameter.PropertyType)
                });
            }
        }

        private static List<DslPropertyDto> BuildEnvironmentScopedDatabaseProperties()
        {
            return DslEnvironmentCardPoolNames.GetAll()
                .Select(entry => new DslPropertyDto
                {
                    Name = entry.PropertyName,
                    DisplayName = $"{GetEnvironmentDisplayName(entry.Environment)}卡池",
                    Description = $"仅包含 {GetEnvironmentDisplayName(entry.Environment)} 环境可用的卡牌定义",
                    TypeName = "List<CardDefinition>"
                })
                .ToList();
        }

        private static string GetEnvironmentDisplayName(DeckEnvironment? environment)
        {
            if (!environment.HasValue)
            {
                return "当前";
            }

            var field = typeof(DeckEnvironment).GetField(environment.Value.ToString());
            var description = field?.GetCustomAttribute<DescriptionAttribute>()?.Description;
            return string.IsNullOrWhiteSpace(description) ? environment.Value.ToString() : description;
        }

        private List<DslEnumTypeDto> BuildEnumMetadata()
        {
            var enumTypes = new HashSet<Type>();
            enumTypes.Add(typeof(EventType));
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
                var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.GetCustomAttribute<DslExposableAttribute>() != null)
                    .ToList();
                if (typeof(GameEventBase).IsAssignableFrom(type))
                {
                    props = props
                        .Where(p => !baseEventFields.Contains(p.Name))
                        .ToList();
                }

                foreach (var prop in props)
                {
                    CollectEnumTypes(prop.PropertyType, enumTypes);
                }
            }

            var eventTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(IGameEvent).IsAssignableFrom(t))
                .ToList();
            foreach (var eventType in eventTypes)
            {
                var descriptor = EventParameterCache.GetDescriptor(eventType);
                foreach (var parameter in descriptor.Parameters)
                {
                    CollectEnumTypes(parameter.PropertyType, enumTypes);
                }
            }

            return enumTypes
                .OrderBy(t => t.Name)
                .Select(t => new DslEnumTypeDto
                {
                    Name = GetCleanTypeName(t),
                    Values = Enum.GetNames(t).ToList()
                })
                .ToList();
        }

        private static void CollectEnumTypes(Type? type, HashSet<Type> output)
        {
            if (type == null) return;

            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null)
            {
                CollectEnumTypes(underlying, output);
                return;
            }

            if (type.IsEnum)
            {
                output.Add(type);
                return;
            }

            if (type.IsArray)
            {
                CollectEnumTypes(type.GetElementType(), output);
                return;
            }

            if (!type.IsGenericType) return;
            foreach (var arg in type.GetGenericArguments())
            {
                CollectEnumTypes(arg, output);
            }
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
            return ToDslPropertyName(property.Name);
        }

        private static string ToDslPropertyName(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                return propertyName;
            }

            return char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1);
        }
    }
}
