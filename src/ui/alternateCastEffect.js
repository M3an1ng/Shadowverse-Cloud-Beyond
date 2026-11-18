const TAU = Math.PI * 2;
const clamp = value => Math.min(1, Math.max(0, value));
const easeOut = t => 1 - (1 - clamp(t)) ** 3;
const smooth = t => t * t * (3 - 2 * t);
const mix = (a, b, t) => a + (b - a) * t;
const windowAt = (ms, start, end) => clamp((ms - start) / (end - start));
const envelope = (ms, start, peak, end) =>
    smooth(windowAt(ms, start, peak)) * (1 - smooth(windowAt(ms, peak, end)));

export const alternateCastTimings = {
    accelerate: { enter: 155, resolve: 390, impact: 550, done: 840, label: '激奏' },
    crystalize: { enter: 165, resolve: 480, impact: 820, done: 1090, label: '结晶' }
};

export function castCardPose(mode, ms, { source, hover, drag, opponent }) {
    const timing = alternateCastTimings[mode];
    const crystal = mode === 'crystalize';
    const facing = opponent ? -1 : 1;
    const enter = easeOut(ms / (drag ? timing.enter * 0.65 : timing.enter));
    const gather = envelope(ms, timing.enter, timing.resolve - 22, timing.resolve + 42);
    const release = smooth(windowAt(ms, timing.resolve, timing.resolve + (crystal ? 105 : 135)));
    const scale = mix(source.scale ?? 0.68, 1.06, enter);
    return {
        x: mix(source.x, hover.x, enter) + (crystal ? 0 : facing * (56 * release - 5 * gather)),
        y: mix(source.y, hover.y, enter) - (crystal ? 3 * gather : facing * (24 * release - 2 * gather)),
        scaleX: scale * (1 - 0.035 * gather + (crystal ? -0.84 : 0.20) * release),
        scaleY: scale * (1 + 0.025 * gather - (crystal ? 0.65 : 0.77) * release),
        rotate: mix(facing * -8, 0, enter) + (crystal ? 0 : -4 * gather - 17 * release),
        opacity: (0.2 + 0.8 * windowAt(ms, 0, 75)) * (1 - release),
        labelOpacity: smooth(windowAt(ms, 90, timing.enter + 25))
            * (1 - smooth(windowAt(ms, timing.resolve + 18, timing.resolve + 105)))
    };
}

function polygon(ctx, points) {
    ctx.beginPath();
    ctx.moveTo(...points[0]);
    for (let i = 1; i < points.length; i++) ctx.lineTo(...points[i]);
    ctx.closePath();
}

function glow(ctx, x, y, rx, ry, rgb, opacity) {
    if (opacity <= 0) return;
    ctx.save();
    ctx.translate(x, y);
    ctx.scale(rx, ry);
    const fill = ctx.createRadialGradient(0, 0, 0, 0, 0, 1);
    fill.addColorStop(0, `rgba(${rgb},${opacity})`);
    fill.addColorStop(0.38, `rgba(${rgb},${opacity * 0.43})`);
    fill.addColorStop(1, `rgba(${rgb},0)`);
    ctx.fillStyle = fill;
    ctx.fillRect(-1, -1, 2, 2);
    ctx.restore();
}

function glint(ctx, x, y, radius, color, angle = 0) {
    ctx.save();
    ctx.translate(x, y);
    ctx.rotate(angle);
    ctx.scale(radius, radius);
    polygon(ctx, [[0, -1], [0.12, -0.13], [0.76, 0], [0.12, 0.13],
        [0, 1], [-0.12, 0.13], [-0.76, 0], [-0.12, -0.13]]);
    ctx.fillStyle = color;
    ctx.fill();
    ctx.restore();
}

