const TAU = Math.PI * 2;
const clamp = value => Math.min(1, Math.max(0, value));
const ease = value => 1 - (1 - clamp(value)) ** 3;
const mix = (from, to, progress) => from + (to - from) * progress;
const windowAt = (ms, start, end) => clamp((ms - start) / (end - start));
const envelope = (ms, start, peak, end) =>
    windowAt(ms, start, peak) * (1 - windowAt(ms, peak, end));

const cool = '97, 217, 255';
const gold = '255, 187, 74';
const rose = '255, 112, 174';
const white = '255, 249, 231';
const rgba = (rgb, opacity) => `rgba(${rgb},${clamp(opacity)})`;

export const alternateCastTimings = {
    accelerate: { enter: 170, resolve: 425, impact: 550, done: 940, label: '激奏' },
    crystalize: { enter: 175, resolve: 515, impact: 820, done: 1200, label: '结晶' }
};

function glow(ctx, x, y, rx, ry, color, power) {
    if (power <= 0) return;
    ctx.save();
    ctx.translate(x, y);
    ctx.scale(rx, ry);
    const fill = ctx.createRadialGradient(0, 0, 0, 0, 0, 1);
    fill.addColorStop(0, rgba(color, power));
    fill.addColorStop(0.28, rgba(color, power * 0.55));
    fill.addColorStop(1, rgba(color, 0));
    ctx.fillStyle = fill;
    ctx.fillRect(-1, -1, 2, 2);
    ctx.restore();
}

function stroke(ctx, points, color, opacity, width, close = false) {
    ctx.beginPath();
    points.forEach(([x, y], i) => i ? ctx.lineTo(x, y) : ctx.moveTo(x, y));
    if (close) ctx.closePath();
    ctx.lineWidth = width;
    ctx.strokeStyle = rgba(color, opacity);
    ctx.stroke();
}

function star(ctx, x, y, size, color, opacity) {
    ctx.save();
    ctx.translate(x, y);
    ctx.rotate(0.12);
    ctx.beginPath();
    for (let i = 0; i < 8; i++) {
        const angle = i * Math.PI / 4;
        const radius = i % 2 ? size * 0.18 : size;
        ctx.lineTo(Math.cos(angle) * radius * 0.74, Math.sin(angle) * radius);
    }
    ctx.closePath();
    ctx.fillStyle = rgba(color, opacity);
    ctx.fill();
    ctx.restore();
}

function sparks(ctx, x, y, p, color, count, width, height, direction = 0) {
    if (p <= 0 || p >= 1) return;
    const alpha = Math.sin(p * Math.PI) * (1 - p * 0.55);
    for (let i = 0; i < count; i++) {
        const angle = i * 2.39996 + 0.43;
        const speed = 0.45 + ((i * 19) % 31) / 40;
        const distance = ease(p) * speed;
        const dx = Math.cos(angle) * width * distance + direction * p;
        const dy = Math.sin(angle) * height * distance - p * 10;
        if (i % 4 === 0) {
            star(ctx, x + dx, y + dy, 2 + speed * 3, white, alpha);
        } else {
            stroke(ctx, [[x + dx - dx * 0.085, y + dy - dy * 0.085], [x + dx, y + dy]],
                color, alpha, 0.55 + speed);
        }
    }
}

export function castCardPose(mode, ms, { source, hover, drag = false }) {
    const timing = alternateCastTimings[mode];
    const isCrystal = mode === 'crystalize';
    const enter = ease(ms / (drag ? timing.enter * 0.65 : timing.enter));
    const exit = windowAt(ms, timing.resolve, timing.resolve + (isCrystal ? 140 : 125));
    const scale = mix(source.scale ?? 0.68, 1.08, enter);
    return {
        x: mix(source.x, hover.x, enter) + (isCrystal ? 0 : 29 * exit ** 2),
        y: mix(source.y, hover.y, enter) - 18 * exit ** 2,
        scaleX: scale * (1 - exit * (isCrystal ? 0.88 : 0.45)),
        scaleY: scale * (1 - exit * (isCrystal ? 0.84 : 0.20)),
        rotate: mix(-10, 0, enter) + (isCrystal ? 0 : 13 * exit),
        opacity: (0.18 + 0.82 * windowAt(ms, 0, 105)) * (1 - exit),
        sheen: envelope(ms, timing.resolve - 30, timing.resolve + 30, timing.resolve + 155),
        labelOpacity: windowAt(ms, 150, 230) * (1 - windowAt(ms, timing.resolve - 15, timing.resolve + 150))
    };
}

