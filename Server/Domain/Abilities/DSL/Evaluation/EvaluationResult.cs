using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation
{
    /// <summary>
    /// Represents the result of a DSL expression evaluation.
    /// </summary>
    public record EvaluationResult
    {
        /// <summary>
        /// Indicates whether the evaluation was successful.
        /// </summary>
        public bool IsSuccess { get; init; }

        /// <summary>
        /// The computed value of the expression if evaluation was successful, otherwise null.
        /// </summary>
        public object? Value { get; init; }

        /// <summary>
        /// A list of errors encountered during evaluation. Empty if successful.
        /// </summary>
        public List<string> Errors { get; init; }

        private EvaluationResult(bool isSuccess, object? value, List<string> errors)
        {
            IsSuccess = isSuccess;
            Value = value;
            Errors = errors ?? new List<string>();
        }

        /// <summary>
        /// Creates a successful evaluation result.
        /// </summary>
        /// <param name="value">The computed value.</param>
        /// <returns>A new EvaluationResult instance.</returns>
        public static EvaluationResult Success(object? value) => new(true, value, new List<string>());

        /// <summary>
        /// Creates a failed evaluation result with a single error message.
        /// </summary>
        /// <param name="error">The error message.</param>
        /// <returns>A new EvaluationResult instance.</returns>
        public static EvaluationResult Failure(string error) => new(false, null, new List<string> { error });

        /// <summary>
        /// Creates a failed evaluation result with multiple error messages.
        /// </summary>
        /// <param name="errors">The list of error messages.</param>
        /// <returns>A new EvaluationResult instance.</returns>
        public static EvaluationResult Failure(List<string> errors) => new(false, null, errors);

        /// <summary>
        /// Adds an error message to the list. Use only for accumulating errors within the evaluator.
        /// </summary>
        /// <param name="error">The error message to add.</param>
        public void AddError(string error)
        {
            // Note: This modifies the list in the record, which is generally discouraged for immutability.
            // However, for accumulating errors during a single evaluation pass, it can be pragmatic.
            // Consider returning a new record if strict immutability is required.
            if (!string.IsNullOrEmpty(error))
            {
                Errors.Add(error);
            }
        }
    }
}