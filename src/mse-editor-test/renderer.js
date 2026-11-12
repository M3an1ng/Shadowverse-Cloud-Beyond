import { CLASSES, normalizeSectionStyle } from './document.js';

export const WIDTH = 1269;
export const HEIGHT = 715;
const ASSETS = '/mse-beyond/';
const images = new Map();
const maskedFrames = new Map();
const fontLoadPromises = new Map();
const loadedFontFamilies = new Set();
// MSE 的普通字体以 96 DPI 下的 pt 定义；卡面坐标仍是像素，不能乘 card dpi。
const PIXELS_PER_POINT = 96 / 72;
const NUMBER_IMAGE_POINT_SIZE = 60;
const NUMBER_SPACING = -15;
// 抵消浏览器 Canvas 对深底浅色 CJK 字体的 sRGB 笔画膨胀（Stem Darkening），对齐桌面版 MSE (GDI) 笔画粗细。
const TEXT_ALPHA_LUT = Uint8Array.from({ length: 256 }, (_, a) => Math.round(((a / 255) ** 1.75) * 255));
const TRADITIONAL_CLASSES = { Neutral: '中立', Forestcraft: '精靈', Swordcraft: '皇家護衛', Runecraft: '巫師', Dragoncraft: '龍族', Abysscraft: '夢魘', Havencraft: '主教', Portalcraft: '復仇者' };

export function loadImage(src) {
  if (!images.has(src)) {
    images.set(src, new Promise((resolve, reject) => {
      const image = new Image();
      image.onload = () => resolve(image);
      image.onerror = () => { images.delete(src); reject(new Error(`无法加载图片：${src.slice(0, 100)}`)); };
      image.src = src;
    }));
  }
  return images.get(src);
}

function loadFontFamily(name) {
  if (loadedFontFamilies.has(name)) {
    return Promise.resolve();
  }
  if (typeof document === 'undefined' || !document.fonts?.load) {
    loadedFontFamilies.add(name);
    return Promise.resolve();
  }
  if (!fontLoadPromises.has(name)) {
    const p = document.fonts.load(`20px "${name}"`)
      .then(() => {
        loadedFontFamilies.add(name);
      })
      .finally(() => {
        if (!loadedFontFamilies.has(name)) {
          fontLoadPromises.delete(name);
        }
      });
    fontLoadPromises.set(name, p);
  }
  return fontLoadPromises.get(name);
}

export function getCardFontFamilies(language = 'zh-CN') {
  const isTw = language === 'zh-TW';
  return {
    primary: isTw ? 'MSE WeiBei TC' : 'MSE WeiBei SC',
    footer: isTw ? 'MSE Noto TC' : 'MSE Noto SC'
  };
}

export function areCardFontsLoaded(language = 'zh-CN', { includeFooter = true } = {}) {
  const { primary, footer } = getCardFontFamilies(language);
  if (!loadedFontFamilies.has(primary)) return false;
  if (includeFooter && !loadedFontFamilies.has(footer)) return false;
  return true;
}

export async function loadCardFonts(language = 'zh-CN', { includeFooter = true, onStage = null } = {}) {
  const { primary, footer } = getCardFontFamilies(language);
  if (!loadedFontFamilies.has(primary)) {
    onStage?.('primary', primary);
    await loadFontFamily(primary);
  }
  if (includeFooter && !loadedFontFamilies.has(footer)) {
    onStage?.('footer', footer);
    await loadFontFamily(footer);
  }
}

export async function loadFonts() {
  await Promise.all(['MSE WeiBei SC', 'MSE WeiBei TC', 'MSE Noto SC', 'MSE Noto TC'].map(loadFontFamily));
}

function canvasOf(width, height) {
  const canvas = document.createElement('canvas');
  canvas.width = width; canvas.height = height;
  return canvas;
}

export function drawCroppedArt(ctx, image, crop, rect) {
  const scale = Math.max(rect.width / image.width, rect.height / image.height) * crop.zoom;
  const width = image.width * scale;
  const height = image.height * scale;
  ctx.save();
  ctx.beginPath(); ctx.rect(rect.x, rect.y, rect.width, rect.height); ctx.clip();
  if (crop?.flipX) {
    ctx.translate(rect.x * 2 + rect.width, 0);
    ctx.scale(-1, 1);
  }
  ctx.drawImage(image, rect.x - (width - rect.width) * crop.x, rect.y - (height - rect.height) * crop.y, width, height);
  ctx.restore();
}

