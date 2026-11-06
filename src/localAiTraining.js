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
 const controls=section('本地训练设置');controls.append(message,row,continuousLabel,buttons);
 const winrates=section('卡组胜率');const winGrid=document.createElement('div');winGrid.className='ai-training-deck-grid';const winTiles=[];
 for(let i=0;i<2;i++){const tile=document.createElement('div');tile.className='ai-training-deck-stat';const name=document.createElement('strong');const percent=document.createElement('span');const details=document.createElement('small');tile.append(name,percent,details);winGrid.append(tile);winTiles.push({name,percent,details});}
 const winNote=document.createElement('p');winNote.className='muted';winNote.textContent='本次训练任务累计的卡组自对战胜率，双方交替换边；失败或中断对局不计入。不是模型的独立评测胜率。';winrates.append(winGrid,winNote);
 modal.body.append(version,architecture,live,winrates,controls,validation);let busy=false,timer,hasDecks=false;
 const refresh=async()=>{if(!modal.overlay.isConnected)return;try{const data=await request('status');version.textContent=`当前对手：${data.modelVersion||'尚未载入'} · 每2秒刷新`;architectureText.textContent=`已载入 ${data.models} 个网络 · ${data.specialists} 组牌组专项 · 本地训练继续更新局面评估网络。`;const s=data.training;if(s.running){continuous.checked=s.continuous;parallel.value=String(s.parallel);games.value=String(s.planned);if(s.playerDeckId)fields[0].value=s.playerDeckId;if(s.opponentDeckId)fields[1].value=s.opponentDeckId;}const total=s.totalCompleted||0;for(let i=0;i<2;i++){const wins=i===0?s.wins:s.losses;const losses=i===0?s.losses:s.wins;winTiles[i].name.textContent=(i===0?'训练卡组':'对手卡组')+' · '+((i===0?s.playerDeckName:s.opponentDeckName)||'尚未选择');winTiles[i].percent.textContent=total?((wins||0)/total*100).toFixed(1)+'%':'--';winTiles[i].details.textContent=`${wins||0}胜 ${losses||0}负 ${s.draws||0}平 · ${total}局`;}
progress.max=Math.max(1,s.planned);progress.value=s.completed+s.failed;metricValues[0].textContent=String(s.completed);metricValues[1].textContent=String(s.failed);metricValues[2].textContent=String(s.samples);live.classList.toggle('ai-current-evaluation',s.running);status.textContent=`${s.message} · 第${s.round||1}轮${s.continuous?" · 自动连续":""} · 完成 ${s.completed}/${s.planned} · ${s.parallel||1}路并行 · 当前同时训练 ${s.active||0} 局`;report.textContent=s.before==null?'等待收集足够的完整对局，开始局面模型学习。':`验证误差：${s.before.toFixed(4)} → ${s.after.toFixed(4)}${s.promoted?' · 已保存新模型':''}`;start.disabled=busy||s.running||!hasDecks;fields.forEach(field=>field.disabled=s.running);games.disabled=s.running;parallel.disabled=s.running;continuous.disabled=s.running;stop.disabled=!s.running;download.disabled=s.running||!data.modelVersion;}catch(e){status.textContent=e.message;}if(modal.overlay.isConnected)timer=setTimeout(refresh,2000);};
 start.onclick=async()=>{busy=true;start.disabled=true;try{await request('start',{method:'POST',body:JSON.stringify({playerDeckId:fields[0].value,opponentDeckId:fields[1].value,games:Number(games.value),parallel:Number(parallel.value),continuous:continuous.checked})});}catch(e){await showAlert(e.message);}finally{busy=false;clearTimeout(timer);void refresh();}};
 stop.onclick=async()=>{try{await request('stop',{method:'POST'});clearTimeout(timer);void refresh();}catch(e){await showAlert(e.message);}};
 download.onclick=async()=>{download.disabled=true;try{const blob=await request('export');const url=URL.createObjectURL(blob);const link=document.createElement('a');link.href=url;link.download='svnebula-ai-model.json';link.click();setTimeout(()=>URL.revokeObjectURL(url),30000);}catch(e){await showAlert(e.message);}finally{download.disabled=false;}};
 try{const response=await fetch('/api/decks?userId='+encodeURIComponent(getUserSession().userId));if(!response.ok)throw Error('卡组载入失败');const decks=await response.json();for(const select of fields)for(const deck of decks){const option=document.createElement('option');option.value=deck.deckId;option.textContent=deck.name;select.append(option);}hasDecks=decks.length>0;if(!hasDecks)message.textContent='请先在牌组编辑中创建训练卡组。';void refresh();}catch(e){status.textContent=e.message;}
}
export async function installLocalAiTraining() {
 document.getElementById('local-ai-training-entry')?.remove();if(!local()||!getUserSession().userId)return;
 try{if(!(await request('capabilities')).enabled)return;const grid=document.querySelector('.dev-tools-grid');if(!grid)return;const card=document.createElement('section');card.id='local-ai-training-entry';card.className='tool-card';const title=document.createElement('h3');title.textContent='AI 训练';const text=document.createElement('p');text.textContent='仅本地开放 · 使用现有卡组继续训练与导出模型';const button=document.createElement('button');button.className='tool-button';button.textContent='打开 AI 训练';button.onclick=()=>openTraining().catch(e=>showAlert(e.message));card.append(title,text,button);grid.append(card);}catch{}
}
document.addEventListener('svb:user-session-changed',installLocalAiTraining);