function paintSpellSeal(ctx, ms, resolve) {
    const light = envelope(ms, 75, resolve - 40, resolve + 193);
    if (!light) return;
    const opening = easeOut(windowAt(ms, 80, resolve));
    const release = easeOut(windowAt(ms, resolve, resolve + 175));
    ctx.save();
    ctx.globalAlpha = light;
    glow(ctx, 0, 0, 91, 99, '29,151,242', 0.30);
    ctx.rotate(-0.32);
    ctx.scale(1, 0.82);
    const radius = 87 - 11 * opening + 23 * release;
    const turn = -0.78 + opening * 0.72 + release * 0.38;
    const enamel = ctx.createLinearGradient(-radius, -radius * 0.8, radius, radius * 0.8);
    enamel.addColorStop(0, '#76d9ff');
    enamel.addColorStop(0.35, '#eefff6');
    enamel.addColorStop(0.62, '#81efe7');
    enamel.addColorStop(1, '#5992ea');
    // Broken arcs keep the cost and the middle of the illustration readable.
    for (let i = 0; i < 6; i++) {
        const start = turn + i * TAU / 6;
        const end = start + (i % 2 ? 0.56 : 0.84) * opening;
        ctx.beginPath();
        ctx.arc(0, 0, radius, start, end);
        ctx.strokeStyle = '#10476d';
        ctx.lineWidth = 4;
        ctx.stroke();
        ctx.strokeStyle = enamel;
        ctx.lineWidth = i % 2 ? 1.3 : 2.1;
        ctx.stroke();
        ctx.beginPath();
        ctx.arc(0, 0, radius - 5, start + 0.03, end + 0.03);
        ctx.strokeStyle = '#65aaff';
        ctx.lineWidth = 0.85;
        ctx.stroke();
        // Small engraved beats give the rotating ring a visible cadence.
        ctx.beginPath();
        for (let tick = 0; tick < 3; tick++) {
            const angle = end - tick * 0.056;
            const dx = Math.cos(angle), dy = Math.sin(angle);
            const outer = radius + (tick === 0 ? 6 : 3);
            ctx.moveTo(dx * (radius + 1), dy * (radius + 1));
            ctx.lineTo(dx * outer, dy * outer);
        }
        ctx.lineWidth = 0.95;
        ctx.strokeStyle = '#c4f9f3';
        ctx.stroke();
    }
    const sweep = -2.58 + opening * 1.40 + release * 1.10;
    for (const angle of [sweep, sweep + Math.PI]) {
        ctx.beginPath();
        ctx.arc(0, 0, radius + 4, angle - 0.65, angle);
        ctx.strokeStyle = '#55cace';
        ctx.lineWidth = 1.5;
        ctx.stroke();
        const x = Math.cos(angle) * (radius + 4), y = Math.sin(angle) * (radius + 4);
        glow(ctx, x, y, 12, 12, '118,239,250', 0.67);
        glint(ctx, x, y, 5.5, '#e7fff9', angle + Math.PI / 2);
    }
    ctx.restore();
}

// A curved, tapered strip gives each streak a broad shoulder and a single sharp tip.
function ribbon(ctx, tip, lane, length, bend, thickness, paint) {
    ctx.beginPath();
    ctx.moveTo(tip - length, lane + bend);
    ctx.bezierCurveTo(tip - length * 0.64, lane + bend + thickness,
        tip - length * 0.25, lane + thickness, tip, lane);
    ctx.bezierCurveTo(tip - length * 0.34, lane - thickness,
        tip - length * 0.70, lane + bend, tip - length, lane + bend);
    ctx.fillStyle = paint;
    ctx.fill();
}

function spellSweep(ms) {
    const { resolve, impact } = alternateCastTimings.accelerate;
    const progress = windowAt(ms, resolve - 60, impact + 103);
    return { progress, tip: mix(-45, 156, smooth(progress)) };
}

const sparkCell = 48;
const DUST = 0, SLIVER = 1, STAR = 2;
const sparkMaterials = [
    { ink: '#678eff', halo: '72,125,255', gatherRadius: 4.1, wakeRadius: 4.7 },
    { ink: '#4ae2ed', halo: '41,222,250', gatherRadius: 5.8, wakeRadius: 6.8 },
    { ink: '#b1fff5', halo: '132,248,255', gatherRadius: 8.5, wakeRadius: 10 }
];
let sparkAtlas;

