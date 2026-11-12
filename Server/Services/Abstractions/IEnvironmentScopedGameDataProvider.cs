using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Services.Abstractions
{
    /// <summary>
    /// Provides environment-scoped card definition pools for DSL and query execution.
    /// </summary>
    public interface IEnvironmentScopedGameDataProvider
    {
        IReadOnlyCollection<CardDefinition> GetCardDefinitionsForEnvironment(DeckEnvironment environment);
        IReadOnlyCollection<CardDefinition> GetMirageCardDefinitions() => System.Array.Empty<CardDefinition>();
        IReadOnlyCollection<CardDefinition> GetMirageLeaderDefinitions() => System.Array.Empty<CardDefinition>();
    }
}
