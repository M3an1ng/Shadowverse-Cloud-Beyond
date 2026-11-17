import { tournamentBrackets, tournamentElement as item, tournamentRoundLabel, tournamentStages } from './tournamentUi.js';

function button(text, data) {
    const element = item('button', text, 'tournament-button');
    element.type = 'button';
    Object.assign(element.dataset, data);
    return element;
}

function playerName(name, entryId, stageNumber, matchId) {
    const element = item(entryId ? 'button' : 'span', name, 'tournament-player-name');
    element.title = name;
    if (entryId) {
        element.type = 'button';
        element.dataset.stageEntryId = entryId;
        element.dataset.lineupStage = stageNumber;
        if (matchId) element.dataset.playerMatchId = matchId;
        element.setAttribute('aria-haspopup', 'dialog');
    }
    return element;
}

function matchCard(match, fallback, stageNumber = match?.stageNumber) {
    const panel = item('article', '', 'tournament-pairing');
    const completed = match?.isComplete;
    panel.dataset.state = completed ? 'complete' : match ? 'available' : 'waiting';
    const seats = match ? [
        [match.playerOne, match.oneWins, match.entryOneId],
        [match.playerTwo ?? '轮空', match.twoWins, match.entryTwoId]
    ] : fallback.map(seat => [seat.text, null, seat.entryId]);
    for (const [name, wins, entryId] of seats) {
        const row = item('div', '', 'tournament-pairing-seat');
        row.append(playerName(name, entryId, stageNumber, match?.id),
            item('strong', match?.entryTwoId ? String(wins) : ''));
        if (completed && entryId && match.winnerEntryId === entryId) {
            row.dataset.winner = 'true';
            row.setAttribute('aria-label', name + ' 胜出');
        }
        panel.append(row);
    }
    if (match?.isMine && match.entryTwoId) {
        panel.dataset.mine = 'true';
        const action = button(completed ? '查看赛果' : '进入房间', { tournamentMatchId: match.id });
        action.classList.add('tournament-pairing-action');
        panel.append(action);
    }
    return panel;
}

function poolRoundStrip(rounds) {
    const content = item('section', '', 'tournament-schedule-section');
    const strip = item('div', '', 'tournament-round-strip');
    strip.dataset.bracket = 'pool';
    for (const [number, cards] of rounds) {
        const column = item('div', '', 'tournament-round');
        const heading = item('header', '', 'tournament-round-heading');
        const complete = cards.filter(card => card.dataset.state === 'complete').length;
        const progress = item('span', complete + '/' + cards.length, 'tournament-round-progress');
        progress.title = '已结束 ' + complete + ' 场，共 ' + cards.length + ' 场';
        heading.dataset.active = String(complete < cards.length);
        heading.append(item('h5', tournamentRoundLabel('pool', number)), progress);
        column.append(heading, ...cards);
        strip.append(column);
    }
    content.append(strip);
    return content;
}

const matchKey = position => [position.bracket, position.round, position.pairing].join('-');

function bracketConnection(source, destination) {
    const link = item('span', '', 'tournament-bracket-connection');
    link.setAttribute('aria-hidden', 'true');
    link.dataset.from = source.key;
    link.dataset.to = destination.key;
    link.toggleAttribute('data-up', source.lane > destination.lane);
    link.toggleAttribute('data-settled', source.card.dataset.state === 'complete');
    link.style.setProperty('--connection-start', Math.min(source.lane, destination.lane));
    link.style.setProperty('--connection-span', Math.abs(source.lane - destination.lane));
    link.append(item('span', '', 'tournament-bracket-connection-elbow'), item('span', '', 'tournament-bracket-arrow'));
    return link;
}

function addFeaturedFinalScore(card, match) {
    const score = item('div', '', 'tournament-featured-final-score');
    if (match?.entryTwoId) {
        const { oneWins, twoWins } = match;
        score.setAttribute('role', 'img');
        score.setAttribute('aria-label', '决赛比分 ' + oneWins + ' 比 ' + twoWins);
        if (oneWins !== twoWins) score.dataset.leading = oneWins > twoWins ? 'one' : 'two';
        score.append(item('strong', String(oneWins)), item('span', '∶'), item('strong', String(twoWins)));
    } else {
        score.textContent = 'VS';
        score.setAttribute('aria-hidden', 'true');
    }
    card.append(score);
}

