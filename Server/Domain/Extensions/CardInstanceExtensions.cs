// File: Server/Domain/Extensions/CardInstanceExtensions.cs
//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 动态效果与Buff系统: 文档/05-游戏系统/10-动态效果与Buff系统.md
//

using System;
using System.Linq;
using ShadowverseBeyond.Server.Domain.Constants;
using ShadowverseBeyond.Server.Domain.Entities;
using ShadowverseBeyond.Server.Dtos;
using ShadowverseBeyond.Server.Dtos.Enums;

namespace ShadowverseBeyond.Server.Domain.Extensions
{
    /// <summary>
    /// CardInstance和CardInstanceDto的扩展方法
    /// 提供关键字检查和游戏逻辑相关的便捷方法
    /// </summary>
    public static class CardInstanceExtensions
    {
        // CardInstance扩展方法
        /// <summary>
        /// 检查CardInstance是否拥有Rush关键字
        /// </summary>
        /// <param name="cardInstance">卡牌实例</param>
        /// <returns>如果拥有Rush关键字则返回true</returns>
        public static bool HasRush(this CardInstance cardInstance)
        {
            return cardInstance.HasKeyword(CardKeyword.Rush);
        }

        /// <summary>
        /// 检查CardInstance是否拥有Storm关键字
        /// </summary>
        /// <param name="cardInstance">卡牌实例</param>
        /// <returns>如果拥有Storm关键字则返回true</returns>
        public static bool HasStorm(this CardInstance cardInstance)
        {
            return cardInstance.HasKeyword(CardKeyword.Storm);
        }

        /// <summary>
        /// 检查CardInstance是否拥有Ward关键字
        /// </summary>
        /// <param name="cardInstance">卡牌实例</param>
        /// <returns>如果拥有Ward关键字则返回true</returns>
        public static bool HasWard(this CardInstance cardInstance)
        {
            return cardInstance.HasKeyword(CardKeyword.Ward);
        }

        /// <summary>
        /// 检查CardInstance是否可以立即攻击（拥有Rush或Storm）
        /// </summary>
        /// <param name="cardInstance">卡牌实例</param>
        /// <returns>如果可以立即攻击则返回true</returns>
        public static bool CanAttackImmediately(this CardInstance cardInstance)
        {
            return cardInstance.Keywords.Any(keyword =>
                keyword == CardKeyword.Rush || keyword == CardKeyword.Storm);
        }

        // CardInstanceDto扩展方法（为了兼容现有代码）
        /// <summary>
        /// 检查CardInstanceDto是否拥有指定关键字
        /// </summary>
        /// <param name="cardDto">卡牌DTO</param>
        /// <param name="keyword">关键字枚举</param>
        /// <returns>如果拥有该关键字则返回true</returns>
        public static bool HasKeyword(this CardInstanceDto cardDto, CardKeyword keyword)
        {
            if (cardDto.Keywords == null)
                return false;

            return cardDto.Keywords.Contains(keyword);
        }

        /// <summary>
        /// 检查CardInstanceDto是否拥有指定关键字（字符串版本，用于向后兼容）
        /// </summary>
        /// <param name="cardDto">卡牌DTO</param>
        /// <param name="keyword">关键字名称</param>
        /// <returns>如果拥有该关键字则返回true</returns>
        public static bool HasKeyword(this CardInstanceDto cardDto, string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword) || cardDto.Keywords == null)
                return false;

            // 尝试将字符串转换为CardKeyword枚举
            if (Enum.TryParse<CardKeyword>(keyword, true, out var keywordEnum))
            {
                return cardDto.Keywords.Contains(keywordEnum);
            }