// Cobalt dust, refractive slivers and a few diffraction stars share one small texture.
function getSparkAtlas(ctx) {
    if (sparkAtlas) return sparkAtlas;
    const sheet = ctx.canvas?.ownerDocument?.createElement('canvas');
    if (!sheet) return null;
    sheet.width = sparkCell * sparkMaterials.length;
    sheet.height = sparkCell;
    const brush = sheet.getContext('2d');
    if (!brush) return null;
    for (const [kind, material] of sparkMaterials.entries()) {
        brush.save();
        brush.translate((kind + 0.5) * sparkCell, sparkCell / 2);
        const halo = brush.createRadialGradient(0, 0, 0, 0, 0, 23);
        halo.addColorStop(0, '#f6fffe');
        halo.addColorStop(0.14, material.ink);
        halo.addColorStop(0.31, `rgba(${material.halo},0.62)`);
        halo.addColorStop(0.63, `rgba(${material.halo},0.17)`);
        halo.addColorStop(1, `rgba(${material.halo},0)`);
        brush.fillStyle = halo;
        brush.fillRect(-24, -24, 48, 48);
        if (kind === SLIVER) {
            polygon(brush, [[-15, 1], [-2, -5], [16, -1], [2, 4]]);
            brush.fillStyle = '#66e8ff';
            brush.fill();
            polygon(brush, [[-10, 1], [-2, -5], [16, -1]]);
            brush.fillStyle = '#e3fffa';
            brush.fill();
        } else if (kind === STAR) {
            glint(brush, 0, 0, 21, '#eafff9', 0.18);
        } else {
            brush.beginPath();
            brush.arc(0, 0, 4.4, 0, TAU);
            brush.fillStyle = '#e3eeff';
            brush.fill();
        }
        brush.restore();
    }
    sparkAtlas = sheet;
    return sheet;
}

// Stable variation keeps a cast identical when scrubbed, mirrored or fast-forwarded.
const variation = (i, step) => ((i + 1) * step % 47) / 46;

const chargingSparks = Array.from({ length: 28 }, (_, i) => {
    const side = i % 2 ? 1 : -1;
    const spread = variation(i, 17);
    const end = Math.PI + side * (0.04 + spread * 0.38);
    const sweep = side * (1.45 + variation(i, 29) * 1.28);
    const start = end - sweep;
    const outer = 87 + 32 * variation(i, 7);
    const inner = 40 + 18 * variation(i, 23);
    // A cubic approximation of a shrinking ellipse. The two streams meet at the blade's heel.
    const tangent = 4 / 3 * Math.tan(sweep / 4);
    const a = { x: outer * Math.cos(start), y: outer * Math.sin(start) * 0.83 };
    const d = { x: inner * Math.cos(end), y: inner * Math.sin(end) * 0.83 };
    const birth = 76 + 151 * variation(i, 37);
    const kind = i % 9 === 0 ? STAR : (i % 3 === 0 ? SLIVER : DUST);
    return {
        a, b: { x: a.x - tangent * outer * Math.sin(start), y: a.y + tangent * outer * Math.cos(start) * 0.83 },
        c: { x: d.x + tangent * inner * Math.sin(end), y: d.y - tangent * inner * Math.cos(end) * 0.83 }, d,
        birth, life: alternateCastTimings.accelerate.resolve + 38 * spread - birth,
        tail: (kind === DUST ? 18 : 42) + 12 * spread,
        radius: sparkMaterials[kind].gatherRadius + 2 * spread,
        kind, gathering: true, brightness: side > 0 ? 0.82 : 1
    };
});

const wakeSparks = Array.from({ length: 42 }, (_, i) => {
    const { resolve, impact, done } = alternateCastTimings.accelerate;
    const band = i % 3;
    const side = i % 2 ? 1 : -1;
    const spread = variation(i, 19);
    const late = i >= 35;
    const birth = late ? impact - 8 + 47 * spread : resolve - 27 + band * 48 + 40 * spread;
    const a = { x: spellSweep(birth).tip - 13 * variation(i, 31), y: side * (1 + band * 3) };
    const backscatter = !late && i % 7 === 0;
    const dx = backscatter ? -22 - 40 * spread : (late ? 31 : 53) + (late ? 40 : 63) * variation(i, 13);
    const dy = side * ((backscatter ? 40 : 15) + 45 * variation(i, 23));
    const kind = [3, 17, 38].includes(i) ? STAR : (i % 3 === 1 ? DUST : SLIVER);
    return {
        a, b: { x: a.x + dx * 0.48, y: a.y + dy * (backscatter ? 0.72 : 0.04) },
        c: { x: a.x + dx * 0.79, y: a.y + dy * 1.16 }, d: { x: a.x + dx, y: a.y + dy },
        birth, life: Math.min(done - 3 - birth, (late ? 216 : 160) + 103 * variation(i, 41)),
        tail: (kind === DUST ? 20 : 43) + 19 * spread,
        radius: (sparkMaterials[kind].wakeRadius + 2.2 * spread) * (late ? 0.83 : 1),
        kind, gathering: false, brightness: 1
    };
});

