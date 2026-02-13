//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
//
using System;
using System.Collections.Concurrent;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions
{
    /// <summary>
    /// DSL函数注册表的默认实现，线程安全
    /// </summary>
    public class FunctionRegistry : IFunctionRegistry
    {
        private readonly ConcurrentDictionary<string, IFunctionDefinition> _functions;
        private readonly ILogger<FunctionRegistry> _logger;

        public FunctionRegistry(ILogger<FunctionRegistry> logger)
        {
            _functions = new ConcurrentDictionary<string, IFunctionDefinition>(StringComparer.OrdinalIgnoreCase);
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // 注册核心函数
            RegisterCoreFunctions();
        }

        /// <summary>
        /// 根据函数名查找函数定义（不区分大小写）
        /// </summary>
        public IFunctionDefinition? GetFunction(string functionName)
        {
            if (string.IsNullOrWhiteSpace(functionName))
            {
                _logger.LogWarning("Attempted to get function with null or empty name");
                return null;
            }

            _functions.TryGetValue(functionName, out var function);

            if (function == null)
            {
                _logger.LogDebug("Function '{FunctionName}' not found in registry", functionName);
            }
            else
            {
                _logger.LogTrace("Function '{FunctionName}' found in registry", functionName);
            }

            return function;
        }

        /// <summary>
        /// 注册一个函数定义
        /// </summary>
        public void RegisterFunction(IFunctionDefinition function)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }

            if (string.IsNullOrWhiteSpace(function.Name))
            {
                throw new ArgumentException("Function name cannot be null or empty", nameof(function));
            }

            var wasAdded = _functions.TryAdd(function.Name, function);
            if (wasAdded)
            {
                _logger.LogDebug("Registered function: {FunctionName}", function.Name);
            }
            else
            {
                // 如果函数已存在，更新它
                _functions[function.Name] = function;
                _logger.LogWarning("Function '{FunctionName}' was already registered and has been replaced", function.Name);
            }
        }

        /// <summary>
        /// 批量注册函数定义
        /// </summary>
        public void RegisterFunctions(IEnumerable<IFunctionDefinition> functions)
        {
            if (functions == null)
            {
                throw new ArgumentNullException(nameof(functions));
            }

            var functionList = functions.ToList();
            foreach (var function in functionList)
            {
                RegisterFunction(function);
            }

            _logger.LogInformation("Registered {Count} functions", functionList.Count);
        }

        /// <summary>
        /// 获取所有已注册的函数名
        /// </summary>
        public IEnumerable<string> GetRegisteredFunctionNames()
        {
            return _functions.Keys.ToList();
        }

        /// <summary>
        /// 检查指定函数是否已注册
        /// </summary>
        public bool IsRegistered(string functionName)
        {
            if (string.IsNullOrWhiteSpace(functionName))
            {
                return false;
            }

            return _functions.ContainsKey(functionName);
        }

        /// <summary>
        /// 移除指定的函数注册
        /// </summary>
        public bool UnregisterFunction(string functionName)
        {
            if (string.IsNullOrWhiteSpace(functionName))
            {
                return false;
            }

            var removed = _functions.TryRemove(functionName, out var removedFunction);
            if (removed && removedFunction != null)
            {
                _logger.LogDebug("Unregistered function: {FunctionName}", functionName);
            }

            return removed;
        }

        /// <summary>
        /// 注册核心函数
        /// </summary>
        private void RegisterCoreFunctions()
        {
            var coreFunctions = new List<IFunctionDefinition>
            {
                new CountFunction(),
                new ByTypeFunction(),
                new FindFunction(),
                new MaxFunction(),
                new MinFunction(),
                new SumFunction(),
                new AbsFunction(),
                new IfFunction(),
                new ContainsFunction(),
                new IsEmptyFunction(),
                new VarFunction(),
                new EventIntFunction(),
                new EventBoolFunction(),
                new EventStringFunction(),
                new EventGuidFunction(),
                new EventGuidListFunction(),
                new EventCardFunction(),
                new EventCardsFunction()
            };

            RegisterFunctions(coreFunctions);
            _logger.LogInformation("Registered {Count} core functions", coreFunctions.Count);
        }
    }
}