export async function maskedFrame(name, maskName = 'card_mask.png') {
  const key = `${name}|${maskName}`;
  if (!maskedFrames.has(key)) {
    const frameSrc = name.startsWith('data:') || name.startsWith('/') ? name : ASSETS + name;
    const [frame, mask] = await Promise.all([loadImage(frameSrc), loadImage(ASSETS + maskName)]);
    const canvas = canvasOf(frame.width, frame.height);
    const ctx = canvas.getContext('2d');
    ctx.drawImage(mask, 0, 0, canvas.width, canvas.height);
    const pixels = ctx.getImageData(0, 0, canvas.width, canvas.height);
    // MSE 的蒙版读取亮度；直接用 PNG alpha 会把黑色挖空区域误画成不透明。
    for (let i = 0; i < pixels.data.length; i += 4) pixels.data[i + 3] = pixels.data[i];
    ctx.putImageData(pixels, 0, 0);
    ctx.globalCompositeOperation = 'source-in';
    ctx.drawImage(frame, 0, 0);
    maskedFrames.set(key, canvas);
  }
  return maskedFrames.get(key);
}

function fontFor(card) {
  // 按语言严格隔离简繁网络字体，避免在 zh-CN 下意外触发 15MB+ 繁体字库下载，并提供系统本地字体瞬时兜底。
  return card.presentation.language === 'zh-TW'
    ? '"MSE WeiBei TC", "DFKai-SB", "BiauKai", "Microsoft JhengHei", "PingFang TC", serif'
    : '"MSE WeiBei SC", "STKaiti", "KaiTi", "Microsoft YaHei", "PingFang SC", serif';
}

function footerFontFor(card) {
  return card.presentation.language === 'zh-TW'
    ? '"MSE Noto TC", "Noto Sans TC", "Microsoft JhengHei", "PingFang TC", sans-serif'
    : '"MSE Noto SC", "Noto Sans SC", "Microsoft YaHei", "PingFang SC", sans-serif';
}

function drawText(ctx, text, x, y, width, size, font, align = 'left', color = '#fff', shadow = null) {
  ctx.save();
  ctx.font = `${size * PIXELS_PER_POINT}px ${font}`;
  ctx.fillStyle = color; ctx.textBaseline = 'middle'; ctx.textAlign = align;
  if (shadow) {
    ctx.shadowColor = shadow.color; ctx.shadowBlur = shadow.blur;
    ctx.shadowOffsetX = shadow.offsetX; ctx.shadowOffsetY = shadow.offsetY;
  }
  ctx.fillText(String(text), align === 'center' ? x + width / 2 : align === 'right' ? x + width : x, y, width);
  ctx.restore();
}

const DIVIDER_SENTINEL = '\x1e';

export function ruleGlyphs(text) {
  let emphasis = false;
  const glyphs = [];
  text = text
    .replace(/·/g, '•')
    .replace(/￥/g, '$')
    .replace(/\r\n/g, '\n')
    .replace(/\n?[ \t]*(?:[-—─]{2,}|==)[ \t]*\n?/g, DIVIDER_SENTINEL);
  for (let i = 0; i < text.length;) {
    if (text.startsWith('<i>', i)) { emphasis = true; i += 3; continue; }
    if (text.startsWith('</i>', i)) { emphasis = false; i += 4; continue; }
    if (text[i] === DIVIDER_SENTINEL) { glyphs.push({ divider: true }); i += 1; continue; }
    const char = String.fromCodePoint(text.codePointAt(i));
    if (char === '【') {
      const closeIdx = text.indexOf('】', i + 1);
      const newlineIdx = text.indexOf('\n', i + 1);
      const sentinelIdx = text.indexOf(DIVIDER_SENTINEL, i + 1);
      const hasValidClose = closeIdx !== -1
        && closeIdx > i + 1
        && (newlineIdx === -1 || closeIdx < newlineIdx)
        && (sentinelIdx === -1 || closeIdx < sentinelIdx);
      if (hasValidClose) {
        glyphs.push({ char: '【', emphasis });
        const innerRaw = text.slice(i + 1, closeIdx);
        let reachedSuffix = false;
        for (let k = 0; k < innerRaw.length;) {
          if (innerRaw.startsWith('<i>', k)) { emphasis = true; k += 3; continue; }
          if (innerRaw.startsWith('</i>', k)) { emphasis = false; k += 4; continue; }
          const innerChar = String.fromCodePoint(innerRaw.codePointAt(k));
          if (innerChar === '_' && k > 0) {
            reachedSuffix = true;
          }
          glyphs.push({ char: innerChar, emphasis: emphasis || !reachedSuffix });
          k += innerChar.length;
        }
        glyphs.push({ char: '】', emphasis });
        i = closeIdx + 1;
        continue;
      }
    }
    glyphs.push({ char, emphasis });
    i += char.length;
  }
  return glyphs;
}