function sampleSpark(spark, age, point) {
    const t = clamp(age / spark.life);
    const p = spark.gathering ? t * (0.62 + 0.38 * t) : t * (1.85 - 0.85 * t);
    const q = 1 - p;
    const a = q ** 3, b = 3 * q * q * p, c = 3 * q * p * p, d = p ** 3;
    point.x = a * spark.a.x + b * spark.b.x + c * spark.c.x + d * spark.d.x;
    point.y = a * spark.a.y + b * spark.b.y + c * spark.c.y + d * spark.d.y;
}

// Spent slivers shed a pair of finer motes from their actual position and tangent.
const afterglowDust = [2, 5, 9, 17, 21, 26, 30, 35, 38].flatMap(i => {
    const parent = wakeSparks[i];
    const split = parent.life * (0.34 + 0.19 * variation(i, 17));
    const a = {}, previous = {};
    sampleSpark(parent, split, a);
    sampleSpark(parent, split - 6, previous);
    const vx = (a.x - previous.x) / 6, vy = (a.y - previous.y) / 6;
    const speed = Math.hypot(vx, vy);
    const birth = parent.birth + split;
    return [-1, 1].map(side => {
        const kick = side * (5 + 10 * variation(i, 31)) / (speed || 1);
        const dx = vx * 60 - vy * kick, dy = vy * 60 + vx * kick;
        return {
            a, b: { x: a.x + vx * 24, y: a.y + vy * 24 },
            c: { x: a.x + dx * 0.84, y: a.y + dy * 0.9 }, d: { x: a.x + dx, y: a.y + dy - 3 },
            birth, life: Math.min(alternateCastTimings.accelerate.done - 4 - birth, 125 + 49 * variation(i, 13)),
            tail: 18, radius: side > 0 ? 4.5 : 3.3, kind: DUST, gathering: false, brightness: 0.75
        };
    });
});
const releaseSparks = afterglowDust.concat(wakeSparks);

function paintSparks(ctx, ms, sparks) {
    const atlas = getSparkAtlas(ctx);
    const head = {}, middle = {}, tail = {};
    ctx.save();
    ctx.globalCompositeOperation = 'lighter';
    const filament = ctx.createLinearGradient(0, 0, 1, 0);
    filament.addColorStop(0, 'rgba(98,109,255,0)');
    filament.addColorStop(0.34, 'rgba(106,139,255,0.48)');
    filament.addColorStop(0.74, '#64d9ef');
    filament.addColorStop(1, '#d8fff4');
    for (const spark of sparks) {
        const age = ms - spark.birth;
        if (age <= 0 || age >= spark.life) continue;
        const p = age / spark.life;
        const alpha = spark.brightness * smooth(clamp(age / 22)) * (1 - smooth(windowAt(p, spark.gathering ? 0.72 : 0.52, 1)));
        const radius = spark.radius * (1 - 0.26 * p - (spark.gathering ? 0 : 0.26 * smooth(windowAt(p, 0.38, 0.98))));
        sampleSpark(spark, age, head);
        sampleSpark(spark, Math.max(0, age - spark.tail / 2), middle);
        sampleSpark(spark, Math.max(0, age - spark.tail), tail);
        const dx = head.x - tail.x, dy = head.y - tail.y;
        const length = Math.hypot(dx, dy);
        if (length > 0.5) {
            // In this local frame the trail spans 0..1, so every curved tail can reuse the same ink.
            const mx = middle.x - tail.x, my = middle.y - tail.y;
            const cx = 2 * (mx * dx + my * dy) / (length * length) - 0.5;
            const cy = 2 * (my * dx - mx * dy) / length;
            const halfWidth = radius * (spark.kind === DUST ? 0.14 : 0.34);
            ctx.save();
            ctx.transform(dx, dy, -dy / length, dx / length, tail.x, tail.y);
            ctx.beginPath();
            ctx.moveTo(0, 0);
            ctx.quadraticCurveTo(cx, cy + halfWidth, 1, 0);
            ctx.quadraticCurveTo(cx, cy - halfWidth, 0, 0);
            ctx.globalAlpha = alpha * (spark.kind === DUST ? 0.52 : 0.94);
            ctx.fillStyle = spark.kind === DUST ? sparkMaterials[DUST].ink : filament;
            ctx.fill();
            ctx.restore();
        }
        ctx.globalAlpha = alpha;
        if (atlas) {
            ctx.save();
            ctx.translate(head.x, head.y);
            if (spark.kind === SLIVER) ctx.rotate(Math.atan2(head.y - middle.y, head.x - middle.x));
            ctx.drawImage(atlas, spark.kind * sparkCell, 0, sparkCell, sparkCell, -radius, -radius, 2 * radius, 2 * radius);
            ctx.restore();
        } else {
            glint(ctx, head.x, head.y, radius * 0.6, sparkMaterials[spark.kind].ink);
        }
    }
    ctx.restore();
}

