using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging; // Added for logging
using ShadowverseBeyond.Server.Domain.Query; // Added for ICardQueryExecutor, QueryContext, CardQueryResult
using ShadowverseBeyond.Server.Dtos; // Added for CardInstanceDto
using ShadowverseBeyond.Server.Domain.Events; // Added for GameContext

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
        /// <param name="context">The unified game context.</param>
        /// <returns>A list of selected TargetIds.</returns>
        public async Task<List<TargetId>> SelectTargetsAsync(TargetConfig config, GameContext context)
        {
            if (config == null)
            {
                _logger.LogError("TargetConfig cannot be null.");
                return new List<TargetId>();
            }
            if (context == null)
            {
                _logger.LogError("GameContext cannot be null.");
                return new List<TargetId>();
            }
            if (context.CurrentGameState == null)
            {
                _logger.LogError("GameContext.CurrentGameState cannot be null for FilteredTargetSelector.");
                // Or handle based on config.OnFailure? For now, return empty.
                return new List<TargetId>();
            }


            // 1. Execute the query using ICardQueryExecutor with GameContext directly
            CardQueryResult queryResult;
            try
            {
                _logger.LogDebug("Executing card query for target selection. SourcePool: {SourcePool}", config.Query?.SourcePoolExpression);
                if (config.Query == null)
                {
                    _logger.LogError("TargetConfig.Query cannot be null for FilteredTargetSelector.");
                    return new List<TargetId>();
                }
                queryResult = await _queryExecutor.ExecuteQueryAsync(config.Query, context);
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

            // 4. Process results and map to TargetId list based on actual type
            var targetIds = new List<TargetId>();
            if (queryResult.Results == null || !queryResult.Results.Any())
            {
                _logger.LogDebug("Query returned no matching targets or results list was null.");
                // Handle based on config.OnFailure? (Consider logging or specific actions)
                // For now, return empty list as before.
            }
            else
            {
                _logger.LogDebug("Processing {ResultCount} items from query result.", queryResult.Results.Count);
                foreach (var item in queryResult.Results)
                {
                    if (item is PlayerStateDto player)
                    {
                        var targetId = TargetId.Player(player.PlayerId); // Create Player TargetId
                        targetIds.Add(targetId);
                        _logger.LogTrace("Mapped PlayerStateDto (PlayerId: {PlayerId}) to TargetId: {TargetId}", player.PlayerId, targetId);
                    }
                    else if (item is CardInstanceDto instance)
                    {
                        var targetId = TargetId.Card(instance.InstanceId); // Create Card TargetId
                        targetIds.Add(targetId);
                        _logger.LogTrace("Mapped CardInstanceDto (InstanceId: {InstanceId}) to TargetId: {TargetId}", instance.InstanceId, targetId);
                    }
                    // Add handling for CardDefinitionDto if necessary for target selection, though less common.
                    // else if (item is CardDefinitionDto definition) { ... }
                    else
                    {
                        _logger.LogWarning("Query result item of unexpected type '{ItemType}' encountered and ignored during target ID mapping.", item?.GetType().Name ?? "null");
                    }
                }
                _logger.LogDebug("Finished processing query results. Generated {Count} TargetIds.", targetIds.Count);
            }


            // Check if any targets were actually generated after processing
            if (!targetIds.Any())
            {
                _logger.LogDebug("No valid targets generated after processing query results.");
                // Handle based on config.OnFailure?
                return new List<TargetId>(); // Return empty list if no valid targets were mapped
            }

            _logger.LogDebug("Selected {Count} targets in total.", targetIds.Count);

            // TODO: Implement PlayerChoice logic if config.SelectorType is PlayerChoice
            // This selector might be reused by PlayerChoiceTargetSelector, or PlayerChoice logic
            // might happen *after* this initial filtering step.
            // If this selector *is* the one handling PlayerChoice, we'd need interaction here.
            // Based on the factory, PlayerChoiceTargetSelector exists, so this one likely just filters.

            return targetIds;
        }
    }
}