function bracketRoundStrip(bracket, rounds, isDouble) {
    const featuredFinal = bracket === 'finals' && rounds.size === 1;
    const section = item('section', '', 'tournament-schedule-section tournament-bracket-section');
    section.dataset.bracketGroup = bracket;
    if (isDouble && bracket !== 'finals') section.append(item('h4', tournamentBrackets[bracket]));
    const strip = item('div', '', 'tournament-round-strip tournament-bracket-strip');
    strip.dataset.bracket = bracket;
    strip.toggleAttribute('data-featured-final', featuredFinal);
    const lastRound = Array.from(rounds.keys()).at(-1);
    const lastLane = Math.max(...Array.from(rounds.values()).flat().map(record => record.lane));
    strip.style.setProperty('--bracket-last-lane', lastLane);
    for (const [number, records] of rounds) {
        const column = item('div', '', 'tournament-round');
        const heading = item('header', '', 'tournament-round-heading');
        let title = tournamentRoundLabel(bracket, number);
        if (!isDouble && number === lastRound) {
            title += ' · 决赛';
            column.setAttribute('data-deciding-round', '');
        } else if (!isDouble && number === lastRound - 1) {
            title += ' · 半决赛';
        }
        heading.append(item('h5', title));
        heading.dataset.active = String(records.some(record => record.card.dataset.state === 'available'));
        const track = item('div', '', 'tournament-bracket-track');
        for (const record of records) {
            if (featuredFinal) addFeaturedFinalScore(record.card, record.match);
            track.append(record.card, ...record.destinations.map(destination => bracketConnection(record, destination)));
        }
        column.append(heading, track);
        strip.append(column);
    }
    section.append(strip);
    return section;
}

function poolRounds(matches) {
    const byRound = new Map();
    for (const match of matches) {
        if (!byRound.has(match.roundNumber)) byRound.set(match.roundNumber, []);
        byRound.get(match.roundNumber).push(match);
    }

    const previousWins = new Map();
    const rounds = new Map();
    for (const [number, roundMatches] of [...byRound].sort(([a], [b]) => a - b)) {
        const ranked = roundMatches.map(match => {
            const one = previousWins.get(match.entryOneId) ?? 0;
            const two = previousWins.get(match.entryTwoId) ?? 0;
            return { match, mostWins: Math.max(one, two), fewestWins: Math.min(one, two) };
        });
        ranked.sort((a, b) => b.mostWins - a.mostWins || b.fewestWins - a.fewestWins
            || a.match.pairingNumber - b.match.pairingNumber);
        rounds.set(number, ranked.map(({ match }) => matchCard(match)));

        for (const match of roundMatches) {
            if (match.isComplete && match.winnerEntryId) {
                previousWins.set(match.winnerEntryId, (previousWins.get(match.winnerEntryId) ?? 0) + 1);
            }
        }
    }
    return rounds;
}

