-- CardAbility测试数据
-- 为基础卡牌添加能力配置，用于测试能力系统的完整流程

-- 5. 智慧之光 (CardId: 2001)
INSERT INTO CardAbilities (CardId, Name, Description, AbilityTag, AbilityType, Priority, IsActive, JsonConfig, CreatedAt, UpdatedAt)
VALUES (
    2001,
    '智慧之光',
    'Draw a card.',
    'Fanfare',
    0,
    1,
    1,
    '{
        "id": "wisdom_of_light",
        "name": "智慧之光",
        "type": "Triggered",
        "abilityTag": "Fanfare",
        "trigger": {
            "eventTypes": ["PlayCard"],
            "relationType": "TargetSelf"
        },
        "conditions": [],
        "targets": [
            {
                "selectorType": "Filtered",
                "query": {
                    "sourcePoolExpression": "owner"
                }
            }
        ],
        "effects": [
            {
                "type": "Draw",
                "parameters": {
                    "drawAmount": { "value": 1 }
                }
            }
        ]
    }',
    datetime('now'),
    datetime('now')
);

-- 6. 天使之箭 (CardId: 2002)
INSERT INTO CardAbilities (CardId, Name, Description, AbilityTag, AbilityType, Priority, IsActive, JsonConfig, CreatedAt, UpdatedAt)
VALUES (
    2002,
    '天使之箭',
    'Deal 1 damage to an enemy follower or leader.',
    'Fanfare',
    0,
    1,
    1,
    '{
        "id": "angelic_arrow",
        "name": "天使之箭",
        "type": "Triggered",
        "abilityTag": "Fanfare",
        "trigger": {
            "eventTypes": ["PlayCard"],
            "relationType": "TargetSelf"
        },
        "conditions": [],
        "targets": [
            {
                "selectorType": "PlayerChoice",
                "playerChoiceOptions": {
                    "isRequired": true,
                    "minCount": 1,
                    "maxCount": 1,
                    "prompt": "选择一个目标造成1点伤害"
                },
                "requireValidTarget": true,
                "query": {
                    "sourcePoolExpression": "opponent.field",
                    "operations": [
                        {
                            "$type": "FilterOperation",
                            "filters": [
                                {
                                    "valueSource": {
                                        "$type": "FormulaValueConfig",
                                        "formula": "self.Type == \"Follower\" or self.Type == \"Leader\""
                                    }
                                }
                            ]
                        }
                    ]
                }
            }
        ],
        "effects": [
            {
                "type": "Damage",
                "parameters": {
                    "damageAmount": { "value": 1 }
                }
            }
        ]
    }',
    datetime('now'),
    datetime('now')
);
