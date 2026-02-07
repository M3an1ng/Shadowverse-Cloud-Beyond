using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Linq; // Added for LINQ operations
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Parser;
using ShadowverseBeyond.Server.Domain.Interfaces;
using ShadowverseBeyond.Server.Domain.Events; // Added for GameContext
using ShadowverseBeyond.Server.Domain.Abilities.Targets;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects
{
    /// <summary>
    /// 值源解析器实现
    /// 根据M12设计文档实现，负责解析效果配置中的动态值
    /// </summary>
    public class ValueSourceResolver : IValueSourceResolver
    {
        private readonly ILogger<ValueSourceResolver> _logger;
        private readonly IDslCompiler _dslCompiler;
        public ValueSourceResolver(
            ILogger<ValueSourceResolver> logger,
            IDslCompiler dslCompiler)
        {
            _logger = logger;
            _dslCompiler = dslCompiler;
        }

        /// <summary>
        /// 解析值源
        /// </summary>
        public object? Resolve(ValueConfig valueConfig, GameContext context)
        {
            // The main responsibility is now delegated!
            return valueConfig.Resolve(context, this);
        }


        // This method is now the core logic for any non-static value.
        public object? ResolveFormulaValue(string formula, GameContext context)
        {
            if (string.IsNullOrEmpty(formula))
            {
                return 0;
            }

            try
            {
                var tsc = new TargetSelectionContext(context, null);
                var getter = _dslCompiler.CompileGetterRuntime(
                    formula,
                    tsc,
                    selfType: null,
                    IDslCompiler.SelfResolutionMode.ContextOnly);

                // For value resolution, the 'self' context is often not applicable,
                // as the value is derived from the global game state (e.g., "player.Hand.Count").
                // We pass `null` as the item context (`self`).
                var value = getter(null!);

                _logger.LogDebug("DSL formula '{Formula}' resolved to: {Value}", formula, value);
                return value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving DSL formula: {Formula}", formula);
                return null; // Return null on error to let the caller decide the default.
            }
        }

        public object? ResolveFormulaValueWithTrace(string formula, GameContext context, IDslTraceCollector traceCollector)
        {
            if (string.IsNullOrEmpty(formula))
            {
                return 0;
            }

            try
            {
                var tsc = new TargetSelectionContext(context, null);
                var getter = _dslCompiler.CompileGetterWithTraceRuntime(
                    formula,
                    tsc,
                    selfType: null,
                    IDslCompiler.SelfResolutionMode.ContextOnly);
                var value = getter(null!, traceCollector);
                _logger.LogDebug("DSL formula '{Formula}' resolved to: {Value} (with trace)", formula, value);
                return value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving DSL formula with trace: {Formula}", formula);
                return null;
            }
        }

    }
}
