using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions;
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
        private readonly IConditionEvaluator _conditionEvaluator;

        public ConditionFactory(
            ILoggerFactory loggerFactory,
            IMapper mapper,
            IConditionEvaluator conditionEvaluator)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _conditionEvaluator = conditionEvaluator ?? throw new ArgumentNullException(nameof(conditionEvaluator));
        }

        /// <inheritdoc />
        public Condition CreateCondition(ConditionConfig config)
        {
            // The factory now receives the correct evaluator via DI.
            // It no longer needs to know how to construct a DslConditionEvaluator.
            // This simplifies the factory and adheres to DI principles.

            if (string.IsNullOrEmpty(config.Expression))
            {
                // If a condition config has no expression, it's arguably invalid.
                // Throwing an exception is a reasonable approach.
                // Alternatively, a "null object" pattern evaluator that always returns true/false could be used.
                throw new InvalidOperationException("Cannot create a condition from a config with an empty expression.");
            }

            // Use the injected evaluator instance.
            var conditionLogger = _loggerFactory.CreateLogger<Condition>();
            return new Condition(config, _conditionEvaluator, _mapper, conditionLogger);
        }
    }
}