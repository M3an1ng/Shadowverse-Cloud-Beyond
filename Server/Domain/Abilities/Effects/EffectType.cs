namespace ShadowverseBeyond.Server.Domain.Effects
{
    /// <summary>
    /// 定义游戏中可能发生的各种效果类型。
    /// 用于效果触发、处理和日志记录。
    ///
    /// 实现状态说明：
    /// ✅ = 已实现Handler
    /// 🔄 = 可基于现有架构快速实现
    /// ⚠️ = 需要额外系统支持
    /// ❓ = 功能不明确，需要确认
    /// </summary>
    public enum EffectType
    {
        // ==================== 基础控制效果 (0-20) ====================

        /// <summary>
        /// 无效果 - 占位符
        /// </summary>
        None = 0,

        /// <summary>
        /// 设置结果 - 可能用于强制设置战斗结果或游戏状态
        /// ❓ 具体用途需要确认
        /// </summary>
        SetResult = 1,

        /// <summary>
        /// 调度卡牌 - 系统操作
        /// </summary>
        Mulligan = 2,

        /// <summary>
        /// 回合开始 - 系统操作
        /// </summary>
        TurnStart = 3,

        /// <summary>
        /// 回合结束 - 系统操作
        /// </summary>
        TurnEnd = 4,

        /// <summary>
        /// 使用卡牌 - 系统操作
        /// </summary>
        Play = 5,

        /// <summary>
        /// 覆盖效果 - 可能用于覆盖其他效果或状态
        /// ❓ 具体机制需要确认
        /// </summary>
        Cover = 6,

        /// <summary>
        /// 随从攻击 - 系统操作
        /// </summary>
        Attack = 7,

        /// <summary>
        /// 进化 - 系统操作
        /// </summary>
        Evolve = 8,

        /// <summary>
        /// 融合效果 - 卡牌融合机制
        /// ⚠️ 需要融合系统支持
        /// </summary>
        Fusion = 9,

        /// <summary>
        /// 启动效果 - 主动激活的能力
        /// ⚠️ 需要启动能力系统
        /// </summary>
        Act = 10,

        /// <summary>
        /// 激奏效果 - Shadowverse的Accelerate机制
        /// ⚠️ 需要激奏系统支持
        /// </summary>
        Accelerate = 11,

        /// <summary>
        /// 结晶效果 - Shadowverse的Crystallize机制
        /// ⚠️ 需要结晶系统支持
        /// </summary>
        Crystalize = 12,

        /// <summary>
        /// 设置攻击次数 - 修改单位每回合可攻击次数
        /// 🔄 可实现：修改CardInstance攻击次数属性
        /// </summary>
        SetAttackTimes = 13,

        // ==================== 特殊控制效果 (90-99) ====================

        /// <summary>
        /// 改变标志 - 修改游戏状态标志或条件
        /// ❓ 具体标志类型需要确认
        /// </summary>
        ChangeFlag = 90,

        /// <summary>
        /// 随机效果 - 执行随机选择或随机数值效果
        /// 🔄 可实现：基于随机数生成器的效果处理
        /// </summary>
        Random = 91,

        // ==================== 核心游戏效果 (100-110) ====================

        /// <summary>
        /// 设置关键字 - 为卡牌添加或移除关键字能力
        /// ✅ 已实现：SetKeywordEffectHandler
        /// </summary>
        SetKeyword = 100,

        /// <summary>
        /// 抽牌效果 - 从牌库抽取卡牌到手牌
        /// ✅ 已实现：DrawCardEffectHandler
        /// </summary>
        Draw = 101,

        /// <summary>
        /// 召唤效果 - 召唤单位到战场
        /// ✅ 已实现：SummonUnitEffectHandler
        /// </summary>
        Summon = 102,

        /// <summary>
        /// 伤害效果 - 对目标造成伤害
        /// ✅ 已实现：DamageEffectHandler
        /// </summary>
        Damage = 103,

        /// <summary>
        /// 治疗效果 - 恢复目标生命值
        /// ✅ 已实现：HealEffectHandler
        /// </summary>
        Heal = 104,

        /// <summary>
        /// 摧毁效果 - 直接摧毁目标单位
        /// ✅ 已实现：DestroyUnitEffectHandler
        /// </summary>
        Destroy = 105,

        /// <summary>
        /// 消失效果 - 使目标消失（不触发死亡效果）
        /// 🔄 可实现：类似Destroy但跳过LastWords
        /// </summary>
        Vanish = 106,

        /// <summary>
        /// 返回效果 - 将目标返回到手牌或牌库
        /// 🔄 可实现：移动卡牌到指定区域
        /// </summary>
        Return = 107,

        /// <summary>
        /// 变形效果 - 将目标变形为其他卡牌
        /// ✅ 已实现：TransformEffectHandler
        /// </summary>
        Transform = 108,

        /// <summary>
        /// 设置状态 - 修改目标的属性状态
        /// ✅ 部分实现：ModifyStatsEffectHandler（可扩展）
        /// </summary>
        SetStatus = 109,

        /// <summary>
        /// 移除Buff - 移除目标身上的增益效果
        /// 🔄 可实现：基于现有Buff系统
        /// </summary>
        RemoveBuff = 110,

        // ==================== 卡牌操作效果 (111-120) ====================

        /// <summary>
        /// 获得衍生物 - 生成特定的衍生卡牌到手牌
        /// ✅ 已实现：基于AddCardToHandEffectHandler
        /// </summary>
        GetToken = 111,

        /// <summary>
        /// 法术增强 - Shadowverse的SpellBoost机制
        /// ⚠️ 需要SpellBoost系统支持
        /// </summary>
        SpellBoost = 112,

        /// <summary>
        /// 设置费用 - 修改卡牌的费用
        /// 🔄 可实现：修改CardInstance.CurrentCost
        /// </summary>
        SetCost = 113,

        /// <summary>
        /// 增加PP上限 - 永久增加玩家PP上限（类似炉石的水晶）
        /// ✅ 已实现：ModifyPPMaxEffectHandler
        /// </summary>
        Ramp = 114,

        /// <summary>
        /// 添加效果 - 为目标添加新的能力效果
        /// ⚠️ 需要动态能力系统支持
        /// </summary>
        AddEffect = 115,

        /// <summary>
        /// 移除效果 - 移除目标的特定能力效果
        /// ⚠️ 需要动态能力系统支持
        /// </summary>
        RemoveEffect = 116,

        /// <summary>
        /// 设置墓地 - 操作墓地中的卡牌
        /// 🔄 可实现：移动卡牌到墓地区域
        /// </summary>
        SetGrave = 117,

        /// <summary>
        /// 设置倒计时 - 修改护符的倒计时数值
        /// 🔄 可实现：修改CardInstance.Countdown
        /// </summary>
        SetCountdown = 118,

        /// <summary>
        /// 添加到牌库 - 将卡牌添加到牌库
        /// 🔄 可实现：类似AddCardToHandEffectHandler但目标是牌库
        /// </summary>
        AddDeck = 119,

        /// <summary>
        /// 混合效果 - 复合型效果，可能包含多种子效果
        /// ⚠️ 需要复合效果处理系统
        /// </summary>
        Hybrid = 120,

        // ==================== 高级游戏机制 (121-134) ====================

        /// <summary>
        /// 埋葬效果 - Shadowverse的Bury机制，将卡牌放入特殊区域
        /// ⚠️ 需要埋葬系统支持
        /// </summary>
        Bury = 121,

        /// <summary>
        /// 复活效果 - Shadowverse的Reanimate机制，从墓地复活单位
        /// ⚠️ 需要复活系统支持
        /// </summary>
        Reanimate = 122,

        /// <summary>
        /// 弃牌效果 - 强制弃置手牌
        /// 🔄 可实现：移动手牌到墓地
        /// </summary>
        Discard = 123,

        /// <summary>
        /// 发现效果 - Shadowverse的发现机制
        /// ⚠️ 需要发现系统支持
        /// </summary>
        Discover = 124,

        /// <summary>
        /// 展示效果 - 展示手牌或牌库中的卡牌
        /// 🔄 可实现：临时显示卡牌信息
        /// </summary>
        Reveal = 125,

        /// <summary>
        /// 设置伤害值 - 修改伤害相关的数值或状态
        /// ❓ 具体机制需要确认
        /// </summary>
        SetDamage = 126,

        /// <summary>
        /// 设置治疗值 - 修改治疗相关的数值或状态
        /// ❓ 具体机制需要确认
        /// </summary>
        SetHeal = 127,

        /// <summary>
        /// 设置离场效果 - 修改单位离场时的行为
        /// ❓ 具体机制需要确认
        /// </summary>
        SetLeave = 128,

        /// <summary>
        /// 设置PP - 直接设置玩家当前PP值
        /// ✅ 已实现：ModifyPPEffectHandler（可扩展为直接设置）
        /// </summary>
        SetPP = 129,

        /// <summary>
        /// 设置EP - 直接设置玩家当前EP值
        /// ✅ 已实现：ModifyEPEffectHandler（可扩展为直接设置）
        /// </summary>
        SetEP = 130,

        /// <summary>
        /// 设置EP上限 - 修改玩家最大EP值
        /// ✅ 已实现：ModifyEPMaxEffectHandler
        /// </summary>
        SetEPMax = 135,

        /// <summary>
        /// 设置充能 - 修改单位的充能状态或数值
        /// ❓ 具体充能机制需要确认
        /// </summary>
        SetCharge = 131,

        /// <summary>
        /// 设置数值 - 通用的数值设置效果
        /// 🔄 可实现：通用数值修改器
        /// </summary>
        SetValue = 132,

        /// <summary>
        /// 抉择效果 - 让玩家进行选择的交互效果
        /// ⚠️ 需要玩家选择交互系统
        /// </summary>
        Choose = 133,

        /// <summary>
        /// 添加Buff - 为目标添加增益效果
        /// 🔄 可实现：基于现有Buff系统
        /// </summary>
        AddBuff = 134,
    }
}