function bracketSections(stage, progress, matches, names) {
    const isDouble = stage.kind === 'doubleElimination';
    const seeds = progress.participants.filter(player => player.canPlay);
    const bySlot = new Map(matches.map(match => [matchKey({
        bracket: match.bracket, round: match.roundNumber, pairing: match.pairingNumber
    }), match]));
    const emptySlots = new Set();
    const sections = new Map();
    const placed = new Map();

    function sourceLabel(slot) {
        const { bracket, round, pairing } = slot.source;
        const group = bracket === 'finals' ? tournamentRoundLabel(bracket, round)
            : (isDouble ? tournamentBrackets[bracket] + ' ' : '') + round + '-' + pairing;
        return group + (slot.takesLoser ? ' 败者' : ' 胜者');
    }

    function resolve(slot) {
        if (slot.seed) {
            const player = seeds[slot.seed - 1];
            return { empty: !player, text: player?.playerName ?? '轮空', entryId: player?.entryId };
        }
        const sourceKey = matchKey(slot.source);
        const source = bySlot.get(sourceKey);
        if (emptySlots.has(sourceKey)) return { empty: true, text: '轮空' };
        if (source?.isComplete) {
            const entryId = slot.takesLoser ? (source.winnerEntryId === source.entryOneId ? source.entryTwoId : source.entryOneId)
                : source.winnerEntryId;
            return { empty: !entryId, text: names.get(entryId) ?? '轮空', entryId, origin: entryId ? slot : null };
        }
        return { empty: false, text: sourceLabel(slot), origin: slot };
    }

    for (const node of progress.bracket) {
        const { position } = node;
        const key = matchKey(position);
        const match = bySlot.get(key);
        if (position.bracket === 'finals' && position.round === 2 && !match) {
            const final = bySlot.get('finals-1-1');
            if (final?.isComplete && final.winnerEntryId === final.entryOneId) continue;
        }
        const one = resolve(node.one);
        const two = resolve(node.two);
        if (!match && one.empty && two.empty) {
            emptySlots.add(key);
            continue;
        }
        if (!sections.has(position.bracket)) sections.set(position.bracket, new Map());
        const rounds = sections.get(position.bracket);
        if (!rounds.has(position.round)) rounds.set(position.round, []);
        const round = rounds.get(position.round);
        const inputs = [one, two];
        const origins = inputs.map(input => input.origin).filter(Boolean);
        const isAdjacent = origin => origin.source.bracket === position.bracket && origin.source.round === position.round - 1;
        const feeds = [...new Set(origins.filter(isAdjacent).map(origin => placed.get(matchKey(origin.source))).filter(Boolean))];
        const preferredLane = feeds.length ? feeds.reduce((total, source) => total + source.lane, 0) / feeds.length : round.length;
        const lane = Math.max(preferredLane, (round.at(-1)?.lane ?? -1) + 1);
        const card = matchCard(match, inputs, stage.number);
        card.classList.add('tournament-bracket-card');
        card.dataset.bracketNode = key;
        card.tabIndex = -1;
        card.style.setProperty('--bracket-lane', lane);
        card.prepend(item('span', '第 ' + position.pairing + ' 场', 'tournament-bracket-card-label'));
        const visibleInputs = match && !match.entryTwoId ? [inputs.find(input => input.entryId === match.entryOneId)] : inputs;
        const rows = card.querySelectorAll('.tournament-pairing-seat');
        rows[0].dataset.side = 'one';
        rows[1].dataset.side = 'two';
        for (const [index, input] of visibleInputs.entries()) {
            const origin = input?.origin;
            if (!origin || isAdjacent(origin) && position.bracket !== 'finals') continue;
            const sourceKey = matchKey(origin.source);
            if (!placed.has(sourceKey)) continue;
            const row = rows[index];
            const name = row.firstElementChild;
            const label = sourceLabel(origin);
            const jump = item('button', label, 'tournament-bracket-source');
            jump.type = 'button';
            jump.dataset.bracketSource = sourceKey;
            jump.dataset.sourceBracket = origin.source.bracket;
            jump.title = '查看来源对局 · ' + label;
            if (!input.entryId) name.textContent = '待定';
            const identity = item('div', '', 'tournament-bracket-player');
            identity.append(name, jump);
            row.prepend(identity);
        }
        const record = { key, lane, card, match, destinations: [] };
        round.push(record);
        placed.set(key, record);
        for (const source of feeds) source.destinations.push(record);
    }
    return [...sections].map(([bracket, rounds]) => bracketRoundStrip(bracket, rounds, isDouble));
}

function poolStandings(stage, standings) {
    const note = item('p', stage.kind === 'plaza' ? '同一逐轮胜负顺序的选手随机配对，组内轮空计一胜。'
        : '依次按胜场、对手胜场总和、小局净胜及初始种子排名。', 'tournament-muted');
    const scroller = item('div', '', 'tournament-standings-wrap');
    const table = item('table', '', 'tournament-standings');
    const header = table.createTHead().insertRow();
    const swiss = stage.kind === 'swiss';
    for (const title of ['名次', '选手', '胜负', swiss ? '对手胜场' : '逐轮战绩', '小局']) {
        const cell = item('th', title);
        cell.scope = 'col';
        header.append(cell);
    }
    const body = table.createTBody();
    for (const row of standings) {
        const tr = body.insertRow();
        const order = row.record.replaceAll('W', '胜 ').replaceAll('L', '负 ').trim();
        const player = item('td');
        player.append(playerName(row.playerName, row.entryId, stage.number));
        tr.append(item('td', String(row.rank)), player);
        for (const text of [row.wins + '–' + row.losses,
            swiss ? row.opponentWins : order || '—', row.gameWins + '–' + row.gameLosses]) {
            tr.append(item('td', String(text)));
        }
    }
    scroller.append(table);
    return [scroller, note];
}

