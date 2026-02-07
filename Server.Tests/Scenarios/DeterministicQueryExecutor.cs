using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using ShadowverseBeyond.Server.Domain.Abilities.Targets;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Events;
using ShadowverseBeyond.Server.Domain.Query;
using ShadowverseBeyond.Server.Services;
using ShadowverseBeyond.Server.Services.Abstractions;

namespace ShadowverseBeyond.Server.Tests.Scenarios
{
    public sealed class DeterministicQueryExecutor : IQueryExecutor
    {
        private readonly ILogger<DeterministicQueryExecutor> _logger;
        private readonly IDslCompiler _dslCompiler;
        private readonly IMapper _mapper;
        private readonly IQueryDslParser _dslParser;
        private readonly Random _random;

        public DeterministicQueryExecutor(
            ILogger<DeterministicQueryExecutor> logger,
            IDslCompiler dslCompiler,
            IMapper mapper,
            IQueryDslParser dslParser,
            Random random)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dslCompiler = dslCompiler ?? throw new ArgumentNullException(nameof(dslCompiler));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _dslParser = dslParser ?? throw new ArgumentNullException(nameof(dslParser));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public QueryResult ExecuteQuery(string dsl, GameContext context, Dictionary<string, List<TargetInfo>>? resolvedTargets = null)
        {
            try
            {
                var queryConfig = _dslParser.Parse(dsl);
                return ExecuteQuery(queryConfig, context, resolvedTargets);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse or execute DSL query: {Dsl}", dsl);
                return new QueryResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to parse or execute DSL query: {ex.Message}"
                };
            }
        }

