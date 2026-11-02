export function shouldAutoConfirmChoice(request, selectedCount, settings) {
    const minimum = Number(request?.MinSelectionCount);
    const maximum = Number(request?.MaxSelectionCount);
    if (!Number.isInteger(maximum) || maximum < 1 || minimum !== maximum || selectedCount !== maximum) return false;
    return (request.TargetKey === 'decision' ? settings.autoConfirmModes : settings.autoConfirmTargets) === true;
}