function paintSpellBlade(ctx, ms) {
    const { resolve, impact, done } = alternateCastTimings.accelerate;
    const light = envelope(ms, resolve - 90, resolve + 46, done - 62);
    if (!light) return;
    ctx.save();
    ctx.globalAlpha = light;
    const { progress: sweep, tip } = spellSweep(ms);
    const length = 46 + 106 * Math.sin(sweep * Math.PI * 0.81);
    const ink = ctx.createLinearGradient(tip - length, 0, tip, 0);
    ink.addColorStop(0, 'rgba(56,101,244,0)');
    ink.addColorStop(0.30, '#417ce9');
    ink.addColorStop(0.67, '#37dfef');
    ink.addColorStop(0.90, '#c2fff5');
    ink.addColorStop(1, '#ffffff');
    glow(ctx, tip - 18, 0, 79, 19, '32,175,255', 0.71);
    const release = envelope(ms, resolve - 24, resolve + 42, impact + 22);
    if (release > 0) {
        const opening = easeOut(windowAt(ms, resolve - 24, impact + 22));
        ctx.save();
        ctx.translate(-9 + 38 * opening, 0);
        ctx.scale(0.27, 1);
        ctx.beginPath();
        ctx.arc(0, 0, 11 + 35 * opening, -1.45, 3.52);
        ctx.strokeStyle = `rgba(169,255,249,${release * 0.81})`;
        ctx.lineWidth = 2.5;
        ctx.stroke();
        ctx.restore();
    }
    ribbon(ctx, tip - 13, -6, length * 1.05, -18, 4.2, ink);
    ribbon(ctx, tip, 0, length, 12, 8, ink);
    ribbon(ctx, tip - 23, 8, length * 0.82, 25, 3, ink);
    ribbon(ctx, tip, 0, length * 0.64, 3.1, 1.9, '#edffff');
    glint(ctx, tip, 0, 4 + 10 * Math.sin(sweep * Math.PI), '#e3ffff', Math.PI / 2);
    ctx.restore();
}

function paintAccelerate(ctx, ms) {
    const { resolve } = alternateCastTimings.accelerate;
    paintSpellSeal(ctx, ms, resolve);
    ctx.save();
    ctx.rotate(-0.37);
    if (ms < resolve + 38) paintSparks(ctx, ms, chargingSparks);
    paintSpellBlade(ctx, ms);
    if (ms >= resolve - 27) paintSparks(ctx, ms, releaseSparks);
    ctx.restore();
}

