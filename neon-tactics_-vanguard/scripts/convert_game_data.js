
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

// Polyfill for __dirname in ES modules
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Paths
const INPUT_PATH = path.join(__dirname, '../game_data.json');
const OUTPUT_DIR = path.join(__dirname, '../src/data');
const OUTPUT_PATH = path.join(OUTPUT_DIR, 'cardDatabase.json');

// Ensure output directory exists
if (!fs.existsSync(OUTPUT_DIR)){
    fs.mkdirSync(OUTPUT_DIR, { recursive: true });
}

console.log('Reading raw data from:', INPUT_PATH);

try {
    const rawData = fs.readFileSync(INPUT_PATH, 'utf8');
    const json = JSON.parse(rawData);

    // 1. Build Trait Map (ID -> Name)
    const traitMap = {};
    if (Array.isArray(json.ID)) {
        json.ID.forEach(item => {
            if (item.Tap === 'Trait') {
                traitMap[item.ID] = item.Name;
            }
        });
    }
    console.log(`Mapped ${Object.keys(traitMap).length} traits.`);

    // 2. Convert Cards
    const cardDatabase = {};
    let count = 0;

    if (Array.isArray(json.Effect)) {
        json.Effect.forEach(raw => {
            if (!raw.ID) return;

            // Collect effects
            const effects = [];
            for (let i = 1; i <= 9; i++) {
                const key = `Effect${i}`;
                if (raw[key] && raw[key].trim() !== "") {
                    effects.push(raw[key]);
                }
            }

            // Map Class
            const classMap = {
                'Forestcraft': 'Forestcraft',
                'Swordcraft': 'Swordcraft',
                'Runecraft': 'Runecraft',
                'Dragoncraft': 'Dragoncraft',
                'Abysscraft': 'Abysscraft',
                'Havencraft': 'Havencraft',
                'Portalcraft': 'Portalcraft',
                'Neutral': 'Neutral'
            };

            // Parse Numbers safely
            const safeNum = (val) => {
                const n = Number(val);
                return isNaN(n) ? 0 : n;
            };

            const cardModel = {
                id: String(raw.ID),
                name: raw.Name,
                cardSet: raw.CardSet,
                class: classMap[raw.Class] || 'Neutral',
                rarity: raw.Rarity || 'Bronze',
                type: raw.Type || 'Follower',
                trait: raw.Trait || "", // Keep original string if present
                cost: safeNum(raw.Cost),
                attack: safeNum(raw.Attack),
                defense: safeNum(raw.Defense),
                countdown: safeNum(raw.Countdown),
                effects: effects
            };

            cardDatabase[cardModel.id] = cardModel;
            count++;
        });
    }

    // 3. Write Output
    fs.writeFileSync(OUTPUT_PATH, JSON.stringify(cardDatabase, null, 2), 'utf8');
    console.log(`Successfully converted ${count} cards.`);
    console.log(`Database saved to: ${OUTPUT_PATH}`);

} catch (err) {
    console.error('Error converting data:', err);
    process.exit(1);
}
