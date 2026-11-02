// Mini's additive grouping rule, adapted to the native server's visible buff DTOs.
// Different expiry/selection conditions never share a total. Set/base/multiply remain separate.
export function summarizeAttachedNumericEffects(summaries) {
    const groups = [];
    for (const summary of summaries || []) {
        for (const entry of summary.NumericEffects ?? summary.numericEffects ?? []) {
            const field = entry.Field ?? entry.field;
            const layer = entry.Layer ?? entry.layer;
            const policy = entry.PolicyKey ?? entry.policyKey;
            const value = Number(entry.Value ?? entry.value);
            if (!field || !Number.isFinite(value)) continue;
            const key = layer === 'Additive' ? JSON.stringify([field,policy,!!(entry.RemoveAfterUse ?? entry.removeAfterUse)]) : `separate:${groups.length}`;
            const source = {id:summary.SourceCardDefinitionId ?? summary.sourceCardDefinitionId,name:summary.SourceCardName ?? summary.sourceCardName,abilityName:summary.SourceAbilityName ?? summary.sourceAbilityName,art:summary.SourceCardArtPreview ?? summary.sourceCardArtPreview};
            const previous = groups.find(group => group.key === key);
            if (previous) { previous.value += value; previous.sources.push(source); previous.count++; }
            else groups.push({key,field,layer,value,policy,oneUse:!!(entry.RemoveAfterUse ?? entry.removeAfterUse),duration:entry.Duration ?? entry.duration,sources:[source],count:1});
        }
    }
    return groups.filter(group => group.layer !== 'Additive' || group.value !== 0).map(group => ({...group,sources:group.sources.filter((source,index,all)=>all.findIndex(other=>other.name===source.name&&other.abilityName===source.abilityName&&other.art===source.art)===index)}));
}
export function numericEffectTitle(effect) {
    const labels = {Attack:'攻击',Health:'生命',Defense:'生命',Cost:'费用',Act:'启动费用',Reanimate:'亡召上限',Enhance:'爆能强化费用',Accelerate:'激奏费用',Crystalize:'结晶费用'};
    const operator = effect.layer === 'Multiplicative' ? '×' : effect.layer === 'Set' ? '设为 ' : effect.layer === 'Base' ? '基础值 ' : effect.value >= 0 ? '+' : '';
    const durations = {EndOfTurn:'回合结束时',StartOfTurn:'下回合开始时',OwnerTurnStart:'我方回合开始时',OwnerTurnEnd:'我方回合结束时',OpponentTurnStart:'敌方回合开始时',OpponentTurnEnd:'敌方回合结束时',ConditionalRemoval:'满足条件时移除'};
    return `${effect.oneUse ? '下一次' : ''}${labels[effect.field] || effect.field} ${operator}${effect.value}${durations[effect.duration] ? ` · ${durations[effect.duration]}` : ''}`;
}

const escape = value => String(value ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
export function effectSourceHtml(source) {
 const art = source.art && /^(\/|https?:\/\/)/i.test(source.art) ? `<img class="buff-summary-source-art" src="${escape(source.art)}" alt="" loading="lazy">` : '';
 const name = source.name || '附加效果';
 const label = name + (source.abilityName && source.abilityName !== name ? `【${source.abilityName}】` : '');
 const id = Number(source.id);
 return Number.isInteger(id) && id > 0
  ? `<button type="button" class="battle-effect-source battle-effect-source-button" data-card-id="${id}" aria-label="查看来源卡牌：${escape(name)}">${art}<span>${escape(label)}</span></button>`
  : `<span class="battle-effect-source">${art}<span>${escape(label)}</span></span>`;
}
export function renderAttachedNumericEffects(groups) {
 const blocks=[];
 for (const group of groups) {
  const previous=group.layer==='Additive' ? blocks.find(block=>block.policy===group.policy&&block.oneUse===group.oneUse&&block.duration===group.duration&&block.layer==='Additive') : null;
  if(previous){previous.effects.push(group);previous.sources.push(...group.sources);}
  else blocks.push({...group,effects:[group],sources:[...group.sources]});
 }
 return blocks.map(block=>{
  const sources=block.sources.filter((source,index,all)=>all.findIndex(other=>other.id===source.id&&other.name===source.name&&other.abilityName===source.abilityName)===index);
  return `<section class="battle-attached-numeric"><div class="battle-attached-values">${block.effects.map(effect=>`<b>${escape(numericEffectTitle(effect))}</b>`).join('')}</div>${sources.map(effectSourceHtml).join('')}</section>`;
 }).join('');
}

const keywordAliases = {Storm:'疾驰',Rush:'突进',Ward:'守护',Bane:'必杀',Ambush:'潜行',Drain:'吸血',Shield:'屏障',Aura:'灵气'};
export function isRepeatedKeywordDetail(detail, currentKeywords = []) {
 const match = String(detail ?? '').trim().match(/^获得\s+([^\s·（(]+)/);
 if (!match) return false;
 const canonical = value => {
  const text=String(value ?? '').trim();
  const known=Object.keys(keywordAliases).find(key=>key.toLowerCase()===text.toLowerCase());
  return known ? keywordAliases[known] : text.toLowerCase();
 };
 return currentKeywords.some(keyword => canonical(keyword) === canonical(match[1]));
}
