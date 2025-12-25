using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Domain.Query.Metadata;
using ShadowverseBeyond.Server.Dtos.Query;
using ShadowverseBeyond.Server.Services.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ShadowverseBeyond.Server.Services
{
    public class QueryOperationMetadataService : IQueryOperationMetadataService
    {
        private readonly IReadOnlyList<QueryOperationMetadataDto> _cachedMetadata;

        public QueryOperationMetadataService()
        {
            _cachedMetadata = DiscoverQueryOperations();
        }

        public IEnumerable<QueryOperationMetadataDto> GetQueryOperationMetadata()
        {
            return _cachedMetadata;
        }

        private IReadOnlyList<QueryOperationMetadataDto> DiscoverQueryOperations()
        {
            var metadataList = new List<QueryOperationMetadataDto>();
            var assembly = Assembly.GetExecutingAssembly(); // Or specify the assembly where operations are defined

            var operationTypes = assembly.GetTypes()
                .Where(t => typeof(IQueryOperation).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract);

            foreach (var type in operationTypes)
            {
                var attribute = type.GetCustomAttribute<QueryOperationMetadataAttribute>();
                if (attribute != null)
                {
                    metadataList.Add(new QueryOperationMetadataDto
                    {
                        TypeName = type.FullName,
                        DisplayName = attribute.DisplayName,
                        Description = attribute.Description
                    });
                }
            }

            return metadataList;
        }
    }
}