//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
//
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions
{
    /// <summary>
    /// DSL函数注册表接口，负责管理和提供DSL函数定义
    /// </summary>
    public interface IFunctionRegistry
    {
        /// <summary>
        /// 根据函数名查找函数定义
        /// </summary>
        /// <param name="functionName">函数名（不区分大小写）</param>
        /// <returns>函数定义，如果未找到则返回null</returns>
        IFunctionDefinition? GetFunction(string functionName);

        /// <summary>
        /// 注册一个函数定义
        /// </summary>
        /// <param name="function">要注册的函数定义</param>
        void RegisterFunction(IFunctionDefinition function);

        /// <summary>
        /// 批量注册函数定义
        /// </summary>
        /// <param name="functions">要注册的函数定义集合</param>
        void RegisterFunctions(IEnumerable<IFunctionDefinition> functions);

        /// <summary>
        /// 获取所有已注册的函数名
        /// </summary>
        /// <returns>函数名集合</returns>
        IEnumerable<string> GetRegisteredFunctionNames();

        /// <summary>
        /// 检查指定函数是否已注册
        /// </summary>
        /// <param name="functionName">函数名</param>
        /// <returns>如果已注册返回true，否则返回false</returns>
        bool IsRegistered(string functionName);

        /// <summary>
        /// 移除指定的函数注册
        /// </summary>
        /// <param name="functionName">要移除的函数名</param>
        /// <returns>如果成功移除返回true，否则返回false</returns>
        bool UnregisterFunction(string functionName);
    }
}