function paintAccelerate(ctx, ms, x, y, w, h) {
    const charge = envelope(ms, 120, 350, 468);
    const sigil = envelope(ms, 325, 449, 705);
    const sweep = windowAt(ms, 360, 700);
    const power = envelope(ms, 345, 473, 760);
    glow(ctx, x, y, w * 1.08, h * 0.83, cool, charge * 0.28);
    if (sigil > 0) {
        ctx.save();
        ctx.translate(x, y);
        ctx.scale(w / 112, h / 154);
        ctx.rotate(ms * 0.0017);
        const radius = 79 + ease(windowAt(ms, 385, 710)) * 30;
        glow(ctx, 0, 0, 122, 105, cool, sigil * 0.34);
        for (const [r, thickness, opacity] of [
            [radius - 4, 0.6, 0.62], [radius, 1.55, 0.95], [radius + 9, 0.7, 0.52]
        ]) {
            ctx.beginPath();
            ctx.arc(0, 0, r, 0, TAU);
            ctx.lineWidth = thickness;
            ctx.strokeStyle = rgba(cool, sigil * opacity);
            ctx.stroke();
        }
        for (let i = 0; i < 48; i++) {
            const angle = i * TAU / 48;
            const dx = Math.cos(angle);
            const dy = Math.sin(angle);
            const length = i % 4 === 0 ? 7 : 3;
            stroke(ctx, [[dx * radius, dy * radius], [dx * (radius + length), dy * (radius + length)]],
                i % 4 === 0 ? white : cool, sigil * 0.74, i % 4 === 0 ? 1.2 : 0.7);
        }
        ctx.restore();
    }

    ctx.save();
    ctx.translate(x, y);
    ctx.rotate(-0.43);
    ctx.scale(w / 112, h / 154);
    for (let i = 0; i < 3; i++) {
        ctx.save();
        ctx.rotate((i - 1) * 0.18);
        ctx.beginPath();
        const start = -2.75 + i * 0.72 + ms * 0.004;
        ctx.ellipse(0, 0, 81 + i * 10, 54 - i * 8, 0, start, start + 2.08);
        ctx.strokeStyle = rgba(i === 1 ? white : cool, charge * (0.72 - i * 0.11));
        ctx.lineWidth = i === 1 ? 0.85 : 1.35;
        ctx.stroke();
        ctx.restore();
    }

    if (power > 0) {
        const center = mix(-68, 153, ease(sweep));
        glow(ctx, center, 0, 134, 36, cool, power * 0.70);
        const light = ctx.createLinearGradient(center - 110, 0, center + 27, 0);
        light.addColorStop(0, rgba(cool, 0));
        light.addColorStop(0.66, rgba(cool, power * 0.62));
        light.addColorStop(1, rgba(white, power * 0.98));
        for (let i = 0; i < 7; i++) {
            const lane = (i - 3) * 8 + Math.sin(i * 3.6) * 6;
            const tip = center + Math.cos(i * 2.1) * 32;
            ctx.beginPath();
            ctx.moveTo(tip - 140 + i * 5, lane * 1.7);
            ctx.quadraticCurveTo(tip - 25, lane * 1.3, tip + 18, lane * 0.45);
            ctx.strokeStyle = light;
            ctx.lineWidth = i === 3 ? 3.4 : i % 2 === 0 ? 1.25 : 0.65;
            ctx.stroke();
        }
        star(ctx, center + 14, 0, 17 * power, white, power);
    }
    ctx.restore();
    sparks(ctx, x, y, windowAt(ms, 405, 870), cool, 27, w * 1.34, h * 0.65, w * 1.10);
}

