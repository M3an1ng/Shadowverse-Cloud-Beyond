using System;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ShadowverseBeyond.Server.Domain.Abilities.Common;

namespace ShadowverseBeyond.Server.Domain.Query
{
    /// <summary>
    /// The concrete implementation for parsing query DSL strings.
    /// </summary>
    public class QueryDslParser : IQueryDslParser
    {
        // Regex to match .take(number) or .take(random 2) at the beginning of the string
        private static readonly Regex TakeRegex = new Regex(
            @"^\.take\(\s*(?:(?<strategy>first|last|random)\s*[ ,]+)?(?<count>\d+)\s*\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Regex to match .orderBy(...) at the beginning of the string
        private static readonly Regex OrderByRegex = new Regex(@"^\.orderBy\((?<expressions>.+?)\)", RegexOptions.Compiled);

        // Regex to match .query(...) at the beginning of the string
        private static readonly Regex QueryRegex = new Regex(@"^\.query\((?<expression>.*?)\)", RegexOptions.Compiled);

        /// <inheritdoc />
        public QueryConfig Parse(string dsl)
        {
            if (string.IsNullOrWhiteSpace(dsl))
            {
                throw new ArgumentException("DSL string cannot be null or whitespace.", nameof(dsl));
            }

            var (sourceExpression, operationsDsl) = SplitSourceAndOperations(dsl.Trim());

            if (string.IsNullOrWhiteSpace(sourceExpression))
            {
                throw new InvalidOperationException("DSL string is missing a source expression.");
            }

            var operations = ParseOperations(operationsDsl);

            return new QueryConfig
            {
                SourcePoolExpression = sourceExpression,
                Operations = operations
            };
        }

        private (string source, string operations) SplitSourceAndOperations(string dsl)
        {
            var match = Regex.Match(dsl, @"(\.query\(|\.orderBy\(|\.take\()");
            if (match.Success)
            {
                var index = match.Index;
                if (index == 0) return (string.Empty, dsl);
                return (dsl.Substring(0, index).Trim(), dsl.Substring(index).Trim());
            }
            return (dsl, string.Empty);
        }

        private List<IQueryOperation> ParseOperations(string operationsDsl)
        {
            var operations = new List<IQueryOperation>();
            var remainingDsl = operationsDsl;

            while (!string.IsNullOrEmpty(remainingDsl))
            {
                var originalLength = remainingDsl.Length;

                remainingDsl = ParseNextOperation(remainingDsl, operations);

                if (remainingDsl.Length == originalLength)
                {
                    throw new InvalidOperationException($"Failed to parse operation chain. Unrecognized segment: {remainingDsl}");
                }
            }

            return operations;
        }

        private string ParseNextOperation(string dsl, List<IQueryOperation> operations)
        {
            var match = TakeRegex.Match(dsl);
            if (match.Success)
            {
                var countStr = match.Groups["count"].Value;
                var strategyRaw = match.Groups["strategy"].Value;
                var strategy = SelectionStrategy.First;
                if (!string.IsNullOrWhiteSpace(strategyRaw))
                {
                    switch (strategyRaw.Trim().ToLowerInvariant())
                    {
                        case "last":
                            strategy = SelectionStrategy.Last;
                            break;
                        case "random":
                            strategy = SelectionStrategy.Random;
                            break;
                        default:
                            strategy = SelectionStrategy.First;
                            break;
                    }
                }
                if (int.TryParse(countStr, out var count))
                {
                    operations.Add(new SelectionOperation(strategy, new FormulaValueConfig { Formula = countStr }));
                    return dsl.Substring(match.Length).Trim();
                }
            }

            match = OrderByRegex.Match(dsl);
            if (match.Success)
            {
                var expressionsStr = match.Groups["expressions"].Value;
                var orderingConfigs = expressionsStr.Split(',')
                    .Select(s => s.Trim())
                    .Select(s =>
                    {
                        var parts = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        var direction = parts.Length > 1 && parts[1].ToLower() == "desc"
                            ? OrderingDirection.Descending
                            : OrderingDirection.Ascending;
                        return new OrderingConfig(new FormulaValueConfig { Formula = parts[0] }, direction);
                    })
                    .ToList();

                if (orderingConfigs.Any())
                {
                    operations.Add(new SortOperation(orderingConfigs));
                    return dsl.Substring(match.Length).Trim();
                }
            }

            match = QueryRegex.Match(dsl);
            if (match.Success)
            {
                var startIndex = match.Index + match.Value.IndexOf('(') + 1;
                var openParen = 1;
                var endIndex = -1;

                for (int i = startIndex; i < dsl.Length; i++)
                {
                    if (dsl[i] == '(')
                    {
                        openParen++;
                    }
                    else if (dsl[i] == ')')
                    {
                        openParen--;
                        if (openParen == 0)
                        {
                            endIndex = i;
                            break;
                        }
                    }
                }

                if (endIndex == -1)
                {
                    throw new InvalidOperationException("Mismatched parentheses in query expression.");
                }

                var expression = dsl.Substring(startIndex, endIndex - startIndex).Trim();
                var totalLength = endIndex + 1;

                if (!string.IsNullOrEmpty(expression))
                {
                    var filterConfig = new FilterConfig(
                        ValueSource: new FormulaValueConfig { Formula = expression },
                        TargetValue: new StaticValueConfig(),
                        Operator: ComparisonOperator.EqualTo
                    );
                    operations.Add(new FilterOperation(new List<FilterConfig> { filterConfig }));
                }
                return dsl.Substring(totalLength).Trim();
            }

            return dsl;
        }
    }
}