            return false;
        }

        /// <summary>
        /// 检查CardInstanceDto是否拥有Rush关键字
        /// </summary>
        /// <param name="cardDto">卡牌DTO</param>
        /// <returns>如果拥有Rush关键字则返回true</returns>
        public static bool HasRush(this CardInstanceDto cardDto)
        {
            return cardDto.HasKeyword(CardKeyword.Rush);
        }

        /// <summary>
        /// 检查CardInstanceDto是否拥有Storm关键字
        /// </summary>
        /// <param name="cardDto">卡牌DTO</param>
        /// <returns>如果拥有Storm关键字则返回true</returns>
        public static bool HasStorm(this CardInstanceDto cardDto)
        {
            return cardDto.HasKeyword(CardKeyword.Storm);
        }

        /// <summary>
        /// 检查CardInstanceDto是否拥有Ward关键字
        /// </summary>
        /// <param name="cardDto">卡牌DTO</param>
        /// <returns>如果拥有Ward关键字则返回true</returns>
        public static bool HasWard(this CardInstanceDto cardDto)
        {
            return cardDto.HasKeyword(CardKeyword.Ward);
        }

        /// <summary>
        /// 检查CardInstanceDto是否可以立即攻击（拥有Rush或Storm）
        /// </summary>
        /// <param name="cardDto">卡牌DTO</param>
        /// <returns>如果可以立即攻击则返回true</returns>
        public static bool CanAttackImmediately(this CardInstanceDto cardDto)
        {
            return cardDto.Keywords?.Any(keyword =>
                keyword == CardKeyword.Rush || keyword == CardKeyword.Storm) ?? false;
        }

        /// <summary>
        /// 检查CardInstanceDto是否可以攻击主战者
        /// </summary>
        /// <param name="cardDto">卡牌DTO</param>
        /// <returns>如果可以攻击主战者则返回true</returns>
        public static bool CanAttackLeader(this CardInstanceDto cardDto)
        {
            if (cardDto.HasKeyword(CardKeyword.Shackle) || cardDto.HasKeyword(CardKeyword.CannotAttackLeader))
                return false;

            if (cardDto.HasStorm())
                return true;

            return cardDto.ZoneStayTurn > 0;
        }

        /// <summary>
        /// 为CardInstanceDto设置基于关键字的初始攻击权限
        /// </summary>
        /// <param name="cardDto">卡牌DTO</param>
        /// <param name="justSummoned">是否刚刚召唤</param>
        /// <returns>是否可以攻击</returns>
        public static bool GetInitialCanAttack(this CardInstanceDto cardDto, bool justSummoned = true)
        {
            if (!justSummoned)
            {
                // 如果不是刚召唤，通常可以攻击（除非有其他限制）
                return true;
            }

            // 刚召唤的单位只有拥有Rush或Storm才能攻击
            return cardDto.CanAttackImmediately();
        }

        /// <summary>
        /// 检查是否可以攻击指定目标
        /// </summary>
        /// <param name="attacker">攻击者</param>
        /// <param name="target">目标</param>
        /// <param name="justSummoned">攻击者是否刚刚召唤</param>
        /// <returns>如果可以攻击则返回true</returns>
        public static bool CanAttackTarget(this CardInstanceDto attacker, CardInstanceDto target, bool justSummoned = false)
        {
            // 基础攻击权限检查
            if (!attacker.CanAttack)
                return false;

            // 如果刚召唤，检查是否有立即攻击能力
            if (justSummoned && !attacker.CanAttackImmediately())
                return false;

            // 如果目标是主战者，检查是否可以攻击主战者
            if (target.Type == CardType.Leader)
            {
                if (attacker.HasKeyword(CardKeyword.CannotAttackLeader))
                    return false;

                // 如果刚召唤且只有Rush（不是Storm），不能攻击主战者
                if (justSummoned && attacker.HasRush() && !attacker.HasStorm())
                    return false;
            }
            else if (target.Type == CardType.Follower && attacker.HasKeyword(CardKeyword.CannotAttackFollower))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 【BUG-017修复】检查是否可以执行攻击（包括攻击次数限制）
        /// 这是DTO级别的攻击前置条件检查，用于AI决策
        /// </summary>
        /// <param name="attacker">攻击者DTO</param>
        /// <param name="currentPlayerId">当前回合玩家ID</param>
        /// <param name="isCurrentPlayerTurn">是否是攻击者的回合</param>
        /// <returns>如果可以执行攻击则返回true</returns>
        public static bool CanPerformAttackDto(this CardInstanceDto attacker, Guid currentPlayerId, bool isCurrentPlayerTurn = true)
        {
            // 基础检查：单位必须能够攻击
            if (!attacker.CanAttack)
                return false;

            // 类型检查：仅随从或主战者可以攻击
            if (attacker.Type != CardType.Follower && attacker.Type != CardType.Leader)
                return false;

            // 回合检查：必须是攻击者的回合
            if (!isCurrentPlayerTurn || attacker.OwnerPlayerId != currentPlayerId)
                return false;

            // 攻击力检查：攻击力必须大于0
            if (attacker.CurrentAttack <= 0)
                return false;

            // 区域检查：必须在场上
            if (attacker.Zone != CardZone.Field)
                return false;

            // 【关键修复】攻击次数检查：检查是否已达到本回合最大攻击次数
            var maxAttacksPerTurn = Math.Max(0, attacker.MaxAttacksPerTurn);
            if (attacker.AttacksThisTurn >= maxAttacksPerTurn)
                return false;

            return true;
        }
    }
}