export function renderTournamentSchedule(target, details, selectedNumber) {
    const { tournament, progress = [], matches, entrants } = details;
    const scheduled = tournament.stages.filter(stage => stage.startedAtUtc || stage.deckSubmissionPending);
    const selected = scheduled.find(stage => stage.number === selectedNumber) ?? scheduled.at(-1);
    const number = String(selected?.number ?? '');
    const sameStage = number === target.dataset.stageNumber;
    const scroll = sameStage ? new Map(Array.from(target.querySelectorAll('.tournament-round-strip'),
        strip => [strip.dataset.bracket, strip.scrollLeft])) : null;
    const focused = sameStage && target.contains(document.activeElement) ? document.activeElement.dataset : null;
    const focusMatch = focused?.tournamentMatchId;
    const focusStage = focused?.scheduleStage;
    const focusPlayer = focused?.stageEntryId;
    const focusBracket = focused?.bracketNode;
    target.dataset.stageNumber = number;
    target.hidden = !selected && tournament.registrationStatus !== 'closed';
    target.replaceChildren();
    if (!selected) {
        if (!target.hidden) target.append(item('p', '正在安排首轮。', 'tournament-muted'));
        return;
    }

    const current = progress.find(stage => stage.stageNumber === selected.number);
    const stageMatches = matches.filter(match => match.stageNumber === selected.number);
    const names = new Map((entrants ?? []).map(entrant => [entrant.entryId, entrant.playerName]));
    if (scheduled.length > 1) {
        const chooser = item('div', '', 'tournament-stage-chooser');
        for (const stage of scheduled) {
            const tab = button(tournamentStages[stage.kind], { scheduleStage: stage.number });
            tab.setAttribute('aria-pressed', String(stage.number === selected.number));
            chooser.append(tab);
        }
        target.append(chooser);
    }

    const winner = names.get(selected.winnerEntryId);
    if (winner) {
        const highlight = item('p', '', 'tournament-schedule-winner');
        highlight.toggleAttribute('data-elimination', Boolean(current?.bracket.length));
        highlight.append(item('span', selected.number === tournament.stages.length ? '冠军' : '阶段第一'),
            playerName(winner, selected.winnerEntryId, selected.number));
        target.append(highlight);
    }
    if (selected.number > 1 && current?.participants.length) {
        const players = item('div', '', 'tournament-stage-chooser');
        for (const player of current.participants) {
            if (selected.startedAtUtc && player.canPlay) {
                players.append(button(player.playerName, { stageEntryId: player.entryId, lineupStage: selected.number }));
            } else {
                players.append(item('span', player.playerName + (player.canPlay ? '' : ' · 卡组不足'), 'tournament-muted'));
            }
        }
        target.append(item('h4', '晋级选手'), players);
    }
    if (selected.deckSubmissionPending) {
        target.append(item('p', '卡组重交截止后安排本阶段对局。', 'tournament-muted'));
    } else if (selected.completedAtUtc && current?.participants.filter(player => player.canPlay).length < 2) {
        target.append(item('p', '可参赛人数不足两人，此阶段已结束。', 'tournament-muted'));
        if (current.standings.length) target.append(...poolStandings(selected, current.standings));
    } else if (current?.bracket.length) {
        target.append(...bracketSections(selected, current, stageMatches, names));
    } else if (current?.standings.length) {
        const rounds = poolRounds(stageMatches);
        if (rounds.size) target.append(poolRoundStrip(rounds));
        target.append(...poolStandings(selected, current.standings));
    }
    if (scroll) {
        for (const strip of target.querySelectorAll('.tournament-round-strip'))
            strip.scrollLeft = scroll.get(strip.dataset.bracket) ?? 0;
    }
    if (focusBracket) {
        target.querySelector(`[data-bracket-node="${focusBracket}"]`)?.focus({ preventScroll: true });
    } else if (focusMatch || focusStage || focusPlayer) {
        const button = Array.from(target.querySelectorAll('button')).find(item => focusMatch
            ? item.dataset.tournamentMatchId === focusMatch : focusStage
                ? item.dataset.scheduleStage === focusStage
                : item.dataset.stageEntryId === focusPlayer && item.dataset.playerMatchId === focused.playerMatchId);
        button?.focus({ preventScroll: true });
    }
}
