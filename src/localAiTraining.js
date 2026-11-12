const playModeNames = { Base: '原始', Enhance: '爆能强化', Accelerate: '激奏', Crystalize: '结晶' };
import {createFeatureModal} from './cardStyles.js';
import {getUserSession} from './userSession.js';
import {showAlert} from './siteDialog.js';
import {ensureEnumLocalization,getCachedEnumLabel} from './enumLocalizationCache.js';
import './styles/local-ai-training.css';
const actionNames = {PlayCard:'出牌', PerformAttack:'攻击', PerformEvolve:'进化', PerformSuperEvolve:'超进化',
 RequestAct:'启动', RequestFusion:'融合', PerformPpBoost:'切换PP+1', EndTurn:'结束回合'};
function describeAction(action, cards) {
 const card = id => cards?.[id] || '#' + id;
 return [actionNames[action.kind] || action.kind, action.cardId && card(action.cardId), action.abilityName,
  action.targetCardId && '→ ' + card(action.targetCardId)].filter(Boolean).join(' ');
}
function describeRootSpot({zone, index, cardId, side}, cards, showSide=false) {
  const place = {hand:'手牌',field:'战场',emblem:'纹章'}[zone];
  const owner = showSide ? (side==='own' ? '我方' : '敌方') : '';
 return owner + (cards?.[cardId] || '#' + cardId) + (place && index != null ? `（${place}第 ${index+1} 张）` : '');
}
function describeRootMove(move, cards) {
 const mode = move.playMode ? playModeNames[move.playMode] || move.playMode : '';
 return [actionNames[move.kind] || move.kind, move.source && describeRootSpot(move.source, cards), mode,
  move.paidCost == null ? '' : `${move.paidCost} PP`,
  move.abilityDefinitionId == null ? '' : `能力 #${move.abilityDefinitionId}`,
  move.targets?.length ? '→ ' + move.targets.map(card => describeRootSpot(card, cards, true)).join('、') : ''].filter(Boolean).join(' ');
}
function describeSubchoice(choice, cards) {
 if (choice.cancelled) return '取消';
 const kind = {mode:'模式',discover:'发现',target:'目标'}[choice.kind] || '选择';
 const selected = choice.selected.map(option => {
  if (option.location) return describeRootSpot(option.location, cards, true);
  if (choice.kind === 'mode') return [`第 ${option.index+1} 项`, option.name].filter(Boolean).join(' ');
  return option.name || cards?.[option.cardId] || '#' + option.cardId;
 });
 return [kind, selected.length ? selected.join('、') : '空选'].join(' ');
}
const local=()=>['localhost','127.0.0.1','[::1]','::1'].includes(location.hostname);
async function request(path,options={}) {
 const response=await fetch('/api/local-ai/'+path,{...options,headers:{'Content-Type':'application/json','X-Editor-UserId':getUserSession().userId||'',...options.headers}});
 if(!response.ok){let message=await response.text();try{const data=JSON.parse(message);message=data.error||data.message||message;}catch{}throw Error(message||'请求失败');}
 return path==='export'?response.blob():response.json();
}
async function openTraining() {
 const modal=createFeatureModal('本地 AI 训练');modal.panel.classList.add('local-ai-training');modal.overlay.classList.add('ai-training-dialog');
 const message=document.createElement('p');message.className='muted';message.textContent='从现有卡组中选择两副进行自对战，继续训练局面网络。误差改善后保留候选，通过新旧模型实战对照后才替换。';
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
 const validationList=document.createElement('ul');validationList.className='muted';validation.append(validationList);
 const modelEvaluation=document.createElement('p');modelEvaluation.setAttribute('aria-live','polite');validation.append(modelEvaluation);
 let validationKey='';
 const showLocalValidation=(items=[])=>{
  const key=JSON.stringify(items);if(key===validationKey)return;validationKey=key;validationList.replaceChildren();
  for(const item of items){const row=document.createElement('li');const loss=Number.isFinite(item.before)&&Number.isFinite(item.after)?` · 误差 ${item.before.toFixed(4)} → ${item.after.toFixed(4)}`:'';
   row.textContent=`${item.scope} · 训练 ${item.trainingGames} 局 / 验证 ${item.validationGames} 局${loss} · ${item.accepted?'误差验证通过':item.reason||'保留原网络'}${item.accepted&&item.selectedEpoch?` · 采用第${item.selectedEpoch}遍`:''}`;
   if(item.epochs?.length){const detail=document.createElement('details');const title=document.createElement('summary');title.textContent='查看每遍训练的误差';const list=document.createElement('ul');
    const show=value=>Number.isFinite(value)?value.toFixed(4):'--';
    for(const epoch of item.epochs){const line=document.createElement('li');line.textContent=`${epoch.epoch===0?'训练前':`第${epoch.epoch}遍`} · 训练 ${show(epoch.trainingLoss)} · 验证 ${show(epoch.validationLoss)}`
      +(Number.isFinite(epoch.turnStartLoss)?` · 回合开始 ${show(epoch.turnStartLoss)}`:'')+(Number.isFinite(epoch.afterActionLoss)?` · 动作后 ${show(epoch.afterActionLoss)}`:'');list.append(line);}
    detail.append(title,list);row.append(detail);}
   validationList.append(row); }
 };
 const note=document.createElement('p');note.className='muted';note.textContent='正在读取本地学习与验证方式。';validation.append(note);
 const continuousLabel=document.createElement('label');continuousLabel.className='local-ai-continuous';const continuous=document.createElement('input');continuous.type='checkbox';const continuousText=document.createElement('span');continuousText.textContent='自动连续训练（每轮结束后自动开始下一轮）';continuousLabel.append(continuous,continuousText);
 const strategyLabel = document.createElement('label');
 strategyLabel.className = 'local-ai-continuous';
 const compareStrategy = document.createElement('input');
 compareStrategy.type = 'checkbox';
 strategyLabel.append(compareStrategy, '大模型对照评测');
 strategyLabel.hidden = true;
 const experienceLabel = document.createElement('label');
 experienceLabel.className = 'local-ai-continuous';
 const compareExperience = document.createElement('input');
 compareExperience.type = 'checkbox';
 experienceLabel.append(compareExperience, '历史经验对照（两组都调用大模型）');
 experienceLabel.hidden = true;
 const choicesLabel = document.createElement('label');
 choicesLabel.className = 'local-ai-continuous';
 const strategyChoicesOnly = document.createElement('input');
 strategyChoicesOnly.type = 'checkbox';
 choicesLabel.append(strategyChoicesOnly, '仅在模式和发现时调用大模型');
 choicesLabel.hidden = true;
 const seedLabel = document.createElement('label');
 seedLabel.className = 'local-ai-continuous';
 seedLabel.hidden = true;
 const benchmarkSeed = document.createElement('input');
 benchmarkSeed.type = 'number';
 benchmarkSeed.min = '-2147483648';
 benchmarkSeed.max = '2147483647';
 benchmarkSeed.step = '1';
 benchmarkSeed.value = '18431';
 benchmarkSeed.required = true;
 seedLabel.append('起手种子', benchmarkSeed);
 const projectionLabel=document.createElement('label');projectionLabel.className='local-ai-continuous';projectionLabel.hidden=true;
 const compareCombatProjection=document.createElement('input');compareCombatProjection.type='checkbox';
 projectionLabel.append(compareCombatProjection,'攻击前效果对照（免费，不训练或替换模型）');
 let supportsCombatProjection=false;
 let supportsExperience=false;
 const groupSize = () => compareStrategy.checked && compareExperience.checked ? 8 : 4;
 const updateRunOptions = () => {
  const strategy = compareStrategy.checked;
  const projection = compareCombatProjection.checked;
  experienceLabel.hidden = !strategy || !supportsExperience;
  choicesLabel.hidden = !strategy;
  seedLabel.hidden = !strategy;
  games.min = String(strategy ? groupSize() : projection ? 12 : 10);
  games.step = String(strategy || projection ? groupSize() : 1);
  continuous.disabled = strategy || projection;
  start.textContent = strategy || projection ? '开始对照评测' : '开始训练';
 };
 compareCombatProjection.onchange=()=>{
  if(compareCombatProjection.checked){
   compareStrategy.checked=false;
   compareExperience.checked=false;
   strategyChoicesOnly.checked=false;
   continuous.checked=false;
   games.value='24';
   parallel.value='1';
  } else if(Number(games.value)<10) games.value='10';
  updateRunOptions();
 };
 compareStrategy.onchange = () => {
  if (compareStrategy.checked) {
   compareCombatProjection.checked=false;
   continuous.checked=false;
   games.value='4';
   parallel.value='1';
  } else {
   compareExperience.checked=false;
   if(Number(games.value)<10) games.value='10';
  }
  updateRunOptions();
 };
 compareExperience.onchange = () => {
  const size=groupSize();
  if(Number(games.value)<size || Number(games.value)%size!==0) games.value=String(size);
  updateRunOptions();
 };
 const controls=section('本地训练设置');controls.append(message,row,continuousLabel,strategyLabel,experienceLabel,choicesLabel,seedLabel,projectionLabel,buttons);

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
 const rootDifferences = document.createElement('details');
 const rootTitle = document.createElement('summary');
 const rootNote = document.createElement('p');
 rootNote.className = 'muted';
 rootNote.textContent = '对照已完成的根动作及其中引擎接收的选择。核对公开战场、资源、选择方手牌和选项。未记录的效果与状态需在回放中核对。';
 const rootList = document.createElement('ul');
 rootDifferences.append(rootTitle, rootNote, rootList);
 rootDifferences.hidden = true;
 const experienceExamples = document.createElement('details');
 const exampleTitle = document.createElement('summary');
 exampleTitle.textContent = '查看已结算的不同动作';
 const exampleNote = document.createElement('p');
 exampleNote.className = 'muted';
 exampleNote.textContent = '列出各局实际执行的动作及同局面的本地候选。结算变化来自已执行的动作，另一组的对局路径可能不同。';
 const exampleList = document.createElement('ul');
 experienceExamples.append(exampleTitle, exampleNote, exampleList);
 experienceExamples.hidden = true;
 const guideText = document.createElement('p');
 const turnText = document.createElement('p');
 const experienceText=document.createElement('p');experienceText.className='muted';
 const rolesDetails = document.createElement('details');
 const rolesTitle = document.createElement('summary');
 rolesTitle.textContent = '查看逐卡定位';
 const roleList = document.createElement('ul');
 rolesDetails.append(rolesTitle, roleList);
 rolesDetails.hidden = true;
 strategyPanel.append(strategySummary,retryStrategy, strategyResults, strategyTiming, cacheUsage, strategyMatchups, rootDifferences, experienceExamples, guideText, turnText, experienceText, rolesDetails);
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
   const [data] = await Promise.all([request('strategy/history'),ensureEnumLocalization('CardKeyword')]);
   const entries = data.history || [];
   historyNote.textContent = entries.length
    ? `显示最近 ${entries.length} 场已完局记录。每副牌组保留最近 60 场，包括模型建议失败后由本地策略完成的对局。`
    : '还没有保存的经验。尝试过模型规划或选择的对局完成后，记录会出现在这里。';
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
      for (const draw of insight.drawObservations || []) {
       const evidence = document.createElement('p');
       evidence.className = 'muted';
       const batches = draw.batches.map(batch => {
        const name = batch.sourceCardId ? data.cards?.[batch.sourceCardId] || '#' + batch.sourceCardId : '来源未知';
        return `${name}抽到 ${batch.resolved}/${batch.requested} 张`;
       }).join('，');
       const exhausted = draw.result === 'lost' ? '抽空判负' : '抽空';
       const exhaustion = draw.deckExhausted
        ? draw.exhaustedBatchIndex == null ? `${exhausted}，失败批次未知` : `第 ${draw.exhaustedBatchIndex + 1} 批${exhausted}`
        : draw.truncated ? '后续记录不完整' : '';
       evidence.textContent = [`对局 ${draw.gameIndex + 1} 第 ${draw.turn} 回合`, batches,
        `牌堆原有 ${draw.ownDeckBefore ?? '未知'} 张`, `手牌 ${draw.handBefore} → ${draw.handAfter}`, exhaustion]
        .filter(Boolean).join(' · ');
       details.append(evidence);
      }
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
    const planCaptures = new Map((lesson.planTextCaptures || []).map(capture => [capture.index, capture]));
    for (const [index, plan] of (lesson.plans || []).entries()) {
     const item = document.createElement('li');
     item.textContent = plan;
     const capture = planCaptures.get(index);
     if (!capture || capture.truncated) {
      const note = document.createElement('p');
      note.className = 'muted';
      note.textContent = capture
       ? `规划说明已省略部分内容（保留 ${capture.storedCharacters} / ${capture.originalCharacters} 个字符）`
       : '旧记录未记录文本完整度，说明可能不完整';
      item.append(note);
     }
     plans.append(item);
    }
    const played = document.createElement('p');
    const names = (lesson.played || []).map(id => data.cards?.[id] || '#' + id);
    played.textContent = names.length ? `实际出牌${lesson.recording?.sections?.plays?.omitted ? '（保留片段）' : '记录'}：${names.join('、')}` : '';
    row.append(title, end);
    if (plans.children.length) {
     const planNote=document.createElement('p');planNote.className='muted';
     planNote.textContent='模型返回的规划说明（未验证，仅供复盘核对）';row.append(planNote,plans);
    }
    row.append(played);
    const ruleNote = document.createElement('p');
    ruleNote.className = 'muted';
    ruleNote.textContent = ({
     'same-config-and-engine': '规则版本与当前一致',
     'config-changed': '卡牌配置已更新，这份经验需要按当前规则重新核对',
     'engine-changed': '程序版本已更新，这份经验需要重新核对',
     'mixed-observed-configs': '记录期间规则库发生过更新，经验需要重新核对',
     unknown: '旧记录或缺少规则版本，无法确认是否与当前规则一致'
    })[entry.ruleCompatibility || 'unknown'];
    row.append(ruleNote);
    if (lesson.turnDecisions || lesson.choiceDecisions) {
     const participation = document.createElement('p');
     participation.className = 'muted';
     const parts = [];
     if (lesson.turnDecisions) parts.push(`规划有效回答 ${lesson.turnDecisions.accepted}/${lesson.turnDecisions.attempts}`);
     if (lesson.choiceDecisions) parts.push(`选择有效回答 ${lesson.choiceDecisions.accepted}/${lesson.choiceDecisions.attempts}`);
     participation.textContent = `${parts.join(' · ')}；有效回答不代表整条建议都已执行。`;
     row.append(participation);
    }
    const coverage = document.createElement('details');
    const coverageTitle = document.createElement('summary');
    coverageTitle.textContent = '记录范围';
    const coverageNote = document.createElement('p');
    coverageNote.className = 'muted';
    const recording = lesson.recording;
    coverageNote.textContent = recording
     ? `从第 ${recording.fromTurn ?? '?'} 回合记录至第 ${recording.throughTurn ?? '?'} 回合。记录较多时保留开局与最近的片段，缺项不能视为没有发生。`
     : '这份旧记录没有提供覆盖范围，无法确定是否省略了动作。';
    coverage.append(coverageTitle, coverageNote);
    if (recording) {
     const labels = {plays:'出牌', handActions:'使用、弃牌与融合素材', abilityActions:'启动与融合', choices:'模式与发现', targetChoices:'目标与弃牌选择', comparisons:'模型决策对照', attackContexts:'攻击前局势', localTacticalBypasses:'本地斩杀省请求', tacticalAdvice:'攻击与进化第一步建议'};
     const list = document.createElement('ul');
     for (const [key, section] of Object.entries(recording.sections || {})) {
      if (!section.observed) continue;
      const item = document.createElement('li');
      item.textContent = `${labels[key] || key}：采集 ${section.observed}，保留 ${section.retained}，未保留 ${section.omitted}`;
      list.append(item);
     }
     const totals = recording.eventTotals || {};
     const missingAttacks = (totals.ownAttackEvents || 0) - (recording.sections?.attackContexts?.observed || 0);
     if (missingAttacks > 0) {
      const item = document.createElement('li');
      item.textContent = `另有 ${missingAttacks} 次攻击未采集到攻击前局势`;
      list.append(item);
     }
     if (totals.unretainedLeaderDamageEvents > 0) {
      const item = document.createElement('li');
      item.textContent = `对敌方主战者的伤害明细省略 ${totals.unretainedLeaderDamageEvents} 次、${totals.unretainedLeaderDamageAmount} 点；采集范围内合计 ${totals.leaderDamageAmount} 点`;
      list.append(item);
     }
     coverage.append(list);
    }
    row.append(coverage);
    if (lesson.actionComparisons?.length) {
     const details = document.createElement('details');
     const label = document.createElement('summary');
     const changed = lesson.actionComparisons.filter(item => item.changed);
     label.textContent = `提交动作与本地候选不同 ${changed.length} / ${lesson.actionComparisons.length} 次${lesson.recording?.sections?.comparisons?.omitted ? '（保留片段）' : ''}`;
     const note = document.createElement('p');
     note.className = 'muted';
     note.textContent = '在同一局面关闭本次模型偏好重新排序。只对照当前动作，没有模拟另一条路线的最终胜负。';
     const kinds = { PlayCard: '出牌', PerformAttack: '攻击', PerformEvolve: '进化', PerformSuperEvolve: '超进化', RequestAct: '启动', RequestFusion: '融合', EndTurn: '结束回合', PerformPpBoost: 'PP+1' };
     const describe = action => [kinds[action.kind] || action.kind,
      action.cardId ? data.cards?.[action.cardId] || '#' + action.cardId : '',
      action.abilityName || '', Number.isInteger(action.fieldIndex) && action.fieldIndex >= 0 ? `（当时场上第 ${action.fieldIndex + 1} 张）` : '', action.playMode ? `${playModeNames[action.playMode] || action.playMode} · 费用 ${action.paidCost ?? '?'} · 第 ${action.handIndex + 1} 张` : '', action.targetCardId ? '→' + (data.cards?.[action.targetCardId] || '#' + action.targetCardId) + (Number.isInteger(action.targetFieldIndex) && action.targetFieldIndex >= 0 ? `（当时敌方场上第 ${action.targetFieldIndex + 1} 张）` : '') : ''].filter(Boolean).join(' ');
     const list = document.createElement('ul');
     for (const item of changed) {
      const line = document.createElement('li');
      line.textContent = `第 ${item.turn} 回合 · 本地：${describe(item.local)}；实际：${describe(item.selected)}`;
      if (item.position) {
       const position = item.position;
       line.textContent += `；行动前 PP ${position.pp}/${position.maxPP}、EP ${position.ep}${position.canEvolve ? '' : '（当前不可用）'}、SEP ${position.sep}${position.canSuperEvolve ? '' : '（当前不可用）'}，敌方场面估算伤害 ${position.visibleEnemyDamage}`;
      }
      list.append(line);
     }
     details.append(label, note, list);
     row.append(details);
    }
    if (lesson.proposedTactics?.length) {
     const details = document.createElement('details');
     const label = document.createElement('summary');
     label.textContent = '模型的攻击与进化第一步建议';
     const note = document.createElement('p');
     note.className = 'muted';
     note.textContent = '建议已按当时的位置和原生规则核对。准备提交不代表已经执行，实际完成动作见对照记录。';
     const kinds = {PerformAttack:'攻击', PerformEvolve:'进化', PerformSuperEvolve:'超进化', EndTurn:'结束回合'};
     const statuses = {'accepted-for-current-position':'已绑定当时局面', rejected:'无效建议，沿用本地判断', 'selected-before-submit':'选为待提交动作', 'blocked-before-submit':'当前局面优先处理其他动作'};
     const reasons = {'candidate-not-in-current-frame':'候选已失效或编号无效', 'conflicting-first-actions':'同时指定了冲突的第一步', 'position-or-turn-changed':'局面或回合改变', 'card-rules-changed':'卡牌规则在规划期间发生变化', 'native-choice-no-longer-available':'动作当前已不可用', 'known-current-finish-has-priority':'已知获胜路线优先', 'visible-lethal-defense-has-priority':'当前致命威胁的防守优先'};
     const describe = ref => !ref ? '' : `${ref.side === 'enemy' ? '敌方' : '我方'}${ref.zone === 'leader' ? '主战者' : '第' + (ref.index + 1) + '张场上卡'}${ref.zone === 'leader' ? '' : '「' + (data.cards?.[ref.cardId] || '#' + ref.cardId) + '」'}`;
     const list = document.createElement('ul');
     for (const item of lesson.proposedTactics) {
      const line = document.createElement('li');
      const choice = item.choice;
      line.textContent = `第 ${item.turn} 回合 · ${choice ? [kinds[choice.kind] || choice.kind, describe(choice.source), choice.target ? '→' + describe(choice.target) : ''].filter(Boolean).join(' ') : '候选 ' + (item.index ?? '?')} · ${statuses[item.status] || item.status}${item.reason ? '：' + (reasons[item.reason] || item.reason) : ''}`;
      list.append(line);
     }
     details.append(label, note, list);
     row.append(details);
    }
    if (lesson.localTacticalBypasses?.length) {
     const details = document.createElement('details');
     const label = document.createElement('summary');
     label.textContent = '本地战术处理 · 已省略回合规划请求';
     const list = document.createElement('ul');
     for (const bypass of lesson.localTacticalBypasses) {
      const line = document.createElement('li');
      line.textContent = `第 ${bypass.turn} 回合 · 基础战斗搜索得到斩杀投影，按当前状态处理；这不是大模型建议，实际执行结果见动作记录。`;
      list.append(line);
     }
     details.append(label, list);
     row.append(details);
    }
    if (lesson.planFollows?.length) {
     const details = document.createElement('details');
     const label = document.createElement('summary');
     label.textContent = '路线续行与中止';
     const list = document.createElement('ul');
     for (const follow of lesson.planFollows) {
      const line = document.createElement('li');
      const status = {continued:'继续下一步', revalidated:'结算后已重新核对', finished:'出牌路线完成', interrupted:'中止原路线'}[follow.status] || follow.status;
      line.textContent = `第 ${follow.turn} 回合 · ${data.cards?.[follow.cardId] || '#' + follow.cardId} · ${playModeNames[follow.mode] || follow.mode} · ${status}${follow.reason ? '：' + follow.reason : ''}`;
      list.append(line);
     }
     details.append(label, list);
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
       + (item.constraintsChecked ? (item.conditionalBudgetSteps?.length ? '（含有条件返费，到账并重新核对后才接续）' : '（费用及已知弃牌约束已核对，未完整模拟效果）') : '（未核对路线约束）')
       + (item.handMaterials == null ? '' : item.handMaterials.complete ? ' · 已核对当前手牌素材' : ' · 未知变化后的素材需结算后重查');
      const modePlans = (item.line?.length ? item.line : [advice]).flatMap(step => (step.choices || []).map(choice =>
       `「${data.cards?.[step.cardId] || '#' + step.cardId}」第 ${choice.effectIndex + 1} 阶段、第 ${choice.executionIndex + 1} 次选模式 ${choice.optionIndex + 1}`));
      if (modePlans.length) {
       const note = document.createElement('p');
       note.className = 'muted';
       note.textContent = '预设分支（建议，非结算记录）：' + modePlans.join('；');
       line.append(note);
      }
      list.append(line);
     }
     details.append(label, list);
     row.append(details);
    }
    if (lesson.targetChoices?.length) {
     const details=document.createElement('details');const title=document.createElement('summary');title.textContent='目标与弃牌选择';const list=document.createElement('ul');
     const pick=p=>`${p.side==='own'?'我方':'敌方'}${p.zone==='hand'?'手牌':p.zone==='leader'?'主战者':'战场'}第${p.index+1}张「${data.cards?.[p.cardId]||'#'+p.cardId}」`;
     const status={selected:'采用模型目标建议',rejected:'目标建议失效，采用本地选择',blocked:'保留本地斩杀优先判断',none:'本地选择','other-key':'其他目标由本地选择'};
     for(const choice of lesson.targetChoices){const item=document.createElement('li');item.textContent=`第${choice.turn}回合 · ${data.cards?.[choice.sourceCardId]||'能力'} · ${status[choice.adviceStatus]||choice.adviceStatus} · 本地：${choice.local.map(pick).join('、')||'未记录位置'} → 提交：${choice.selected.map(pick).join('、')||'未记录位置'}`+(choice.reason?`（${choice.reason}）`:'');list.append(item);}
     const note=document.createElement('p');note.className='muted';note.textContent='这里记录提交的选择；实际效果是否成功，见已完成动作。';details.append(title,note,list);row.append(details);
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
      if (choice.comparison) {
       const comparison = choice.comparison;
       const local = choice.kind === 'mode'
        ? `模式 ${comparison.localIndexes.join('、')}`
        : comparison.localCardIds?.map(id => data.cards?.[id] || '#' + id).join('、');
       item.textContent += comparison.different ? ` · 本地候选 ${local}` : ' · 与本地候选一致';
      }
      if (choice.position) {
       const position = choice.position;
       const flow = position.decisionFlow;
       if (flow?.nativeAction?.kind === 'PlayCard') {
        const action = flow.nativeAction;
        item.textContent += ` · 当时出牌请求 ${playModeNames[action.playMode] || action.playMode || '未指定模式'}，申报费用 ${action.paidCostQuote ?? '未提供'}`;
       }
       if (flow) item.textContent += ` · ${flow.phase === 'preflight' ? '结算前预选' : '执行中选择'} · 效果 ${flow.effectIndex + 1}，第 ${flow.executionIndex + 1} 次（此前记录 ${flow.priorChoicesObserved} 次选择${flow.priorChoicesKnown ? '' : '，记录不完整'}）`;
       item.textContent += ` · 当时 PP ${position.pp}，手牌 ${position.handSize}，双方场面 ${position.ownFieldSize}/${position.enemyFieldSize}，生命 ${position.ownHealth ?? '未知'}/${position.enemyHealth ?? '未知'}，敌方可见攻击估算 ${position.visibleEnemyDamage}`;
      }
      choices.append(item);
     }
     row.append(choices);
    }
    if(lesson.experienceRetrievals?.length){
     const detail=document.createElement('details'),title=document.createElement('summary');
     title.textContent=`经验检索 · ${lesson.experienceRetrievals.length} 次请求准备`;
     const note=document.createElement('p');note.className='muted';note.textContent='相似案例供模型参考，不代表模型已采用，也不证明某个动作导致胜负。历史位置以当次请求为准。';
     const list=document.createElement('ul');
     for(const fact of lesson.experienceRetrievals){
      const line=document.createElement('li');
      const matches=(fact.matches||[]).map(item=>`历史第 ${item.historyIndexAtRequest+1} 局 T${item.evidenceTurn}：${[...(item.ownCardIds||[]),...(item.enemyCardIds||[]),...(item.focusCardIds||[])].filter((id,i,all)=>all.indexOf(id)===i).map(id=>data.cards?.[id]||'#'+id).join('、')}`).join('；');
      line.textContent=`T${fact.turn} ${fact.decision==='turn-plan'?'回合规划':fact.decision==='discover'?'发现':fact.decision==='decision'?'模式选择':'普通经验'} · 从 ${fact.available} 场选取 ${fact.selected} 场，规则一致 ${fact.compatibleSelected} 场，相关案例 ${fact.relevantSelected} 场${matches?' · '+matches:''}`;
      list.append(line);
     }
     detail.append(title,note,list);row.append(detail);
    }
    if (lesson.completedActions?.length) {
     const detail=document.createElement('details');const title=document.createElement('summary');
     title.textContent=`已完成动作前后对照 · ${lesson.completedActions.length} 条记录`;
     const note=document.createElement('p');note.className='muted';note.textContent='确认动作完成后的公开结果，包含触发响应；不代表未选路线的模拟结果。';
     const list=document.createElement('ul');
     for(const fact of lesson.completedActions){
      const a=fact.before,b=fact.after,line=document.createElement('li');
      line.textContent=`T${fact.turn} ${actionNames[fact.action.kind]||fact.action.kind}${fact.action.cardId?' · '+(data.cards?.[fact.action.cardId]||'#'+fact.action.cardId):''}：我方生命 ${a.ownHealth??'?'}→${b.ownHealth??'?'}，敌方生命 ${a.enemyHealth??'?'}→${b.enemyHealth??'?'}；PP ${a.pp}→${b.pp}，EP ${a.ep}→${b.ep}，SEP ${a.sep}→${b.sep}；手牌 ${a.ownHand}→${b.ownHand}，战场 ${a.ownField}/${a.enemyField}→${b.ownField}/${b.enemyField}`;
      const resourceDetail=document.createElement('p');resourceDetail.className='muted';
      const emblems=(ids,omitted)=>ids==null?'未知':(ids.length?ids.map(id=>data.cards?.[id]||'#'+id).join('、'):'无')+(omitted>0?`（另有 ${omitted} 张未展开）`:'');
      resourceDetail.textContent=`血池 ${a.bloodPool??'未知'}→${b.bloodPool??'未知'}；我方纹章：${emblems(a.ownEmblemIds,a.ownEmblemsOmitted)} → ${emblems(b.ownEmblemIds,b.ownEmblemsOmitted)}；敌方纹章：${emblems(a.enemyEmblemIds,a.enemyEmblemsOmitted)} → ${emblems(b.enemyEmblemIds,b.enemyEmblemsOmitted)}`;
      if (fact.decision) {
       const comparison = document.createElement('p');
       comparison.className = 'muted';
       const alternative = describeAction(fact.decision.local, data.cards);
       const policy = fact.decision.policy === 'model-first-action' ? '采纳模型第一步建议' : '使用计划参与排序';
       comparison.textContent = `${policy} · ${fact.decision.different ? '本地候选未执行' : '与本地候选一致'} · ${alternative}`;
       line.append(comparison);
      }
      line.append(resourceDetail);
      if(fact.ownHandChanges){
       const changes=fact.ownHandChanges.changes||[];
       if(changes.length){
        const hand=document.createElement('ul');
        const describe=card=>`${data.cards?.[card.cardId]||'#'+card.cardId} · ${card.cost}费${card.attack!=null?' · '+card.attack+'/'+card.health:''} · 当时第 ${card.index+1} 张`;
        for(const change of changes){
         const item=document.createElement('li');
         item.textContent=change.change==='arrived'?`进入手牌：${describe(change.after)}`:change.change==='departed'?`离开手牌：${describe(change.before)}`:`手牌状态变化：${describe(change.before)} → ${describe(change.after)}`;
         hand.append(item);
        }
        line.append(hand);
       }
      }
      if(fact.choiceTargets){
       const result=fact.choiceTargets,targets=document.createElement('ul');
       const keywords=c=>c.keywords==null?'未记录':(c.keywords.map(k=>getCachedEnumLabel('CardKeyword',k,k)).join('、')||'无')+(c.keywordsOmitted?`（另有 ${c.keywordsOmitted} 项未保留）`:'');
       const describe=c=>`${c.side==='own'?'我方':'敌方'}${c.zone==='hand'?'手牌':c.zone==='leader'?'主战者':'战场'}「${data.cards?.[c.cardId]||'#'+c.cardId}」${c.health!=null?' '+(c.attack!=null?c.attack+'/':'')+c.health:''} · 关键词 ${keywords(c)}`;
       for(const request of result.requests||[]){
        if(request.targetsSubmitted>(request.targets||[]).length){const omitted=document.createElement('li');omitted.textContent=`已提交 ${request.targetsSubmitted} 个目标，仅保留 ${(request.targets||[]).length} 个可观察目标`;targets.append(omitted);}
        for(const target of request.targets||[]){const item=document.createElement('li');
        const after=target.observation==='visible'&&target.after?describe(target.after):target.observation==='not-visible'?'已不在可观察区域（不推断离场原因）':'未取得结算后状态';
        item.textContent=`选择目标：${describe(target.before)} → ${after}`;targets.append(item);
        }
       }
       if(result.submissionsOmitted||result.observationFailures){const item=document.createElement('li');item.textContent=`目标观察：未保留 ${result.submissionsOmitted||0} 次提交，观察异常 ${result.observationFailures||0} 次`;targets.append(item);}
       if(targets.childNodes.length)line.append(targets);
      }
      list.append(line);
     }
     const scope=document.createElement('p');scope.className='muted';scope.textContent='手牌详情是动作前后对照，位置对应当时手牌；不推断获取或离开的原因，也不表示当前一定可用。旧记录未采集则不补填。';
     detail.append(title,note,list,scope);row.append(detail);
    }
    if (lesson.leaderHealthChanges?.length) {
     const detail=document.createElement('details');const title=document.createElement('summary');
     title.textContent=`主战者生命变化 · ${lesson.leaderHealthChanges.length} 条记录`;
     const note=document.createElement('p');note.className='muted';note.textContent='已提交的生命变化事件；保留开局与最近片段，不推断变化来源。';
     const list=document.createElement('ul');
     for(const fact of lesson.leaderHealthChanges){const line=document.createElement('li');line.textContent=`T${fact.turn} ${fact.targetSide==='own'?'我方':'敌方'}主战者 · ${fact.eventBefore} → ${fact.eventAfter}（${fact.change>0?'+':''}${fact.change}）`;list.append(line);}
     detail.append(title,note,list);row.append(detail);
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
   ? `已接入 ${data.strategyModel || '大模型'}。标准对照只有实验侧使用模型。历史经验对照的两组均使用模型。`
   : '尚未配置大模型。普通训练使用本地模型。';
  if (data.providerStatus?.available === false && data.strategyConfigured)
   strategySummary.textContent = data.providerStatus.message;
  retryStrategy.hidden = !data.strategyConfigured || data.providerStatus?.available !== false;
  retryStrategy.disabled = s.running;
  const completed = s.completed || 0;
  const experienceBenchmark = s.strategyExperienceBenchmark;
  const benchmark = experienceBenchmark ?? s.strategyBenchmark;
  const completeGroups = new Set(benchmark?.completeGroups ?? []);
  const scope = experienceBenchmark ? '对照使用开跑时冻结的经验' : '各牌组沿用开跑时的经验和攻略';
  const strategyProgress = `已完赛 ${completed} 局 · ${s.strategyGames || 0}/${completed} 局获得模型建议 · ${s.strategyPlans || 0} 次规划 · ${scope}`;
  if (experienceBenchmark) {
   const outcomes = experienceBenchmark.pairOutcomes;
   const results = outcomes
    ? ` · ${outcomes.improved} 对有经验组更好 · ${outcomes.regressed} 对更差 · ${outcomes.unchanged} 对结果相同 · ${outcomes.withAdviceInBoth} 对两边均有有效模型建议` : '';
   const summary = completeGroups.size
    ? `完整对照 ${completeGroups.size}/${benchmark.plannedGroups} 组${results}`
    : '尚无完整对照组 · 每组八局，每个牌组和先后手组合各跑有经验、无经验两局';
   const pending = benchmark.unpairedGames ? ` · ${benchmark.unpairedGames} 局未纳入配对` : '';
   const mismatches = (benchmark.openingMismatchGroups || 0) + (benchmark.postMulliganMismatchGroups || 0);
   const mismatch = mismatches ? ` · ${mismatches} 组开局或换牌后牌序不一致` : '';
   strategyResults.textContent = `${summary}${pending}${mismatch} · 起手种子 ${s.benchmarkSeed} · ${strategyProgress}`;
  } else if (s.compareStrategy && benchmark) {
   const paired = completeGroups.size
    ? `完整对照 ${completeGroups.size}/${benchmark.plannedGroups} 组 · 实验侧 ${benchmark.wins} 胜 ${benchmark.losses} 负 ${benchmark.draws} 平 · 分组 ${benchmark.groupsAhead} 优 ${benchmark.groupsBehind} 劣 ${benchmark.groupsTied} 平`
    : `尚无完整对照组 · 每组四局，交换牌组和先后手`;
   const unpaired = benchmark.unpairedGames ? ` · ${benchmark.unpairedGames} 局尚未配成整组` : '';
   const impact = completeGroups.size
    ? ` · ${benchmark.gamesWithDifferentActions} 局确认执行了不同于本地候选的动作，共 ${benchmark.differentCompletedActions} 次`
    : '';
   const optionImpact = completeGroups.size && benchmark.differentCommittedOptions != null
    ? ` · ${benchmark.gamesWithDifferentOptions} 局确认改选模式或发现，共 ${benchmark.differentCommittedOptions} 次`
    : '';
   const openingMismatch = benchmark.openingMismatchGroups
    ? ` · ${benchmark.openingMismatchGroups} 组起手牌序未通过校验`
    : '';
   strategyResults.textContent = `${paired}${unpaired}${openingMismatch}${impact}${optionImpact} · 起手种子 ${s.benchmarkSeed} · ${strategyProgress}`;
  } else {
   strategyResults.textContent = s.compareStrategy
    ? strategyProgress
    : '普通训练不调用大模型，继续按现有流程训练局面网络。';
  }
  const timing = (label, metrics) => {
   if (!metrics?.attempts) return '';
   const average = Math.round(metrics.totalMs / metrics.attempts);
   return `${label} ${metrics.accepted}/${metrics.attempts} 次采纳，平均 ${average} ms，最长 ${metrics.maxMs} ms`
    + `（超时 ${metrics.timedOut}，无效 ${metrics.invalid}，错误 ${metrics.errors}）`;
  };
  const usage = data.providerUsage;
  const measuredCache=usage?.cacheReportedPromptTokens;
  const cacheRatio=measuredCache>0?`${(100*(usage.cachedTokens||0)/measuredCache).toFixed(1)}%`:'暂无可比输入';
  cacheUsage.textContent=usage?.responses>0
   ? `本次服务启动以来 · API 已报告输入 ${usage.promptTokens||0} tokens · 已报告输出 ${usage.completionTokens||0} tokens · 缓存命中 ${usage.cachedTokens||0} / ${measuredCache??0} tokens（${cacheRatio}，仅统计返回完整缓存数据的响应）${usage.cacheUnknownResponses?` · ${usage.cacheUnknownResponses} 次响应未提供有效缓存数据，未按零命中计算`:''}${usage.promptUnreportedResponses?` · ${usage.promptUnreportedResponses} 次响应的输入用量未知`:''}`
   : '等待 API 返回用量统计';
  const groups = new Map();
  const comparisonSize = experienceBenchmark ? 8 : 4;
  const matched = (s.strategyMatches ?? []).filter(match =>
   !s.compareStrategy || completeGroups.has(Math.floor((match.episode - 1) / comparisonSize)));
  const armName = withHistory => withHistory ? '有经验组' : '无经验组';
  for (const match of matched) {
   const arm = experienceBenchmark ? armName(match.withStrategyExperience) : '';
   const key = [match.deckId, match.first, arm].join(':');
   const group = groups.get(key) ?? { name: match.deckName, arm, first: match.first, win: 0, loss: 0, draw: 0, total: 0, elapsed: 0 };
   group[match.result]++;
   group.total++;
   group.elapsed += match.elapsedMs;
   groups.set(key, group);
  }
  strategyMatchups.replaceChildren(...[...groups.values()]
   .sort((a, b) => a.name.localeCompare(b.name, 'zh-CN') || Number(b.first) - Number(a.first) || a.arm.localeCompare(b.arm, 'zh-CN'))
   .map(group => {
    const item = document.createElement('li');
    item.textContent = `${group.arm ? group.arm + ' · ' : ''}${group.name} · ${group.first ? '先手' : '后手'} · ${group.win} 胜 ${group.loss} 负 ${group.draw} 平 · 每局平均 ${(group.elapsed / group.total / 1000).toFixed(1)} 秒`;
    return item;
   }));
  strategyMatchups.hidden = groups.size === 0;
  const rootComparisons = experienceBenchmark?.rootComparisons ?? [];
  const actionDifferences = rootComparisons.filter(pair => pair.status === 'root-action-difference').length;
  rootDifferences.hidden = rootComparisons.length === 0;
  const choiceDifferences = rootComparisons.filter(pair => pair.status === 'subchoice-difference').length;
  rootTitle.textContent = `查看两组的首次分歧 · ${actionDifferences}/${rootComparisons.length} 对在相同记录局面采取不同动作 · ${choiceDifferences} 对在结算中改选`;
  const rootStatuses = {
   'position-difference':'下一步前的局面记录已不同',
   'same-recorded-roots':'已记录的根动作一致',
   'trace-limit':'追踪达到上限',
   'different-length':'动作记录长度不同',
   'unobserved':'缺少可比较的动作记录',
   'choice-unobserved':'缺少引擎接收的选择或对应选项记录',
   'choice-context-difference':'结算中出现不同的局面或选项',
   'choice-length-difference':'结算中的选择次数不同',
   'choice-limit':'结算中可比较的选择已达上限'
  };
  rootList.replaceChildren(...rootComparisons.slice(-16).map(pair => {
   const item = document.createElement('li');
   const prefix = `第 ${pair.historyEpisode} / ${pair.controlEpisode} 局 · 前 ${pair.matchedRoots} 步一致`;
   const history = pair.history;
   item.textContent = pair.status === 'root-action-difference' && history && pair.control
    ? `${prefix} · T${history.turn} ${history.seat==='model'?'模型席':'对手席'} · 有经验组 ${describeRootMove(history, data.strategyExampleCards)} · 无经验组 ${describeRootMove(pair.control, data.strategyExampleCards)}`
    : pair.status === 'subchoice-difference' && history && pair.historyChoice && pair.controlChoice
     ? `${prefix} · T${history.turn} ${describeRootMove(history, data.strategyExampleCards)} · 第 ${pair.choiceIndex+1} 次选择 · 有经验组 ${describeSubchoice(pair.historyChoice, data.strategyExampleCards)} · 无经验组 ${describeSubchoice(pair.controlChoice, data.strategyExampleCards)}`
     : `${prefix} · ${rootStatuses[pair.status] || '未取得分歧动作'}`;
   return item;
  }));
  const examples = experienceBenchmark ? matched.sort((a,b) => a.episode - b.episode)
   .flatMap(match => (match.experienceOutcomeExamples ?? []).filter(fact => fact.decision?.different)
    .map(fact => ({match, fact}))).slice(-16) : [];
  experienceExamples.hidden = examples.length === 0;
  exampleList.replaceChildren(...examples.map(({match, fact}) => {
   const item = document.createElement('li');
   const actual = describeAction(fact.action, data.strategyExampleCards);
   const alternative = describeAction(fact.decision.local, data.strategyExampleCards);
   const {before, after} = fact;
   item.textContent = `${armName(match.withStrategyExperience)} · 第 ${match.episode} 局 · T${fact.turn} · 执行 ${actual} · 本地候选 ${alternative}`
    + ` · PP ${before.pp} → ${after.pp} · 手牌 ${before.ownHand} → ${after.ownHand} · 敌方生命 ${before.enemyHealth ?? '未知'} → ${after.enemyHealth ?? '未知'}`;
   return item;
  }));
  const armMetrics = (label, arm) => `${label} · 有效回合规划 ${arm.acceptedPlans}/${arm.planningAttempts}，有效模式与发现 ${arm.acceptedChoices}/${arm.choiceAttempts}，实际改动 ${arm.differentCompletedActions} 次动作、${arm.differentCommittedOptions} 次模式或发现`;
  strategyTiming.textContent = experienceBenchmark
   ? completeGroups.size ? [armMetrics('有经验组', experienceBenchmark.withHistory),
    armMetrics('无经验组', experienceBenchmark.withoutHistory)].join(' / ') : ''
   : s.compareStrategy ? [timing('回合规划', s.turnDecisions), timing('模式与发现', s.optionDecisions)].filter(Boolean).join(' · ') : '';
  const latest = s.latestStrategy;
  const retrieved=latest?.experienceRetrieval;
  experienceText.textContent=retrieved?`经验检索：${retrieved.available} 场中选取 ${retrieved.selected} 场，规则一致 ${retrieved.compatibleSelected} 场，相关案例 ${retrieved.relevantSelected} 场（请求准备，不代表已采纳）`:'';
  const roles = latest?.guide?.cards || [];
  guideText.textContent = latest?.guide ? `牌组打法（参考 ${latest.priorGames || 0} 场历史对局）：${latest.guide.gamePlan}` : '';
  if(latest?.guide?.ruleCorrections?.length)guideText.textContent+=`\n${latest.guide.ruleCorrections.join(' ' )}`;
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
  supportsExperience=Boolean(data.strategyExperienceBenchmarkSupported);
  supportsCombatProjection=Boolean(data.combatProjectionBenchmarkSupported);
  projectionLabel.hidden=!supportsCombatProjection;
  if(s.running) {
   compareStrategy.checked=Boolean(s.compareStrategy);
   compareExperience.checked=Boolean(s.compareStrategyExperience);
   strategyChoicesOnly.checked=Boolean(s.strategyChoicesOnly);
   compareCombatProjection.checked=Boolean(s.compareCombatProjection);
   if(s.compareStrategy) benchmarkSeed.value=String(s.benchmarkSeed);
  }
  if(!supportsCombatProjection) compareCombatProjection.checked=false;
  if(!supportsExperience) compareExperience.checked=false;
  updateRunOptions();
  choicesLabel.hidden ||= !data.strategyConfigured;
  experienceLabel.hidden ||= !data.strategyConfigured;
  compareStrategy.disabled=s.running;
  compareExperience.disabled=s.running;
  strategyChoicesOnly.disabled=s.running;
  compareCombatProjection.disabled=s.running;
 };
 const winrates=section('卡组胜率');const winGrid=document.createElement('div');winGrid.className='ai-training-deck-grid';const winTiles=[];
 for(let i=0;i<2;i++){const tile=document.createElement('div');tile.className='ai-training-deck-stat';const name=document.createElement('strong');const percent=document.createElement('span');const details=document.createElement('small');tile.append(name,percent,details);winGrid.append(tile);winTiles.push({name,percent,details});}
 const winNote=document.createElement('p');winNote.className='muted';winNote.textContent='本次训练任务累计的卡组自对战胜率，双方交替换边；失败或中断对局不计入。不是模型的独立评测胜率。';winrates.append(winGrid,winNote);
 modal.body.append(version,architecture,live,winrates,controls,strategyPanel,validation);let busy=false,timer,hasDecks=false;
 const refresh=async()=>{if(!modal.overlay.isConnected)return;try{const data=await request('status');version.textContent=`当前对手：${data.modelVersion||'尚未载入'} · 每2秒刷新`;architectureText.textContent=`已载入 ${data.models} 个网络 · ${data.specialists} 组牌组专项 · 本地训练继续更新局面评估网络。`;note.textContent=data.modelPromotionGate?'按完整对局隔离训练与验证。误差改善后保存候选，再进行24局免费的新旧模型对照，通过后才替换当前模型。':'当前服务仅进行预测误差验证，暂未启用实战对照。';const s=data.training;showStrategy(data,s);showLocalValidation(s.modelValidations);const evaluation=s.modelEvaluation;modelEvaluation.textContent=s.validatingModel?'正在对战旧模型，验证候选的实际表现。':evaluation?`实战对照：${evaluation.wins}胜 ${evaluation.losses}负 ${evaluation.draws}平 · 完成 ${evaluation.games}/24局${evaluation.failed?` · 失败 ${evaluation.failed}局`:''} · ${evaluation.message}${s.promoted?' · 当前模型已更新':''}`:'';if(s.running){if(!s.validatingModel){continuous.checked=s.continuous;parallel.value=String(s.parallel);games.value=String(s.planned);}if(s.playerDeckId)fields[0].value=s.playerDeckId;if(s.opponentDeckId)fields[1].value=s.opponentDeckId;}winNote.textContent = s.compareStrategyExperience
 ? '两组分别计算胜率。仅纳入开局和换牌后牌序均一致的配对，后续行动变化会影响局势和随机效果。'
 : s.compareStrategy
 ? '胜率按开局牌序一致的完整四局组统计。每组交换牌组与先后手，换牌和后续效果仍有随机性。'
 : s.benchmarkSeed != null
  ? '双方按起手分组交换牌组与先后手；胜率只反映本组对照，失败或中断对局不计入。'
  : '本次训练任务累计的卡组自对战胜率，双方交替换边；失败或中断对局不计入。不是模型的独立评测胜率。';
const winStats = s.compareStrategy ? s.strategyBenchmark : s;
for (let i = 0; i < 2; i++) {
 const experience = s.strategyExperienceBenchmark;
 const arm = i === 0 ? experience?.withHistory : experience?.withoutHistory;
 const total = arm?.games ?? (s.compareStrategy ? (winStats?.completeGroups.length ?? 0) * 4 : s.totalCompleted || 0);
 const draws = arm?.draws ?? winStats?.draws ?? 0;
 const wins = arm?.wins ?? (i === 0 ? winStats?.wins : winStats?.losses);
 const losses = arm?.losses ?? (i === 0 ? winStats?.losses : winStats?.wins);
 winTiles[i].name.textContent=s.validatingModel?(i===0?'候选模型':'旧模型'):s.compareCombatProjection?(i===0?'计入攻击前效果':'原基础预测'):s.compareSummonSources?(i===0?'识别召唤斩杀出牌':'原出牌判断'):s.compareGeneratedTokens?(i===0?'本地生成疾驰路线':'原本地策略'):s.compareValueSearch
 ? (i===0?'模型参与搜索':'原搜索对照')
 :s.compareStrategyExperience
 ? (i===0?'有经验组':'无经验组')
 :s.compareStrategy
 ? (i===0?'大模型实验侧':'本地模型对照侧')
 : (i===0?'训练卡组':'对手卡组')+' · '+((i===0?s.playerDeckName:s.opponentDeckName)||'尚未选择');winTiles[i].percent.textContent=total?((wins||0)/total*100).toFixed(1)+'%':'--';winTiles[i].details.textContent=`${wins||0}胜 ${losses||0}负 ${draws}平 · ${total}局`;}
progress.max=Math.max(1,s.planned);progress.value=s.completed+s.failed;metricValues[0].textContent=String(s.completed);metricValues[1].textContent=String(s.failed);metricValues[2].textContent=String(s.samples);live.classList.toggle('ai-current-evaluation',s.running);status.textContent=`${s.message} · 第${s.round||1}轮${s.continuous?" · 自动连续":""} · 完成 ${s.completed}/${s.planned} · ${s.parallel||1}路并行 · 当前同时训练 ${s.active||0} 局`;const coverage=s.trainingPositions;const sampling=coverage?` · 已观察回合开始 ${coverage.observedTurnStarts||0} 次、结算后 ${coverage.observedAfterActions||0} 次；入模 ${coverage.samplesTurnStarts||0}/${coverage.samplesAfterActions||0} 个` : '';report.textContent=s.compareCombatProjection?`已通过真实结算确认 ${s.combatProjectionCommittedChanges||0} 次不同首步。${s.combatProjectionCommittedChanges?'胜负仅反映本组固定卡组对照。':'尚无确认的不同首步，胜负不能归因于该功能。'}对照不调用大模型、不训练或替换权重。`:s.compareSummonSources?`新增出牌判断实际触发 ${s.generatedSummonTacticUses||0} 次；${s.generatedSummonTacticUses?'胜率为本组对照结果。':'尚未触发时，本组胜负不能归因于该功能。'}对照期间不训练或替换权重。`:s.compareGeneratedTokens?`实际执行生成疾驰路线 ${s.generatedTokenTacticUses||0} 次；交换牌组与先后手，对照期间不训练或替换权重。`:s.compareValueSearch?`本地模型局面评估 ${s.valueSearchEvaluations||0} 次；交换牌组与先后手进行搜索对照。本次不训练、不替换权重。`:s.compareStrategy?'对照评测记录规划和胜负，模型权重由普通训练更新。':s.before==null?'等待收集足够的完整对局，开始局面模型学习。':`验证误差：${s.before.toFixed(4)} → ${s.after.toFixed(4)}${s.promoted?' · 已替换当前模型':s.candidateVersion?' · 已保存候选':''}`;report.textContent+=sampling+(s.sampleReplaySaved?' · 本轮训练数据已保留，可重复检验':'');start.disabled=busy||s.running||!hasDecks;fields.forEach(field=>field.disabled=s.running);games.disabled=s.running;parallel.disabled=s.running;benchmarkSeed.disabled=s.running;continuous.disabled=s.running||compareStrategy.checked||compareCombatProjection.checked;stop.disabled=!s.running;download.disabled=s.running||!data.modelVersion;}catch(e){status.textContent=e.message;}if(modal.overlay.isConnected)timer=setTimeout(refresh,2000);};
 start.onclick=async()=>{
  if(compareCombatProjection.checked&&!supportsCombatProjection){await showAlert('当前后端尚未支持此对照，请先更新本地服务。');return;}
  const size=groupSize();
  const minimumEvaluationGames = compareStrategy.checked ? size : 12;
  if ((compareStrategy.checked||compareCombatProjection.checked) && (Number(games.value) < minimumEvaluationGames || Number(games.value) % size !== 0 || Number(parallel.value) > 2)) {
   await showAlert(`对照评测至少 ${minimumEvaluationGames} 局，局数须为 ${size} 的倍数，并行最多 2 路。`);
   return;
  }
  if (compareStrategy.checked && !benchmarkSeed.checkValidity()) {
   await showAlert('请输入有效的起手种子，范围为 −2147483648 至 2147483647。');
   return;
  }
  busy=true;start.disabled=true;try{await request('start',{method:'POST',body:JSON.stringify({playerDeckId:fields[0].value,opponentDeckId:fields[1].value,games:Number(games.value),parallel:Number(parallel.value),continuous:continuous.checked,compareStrategy:compareStrategy.checked,compareStrategyExperience:compareStrategy.checked&&compareExperience.checked,benchmarkSeed:compareStrategy.checked?Number(benchmarkSeed.value):18431,compareCombatProjection:compareCombatProjection.checked,strategyChoicesOnly:compareStrategy.checked&&strategyChoicesOnly.checked})});}catch(e){await showAlert(e.message);}finally{busy=false;clearTimeout(timer);void refresh();}};
 stop.onclick=async()=>{try{await request('stop',{method:'POST'});clearTimeout(timer);void refresh();}catch(e){await showAlert(e.message);}};
 download.onclick=async()=>{download.disabled=true;try{const blob=await request('export');const url=URL.createObjectURL(blob);const link=document.createElement('a');link.href=url;link.download='svnebula-ai-model.json';link.click();setTimeout(()=>URL.revokeObjectURL(url),30000);}catch(e){await showAlert(e.message);}finally{download.disabled=false;}};
 try{const response=await fetch('/api/decks?userId='+encodeURIComponent(getUserSession().userId));if(!response.ok)throw Error('卡组载入失败');const decks=await response.json();for(const select of fields)for(const deck of decks){const option=document.createElement('option');option.value=deck.deckId;option.textContent=deck.name;select.append(option);}hasDecks=decks.length>0;if(!hasDecks)message.textContent='请先在牌组编辑中创建训练卡组。';void refresh();}catch(e){status.textContent=e.message;}
}
export async function installLocalAiTraining() {
 document.getElementById('local-ai-training-entry')?.remove();if(!local()||!getUserSession().userId)return;
 try{if(!(await request('capabilities')).enabled)return;const grid=document.querySelector('.dev-tools-grid');if(!grid)return;const card=document.createElement('section');card.id='local-ai-training-entry';card.className='tool-card';const title=document.createElement('h3');title.textContent='AI 训练';const text=document.createElement('p');text.textContent='仅本地开放 · 使用现有卡组继续训练与导出模型';const button=document.createElement('button');button.className='tool-button';button.textContent='打开 AI 训练';button.onclick=()=>openTraining().catch(e=>showAlert(e.message));card.append(title,text,button);grid.append(card);}catch{}
}
document.addEventListener('svb:user-session-changed',installLocalAiTraining);