function ruleLines(ctx, text, width, fontSize, font) {
  ctx.font = `${fontSize * PIXELS_PER_POINT}px ${font}`;
  const lines = [];
  let line = []; let length = 0;
  for (const glyph of ruleGlyphs(text)) {
    if (glyph.divider) {
      if (line.length) { lines.push(line); line = []; length = 0; }
      lines.push([{ divider: true }]);
      continue;
    }
    if (glyph.char === '\n') {
      lines.push(line); line = []; length = 0;
      continue;
    }
    const advance = ctx.measureText(glyph.char).width;
    if (length + advance > width && line.length) { lines.push(line); line = []; length = 0; }
    line.push({ ...glyph, advance }); length += advance;
  }
  if (line.length || !lines.length) lines.push(line);
  return lines;
}

function layoutRuleSections(ctx, sections, fontSize, font) {
  const renderedSections = [];
  let previous = null;
  let overflow = false;
  // 原版 MSE 度量：16.8pt (22.4px) 下单行净高 22px、行步进 32px；末行不含额外行间空隙（栅栏定律）
  const glyphHeight = fontSize * PIXELS_PER_POINT * (22 / 22.4);
  const lineHeight = fontSize * PIXELS_PER_POINT * (32 / 22.4);

  for (let i = 0; i < sections.length; i++) {
    const rawSection = sections[i];
    const style = normalizeSectionStyle(rawSection.style);
    const section = style === rawSection.style ? rawSection : { ...rawSection, style };
    if (i > 0 && !sections[i - 1].text.trim() && !section.text.trim()) continue;
    const crest = ['纹章', '信仰'].includes(style);
    const ordinary = style.startsWith('普通');
    const hasTrailingDivider = style === '普通（分割线）' && Boolean(section.text.trim());
    const baseTop = previous
      ? previous.top + previous.effectiveHeight + (previous.hasTrailingDivider ? 22 : previous.ordinary ? 0 : 11) + 20
      : 180;
    const top = i === 0 ? baseTop + (crest ? 60 : 0) : baseTop + (crest ? 65 : ordinary ? -6 : 5);
    const backgroundTop = i === 0 ? 175 : baseTop + (ordinary ? -6 : 0);
    const crestTop = i === 0 ? 187 : baseTop + 12;
    const lines = section.text ? ruleLines(ctx, section.text, 614, fontSize, font) : [];
    const contentHeight = lines.length ? glyphHeight + (lines.length - 1) * lineHeight : 0;
    const effectiveHeight = Math.max(glyphHeight, contentHeight);
    const lineCountForBg = section.text ? Math.max(1, Math.min(5, lines.length)) : 1;
    const prefix = style === '进化' ? 'ev' : style === '超进化' ? 'sev' : crest ? 'crest' : '';
    const hasVisualContent = Boolean(section.text.trim()) || style !== '无';
    const visualBottom = hasVisualContent ? Math.max(
      top + effectiveHeight,
      hasTrailingDivider ? top + effectiveHeight + 22 : 0,
      prefix ? backgroundTop + effectiveHeight + 16 + (crest ? 60 : 0) : 0
    ) : 0;
    overflow ||= visualBottom > 571;
    renderedSections.push({
      i, section, crest, ordinary, hasTrailingDivider,
      top, backgroundTop, crestTop, lines,
      glyphHeight, lineHeight, contentHeight, effectiveHeight, lineCountForBg, prefix
    });
    previous = { top, effectiveHeight, ordinary, hasTrailingDivider };
  }
  return { sections: renderedSections, overflow };
}

