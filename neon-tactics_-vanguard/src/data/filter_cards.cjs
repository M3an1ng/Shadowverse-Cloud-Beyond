const fs = require('fs');
const path = require('path');

// Read the card database
const cardDatabasePath = path.join(__dirname, 'cardDatabase.json');
const cardDatabase = JSON.parse(fs.readFileSync(cardDatabasePath, 'utf8'));

// Filter cards where cardSet is "神眷的棋盘"
const filteredCards = Object.values(cardDatabase).filter(card => card.cardSet === '星云基础');

// Group cards by class
const cardsByClass = {};
filteredCards.forEach(card => {
  if (!cardsByClass[card.class]) {
    cardsByClass[card.class] = {};
  }
  cardsByClass[card.class][card.id] = card;
});

// Write each class to a separate file
Object.keys(cardsByClass).forEach(className => {
  const outputPath = path.join(__dirname, `cardDatabase_${className}.json`);
  fs.writeFileSync(outputPath, JSON.stringify(cardsByClass[className], null, 2));
  console.log(`Created ${outputPath} with ${Object.keys(cardsByClass[className]).length} cards.`);
});

console.log('Filtering and splitting complete.');