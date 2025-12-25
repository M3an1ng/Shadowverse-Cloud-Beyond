-- A fully compliant test insert statement for a single ability.
INSERT INTO CardAbilities (Id, CardId, Name, Description, AbilityTag, AbilityType, IsActive, Priority, JsonConfig, CreatedAt, UpdatedAt)
VALUES (
    999901,
    9999,
    'Test Fanfare',
    'A simple test fanfare ability that does nothing.',
    'Fanfare',
    1, -- Corresponds to AbilityType enum (e.g., 1 for Triggered)
    1, -- IsActive = true
    0, -- Priority
    '{
        "id": "test_fanfare_9999",
        "name": "Test Fanfare",
        "type": "Triggered",
        "abilityTag": "Fanfare",
        "trigger": {
            "eventTypes": ["CardPlayed"],
            "scope": "Self"
        },
        "effects": []
    }',
    datetime('now'),
    datetime('now')
);