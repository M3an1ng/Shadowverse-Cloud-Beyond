using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Domain.Abilities.Common;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ShadowverseBeyond.Server.Domain.Abilities.Effects
{
    /// <summary>
    /// 值源解析器实现
    /// 根据M12设计文档实现，负责解析效果配置中的动态值
    /// </summary>
    public class ValueSourceResolver : IValueSourceResolver
    {
        private readonly ILogger<ValueSourceResolver> _logger;

        public ValueSourceResolver(ILogger<ValueSourceResolver> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 解析值源
        /// </summary>
        public async Task<object> ResolveAsync(ValueConfig valueConfig, ValueResolutionContext context)
        {
            try
            {
                return valueConfig.Type switch
                {
                    Common.ValueType.Static => ResolveStaticValue(valueConfig),
                    Common.ValueType.Dynamic => await ResolveDynamicValue(valueConfig, context),
                    Common.ValueType.Formula => await ResolveFormulaValue(valueConfig, context),
                    Common.ValueType.Variable => ResolveVariableValue(valueConfig, context),
                    _ => throw new NotSupportedException($"Value type {valueConfig.Type} is not supported")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving value: {ValueConfig}", JsonSerializer.Serialize(valueConfig));
                return valueConfig.StaticValue ?? 0;
            }
        }

        /// <summary>
        /// 解析为整数
        /// </summary>
        public async Task<int> ResolveAsIntAsync(ValueConfig valueConfig, ValueResolutionContext context)
        {
            var value = await ResolveAsync(valueConfig, context);
            return ConvertToInt(value);
        }

        /// <summary>
        /// 解析为字符串
        /// </summary>
        public async Task<string> ResolveAsStringAsync(ValueConfig valueConfig, ValueResolutionContext context)
        {
            var value = await ResolveAsync(valueConfig, context);
            return value?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// 解析为布尔值
        /// </summary>
        public async Task<bool> ResolveAsBoolAsync(ValueConfig valueConfig, ValueResolutionContext context)
        {
            var value = await ResolveAsync(valueConfig, context);
            return ConvertToBool(value);
        }

        /// <summary>
        /// 解析静态值
        /// </summary>
        private object ResolveStaticValue(ValueConfig valueConfig)
        {
            return valueConfig.StaticValue ?? 0;
        }

        /// <summary>
        /// 解析动态值
        /// </summary>
        private async Task<object> ResolveDynamicValue(ValueConfig valueConfig, ValueResolutionContext context)
        {
            var source = valueConfig.DynamicSource;

            if (string.IsNullOrEmpty(source))
                return 0;

            // 解析动态源路径，如 "source.attack", "target.health", "player.pp" 等
            var parts = source.Split('.');
            if (parts.Length < 2)
                return 0;

            var objectType = parts[0].ToLower();
            var property = parts[1].ToLower();

            return objectType switch
            {
                "source" => GetCardProperty(context.SourceCard, property),
                "target" => GetCardProperty(context.TargetCard, property),
                "player" => await GetPlayerProperty(context, property),
                "game" => await GetGameProperty(context, property),
                "event" => GetEventProperty(context.TriggeringEvent, property),
                _ => 0
            };
        }

        /// <summary>
        /// 解析公式值
        /// </summary>
        private async Task<object> ResolveFormulaValue(ValueConfig valueConfig, ValueResolutionContext context)
        {
            // TODO: 实现公式解析器
            // 这里可以使用表达式解析库或自定义解析器
            var formula = valueConfig.Formula;

            if (string.IsNullOrEmpty(formula))
            {
                await Task.CompletedTask; // Suppress CS1998
                return 0;
            }

            // 简单的公式解析示例
            // 实际实现应该更复杂，支持各种数学运算和函数
            if (formula.Contains("source.attack"))
            {
                var sourceAttack = GetCardProperty(context.SourceCard, "attack");
                var formulaValue = formula.Replace("source.attack", sourceAttack.ToString());

                // 这里应该使用真正的表达式求值器
                // 暂时返回简单的值
                await Task.CompletedTask; // Suppress CS1998
                return ConvertToInt(sourceAttack);
            }

            await Task.CompletedTask; // Suppress CS1998
            return 0;
        }

        /// <summary>
        /// 解析变量值
        /// </summary>
        private object ResolveVariableValue(ValueConfig valueConfig, ValueResolutionContext context)
        {
            var variableName = valueConfig.DynamicSource;

            if (string.IsNullOrEmpty(variableName))
                return 0;

            // Use TryGetValue for dictionary access
            return context.Variables.TryGetValue(variableName, out var value) ? value : 0;
        }

        /// <summary>
        /// 获取卡牌属性
        /// </summary>
        private object GetCardProperty(CardInstance? card, string property)
        {
            if (card == null)
                return 0;

            return property.ToLower() switch
            {
                "attack" => card.CurrentAttack,
                "health" => card.CurrentHealth,
                "maxhealth" => card.MaxHealth,
                "cost" => card.CurrentCost,
                "id" => card.InstanceId,
                "definitionid" => card.DefinitionId,
                "ownerid" => card.OwnerPlayerId,
                _ => 0
            };
        }

        /// <summary>
        /// 获取玩家属性
        /// </summary>
        private async Task<object> GetPlayerProperty(ValueResolutionContext context, string property)
        {
            var player = context.SourcePlayerId == context.CurrentGameState.Player1.PlayerId
                ? context.CurrentGameState.Player1
                : context.CurrentGameState.Player2;

            return property.ToLower() switch
            {
                "pp" => player.CurrentPlayPoints,
                "maxpp" => player.MaxPlayPoints,
                "ep" => player.CurrentEvolutionPoints,
                "maxep" => player.MaxEvolutionPoints,
                "handcount" => player.Hand.Count,
                "deckcount" => player.Deck.Count,
                "fieldcount" => player.Field.Count,
                "graveyardcount" => player.Graveyard.Count,
                _ => await Task.FromResult(0)
            };
        }

        /// <summary>
        /// 获取游戏属性
        /// </summary>
        private async Task<object> GetGameProperty(ValueResolutionContext context, string property)
        {
            return property.ToLower() switch
            {
                "turn" => context.CurrentGameState.TurnNumber,
                "currentplayerid" => context.CurrentGameState.CurrentTurnPlayerId,
                "phase" => context.CurrentGameState.Phase.ToString(),
                "status" => context.CurrentGameState.Status.ToString(),
                _ => await Task.FromResult(0)
            };
        }

        /// <summary>
        /// 获取事件属性
        /// </summary>
        private object GetEventProperty(object? eventObj, string property)
        {
            if (eventObj == null)
                return 0;

            // TODO: 根据事件类型解析属性
            // 这里需要根据具体的事件类型来实现
            return 0;
        }

        /// <summary>
        /// 转换为整数
        /// </summary>
        private int ConvertToInt(object value)
        {
            return value switch
            {
                int i => i,
                long l => (int)l,
                float f => (int)f,
                double d => (int)d,
                string s when int.TryParse(s, out var result) => result,
                bool b => b ? 1 : 0,
                _ => 0
            };
        }

        /// <summary>
        /// 转换为布尔值
        /// </summary>
        private bool ConvertToBool(object value)
        {
            return value switch
            {
                bool b => b,
                int i => i != 0,
                long l => l != 0,
                float f => f != 0,
                double d => d != 0,
                string s => !string.IsNullOrEmpty(s) && s.ToLower() != "false" && s != "0",
                _ => false
            };
        }
    }
}
