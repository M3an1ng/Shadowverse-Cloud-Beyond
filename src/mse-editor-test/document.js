export const CLASSES = {
  Neutral: '中立', Forestcraft: '精灵', Swordcraft: '皇家护卫', Runecraft: '巫师',
  Dragoncraft: '龙族', Abysscraft: '梦魇', Havencraft: '主教', Portalcraft: '超越者'
};
export const TYPES = { Follower: '从者', Spell: '法术', Amulet: '护符', Leader: '主战者', Crest: '纹章', Faith: '信仰' };
export const RARITIES = { Bronze: '青铜', Silver: '白银', Gold: '黄金', Legend: '传说' };
export const SECTION_STYLES = ['普通', '普通（分割线）', '进化', '超进化', '纹章', '信仰'];
export const PACKS = {};
export const DEFAULT_NUMBER_STYLE = 'shadowverse-symd2';
export const NUMBER_STYLES = [{ id: 'shadowverse-symd2', label: '默认（symd2）' }];
export const FILE_FORMAT = 'svwb-mse-web-card-set';

export function newCard() {
  return {
    editorId: crypto.randomUUID(), sourceCardId: null, name: '新卡牌', type: 'Follower',
    class: 'Neutral', rarity: 'Bronze', cardSet: '默认', cost: 0, attack: 0, health: 1,
    keywords: [], tribe: [], countdown: null, isCollectible: true, isImplemented: false, abilityIds: [],
    presentation: {
      template: 'shadowverse-beyond', version: 1, language: 'zh-TW', fontSize: 16.8, autoFitFontSize: true,
      numberStyle: DEFAULT_NUMBER_STYLE, alternateArt: false, artist: '', showArtist: true,
      art: { src: '', crop: { zoom: 1, x: 0.5, y: 0.5, flipX: false } }, background: '', cardPackLogo: '',
      sections: Array.from({ length: 5 }, () => ({ text: '', style: '普通', icon: '' }))
    }
  };
}

export function initialSet() {
  const card = newCard();
  // 示例只读取项目现有静态卡牌与插画，不访问正式卡库。
  Object.assign(card, {
    sourceCardId: 1401301, name: '治愈的旋律', type: 'Spell', rarity: 'Silver', cost: 3, attack: null, health: null,
    cardSet: '星云基础'
  });
  card.presentation.art.src = '/assets/cards/1401301/preview.webp';
  card.presentation.sections[0].text = '回复自己的主战者3点体力。\n抽取1张卡牌。';
  return { name: 'Beyond 测试卡集', author: '', description: '', cards: [card] };
}

export function descriptionFromSections(sections) {
  return sections.filter(section => section.text.trim()).map(section => {
    const prefix = ['进化', '超进化', '纹章', '信仰'].includes(section.style) ? `【${section.style}】\n` : '';
    return prefix + section.text.replace(/<\/?i>/g, '').replace(/==/g, '\n──\n');
  }).join('\n\n');
}

export function cardParameters(card) {
  return {
    name: card.name, type: card.type, class: card.class, rarity: card.rarity,
    card_set: card.cardSet, cost: card.cost,
    attack: card.type === 'Follower' ? card.attack : null,
    health: (card.type === 'Follower' || card.type === 'Leader') ? card.health : null,
    description: descriptionFromSections(card.presentation.sections), keywords: card.keywords,
    tribe: card.tribe, countdown: card.countdown, isCollectible: card.isCollectible, isImplemented: card.isImplemented ?? true,
    abilityIds: card.abilityIds, presentation: structuredClone(card.presentation)
  };
}

function requireText(value, label) {
  if (typeof value !== 'string') throw new Error(`${label}必须是文字。`);
}

function requireImage(src) {
  requireText(src, '图片');
  if (src && !/^data:image\/(png|jpeg|webp|gif);base64,/.test(src) && !/^\/assets\/(cards|card-sets|card-styles)\/[\w/.-]+\.(webp|png|jpe?g)$/.test(src)) {
    throw new Error('图片必须是导入文件内的图片数据或项目卡牌素材。');
  }
}

