using System.Collections.Generic;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Entities; // Assuming CardDefinition is here

namespace ShadowverseBeyond.Server.Domain.Interfaces
{
    /// <summary>
    /// 定义访问卡牌定义数据的仓库接口。
    /// 卡牌定义通常是静态的，从数据库或配置文件加载。
    /// </summary>
    public interface ICardRepository
    {
        /// <summary>
        /// 异步获取所有卡牌定义。
        /// </summary>
        /// <returns>包含所有卡牌定义的只读列表。</returns>
        Task<IReadOnlyList<CardDefinition>> GetAllCardDefinitionsAsync();

        /// <summary>
        /// 异步根据卡牌定义 ID 获取单个卡牌定义。
        /// </summary>
        /// <param name="definitionId">卡牌定义的 ID。</param>
        /// <returns>找到的卡牌定义，如果未找到则返回 null。</returns>
        Task<CardDefinition?> GetCardDefinitionByIdAsync(int definitionId);

        /// <summary>
        /// 创建卡牌定义。
        /// </summary>
        Task<CardDefinition> AddAsync(CardDefinition definition);

        /// <summary>
        /// 更新卡牌定义。
        /// </summary>
        Task<CardDefinition?> UpdateAsync(CardDefinition definition);

        // 可以根据需要添加其他查询方法，例如按稀有度、类型等查询
    }
}