async function drawNumbers(ctx, value, rect, style) {
  const digits = String(value).split('');
  const sprites = await Promise.all(digits.map(digit => loadImage(`${ASSETS}numbers/shadowverse-symd/${digit}.png`)));
  // 图片符号另按自身 image font size 缩放；MSE 的 horizontal space 直接以渲染像素计，不乘 symbolScale。
  const symbolScale = 28 / NUMBER_IMAGE_POINT_SIZE;
  const spacing = NUMBER_SPACING;
  const widths = sprites.map(sprite => sprite.width * symbolScale);
  const heights = sprites.map(sprite => sprite.height * symbolScale);
  const total = widths.reduce((a, b) => a + b, 0) + spacing * (digits.length - 1);
  const scale = Math.min(1, rect.width / total);
  let x = rect.x + (rect.width - total * scale) / 2;
  for (let i = 0; i < sprites.length; i++) {
    ctx.drawImage(sprites[i], x, rect.y + (rect.height - heights[i] * scale) / 2, widths[i] * scale, heights[i] * scale);
    x += (widths[i] + spacing) * scale;
  }
}

async function drawMirageStarTier(ctx, tierValue, rect) {
  const tier = Math.max(1, Math.trunc(Number(tierValue) || 1));
  const symbolScale = 28 / NUMBER_IMAGE_POINT_SIZE;
  if (tier <= 7) {
    const sprite = await loadImage(`${ASSETS}numbers/mirage-stars/${tier}.png`);
    const w = sprite.width * symbolScale;
    const h = sprite.height * symbolScale;
    const scale = Math.min(1, rect.width / w, rect.height / h);
    const drawW = w * scale;
    const drawH = h * scale;
    ctx.drawImage(sprite, rect.x + (rect.width - drawW) / 2, rect.y + (rect.height - drawH) / 2, drawW, drawH);
    return;
  }

  const digits = String(tier).split('');
  const [starSprite, ...digitSprites] = await Promise.all([
    loadImage(`${ASSETS}numbers/mirage-stars/1.png`),
    ...digits.map(d => loadImage(`${ASSETS}numbers/shadowverse-symd/${d}.png`))
  ]);
  const sprites = [starSprite, ...digitSprites];
  const spacing = -12;
  const widths = sprites.map((s, idx) => s.width * symbolScale * (idx === 0 ? 0.82 : 0.9));
  const heights = sprites.map((s, idx) => s.height * symbolScale * (idx === 0 ? 0.82 : 0.9));
  const total = widths.reduce((a, b) => a + b, 0) + spacing * (sprites.length - 1);
  const scale = Math.min(1, rect.width / total);
  let x = rect.x + (rect.width - total * scale) / 2;
  for (let i = 0; i < sprites.length; i++) {
    ctx.drawImage(sprites[i], x, rect.y + (rect.height - heights[i] * scale) / 2, widths[i] * scale, heights[i] * scale);
    x += (widths[i] + spacing) * scale;
  }
}