const gemOutline = [[0,-1], [0.45,-0.62], [0.62,-0.17], [0.49,0.33], [-0.06,1], [-0.47,0.39], [-0.57,-0.15], [-0.34,-0.66]];
const gemTable = [[-0.11,-0.49], [0.20,-0.39], [0.32,-0.09], [0.20,0.20], [-0.08,0.45], [-0.24,0.19], [-0.30,-0.06], [-0.23,-0.33]];
const bevelTones = [
    ['#fff8cf','#f1ba70','#b67770'], ['#fdd99e','#936184','#654770'],
    ['#d99bb1','#b86f7f','#74466c'], ['#8b6390','#534669','#d78791'],
    ['#c18ea4','#78486a','#fdc78f'], ['#f6b58b','#a05d71','#e69580'],
    ['#ffe3a6','#cf8f61','#8e526d'], ['#fff4cc','#f7ca83','#fff0b2']
];
let prismSprite;

function paintPrism(ctx) {
    polygon(ctx, gemOutline);
    ctx.fillStyle = '#704a64';
    ctx.fill();
    ctx.lineWidth = 0.045;
    ctx.strokeStyle = '#734e68';
    ctx.stroke();
    for (let i = 0; i < gemOutline.length; i++) {
        const j = (i + 1) % gemOutline.length;
        const from = gemOutline[i], to = gemTable[j];
        const cut = ctx.createLinearGradient(...from, ...to);
        const [edge, midtone, inside] = bevelTones[i];
        cut.addColorStop(0, edge);
        cut.addColorStop(0.57, midtone);
        cut.addColorStop(1, inside);
        polygon(ctx, [from, gemOutline[j], to, gemTable[i]]);
        ctx.fillStyle = cut;
        ctx.fill();
        polygon(ctx, [from, gemOutline[j], gemTable[i]]);
        ctx.fillStyle = i % 2 ? 'rgba(250,229,183,0.23)' : 'rgba(67,53,99,0.17)';
        ctx.fill();
    }
    polygon(ctx, gemTable);
    const table = ctx.createLinearGradient(-0.37, -0.55, 0.26, 0.57);
    table.addColorStop(0, '#885c86');
    table.addColorStop(0.26, '#ba6c76');
    table.addColorStop(0.47, '#f9b976');
    table.addColorStop(0.55, '#ffc997');
    table.addColorStop(0.79, '#ae738f');
    table.addColorStop(1, '#645e8d');
    ctx.fillStyle = table;
    ctx.fill();
    ctx.strokeStyle = '#ffe6bbbf';
    ctx.lineWidth = 0.015;
    ctx.stroke();
    polygon(ctx, [gemTable[0], gemTable[2], gemTable[6]]);
    ctx.fillStyle = 'rgba(255,237,191,0.25)';
    ctx.fill();
    polygon(ctx, [gemTable[6], gemTable[3], gemTable[4]]);
    ctx.fillStyle = 'rgba(113,78,143,0.30)';
    ctx.fill();
    ctx.beginPath();
    ctx.moveTo(...gemOutline[6]);
    ctx.lineTo(...gemOutline[7]);
    ctx.lineTo(...gemOutline[0]);
    ctx.lineTo(...gemOutline[1]);
    ctx.strokeStyle = '#fff2c1';
    ctx.lineWidth = 0.026;
    ctx.stroke();
}

function cutGem(ctx, x, y, size, angle, reflection) {
    if (size < 0.5) return;
    ctx.save();
    ctx.translate(x, y);
    ctx.rotate(angle);
    ctx.scale(size, size);
    // The stationary cuts only need rasterizing once. The moving reflection stays live.
    if (!prismSprite && ctx.canvas.ownerDocument) {
        const sprite = ctx.canvas.ownerDocument.createElement('canvas');
        sprite.width = 160;
        sprite.height = 224;
        const brush = sprite.getContext('2d');
        if (brush) {
            brush.setTransform(104, 0, 0, 104, 80, 112);
            brush.lineJoin = 'round';
            paintPrism(brush);
            prismSprite = sprite;
        }
    }
    if (prismSprite) ctx.drawImage(prismSprite, -80 / 104, -112 / 104, 160 / 104, 224 / 104);
    else paintPrism(ctx);
    if (reflection > 0 && reflection < 1) {
        polygon(ctx, gemOutline);
        ctx.clip();
        const offset = mix(-1.5, 1.4, reflection);
        const shine = ctx.createLinearGradient(offset - 0.26, -0.14, offset + 0.26, 0.14);
        shine.addColorStop(0, 'rgba(255,248,221,0)');
        shine.addColorStop(0.55, 'rgba(255,248,221,0.68)');
        shine.addColorStop(1, 'rgba(255,248,221,0)');
        ctx.fillStyle = shine;
        ctx.fillRect(-1, -1, 2, 2);
    }
    ctx.restore();
}

