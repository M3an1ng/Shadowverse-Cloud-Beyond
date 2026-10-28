export function unwrapStaticValueConfig(value) {
    if (!value) return { ok: false, value: null };
    if (typeof value !== 'object') return { ok: false, value: null };
    if (value.$type === 'StaticValueConfig' && Object.prototype.hasOwnProperty.call(value, 'Value')) {
        return { ok: true, value: value.Value };
    }
    if (Object.prototype.hasOwnProperty.call(value, 'value')) {
        return { ok: true, value: value.value };
    }
    if (Object.prototype.hasOwnProperty.call(value, 'Value')) {
        return { ok: true, value: value.Value };
    }
    return { ok: false, value: null };
}