function crystalShell(ctx, ms, x, y, w, h) {
    const phase = envelope(ms, 210, 472, 666);
    if (!phase) return;
    const settle = ease(windowAt(ms, 225, 449));
    const size = mix(1.40, 1.02, settle) * (1 - 0.67 * windowAt(ms, 524, 666));
    const face = [
        [-0.36, -0.54], [0.32, -0.54], [0.55, -0.34], [0.54, 0.37],
        [0.29, 0.54], [-0.31, 0.54], [-0.55, 0.34], [-0.55, -0.34]
    ];
    const inner = [
        [-0.29, -0.39], [0.22, -0.42], [0.42, -0.24], [0.38, 0.28],
        [0.19, 0.40], [-0.23, 0.39], [-0.38, 0.22], [-0.41, -0.21]
    ];
    const drift = Math.sin(ms * 0.014) * w * 0.055;
    const points = face.map(([dx, dy]) => [x + dx * w * size, y + dy * h * size]);
    const centers = inner.map(([dx, dy]) => [x + dx * w * size + drift, y + dy * h * size]);
    glow(ctx, x, y, w * 1.45, h * 0.91, gold, phase * 0.47);
    for (let i = 0; i < points.length; i++) {
        const next = (i + 1) % points.length;
        const [a, b, c, d] = [points[i], points[next], centers[next], centers[i]];
        const gradient = ctx.createLinearGradient(a[0], a[1], c[0], c[1]);
        gradient.addColorStop(0, rgba(i < 3 ? white : gold, phase * (0.62 - i * 0.035)));
        gradient.addColorStop(1, rgba(i < 5 ? gold : rose, phase * (i % 2 ? 0.28 : 0.11)));
        ctx.beginPath();
        ctx.moveTo(...a);
        ctx.lineTo(...b);
        ctx.lineTo(...c);
        ctx.lineTo(...d);
        ctx.closePath();
        ctx.fillStyle = gradient;
        ctx.fill();
        stroke(ctx, [a, d, c], gold, phase * 0.58, 0.9);
    }
    stroke(ctx, points, gold, phase * 0.52, 6, true);
    stroke(ctx, points, white, phase * 0.92, 1.5, true);
    stroke(ctx, [centers[0], centers[2], centers[4], centers[6]], white, phase * 0.24, 1);
    for (let i = 0; i < 6; i++) {
        const [sx, sy] = points[(i * 3) % points.length];
        star(ctx, sx, sy, 4 + (i % 3) * 3, white, phase * 0.92);
    }
    sparks(ctx, x, y, windowAt(ms, 360, 775), gold, 24, w * 1.38, h * 0.82);
}

function paintCrystalize(ctx, ms, x, y, w, h, target) {
    crystalShell(ctx, ms, x, y, w, h);
    const flash = envelope(ms, 505, 580, 690);
    glow(ctx, x, y, w * 0.91, h * 0.63, white, flash * 0.68);
    star(ctx, x, y, 34 * flash, white, flash);

    const fly = windowAt(ms, 565, 820);
    const arc = t => [mix(x, target.x, t), mix(y, target.y, t) - Math.sin(t * Math.PI) * 36];
    if (fly > 0 && fly < 1) {
        const travel = fly ** 1.46;
        const [tipX, tipY] = arc(travel);
        for (let i = 0; i < 16; i++) {
            const tail = Math.max(0, travel - i * 0.026);
            const [tailX, tailY] = arc(tail);
            glow(ctx, tailX, tailY, 13 - i * 0.36, 13 - i * 0.36, i % 2 ? gold : rose, 0.17 * (1 - i / 17));
        }
        glow(ctx, tipX, tipY, 38, 32, gold, 0.8);
        star(ctx, tipX, tipY, 12, white, 1);
    }

    const land = windowAt(ms, 785, 1160);
    if (land > 0 && land < 1) {
        const alpha = 1 - land;
        glow(ctx, target.x, target.y, w * 0.78, h * 0.60, rose, alpha * 0.48);
        glow(ctx, target.x, target.y, w * 0.54, h * 0.35, gold, alpha ** 2 * 0.52);
        for (let ring = 0; ring < 3; ring++) {
            const radius = (12 + ease(land) * 47 + ring * 9) * w / 112;
            ctx.beginPath();
            ctx.ellipse(target.x, target.y + 6, radius, radius * (0.59 + ring * 0.12), -0.19,
                ms * 0.009 + ring, ms * 0.009 + ring + Math.PI * 1.71);
            ctx.strokeStyle = rgba(ring % 2 ? gold : rose, alpha ** 1.4 * 0.85);
            ctx.lineWidth = 1.4 - ring * 0.32;
            ctx.stroke();
        }
        sparks(ctx, target.x, target.y, land, gold, 17, w * 0.68, h * 0.46);
    }
}

export function paintAlternateCastEffect(ctx, mode, ms, { hover, target, width, height }) {
    if (ms < 0 || ms >= alternateCastTimings[mode].done) return;
    ctx.save();
    ctx.globalCompositeOperation = 'lighter';
    ctx.lineCap = 'round';
    if (mode === 'crystalize') {
        paintCrystalize(ctx, ms, hover.x, hover.y, width, height, target);
    } else {
        paintAccelerate(ctx, ms, hover.x, hover.y, width, height);
    }
    ctx.restore();
}
