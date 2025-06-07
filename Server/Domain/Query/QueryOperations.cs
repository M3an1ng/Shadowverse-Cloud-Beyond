using System.Collections.Generic;
using ShadowverseBeyond.Server.Domain.Abilities.Common; // For ValueConfig
namespace ShadowverseBeyond.Server.Domain.Query
{
    /// <summary>
    /// Marker interface for query pipeline operations.
    /// </summary>
    public interface IQueryOperation
    {
    }

    /// <summary>
    /// Represents a single filtering condition within a FilterOperation.
    /// </summary>
    /// <param name="ValueSource">The source of the value on the item being filtered.</param>
    /// <param name="TargetValue">The value to compare against.</param>
    /// <param name="Operator">The comparison operator.</param>
    public record FilterConfig(
        ValueConfig ValueSource,
        ValueConfig TargetValue,
        ComparisonOperator Operator = ComparisonOperator.EqualTo
    );

    /// <summary>
    /// Represents a single sorting criterion within a SortOperation.
    /// </summary>
    /// <param name="ValueSource">The source of the value to sort by.</param>
    /// <param name="Direction">The sorting direction.</param>
    public record OrderingConfig(
        ValueConfig ValueSource,
        OrderingDirection Direction = OrderingDirection.Ascending
    );

    /// <summary>
    /// Represents a filtering step in the query pipeline, containing one or more filter conditions.
    /// Assumes conditions are combined with logical AND.
    /// </summary>
    /// <param name="Filters">A list of filter conditions to apply.</param>
    public record FilterOperation(
        List<FilterConfig> Filters
    ) : IQueryOperation;

    /// <summary>
    /// Represents a sorting step in the query pipeline, containing one or more sorting criteria.
    /// </summary>
    /// <param name="Ordering">A list of ordering criteria to apply sequentially.</param>
    public record SortOperation(
        List<OrderingConfig> Ordering
    ) : IQueryOperation;

    /// <summary>
    /// Represents a selection/limit step in the query pipeline.
    /// </summary>
    /// <param name="Strategy">The selection strategy to use.</param>
    /// <param name="CountSource">The source defining how many items to select.</param>
    public record SelectionOperation(
        SelectionStrategy Strategy = SelectionStrategy.First,
        ValueConfig? CountSource = null // Use ValueConfig for dynamic count
    ) : IQueryOperation;

    /// <summary>
    /// Defines the types of post-processing available.
    /// </summary>
    public enum PostProcessingType
    {
        /// <summary>
        /// No post-processing.
        /// </summary>
        None,
        /// <summary>
        /// Converts items to CardDefinitionDto.
        /// </summary>
        ToCardDefinitionDto,
        /// <summary>
        /// Converts items to CardInstanceDto.
        /// </summary>
        ToCardInstanceDto,
        // Add other specific post-processing types as needed
    }

    /// <summary>
    /// Represents a post-processing step in the query pipeline.
    /// </summary>
    /// <param name="Type">The type of post-processing to apply.</param>
    /// <param name="Parameters">Optional parameters for the post-processing step.</param>
    public record PostProcessingOperation(
        PostProcessingType Type,
        Dictionary<string, object>? Parameters = null // Corrected generic syntax
    ) : IQueryOperation;
}