function splinter(ctx, x, y, size, angle) {
    ctx.save();
    ctx.translate(x, y);
    ctx.rotate(angle);
    ctx.scale(size, size);
    polygon(ctx, [[0,-1], [0.33,0.05], [-0.1,0.80], [-0.23,-0.04]]);
    ctx.fillStyle = '#d597a5';
    ctx.fill();
    polygon(ctx, [[0,-1], [-0.08,0.17], [-0.1,0.80], [-0.23,-0.04]]);
    ctx.fillStyle = '#ffe7a8';
    ctx.fill();
    ctx.restore();
}

const shellOutline = [[-36,-75], [32,-75], [56,-49], [56,43], [32,74], [-31,74], [-56,46], [-56,-45]];
const shellInset = [[-31,-61], [27,-65], [46,-40], [43,36], [25,62], [-27,59], [-45,36], [-45,-33]];

function paintCrystalShell(ctx, ms, resolve) {
    const light = envelope(ms, 110, resolve - 80, resolve + 99);
    if (!light) return;
    const grow = easeOut(windowAt(ms, 110, 350));
    const fold = smooth(windowAt(ms, resolve - 18, resolve + 89));
    ctx.save();
    ctx.globalAlpha = light;
    glow(ctx, 0, 5, 90, 101, '235,151,95', 0.37);
    // Both outlines fold into the corresponding cuts of the traveling prism.
    const edge = shellOutline.map(([x,y], i) => [
        mix(x * (1.12 - 0.12 * grow), gemOutline[i][0] * 31, fold),
        mix(y * (1.09 - 0.09 * grow), gemOutline[i][1] * 31, fold)
    ]);
    const inset = shellInset.map(([x,y], i) => [
        mix(x, gemTable[i][0] * 31, fold), mix(y, gemTable[i][1] * 31, fold)
    ]);
    for (let i = 0; i < edge.length; i++) {
        const j = (i + 1) % edge.length;
        const side = ctx.createLinearGradient(...edge[i], ...inset[j]);
        side.addColorStop(0, i < 2 || i > 5 ? '#fff0babc' : '#e1a4a892');
        side.addColorStop(0.43, '#dc966d69');
        side.addColorStop(1, '#a385b235');
        polygon(ctx, [edge[i], edge[j], inset[j], inset[i]]);
        ctx.fillStyle = side;
        ctx.fill();
        polygon(ctx, [edge[i], inset[j], inset[i]]);
        ctx.fillStyle = i % 2 ? '#f9d1972b' : '#72548264';
        ctx.fill();
    }
    polygon(ctx, edge);
    ctx.strokeStyle = '#ffe4b1e3';
    ctx.lineWidth = 1.45;
    ctx.stroke();
    polygon(ctx, inset);
    ctx.strokeStyle = '#ffcdb185';
    ctx.lineWidth = 0.75;
    ctx.stroke();
    for (const i of [2, 5, 7]) {
        const [x,y] = edge[i];
        splinter(ctx, x, y, (i === 5 ? 11 : 7) * grow * (1 - fold), i * 0.72);
    }
    const step = windowAt(ms, 150, resolve + 60) * 5.9;
    for (const offset of [0, 4]) {
        const i = (Math.floor(step) + offset) % 8;
        const next = edge[(i + 1) % 8], fraction = step % 1;
        const x = mix(edge[i][0], next[0], fraction), y = mix(edge[i][1], next[1], fraction);
        glow(ctx, x, y, 9, 9, '255,220,158', 0.75);
        glint(ctx, x, y, offset ? 4.5 : 7, '#fff1c9', 0.2);
    }
    ctx.restore();
}