export function validateSet(set) {
  if (!set || !Array.isArray(set.cards) || set.cards.length === 0) throw new Error('卡集至少需要一张卡牌。');
  for (const field of ['name', 'author', 'description']) requireText(set[field], '卡集信息');
  const ids = new Set();
  for (const card of set.cards) {
    if (typeof card.editorId !== 'string' || ids.has(card.editorId)) throw new Error('卡牌编辑 ID 缺失或重复。');
    ids.add(card.editorId);
    for (const field of ['name', 'cardSet']) requireText(card[field], '卡牌信息');
    if (!card.name.trim()) throw new Error('请填写卡牌名称后再保存。');
    if (!(card.type in TYPES) || !(card.class in CLASSES) || !(card.rarity in RARITIES)) throw new Error('卡牌类型、职业或稀有度不受支持。');
    if (!Number.isInteger(card.cost) || card.cost < 0 || card.cost > 99) throw new Error('费用必须是 0–99 的整数。');
    if (card.type === 'Follower' && (!Number.isInteger(card.attack) || card.attack < 0 || card.attack > 99 || !Number.isInteger(card.health) || card.health < 1 || card.health > 99)) throw new Error('从者攻防数值不正确。');
    if (card.type === 'Leader' && (!Number.isInteger(card.health) || card.health < 1 || card.health > 99)) throw new Error('主战者生命值数值不正确。');
    if (!Array.isArray(card.tribe) || !Array.isArray(card.keywords) || !card.tribe.every(x => typeof x === 'string') || !card.keywords.every(x => typeof x === 'string')) throw new Error('种类与关键字必须是文字列表。');
    if (!Array.isArray(card.abilityIds) || !card.abilityIds.every(Number.isSafeInteger)) throw new Error('能力关联 ID 格式不正确。');
    if (typeof card.isCollectible !== 'boolean' || (card.countdown !== null && (!Number.isInteger(card.countdown) || card.countdown < 0))) throw new Error('衍生卡或倒数设置不正确。');
    const p = card.presentation;
    if (!p || p.template !== 'shadowverse-beyond' || p.version !== 1 || !['zh-CN', 'zh-TW'].includes(p.language)) throw new Error('卡牌模板或语言不受支持。');
    p.numberStyle = DEFAULT_NUMBER_STYLE;
    if (!Number.isFinite(p.fontSize) || p.fontSize < 11 || p.fontSize > 32) throw new Error('字号不受支持。');
    if (typeof p.alternateArt !== 'boolean' || typeof p.showArtist !== 'boolean') throw new Error('外观设置不正确。');
    requireText(p.artist, '绘师');
    requireImage(p.background); requireImage(p.cardPackLogo);
    if (!p.art || !p.art.crop) throw new Error('缺少插画裁切设置。');
    requireImage(p.art.src);
    const crop = p.art.crop;
    if (!Number.isFinite(crop.zoom) || crop.zoom < 1 || crop.zoom > 4 || !Number.isFinite(crop.x) || crop.x < 0 || crop.x > 1 || !Number.isFinite(crop.y) || crop.y < 0 || crop.y > 1) throw new Error('插画裁切参数超出范围。');
    if (!Array.isArray(p.sections) || p.sections.length !== 5) throw new Error('Beyond 模板需要五个规则段落。');
    for (const section of p.sections) {
      requireText(section.text, '段落'); requireImage(section.icon);
      if (!SECTION_STYLES.includes(section.style)) throw new Error('段落样式不受支持。');
    }
  }
  return set;
}

export function readSetFile(text) {
  const file = JSON.parse(text);
  if (file.format !== FILE_FORMAT || file.version !== 1) throw new Error('请选择本测试页导出的卡集 JSON 文件。');
  return validateSet(file.set);
}

export async function openStorage() {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open('svwb-mse-editor-test', 1);
    request.onupgradeneeded = () => request.result.createObjectStore('sets');
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

export function loadSet(db) {
  return new Promise((resolve, reject) => {
    const request = db.transaction('sets').objectStore('sets').get('working-set');
    request.onsuccess = () => resolve(request.result ? validateSet(request.result) : null);
    request.onerror = () => reject(request.error);
  });
}

export function saveSet(db, set) {
  validateSet(set);
  return new Promise((resolve, reject) => {
    const transaction = db.transaction('sets', 'readwrite');
    transaction.objectStore('sets').put(structuredClone(set), 'working-set');
    transaction.oncomplete = resolve;
    transaction.onerror = () => reject(transaction.error);
    transaction.onabort = () => reject(transaction.error);
  });
}