        public QueryResult ExecuteQuery(QueryConfig queryConfig, GameContext context, Dictionary<string, List<TargetInfo>>? resolvedTargets = null)
        {
            queryConfig = NormalizeQueryConfig(queryConfig);
            var tsc = new TargetSelectionContext(context, resolvedTargets);
            List<object> currentPool;

            try
            {
                currentPool = ExecuteSourceDsl(queryConfig.SourcePoolExpression, tsc);

                if (queryConfig.Operations != null && queryConfig.Operations.Any())
                {
                    foreach (var operation in queryConfig.Operations)
                    {
                        if (operation is FilterOperation filterOp)
                        {
                            currentPool = ApplyFilterOperation(currentPool, filterOp, tsc);
                        }
                        else if (operation is SortOperation sortOp)
                        {
                            currentPool = ApplySortOperation(currentPool, sortOp, tsc);
                        }
                        else if (operation is SelectOperation selectOp)
                        {
                            currentPool = ApplySelectOperation(currentPool, selectOp, tsc);
                        }
                        else if (operation is SelectionOperation selectionOp)
                        {
                            currentPool = ApplySelectionOperation(currentPool, selectionOp, tsc);
                        }
                        else if (operation is PostProcessingOperation postProcessingOp)
                        {
                            currentPool = ApplyPostProcessingOperation(currentPool, postProcessingOp, tsc);
                        }
                    }
                }

                return new QueryResult
                {
                    Results = currentPool.Cast<object>().ToList().AsReadOnly(),
                    Success = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing card query pipeline.");
                return new QueryResult
                {
                    Results = Array.Empty<object>(),
                    Success = false,
                    ErrorMessage = $"An error occurred during query execution: {ex.Message}"
                };
            }
        }

        private List<object> ExecuteSourceDsl(string dsl, TargetSelectionContext tsc)
        {
            if (string.IsNullOrWhiteSpace(dsl))
            {
                return new List<object>();
            }

            try
            {
                var sourceGetter = _dslCompiler.CompileGetter<object>(dsl, tsc);
                var result = sourceGetter(null!);

                return result switch
                {
                    List<object> list => list,
                    IEnumerable<object> enumerable => enumerable.ToList(),
                    System.Collections.IEnumerable nonGeneric when result is not string => nonGeneric.Cast<object>().ToList(),
                    null => new List<object>(),
                    _ => new List<object> { result }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to compile or execute source DSL: {Dsl}", dsl);
                return new List<object>();
            }
        }

        private QueryConfig NormalizeQueryConfig(QueryConfig queryConfig)
        {
            if (queryConfig == null)
            {
                throw new ArgumentNullException(nameof(queryConfig));
            }

            if (queryConfig.Operations != null && queryConfig.Operations.Any())
            {
                return queryConfig;
            }

            var dsl = queryConfig.SourcePoolExpression?.Trim();
            if (string.IsNullOrWhiteSpace(dsl))
            {
                return queryConfig;
            }

            if (dsl.Contains(".query(") || dsl.Contains(".orderBy(") || dsl.Contains(".take(") || dsl.Contains(".select("))
            {
                try
                {
                    return _dslParser.Parse(dsl);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to normalize query DSL from SourcePoolExpression: {Dsl}", dsl);
                }
            }

            return queryConfig;
        }

        private List<object> ApplyFilterOperation(List<object> pool, FilterOperation filterOp, TargetSelectionContext tsc)
        {
            if (filterOp.Filters == null || !filterOp.Filters.Any())
            {
                return pool;
            }

            var current = pool;
            foreach (var filterConfig in filterOp.Filters)
            {
                if (filterConfig.ValueSource is not FormulaValueConfig formulaConfig || string.IsNullOrWhiteSpace(formulaConfig.Formula))
                {
                    continue;
                }

                try
                {
                    var predicate = _dslCompiler.CompilePredicate<object>(formulaConfig.Formula, tsc);
                    current = current.Where(item => predicate(item)).ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to compile or execute filter DSL: {Dsl}", formulaConfig.Formula);
                }

                if (!current.Any())
                {
                    break;
                }
            }

            return current;
        }

        private List<object> ApplySortOperation(List<object> pool, SortOperation sortOp, TargetSelectionContext tsc)
        {
            if (sortOp.Ordering == null || !sortOp.Ordering.Any())
            {
                return pool;
            }

            IOrderedEnumerable<object>? sortedPool = null;

            for (var i = 0; i < sortOp.Ordering.Count; i++)
            {
                var ordering = sortOp.Ordering[i];
                if (ordering.ValueSource is not FormulaValueConfig formulaConfig || string.IsNullOrWhiteSpace(formulaConfig.Formula))
                {
                    continue;
                }

                try
                {
                    var selector = _dslCompiler.CompileGetter<object>(formulaConfig.Formula, tsc);
                    if (i == 0)
                    {
                        sortedPool = ordering.Direction == OrderingDirection.Ascending
                            ? pool.OrderBy(item => selector(item))
                            : pool.OrderByDescending(item => selector(item));
                    }
                    else if (sortedPool != null)
                    {
                        sortedPool = ordering.Direction == OrderingDirection.Ascending
                            ? sortedPool.ThenBy(item => selector(item))
                            : sortedPool.ThenByDescending(item => selector(item));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to compile or execute sort DSL: {Dsl}", formulaConfig.Formula);
                }
            }

            return sortedPool?.ToList() ?? pool;
        }

        private List<object> ApplySelectionOperation(List<object> pool, SelectionOperation selectionOp, TargetSelectionContext tsc)
        {
            var count = 1;
            if (selectionOp.CountSource is FormulaValueConfig countFormula && !string.IsNullOrWhiteSpace(countFormula.Formula))
            {
                try
                {
                    var countGetter = _dslCompiler.CompileGetter<object>(countFormula.Formula, tsc);
                    var resolved = countGetter(null!);
                    if (resolved is int resolvedCount && resolvedCount >= 0)
                    {
                        count = resolvedCount;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to compile or execute count DSL: {Dsl}", countFormula.Formula);
                }
            }
            else if (selectionOp.CountSource is StaticValueConfig staticConfig)
            {
                count = TypeConverter.ConvertToInt(staticConfig.Value);
            }

            if (count < 0)
            {
                return pool;
            }

            return selectionOp.Strategy switch
            {
                SelectionStrategy.First => pool.Take(count).ToList(),
                SelectionStrategy.Last => pool.Skip(Math.Max(0, pool.Count - count)).Take(count).ToList(),
                SelectionStrategy.Random => ApplyRandomSelection(pool, count),
                _ => pool
            };
        }

        private List<object> ApplyRandomSelection(List<object> pool, int count)
        {
            if (count <= 0)
            {
                return new List<object>();
            }

            if (count >= pool.Count)
            {
                return pool;
            }

            return pool.OrderBy(_ => _random.Next()).Take(count).ToList();
        }

        private List<object> ApplyPostProcessingOperation(List<object> pool, PostProcessingOperation postProcessingOp, TargetSelectionContext tsc)
        {
            var selector = BuildProjectionSelector(postProcessingOp.Projection, tsc);
            if (selector == null)
            {
                return pool;
            }

            var resultPool = new List<object>();
            foreach (var item in pool)
            {
                resultPool.Add(selector(item) ?? (object?)null!);
            }

            return resultPool;
        }

        private List<object> ApplySelectOperation(List<object> pool, SelectOperation selectOp, TargetSelectionContext tsc)
        {
            var selector = BuildProjectionSelector(selectOp.Selector, tsc);
            if (selector == null)
            {
                return pool;
            }

            var resultPool = new List<object>();
            foreach (var item in pool)
            {
                resultPool.Add(selector(item) ?? (object?)null!);
            }

            return resultPool;
        }

        private Func<object, object?>? BuildProjectionSelector(ValueConfig? selectorConfig, TargetSelectionContext tsc)
        {
            if (selectorConfig == null)
            {
                return null;
            }

            if (selectorConfig is FormulaValueConfig formulaConfig)
            {
                var dsl = formulaConfig.Formula;
                if (string.IsNullOrWhiteSpace(dsl))
                {
                    return null;
                }

                var getter = _dslCompiler.CompileGetter<object>(dsl, tsc);
                return item => getter(item);
            }

            if (selectorConfig is StaticValueConfig staticConfig)
            {
                var staticValue = staticConfig.Value;
                return _ => staticValue;
            }

            return null;
        }
    }
}
