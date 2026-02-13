using System.ComponentModel;

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
        [Description("无效果")]
        None = 0,

        /// <summary>
        /// 设置结果 - 可能用于强制设置战斗结果或游戏状态
        /// ❓ 具体用途需要确认
        /// </summary>
        [Description("设置结果")]
        SetResult = 1,

        /// <summary>
        /// 调度卡牌 - 系统操作
        /// </summary>
        [Description("调度卡牌")]
        Mulligan = 2,

        /// <summary>
        /// 回合开始 - 系统操作
        /// </summary>
        [Description("回合开始")]
        TurnStart = 3,

        /// <summary>
        /// 回合结束 - 系统操作
        /// </summary>
        [Description("回合结束")]
        TurnEnd = 4,

        /// <summary>
        /// 使用卡牌 - 系统操作
        /// </summary>
        [Description("使用卡牌")]
        Play = 5,

        /// <summary>
        /// 覆盖效果 - 可能用于覆盖其他效果或状态
        /// ❓ 具体机制需要确认
        /// </summary>
        [Description("覆盖效果")]
        Cover = 6,

        /// <summary>
        /// 随从攻击 - 系统操作
        /// </summary>
        [Description("攻击")]
        Attack = 7,

        /// <summary>
        /// 进化 - 系统操作
        /// </summary>
        [Description("进化")]
        Evolve = 8,

        /// <summary>
        /// 融合效果 - 卡牌融合机制
        /// ⚠️ 需要融合系统支持
        /// </summary>
        [Description("融合")]
        Fusion = 9,

        /// <summary>
        /// 启动效果 - 主动激活的能力
        /// ⚠️ 需要启动能力系统
        /// </summary>
        [Description("启动")]
        Act = 10,

        /// <summary>
        /// 激奏效果 - Shadowverse的Accelerate机制
        /// ⚠️ 需要激奏系统支持
        /// </summary>
        [Description("激奏")]
        Accelerate = 11,

        /// <summary>
        /// 结晶效果 - Shadowverse的Crystallize机制
        /// ⚠️ 需要结晶系统支持
        /// </summary>
        [Description("结晶")]
        Crystalize = 12,

        /// <summary>
        /// 设置攻击次数 - 修改单位每回合可攻击次数
        /// 🔄 可实现：修改CardInstance攻击次数属性
        /// </summary>
        [Description("设置攻击次数")]
        SetAttackTimes = 13,

        /// <summary>
        /// 超进化 - 触发超进化流程
        /// 🔄 可实现：调用超进化流程
        /// </summary>
        [Description("超进化")]
        SuperEvolve = 14,

        /// <summary>
        /// 重置攻击次数 - 重置单位本回合已攻击次数
        /// 🔄 可实现：清空AttacksThisTurn
        /// </summary>
        [Description("重置攻击次数")]
        ResetAttackTimes = 15,

        // ==================== 特殊控制效果 (90-99) ====================

        /// <summary>
        /// 改变标志 - 修改游戏状态标志或条件
        /// ❓ 具体标志类型需要确认
        /// </summary>
        [Description("改变标志")]
        ChangeFlag = 90,

        /// <summary>
        /// 随机效果 - 执行随机选择或随机数值效果
        /// 🔄 可实现：基于随机数生成器的效果处理
        /// </summary>
        [Description("随机")]
        Random = 91,

        /// <summary>
        /// 改写事件参数 - Before阶段用于改写触发事件的参数
        /// ⚠️ 仅用于Before拦截机制
        /// </summary>
        [Description("改写事件参数")]
        RewriteEventParams = 92,

        /// <summary>
        /// 替换触发事件 - Before阶段用于替换触发事件类型
        /// ⚠️ 仅用于Before拦截机制
        /// </summary>
        [Description("替换事件")]
        ReplaceEvent = 93,

        /// <summary>
        /// 阻止触发事件 - Before阶段用于阻止当前触发事件继续执行
        /// ⚠️ 仅用于Before拦截机制
        /// </summary>
        [Description("阻止事件")]
        PreventEvent = 95,

        /// <summary>
        /// 依序效果 - 按顺序或逆序选择若干分支并触发对应连锁能力
        /// 🔄 可实现：基于决策事件系统执行有状态游标选择
        /// </summary>
        [Description("依序")]
        Sequence = 94,

        // ==================== 核心游戏效果 (100-110) ====================

        /// <summary>
        /// 设置关键字 - 为卡牌添加或移除关键字能力
        /// ✅ 已实现：SetKeywordEffectHandler
        /// </summary>
        [Description("设置关键字")]
        SetKeyword = 100,

        /// <summary>
        /// 抽牌效果 - 从牌库抽取卡牌到手牌
        /// ✅ 已实现：DrawCardEffectHandler
        /// </summary>
        [Description("抽牌")]
        Draw = 101,

        /// <summary>
        /// 召唤效果 - 召唤单位到战场
        /// ✅ 已实现：SummonUnitEffectHandler
        /// </summary>
        [Description("召唤")]
        Summon = 102,

        /// <summary>
        /// 伤害效果 - 对目标造成伤害
        /// ✅ 已实现：DamageEffectHandler
        /// </summary>
        [Description("伤害")]
        Damage = 103,

        /// <summary>
        /// 治疗效果 - 恢复目标生命值
        /// ✅ 已实现：HealEffectHandler
        /// </summary>
        [Description("治疗")]
        Heal = 104,

        /// <summary>
        /// 摧毁效果 - 直接摧毁目标单位
        /// ✅ 已实现：DestroyUnitEffectHandler
        /// </summary>
        [Description("破坏")]
        Destroy = 105,

        /// <summary>
        /// 消失效果 - 使目标消失（不触发死亡效果）
        /// 🔄 可实现：类似Destroy但跳过LastWords
        /// </summary>
        [Description("消失")]
        Vanish = 106,

        /// <summary>
        /// 返回效果 - 将目标返回到手牌或牌库
        /// 🔄 可实现：移动卡牌到指定区域
        /// </summary>
        [Description("回手")]
        Return = 107,

        /// <summary>
        /// 洗回效果 - 将目标洗回牌库并洗牌
        /// 🔄 可实现：移动卡牌到牌库并洗牌
        /// </summary>
        [Description("洗回")]
        Shuffle = 141,

        /// <summary>
        /// 洗回牌库效果 - 将目标从战场洗回牌库
        /// 🔄 可实现：移动卡牌到牌库
        /// </summary>
        [Description("洗回牌库")]
        Tuck = 142,

        /// <summary>
        /// 变形效果 - 将目标变形为其他卡牌
        /// ✅ 已实现：TransformEffectHandler
        /// </summary>
        [Description("变形")]
        Transform = 108,

        /// <summary>
        /// 修改状态 - 修改目标的属性状态
        /// ✅ 部分实现：ModifyStatsEffectHandler（可扩展）
        /// </summary>
        [Description("修改状态")]
        ModifyStatus = 109,

        /// <summary>
        /// 设置属性 - 将目标的属性设置为指定值
        /// 🔄 可实现：基于StatChangedEvent的Set层修正
        /// </summary>
        [Description("设置状态")]
        SetStatus = 143,

        /// <summary>
        /// 调整生命上限 - 仅修改最大生命值，且不会抬高当前生命值
        /// ✅ 已实现：AdjustMaxHealthEffectHandler
        /// </summary>
        [Description("调整生命上限")]
        AdjustMaxHealth = 149,

        /// <summary>
        /// 移除Buff - 移除目标身上的增益效果
        /// 🔄 可实现：基于现有Buff系统
        /// </summary>
        [Description("移除增益")]
        RemoveBuff = 110,

        // ==================== 卡牌操作效果 (111-120) ====================

        /// <summary>
        /// 获得衍生物 - 生成特定的衍生卡牌到手牌
        /// ✅ 已实现：基于GetTokenEffectHandler
        /// </summary>
        [Description("获得衍生物")]
        GetToken = 111,

        /// <summary>
        /// 法术增强 - Shadowverse的SpellBoost机制
        /// ⚠️ 需要SpellBoost系统支持
        /// </summary>
        [Description("魔力增幅")]
        SpellBoost = 112,

        /// <summary>
        /// 设置费用 - 修改卡牌的费用
        /// 🔄 可实现：修改CardInstance.CurrentCost
        /// </summary>
        [Description("设置费用")]
        SetCost = 113,

        /// <summary>
        /// 增加PP上限 - 永久增加玩家PP上限（类似炉石的水晶）
        /// ✅ 已实现：ModifyPPMaxEffectHandler
        /// </summary>
        [Description("增加PP上限")]
        Ramp = 114,

        /// <summary>
        /// 添加效果 - 为目标添加新的能力效果
        /// ⚠️ 需要动态能力系统支持
        /// </summary>
        [Description("添加效果")]
        AddEffect = 115,

        /// <summary>
        /// 授予能力 - 为目标授予能力配置
        /// 🔄 可实现：基于Buff系统的AbilityBuffEffect
        /// </summary>
        [Description("授予能力")]
        GrantAbility = 144,

        /// <summary>
        /// 添加种类 - 为目标附加额外种类（Tribe）Buff
        /// ✅ 已实现：AddTribeEffectHandler
        /// </summary>
        [Description("添加种类")]
        AddTribe = 150,

        /// <summary>
        /// 添加光环 - 注册可刷新的持续效果
        /// 🔄 可实现：基于AuraManager与Buff系统
        /// </summary>
        [Description("添加光环")]
        AddAura = 145,

        /// <summary>
        /// 修改变量 - 修改玩家或卡牌的计数器/临时变量
        /// 🔄 可实现：基于Counters系统
        /// </summary>
        [Description("修改变量")]
        ModifyVariable = 146,

        /// <summary>
        /// 移除能力 - 抑制目标的能力
        /// 🔄 可实现：基于Buff系统的AbilitySuppressionBuffEffect
        /// </summary>
        [Description("移除能力")]
        RemoveAbility = 147,

        /// <summary>
        /// 移除效果 - 移除目标的特定能力效果
        /// ⚠️ 需要动态能力系统支持
        /// </summary>
        [Description("移除效果")]
        RemoveEffect = 116,

        /// <summary>
        /// 设置墓地 - 操作墓地中的卡牌
        /// 🔄 可实现：移动卡牌到墓地区域
        /// </summary>
        [Description("设置墓地")]
        SetGrave = 117,

        /// <summary>
        /// 设置倒计时 - 修改护符的倒计时数值
        /// 🔄 可实现：修改CardInstance.Countdown
        /// </summary>
        [Description("设置倒计时")]
        SetCountdown = 118,

        /// <summary>
        /// 添加到牌库 - 将卡牌添加到牌库
        /// 🔄 可实现：类似GetTokenEffectHandler但目标是牌库
        /// </summary>
        [Description("添加到牌库")]
        AddDeck = 119,

        /// <summary>
        /// 添加纹章 - 将纹章卡牌添加到纹章区
        /// 🔄 可实现：类似AddDeckEffectHandler但目标是纹章区
        /// </summary>
        [Description("添加纹章")]
        AddEmblem = 136,

        /// <summary>
        /// 土之印增加 - 增加土之印计数或生成土之印护符
        /// ⚠️ 需要土之印系统支持
        /// </summary>
        [Description("添加土之印")]
        AddEarthSigil = 137,

        /// <summary>
        /// 土之秘术 - 消耗土之印计数
        /// ⚠️ 需要土之秘术系统支持
        /// </summary>
        [Description("土之秘术")]
        EarthRite = 138,

        /// <summary>
        /// 死灵术 - 消耗墓场计数
        /// 🔄 可实现：消耗玩家墓场计数
        /// </summary>
        [Description("死灵术")]
        Necromancy = 139,

        /// <summary>
        /// 血祭 - 消耗血池计数
        /// 🔄 可实现：消耗玩家血池计数
        /// </summary>
        [Description("血祭")]
        Sacrifice = 140,

        /// <summary>
        /// 混合效果 - 复合型效果，可能包含多种子效果
        /// ⚠️ 需要复合效果处理系统
        /// </summary>
        [Description("混合效果")]
        Hybrid = 120,

        // ==================== 高级游戏机制 (121-134) ====================

        /// <summary>
        /// 埋葬效果 - Shadowverse的Bury机制，将卡牌放入特殊区域
        /// ⚠️ 需要埋葬系统支持
        /// </summary>
        [Description("葬送")]
        Bury = 121,

        /// <summary>
        /// 复活效果 - Shadowverse的Reanimate机制，从墓地复活单位
        /// ⚠️ 需要复活系统支持
        /// </summary>
        [Description("复活")]
        Reanimate = 122,

        /// <summary>
        /// 弃牌效果 - 强制弃置手牌
        /// 🔄 可实现：移动手牌到墓地
        /// </summary>
        [Description("弃牌")]
        Discard = 123,

        /// <summary>
        /// 发现效果 - Shadowverse的发现机制
        /// ⚠️ 需要发现系统支持
        /// </summary>
        [Description("发现")]
        Discover = 124,

        /// <summary>
        /// 展示效果 - 展示手牌或牌库中的卡牌
        /// 🔄 可实现：临时显示卡牌信息
        /// </summary>
        [Description("展示")]
        Reveal = 125,

        /// <summary>
        /// 设置伤害值 - 修改伤害相关的数值或状态
        /// ❓ 具体机制需要确认
        /// </summary>
        [Description("设置伤害")]
        SetDamage = 126,

        /// <summary>
        /// 设置治疗值 - 修改治疗相关的数值或状态
        /// ❓ 具体机制需要确认
        /// </summary>
        [Description("设置治疗")]
        SetHeal = 127,

        /// <summary>
        /// 设置离场效果 - 修改单位离场时的行为
        /// ❓ 具体机制需要确认
        /// </summary>
        [Description("设置离场")]
        SetLeave = 128,

        /// <summary>
        /// 设置PP - 直接设置玩家当前PP值
        /// ✅ 已实现：ModifyPPEffectHandler（可扩展为直接设置）
        /// </summary>
        [Description("设置PP")]
        SetPP = 129,

        /// <summary>
        /// 设置EP - 直接设置玩家当前EP值
        /// ✅ 已实现：ModifyEPEffectHandler（可扩展为直接设置）
        /// </summary>
        [Description("设置EP")]
        SetEP = 130,

        /// <summary>
        /// 设置SEP - 直接设置玩家当前SEP值
        /// ✅ 已实现：ModifySEPEffectHandler（可扩展为直接设置）
        /// </summary>
        [Description("设置SEP")]
        SetSEP = 148,

        /// <summary>
        /// 设置EP上限 - 修改玩家最大EP值
        /// ✅ 已实现：ModifyEPMaxEffectHandler
        /// </summary>
        [Description("设置EP上限")]
        SetEPMax = 135,

        /// <summary>
        /// 设置充能 - 修改单位的充能状态或数值
        /// ❓ 具体充能机制需要确认
        /// </summary>
        [Description("设置充能")]
        SetCharge = 131,

        /// <summary>
        /// 设置数值 - 通用的数值设置效果
        /// 🔄 可实现：通用数值修改器
        /// </summary>
        [Description("设置数值")]
        SetValue = 132,

        /// <summary>
        /// 抉择效果 - 让玩家进行选择的交互效果
        /// ⚠️ 需要玩家选择交互系统
        /// </summary>
        [Description("抉择")]
        Choose = 133,

        /// <summary>
        /// 添加Buff - 为目标添加增益效果
        /// 🔄 可实现：基于现有Buff系统
        /// </summary>
        [Description("添加增益")]
        AddBuff = 134,
    }
}
