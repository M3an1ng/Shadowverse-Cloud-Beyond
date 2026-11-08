const playModeNames = { Base: '原始', Enhance: '爆能强化', Accelerate: '激奏', Crystalize: '结晶' };
import {createFeatureModal} from './cardStyles.js';
import {getUserSession} from './userSession.js';
import {showAlert} from './siteDialog.js';
import './styles/local-ai-training.css';
const local=()=>['localhost','127.0.0.1','[::1]','::1'].includes(location.hostname);
async function request(path,options={}) {
 const response=await fetch('/api/local-ai/'+path,{...options,headers:{'Content-Type':'application/json','X-Editor-UserId':getUserSession().userId||'',...options.headers}});
 if(!response.ok){let message=await response.text();try{const data=JSON.parse(message);message=data.error||data.message||message;}catch{}throw Error(message||'请求失败');}
 return path==='export'?response.blob():response.json();
}
async function openTraining() {
 const modal=createFeatureModal('本地 AI 训练');modal.panel.classList.add('local-ai-training');modal.overlay.classList.add('ai-training-dialog');
 const message=document.createElement('p');message.className='muted';message.textContent='从当前账号现有卡组中选择两副进行自对战，继续训练局面网络。验证集改善后保存模型。';
 const version=document.createElement('p');version.className='muted';
 const status=document.createElement('p');status.setAttribute('aria-live','polite');
 const section=(title)=>{const node=document.createElement('section');node.className='ai-training-run';const heading=document.createElement('h3');heading.textContent=title;node.append(heading);return node;};
 const architecture=section('通用判断＋牌组专项');const architectureText=document.createElement('p');architecture.append(architectureText);
 const architectureNote=document.createElement('p');architectureNote.className='muted';architectureNote.textContent='专项网络按当前卡组匹配，其余卡组使用通用模型。卡组由你选择，不导入预设卡组。';architecture.append(architectureNote);
 const live=section('当前训练');const progress=document.createElement('progress');progress.max=1;progress.value=0;progress.setAttribute('aria-label','训练进度');
 const metrics=document.createElement('div');metrics.className='ai-training-deck-grid';const metricValues=[];
 for(const label of ['已完成对局','失败对局','局面样本']){const tile=document.createElement('div');tile.className='ai-training-deck-stat';const text=document.createElement('strong');text.textContent=label;const value=document.createElement('span');value.textContent='0';tile.append(text,value);metrics.append(tile);metricValues.push(value);}
 live.append(progress,status,metrics);
 const row=document.createElement('div');row.className='local-ai-controls';const fields=[];
 for(const label of ['训练卡组','对手卡组']){const wrap=document.createElement('label');wrap.textContent=label;const select=document.createElement('select');select.setAttribute('aria-label',label);wrap.append(select);row.append(wrap);fields.push(select);}
 const gamesLabel=document.createElement('label');gamesLabel.textContent='每轮局数';const games=document.createElement('input');games.type='number';games.min='10';games.max='1000';games.value='20';gamesLabel.append(games);row.append(gamesLabel);
 const parallelLabel=document.createElement('label');parallelLabel.textContent='并行路数';const parallel=document.createElement('select');parallel.setAttribute('aria-label','并行路数');for(let n=1;n<=16;n++){const option=document.createElement('option');option.value=String(n);option.textContent=n+' 路';option.selected=n===2;parallel.append(option);}parallelLabel.append(parallel);row.append(parallelLabel);
 const buttons=document.createElement('div');buttons.className='local-ai-actions';const start=document.createElement('button');start.textContent='开始训练';const stop=document.createElement('button');stop.textContent='停止训练';const download=document.createElement('button');download.textContent='导出模型';buttons.append(start,stop,download);
 const report=document.createElement('p');
 const validation=section('最近模型学习与验证');validation.append(report);
 const note=document.createElement('p');note.className='muted';note.textContent='按完整对局隔离训练集与验证集。验证误差改善后才保存模型；误差下降不代表对战胜率提高。';validation.append(note);
 const continuousLabel=document.createElement('label');continuousLabel.className='local-ai-continuous';const continuous=document.createElement('input');continuous.type='checkbox';const continuousText=document.createElement('span');continuousText.textContent='自动连续训练（每轮结束后自动开始下一轮）';continuousLabel.append(continuous,continuousText);
 const strategyLabel = document.createElement('label');
 strategyLabel.className = 'local-ai-continuous';
 const compareStrategy = document.createElement('input');
 compareStrategy.type = 'checkbox';
 strategyLabel.append(compareStrategy, '大模型对照评测');
 strategyLabel.hidden = true;
 const choicesLabel = document.createElement('label');
 choicesLabel.className = 'local-ai-continuous';
 const strategyChoicesOnly = document.createElement('input');
 strategyChoicesOnly.type = 'checkbox';
 choicesLabel.append(strategyChoicesOnly, '仅在模式和发现时调用大模型');
 choicesLabel.hidden = true;
 compareStrategy.onchange = () => {
  choicesLabel.hidden = !compareStrategy.checked;
  if (compareStrategy.checked) {
   continuous.checked = false;
   games.value = '12';
   parallel.value = '1';
  }
  games.min = compareStrategy.checked ? '12' : '10';
  games.step = compareStrategy.checked ? '4' : '1';
  continuous.disabled = compareStrategy.checked;
  start.textContent = compareStrategy.checked ? '开始对照评测' : '开始训练';
 };
 const controls=section('本地训练设置');controls.append(message,row,continuousLabel,strategyLabel,choicesLabel,buttons);

 const strategyPanel = section('大模型规划');
 const strategySummary = document.createElement('p');
 const retryStrategy = document.createElement('button');
 retryStrategy.textContent = '重试模型';
 retryStrategy.hidden = true;
 retryStrategy.onclick = async () => {
  retryStrategy.disabled = true;
  try { await request('strategy/retry', {method:'POST'}); }
  catch (error) { await showAlert(error.message); }
  finally { retryStrategy.disabled = false; }
 };
 const strategyResults = document.createElement('p');
 const strategyTiming = document.createElement('p');
 const cacheUsage = document.createElement('p');
 const strategyMatchups = document.createElement('ul');
 const guideText = document.createElement('p');
 const turnText = document.createElement('p');
 const rolesDetails = document.createElement('details');
 const rolesTitle = document.createElement('summary');
 rolesTitle.textContent = '查看逐卡定位';
 const roleList = document.createElement('ul');
 rolesDetails.append(rolesTitle, roleList);
 rolesDetails.hidden = true;
 strategyPanel.append(strategySummary,retryStrategy, strategyResults, strategyTiming, cacheUsage, strategyMatchups, guideText, turnText, rolesDetails);
 const history = document.createElement('details');
 const historyTitle = document.createElement('summary');
 historyTitle.textContent = '历史对局与学习经验';
 const historyNote = document.createElement('p');
 historyNote.className = 'muted';
 const refreshHistoryButton = document.createElement('button');
 refreshHistoryButton.type = 'button';
 refreshHistoryButton.textContent = '刷新记录';
 const historyOverview = document.createElement('ul');
 const historyList = document.createElement('div');
 historyList.className = 'ai-strategy-history-list';
 history.append(historyTitle, historyNote, refreshHistoryButton, historyOverview, historyList);
 strategyPanel.append(history);
 let loadingHistory = false;
 let historySignature = '';
 let observedCompletion;
 const refreshHistory = async () => {
  if (loadingHistory) return;
  loadingHistory = true;
  refreshHistoryButton.disabled = true;
  try {
   const data = await request('strategy/history');
   const entries = data.history || [];
   historyNote.textContent = entries.length
    ? `显示最近 ${entries.length} 场有效对局。每副牌组保留最近 60 场，后续规划会读取这些记录。`
    : '还没有保存的经验。大模型给出有效规划且对局打完后，记录会出现在这里。';
   const signature = JSON.stringify([entries, data.reflections]);
   if (signature === historySignature) return;
   historySignature = signature;
   const grouped = new Map();
   for (const entry of entries) {
    const lesson = entry.lesson;
    const group = grouped.get(entry.deckKey) || {
     deck: lesson.deckName || `牌组 ${entry.deckKey.slice(0, 8)}`,
     games: 0, wins: 0, choices: 0, adopted: 0
    };
    group.games += 1;
    group.wins += Number(lesson.won);
    group.choices += (lesson.choices || []).filter(choice => choice.modelSuggested != null).length;
    group.adopted += (lesson.choices || []).filter(choice => choice.modelSuggested).length;
    grouped.set(entry.deckKey, group);
   }
   historyOverview.replaceChildren(...Array.from(grouped.entries(), ([deckKey, group]) => {
    const item = document.createElement('li');
    item.textContent = `${group.deck}：${group.games} 场记录，${group.wins} 胜 ${group.games - group.wins} 负`
     + (group.choices ? ` · 采纳大模型建议的选择 ${group.adopted}/${group.choices} 次` : '');
    if (group.games >= 4) {
     const summarize = document.createElement('button');
     summarize.type = 'button';
     summarize.textContent = '总结已有记录';
     summarize.onclick = async () => {
      summarize.disabled = true;
      try {
       const result = await request('strategy/reflect', { method: 'POST', body: JSON.stringify({ deckKey }) });
       summarize.textContent = result.queued ? '后台总结中，稍后刷新记录' : '总结已是最新或后台正在处理';
      } catch (error) {
       summarize.textContent = error.message;
       summarize.disabled = false;
      }
     };
     item.append(' ', summarize);
    }
    const reflection = data.reflections?.[deckKey];
    if (reflection?.insights?.length) {
     const details = document.createElement('details');
     const summary = document.createElement('summary');
     summary.textContent = '经验总结 · 待验证的打法假设';
     details.append(summary);
     for (const insight of reflection.insights) {
      const paragraph = document.createElement('p');
      paragraph.textContent = insight.hypothesis;
      details.append(paragraph);
      for (const index of insight.sources || []) {
       const evidence = reflection.evidence?.[index];
       if (!evidence) continue;
       const source = document.createElement('p');
       source.className = 'muted';
       source.textContent = `依据对局 ${index + 1} · ${evidence.won ? '胜' : '负'} · ${evidence.first ? '先手' : '后手'} · ${evidence.turns} 回合 · ${evidence.finishedAt ? new Date(evidence.finishedAt).toLocaleString() : ''}`;
       details.append(source);
      }
     }
     item.append(details);
    }
    return item;
   }));
   const classNames = {
    Neutral: '中立', Forestcraft: '精灵', Swordcraft: '皇家护卫', Runecraft: '巫师',
    Dragoncraft: '龙族', Abysscraft: '梦魇', Havencraft: '主教', Portalcraft: '复仇者'
   };
   historyList.replaceChildren(...entries.map(entry => {
    const lesson = entry.lesson;
    const row = document.createElement('details');
    const title = document.createElement('summary');
    const date = lesson.finishedAt ? new Date(lesson.finishedAt).toLocaleString() : '';
    const deck = lesson.deckName || `牌组 ${entry.deckKey.slice(0, 8)}`;
    const opponent = classNames[lesson.opponentClass] || lesson.opponentClass;
    const mode = { benchmark: '对照评测', 'local-match': '本地人机' }[lesson.mode];
    title.textContent = [deck, mode, lesson.won ? '胜' : '负', lesson.first ? '先手' : '后手',
     `对阵 ${opponent}`, `${lesson.turns} 回合`, date].filter(Boolean).join(' · ');
    const end = document.createElement('p');
    end.textContent = lesson.endReason || '已完局';
    const plans = document.createElement('ul');
    for (const plan of lesson.plans || []) {
     const item = document.createElement('li');
     item.textContent = plan;
     plans.append(item);
    }
    const played = document.createElement('p');
    const names = (lesson.played || []).map(id => data.cards?.[id] || '#' + id);
    played.textContent = names.length ? `实际出牌：${names.join('、')}` : '';
    row.append(title, end, plans, played);
    if (lesson.actionComparisons?.length) {
     const details = document.createElement('details');
     const label = document.createElement('summary');
     const changed = lesson.actionComparisons.filter(item => item.changed);
     label.textContent = `模型偏好改变决策 ${changed.length} / ${lesson.actionComparisons.length} 次`;
     const note = document.createElement('p');
     note.className = 'muted';
     note.textContent = '在同一局面关闭本次模型偏好重新排序。只对照当前动作，没有模拟另一条路线的最终胜负。';
     const kinds = { PlayCard: '出牌', PerformAttack: '攻击', PerformEvolve: '进化', PerformSuperEvolve: '超进化', RequestAct: '启动', RequestFusion: '融合', EndTurn: '结束回合', PerformPpBoost: 'PP+1' };
     const describe = action => [kinds[action.kind] || action.kind,
      action.cardId ? data.cards?.[action.cardId] || '#' + action.cardId : '',
      action.abilityName || '', action.playMode ? `${playModeNames[action.playMode] || action.playMode} · 费用 ${action.paidCost ?? '?'} · 第 ${action.handIndex + 1} 张` : '', action.targetCardId ? '→' + (data.cards?.[action.targetCardId] || '#' + action.targetCardId) : ''].filter(Boolean).join(' ');
     const list = document.createElement('ul');
     for (const item of changed) {
      const line = document.createElement('li');
      line.textContent = `第 ${item.turn} 回合 · 本地：${describe(item.local)}；实际：${describe(item.selected)}`;
      list.append(line);
     }
     details.append(label, note, list);
     row.append(details);
    }
    if (lesson.planValidations?.length) {
     const details = document.createElement('details');
     const label = document.createElement('summary');
     label.textContent = `路线约束检查 · ${lesson.planValidations.length} 条错误提议`;
     const list = document.createElement('ul');
     for (const check of lesson.planValidations) {
      const line = document.createElement('li');
      line.textContent = `第 ${check.turn} 回合 · ${check.error} · ${check.repaired ? '模型已修正提议' : '未通过，使用本地判断'}（错误提议并未作为实际出牌记录）`;
      list.append(line);
     }
     details.append(label, list);
     row.append(details);
    }
    if (lesson.proposedPlays?.length) {
     const details = document.createElement('details');
     const label = document.createElement('summary');
     label.textContent = '模型的具体出牌建议';
     const list = document.createElement('ul');
     for (const item of lesson.proposedPlays) {
      const advice = item.advice;
      const line = document.createElement('li');
      line.textContent = `第 ${item.turn} 回合 · 建议${advice.intent === 'use' ? '使用' : '暂缓'}当时第 ${advice.handIndex + 1} 张「${data.cards?.[advice.cardId] || '#' + advice.cardId}」· ${playModeNames[advice.mode] || advice.mode} · 费用 ${advice.cost}`;
      if (item.line?.length) line.textContent += ' · 路线：' + item.line.map(step =>
       `第 ${step.handIndex + 1} 张 / ${playModeNames[step.mode] || step.mode} / ${step.cost}费`).join(' → ')
       + (item.constraintsChecked ? '（费用和已知自动舍弃约束已核对，未完整模拟效果）' : '（未核对路线约束）');
      list.append(line);
     }
     details.append(label, list);
     row.append(details);
    }
    if (lesson.proposedAbilities?.length) {
     const proposed = document.createElement('details');
     const label = document.createElement('summary');
     label.textContent = '模型对能力的建议';
     const list = document.createElement('ul');
     for (const item of lesson.proposedAbilities) {
      const row = document.createElement('li');
      row.textContent = `第 ${item.turn} 回合 · ${item.advice.intent === 'use' ? '优先' : '暂缓'}${item.advice.kind === 'fusion' ? '融合' : '启动'}「${item.advice.name}」· 费用 ${item.advice.cost} · 本地候选排名 ${item.localRank}`;
      list.append(row);
     }
     proposed.append(label, list);
     row.append(proposed);
    }
    if (lesson.abilityActions?.length) {
     const actions = document.createElement('ul');
     for (const action of lesson.abilityActions) {
      const item = document.createElement('li');
      item.textContent = `第 ${action.turn} 回合 · ${action.kind === 'fusion' ? '融合' : '启动'} ${data.cards?.[action.cardId] || '#' + action.cardId} · ${action.name} · 费用 ${action.cost}${action.matchesModelAdvice ? ' · 与模型建议一致' : ''}`;
      actions.append(item);
     }
     row.append(actions);
    }
    if (lesson.attacks?.length) {
     const details = document.createElement('details');
     const label = document.createElement('summary');
     const storm = lesson.attacks.filter(attack => attack.storm);
     label.textContent = `查看攻击取舍 · 疾驰打脸 ${storm.filter(attack => attack.face).length} 次 / 解场 ${storm.filter(attack => !attack.face).length} 次`;
     const list = document.createElement('ol');
     for (const attack of lesson.attacks) {
      const line = document.createElement('li');
      const source = data.cards?.[attack.attackerCardId] || '#' + attack.attackerCardId;
      const target = attack.face ? '敌方主战者' : `${data.cards?.[attack.targetCardId] || '#' + attack.targetCardId}（${attack.targetAttack}/${attack.targetHealth}）`;
      line.textContent = `第 ${attack.turn} 回合 · ${source}${attack.storm ? '〔疾驰〕' : ''} → ${target}${attack.targetKeywords?.includes('Ward') ? '〔守护〕' : ''} · 双方生命 ${attack.ownHealth ?? '?'} / ${attack.enemyHealth ?? '?'} · 敌方公开场面伤害估算 ${attack.visibleEnemyDamage}`;
      list.append(line);
     }
     details.append(label, list);
     row.append(details);
    }
    if (lesson.handActions?.length) {
     const timeline = document.createElement('details');
     const label = document.createElement('summary');
     label.textContent = '查看出牌、弃牌与融合素材';
     const list = document.createElement('ol');
     for (const action of lesson.handActions) {
      const item = document.createElement('li');
      const kind = {play:'使用', discard:'舍弃', fusion:'融合素材'}[action.kind] || action.kind;
      item.textContent = `第 ${action.turn} 回合 · ${kind} ${data.cards?.[action.cardId] || '#' + action.cardId}`;
      if (action.kind === 'play' && action.playMode)
       item.textContent += ` · ${playModeNames[action.playMode] || action.playMode} · 实付 ${action.paidCost ?? '?'} 费`;
      if (action.kind === 'fusion' && action.sourceCardId)
       item.textContent += ` → ${data.cards?.[action.sourceCardId] || '#' + action.sourceCardId}`;
      list.append(item);
     }
     timeline.append(label, list);
     row.append(timeline);
    }
    if (lesson.choices?.length) {
     const choices = document.createElement('ul');
     for (const choice of lesson.choices) {
      const item = document.createElement('li');
      const name = data.cards?.[choice.cardId] || '#' + choice.cardId;
      item.textContent = choice.kind === 'mode'
       ? `第 ${choice.turn} 回合 · ${name} · 模式 ${choice.selected.join('、')}${choice.modelSuggested ? ' · 采纳大模型建议' : choice.policy === 'tactical' ? ' · 战术斩杀' : ''}`
       : `第 ${choice.turn} 回合 · 发现 ${name}${choice.modelSuggested ? ' · 采纳大模型建议' : ''}`;
      if (choice.position) {
       const position = choice.position;
       item.textContent += ` · 当时 PP ${position.pp}，手牌 ${position.handSize}，双方场面 ${position.ownFieldSize}/${position.enemyFieldSize}，生命 ${position.ownHealth ?? '未知'}/${position.enemyHealth ?? '未知'}，敌方可见攻击估算 ${position.visibleEnemyDamage}`;
      }
      choices.append(item);
     }
     row.append(choices);
    }
    if (lesson.leaderDamage?.length) {
     const damage = document.createElement('p');
     const facts = lesson.leaderDamage.map(hit =>
      `T${hit.turn} ${data.cards?.[hit.cardId] || '#' + hit.cardId} ${hit.damage} 点`);
     damage.textContent = '对敌方主战者的实际伤害：' + facts.join('、');
     row.append(damage);
    }
    return row;
   }));
  } catch (error) {
   historyNote.textContent = `无法读取历史：${error.message}`;
  } finally {
   loadingHistory = false;
   refreshHistoryButton.disabled = false;
  }
 };
 history.addEventListener('toggle', () => { if (history.open) void refreshHistory(); });
 refreshHistoryButton.onclick = () => void refreshHistory();
 let renderedRoles = '';
 const showStrategy = (data, s) => {
  const completion = `${s.compareStrategy}:${s.completed}:${s.strategyGames}`;
  if (completion !== observedCompletion) {
   observedCompletion = completion;
   if (history.open) void refreshHistory();
  }
  strategyLabel.hidden = !data.strategyConfigured;
  strategySummary.textContent = data.strategyConfigured
   ? `已接入 ${data.strategyModel || '大模型'}。勾选对照评测后，只有实验侧使用大模型规划。`
   : '尚未配置大模型。普通训练使用本地模型。';
  if (data.providerStatus?.available === false && data.strategyConfigured)
   strategySummary.textContent = data.providerStatus.message;
  retryStrategy.hidden = !data.strategyConfigured || data.providerStatus?.available !== false;
  retryStrategy.disabled = s.running;
  const completed = s.completed || 0;
  strategyResults.textContent = s.compareStrategy
   ? `实验侧 ${s.wins || 0} 胜 / ${s.losses || 0} 负 / ${s.draws || 0} 平 · ${s.strategyGames || 0}/${completed} 个完局获得模型建议 · ${s.strategyPlans || 0} 次规划`
   : '普通训练不调用大模型，继续按现有流程训练局面网络。';
  const timing = (label, metrics) => {
   if (!metrics?.attempts) return '';
   const average = Math.round(metrics.totalMs / metrics.attempts);
   return `${label} ${metrics.accepted}/${metrics.attempts} 次采纳，平均 ${average} ms，最长 ${metrics.maxMs} ms`
    + `（超时 ${metrics.timedOut}，无效 ${metrics.invalid}，错误 ${metrics.errors}）`;
  };
  const usage = data.providerUsage;
  cacheUsage.textContent = usage?.promptTokens > 0
   ? `本次服务启动以来 · API 输入 ${usage.promptTokens} tokens · 缓存命中 ${usage.cachedTokens} tokens（${(100 * usage.cachedTokens / usage.promptTokens).toFixed(1)}%） · 输出 ${usage.completionTokens} tokens`
   : '等待 API 返回缓存用量统计';
  const groups = new Map();
  for (const match of s.strategyMatches ?? []) {
   const key = match.deckId + ':' + match.first;
   const group = groups.get(key) ?? { name: match.deckName, first: match.first, win: 0, loss: 0, draw: 0, total: 0, elapsed: 0 };
   group[match.result]++;
   group.total++;
   group.elapsed += match.elapsedMs;
   groups.set(key, group);
  }
  strategyMatchups.replaceChildren(...[...groups.values()]
   .sort((a, b) => a.name.localeCompare(b.name, 'zh-CN') || Number(b.first) - Number(a.first))
   .map(group => {
    const item = document.createElement('li');
    item.textContent = `${group.name} · ${group.first ? '先手' : '后手'} · ${group.win} 胜 ${group.loss} 负 ${group.draw} 平 · 每局平均 ${(group.elapsed / group.total / 1000).toFixed(1)} 秒`;
    return item;
   }));
  strategyMatchups.hidden = groups.size === 0;
  strategyTiming.textContent = s.compareStrategy
   ? [timing('回合规划', s.turnDecisions), timing('模式与发现', s.optionDecisions)].filter(Boolean).join(' · ')
   : '';
  const latest = s.latestStrategy;
  const roles = latest?.guide?.cards || [];
  guideText.textContent = latest?.guide ? `牌组打法（参考 ${latest.priorGames || 0} 场历史对局）：${latest.guide.gamePlan}` : '';
  const labels = { stabilize: '稳场', pressure: '施压', setup: '准备组合', value: '赚取资源' };
  const names = (ids = []) => ids.map(id => roles.find(card => card.id === id)?.name || '#' + id).join('、');
  turnText.textContent = latest?.plan
   ? `第 ${latest.planTurn} 回合 · ${labels[latest.plan.focus] || latest.plan.focus}：${latest.plan.plan}`
      + (latest.plan.prioritize?.length ? ` · 优先：${names(latest.plan.prioritize)}` : '')
      + (latest.plan.hold?.length ? ` · 保留：${names(latest.plan.hold)}` : '')
   : '';
  if (latest?.plan?.ability) {
   const advice = latest.plan.ability;
   turnText.textContent += ` · 建议${advice.intent === 'use' ? '优先' : '暂缓'}${advice.kind === 'fusion' ? '融合' : '启动'}「${advice.name}」（费用 ${advice.cost}）`;
  }

  const signature = JSON.stringify(roles);
  if (signature !== renderedRoles) {
   renderedRoles = signature;
   roleList.replaceChildren(...roles.map(card => {
    const item = document.createElement('li');
    item.textContent = `${card.name || '#' + card.id}：${card.role}`;
    return item;
   }));
  }
  rolesDetails.hidden = roles.length === 0;
  if (s.running) compareStrategy.checked = Boolean(s.compareStrategy);
  compareStrategy.disabled = s.running;
  if (s.running) strategyChoicesOnly.checked = Boolean(s.strategyChoicesOnly);
  strategyChoicesOnly.disabled = s.running;
  choicesLabel.hidden = !data.strategyConfigured || !compareStrategy.checked;
  start.textContent = compareStrategy.checked ? '开始对照评测' : '开始训练';
 };
 const winrates=section('卡组胜率');const winGrid=document.createElement('div');winGrid.className='ai-training-deck-grid';const winTiles=[];
 for(let i=0;i<2;i++){const tile=document.createElement('div');tile.className='ai-training-deck-stat';const name=document.createElement('strong');const percent=document.createElement('span');const details=document.createElement('small');tile.append(name,percent,details);winGrid.append(tile);winTiles.push({name,percent,details});}
 const winNote=document.createElement('p');winNote.className='muted';winNote.textContent='本次训练任务累计的卡组自对战胜率，双方交替换边；失败或中断对局不计入。不是模型的独立评测胜率。';winrates.append(winGrid,winNote);
 modal.body.append(version,architecture,live,winrates,controls,strategyPanel,validation);let busy=false,timer,hasDecks=false;
 const refresh=async()=>{if(!modal.overlay.isConnected)return;try{const data=await request('status');version.textContent=`当前对手：${data.modelVersion||'尚未载入'} · 每2秒刷新`;architectureText.textContent=`已载入 ${data.models} 个网络 · ${data.specialists} 组牌组专项 · 本地训练继续更新局面评估网络。`;const s=data.training;showStrategy(data,s);if(s.running){continuous.checked=s.continuous;parallel.value=String(s.parallel);games.value=String(s.planned);if(s.playerDeckId)fields[0].value=s.playerDeckId;if(s.opponentDeckId)fields[1].value=s.opponentDeckId;}const total=s.totalCompleted||0;for(let i=0;i<2;i++){const wins=i===0?s.wins:s.losses;const losses=i===0?s.losses:s.wins;winTiles[i].name.textContent=s.compareStrategy
 ? (i===0?'大模型实验侧':'本地模型对照侧')
 : (i===0?'训练卡组':'对手卡组')+' · '+((i===0?s.playerDeckName:s.opponentDeckName)||'尚未选择');winTiles[i].percent.textContent=total?((wins||0)/total*100).toFixed(1)+'%':'--';winTiles[i].details.textContent=`${wins||0}胜 ${losses||0}负 ${s.draws||0}平 · ${total}局`;}
progress.max=Math.max(1,s.planned);progress.value=s.completed+s.failed;metricValues[0].textContent=String(s.completed);metricValues[1].textContent=String(s.failed);metricValues[2].textContent=String(s.samples);live.classList.toggle('ai-current-evaluation',s.running);status.textContent=`${s.message} · 第${s.round||1}轮${s.continuous?" · 自动连续":""} · 完成 ${s.completed}/${s.planned} · ${s.parallel||1}路并行 · 当前同时训练 ${s.active||0} 局`;report.textContent=s.compareStrategy?'对照评测记录规划和胜负，模型权重由普通训练更新。':s.before==null?'等待收集足够的完整对局，开始局面模型学习。':`验证误差：${s.before.toFixed(4)} → ${s.after.toFixed(4)}${s.promoted?' · 已保存新模型':''}`;start.disabled=busy||s.running||!hasDecks;fields.forEach(field=>field.disabled=s.running);games.disabled=s.running;parallel.disabled=s.running;continuous.disabled=s.running||compareStrategy.checked;stop.disabled=!s.running;download.disabled=s.running||!data.modelVersion;}catch(e){status.textContent=e.message;}if(modal.overlay.isConnected)timer=setTimeout(refresh,2000);};
 start.onclick=async()=>{
  if (compareStrategy.checked && (Number(games.value) < 12 || Number(games.value) % 4 !== 0 || Number(parallel.value) > 2)) {
   await showAlert('大模型评测至少 12 局，局数须为 4 的倍数，并行最多 2 路。');
   return;
  }
  busy=true;start.disabled=true;try{await request('start',{method:'POST',body:JSON.stringify({playerDeckId:fields[0].value,opponentDeckId:fields[1].value,games:Number(games.value),parallel:Number(parallel.value),continuous:continuous.checked,compareStrategy:compareStrategy.checked,strategyChoicesOnly:compareStrategy.checked&&strategyChoicesOnly.checked})});}catch(e){await showAlert(e.message);}finally{busy=false;clearTimeout(timer);void refresh();}};
 stop.onclick=async()=>{try{await request('stop',{method:'POST'});clearTimeout(timer);void refresh();}catch(e){await showAlert(e.message);}};
 download.onclick=async()=>{download.disabled=true;try{const blob=await request('export');const url=URL.createObjectURL(blob);const link=document.createElement('a');link.href=url;link.download='svnebula-ai-model.json';link.click();setTimeout(()=>URL.revokeObjectURL(url),30000);}catch(e){await showAlert(e.message);}finally{download.disabled=false;}};
 try{const response=await fetch('/api/decks?userId='+encodeURIComponent(getUserSession().userId));if(!response.ok)throw Error('卡组载入失败');const decks=await response.json();for(const select of fields)for(const deck of decks){const option=document.createElement('option');option.value=deck.deckId;option.textContent=deck.name;select.append(option);}hasDecks=decks.length>0;if(!hasDecks)message.textContent='请先在牌组编辑中创建训练卡组。';void refresh();}catch(e){status.textContent=e.message;}
}
export async function installLocalAiTraining() {
 document.getElementById('local-ai-training-entry')?.remove();if(!local()||!getUserSession().userId)return;
 try{if(!(await request('capabilities')).enabled)return;const grid=document.querySelector('.dev-tools-grid');if(!grid)return;const card=document.createElement('section');card.id='local-ai-training-entry';card.className='tool-card';const title=document.createElement('h3');title.textContent='AI 训练';const text=document.createElement('p');text.textContent='仅本地开放 · 使用现有卡组继续训练与导出模型';const button=document.createElement('button');button.className='tool-button';button.textContent='打开 AI 训练';button.onclick=()=>openTraining().catch(e=>showAlert(e.message));card.append(title,text,button);grid.append(card);}catch{}
}
document.addEventListener('svb:user-session-changed',installLocalAiTraining);
