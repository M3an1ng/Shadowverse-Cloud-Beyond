using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging; // Added for logging
using ShadowverseBeyond.Server.Domain.Query; // Added for ICardQueryExecutor, QueryContext, CardQueryResult
using ShadowverseBeyond.Server.Dtos; // Added for CardInstanceDto

namespace ShadowverseBeyond.Server.Domain.Abilities.Targets.TargetSelectors
{
    /// <summary>
    /// A target selector that uses a CardQueryConfig to filter and select targets.
    /// This is the primary selector used when the TargetConfig specifies filtering, ordering, or limiting logic.
    /// </summary>
    public class FilteredTargetSelector : ITargetSelector
    {
        private readonly ICardQueryExecutor _queryExecutor;
        private readonly ILogger<FilteredTargetSelector> _logger;

        public FilteredTargetSelector(ICardQueryExecutor queryExecutor, ILogger<FilteredTargetSelector> logger)
        {
            _queryExecutor = queryExecutor ?? throw new ArgumentNullException(nameof(queryExecutor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // Removed constructor overload taking TargetConfig.
        // The config is provided via the SelectTargetsAsync method.
        // Dependencies should be injected via the primary constructor.

        /// <summary>
        /// Asynchronously selects targets by executing the CardQueryConfig defined in the TargetConfig.
        /// </summary>
        /// <param name="config">The target configuration containing the query.</param>
        /// <param name="context">The runtime ability context.</param>
        /// <returns>A list of selected TargetIds.</returns>
        public async Task<List<TargetId>> SelectTargetsAsync(TargetConfig config, AbilityContext context)
        {
            if (config == null)
            {
                _logger.LogError("TargetConfig cannot be null.");
                return new List<TargetId>();
            }
            if (context == null)
            {
                _logger.LogError("AbilityContext cannot be null.");
                return new List<TargetId>();
            }
            if (context.CurrentGameState == null)
            {
                _logger.LogError("AbilityContext.CurrentGameState cannot be null for FilteredTargetSelector.");
                // Or handle based on config.OnFailure? For now, return empty.
                return new List<TargetId>();
            }


            // 1. Create QueryContext from AbilityContext
            var queryContext = new QueryContext
            {
                RequestingPlayerId = context.RequestingPlayerId.ToString(), // Assuming RequestingPlayerId in AbilityContext is int, convert if needed
                CurrentGameState = context.CurrentGameState, // Pass the GameStateDto
                TriggeringEvent = context.TriggeringEvent
                // Add TriggeringCardInstanceId if QueryContext supports it and it's available in AbilityContext
                // TriggeringCardInstanceId = context.SourceCard?.InstanceId
            };

            // 2. Execute the query using ICardQueryExecutor
            CardQueryResult queryResult;
            try
            {
                _logger.LogDebug("Executing card query for target selection. SourcePool: {SourcePool}", config.Query?.SourcePoolExpression);
                queryResult = await _queryExecutor.ExecuteQueryAsync(config.Query, queryContext);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing card query during target selection.");
                // Handle based on config.OnFailure?
                return new List<TargetId>(); // Return empty on execution error
            }

            // 3. Process the result
            if (!queryResult.Success)
            {
                _logger.LogWarning("Card query execution failed: {ErrorMessage}", queryResult.ErrorMessage);
                // Handle based on config.OnFailure?
                return new List<TargetId>();
            }

            // Assuming the query returns CardInstanceDto objects when targeting instances
            // Adjust the type parameter if the query can return CardDefinitionDto
            var selectedInstances = queryResult.GetResultsAs<CardInstanceDto>();

            if (!selectedInstances.Any())
            {
                _logger.LogDebug("Query returned no matching targets.");
                // Handle based on config.OnFailure?
                return new List<TargetId>();
            }

            // 4. Map results to TargetId list
            var targetIds = selectedInstances
                .Select(instance => TargetId.Card(instance.InstanceId.ToString())) // Use static factory method
                .ToList();

            _logger.LogDebug("Selected {Count} targets.", targetIds.Count);

            // TODO: Implement PlayerChoice logic if config.SelectorType is PlayerChoice
            // This selector might be reused by PlayerChoiceTargetSelector, or PlayerChoice logic
            // might happen *after* this initial filtering step.
            // If this selector *is* the one handling PlayerChoice, we'd need interaction here.
            // Based on the factory, PlayerChoiceTargetSelector exists, so this one likely just filters.

            return targetIds;
        }
    }
}