function paintCrystalLanding(ctx, ms, x, y) {
    const { impact, done } = alternateCastTimings.crystalize;
    const t = windowAt(ms, impact, done);
    if (!t || t === 1) return;
    const spread = easeOut(t);
    ctx.save();
    ctx.translate(x, y);
    ctx.globalAlpha = 1 - smooth(t);
    glow(ctx, 0, 10, 62, 45, '255,174,116', 0.44 * (1 - t));
    ctx.save();
    ctx.translate(0, 23 + 9 * spread);
    ctx.scale(1, 0.34);
    for (let i = 0; i < 2; i++) {
        const radius = 23 + (i ? 36 : 47) * spread;
        ctx.beginPath();
        for (let j = 0; j < 6; j++) {
            const angle = (j + 0.12) * TAU / 6;
            const px = Math.cos(angle) * radius, py = Math.sin(angle) * radius;
            if (j === 0) ctx.moveTo(px, py);
            else ctx.lineTo(px, py);
        }
        ctx.closePath();
        ctx.strokeStyle = i ? '#e6a2a4' : '#ffe1a3';
        ctx.lineWidth = i ? 1.5 : 3;
        ctx.stroke();
    }
    ctx.restore();
    for (let i = 0; i < 7; i++) {
        const angle = i * 2.4;
        const px = Math.cos(angle) * (27 + 32 * spread);
        const py = Math.sin(angle) * 20 + 20 - Math.sin(t * Math.PI) * (12 + i * 2);
        splinter(ctx, px, py, (3 + i % 3) * (1 - t), angle + t * 0.5);
    }
    ctx.restore();
}

function paintCrystalize(ctx, ms, targetX, targetY) {
    const { resolve, impact } = alternateCastTimings.crystalize;
    paintCrystalShell(ctx, ms, resolve);
    const form = smooth(windowAt(ms, resolve - 45, resolve + 78));
    if (form && ms < impact) {
        const travel = smooth(windowAt(ms, resolve + 82, impact));
        const lift = Math.min(42, Math.hypot(targetX, targetY) * 0.24);
        const arcY = t => targetY * t - 4 * lift * t * (1 - t);
        const x = targetX * travel, y = arcY(travel);
        if (travel > 0 && lift > 0) {
            const tail = Math.max(0, travel - 0.36);
            const trail = ctx.createLinearGradient(targetX * tail, arcY(tail), x, y);
            trail.addColorStop(0, 'rgba(216,125,171,0)');
            trail.addColorStop(0.63, 'rgba(244,176,147,0.48)');
            trail.addColorStop(1, '#fff0be');
            ctx.strokeStyle = trail;
            for (const offset of [-4, 2]) {
                ctx.beginPath();
                for (let i = 0; i <= 8; i++) {
                    const t = mix(tail, travel, i / 8);
                    const py = arcY(t) + offset * Math.sin(Math.PI * i / 8);
                    if (i === 0) ctx.moveTo(targetX * t, py);
                    else ctx.lineTo(targetX * t, py);
                }
                ctx.lineWidth = offset < 0 ? 3.2 : 1.1;
                ctx.stroke();
            }
        }
        const size = form * mix(32, 13, travel);
        glow(ctx, x, y, 1 + size * 1.5, 1 + size * 1.65, '255,178,105', 0.58 * form);
        const lean = Math.sign(targetX) * 0.48 * Math.sin(travel * Math.PI);
        cutGem(ctx, x, y, size, lean, windowAt(ms, resolve - 8, impact - 36));
        glint(ctx, x - size * 0.17, y - size * 0.77, size * 0.25, '#fff6d2', 0.15);
    }
    paintCrystalLanding(ctx, ms, targetX, targetY);
}

export function paintAlternateCastEffect(ctx, mode, ms, { hover, target = hover, width, height, opponent }) {
    if (ms < 0 || ms >= alternateCastTimings[mode].done) return;
    ctx.save();
    ctx.translate(hover.x, hover.y);
    ctx.scale(width / 100, height / 140);
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';
    if (mode === 'crystalize') {
        paintCrystalize(ctx, ms, (target.x - hover.x) * 100 / width, (target.y - hover.y) * 140 / height);
    } else {
        if (opponent) ctx.rotate(Math.PI);
        paintAccelerate(ctx, ms);
    }
    ctx.restore();
}
