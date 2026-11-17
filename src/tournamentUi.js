export const tournamentFormats = { plaza: '广场赛', major: '大赛' };
export const tournamentStages = {
    plaza: '广场赛', swiss: '瑞士轮', singleElimination: '单败淘汰', doubleElimination: '双败淘汰'
};
export const tournamentMatchFormats = { bo1: 'BO1', bo3: 'BO3', bo3Ban1: 'BO3 Ban1', bo5: 'BO5' };
export const tournamentBrackets = { winners: '胜者组', losers: '败者组', finals: '决赛' };

export function isTournamentDeckSubmissionOpen(stage, now) {
    return stage.deckSubmissionPending
        && (!stage.deckSubmissionClosesAtUtc || Date.parse(stage.deckSubmissionClosesAtUtc) > now);
}

export function tournamentRoundLabel(bracket, round) {
    if (bracket === 'finals') return round === 1 ? '总决赛' : '总决赛加赛';
    return '第 ' + round + ' 轮';
}

export function tournamentElement(tag, text, className) {
    const node = document.createElement(tag);
    if (text) node.textContent = text;
    if (className) node.className = className;
    return node;
}

export function describeTournamentStage(stage) {
    const name = stage.kind === 'plaza' ? '' : tournamentStages[stage.kind] + ' ';
    const rounds = stage.rounds ? stage.rounds + ' 轮 ' : '';
    return name + rounds + tournamentMatchFormats[stage.matchFormat];
}

export async function fetchTournamentJson(path, options) {
    const response = await fetchTournamentResponse(path, options);
    return response.status === 204 ? null : response.json();
}

export async function fetchTournamentResponse(path, options) {
    const response = await fetch(path, { cache: 'no-store', ...options });
    if (response.ok || response.status === 304) return response;

    let message = '连接赛事服务失败，请重试。';
    if (response.status === 401) {
        message = '请重新登录账号后重试。';
    } else if (response.status === 403) {
        message = '当前账号没有操作权限，请检查登录状态。';
    } else if (response.status < 500) {
        const body = await response.text();
        try {
            const payload = JSON.parse(body);
            message = Object.values(payload?.errors ?? {}).flat().join(' ')
                || payload?.message || (typeof payload === 'string' ? payload : message);
        } catch {
            if (body && !body.startsWith('<')) message = body;
        }
    }
    throw Object.assign(new Error(message), { status: response.status });
}
