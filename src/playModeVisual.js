// Keep payment modes distinct while supporting legacy alternate card definitions.
export function playModeVisual(mode, name = '') {
    const value = String(mode ?? '').trim().toLowerCase();
    if (value === '2' || value === 'accelerate' || /^激奏[:：]/.test(name)) return { label: 'ACC', alternate: true, animation: 'accelerate' };
    if (value === '3' || value === 'crystalize' || /^(结晶|結晶)[:：]/.test(name)) return { label: 'CRY', alternate: true, animation: 'crystalize' };
    if (value === '1' || value === 'enhance') return { label: 'ENH', alternate: false };
    return { label: '', alternate: false };
}
