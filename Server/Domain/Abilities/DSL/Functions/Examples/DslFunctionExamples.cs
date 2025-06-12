//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
//
using System.Text.Json;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions.Examples
{
    /// <summary>
    /// DSL函数在卡牌能力配置中的使用示例
    /// </summary>
    public static class DslFunctionExamples
    {
        /// <summary>
        /// 示例1：使用count函数的条件配置
        /// 条件：当场上友方随从数量大于等于3时触发
        /// </summary>
        public static string CountConditionExample => JsonSerializer.Serialize(new
        {
            Name = "群体强化",
            Description = "当场上有3个或更多友方随从时触发",
            TriggerType = "OnTurnStart",
            Conditions = new[]
            {
                new
                {
                    Type = "DslExpression",
                    Expression = "count(field.allies) >= 3"
                }
            },
            Effects = new[]
            {
                new
                {
                    Type = "ModifyStats",
                    Targets = new
                    {
                        SelectorType = "All",
                        Filter = "ally_followers"
                    },
                    Values = new
                    {
                        Attack = new { Type = "Static", Value = 1 },
                        Health = new { Type = "Static", Value = 1 }
                    }
                }
            }
        }, new JsonSerializerOptions { WriteIndented = true });

        /// <summary>
        /// 示例2：使用max函数的效果配置
        /// 效果：造成等于场上友方随从最高攻击力的伤害
        /// </summary>
        public static string MaxDamageEffectExample => JsonSerializer.Serialize(new
        {
            Name = "最强一击",
            Description = "造成等于场上友方随从最高攻击力的伤害",
            TriggerType = "OnPlay",
            Effects = new[]
            {
                new
                {
                    Type = "DealDamage",
                    Targets = new
                    {
                        SelectorType = "Single",
                        Filter = "enemy_followers"
                    },
                    Values = new
                    {
                        Damage = new 
                        { 
                            Type = "Formula", 
                            Formula = "max(field.allies, 'attack')" 
                        }
                    }
                }
            }
        }, new JsonSerializerOptions { WriteIndented = true });

        /// <summary>
        /// 示例3：使用min函数的治疗效果
        /// 效果：恢复等于场上友方随从最低生命值的生命
        /// </summary>
        public static string MinHealEffectExample => JsonSerializer.Serialize(new
        {
            Name = "同情治疗",
            Description = "恢复等于场上友方随从最低生命值的生命",
            TriggerType = "OnPlay",
            Effects = new[]
            {
                new
                {
                    Type = "RestoreHealth",
                    Targets = new
                    {
                        SelectorType = "Single",
                        Filter = "ally_leader"
                    },
                    Values = new
                    {
                        Amount = new 
                        { 
                            Type = "Formula", 
                            Formula = "min(field.allies, 'health')" 
                        }
                    }
                }
            }
        }, new JsonSerializerOptions { WriteIndented = true });

        /// <summary>
        /// 示例4：使用sum函数的复杂公式
        /// 效果：造成等于所有友方随从攻击力总和的伤害
        /// </summary>
        public static string SumAttackEffectExample => JsonSerializer.Serialize(new
        {
            Name = "集体冲锋",
            Description = "造成等于所有友方随从攻击力总和的伤害",
            TriggerType = "OnPlay",
            Conditions = new[]
            {
                new
                {
                    Type = "DslExpression",
                    Expression = "count(field.allies) > 0"
                }
            },
            Effects = new[]
            {
                new
                {
                    Type = "DealDamage",
                    Targets = new
                    {
                        SelectorType = "Single",
                        Filter = "enemy_leader"
                    },
                    Values = new
                    {
                        Damage = new 
                        { 
                            Type = "Formula", 
                            Formula = "sum(field.allies, 'attack')" 
                        }
                    }
                }
            }
        }, new JsonSerializerOptions { WriteIndented = true });

        /// <summary>
        /// 示例5：使用if函数的条件效果
        /// 效果：如果生命值低于一半则恢复5点生命，否则获得2点攻击力
        /// </summary>
        public static string ConditionalEffectExample => JsonSerializer.Serialize(new
        {
            Name = "适应性强化",
            Description = "根据当前生命值选择不同的强化效果",
            TriggerType = "OnPlay",
            Effects = new[]
            {
                new
                {
                    Type = "ConditionalEffect",
                    Targets = new
                    {
                        SelectorType = "Single",
                        Filter = "source"
                    },
                    Values = new
                    {
                        HealthRestore = new 
                        { 
                            Type = "Formula", 
                            Formula = "if(source.health < source.max_health / 2, 5, 0)" 
                        },
                        AttackBonus = new 
                        { 
                            Type = "Formula", 
                            Formula = "if(source.health >= source.max_health / 2, 2, 0)" 
                        }
                    }
                }
            }
        }, new JsonSerializerOptions { WriteIndented = true });

        /// <summary>
        /// 示例6：使用contains函数的条件检查
        /// 条件：检查手牌中是否包含特定类型的卡牌
        /// </summary>
        public static string ContainsConditionExample => JsonSerializer.Serialize(new
        {
            Name = "协同效应",
            Description = "当手牌中有法术卡时触发额外效果",
            TriggerType = "OnPlay",
            Conditions = new[]
            {
                new
                {
                    Type = "DslExpression",
                    Expression = "contains(hand.cards, 'spell')"
                }
            },
            Effects = new[]
            {
                new
                {
                    Type = "DrawCard",
                    Targets = new
                    {
                        SelectorType = "Single",
                        Filter = "source_player"
                    },
                    Values = new
                    {
                        Count = new { Type = "Static", Value = 1 }
                    }
                }
            }
        }, new JsonSerializerOptions { WriteIndented = true });

        /// <summary>
        /// 示例7：使用isEmpty函数的条件检查
        /// 条件：当手牌为空时触发
        /// </summary>
        public static string EmptyHandConditionExample => JsonSerializer.Serialize(new
        {
            Name = "绝境反击",
            Description = "当手牌为空时获得强大的效果",
            TriggerType = "OnTurnStart",
            Conditions = new[]
            {
                new
                {
                    Type = "DslExpression",
                    Expression = "isEmpty(hand.cards)"
                }
            },
            Effects = new[]
            {
                new
                {
                    Type = "ModifyStats",
                    Targets = new
                    {
                        SelectorType = "Single",
                        Filter = "source"
                    },
                    Values = new
                    {
                        Attack = new { Type = "Static", Value = 3 },
                        Health = new { Type = "Static", Value = 3 }
                    }
                }
            }
        }, new JsonSerializerOptions { WriteIndented = true });

        /// <summary>
        /// 示例8：复杂的DSL表达式组合
        /// 条件：当场上友方随从数量大于敌方且手牌数量少于3时
        /// </summary>
        public static string ComplexConditionExample => JsonSerializer.Serialize(new
        {
            Name = "战术优势",
            Description = "在特定战场条件下触发的复杂效果",
            TriggerType = "OnTurnEnd",
            Conditions = new[]
            {
                new
                {
                    Type = "DslExpression",
                    Expression = "count(field.allies) > count(field.enemies) AND count(hand.cards) < 3"
                }
            },
            Effects = new[]
            {
                new
                {
                    Type = "DrawCard",
                    Targets = new
                    {
                        SelectorType = "Single",
                        Filter = "source_player"
                    },
                    Values = new
                    {
                        Count = new
                        {
                            Type = "Formula",
                            Formula = "min(3, max(1, count(field.allies) - count(hand.cards)))"
                        }
                    }
                }
            }
        }, new JsonSerializerOptions { WriteIndented = true });

        /// <summary>
        /// 示例9：ApplyBuff效果与IsNonStackable参数
        /// 效果：应用一个不可叠加的攻击力增强Buff
        /// </summary>
        public static string NonStackableBuffExample => JsonSerializer.Serialize(new
        {
            Name = "独特强化",
            Description = "应用一个不可叠加的攻击力增强效果，新的会替换旧的",
            TriggerType = "OnPlay",
            Effects = new[]
            {
                new
                {
                    Type = "ApplyBuff",
                    Targets = new
                    {
                        SelectorType = "Single",
                        Filter = "source"
                    },
                    Values = new
                    {
                        BuffType = "ModifyAttack",
                        ModifyAttackAmount = 3,
                        Duration = "UntilEndOfTurn",
                        IsNonStackable = true  // 关键参数：标记为不可叠加
                    }
                }
            }
        }, new JsonSerializerOptions { WriteIndented = true });

        /// <summary>
        /// 示例10：可叠加的Buff效果（默认行为）
        /// 效果：应用一个可叠加的关键字Buff
        /// </summary>
        public static string StackableBuffExample => JsonSerializer.Serialize(new
        {
            Name = "叠加强化",
            Description = "应用一个可叠加的Rush关键字，多次使用会叠加",
            TriggerType = "OnPlay",
            Effects = new[]
            {
                new
                {
                    Type = "ApplyBuff",
                    Targets = new
                    {
                        SelectorType = "Single",
                        Filter = "source"
                    },
                    Values = new
                    {
                        BuffType = "AddKeyword",
                        Keyword = "Rush",
                        Duration = "Permanent",
                        IsNonStackable = false  // 明确标记为可叠加（也可以省略，默认为false）
                    }
                }
            }
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}
