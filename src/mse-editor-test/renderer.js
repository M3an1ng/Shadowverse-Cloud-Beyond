import { CLASSES } from './document.js';

export const WIDTH = 1269;
export const HEIGHT = 715;
const ASSETS = '/mse-beyond/';
const images = new Map();
const maskedFrames = new Map();
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

export async function loadFonts() {
  await Promise.all(['MSE WeiBei SC', 'MSE WeiBei TC', 'MSE Noto SC', 'MSE Noto TC'].map(name => document.fonts.load(`20px "${name}"`)));
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

async function maskedFrame(name, maskName = 'card_mask.png') {
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
  return card.presentation.language === 'zh-TW' ? '"MSE WeiBei TC", "MSE WeiBei SC"' : '"MSE WeiBei SC", "MSE WeiBei TC"';
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

function ruleGlyphs(text) {
  let emphasis = false;
  let keyword = false;
  const glyphs = [];
  text = text.replace(/·/g, '•').replace(/￥/g, '$').replace(/\r?\n?[ \t]*==[ \t]*\r?\n?/g, '==');
  for (let i = 0; i < text.length;) {
    if (text.startsWith('<i>', i)) { emphasis = true; i += 3; continue; }
    if (text.startsWith('</i>', i)) { emphasis = false; i += 4; continue; }
    if (text.startsWith('==', i)) { glyphs.push({ divider: true }); i += 2; continue; }
    const char = String.fromCodePoint(text.codePointAt(i));
    if (char === '【') keyword = true;
    glyphs.push({ char, emphasis: emphasis || keyword });
    if (char === '】') keyword = false;
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
  for (let i = 0; i < sections.length; i++) {
    const section = sections[i];
    if (i > 0 && !sections[i - 1].text.trim() && !section.text.trim()) continue;
    const crest = ['纹章', '信仰'].includes(section.style);
    const ordinary = section.style.startsWith('普通');
    const baseTop = previous
      ? previous.top + previous.effectiveHeight + (previous.style === '普通（分割线）' ? 22 : previous.style === '普通' ? 0 : 11) + 20
      : 180;
    const top = i === 0 ? baseTop + (crest ? 60 : 0) : baseTop + (crest ? 65 : ordinary ? -6 : 5);
    const backgroundTop = i === 0 ? 175 : baseTop + (ordinary ? -6 : 0);
    const crestTop = i === 0 ? 187 : baseTop + 12;
    const lines = section.text ? ruleLines(ctx, section.text, 614, fontSize, font) : [];
    const lineHeight = fontSize * PIXELS_PER_POINT * 1.4;
    const contentHeight = lines.length * lineHeight;
    const effectiveHeight = Math.max(22, contentHeight);
    const lineCountForBg = section.text ? Math.max(1, Math.min(5, lines.length)) : 1;
    const prefix = section.style === '进化' ? 'ev' : section.style === '超进化' ? 'sev' : crest ? 'crest' : '';
    const hasVisualContent = Boolean(section.text.trim()) || section.style !== '无';
    const visualBottom = hasVisualContent ? Math.max(
      top + effectiveHeight,
      section.style === '普通（分割线）' && section.text.trim() ? top + effectiveHeight + 22 : 0,
      prefix ? backgroundTop + effectiveHeight + 16 + (crest ? 60 : 0) : 0
    ) : 0;
    overflow ||= visualBottom > 571;
    renderedSections.push({ i, section, crest, top, backgroundTop, crestTop, lines, lineHeight, contentHeight, effectiveHeight, lineCountForBg, prefix });
    previous = { top, effectiveHeight, style: section.style };
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

export async function renderCard(card, options = {}) {
  const omitKey = options?.omitTextField || null;
  const canvas = canvasOf(WIDTH, HEIGHT);
  const ctx = canvas.getContext('2d');
  const textCanvas = canvasOf(WIDTH, HEIGHT);
  const textCtx = textCanvas.getContext('2d');
  const fields = [];
  const p = card.presentation;
  const font = fontFor(card);
  const footerFont = p.language === 'zh-TW' ? '"MSE Noto TC", "MSE Noto SC"' : '"MSE Noto SC", "MSE Noto TC"';
  const typography = (points, family = font, align = 'left') => ({ family, size: points * PIXELS_PER_POINT, align });
  const classKey = card.class.toLowerCase();
  const isFollower = card.type === 'Follower';
  const isLeader = card.type === 'Leader';
  const frameType = (isFollower || isLeader) ? 'minion' : card.type === 'Spell' ? 'spell' : 'amulet';
  const rarity = p.alternateArt ? 'special' : card.rarity === 'Legend' ? 'legend' : card.rarity.toLowerCase();
  const picture = async (name, x, y, width, height) => ctx.drawImage(await loadImage(ASSETS + name), x, y, width, height);
  const field = (key, label, x, y, width, height, kind = 'text', textStyle = null) => fields.push({ key, label, x, y, width, height, kind, textStyle });

  ctx.drawImage(await loadImage(p.background || `${ASSETS}background_${classKey}.png`), 0, 0, WIDTH, HEIGHT);
  await picture('title_bg.png', 0, 0, WIDTH, 130);
  await picture('card_detail_background.png', 483, 145, 701, 488);
  if (p.art.src) {
    try {
      drawCroppedArt(ctx, await loadImage(p.art.src), p.art.crop, { x: 136, y: 230, width: 293, height: 363 });
    } catch (_) {
      // 忽略缺失的旧卡牌插画，继续绘制卡框与文字
    }
  }
  ctx.drawImage(await maskedFrame(`card_${frameType}_${rarity}.png`), 81, 131, 391, 513);
  await picture(`gem_${classKey}.png`, 268, 572, 25, 26);
  await picture(`Emblem_${card.class}.png`, 864, 48, 24, 24);
  if (p.cardPackLogo) {
    try {
      const logoImg = await loadImage(p.cardPackLogo);
      const scale = Math.min(160 / logoImg.width, 80 / logoImg.height);
      const width = logoImg.width * scale;
      const height = logoImg.height * scale;
      ctx.drawImage(logoImg, 1035 + (160 - width) / 2, 32 + (80 - height) / 2, width, height);
    } catch (_) {}
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
  await drawNumbers(ctx, card.cost, { x: 90, y: 163, width: 85, height: 85 });
  if (isFollower) {
    await drawNumbers(ctx, card.attack, { x: 94, y: 540, width: 85, height: 102 });
    await drawNumbers(ctx, card.health, { x: 383, y: 540, width: 85, height: 102 });
  } else if (isLeader) {
    await drawNumbers(ctx, card.health ?? 20, { x: 383, y: 540, width: 85, height: 102 });
  }

  field('art', '插画 · 双击更换／拖动裁切', 136, 230, 293, 310, 'art');
  field('name', '卡牌名称', 167, 190, nameWidth, 36, 'text', typography(nameSize, font, 'center'));
  field('title-name', '卡牌标题', 86, 72, 650, 46, 'text', typography(28.6));
  field('cost', '费用', 90, 163, 85, 85, 'number', typography(45, font, 'center'));
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
  for (const { i, section, crest, top, backgroundTop, crestTop, lines, lineHeight, contentHeight, effectiveHeight, lineCountForBg, prefix } of layout.sections) {
    if (prefix) await picture(`bg_${prefix}_${lineCountForBg}.png`, 513, backgroundTop, 642, effectiveHeight + 16 + (crest ? 60 : 0));
    if (crest) {
      await picture(section.style === '纹章' ? 'Crest.png' : 'Faith.png', 523, crestTop, 449, 48);
      await picture(section.style === '纹章' ? 'Crest_mark.png' : 'Faith_mark.png', 544, crestTop + 4, 36, 40);
      const iconName = section.icon || `${ASSETS}Crest_default.png`;
      const maskedIcon = await maskedFrame(iconName, 'crest_mask.png');
      ctx.drawImage(maskedIcon, section.icon ? 583 : 584, crestTop + (section.icon ? 4 : 5), section.icon ? (i === 0 ? 40 : 39) : 38, section.icon ? (i === 0 ? 40 : 39) : 38);
      drawText(textCtx, section.style === '纹章' ? (p.language === 'zh-TW' ? '紋章' : '纹章') : '信仰', 630, crestTop + 24, 100, 16.8, font, 'left', '#eccf71');
      field(`icon.${i}`, `段落 ${i + 1} 图标`, 583, crestTop + 4, 40, 40, 'image');
    }
    if (omitKey !== `section.${i}`) {
      const textStartY = top + (effectiveHeight + 6 - contentHeight) / 2;
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
          await picture('detail_spit.png', 507, y + (lineHeight - 2) / 2, 653, 2);
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
    if (section.style === '普通（分割线）' && section.text.trim()) {
      ctx.save(); ctx.globalAlpha = 0.3;
      await picture('detail_spit.png', 507, top + effectiveHeight + 20, 653, 2);
      ctx.restore();
    }
    field(`section.${i}`, `段落 ${i + 1}`, 527, top - 2, 614, Math.max(27, effectiveHeight + 6), 'multiline', typography(effectiveFontSize));
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