export async function renderCard(card, options = {}) {
  const omitKey = options?.omitTextField || null;
  const canvas = canvasOf(WIDTH, HEIGHT);
  const ctx = canvas.getContext('2d');
  const textCanvas = canvasOf(WIDTH, HEIGHT);
  const textCtx = textCanvas.getContext('2d');
  const fields = [];
  const p = card.presentation;
  const font = fontFor(card);
  const footerFont = footerFontFor(card);
  const typography = (points, family = font, align = 'left') => ({ family, size: points * PIXELS_PER_POINT, align });
  const classKey = card.class.toLowerCase();
  const isFollower = card.type === 'Follower';
  const isLeader = card.type === 'Leader';
  const isMirage = Boolean(card.isMirageCard ?? card.IsMirageCard);
  const mirageTier = Math.max(1, Math.trunc(Number(card.cost) || 1));
  const frameType = (isFollower || isLeader) ? 'minion' : card.type === 'Spell' ? 'spell' : 'amulet';
  const rarity = p.alternateArt ? 'special' : card.rarity === 'Legend' ? 'legend' : card.rarity.toLowerCase();
  const picture = async (name, x, y, width, height) => ctx.drawImage(await loadImage(ASSETS + name), x, y, width, height);
  const field = (key, label, x, y, width, height, kind = 'text', textStyle = null) => fields.push({ key, label, x, y, width, height, kind, textStyle });

  // 并行预热核心底图、卡框蒙版、数字精灵及插画，避免公网环境下串行等待多个 RTT
  const bgSrc = p.background || `${ASSETS}background_${classKey}.png`;
  const frameName = `card_${frameType}_${rarity}.png`;
  const digitSet = new Set();
  if (!isMirage) {
    String(card.cost).split('').forEach(d => digitSet.add(d));
  } else if (!isLeader && mirageTier > 7) {
    String(mirageTier).split('').forEach(d => digitSet.add(d));
  }
  if (isFollower) {
    String(card.attack).split('').forEach(d => digitSet.add(d));
    String(card.health).split('').forEach(d => digitSet.add(d));
  } else if (isLeader) {
    String(card.health ?? 20).split('').forEach(d => digitSet.add(d));
  }
  const mirageStarPreloads = isMirage && !isLeader
    ? [loadImage(`${ASSETS}numbers/mirage-stars/${mirageTier <= 7 ? mirageTier : 1}.png`)]
    : [];
  const preloadTasks = [
    loadImage(bgSrc),
    loadImage(`${ASSETS}title_bg.png`),
    loadImage(`${ASSETS}card_detail_background.png`),
    maskedFrame(frameName),
    loadImage(`${ASSETS}gem_${classKey}.png`),
    loadImage(`${ASSETS}Emblem_${card.class}.png`),
    ...mirageStarPreloads,
    ...[...digitSet].filter(d => /^\d$/.test(d)).map(d => loadImage(`${ASSETS}numbers/shadowverse-symd/${d}.png`)),
    p.art.src ? loadImage(p.art.src).catch(() => null) : Promise.resolve(null),
    p.cardPackLogo ? loadImage(p.cardPackLogo).catch(() => null) : Promise.resolve(null)
  ];
  const [bgImg, , , frameCanvas, , , ...restPreloaded] = await Promise.all(preloadTasks);
  const logoImg = restPreloaded[restPreloaded.length - 1];
  const artImg = restPreloaded[restPreloaded.length - 2];

  ctx.drawImage(bgImg, 0, 0, WIDTH, HEIGHT);
  await picture('title_bg.png', 0, 0, WIDTH, 130);
  await picture('card_detail_background.png', 483, 145, 701, 488);
  if (artImg) {
    drawCroppedArt(ctx, artImg, p.art.crop, { x: 136, y: 230, width: 293, height: 363 });
  }
  ctx.drawImage(frameCanvas, 81, 131, 391, 513);
  await picture(`gem_${classKey}.png`, 268, 572, 25, 26);
  await picture(`Emblem_${card.class}.png`, 864, 48, 24, 24);
  if (logoImg) {
    const scale = Math.min(160 / logoImg.width, 80 / logoImg.height);
    const width = logoImg.width * scale;
    const height = logoImg.height * scale;
    ctx.drawImage(logoImg, 1035 + (160 - width) / 2, 32 + (80 - height) / 2, width, height);
  }

  const filteredName = card.name.replace(/[·.．]/g, '•');
  const blackShadow = { color: '#000', blur: 3, offsetX: 0.5, offsetY: 0.5 };
  const ruleShadow = { color: 'rgba(0, 0, 0, 0.4)', blur: 3, offsetX: 0.5, offsetY: 0.5 };
  if (omitKey !== 'title-name') {
    drawText(textCtx, filteredName, 86, 95, 650, 28.6, font);
  }
  const nameWidth = filteredName.length <= 5 ? 231 : filteredName.length <= 6 ? 247 : 263;
  const nominalNameSize = Math.max(15, filteredName.length <= 5 ? 22 : 28 - filteredName.length);
  textCtx.font = `${nominalNameSize * PIXELS_PER_POINT}px ${font}`;
  const nameSize = Math.max(15, nominalNameSize * Math.min(1, nameWidth / (textCtx.measureText(filteredName).width || nameWidth)));
  if (omitKey !== 'name') {
    drawText(textCtx, filteredName, 167, 208, nameWidth, nameSize, font, 'center');
  }
  drawText(textCtx, p.language === 'zh-TW' ? '職業' : '职业', 800, 60, 60, 18, font, 'left', '#d9be97', blackShadow);
  drawText(textCtx, (p.language === 'zh-TW' ? TRADITIONAL_CLASSES : CLASSES)[card.class], 894, 60, 130, 18, font, 'left', '#fff', blackShadow);
  drawText(textCtx, p.language === 'zh-TW' ? '類型' : '类型', 800, 95, 60, 18, font, 'left', '#d9be97', blackShadow);
  if (omitKey !== 'tribe') {
    drawText(textCtx, card.tribe.map(value => value.replace(/[·.．]/g, '•')).join('•') || '-', 864, 95, 160, 18, font, 'left', '#fff', blackShadow);
  }
  if (isMirage) {
    if (!isLeader) {
      await drawMirageStarTier(ctx, mirageTier, { x: 90, y: 163, width: 85, height: 85 });
    }
  } else {
    await drawNumbers(ctx, card.cost, { x: 90, y: 163, width: 85, height: 85 });
  }
  if (isFollower) {
    await drawNumbers(ctx, card.attack, { x: 94, y: 540, width: 85, height: 102 });
    await drawNumbers(ctx, card.health, { x: 383, y: 540, width: 85, height: 102 });
  } else if (isLeader) {
    await drawNumbers(ctx, card.health ?? 20, { x: 383, y: 540, width: 85, height: 102 });
  }

  field('art', '插画 · 双击更换／拖动裁切', 136, 230, 293, 310, 'art');
  field('name', '卡牌名称', 167, 190, nameWidth, 36, 'text', typography(nameSize, font, 'center'));
  field('title-name', '卡牌标题', 86, 72, 650, 46, 'text', typography(28.6));
  if (!isMirage || !isLeader) {
    field('cost', isMirage ? '星级' : '费用', 90, 163, 85, 85, 'number', typography(45, font, 'center'));
  }
  if (isFollower) {
    field('attack', '攻击力', 94, 540, 85, 102, 'number', typography(45, font, 'center'));
    field('health', '生命值', 383, 540, 85, 102, 'number', typography(45, font, 'center'));
  } else if (isLeader) {
    field('health', '生命值', 383, 540, 85, 102, 'number', typography(45, font, 'center'));
  }
  field('class', '职业', 862, 43, 165, 34, 'select');
  field('tribe', '种类（用 · 分隔）', 864, 79, 162, 32, 'text', typography(18));
  field('cardSet', '卡包', 1035, 32, 160, 80, 'select');
  field('rarity', '稀有度', 218, 574, 100, 44, 'select');
  field('type', '卡牌类型', 444, 257, 26, 250, 'select');

  let fontTicks = Math.round((Number(p.fontSize) || 16.8) * 10);
  let layout = layoutRuleSections(ctx, p.sections, fontTicks / 10, font);
  if (p.autoFitFontSize !== false) {
    while (layout.overflow && fontTicks > 110) {
      fontTicks = Math.max(110, fontTicks - 2);
      layout = layoutRuleSections(ctx, p.sections, fontTicks / 10, font);
    }
  }
  const effectiveFontSize = fontTicks / 10;
  for (const { i, section, crest, hasTrailingDivider, top, backgroundTop, crestTop, lines, glyphHeight, lineHeight, contentHeight, effectiveHeight, lineCountForBg, prefix } of layout.sections) {
    if (prefix) await picture(`bg_${prefix}_${lineCountForBg}.png`, 513, backgroundTop, 642, effectiveHeight + 16 + (crest ? 60 : 0));
    if (crest) {
      await picture(section.style === '纹章' ? 'Crest.png' : 'Faith.png', 523, crestTop, 449, 48);
      await picture(section.style === '纹章' ? 'Crest_mark.png' : 'Faith_mark.png', 544, crestTop + 4, 36, 40);
      const defaultIconSrc = `${ASSETS}Crest_default.png`;
      const iconName = section.icon || defaultIconSrc;
      let hasCustomIcon = Boolean(section.icon);
      const maskedIcon = await maskedFrame(iconName, 'crest_mask.png').catch(() => {
        hasCustomIcon = false;
        return maskedFrame(defaultIconSrc, 'crest_mask.png');
      });
      ctx.drawImage(maskedIcon, hasCustomIcon ? 583 : 584, crestTop + (hasCustomIcon ? 4 : 5), hasCustomIcon ? (i === 0 ? 40 : 39) : 38, hasCustomIcon ? (i === 0 ? 40 : 39) : 38);
      drawText(textCtx, section.style === '纹章' ? (p.language === 'zh-TW' ? '紋章' : '纹章') : '信仰', 630, crestTop + 24, 100, 16.8, font, 'left', '#eccf71');
      field(`icon.${i}`, `段落 ${i + 1} 图标`, 583, crestTop + 4, 40, 40, 'image');
    }
    const textStartY = top + (effectiveHeight + 6 - contentHeight) / 2;
    if (omitKey !== `section.${i}`) {
      textCtx.save();
      textCtx.beginPath(); textCtx.rect(505, 151, 660, 477); textCtx.clip();
      textCtx.font = `${effectiveFontSize * PIXELS_PER_POINT}px ${font}`; textCtx.textBaseline = 'top';
      textCtx.shadowColor = ruleShadow.color; textCtx.shadowBlur = ruleShadow.blur; textCtx.shadowOffsetX = ruleShadow.offsetX; textCtx.shadowOffsetY = ruleShadow.offsetY;
      for (let row = 0; row < lines.length; row++) {
        const line = lines[row];
        const y = textStartY + row * lineHeight;
        if (line[0]?.divider) {
          ctx.save();
          ctx.beginPath(); ctx.rect(505, 151, 660, 477); ctx.clip();
          ctx.globalAlpha = 0.3;
          // 段内分割线锚定在上下两行文字墨迹的正几何中心，消除上方留白偏大、下方贴顶
          await picture('detail_spit.png', prefix ? 523 : 507, y + (glyphHeight - 2) / 2, prefix ? 622 : 653, 2);
          ctx.restore();
          continue;
        }
        let x = 527;
        for (let start = 0; start < line.length;) {
          let end = start + 1;
          while (end < line.length && line[end].emphasis === line[start].emphasis) end++;
          const run = line.slice(start, end);
          const runText = run.map(glyph => glyph.char).join('');
          textCtx.fillStyle = run[0].emphasis ? '#eccf71' : '#fff';
          textCtx.fillText(runText, x, y);
          x += run.reduce((width, glyph) => width + glyph.advance, 0);
          start = end;
        }
      }
      textCtx.restore();
    }
    if (hasTrailingDivider) {
      ctx.save();
      ctx.beginPath(); ctx.rect(505, 151, 660, 477); ctx.clip();
      ctx.globalAlpha = 0.3;
      await picture('detail_spit.png', 507, top + effectiveHeight + 20, 653, 2);
      ctx.restore();
    }
    // 有背景框（进化/超进化/纹章/信仰）时，热区直接严丝合缝贴合背景框四边，消除双层底边距离叠加
    const hotspotX = prefix ? 513 : 521;
    const hotspotY = prefix ? (crest ? backgroundTop + 60 : backgroundTop) : (textStartY - 4);
    const hotspotW = prefix ? 642 : 626;
    const hotspotH = prefix ? (effectiveHeight + 16) : Math.max(30, effectiveHeight + 8);
    const secStyle = {
      ...typography(effectiveFontSize),
      lineHeight,
      textRect: { x: 527, y: textStartY, width: 614, height: effectiveHeight }
    };
    field(`section.${i}`, `段落 ${i + 1}`, hotspotX, hotspotY, hotspotW, hotspotH, 'multiline', secStyle);
    field(`style.${i}`, `段落 ${i + 1} 样式`, 507, top + 4, 16, 18, 'select');
  }

  if (p.showArtist !== false) {
    const artist = String(p.artist ?? '').trim();
    if (artist && omitKey !== 'artist') {
      drawText(textCtx, `${p.language === 'zh-TW' ? '繪師' : '画师'}：${artist}`, 523, 595, 200, 14, footerFont, 'left', '#e1e1e1');
    }
    field('artist', '绘师', 523, 579, 360, 32, 'text', typography(14, footerFont));
  }
  if (!card.isCollectible) drawText(textCtx, p.language === 'zh-TW' ? '※本卡牌為附屬卡牌。' : '※本卡牌为附属卡牌。', 790, 655, 400, 14, footerFont, 'right', '#e1e1e1');
  drawText(textCtx, p.language === 'zh-TW' ? '※卡牌能力為開發中內容。' : '※卡牌能力为开发中内容。', 790, 685, 400, 14, footerFont, 'right', '#e1e1e1');
  const textPixels = textCtx.getImageData(0, 0, WIDTH, HEIGHT);
  for (let i = 3; i < textPixels.data.length; i += 4) {
    if (textPixels.data[i]) textPixels.data[i] = TEXT_ALPHA_LUT[textPixels.data[i]];
  }
  textCtx.putImageData(textPixels, 0, 0);
  ctx.drawImage(textCanvas, 0, 0);
  return { canvas, fields, overflow: layout.overflow, effectiveFontSize };
}
