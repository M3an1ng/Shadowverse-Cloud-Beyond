-- CardAbility测试数据
-- 为基础卡牌添加能力配置，用于测试能力系统的完整流程

-- 1. Goblin (CardId: 1001) - 简单的入场曲能力
INSERT INTO CardAbilities (CardId, AbilityTag, Priority, IsActive, DslExpression, JsonConfiguration, CreatedAt, UpdatedAt)
VALUES (
    1001,
    'Fanfare',
    1,
    1,
    'count(enemy_followers) > 0',
    '{
        "id": "goblin_fanfare",
        "name": "Goblin Fanfare",
        "type": "Triggered",
        "abilityTag": "Fanfare",
        "trigger": {
            "eventTypes": ["CardPlayed"],
            "scope": "Self"
        },
        "conditions": [
            {
                "type": "DslExpression",
                "expression": "count(enemy_followers) > 0"
            }
        ],
        "targets": [
            {
                "type": "DslSelector",
                "expression": "select_random(enemy_followers, 1)"
            }
        ],
        "effects": [
            {
                "type": "DealDamage",
                "amount": {
                    "type": "Fixed",
                    "value": 1
                }
            }
        ]
    }',
    datetime('now'),
    datetime('now')
);

-- 2. Fighter (CardId: 1002) - 攻击时触发的能力
INSERT INTO CardAbilities (CardId, AbilityTag, Priority, IsActive, DslExpression, JsonConfiguration, CreatedAt, UpdatedAt)
VALUES (
    1002,
    'OnAttack',
    1,
    1,
    'source.attack >= 2',
    '{
        "id": "fighter_attack_boost",
        "name": "Fighter Attack Boost",
        "type": "Triggered",
        "abilityTag": "OnAttack",
        "trigger": {
            "eventTypes": ["AttackDeclared"],
            "scope": "Self"
        },
        "conditions": [
            {
                "type": "DslExpression",
                "expression": "source.attack >= 2"
            }
        ],
        "targets": [
            {
                "type": "Self"
            }
        ],
        "effects": [
            {
                "type": "ModifyStats",
                "attackModifier": {
                    "type": "Fixed",
                    "value": 1
                },
                "healthModifier": {
                    "type": "Fixed",
                    "value": 0
                }
            }
        ]
    }',
    datetime('now'),
    datetime('now')
);

-- 3. Shield Guardian (CardId: 1005) - 防御能力
INSERT INTO CardAbilities (CardId, AbilityTag, Priority, IsActive, DslExpression, JsonConfiguration, CreatedAt, UpdatedAt)
VALUES (
    1005,
    'Ward',
    1,
    1,
    'true',
    '{
        "id": "shield_guardian_ward",
        "name": "Shield Guardian Ward",
        "type": "Passive",
        "abilityTag": "Ward",
        "trigger": {
            "eventTypes": ["CardPlayed"],
            "scope": "Self"
        },
        "conditions": [],
        "targets": [
            {
                "type": "Self"
            }
        ],
        "effects": [
            {
                "type": "GrantKeyword",
                "keyword": "Ward"
            }
        ]
    }',
    datetime('now'),
    datetime('now')
);

-- 4. Bellringer Angel (CardId: 1006) - 抽牌能力
INSERT INTO CardAbilities (CardId, AbilityTag, Priority, IsActive, DslExpression, JsonConfiguration, CreatedAt, UpdatedAt)
VALUES (
    1006,
    'Fanfare',
    1,
    1,
    'count(hand.cards) < 9',
    '{
        "id": "bellringer_angel_draw",
        "name": "Bellringer Angel Draw",
        "type": "Triggered",
        "abilityTag": "Fanfare",
        "trigger": {
            "eventTypes": ["CardPlayed"],
            "scope": "Self"
        },
        "conditions": [
            {
                "type": "DslExpression",
                "expression": "count(hand.cards) < 9"
            }
        ],
        "targets": [
            {
                "type": "Player",
                "playerType": "Owner"
            }
        ],
        "effects": [
            {
                "type": "DrawCards",
                "amount": {
                    "type": "Fixed",
                    "value": 1
                }
            }
        ]
    }',
    datetime('now'),
    datetime('now')
);

-- 5. Insight (CardId: 2001) - 法术抽牌
INSERT INTO CardAbilities (CardId, AbilityTag, Priority, IsActive, DslExpression, JsonConfiguration, CreatedAt, UpdatedAt)
VALUES (
    2001,
    'Spell',
    1,
    1,
    'count(hand.cards) < 8',
    '{
        "id": "insight_draw",
        "name": "Insight Draw",
        "type": "Triggered",
        "abilityTag": "Spell",
        "trigger": {
            "eventTypes": ["CardPlayed"],
            "scope": "Self"
        },
        "conditions": [
            {
                "type": "DslExpression",
                "expression": "count(hand.cards) < 8"
            }
        ],
        "targets": [
            {
                "type": "Player",
                "playerType": "Owner"
            }
        ],
        "effects": [
            {
                "type": "DrawCards",
                "amount": {
                    "type": "Fixed",
                    "value": 2
                }
            }
        ]
    }',
    datetime('now'),
    datetime('now')
);

-- 6. Angelic Snipe (CardId: 2002) - 法术伤害
INSERT INTO CardAbilities (CardId, AbilityTag, Priority, IsActive, DslExpression, JsonConfiguration, CreatedAt, UpdatedAt)
VALUES (
    2002,
    'Spell',
    1,
    1,
    'count(enemy_followers) > 0',
    '{
        "id": "angelic_snipe_damage",
        "name": "Angelic Snipe Damage",
        "type": "Triggered",
        "abilityTag": "Spell",
        "trigger": {
            "eventTypes": ["CardPlayed"],
            "scope": "Self"
        },
        "conditions": [
            {
                "type": "DslExpression",
                "expression": "count(enemy_followers) > 0"
            }
        ],
        "targets": [
            {
                "type": "DslSelector",
                "expression": "select_any(enemy_followers, 1)"
            }
        ],
        "effects": [
            {
                "type": "DealDamage",
                "amount": {
                    "type": "Fixed",
                    "value": 3
                }
            }
        ]
    }',
    datetime('now'),
    datetime('now')
);
