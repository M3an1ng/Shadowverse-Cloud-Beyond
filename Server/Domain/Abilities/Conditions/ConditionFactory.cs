using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Processing;
using ShadowverseBeyond.Server.Domain.Abilities.Runtime;
using ShadowverseBeyond.Server.Domain.Interfaces;
using AutoMapper;

namespace ShadowverseBeyond.Server.Domain.Abilities.Conditions
{
    /// <summary>
    /// Default implementation of IConditionFactory.
    /// Creates Condition instances and handles the creation of their dependencies like DslConditionEvaluator.
    /// </summary>
    public class ConditionFactory : IConditionFactory
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly IMapper _mapper;
        private readonly IFunctionRegistry _functionRegistry;
        // Consider injecting IDslEvaluator and IDslExpressionParser if they are registered as services
        // For now, creating them internally for simplicity, assuming they don't have complex dependencies.

        public ConditionFactory(
            ILoggerFactory loggerFactory,
            IMapper mapper,
            IFunctionRegistry functionRegistry)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _functionRegistry = functionRegistry ?? throw new ArgumentNullException(nameof(functionRegistry));
        }

        /// <inheritdoc />
        public Condition CreateCondition(ConditionConfig config)
        {
            // Determine which evaluator is needed based on the config
            // For now, assuming DslConditionEvaluator is the primary one if DslExpression is present.
            // More complex logic might be needed for different condition types.

            IConditionEvaluator evaluator;

            // Use the correct property name 'Expression' from ConditionConfig
            if (!string.IsNullOrEmpty(config.Expression))
            {
                // Create dependencies for DslConditionEvaluator
                var dslEvaluatorLogger = _loggerFactory.CreateLogger<DslEvaluator>();
                var dslParserLogger = _loggerFactory.CreateLogger<DslExpressionParser>();
                var dslConditionEvaluatorLogger = _loggerFactory.CreateLogger<DslConditionEvaluator>();

                // TODO: Consider injecting these if they become complex or are registered services
                var dslEvaluator = new DslEvaluator(dslEvaluatorLogger, _functionRegistry);
                var dslParser = new DslExpressionParser(dslParserLogger);

                evaluator = new DslConditionEvaluator(
                    dslEvaluator,
                    dslParser,
                    dslConditionEvaluatorLogger);
            }
            else
            {
                // TODO: Implement logic for other condition types if needed
                // For now, throw an exception or return a default "always true/false" evaluator
                // based on expected behavior when no Expression is provided.
                // Let's assume a default evaluator that always returns true if no Expression.
                // Or perhaps better, throw if config is invalid.
                // For now, let's stick to the DslConditionEvaluator path as it was the one being refactored.
                throw new InvalidOperationException("ConditionConfig must have an Expression for DslConditionEvaluator.");
                // Alternatively, create a simpler evaluator if needed:
                // evaluator = new SimpleConditionEvaluator(config); // Assuming SimpleConditionEvaluator exists and implements IConditionEvaluator
            }

            var conditionLogger = _loggerFactory.CreateLogger<Condition>();
            return new Condition(config, evaluator, _mapper, conditionLogger);
        }
    }
}