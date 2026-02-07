using System.Collections.Generic;
using System.Text.Json.Serialization;
using ShadowverseBeyond.Server.Domain.Abilities.Common; // For ValueConfig
using ShadowverseBeyond.Server.Domain.Query.Metadata;

namespace ShadowverseBeyond.Server.Domain.Query
{
    /// <summary>
    /// Marker interface for query pipeline operations.
    /// This interface is configured for polymorphic deserialization using System.Text.Json.
    /// Each derived type is registered with a unique type discriminator, which corresponds
    /// to the value of the "$type" property in the JSON payload.
    /// </summary>
    [JsonDerivedType(typeof(FilterOperation), typeDiscriminator: "FilterOperation")]
    [JsonDerivedType(typeof(SortOperation), typeDiscriminator: "SortOperation")]
    [JsonDerivedType(typeof(SelectionOperation), typeDiscriminator: "SelectionOperation")]
    [JsonDerivedType(typeof(DistinctOperation), typeDiscriminator: "DistinctOperation")]
    [JsonDerivedType(typeof(SelectOperation), typeDiscriminator: "SelectOperation")]
    [JsonDerivedType(typeof(PostProcessingOperation), typeDiscriminator: "PostProcessingOperation")]
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
    [QueryOperationMetadata("Filter", "Apply a set of conditions to filter the card list.")]
    public record FilterOperation(
        List<FilterConfig> Filters
    ) : IQueryOperation;

    /// <summary>
    /// Represents a sorting step in the query pipeline, containing one or more sorting criteria.
    /// </summary>
    /// <param name="Ordering">A list of ordering criteria to apply sequentially.</param>
    [QueryOperationMetadata("Sort", "Sort the card list based on one or more criteria.")]
    public record SortOperation(
        List<OrderingConfig> Ordering
    ) : IQueryOperation;

    /// <summary>
    /// Represents a selection/limit step in the query pipeline.
    /// </summary>
    /// <param name="Strategy">The selection strategy to use.</param>
    /// <param name="CountSource">The source defining how many items to select.</param>
    [QueryOperationMetadata("Take", "Select a subset of the card list (e.g., first, last, random).")]
    public record SelectionOperation(
        SelectionStrategy Strategy = SelectionStrategy.First,
        ValueConfig? CountSource = null // Use ValueConfig for dynamic count
    ) : IQueryOperation;

    /// <summary>
    /// Represents a distinct step in the query pipeline.
    /// </summary>
    /// <param name="KeySelector">The key selector used to deduplicate items.</param>
    [QueryOperationMetadata("Distinct", "Deduplicate items based on a key expression.")]
    public record DistinctOperation(
        ValueConfig KeySelector
    ) : IQueryOperation;

    /// <summary>
    /// Represents a projection step in the query pipeline.
    /// </summary>
    /// <param name="Selector">The projection expression for each item.</param>
    [QueryOperationMetadata("Select", "Project each item in the list to a new value.")]
    public record SelectOperation(
        ValueConfig Selector
    ) : IQueryOperation;

    /// <summary>
    /// Represents a post-processing step in the query pipeline.
    /// </summary>
    /// <param name="Projection">The projection expression to apply on the final result set.</param>
    [QueryOperationMetadata("Post-process", "Apply a projection to the final result set.")]
    public record PostProcessingOperation(
        ValueConfig Projection
    ) : IQueryOperation;
}
