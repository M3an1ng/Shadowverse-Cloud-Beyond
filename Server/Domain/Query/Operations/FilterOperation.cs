using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Entities; // Assuming CardInstance is here
using ShadowverseBeyond.Server.Domain.Query; // For FilterConfig and ComparisonOperator
using ShadowverseBeyond.Server.Domain.Abilities.Common; // For ValueConfig

namespace ShadowverseBeyond.Server.Domain.Query.Operations
{
    /// <summary>
    /// Query operation to filter a collection of cards based on specified criteria.
    /// </summary>
    public class FilterOperation : IQueryOperation
    {
        public List<FilterConfig> Filters { get; }

        public FilterOperation(List<FilterConfig> filters)
        {
            Filters = filters ?? new List<FilterConfig>();
        }

        public FilterOperation(FilterConfig singleFilter)
        {
            Filters = new List<FilterConfig> { singleFilter };
        }


        public async Task<IEnumerable<CardInstance>> ApplyAsync(IEnumerable<CardInstance> cards, QueryContext context)
        {
            if (!Filters.Any())
            {
                return cards; // No filters to apply
            }

            var filteredCards = cards;

            foreach (var filter in Filters)
            {
                // Apply each filter sequentially
                // Note: This assumes AND logic between filters. OR logic would require grouping.
                filteredCards = await ApplySingleFilterAsync(filteredCards, filter, context);
            }

            return filteredCards;
        }

        private async Task<IEnumerable<CardInstance>> ApplySingleFilterAsync(IEnumerable<CardInstance> cards, FilterConfig filter, QueryContext context)
        {
            var targetValue = await filter.TargetValue.ResolveAsync(context); // Resolve the target value first

            var result = new List<CardInstance>();
            foreach (var card in cards)
            {
                var sourceValue = await filter.ValueSource.ResolveAsync(context, card); // Resolve source value for each card

                if (CompareValues(sourceValue, targetValue, filter.Operator))
                {
                    result.Add(card);
                }
            }
            // Using ToList() to execute the filtering immediately before the next filter potentially modifies the context used by ResolveAsync
            return result.ToList();
        }


        // TODO: Move this comparison logic to a shared utility or extension method
        private bool CompareValues(object? sourceValue, object? targetValue, ComparisonOperator op)
        {
            // Handle nulls - adjust logic as needed (e.g., should null == null be true?)
            if (sourceValue == null || targetValue == null)
            {
                return op == ComparisonOperator.EqualTo ? sourceValue == targetValue :
                       op == ComparisonOperator.NotEqualTo ? sourceValue != targetValue :
                       false; // Most comparisons are false if one operand is null
            }

            // Handle numeric comparisons (assuming values can be converted)
            if (TryConvertToDecimal(sourceValue, out var sourceDecimal) && TryConvertToDecimal(targetValue, out var targetDecimal))
            {
                return op switch
                {
                    ComparisonOperator.EqualTo => sourceDecimal == targetDecimal,
                    ComparisonOperator.NotEqualTo => sourceDecimal != targetDecimal,
                    ComparisonOperator.GreaterThan => sourceDecimal > targetDecimal,
                    ComparisonOperator.GreaterThanOrEqualTo => sourceDecimal >= targetDecimal,
                    ComparisonOperator.LessThan => sourceDecimal < targetDecimal,
                    ComparisonOperator.LessThanOrEqualTo => sourceDecimal <= targetDecimal,
                    _ => false // Contains/NotContains not applicable to decimals here
                };
            }

            // Handle string comparisons
            var sourceString = sourceValue.ToString();
            var targetString = targetValue.ToString();
            if (sourceString != null && targetString != null)
            {
                return op switch
                {
                    ComparisonOperator.EqualTo => sourceString.Equals(targetString, StringComparison.OrdinalIgnoreCase), // Case-insensitive?
                    ComparisonOperator.NotEqualTo => !sourceString.Equals(targetString, StringComparison.OrdinalIgnoreCase),
                    ComparisonOperator.Contains => sourceString.Contains(targetString, StringComparison.OrdinalIgnoreCase),
                    ComparisonOperator.NotContains => !sourceString.Contains(targetString, StringComparison.OrdinalIgnoreCase),
                    // Other string comparisons? StartsWith, EndsWith?
                    _ => false
                };
            }

            // Handle boolean comparisons
            if (sourceValue is bool sourceBool && targetValue is bool targetBool)
            {
                return op switch
                {
                    ComparisonOperator.EqualTo => sourceBool == targetBool,
                    ComparisonOperator.NotEqualTo => sourceBool != targetBool,
                    _ => false
                };
            }

            // Handle collection Contains/NotContains (assuming sourceValue is a collection)
            if (op == ComparisonOperator.Contains || op == ComparisonOperator.NotContains)
            {
                if (sourceValue is System.Collections.IEnumerable enumerable)
                {
                    bool contains = false;
                    foreach (var item in enumerable)
                    {
                        // Need a way to compare item with targetValue (potentially recursive or type-specific)
                        if (item != null && item.Equals(targetValue)) // Simple equality check
                        {
                            contains = true;
                            break;
                        }
                    }
                    return op == ComparisonOperator.Contains ? contains : !contains;
                }
            }


            // Default comparison (e.g., for enums or other types)
            try
            {
                return op switch
                {
                    ComparisonOperator.EqualTo => sourceValue.Equals(targetValue),
                    ComparisonOperator.NotEqualTo => !sourceValue.Equals(targetValue),
                    _ => false // Other operators might not make sense for arbitrary objects
                };
            }
            catch
            {
                return false; // Comparison failed
            }
        }

        private bool TryConvertToDecimal(object? value, out decimal result)
        {
            result = 0;
            if (value == null) return false;
            try
            {
                result = Convert.ToDecimal(value);
                return true;
            }
            catch (FormatException) { return false; }
            catch (InvalidCastException) { return false; }
            catch (OverflowException) { return false; }
        }
    }
}