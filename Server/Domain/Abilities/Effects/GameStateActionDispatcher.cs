using ShadowverseBeyond.Server.Domain.Entities;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using ShadowverseBeyond.Server.Dtos; // Added for CardZone enum

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects
{
    /// <summary>
    /// GameState动作调度器实现
    /// 根据M12设计文档实现，负责将效果类型映射到GameState的原子操作方法
    /// </summary>
    public class GameStateActionDispatcher : IGameStateActionDispatcher
    {
        private readonly ConcurrentDictionary<string, Func<AbilityContext, Task<GameStateActionResult>>> _handlers; // Updated handler signature
        private readonly ILogger<GameStateActionDispatcher> _logger;

        public GameStateActionDispatcher(ILogger<GameStateActionDispatcher> logger)
        {
            _handlers = new ConcurrentDictionary<string, Func<AbilityContext, Task<GameStateActionResult>>>(); // Updated handler signature
            _logger = logger;

            // 注册标准效果处理器
            RegisterStandardHandlers();
        }

        /// <summary>
        /// 异步调度 GameState 原子操作。
        /// </summary>
        /// <param name="effectType">要执行的效果类型 (作为动作标识)。</param>
        /// <param name="context">当前的能力上下文，包含执行所需的所有信息。</param>
        /// <returns>GameState 操作的结果。</returns>
        public async Task<GameStateActionResult> DispatchAsync(string effectType, AbilityContext context) // Updated signature
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                if (!_handlers.TryGetValue(effectType, out var handler))
                {
                    var errorMessage = $"No handler registered for effect type: {effectType}";
                    _logger.LogError(errorMessage);

                    return new GameStateActionResult
                    {
                        Success = false,
                        ErrorMessage = errorMessage,
                        ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                    };
                }

                _logger.LogDebug("Dispatching effect: {EffectType} with {TargetCount} targets",
                    effectType, context.SelectedTargets.Count); // Use context.SelectedTargets

                // Pass the context directly to the handler
                var result = await handler(context); // Updated handler call
                result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

                if (result.Success)
                {
                    _logger.LogDebug("Effect {EffectType} executed successfully in {ExecutionTime}ms",
                        effectType, result.ExecutionTimeMs);
                }
                else
                {
                    _logger.LogWarning("Effect {EffectType} failed: {ErrorMessage}",
                        effectType, result.ErrorMessage);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error dispatching effect: {EffectType}", effectType);

                return new GameStateActionResult
                {
                    Success = false,
                    ErrorMessage = $"Exception during effect execution: {ex.Message}",
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
        }

        /// <summary>
        /// 注册效果类型处理器委托。
        /// </summary>
        /// <param name="effectType">效果类型。</param>
        /// <param name="handler">处理该效果类型的委托，接收 AbilityContext 并返回 Task<GameStateActionResult>。</param>
        public void RegisterHandler(string effectType, Func<AbilityContext, Task<GameStateActionResult>> handler) // Updated signature
        {
            // The handler type now matches the dictionary type, so AddOrUpdate should work directly.
            _handlers.AddOrUpdate(effectType, handler, (key, oldValue) => handler);
            _logger.LogDebug("Registered handler for effect type: {EffectType}", effectType);
        }

        /// <summary>
        /// 获取支持的效果类型列表
        /// </summary>
        public IEnumerable<string> GetSupportedEffectTypes()
        {
            return _handlers.Keys.ToList();
        }

        /// <summary>
        /// 注册标准效果处理器
        /// </summary>
        private void RegisterStandardHandlers()
        {
            // 伤害相关
            RegisterHandler(StandardEffectTypes.Damage, HandleDamageAsync);
            RegisterHandler(StandardEffectTypes.Heal, HandleHealAsync);
            RegisterHandler(StandardEffectTypes.ModifyHealth, HandleModifyHealthAsync);

            // 属性修改
            RegisterHandler(StandardEffectTypes.ModifyAttack, HandleModifyAttackAsync);
            RegisterHandler(StandardEffectTypes.ModifyStats, HandleModifyStatsAsync);
            RegisterHandler(StandardEffectTypes.AddKeyword, HandleAddKeywordAsync);
            RegisterHandler(StandardEffectTypes.RemoveKeyword, HandleRemoveKeywordAsync);

            // 卡牌操作
            RegisterHandler(StandardEffectTypes.DrawCard, HandleDrawCardAsync);
            RegisterHandler(StandardEffectTypes.DiscardCard, HandleDiscardCardAsync);
            RegisterHandler(StandardEffectTypes.AddCardToHand, HandleAddCardToHandAsync);
            RegisterHandler(StandardEffectTypes.SummonUnit, HandleSummonUnitAsync);
            RegisterHandler(StandardEffectTypes.DestroyUnit, HandleDestroyUnitAsync);

            // 资源操作
            RegisterHandler(StandardEffectTypes.ModifyPP, HandleModifyPPAsync);
            RegisterHandler(StandardEffectTypes.ModifyEP, HandleModifyEPAsync);
            RegisterHandler(StandardEffectTypes.GainPPOrb, HandleGainPPOrbAsync);

            // 特殊效果
            RegisterHandler(StandardEffectTypes.Transform, HandleTransformAsync);
            RegisterHandler(StandardEffectTypes.Evolve, HandleEvolveAsync);
            RegisterHandler(StandardEffectTypes.Silence, HandleSilenceAsync);
            RegisterHandler(StandardEffectTypes.Freeze, HandleFreezeAsync);

            // 游戏流程
            RegisterHandler(StandardEffectTypes.EndTurn, HandleEndTurnAsync);
            RegisterHandler(StandardEffectTypes.SkipTurn, HandleSkipTurnAsync);
            RegisterHandler(StandardEffectTypes.ExtraTurn, HandleExtraTurnAsync);

            // 变量操作
            RegisterHandler(StandardEffectTypes.SetVariable, HandleSetVariableAsync);
            RegisterHandler(StandardEffectTypes.ModifyVariable, HandleModifyVariableAsync);
        }

        // 以下是各种效果的处理方法，目前只提供基本框架
        // 实际实现需要调用GameState的相应方法

        private async Task<GameStateActionResult> HandleDamageAsync(AbilityContext context) // Signature already updated
        {
            // Use context.SourceCard?.InstanceId for logging
            _logger.LogDebug("Executing HandleDamageAsync for source card {SourceCardId}", context.SourceCard?.InstanceId); // Corrected property name

            // Use CurrentGameState and check Parameters dictionary
            if (context.CurrentGameState == null || context.SelectedTargets == null || !context.SelectedTargets.Any() || context.Parameters == null)
            {
                _logger.LogWarning("HandleDamageAsync: Missing required context information (CurrentGameState, SelectedTargets, or Parameters).");
                return new GameStateActionResult { Success = false, ErrorMessage = "Missing required context." };
            }

            // Get damage amount from context.Parameters
            if (!context.Parameters.TryGetValue("DamageAmount", out var damageValueObj) || !(damageValueObj is int damageAmount))
            {
                // Log using SourceCard.InstanceId
                _logger.LogWarning("HandleDamageAsync: Could not retrieve 'DamageAmount' from context parameters for source card {SourceCardId}.", context.SourceCard?.InstanceId); // Corrected property name
                return new GameStateActionResult { Success = false, ErrorMessage = "Missing or invalid 'DamageAmount' parameter." };
            }

            // Use CurrentGameState
            var gameState = context.CurrentGameState;
            var results = new List<IndividualActionResult>(); // This type is now defined
            var allGeneratedEvents = new List<object>(); // Collect all generated events
            bool overallSuccess = true;
            string? firstErrorMessage = null;

            foreach (var target in context.SelectedTargets)
            {
                // Ensure damageAmount is accessible here (it should be due to declaration scope)
                _logger.LogDebug("Applying damage {DamageAmount} to target {TargetId}", damageAmount, target.Id);

                // Example: Assume GameState has a method to apply damage to a target entity
                // We need the actual CardInstance, not just TargetId, for GameState operations.
                var targetInstance = gameState.FindCardInstanceById(target.Id);
                if (targetInstance == null)
                {
                    _logger.LogWarning("HandleDamageAsync: Target instance with ID {TargetId} not found in GameState.", target.Id);
                    results.Add(new IndividualActionResult { TargetId = target, Success = false, Message = "Target not found." });
                    overallSuccess = false;
                    if (firstErrorMessage == null) firstErrorMessage = "Target not found.";
                    continue; // Skip to the next target
                }

                // Apply damage to the target instance and collect generated events
                var generatedEvents = targetInstance.TakeDamage(damageAmount, context.SourceCard);

                // TakeDamage now returns events, so we can check for success based on events generated
                bool individualSuccess = true; // Assume success if no exceptions
                string? individualMessage = $"Applied {damageAmount} damage.";

                // Store generated events in the result for further processing
                if (generatedEvents.Any())
                {
                    individualMessage += $" Generated {generatedEvents.Count} events.";
                    allGeneratedEvents.AddRange(generatedEvents); // Collect events for the overall result
                }

                results.Add(new IndividualActionResult { TargetId = target, Success = individualSuccess, Message = individualMessage });
            }

            _logger.LogInformation("HandleDamageAsync completed. Overall Success: {Success}, Generated Events: {EventCount}", overallSuccess, allGeneratedEvents.Count);

            await Task.CompletedTask; // Suppress CS1998
            // GameStateActionResult now has the Results property
            return new GameStateActionResult
            {
                Success = overallSuccess,
                ErrorMessage = firstErrorMessage ?? string.Empty, // Handle potential null
                Results = results, // Store individual results
                GeneratedEvents = allGeneratedEvents // Store all generated events for event chain processing
            };
        }

        private async Task<GameStateActionResult> HandleHealAsync(AbilityContext context) // Updated signature
        {
            _logger.LogDebug("Executing HandleHealAsync for source card {SourceCardId}", context.SourceCard?.InstanceId);

            if (context.CurrentGameState == null || context.SelectedTargets == null || !context.SelectedTargets.Any() || context.Parameters == null)
            {
                _logger.LogWarning("HandleHealAsync: Missing required context information (CurrentGameState, SelectedTargets, or Parameters).");
                return new GameStateActionResult { Success = false, ErrorMessage = "Missing required context." };
            }

            // Get heal amount from context.Parameters
            if (!context.Parameters.TryGetValue("HealAmount", out var healValueObj) || !(healValueObj is int healAmount))
            {
                _logger.LogWarning("HandleHealAsync: Could not retrieve 'HealAmount' from context parameters for source card {SourceCardId}.", context.SourceCard?.InstanceId);
                return new GameStateActionResult { Success = false, ErrorMessage = "Missing or invalid 'HealAmount' parameter." };
            }

            var gameState = context.CurrentGameState;
            var results = new List<IndividualActionResult>();
            var allGeneratedEvents = new List<object>(); // Collect all generated events
            bool overallSuccess = true;
            string? firstErrorMessage = null;

            foreach (var target in context.SelectedTargets)
            {
                _logger.LogDebug("Applying heal {HealAmount} to target {TargetId}", healAmount, target.Id);

                var targetInstance = gameState.FindCardInstanceById(target.Id);
                if (targetInstance == null)
                {
                    _logger.LogWarning("HandleHealAsync: Target instance with ID {TargetId} not found in GameState.", target.Id);
                    results.Add(new IndividualActionResult { TargetId = target, Success = false, Message = "Target not found." });
                    overallSuccess = false;
                    if (firstErrorMessage == null) firstErrorMessage = "Target not found.";
                    continue;
                }

                // Apply healing to the target instance and collect generated events
                var generatedEvents = targetInstance.Heal(healAmount, context.SourceCard);

                // Heal now returns events, so we can check for success based on events generated
                bool individualSuccess = true; // Assume success if no exceptions
                string? individualMessage = $"Applied {healAmount} healing.";

                // Store generated events in the result for further processing
                if (generatedEvents.Any())
                {
                    individualMessage += $" Generated {generatedEvents.Count} events.";
                    allGeneratedEvents.AddRange(generatedEvents); // Collect events for the overall result
                }

                results.Add(new IndividualActionResult { TargetId = target, Success = individualSuccess, Message = individualMessage });
            }

            _logger.LogInformation("HandleHealAsync completed. Overall Success: {Success}, Generated Events: {EventCount}", overallSuccess, allGeneratedEvents.Count);

            await Task.CompletedTask; // Suppress CS1998
            return new GameStateActionResult
            {
                Success = overallSuccess,
                ErrorMessage = firstErrorMessage ?? string.Empty, // Handle potential null
                Results = results,
                GeneratedEvents = allGeneratedEvents // Store all generated events for event chain processing
            };
        }

        private async Task<GameStateActionResult> HandleModifyHealthAsync(AbilityContext context) // Updated signature
        {
            _logger.LogDebug("Executing HandleModifyHealthAsync for source card {SourceCardId}", context.SourceCard?.InstanceId);

            if (context.CurrentGameState == null || context.SelectedTargets == null || !context.SelectedTargets.Any() || context.Parameters == null)
            {
                _logger.LogWarning("HandleModifyHealthAsync: Missing required context information (CurrentGameState, SelectedTargets, or Parameters).");
                return new GameStateActionResult { Success = false, ErrorMessage = "Missing required context." };
            }

            // Get modification amount from context.Parameters (can be positive or negative)
            if (!context.Parameters.TryGetValue("ModifyAmount", out var modifyValueObj) || !(modifyValueObj is int modifyAmount))
            {
                _logger.LogWarning("HandleModifyHealthAsync: Could not retrieve 'ModifyAmount' from context parameters for source card {SourceCardId}.", context.SourceCard?.InstanceId);
                return new GameStateActionResult { Success = false, ErrorMessage = "Missing or invalid 'ModifyAmount' parameter." };
            }

            var gameState = context.CurrentGameState;
            var results = new List<IndividualActionResult>();
            bool overallSuccess = true;
            string? firstErrorMessage = null;

            foreach (var target in context.SelectedTargets)
            {
                _logger.LogDebug("Applying health modification {ModifyAmount} to target {TargetId}", modifyAmount, target.Id);

                var targetInstance = gameState.FindCardInstanceById(target.Id);
                if (targetInstance == null)
                {
                    _logger.LogWarning("HandleModifyHealthAsync: Target instance with ID {TargetId} not found in GameState.", target.Id);
                    results.Add(new IndividualActionResult { TargetId = target, Success = false, Message = "Target not found." });
                    overallSuccess = false;
                    if (firstErrorMessage == null) firstErrorMessage = "Target not found.";
                    continue;
                }

                // Modify health directly, clamping between 0 and MaxHealth
                int originalHealth = targetInstance.CurrentHealth;
                int newHealth = Math.Max(0, Math.Min(originalHealth + modifyAmount, targetInstance.MaxHealth)); // Clamp health
                targetInstance.CurrentHealth = newHealth;

                // Assume success, report the change
                bool individualSuccess = true;
                string? individualMessage = $"Modified health from {originalHealth} to {newHealth} (Amount: {modifyAmount}).";

                results.Add(new IndividualActionResult { TargetId = target, Success = individualSuccess, Message = individualMessage });

                // TODO: Consider if health reaching 0 should trigger destruction events here or elsewhere.
                if (newHealth == 0 && originalHealth > 0)
                {
                    _logger.LogInformation("Target {TargetId} health reached 0.", target.Id);
                    // Potentially add info to result or rely on separate event handling for destruction
                }
            }

            _logger.LogInformation("HandleModifyHealthAsync completed. Overall Success: {Success}", overallSuccess);

            await Task.CompletedTask; // Suppress CS1998
            return new GameStateActionResult
            {
                Success = overallSuccess,
                ErrorMessage = firstErrorMessage ?? string.Empty, // Handle potential null
                Results = results
            };
        }

        private async Task<GameStateActionResult> HandleModifyAttackAsync(AbilityContext context) // Updated signature
        {
            _logger.LogDebug("Executing HandleModifyAttackAsync for source card {SourceCardId}", context.SourceCard?.InstanceId);

            if (context.CurrentGameState == null || context.SelectedTargets == null || !context.SelectedTargets.Any() || context.Parameters == null)
            {
                _logger.LogWarning("HandleModifyAttackAsync: Missing required context information (CurrentGameState, SelectedTargets, or Parameters).");
                return new GameStateActionResult { Success = false, ErrorMessage = "Missing required context." };
            }

            // Get modification amount from context.Parameters (can be positive or negative)
            // Assuming the same parameter key "ModifyAmount" is used for attack modification. Adjust if needed.
            if (!context.Parameters.TryGetValue("ModifyAmount", out var modifyValueObj) || !(modifyValueObj is int modifyAmount))
            {
                _logger.LogWarning("HandleModifyAttackAsync: Could not retrieve 'ModifyAmount' from context parameters for source card {SourceCardId}.", context.SourceCard?.InstanceId);
                return new GameStateActionResult { Success = false, ErrorMessage = "Missing or invalid 'ModifyAmount' parameter." };
            }

            var gameState = context.CurrentGameState;
            var results = new List<IndividualActionResult>();
            bool overallSuccess = true;
            string? firstErrorMessage = null;

            foreach (var target in context.SelectedTargets)
            {
                _logger.LogDebug("Applying attack modification {ModifyAmount} to target {TargetId}", modifyAmount, target.Id);

                var targetInstance = gameState.FindCardInstanceById(target.Id);
                if (targetInstance == null)
                {
                    _logger.LogWarning("HandleModifyAttackAsync: Target instance with ID {TargetId} not found in GameState.", target.Id);
                    results.Add(new IndividualActionResult { TargetId = target, Success = false, Message = "Target not found." });
                    overallSuccess = false;
                    if (firstErrorMessage == null) firstErrorMessage = "Target not found.";
                    continue;
                }

                // Modify attack directly, clamping at 0 minimum.
                int originalAttack = targetInstance.CurrentAttack;
                int newAttack = Math.Max(0, originalAttack + modifyAmount); // Clamp attack >= 0
                targetInstance.CurrentAttack = newAttack;

                // Assume success, report the change
                bool individualSuccess = true;
                string? individualMessage = $"Modified attack from {originalAttack} to {newAttack} (Amount: {modifyAmount}).";

                results.Add(new IndividualActionResult { TargetId = target, Success = individualSuccess, Message = individualMessage });
            }

            _logger.LogInformation("HandleModifyAttackAsync completed. Overall Success: {Success}", overallSuccess);

            await Task.CompletedTask; // Suppress CS1998
            return new GameStateActionResult
            {
                Success = overallSuccess,
                ErrorMessage = firstErrorMessage ?? string.Empty, // Handle potential null
                Results = results
            };
        }

        private async Task<GameStateActionResult> HandleModifyStatsAsync(AbilityContext context) // Updated signature
        {
            _logger.LogDebug("Executing HandleModifyStatsAsync for source card {SourceCardId}", context.SourceCard?.InstanceId);

            if (context.CurrentGameState == null || context.SelectedTargets == null || !context.SelectedTargets.Any() || context.Parameters == null)
            {
                _logger.LogWarning("HandleModifyStatsAsync: Missing required context information (CurrentGameState, SelectedTargets, or Parameters).");
                return new GameStateActionResult { Success = false, ErrorMessage = "Missing required context." };
            }

            // Declare variables with default values first
            int attackModifyAmount = 0;
            int healthModifyAmount = 0;
            bool parameterFound = false; // Flag to track if at least one parameter is found

            // Get Attack modification amount
            if (context.Parameters.TryGetValue("ModifyAttackAmount", out object? attackModObj))
            {
                if (attackModObj is int tempAttackMod)
                {
                    attackModifyAmount = tempAttackMod;
                    parameterFound = true;
                }
                else
                {
                    _logger.LogWarning("HandleModifyStatsAsync: 'ModifyAttackAmount' parameter is not an integer for source card {SourceCardId}.", context.SourceCard?.InstanceId);
                    // Decide if this is an error or just ignore the parameter
                }
            }

            // Get Health modification amount
            if (context.Parameters.TryGetValue("ModifyHealthAmount", out object? healthModObj))
            {
                if (healthModObj is int tempHealthMod)
                {
                    healthModifyAmount = tempHealthMod;
                    parameterFound = true;
                }
                else
                {
                    _logger.LogWarning("HandleModifyStatsAsync: 'ModifyHealthAmount' parameter is not an integer for source card {SourceCardId}.", context.SourceCard?.InstanceId);
                    // Decide if this is an error or just ignore the parameter
                }
            }

            // Check if at least one valid parameter was found
            if (!parameterFound)
            {
                _logger.LogWarning("HandleModifyStatsAsync: Could not retrieve valid 'ModifyAttackAmount' or 'ModifyHealthAmount' from context parameters for source card {SourceCardId}.", context.SourceCard?.InstanceId);
                return new GameStateActionResult { Success = false, ErrorMessage = "Missing or invalid stat modification parameters." };
            }

            var gameState = context.CurrentGameState;
            var results = new List<IndividualActionResult>();
            bool overallSuccess = true;
            string? firstErrorMessage = null;

            foreach (var target in context.SelectedTargets)
            {
                _logger.LogDebug("Applying stats modification (Atk: {AttackMod}, Def: {HealthMod}) to target {TargetId}",
                                attackModifyAmount, healthModifyAmount, target.Id);

                var targetInstance = gameState.FindCardInstanceById(target.Id);
                if (targetInstance == null)
                {
                    _logger.LogWarning("HandleModifyStatsAsync: Target instance with ID {TargetId} not found in GameState.", target.Id);
                    results.Add(new IndividualActionResult { TargetId = target, Success = false, Message = "Target not found." });
                    overallSuccess = false;
                    if (firstErrorMessage == null) firstErrorMessage = "Target not found.";
                    continue;
                }

                // Modify stats directly
                int originalAttack = targetInstance.CurrentAttack;
                int originalHealth = targetInstance.CurrentHealth;

                int newAttack = Math.Max(0, originalAttack + attackModifyAmount); // Clamp attack >= 0
                int newHealth = Math.Max(0, Math.Min(originalHealth + healthModifyAmount, targetInstance.MaxHealth)); // Clamp health

                targetInstance.CurrentAttack = newAttack;
                targetInstance.CurrentHealth = newHealth;

                // Assume success, report the change
                bool individualSuccess = true;
                string? individualMessage = $"Modified stats: Atk {originalAttack}->{newAttack} (Mod:{attackModifyAmount}), Health {originalHealth}->{newHealth} (Mod:{healthModifyAmount}).";

                results.Add(new IndividualActionResult { TargetId = target, Success = individualSuccess, Message = individualMessage });

                if (newHealth == 0 && originalHealth > 0)
                {
                    _logger.LogInformation("Target {TargetId} health reached 0 due to stat modification.", target.Id);
                    // Potentially add info to result or rely on separate event handling for destruction
                }
            }

            _logger.LogInformation("HandleModifyStatsAsync completed. Overall Success: {Success}", overallSuccess);

            await Task.CompletedTask; // Suppress CS1998
            return new GameStateActionResult
            {
                Success = overallSuccess,
                ErrorMessage = firstErrorMessage ?? string.Empty, // Handle potential null
                Results = results
            };
        }

        private async Task<GameStateActionResult> HandleAddKeywordAsync(AbilityContext context) // Updated signature
        {
            _logger.LogDebug("Executing HandleAddKeywordAsync for source card {SourceCardId}", context.SourceCard?.InstanceId);

            if (context.CurrentGameState == null || context.SelectedTargets == null || !context.SelectedTargets.Any() || context.Parameters == null)
            {
                _logger.LogWarning("HandleAddKeywordAsync: Missing required context information (CurrentGameState, SelectedTargets, or Parameters).");
                return new GameStateActionResult { Success = false, ErrorMessage = "Missing required context." };
            }

            // Get the keyword to add from context.Parameters
            if (!context.Parameters.TryGetValue("Keyword", out var keywordObj) || !(keywordObj is string keyword) || string.IsNullOrEmpty(keyword))
            {
                _logger.LogWarning("HandleAddKeywordAsync: Could not retrieve valid 'Keyword' (string) from context parameters for source card {SourceCardId}.", context.SourceCard?.InstanceId);
                return new GameStateActionResult { Success = false, ErrorMessage = "Missing or invalid 'Keyword' parameter." };
            }

            var gameState = context.CurrentGameState;
            var results = new List<IndividualActionResult>();
            bool overallSuccess = true;
            string? firstErrorMessage = null;

            foreach (var target in context.SelectedTargets)
            {
                _logger.LogDebug("Adding keyword '{Keyword}' to target {TargetId}", keyword, target.Id);

                var targetInstance = gameState.FindCardInstanceById(target.Id);
                if (targetInstance == null)
                {
                    _logger.LogWarning("HandleAddKeywordAsync: Target instance with ID {TargetId} not found in GameState.", target.Id);
                    results.Add(new IndividualActionResult { TargetId = target, Success = false, Message = "Target not found." });
                    overallSuccess = false;
                    if (firstErrorMessage == null) firstErrorMessage = "Target not found.";
                    continue;
                }

                // Add keyword to the target instance.
                // ASSUMPTION: Keywords are managed as strings in the ActiveEffects list. Adjust if incorrect.
                // Consider adding checks to prevent duplicate keywords if necessary.
                if (!targetInstance.ActiveEffects.Contains(keyword)) // Example check for duplicates
                {
                    targetInstance.ActiveEffects.Add(keyword); // Add the keyword string
                }

                // Assume success, report the change
                bool individualSuccess = true;
                string? individualMessage = $"Added keyword '{keyword}'.";

                results.Add(new IndividualActionResult { TargetId = target, Success = individualSuccess, Message = individualMessage });
            }

            _logger.LogInformation("HandleAddKeywordAsync completed. Overall Success: {Success}", overallSuccess);

            await Task.CompletedTask; // Suppress CS1998
            return new GameStateActionResult
            {
                Success = overallSuccess,
                ErrorMessage = firstErrorMessage ?? string.Empty, // Handle potential null
                Results = results
            };
        }

        private async Task<GameStateActionResult> HandleRemoveKeywordAsync(AbilityContext context) // Updated signature
        {
            _logger.LogDebug("Executing HandleRemoveKeywordAsync for source card {SourceCardId}", context.SourceCard?.InstanceId);

            if (context.CurrentGameState == null || context.SelectedTargets == null || !context.SelectedTargets.Any() || context.Parameters == null)
            {
                _logger.LogWarning("HandleRemoveKeywordAsync: Missing required context information (CurrentGameState, SelectedTargets, or Parameters).");
                return new GameStateActionResult { Success = false, ErrorMessage = "Missing required context." };
            }

            // Get the keyword to remove from context.Parameters
            if (!context.Parameters.TryGetValue("Keyword", out var keywordObj) || !(keywordObj is string keyword) || string.IsNullOrEmpty(keyword))
            {
                _logger.LogWarning("HandleRemoveKeywordAsync: Could not retrieve valid 'Keyword' (string) from context parameters for source card {SourceCardId}.", context.SourceCard?.InstanceId);
                return new GameStateActionResult { Success = false, ErrorMessage = "Missing or invalid 'Keyword' parameter." };
            }

            var gameState = context.CurrentGameState;
            var results = new List<IndividualActionResult>();
            bool overallSuccess = true;
            string? firstErrorMessage = null;

            foreach (var target in context.SelectedTargets)
            {
                _logger.LogDebug("Removing keyword '{Keyword}' from target {TargetId}", keyword, target.Id);

                var targetInstance = gameState.FindCardInstanceById(target.Id);
                if (targetInstance == null)
                {
                    _logger.LogWarning("HandleRemoveKeywordAsync: Target instance with ID {TargetId} not found in GameState.", target.Id);
                    results.Add(new IndividualActionResult { TargetId = target, Success = false, Message = "Target not found." });
                    overallSuccess = false;
                    if (firstErrorMessage == null) firstErrorMessage = "Target not found.";
                    continue;
                }

                // Remove keyword from the target instance.
                // ASSUMPTION: Keywords are managed as strings in the ActiveEffects list.
                bool removed = targetInstance.ActiveEffects.Remove(keyword); // Use Remove()

                // Report success/failure of removal
                bool individualSuccess = removed;
                string? individualMessage = removed ? $"Removed keyword '{keyword}'." : $"Keyword '{keyword}' not found on target.";

                results.Add(new IndividualActionResult { TargetId = target, Success = individualSuccess, Message = individualMessage });

                // If removal failed for any target, the overall operation might be considered partially failed.
                if (!removed)
                {
                    overallSuccess = false; // Or maintain overallSuccess = true if finding the target is the main success criteria
                    if (firstErrorMessage == null) firstErrorMessage = $"Keyword '{keyword}' not found on target {target.Id}.";
                }
            }

            _logger.LogInformation("HandleRemoveKeywordAsync completed. Overall Success: {Success}", overallSuccess);

            await Task.CompletedTask; // Suppress CS1998
            return new GameStateActionResult
            {
                Success = overallSuccess,
                ErrorMessage = firstErrorMessage ?? string.Empty, // Handle potential null
                Results = results
            };
        }

        private async Task<GameStateActionResult> HandleDrawCardAsync(AbilityContext context) // Updated signature
        {
            _logger.LogDebug("Executing HandleDrawCardAsync for source card {SourceCardId}", context.SourceCard?.InstanceId);

            if (context.CurrentGameState == null || context.SelectedTargets == null || !context.SelectedTargets.Any() || context.Parameters == null)
            {
                _logger.LogWarning("HandleDrawCardAsync: Missing required context information (CurrentGameState, SelectedTargets, or Parameters).");
                return new GameStateActionResult { Success = false, ErrorMessage = "Missing required context." };
            }

            // Get draw amount from context.Parameters, default to 1 if not specified
            int drawAmount = 1; // Default draw amount
            if (context.Parameters.TryGetValue("DrawAmount", out var drawAmountObj) && drawAmountObj is int amount)
            {
                drawAmount = Math.Max(0, amount); // Ensure non-negative draw amount
            }
            else if (drawAmountObj != null) // Parameter exists but is not an int
            {
                _logger.LogWarning("HandleDrawCardAsync: 'DrawAmount' parameter is not a valid integer for source card {SourceCardId}. Using default value 1.", context.SourceCard?.InstanceId);
            }

            if (drawAmount == 0)
            {
                _logger.LogInformation("HandleDrawCardAsync: Draw amount is 0, no cards drawn.");
                // Removed Message property as it doesn't exist on GameStateActionResult
                return new GameStateActionResult { Success = true }; // Successful operation, nothing happened
            }


            var gameState = context.CurrentGameState;
            var results = new List<IndividualActionResult>();
            bool overallSuccess = true;
            string? firstErrorMessage = null;

            foreach (var target in context.SelectedTargets)
            {
                // ASSUMPTION: TargetId represents the PlayerId for draw actions.
                // Need to find the PlayerStateDto associated with this TargetId.
                // Use the GetPlayerState helper method from GameStateDto
                PlayerStateDto? targetPlayerState = gameState.GetPlayerState(target.Id);

                if (targetPlayerState == null)
                {
                    _logger.LogWarning("HandleDrawCardAsync: PlayerState with ID {PlayerId} not found in GameState.", target.Id);
                    results.Add(new IndividualActionResult { TargetId = target, Success = false, Message = "Target player not found." });
                    overallSuccess = false;
                    if (firstErrorMessage == null) firstErrorMessage = "Target player not found.";
                    continue;
                }

                _logger.LogDebug("Player {PlayerId} drawing {DrawAmount} card(s)", target.Id, drawAmount);

                // PlayerState does not have DrawCards. Implement logic here.
                // TODO: Consider moving this draw logic into a method within PlayerState for better encapsulation.
                int cardsDrawn = 0;
                bool deckIsEmpty = false;
                for (int i = 0; i < drawAmount; i++)
                {
                    if (targetPlayerState.Deck.Count > 0)
                    {
                        CardInstanceDto drawnCard = targetPlayerState.Deck[0]; // Get top card
                        targetPlayerState.Deck.RemoveAt(0); // Remove from deck
                        targetPlayerState.Hand.Add(drawnCard); // Add to hand
                        cardsDrawn++;
                        // Corrected log: Removed access to Definition.Name, log DefinitionId instead.
                        _logger.LogTrace("Player {PlayerId} drew card instance {InstanceId} (DefId: {DefinitionId}). Deck size: {DeckCount}, Hand size: {HandCount}",
                                         target.Id, drawnCard.InstanceId, drawnCard.CardId, targetPlayerState.Deck.Count, targetPlayerState.Hand.Count);
                    }
                    else
                    {
                        _logger.LogWarning("Player {PlayerId} deck is empty. Cannot draw more cards.", target.Id);
                        deckIsEmpty = true;
                        break; // Stop drawing if deck is empty
                    }
                }

                // Report result for this target
                bool individualSuccess = true; // Considered successful even if deck ran out
                string individualMessage = $"Player {target.Id} drew {cardsDrawn} card(s).";
                if (deckIsEmpty && cardsDrawn < drawAmount)
                {
                    individualMessage += " Deck is now empty.";
                }
                else if (cardsDrawn == 0 && drawAmount > 0) // Handle case where deck was already empty
                {
                    individualMessage = $"Player {target.Id} attempted to draw {drawAmount} card(s), but deck was empty.";
                }


                results.Add(new IndividualActionResult { TargetId = target, Success = individualSuccess, Message = individualMessage });

                // Note: No exception handling needed here unless List operations fail unexpectedly.
            }

            _logger.LogInformation("HandleDrawCardAsync completed. Overall Success: {Success}", overallSuccess);

            await Task.CompletedTask; // Suppress CS1998
            return new GameStateActionResult
            {
                Success = overallSuccess,
                ErrorMessage = firstErrorMessage ?? string.Empty, // Handle potential null
                Results = results
            };
        }

        private async Task<GameStateActionResult> HandleDiscardCardAsync(AbilityContext context) // Updated signature
        {
            _logger.LogDebug("Executing HandleDiscardCardAsync for source card {SourceCardId}", context.SourceCard?.InstanceId);

            if (context.CurrentGameState == null || context.SelectedTargets == null || !context.SelectedTargets.Any() || context.Parameters == null)
            {
                _logger.LogWarning("HandleDiscardCardAsync: Missing required context information (CurrentGameState, SelectedTargets, or Parameters).");
                return new GameStateActionResult { Success = false, ErrorMessage = "Missing required context." };
            }

            // Get discard amount from context.Parameters, default to 1
            int discardAmount = 1;
            if (context.Parameters.TryGetValue("DiscardAmount", out var discardAmountObj) && discardAmountObj is int amount)
            {
                discardAmount = Math.Max(0, amount);
            }
            else if (discardAmountObj != null)
            {
                _logger.LogWarning("HandleDiscardCardAsync: 'DiscardAmount' parameter is not a valid integer. Using default value 1.");
            }

            // TODO: Implement DiscardCriteria parsing (Random, LowestCost, HighestCost, SpecificInstanceIds)
            // For now, default to Random discard.
            string discardCriteria = "Random";
            if (context.Parameters.TryGetValue("DiscardCriteria", out var criteriaObj) && criteriaObj is string criteria)
            {
                discardCriteria = criteria; // In a real implementation, validate this string
            }

            var gameState = context.CurrentGameState;
            var results = new List<IndividualActionResult>();
            bool overallSuccess = true;
            string? firstErrorMessage = null;
            var random = new Random(); // For random discard

            foreach (var target in context.SelectedTargets)
            {
                PlayerStateDto? targetPlayerState = gameState.GetPlayerState(target.Id);

                if (targetPlayerState == null)
                {
                    _logger.LogWarning("HandleDiscardCardAsync: PlayerState with ID {PlayerId} not found.", target.Id);
                    results.Add(new IndividualActionResult { TargetId = target, Success = false, Message = "Target player not found." });
                    overallSuccess = false;
                    if (firstErrorMessage == null) firstErrorMessage = "Target player not found.";
                    continue;
                }

                _logger.LogDebug("Player {PlayerId} discarding {DiscardAmount} card(s) (Criteria: {Criteria})", target.Id, discardAmount, discardCriteria);

                List<CardInstanceDto> cardsToDiscard = new List<CardInstanceDto>();
                List<string> discardedCardInfo = new List<string>(); // For logging/message

                // --- Card Selection Logic ---
                var availableHand = targetPlayerState.Hand.ToList(); // Work with a copy

                if (!availableHand.Any())
                {
                    _logger.LogInformation("Player {PlayerId} has no cards in hand to discard.", target.Id);
                    results.Add(new IndividualActionResult { TargetId = target, Success = true, Message = "Hand is empty, no cards discarded." });
                    continue; // Move to next target
                }

                // Implement selection based on criteria
                if (discardCriteria.Equals("Random", StringComparison.OrdinalIgnoreCase))
                {
                    for (int i = 0; i < discardAmount && availableHand.Any(); i++)
                    {
                        int randomIndex = random.Next(availableHand.Count);
                        cardsToDiscard.Add(availableHand[randomIndex]);
                        availableHand.RemoveAt(randomIndex); // Remove from the temporary list to avoid re-selection
                    }
                }
                // TODO: Implement other criteria (LowestCost, HighestCost, SpecificInstanceIds)
                // else if (discardCriteria.Equals("LowestCost", StringComparison.OrdinalIgnoreCase)) { ... LINQ OrderBy ... }
                // else if (discardCriteria.Equals("HighestCost", StringComparison.OrdinalIgnoreCase)) { ... LINQ OrderByDescending ... }
                // else if (discardCriteria.Equals("SpecificInstanceIds", StringComparison.OrdinalIgnoreCase)) { ... Get InstanceIds from Parameters, LINQ Where ... }
                else
                {
                    _logger.LogWarning("HandleDiscardCardAsync: Unsupported DiscardCriteria '{Criteria}'. Defaulting to Random.", discardCriteria);
                    // Fallback to random if criteria is unknown
                    for (int i = 0; i < discardAmount && availableHand.Any(); i++)
                    {
                        int randomIndex = random.Next(availableHand.Count);
                        cardsToDiscard.Add(availableHand[randomIndex]);
                        availableHand.RemoveAt(randomIndex);
                    }
                }


                // --- Perform Discard ---
                if (!cardsToDiscard.Any())
                {
                    _logger.LogInformation("Player {PlayerId}: No cards selected for discard based on criteria or amount.", target.Id);
                    results.Add(new IndividualActionResult { TargetId = target, Success = true, Message = "No cards matched criteria or hand empty." });
                    continue;
                }

                foreach (var card in cardsToDiscard)
                {
                    if (targetPlayerState.Hand.Remove(card)) // Remove from actual hand
                    {
                        targetPlayerState.Graveyard.Add(card); // Add to graveyard
                        card.Zone = CardZone.Graveyard; // Update card's zone
                        discardedCardInfo.Add($"InstanceId: {card.InstanceId}, CardId: {card.CardId}");
                        _logger.LogTrace("Player {PlayerId} discarded card {InstanceId} (CardId: {CardId}) to Graveyard.", target.Id, card.InstanceId, card.CardId);
                    }
                    else
                    {
                        // This shouldn't happen if we selected from a copy of the hand, but log just in case.
                        _logger.LogError("Failed to remove card {InstanceId} from Player {PlayerId}'s hand during discard.", card.InstanceId, target.Id);
                        overallSuccess = false; // Indicate an internal error occurred
                        if (firstErrorMessage == null) firstErrorMessage = "Internal error during discard.";
                    }
                }

                results.Add(new IndividualActionResult
                {
                    TargetId = target,
                    Success = true, // Individual discard operation considered successful if cards were moved
                    Message = $"Discarded {cardsToDiscard.Count} card(s): [{string.Join(", ", discardedCardInfo)}]"
                });
            }

            _logger.LogInformation("HandleDiscardCardAsync completed. Overall Success: {Success}", overallSuccess);

            await Task.CompletedTask; // Suppress CS1998
            return new GameStateActionResult
            {
                Success = overallSuccess,
                ErrorMessage = firstErrorMessage ?? string.Empty, // Handle potential null
                Results = results
            };
        }

        private async Task<GameStateActionResult> HandleAddCardToHandAsync(AbilityContext context) // Updated signature
        {
            _logger.LogDebug("Executing HandleAddCardToHandAsync for source card {SourceCardId}", context.SourceCard?.InstanceId);

            if (context.CurrentGameState == null || context.SelectedTargets == null || !context.SelectedTargets.Any() || context.Parameters == null)
            {
                _logger.LogWarning("HandleAddCardToHandAsync: Missing required context information (CurrentGameState, SelectedTargets, or Parameters).");
                return new GameStateActionResult { Success = false, ErrorMessage = "Missing required context." };
            }

            // Get CardDefinitionId (mandatory)
            if (!context.Parameters.TryGetValue("CardDefinitionId", out var definitionIdObj) || !(definitionIdObj is int cardDefinitionId))
            {
                _logger.LogError("HandleAddCardToHandAsync: Missing or invalid 'CardDefinitionId' parameter.");
                return new GameStateActionResult { Success = false, ErrorMessage = "Missing or invalid 'CardDefinitionId' parameter." };
            }

            // Get Amount (optional, default 1)
            int amountToAdd = 1;
            if (context.Parameters.TryGetValue("Amount", out var amountObj) && amountObj is int amount)
            {
                amountToAdd = Math.Max(1, amount); // Ensure at least 1 if specified
            }
            else if (amountObj != null)
            {
                _logger.LogWarning("HandleAddCardToHandAsync: 'Amount' parameter is not a valid integer. Using default value 1.");
            }

            var gameState = context.CurrentGameState;
            var results = new List<IndividualActionResult>();
            bool overallSuccess = true;
            string? firstErrorMessage = null;

            foreach (var target in context.SelectedTargets)
            {
                PlayerStateDto? targetPlayerState = gameState.GetPlayerState(target.Id);

                if (targetPlayerState == null)
                {
                    _logger.LogWarning("HandleAddCardToHandAsync: PlayerState with ID {PlayerId} not found.", target.Id);
                    results.Add(new IndividualActionResult { TargetId = target, Success = false, Message = "Target player not found." });
                    overallSuccess = false;
                    if (firstErrorMessage == null) firstErrorMessage = "Target player not found.";
                    continue;
                }

                _logger.LogDebug("Adding {Amount} card(s) with DefinitionId {DefinitionId} to Player {PlayerId}'s hand.",
                                 amountToAdd, cardDefinitionId, target.Id);

                List<string> addedCardInfo = new List<string>();

                for (int i = 0; i < amountToAdd; i++)
                {
                    // Create new CardInstanceDto
                    Guid newInstanceId = Guid.NewGuid();
                    var newCardInstance = new CardInstanceDto()
                    {
                        InstanceId = newInstanceId,
                        CardId = cardDefinitionId,
                        ControllerPlayerId = targetPlayerState.PlayerId,
                        Zone = CardZone.Hand
                        // TODO: IMPORTANT - Populate other properties (Type, CurrentCost, CurrentAttack, CurrentHealth, MaxHealth etc.)
                        // This requires looking up the CardDefinition based on cardDefinitionId.
                        // This lookup might belong in a separate service/factory injected here.
                        // For now, these properties will have default values (0, empty string, etc.).
                    };

                    // Add to hand
                    targetPlayerState.Hand.Add(newCardInstance);
                    addedCardInfo.Add($"InstanceId: {newInstanceId}, CardId: {cardDefinitionId}");

                    _logger.LogTrace("Added new card instance {InstanceId} (CardId: {CardId}) to Player {PlayerId}'s hand. Hand size: {HandCount}",
                                     newInstanceId, cardDefinitionId, target.Id, targetPlayerState.Hand.Count);

                    // TODO: Consider hand size limits if applicable in the game rules.
                }

                results.Add(new IndividualActionResult
                {
                    TargetId = target,
                    Success = true,
                    Message = $"Added {amountToAdd} card(s) (DefId: {cardDefinitionId}) to hand: [{string.Join(", ", addedCardInfo)}]"
                });
            }

            _logger.LogInformation("HandleAddCardToHandAsync completed. Overall Success: {Success}", overallSuccess);

            await Task.CompletedTask; // Suppress CS1998
            return new GameStateActionResult
            {
                Success = overallSuccess,
                ErrorMessage = firstErrorMessage ?? string.Empty, // Handle potential null
                Results = results
            };
        }

        private async Task<GameStateActionResult> HandleSummonUnitAsync(AbilityContext context) // Updated signature
        {
            _logger.LogDebug("Executing HandleSummonUnitAsync for source card {SourceCardId}", context.SourceCard?.InstanceId);

            // 1. Validate context
            if (context.CurrentGameState == null || context.SelectedTargets == null || !context.SelectedTargets.Any() || context.Parameters == null)
            {
                _logger.LogWarning("HandleSummonUnitAsync: Missing required context information (CurrentGameState, SelectedTargets, or Parameters).");
                return new GameStateActionResult { Success = false, ErrorMessage = "Missing required context." };
            }

            // 2. Get CardDefinitionId from Parameters
            if (!context.Parameters.TryGetValue("CardDefinitionId", out var definitionIdObj) || !(definitionIdObj is int cardDefinitionId))
            {
                _logger.LogError("HandleSummonUnitAsync: Missing or invalid 'CardDefinitionId' parameter.");
                return new GameStateActionResult { Success = false, ErrorMessage = "Missing or invalid 'CardDefinitionId' parameter." };
            }

            // 3. Get Amount (optional, default 1) - For now, assume summoning 1 unit per call.
            // int amountToSummon = 1; // If needed later

            var gameState = context.CurrentGameState;
            var results = new List<IndividualActionResult>();
            bool overallSuccess = true;
            string? firstErrorMessage = null;

            // 4. Loop through targets (assuming target is the Player to summon for)
            foreach (var target in context.SelectedTargets)
            {
                PlayerStateDto? targetPlayerState = gameState.GetPlayerState(target.Id);
                if (targetPlayerState == null)
                {
                    _logger.LogWarning("HandleSummonUnitAsync: PlayerState with ID {PlayerId} not found.", target.Id);
                    results.Add(new IndividualActionResult { TargetId = target, Success = false, Message = "Target player not found." });
                    overallSuccess = false;
                    if (firstErrorMessage == null) firstErrorMessage = "Target player not found.";
                    continue;
                }

                _logger.LogDebug("Attempting to summon unit (DefId: {DefinitionId}) for Player {PlayerId}", cardDefinitionId, target.Id);

                // Use the player's field directly from PlayerStateDto
                var playerField = targetPlayerState.Field;


                // 5. Check field limit (Assume max 5 units)
                const int MAX_FIELD_SIZE = 5;
                if (playerField.Count >= MAX_FIELD_SIZE)
                {
                    _logger.LogWarning("Player {PlayerId}'s field is full ({CurrentCount}/{MaxCount}). Cannot summon unit (DefId: {DefinitionId}).",
                                     target.Id, playerField.Count, MAX_FIELD_SIZE, cardDefinitionId);
                    results.Add(new IndividualActionResult { TargetId = target, Success = false, Message = "Field is full." });
                    overallSuccess = false; // Summon failed for this target
                    if (firstErrorMessage == null) firstErrorMessage = "Field is full.";
                    continue; // Move to next target
                }

                // 6. Create the new unit instance
                Guid newInstanceId = Guid.NewGuid();
                var newUnitInstance = new CardInstanceDto()
                {
                    InstanceId = newInstanceId,
                    CardId = cardDefinitionId,
                    ControllerPlayerId = targetPlayerState.PlayerId,
                    Zone = CardZone.Field
                    // TODO: IMPORTANT - Populate other properties (Type, Cost, Attack, Health, MaxHealth, Keywords etc.)
                    // from CardDefinition lookup (requires external service/data access).
                };

                // 7. Add to field list
                playerField.Add(newUnitInstance);
                // TODO: Assign FieldPosition if needed (e.g., newUnitInstance.FieldPosition = playerField.Count - 1;)

                _logger.LogTrace("Summoned new unit instance {InstanceId} (DefId: {DefinitionId}) for Player {PlayerId}. Field size: {FieldCount}",
                                 newInstanceId, cardDefinitionId, target.Id, playerField.Count);

                // 8. Add result for this target player
                results.Add(new IndividualActionResult
                {
                    TargetId = target,
                    Success = true,
                    Message = $"Summoned unit (DefId: {cardDefinitionId}). Field count: {playerField.Count}/{MAX_FIELD_SIZE}."
                });

            } // End foreach target

            _logger.LogInformation("HandleSummonUnitAsync completed. Overall Success: {Success}", overallSuccess);

            await Task.CompletedTask; // Suppress CS1998
            return new GameStateActionResult
            {
                Success = overallSuccess,
                ErrorMessage = firstErrorMessage ?? string.Empty, // Handle potential null
                Results = results
            };
        }

        private async Task<GameStateActionResult> HandleDestroyUnitAsync(AbilityContext context) // Updated signature
        {
            _logger.LogDebug("Executing HandleDestroyUnitAsync for source card {SourceCardId}", context.SourceCard?.InstanceId);

            // 1. Validate context
            if (context.CurrentGameState == null || context.SelectedTargets == null || !context.SelectedTargets.Any())
            {
                _logger.LogWarning("HandleDestroyUnitAsync: Missing required context information (CurrentGameState or SelectedTargets).");
                return new GameStateActionResult { Success = false, ErrorMessage = "Missing required context." };
            }

            var gameState = context.CurrentGameState;
            var results = new List<IndividualActionResult>();
            bool overallSuccess = true;
            string? firstErrorMessage = null;

            // 2. Loop through targets (InstanceIds of units to destroy)
            foreach (var target in context.SelectedTargets)
            {
                string instanceIdToDestroy = target.Id;
                _logger.LogDebug("Attempting to destroy unit with InstanceId: {InstanceId}", instanceIdToDestroy);

                // 3. Find the unit instance DTO directly in player fields
                CardInstanceDto? targetInstanceDto = null;
                PlayerStateDto? ownerPlayerState = null;

                // Search for the card in all players' fields
                foreach (var player in gameState.Players)
                {
                    var foundCard = player.Field.FirstOrDefault(c => c.InstanceId.ToString() == instanceIdToDestroy);
                    if (foundCard != null)
                    {
                        targetInstanceDto = foundCard;
                        ownerPlayerState = player;
                        break;
                    }
                }

                if (targetInstanceDto == null || ownerPlayerState == null)
                {
                    _logger.LogWarning("HandleDestroyUnitAsync: Target unit instance with ID {InstanceId} not found on any player's field.", instanceIdToDestroy);
                    results.Add(new IndividualActionResult { TargetId = target, Success = false, Message = "Target unit not found on field." });
                    overallSuccess = false;
                    if (firstErrorMessage == null) firstErrorMessage = "Target unit not found on field.";
                    continue; // Skip to the next target
                }

                // 4. Check if the unit is on the field (should be true since we found it there)
                if (targetInstanceDto.Zone != CardZone.Field)
                {
                    _logger.LogWarning("HandleDestroyUnitAsync: Target unit {InstanceId} is not on the field (Zone: {Zone}). Cannot destroy.", instanceIdToDestroy, targetInstanceDto.Zone);
                    results.Add(new IndividualActionResult { TargetId = target, Success = false, Message = $"Unit not on field (Zone: {targetInstanceDto.Zone})." });
                    overallSuccess = false;
                    if (firstErrorMessage == null) firstErrorMessage = "Unit not on field.";
                    continue;
                }

                // 5. Remove from field
                bool removedFromField = ownerPlayerState.Field.Remove(targetInstanceDto);
                if (!removedFromField)
                {
                    // This indicates another inconsistency
                    _logger.LogError("HandleDestroyUnitAsync: Failed to remove unit {InstanceId} from Player {PlayerId}'s field list.", instanceIdToDestroy, ownerPlayerState.PlayerId);
                    results.Add(new IndividualActionResult { TargetId = target, Success = false, Message = "Internal error: Failed to remove from field." });
                    overallSuccess = false;
                    if (firstErrorMessage == null) firstErrorMessage = "Internal error: Failed to remove from field.";
                    // Continue processing other targets, but mark this one as failed.
                }

                // 6. Add to graveyard and update zone (even if removal from field failed, try to ensure it ends up in graveyard)
                ownerPlayerState.Graveyard.Add(targetInstanceDto);
                targetInstanceDto.Zone = CardZone.Graveyard;

                _logger.LogTrace("Destroyed unit {InstanceId} (CardId: {CardId}) belonging to Player {PlayerId}. Moved to Graveyard.",
                                 instanceIdToDestroy, targetInstanceDto.CardId, ownerPlayerState.PlayerId);

                // 9. Add result for this target
                // If removedFromField was false, we still report success for the *destroy action* itself,
                // but the overallSuccess flag and error message will reflect the internal issue.
                results.Add(new IndividualActionResult
                {
                    TargetId = target,
                    Success = true, // The intent to destroy was processed
                    Message = $"Unit {instanceIdToDestroy} destroyed and moved to graveyard." + (!removedFromField ? " (Warning: Field removal inconsistency)" : "")
                });

            } // End foreach target

            _logger.LogInformation("HandleDestroyUnitAsync completed. Overall Success: {Success}", overallSuccess);

            await Task.CompletedTask; // Suppress CS1998
            return new GameStateActionResult
            {
                Success = overallSuccess,
                ErrorMessage = firstErrorMessage ?? string.Empty, // Handle potential null
                Results = results
            };
        }

        private async Task<GameStateActionResult> HandleModifyPPAsync(AbilityContext context) // Updated signature
        {
            _logger.LogDebug("Executing HandleModifyPPAsync for source card {SourceCardId}", context.SourceCard?.InstanceId);

            // 1. Validate context
            if (context.CurrentGameState == null || context.SelectedTargets == null || !context.SelectedTargets.Any() || context.Parameters == null)
            {
                _logger.LogWarning("HandleModifyPPAsync: Missing required context information (CurrentGameState, SelectedTargets, or Parameters).");
                return new GameStateActionResult { Success = false, ErrorMessage = "Missing required context." };
            }

            // 2. Get ModifyAmount from Parameters
            if (!context.Parameters.TryGetValue("ModifyAmount", out var modifyValueObj) || !(modifyValueObj is int modifyAmount))
            {
                _logger.LogWarning("HandleModifyPPAsync: Could not retrieve 'ModifyAmount' from context parameters for source card {SourceCardId}.", context.SourceCard?.InstanceId);
                return new GameStateActionResult { Success = false, ErrorMessage = "Missing or invalid 'ModifyAmount' parameter." };
            }

            var gameState = context.CurrentGameState;
            var results = new List<IndividualActionResult>();
            bool overallSuccess = true;
            string? firstErrorMessage = null;

            // 3. Loop through targets (PlayerIds)
            foreach (var target in context.SelectedTargets)
            {
                string targetPlayerId = target.Id;
                _logger.LogDebug("Attempting to modify PP by {ModifyAmount} for Player {PlayerId}", modifyAmount, targetPlayerId);

                // 4. Find the PlayerState
                PlayerStateDto? targetPlayerState = gameState.GetPlayerState(targetPlayerId);
                if (targetPlayerState == null)
                {
                    _logger.LogWarning("HandleModifyPPAsync: PlayerState with ID {PlayerId} not found.", targetPlayerId);
                    results.Add(new IndividualActionResult { TargetId = target, Success = false, Message = "Target player not found." });
                    overallSuccess = false;
                    if (firstErrorMessage == null) firstErrorMessage = "Target player not found.";
                    continue;
                }

                // 5. Modify PP with clamping
                int originalPP = targetPlayerState.CurrentPP;
                int maxPP = targetPlayerState.MaxPP;
                int newPP = Math.Max(0, Math.Min(originalPP + modifyAmount, maxPP)); // Clamp between 0 and MaxPP

                targetPlayerState.CurrentPP = newPP;

                _logger.LogTrace("Modified Player {PlayerId}'s PP from {OriginalPP} to {NewPP} (Amount: {ModifyAmount}, MaxPP: {MaxPP})",
                                 targetPlayerId, originalPP, newPP, modifyAmount, maxPP);

                // 6. Add result
                results.Add(new IndividualActionResult
                {
                    TargetId = target,
                    Success = true,
                    Message = $"Modified PP from {originalPP} to {newPP} (Amount: {modifyAmount})."
                });
            } // End foreach target

            _logger.LogInformation("HandleModifyPPAsync completed. Overall Success: {Success}", overallSuccess);

            await Task.CompletedTask; // Suppress CS1998
            return new GameStateActionResult
            {
                Success = overallSuccess,
                ErrorMessage = firstErrorMessage ?? string.Empty, // Handle potential null
                Results = results
            };
        }

        private async Task<GameStateActionResult> HandleModifyEPAsync(AbilityContext context) // Updated signature
        {
            _logger.LogDebug("Executing HandleModifyEPAsync for source card {SourceCardId}", context.SourceCard?.InstanceId);

            // 1. Validate context
            if (context.CurrentGameState == null || context.SelectedTargets == null || !context.SelectedTargets.Any() || context.Parameters == null)
            {
                _logger.LogWarning("HandleModifyEPAsync: Missing required context information (CurrentGameState, SelectedTargets, or Parameters).");
                return new GameStateActionResult { Success = false, ErrorMessage = "Missing required context." };
            }

            // 2. Get ModifyAmount from Parameters
            if (!context.Parameters.TryGetValue("ModifyAmount", out var modifyValueObj) || !(modifyValueObj is int modifyAmount))
            {
                _logger.LogWarning("HandleModifyEPAsync: Could not retrieve 'ModifyAmount' from context parameters for source card {SourceCardId}.", context.SourceCard?.InstanceId);
                return new GameStateActionResult { Success = false, ErrorMessage = "Missing or invalid 'ModifyAmount' parameter." };
            }

            var gameState = context.CurrentGameState;
            var results = new List<IndividualActionResult>();
            bool overallSuccess = true;
            string? firstErrorMessage = null;

            // 3. Loop through targets (PlayerIds)
            foreach (var target in context.SelectedTargets)
            {
                string targetPlayerId = target.Id;
                _logger.LogDebug("Attempting to modify EP by {ModifyAmount} for Player {PlayerId}", modifyAmount, targetPlayerId);

                // 4. Find the PlayerState
                PlayerStateDto? targetPlayerState = gameState.GetPlayerState(targetPlayerId);
                if (targetPlayerState == null)
                {
                    _logger.LogWarning("HandleModifyEPAsync: PlayerState with ID {PlayerId} not found.", targetPlayerId);
                    results.Add(new IndividualActionResult { TargetId = target, Success = false, Message = "Target player not found." });
                    overallSuccess = false;
                    if (firstErrorMessage == null) firstErrorMessage = "Target player not found.";
                    continue;
                }

                // 5. Modify EP with clamping
                int originalEP = targetPlayerState.CurrentEP;
                int maxEP = targetPlayerState.MaxEP;
                int newEP = Math.Max(0, Math.Min(originalEP + modifyAmount, maxEP)); // Clamp between 0 and MaxEP

                targetPlayerState.CurrentEP = newEP;

                _logger.LogTrace("Modified Player {PlayerId}'s EP from {OriginalEP} to {NewEP} (Amount: {ModifyAmount}, MaxEP: {MaxEP})",
                                 targetPlayerId, originalEP, newEP, modifyAmount, maxEP);

                // 6. Add result
                results.Add(new IndividualActionResult
                {
                    TargetId = target,
                    Success = true,
                    Message = $"Modified EP from {originalEP} to {newEP} (Amount: {modifyAmount})."
                });
            } // End foreach target

            _logger.LogInformation("HandleModifyEPAsync completed. Overall Success: {Success}", overallSuccess);

            await Task.CompletedTask; // Suppress CS1998
            return new GameStateActionResult
            {
                Success = overallSuccess,
                ErrorMessage = firstErrorMessage ?? string.Empty, // Handle potential null
                Results = results
            };
        }

        private async Task<GameStateActionResult> HandleGainPPOrbAsync(AbilityContext context) // Updated signature
        {
            _logger.LogDebug("Executing HandleGainPPOrbAsync for source card {SourceCardId}", context.SourceCard?.InstanceId);

            // 1. Validate context
            if (context.CurrentGameState == null || context.SelectedTargets == null || !context.SelectedTargets.Any())
            {
                _logger.LogWarning("HandleGainPPOrbAsync: Missing required context information (CurrentGameState or SelectedTargets).");
                return new GameStateActionResult { Success = false, ErrorMessage = "Missing required context." };
            }

            // 2. Get Amount from Parameters (optional, default 1)
            int amountToGain = 1;
            if (context.Parameters != null && context.Parameters.TryGetValue("Amount", out var amountObj) && amountObj is int amount)
            {
                amountToGain = Math.Max(1, amount); // Ensure at least 1 if specified
            }
            else if (context.Parameters != null && context.Parameters.ContainsKey("Amount")) // Parameter exists but is not int
            {
                _logger.LogWarning("HandleGainPPOrbAsync: 'Amount' parameter is not a valid integer. Using default value 1.");
            }

            var gameState = context.CurrentGameState;
            var results = new List<IndividualActionResult>();
            bool overallSuccess = true;
            string? firstErrorMessage = null;
            const int MAX_PP_ORBS_CAP = 10; // Assume a game-wide cap of 10 Max PP Orbs

            // 3. Loop through targets (PlayerIds)
            foreach (var target in context.SelectedTargets)
            {
                string targetPlayerId = target.Id;
                _logger.LogDebug("Attempting to gain {Amount} PP Orb(s) for Player {PlayerId}", amountToGain, targetPlayerId);

                // 4. Find the PlayerState
                PlayerStateDto? targetPlayerState = gameState.GetPlayerState(targetPlayerId);
                if (targetPlayerState == null)
                {
                    _logger.LogWarning("HandleGainPPOrbAsync: PlayerState with ID {PlayerId} not found.", targetPlayerId);
                    results.Add(new IndividualActionResult { TargetId = target, Success = false, Message = "Target player not found." });
                    overallSuccess = false;
                    if (firstErrorMessage == null) firstErrorMessage = "Target player not found.";
                    continue;
                }

                // 5. Increase MaxPP with clamping
                int originalMaxPP = targetPlayerState.MaxPP;
                int newMaxPP = Math.Min(originalMaxPP + amountToGain, MAX_PP_ORBS_CAP); // Clamp at the game cap

                if (newMaxPP > originalMaxPP)
                {
                    targetPlayerState.MaxPP = newMaxPP;
                    _logger.LogTrace("Increased Player {PlayerId}'s Max PP Orbs from {OriginalMaxPP} to {NewMaxPP} (Amount Gained: {AmountGained})",
                                     targetPlayerId, originalMaxPP, newMaxPP, newMaxPP - originalMaxPP);

                    // 6. Add result
                    results.Add(new IndividualActionResult
                    {
                        TargetId = target,
                        Success = true,
                        Message = $"Gained {newMaxPP - originalMaxPP} PP Orb(s). New Max PP: {newMaxPP}."
                    });
                }
                else
                {
                    _logger.LogInformation("Player {PlayerId} is already at the Max PP Orb cap ({MaxCap}). No orbs gained.", targetPlayerId, MAX_PP_ORBS_CAP);
                    // Still considered a success, just no change happened.
                    results.Add(new IndividualActionResult
                    {
                        TargetId = target,
                        Success = true,
                        Message = $"Already at Max PP Orb cap ({MAX_PP_ORBS_CAP}). No change."
                    });
                }
            } // End foreach target

            _logger.LogInformation("HandleGainPPOrbAsync completed. Overall Success: {Success}", overallSuccess);

            await Task.CompletedTask; // Suppress CS1998
            return new GameStateActionResult
            {
                Success = overallSuccess,
                ErrorMessage = firstErrorMessage ?? string.Empty, // Handle potential null
                Results = results
            };
        }

        private async Task<GameStateActionResult> HandleTransformAsync(AbilityContext context) // Updated signature
        {
            _logger.LogDebug("Executing HandleTransformAsync for source card {SourceCardId}", context.SourceCard?.InstanceId);

            // 1. Validate context
            if (context.CurrentGameState == null || context.SelectedTargets == null || !context.SelectedTargets.Any() || context.Parameters == null)
            {
                _logger.LogWarning("HandleTransformAsync: Missing required context information (CurrentGameState, SelectedTargets, or Parameters).");
                return new GameStateActionResult { Success = false, ErrorMessage = "Missing required context." };
            }

            // 2. Get NewCardDefinitionId from Parameters
            if (!context.Parameters.TryGetValue("NewCardDefinitionId", out var definitionIdObj) || !(definitionIdObj is int newCardDefinitionId))
            {
                _logger.LogError("HandleTransformAsync: Missing or invalid 'NewCardDefinitionId' parameter.");
                return new GameStateActionResult { Success = false, ErrorMessage = "Missing or invalid 'NewCardDefinitionId' parameter." };
            }

            var gameState = context.CurrentGameState;
            var results = new List<IndividualActionResult>();
            bool overallSuccess = true;
            string? firstErrorMessage = null;

            // 3. Loop through targets (InstanceIds of units to transform)
            foreach (var target in context.SelectedTargets)
            {
                string instanceIdToTransform = target.Id;
                _logger.LogDebug("Attempting to transform unit {InstanceId} into DefinitionId {NewDefinitionId}", instanceIdToTransform, newCardDefinitionId);

                // 4. Find the unit instance
                var targetInstance = gameState.FindCardInstanceById(instanceIdToTransform);
                if (targetInstance == null)
                {
                    _logger.LogWarning("HandleTransformAsync: Target unit instance with ID {InstanceId} not found in GameState.", instanceIdToTransform);
                    results.Add(new IndividualActionResult { TargetId = target, Success = false, Message = "Target unit not found." });
                    overallSuccess = false;
                    if (firstErrorMessage == null) firstErrorMessage = "Target unit not found.";
                    continue;
                }

                // 5. Check if the unit is on the field
                if (targetInstance.CurrentZone != CardZone.Field)
                {
                    _logger.LogWarning("HandleTransformAsync: Target unit {InstanceId} is not on the field (CurrentZone: {Zone}). Cannot transform.", instanceIdToTransform, targetInstance.CurrentZone);
                    results.Add(new IndividualActionResult { TargetId = target, Success = false, Message = $"Unit not on field (Zone: {targetInstance.CurrentZone})." });
                    overallSuccess = false;
                    if (firstErrorMessage == null) firstErrorMessage = "Unit not on field.";
                    continue;
                }

                // 6. Perform the transformation (change DefinitionId)
                int originalDefinitionId = targetInstance.DefinitionId;
                targetInstance.DefinitionId = newCardDefinitionId;

                // --- IMPORTANT NOTE ---
                // This handler ONLY changes the DefinitionId.
                // Resetting stats (Attack, Health, MaxHealth), keywords (ActiveEffects),
                // costs, card type, etc., based on the new definition MUST be handled
                // by a separate mechanism (e.g., an event listener reacting to a
                // 'UnitTransformed' event, or a service that has access to card definition data).
                // This dispatcher does not have access to card definition details.
                // TODO: Ensure such a mechanism exists and is triggered appropriately.

                _logger.LogTrace("Transformed unit {InstanceId} from DefinitionId {OriginalDefinitionId} to {NewDefinitionId}.",
                                 instanceIdToTransform, originalDefinitionId, newCardDefinitionId);

                // 7. Add result
                results.Add(new IndividualActionResult
                {
                    TargetId = target,
                    Success = true,
                    Message = $"Transformed unit {instanceIdToTransform} from DefId {originalDefinitionId} to {newCardDefinitionId}. (Note: Stats/effects reset handled elsewhere)."
                });

            } // End foreach target

            _logger.LogInformation("HandleTransformAsync completed. Overall Success: {Success}", overallSuccess);

            await Task.CompletedTask; // Suppress CS1998
            return new GameStateActionResult
            {
                Success = overallSuccess,
                ErrorMessage = firstErrorMessage ?? string.Empty, // Handle potential null
                Results = results
            };
        }

        private async Task<GameStateActionResult> HandleEvolveAsync(AbilityContext context) // Updated signature
        {
            _logger.LogDebug("Executing HandleEvolveAsync for source card {SourceCardId}", context.SourceCard?.InstanceId);

            // 1. Validate context
            if (context.CurrentGameState == null || context.SelectedTargets == null || !context.SelectedTargets.Any())
            {
                _logger.LogWarning("HandleEvolveAsync: Missing required context information (CurrentGameState or SelectedTargets).");
                return new GameStateActionResult { Success = false, ErrorMessage = "Missing required context." };
            }

            var gameState = context.CurrentGameState;
            var results = new List<IndividualActionResult>();
            bool overallSuccess = true;
            string? firstErrorMessage = null;
            const int EVOLVE_COST = 1; // Standard EP cost for evolution

            // 2. Loop through targets (InstanceIds of units to evolve)
            foreach (var target in context.SelectedTargets)
            {
                string instanceIdToEvolve = target.Id;
                _logger.LogDebug("Attempting to evolve unit with InstanceId: {InstanceId}", instanceIdToEvolve);

                // 3. Find the unit instance
                var targetInstance = gameState.FindCardInstanceById(instanceIdToEvolve);
                if (targetInstance == null)
                {
                    _logger.LogWarning("HandleEvolveAsync: Target unit instance with ID {InstanceId} not found.", instanceIdToEvolve);
                    results.Add(new IndividualActionResult { TargetId = target, Success = false, Message = "Target unit not found." });
                    overallSuccess = false;
                    if (firstErrorMessage == null) firstErrorMessage = "Target unit not found.";
                    continue;
                }

                // 4. Check if the unit is on the field and is a follower
                if (targetInstance.CurrentZone != CardZone.Field)
                {
                    _logger.LogWarning("HandleEvolveAsync: Target unit {InstanceId} is not on the field (Zone: {Zone}). Cannot evolve.", instanceIdToEvolve, targetInstance.CurrentZone);
                    results.Add(new IndividualActionResult { TargetId = target, Success = false, Message = $"Unit not on field (Zone: {targetInstance.CurrentZone})." });
                    overallSuccess = false;
                    if (firstErrorMessage == null) firstErrorMessage = "Unit not on field.";
                    continue;
                }
                // Assuming IsFollower property exists and is correctly populated
                if (!targetInstance.IsFollower)
                {
                    _logger.LogWarning("HandleEvolveAsync: Target card {InstanceId} is not a follower. Cannot evolve.", instanceIdToEvolve);
                    results.Add(new IndividualActionResult { TargetId = target, Success = false, Message = "Target is not a follower." });
                    overallSuccess = false;
                    if (firstErrorMessage == null) firstErrorMessage = "Target is not a follower.";
                    continue;
                }


                // 5. Check if already evolved
                if (targetInstance.IsEvolved)
                {
                    _logger.LogInformation("HandleEvolveAsync: Target unit {InstanceId} is already evolved.", instanceIdToEvolve);
                    results.Add(new IndividualActionResult { TargetId = target, Success = false, Message = "Unit already evolved." });
                    overallSuccess = false; // Evolving an already evolved unit is considered a failure for this action
                    if (firstErrorMessage == null) firstErrorMessage = "Unit already evolved.";
                    continue;
                }

                // 6. Check if the unit *can* evolve (based on game rules/card properties)
                // Assuming CanEvolve property exists and reflects game rules (e.g., turn number, specific conditions met)
                if (!targetInstance.CanEvolve)
                {
                    _logger.LogWarning("HandleEvolveAsync: Target unit {InstanceId} cannot evolve at this time (CanEvolve is false).", instanceIdToEvolve);
                    results.Add(new IndividualActionResult { TargetId = target, Success = false, Message = "Unit cannot evolve currently." });
                    overallSuccess = false;
                    if (firstErrorMessage == null) firstErrorMessage = "Unit cannot evolve currently.";
                    continue;
                }

                // 7. Find the owner player and check EP cost
                string ownerPlayerId = targetInstance.OwnerPlayerId;
                PlayerStateDto? ownerPlayerState = gameState.GetPlayerState(ownerPlayerId);
                if (ownerPlayerState == null)
                {
                    _logger.LogError("HandleEvolveAsync: Owner PlayerState with ID {PlayerId} not found for unit {InstanceId}.", ownerPlayerId, instanceIdToEvolve);
                    results.Add(new IndividualActionResult { TargetId = target, Success = false, Message = "Internal error: Owner player not found." });
                    overallSuccess = false;
                    if (firstErrorMessage == null) firstErrorMessage = "Internal error: Owner player not found.";
                    continue;
                }

                if (ownerPlayerState.CurrentEP < EVOLVE_COST)
                {
                    _logger.LogWarning("HandleEvolveAsync: Player {PlayerId} does not have enough EP ({CurrentEP}/{RequiredEP}) to evolve unit {InstanceId}.",
                                     ownerPlayerId, ownerPlayerState.CurrentEP, EVOLVE_COST, instanceIdToEvolve);
                    results.Add(new IndividualActionResult { TargetId = target, Success = false, Message = $"Not enough EP (Have {ownerPlayerState.CurrentEP}, Need {EVOLVE_COST})." });
                    overallSuccess = false;
                    if (firstErrorMessage == null) firstErrorMessage = "Not enough EP.";
                    continue;
                }

                // 8. Perform Evolution
                // a. Deduct EP cost
                ownerPlayerState.CurrentEP -= EVOLVE_COST;

                // b. Set IsEvolved flag
                targetInstance.IsEvolved = true;

                // c. Apply Evolve Stats/Effects (IMPORTANT: This needs external data)
                //    Similar to Transform, the actual stat changes (Attack, Health, MaxHealth)
                //    and gaining/losing keywords upon evolution depend on the card's evolved definition.
                //    This dispatcher doesn't know those details. It should be handled by
                //    another system reacting to an 'UnitEvolved' event or similar.
                // TODO: Ensure a mechanism exists to apply evolved stats/effects based on DefinitionId.
                _logger.LogInformation("Unit {InstanceId} evolved successfully. EP Cost: {Cost}. Player {PlayerId} EP remaining: {RemainingEP}. (Note: Evolved stats/effects applied elsewhere).",
                                     instanceIdToEvolve, EVOLVE_COST, ownerPlayerId, ownerPlayerState.CurrentEP);

                // 9. Add result
                results.Add(new IndividualActionResult
                {
                    TargetId = target,
                    Success = true,
                    Message = $"Unit {instanceIdToEvolve} evolved successfully. EP cost: {EVOLVE_COST}."
                });

            } // End foreach target

            _logger.LogInformation("HandleEvolveAsync completed. Overall Success: {Success}", overallSuccess);

            await Task.CompletedTask; // Suppress CS1998
            return new GameStateActionResult
            {
                Success = overallSuccess,
                ErrorMessage = firstErrorMessage ?? string.Empty, // Handle potential null
                Results = results
            };
        }

        private async Task<GameStateActionResult> HandleSilenceAsync(AbilityContext context) // Updated signature
        {
            // TODO: 实现沉默逻辑 - Needs refactoring to use context
            return await Task.FromResult(new GameStateActionResult { Success = true });
        }

        private async Task<GameStateActionResult> HandleFreezeAsync(AbilityContext context) // Updated signature
        {
            // TODO: 实现冻结逻辑 - Needs refactoring to use context
            return await Task.FromResult(new GameStateActionResult { Success = true });
        }

        private async Task<GameStateActionResult> HandleEndTurnAsync(AbilityContext context) // Updated signature
        {
            // TODO: 实现结束回合逻辑 - Needs refactoring to use context
            return await Task.FromResult(new GameStateActionResult { Success = true });
        }

        private async Task<GameStateActionResult> HandleSkipTurnAsync(AbilityContext context) // Updated signature
        {
            // TODO: 实现跳过回合逻辑 - Needs refactoring to use context
            return await Task.FromResult(new GameStateActionResult { Success = true });
        }

        private async Task<GameStateActionResult> HandleExtraTurnAsync(AbilityContext context) // Updated signature
        {
            // TODO: 实现额外回合逻辑 - Needs refactoring to use context
            return await Task.FromResult(new GameStateActionResult { Success = true });
        }

        private async Task<GameStateActionResult> HandleSetVariableAsync(AbilityContext context) // Updated signature
        {
            // TODO: 实现设置变量逻辑 - Needs refactoring to use context
            return await Task.FromResult(new GameStateActionResult { Success = true });
        }

        private async Task<GameStateActionResult> HandleModifyVariableAsync(AbilityContext context) // Updated signature
        {
            // TODO: 实现修改变量逻辑 - Needs refactoring to use context
            return await Task.FromResult(new GameStateActionResult { Success = true });
        }
    }
}
