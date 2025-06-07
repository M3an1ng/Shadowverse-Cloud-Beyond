namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Processing
{
    /// <summary>
    /// Responsible for parsing DSL expression strings into Abstract Syntax Trees (AST).
    /// This class focuses solely on parsing the expression syntax defined in the DSL design.
    /// </summary>
    public class DslExpressionParser // Renamed class
    {
        private readonly ILogger<DslExpressionParser> _logger; // Updated logger type

        public DslExpressionParser(ILogger<DslExpressionParser> logger) // Updated constructor
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Parses a DSL expression string into its AST representation.
        /// </summary>
        /// <param name="dslExpression">The DSL expression string.</param>
        /// <returns>The root AST.ExpressionNode if parsing is successful, otherwise null.</returns>
        public AST.ExpressionNode? ParseExpression(string dslExpression) // Renamed method, changed return type
        {
            var lexer = new DSL.Parser.Lexer(dslExpression);
            var tokens = lexer.Tokenize();

            // Handle lexer errors
            if (lexer.Errors.Any())
            {
                var errorMessages = lexer.Errors.Select(e => e.ToString()).ToList();
                _logger.LogError("DSL Lexing failed for expression:\n{DslExpression}\nErrors:\n{Errors}", dslExpression, string.Join("\n", errorMessages));
                // Optionally throw or just return null based on desired error handling
                return null;
            }

            var parser = new DSL.Parser.Parser(tokens);
            // Call ParseSingleExpression instead of ParseAbility
            var expressionAst = parser.ParseSingleExpression();

            // Handle parser errors
            if (parser.Errors.Any())
            {
                var errorMessages = parser.Errors.Select(e => e.ToString()).ToList();
                _logger.LogError("DSL Parsing failed for expression:\n{DslExpression}\nErrors:\n{Errors}", dslExpression, string.Join("\n", errorMessages));
                // Optionally throw or just return null
                return null; // Return null if parsing failed
            }

            // No longer creating AbilityConfig or validating it here.
            // The responsibility is now just to return the parsed AST node.

            return expressionAst; // Return the parsed expression AST node (can be null if parsing failed)
        }

        // All helper methods related to mapping AST to AbilityConfig have been removed.
        // The class now focuses solely on parsing DSL expressions into AST nodes.
    }
}