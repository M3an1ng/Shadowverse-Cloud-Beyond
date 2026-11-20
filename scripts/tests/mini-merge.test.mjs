import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import { parseHTML } from 'linkedom';
import { parseAst } from 'rollup/parseAst';
import { createServer } from 'vite';

globalThis.document = parseHTML('<!doctype html><html><body></body></html>').document;
// Use the same asset and import.meta.glob transforms as the game.
const vite = await createServer({
 configFile: false,
 appType: 'custom',
 server: { middlewareMode: true, hmr: false, watch: null },
 optimizeDeps: { noDiscovery: true },
 logLevel: 'silent',
});
async function load(entry) {
 try {
  return await vite.ssrLoadModule('/src/' + entry + '.js');
 } catch (error) {
  await vite.close();
  throw error;
 }
}
// Finish loading before registering tests so an import failure fails the entire file.
const settings = await load('operationSettings');
const choice = await load('choiceSettingsPolicy');
const dialog = await load('siteDialog');
const manager = await load('gameManager');
const alternateConfig = await load('alternatePlayConfig');
const liveEffects = await load('liveEffectSummary');
const dslOptions = await load('components/dsl_editor/options-provider');
const modeVisual = await load('playModeVisual');
const references = await load('previewDescriptionReferences');
const reveals = await load('revealAnimation');
const catalog = await load('cardCatalogQuery');
const draftDeck = await load('draftDeckModel');
const roomPresentation = await load('multiplayerRoomPresentation');
const revealGateModule = await load('revealPresentationGate');
const stylePresentation = await load('cardStylePresentation');
test.after(() => vite.close());
const storage={value:null,getItem(){return this.value;},setItem(key,value){this.value=value;}};
test('operation settings default to showing card buttons',()=>assert.deepEqual(settings.readOperationSettings(storage),{autoConfirmTargets:true,evolutionConfirmation:true,autoConfirmModes:true,endTurnConfirmation:true,fusionConfirmation:false,hideCardButtons:false}));
test('settings persist explicit false values',()=>{settings.saveOperationSettings({autoConfirmModes:false,evolutionConfirmation:false},storage);assert.equal(settings.readOperationSettings(storage).autoConfirmModes,false);assert.equal(settings.readOperationSettings(storage).evolutionConfirmation,false);});
test('invalid stored switches fall back individually',()=>{storage.value=JSON.stringify({autoConfirmTargets:'false',fusionConfirmation:true});assert.equal(settings.readOperationSettings(storage).autoConfirmTargets,true);assert.equal(settings.readOperationSettings(storage).fusionConfirmation,true);});
test('corrupt or disabled storage remains usable',()=>{storage.value='{';assert.deepEqual(settings.readOperationSettings(storage),settings.defaultOperationSettings);assert.doesNotThrow(()=>settings.saveOperationSettings({}, {setItem(){throw Error('disabled');}}));});
test('mode confirmation respects independent mode preference',()=>assert.equal(choice.shouldAutoConfirmChoice({TargetKey:'decision',MinSelectionCount:1,MaxSelectionCount:1},1,{autoConfirmModes:false,autoConfirmTargets:true}),false));
test('targets respect independent target preference',()=>assert.equal(choice.shouldAutoConfirmChoice({TargetKey:'one',MinSelectionCount:1,MaxSelectionCount:1},1,{autoConfirmModes:false,autoConfirmTargets:true}),true));
test('variable ranges and incomplete selections require confirmation',()=>{assert.equal(choice.shouldAutoConfirmChoice({MinSelectionCount:0,MaxSelectionCount:2},2,settings.defaultOperationSettings),false);assert.equal(choice.shouldAutoConfirmChoice({MinSelectionCount:2,MaxSelectionCount:2},1,settings.defaultOperationSettings),false);});

const tick=()=>new Promise(resolve=>setImmediate(resolve));

function currentDialog() {
 const panels = document.querySelectorAll('[role="dialog"]');
 assert.equal(panels.length, 1);
 return panels[0];
}

function dialogMessage(panel) {
 const message = document.getElementById(panel.getAttribute('aria-describedby'));
 assert.ok(message && panel.contains(message));
 return message;
}

test('confirmation waits for explicit website choice', async () => {
 let done = false;
 const result = dialog.confirmDialog('测试').then(value => { done = true; return value; });
 await tick();
 assert.equal(done, false);
 currentDialog().querySelector('.site-dialog-primary').click();
 assert.equal(await result, true);
 assert.equal(document.body.childElementCount, 0);
});
test('cancel has no destructive side effects', async () => {
 const result = dialog.confirmDialog('删除');
 await tick();
 const cancel = currentDialog().querySelector('button');
 assert.equal(cancel.textContent, '取消');
 cancel.click();
 assert.equal(await result, false);
});
test('modal text is rendered as text, not HTML', async () => {
 const text = '<strong>操作提示</strong>';
 const result = dialog.showAlert(text);
 await tick();
 const panel = currentDialog();
 const message = dialogMessage(panel);
 assert.equal(message.textContent, text);
 assert.equal(message.childElementCount, 0);
 panel.querySelector('button').click();
 await result;
});
test('simultaneous dialogs queue without overlapping', async () => {
 const first = dialog.showAlert('第一条提示');
 const second = dialog.showAlert('第二条提示');
 await tick();
 const firstPanel = currentDialog();
 assert.equal(dialogMessage(firstPanel).textContent, '第一条提示');
 firstPanel.querySelector('button').click();
 await first;
 await tick();
 const secondPanel = currentDialog();
 assert.equal(dialogMessage(secondPanel).textContent, '第二条提示');
 secondPanel.querySelector('button').click();
 await second;
 assert.equal(document.body.childElementCount, 0);
});
test('prompt returns typed text or cancellation', async () => {
 const result = dialog.promptDialog('名称', 'default');
 await tick();
 const panel = currentDialog();
 panel.querySelector('input').value = '新卡池';
 panel.querySelector('.site-dialog-primary').click();
 assert.equal(await result, '新卡池');
});

test('frontend has no remaining browser native dialogs and all modules parse',async()=>{
 async function walk(dir){for(const entry of await fs.readdir(dir,{withFileTypes:true})){const filename=path.join(dir,entry.name);if(entry.isDirectory())await walk(filename);else if(filename.endsWith('.js')){const source=await fs.readFile(filename,'utf8');const ast=parseAst(source);function visit(n){if(!n||typeof n!=='object')return;if(n.type==='CallExpression'){const c=n.callee;assert.ok(!(c?.type==='Identifier'&&['alert','confirm','prompt'].includes(c.name)),filename);assert.ok(!(c?.object?.name==='window'&&['alert','confirm','prompt'].includes(c?.property?.name)),filename);}for(const value of Object.values(n))if(Array.isArray(value))value.forEach(visit);else if(value&&typeof value==='object')visit(value);}visit(ast);}}}await walk('src');
});

test('server transform identity replaces field card, selection and preview',()=>{
 const previous='11111111-1111-1111-1111-111111111111';const next='22222222-2222-2222-2222-222222222222';
 const old={uid:previous,id:2,zone:'Field',ownerId:'owner',currentHealth:1};
 manager.gameState.players=[{id:'owner',hand:[],field:[old],emblem:[],graveyard:[],deck:[],leaderCard:null}];manager.gameState.selectedCardUid=previous;manager.gameState.selectedAttackerUid=previous;manager.gameState.playabilityCache={owner:{[previous]:{isPlayable:true}}};
 let previewIdentity=null;let renders=0;
 manager.bindGameView(new Proxy({}, {get:(_,name)=> name==='replaceBattleCardPreviewIdentity' ? (uid,card)=>{previewIdentity=[uid,card.uid];} : name==='renderGame' ? ()=>{renders++;} : ()=>{}}));
 manager.handleCardInstanceUpdated({ReplacedCardInstanceIds:{[previous]:next},UpdatedCards:[{InstanceId:next,CardId:3,Name:'New',Zone:'Field',OwnerPlayerId:'owner',CurrentHealth:4,CurrentAttack:3,CanAttack:false,Keywords:[]}]});
 assert.equal(manager.gameState.players[0].field.length,1);assert.equal(manager.gameState.players[0].field[0].uid,next);assert.equal(manager.gameState.players[0].field[0].currentHealth,4);assert.equal(manager.gameState.selectedAttackerUid,null);assert.equal(manager.gameState.selectedCardUid,null);assert.equal(manager.gameState.playabilityCache.owner[previous],undefined);assert.deepEqual(previewIdentity,[previous,next]);assert.equal(renders,1);
});
test('leader transformation replaces native leaderCard reference',()=>{
 const previous='33333333-3333-3333-3333-333333333333';const next='44444444-4444-4444-4444-444444444444';
 manager.gameState.players=[{id:'owner',hand:[],field:[],emblem:[],graveyard:[],deck:[],leaderCard:{uid:previous,zone:'Leader',ownerId:'owner'}}];
 manager.handleCardInstanceUpdated({ReplacedCardInstanceIds:{[previous]:next},UpdatedCards:[{InstanceId:next,CardId:4,Name:'Leader',Zone:'Leader',OwnerPlayerId:'owner',CurrentHealth:20,Keywords:[]}]});
 assert.equal(manager.gameState.players[0].leaderCard.uid,next);
});

test('alternate selection fills empty declaration effect',()=>{
 const patch=alternateConfig.alternatePlayPatch({abilityKeywords:[{keyword:'Accelerate',value:0}],effects:[{type:'',parameters:{}}]},8);
 assert.equal(patch.alternatePlay.playAsCardDefinitionId,8);assert.equal(patch.effects[0].type,'Accelerate');assert.equal(patch.effects[0].parameters.CardDefinitionId,8);
});
test('changing alternate card updates both native and declaration values',()=>{
 const patch=alternateConfig.alternatePlayPatch({alternatePlay:{playAsCardDefinitionId:8,availabilityConditions:[{expression:'true'}]},effects:[{type:'Accelerate',parameters:{CardDefinitionId:8}}]},9);
 assert.equal(patch.alternatePlay.playAsCardDefinitionId,9);assert.equal(patch.effects[0].parameters.CardDefinitionId,9);assert.equal(patch.alternatePlay.availabilityConditions.length,1);
});
test('clearing alternate cannot resurrect it from a stale declaration',()=>{
 const patch=alternateConfig.alternatePlayPatch({alternatePlay:{playAsCardDefinitionId:8},effects:[{type:'Accelerate',parameters:{CardDefinitionId:8}}]},0);
 assert.equal(patch.alternatePlay,null);assert.equal(patch.effects[0].parameters.CardDefinitionId,0);
});
test('alternate patch preserves unrelated effects',()=>{
 const effect={type:'Draw',parameters:{DrawAmount:2}};assert.deepEqual(alternateConfig.alternatePlayPatch({effects:[effect]},8).effects,[effect]);
});

test('effect picker changes override a stale alternate declaration',()=>assert.equal(alternateConfig.alternatePlayFromEffects({alternatePlay:{playAsCardDefinitionId:8},effects:[{type:'Accelerate',parameters:{CardDefinitionId:{$type:'StaticValueConfig',Value:9}}}]}).playAsCardDefinitionId,9));
test('cleared effect definition clears the alternate declaration',()=>assert.equal(alternateConfig.alternatePlayFromEffects({alternatePlay:{playAsCardDefinitionId:8},effects:[{type:'Accelerate',parameters:{CardDefinitionId:0}}]}),null));

const numeric=(value,policy='same',layer='Additive')=>({SourceCardName:'Source',SourceAbilityName:'Discount',NumericEffects:[{Field:'Act',Layer:layer,Value:value,PolicyKey:policy}]});
test('matching numeric discounts show their total',()=>{const [group]=liveEffects.summarizeAttachedNumericEffects([numeric(-1),numeric(-1)]);assert.equal(liveEffects.numericEffectTitle(group),'启动费用 -2');assert.equal(group.sources.length,1);});
test('different numeric durations or conditions stay separate',()=>assert.equal(liveEffects.summarizeAttachedNumericEffects([numeric(-1,'turn1'),numeric(-1,'turn2')]).length,2));
test('set and multiply effects never collapse additively',()=>{assert.equal(liveEffects.summarizeAttachedNumericEffects([numeric(2,'same','Set'),numeric(3,'same','Set')]).length,2);assert.equal(liveEffects.numericEffectTitle({field:'Act',layer:'Multiplicative',value:0.5}),'启动费用 ×0.5');});
test('opposing additive effects cancel without showing a false bonus',()=>assert.equal(liveEffects.summarizeAttachedNumericEffects([numeric(-1),numeric(1)]).length,0));

test('reanimate fixed item context appears as Current Item with candidate type',()=>{
 const options=dslOptions.getContextObjectsForOptions({metadata:{dsl:{contextObjects:[]}},fixedRootContext:{name:'self',displayName:'当前项',typeName:'CardInstance',description:'亡召候选随从'}});
 assert.ok(options.some(option=>option.name==='self'&&option.displayName==='当前项'&&option.typeName==='CardInstance'));
});

test('payment modes select the correct label and animation for numeric, named and legacy inputs', () => {
 const cases = [
  { inputs: [[2], ['Accelerate'], [0, '激奏：测试']], label: 'ACC', alternate: true, animation: 'accelerate' },
  { inputs: [[3], ['Crystalize'], [0, '结晶：测试'], [0, '結晶：测试']], label: 'CRY', alternate: true, animation: 'crystalize' },
  { inputs: [[1], ['Enhance']], label: 'ENH', alternate: false },
  { inputs: [[0, '普通卡片']], label: '', alternate: false },
 ];
 for (const { inputs, label, alternate, animation } of cases) {
  for (const args of inputs) {
   const actual = modeVisual.playModeVisual(...args);
   assert.equal(actual.label, label);
   assert.equal(actual.alternate, alternate);
   assert.equal(actual.animation, animation);
  }
 }
});

test('the card detail attack, health and cost icons resolve to PNG images', async () => {
 const signature = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
 for (const stat of ['attack', 'health', 'cost']) {
  const { default: url } = await vite.ssrLoadModule('/src/icon/utx_ic_card_' + stat + '.png');
  const bytes = url.startsWith('data:image/png;base64,')
   ? Buffer.from(url.slice('data:image/png;base64,'.length), 'base64')
   : await fs.readFile(path.join(vite.config.root, url));
  assert.deepEqual(bytes.subarray(0, signature.length), signature, stat);
 }
});

test('alternate text references use Mini cost frames instead of card art', () => {
 for (const name of ['激奏：粗劣的玩具','结晶：崇拜的圣骑士','結晶：测试']) {
  const html=references.buildReferenceCardHtml({name,cost:2,artPreview:'/art.png',description:'测试能力'},name,{fallbackPreviewUrl:'/original.png'});
  assert.match(html,/reference-cost\/cost_2.png/);
  assert.doesNotMatch(html,/background-image|art.png|original.png/);
  assert.match(html,/测试能力/);
 }
 const html=references.buildReferenceCardHtml({name:'普通衍生卡',cost:2,artPreview:'/art.png'},'普通衍生卡');
 assert.match(html,/background-image/);assert.doesNotMatch(html,/reference-cost/);
});

test('reference headers open the exact card by click and keyboard', async () => {
 const handlers={};const header={addEventListener:(name,fn)=>handlers[name]=fn};
 const card={id:123,name:'激奏：测试',type:'Spell',cost:1};const opened=[];
 references.bindReferenceCardDetails({querySelector:()=>header},card,{openCardDetail:value=>opened.push(value)});
 let stopped=0;const event={preventDefault(){},stopPropagation(){stopped++;}};
 await handlers.click(event);assert.equal(opened[0],card);assert.equal(stopped,1);
 handlers.keydown({...event,key:'Enter'});await tick();assert.equal(opened.length,2);
 handlers.keydown({...event,key:' '});await tick();assert.equal(opened.length,3);
 handlers.keydown({...event,key:'Escape'});assert.equal(opened.length,3);
 assert.match(references.buildReferenceCardHtml(card,card.name,{openCardDetail(){}}),/role="button" tabindex="0"/);
});
test('runtime reference details resolve full catalog fields before opening', async () => {
 let click;const full={id:456,name:'结晶：测试',type:'Amulet',class:'Havencraft',cost:2};let opened;
 references.bindReferenceCardDetails({querySelector:()=>({addEventListener:(name,fn)=>{if(name==='click')click=fn;}})},{Id:456,Name:full.name},{cards:[full],openCardDetail:value=>opened=value});
 await click({preventDefault(){},stopPropagation(){}});assert.equal(opened,full);
});

test('reveal geometry moves from the real hand slot to center under browser scaling',()=>{
 const geometry=reveals.revealGeometry({left:10,top:20,width:1000,height:600,layoutWidth:2000},{left:110,top:60,width:60,height:84});
 assert.equal(geometry.x,260);assert.equal(geometry.y,164);
 assert.equal(geometry.dx,740);assert.equal(geometry.dy,436);
 assert.equal(geometry.width*geometry.scale,120);
 assert.ok(Number.isFinite(geometry.height));
});
test('revealing logs the owner and card once without changing the hand',()=>{
 const card={InstanceId:'revealed',Name:'公开测试卡',Cost:2,OwnerPlayerId:'opponent'};
 manager.gameState.localPlayerId='self';manager.gameState.players=[{id:'opponent',hand:[{hidden:true}]}];manager.gameState.gameLog=[];
 let shown;manager.bindGameView(new Proxy({}, {get:(_,name)=>name==='showRevealOverlay'?(...args)=>shown=args:()=>{}}));
 const before=JSON.stringify(manager.gameState.players);
 manager.handleCardRevealed({RevealedCard:card,RevealingPlayerId:'opponent',HandIndex:0,Announcement:'[[player:opponent]]公开了[[cardname:公开测试卡]]。'});
 assert.equal(manager.gameState.gameLog.length,1);assert.match(manager.gameState.gameLog[0],/公开了/);
 assert.equal(shown[0],card);assert.equal(shown[1].handIndex,0);assert.equal(JSON.stringify(manager.gameState.players),before);
 manager.handleCardRevealed({revealedCard:card,revealingPlayerId:'opponent'});assert.match(manager.gameState.gameLog[1],/player:opponent/);
});
test('numeric effects merge two sources into one readable block',()=>{
 const summaries=[numeric(-2),{...numeric(-2),SourceCardName:'Another source',SourceAbilityName:'Another discount'}];
 const groups=liveEffects.summarizeAttachedNumericEffects(summaries),html=liveEffects.renderAttachedNumericEffects(groups);
 assert.equal(groups.length,1);assert.match(html,/费用 -4/);assert.equal((html.match(/<section/g)||[]).length,1);
 assert.match(html,/Another discount/);assert.match(html,/Discount/);
});

test('different stat fields share one block and sources remain clickable',()=>{
 const groups=liveEffects.summarizeAttachedNumericEffects([{SourceCardDefinitionId:123,SourceCardName:'来源',SourceAbilityName:'增益',NumericEffects:[{Field:'Attack',Layer:'Additive',Value:2,PolicyKey:'same'},{Field:'Health',Layer:'Additive',Value:2,PolicyKey:'same'}]}]);
 const html=liveEffects.renderAttachedNumericEffects(groups);assert.equal((html.match(/<section/g)||[]).length,1);assert.match(html,/攻击 \+2/);assert.match(html,/生命 \+2/);assert.match(html,/data-card-id="123"/);assert.equal((html.match(/查看来源卡牌/g)||[]).length,1);
});
test('mulligan hand snapshots keep server replacement slot order',()=>{
 const cards=['kept-first','replacement-middle','kept-last'];let rendered;
 manager.gameState.players=[{id:'owner',hand:[]}];manager.gameState.localPlayerId='different';
 manager.bindGameView(new Proxy({}, {get:(_,name)=>name==='updateHandDisplay'?(id,hand)=>rendered=hand.map(c=>c.uid):()=>{}}));
 manager.handleHandUpdated({PlayerId:'owner',UpdatedHand:cards.map(InstanceId=>({InstanceId,CardId:2,Name:InstanceId,Zone:'Hand',Keywords:[]}))});
 assert.deepEqual(rendered,cards);
});

test('one-use reductions display the next Act total and never merge into permanent totals',()=>{
 const summaries=[1,2].map(i=>({SourceCardName:'双界异兽',SourceAbilityName:'减费'+i,NumericEffects:[{Field:'Act',Layer:'Additive',Value:-1,PolicyKey:'same',RemoveAfterUse:true}]}));
 const groups=liveEffects.summarizeAttachedNumericEffects(summaries);assert.equal(groups.length,1);assert.equal(liveEffects.numericEffectTitle(groups[0]),'下一次启动费用 -2');
 summaries.push({NumericEffects:[{Field:'Act',Layer:'Additive',Value:-1,PolicyKey:'same',RemoveAfterUse:false}]});assert.equal(liveEffects.summarizeAttachedNumericEffects(summaries).length,2);
});

test('keyword grants already listed do not create repeated effect headings',()=>{
 assert.equal(liveEffects.isRepeatedKeywordDetail('获得 Storm',['Storm']),true);
 assert.equal(liveEffects.isRepeatedKeywordDetail('获得 Storm · EndOfTurn（1次）',['疾驰']),true);
 assert.equal(liveEffects.isRepeatedKeywordDetail('获得 Ward',['Storm']),false);
 assert.equal(liveEffects.isRepeatedKeywordDetail('失去 Storm',['Storm']),false);
 assert.equal(liveEffects.isRepeatedKeywordDetail('Cost -2',['Storm']),false);
});

test('identical alert requests share one pending dialog',async()=>{
 const first = dialog.showAlert('重复提示');
 assert.equal(dialog.showAlert('重复提示'), first);
 await tick();
 currentDialog().querySelector('button').click();
 await first;
 assert.equal(document.body.childElementCount, 0);
});
test('destroy snapshots are available to DSL in the On timing',()=>{
 const options=dslOptions.getTriggeringEventFieldOptions({triggeringEventContext:{eventTypes:['Destroy'],timing:'On'},metadata:{events:[{eventType:'Destroy',parameters:[{propertyName:'DestroyTargetSnapshot',displayName:'被破坏目标快照',typeName:'CardInstanceSnapshot',scope:'Both'}]}]},normalizeTypeName:value=>value,getDisplayTypeName:value=>value});
 assert.ok(options.some(option=>option.value==='DestroyTargetSnapshot'));
});

const sample={id:1,name:'测试卡',cost:2,class:'Forestcraft',type:'follower',rarity:'Legend',card_set:'星云基础',card_set_environment:['rotation'],isCollectible:true,attack:3,health:4,tribe:['妖精'],description:'入场曲'};
test('gallery defaults to rotation with no attached cards or emblems',()=>{const f=catalog.defaultCatalogFilter();assert.equal(catalog.matchCatalogCard(sample,f),true);assert.equal(catalog.matchCatalogCard({...sample,isCollectible:false},f),false);assert.equal(catalog.matchCatalogCard({...sample,type:'crest'}, {...f,includeAttached:true}),false);assert.equal(catalog.matchCatalogCard({...sample,card_set_environment:['open']},f),false);});
test('gallery supports stat ranges, searchable tribes, styles, and empty profession means all',()=>{const f={...catalog.defaultCatalogFilter(),attackMin:3,attackMax:3,healthMin:4,healthMax:5,tribe:'妖',hasStyles:true};assert.equal(catalog.matchCatalogCard(sample,f,new Set([1])),true);assert.equal(catalog.matchCatalogCard(sample,{...f,attackMin:4},new Set([1])),false);assert.equal(catalog.matchCatalogCard(sample,f),false);});
test('gallery sorts by cost then profession then pack',()=>{const cards=[{...sample,id:3,cost:1,class:'Dragoncraft'},{...sample,id:2,cost:1},{...sample,id:1,cost:0,class:'Portalcraft'}];assert.deepEqual(cards.sort(catalog.compareCatalogCards).map(c=>c.id),[1,2,3]);});

test('gallery environments distinguish unlimited from open',()=>{
 const f={...catalog.defaultCatalogFilter(),environment:'unlimited'};
 assert.equal(catalog.matchCatalogCard({...sample,card_set_environment:['open']},f),false);
 assert.equal(catalog.matchCatalogCard({...sample,card_set_environment:['rotation','unlimited','open']},f),true);
 assert.equal(catalog.matchCatalogCard({...sample,card_set_environment:['open']},{...f,environment:'open'}),true);
});
test('gallery packs group rotation then unlimited then open, deduplicating memberships',()=>{
 const packs=[['开放',1,['open']],['无限',2,['unlimited','open']],['指定乙',4,['rotation','unlimited']],['指定甲',3,[0,1,2]]].map(([card_set,card_set_code,card_set_environment])=>({...sample,card_set,card_set_code,card_set_environment}));
 assert.deepEqual(catalog.catalogPackOptions([...packs,packs[0]]),['指定甲','指定乙','无限','开放']);
});
test('gallery localized keyword search matches raw abilities',()=>{
 assert.equal(catalog.matchCatalogCard({...sample,keywords:['Storm']},{...catalog.defaultCatalogFilter(),keyword:'疾驰'},new Set(),new Map([['storm','疾驰']])),true);
});

test('Mini draft preview merges copies and sorts by cost without changing pick order',()=>{
 const ids=[1,2,1,3],before=[...ids],map=new Map([[1,{id:1,cost:5,type:'Follower'}],[2,{id:2,cost:1,type:'Spell'}],[3,{id:3,cost:10,type:'Amulet'}]]);
 const result=draftDeck.summarizeDraftDeck(ids,map);
 assert.deepEqual(result.entries.map(e=>[e.id,e.count]),[[2,1],[1,2],[3,1]]);
 assert.deepEqual(result.counts,{follower:2,spell:1,amulet:1,missing:0});
 assert.equal(result.curve[5],2);assert.equal(result.curve[8],1);assert.equal(result.total,4);assert.deepEqual(ids,before);
});
test('Mini draft preview supports empty and missing-card states',()=>{
 const empty=draftDeck.summarizeDraftDeck([],new Map());assert.equal(empty.total,0);assert.equal(empty.curve.reduce((a,b)=>a+b,0),0);
 const missing=draftDeck.summarizeDraftDeck([99],new Map());assert.equal(missing.counts.missing,1);assert.equal(missing.entries[0].count,1);
});

test('normal and draft room receipts share copyable fields and normalize draft GUIDs',()=>{
 const receipt=roomPresentation.draftRoomHandshake({id:'123456781234123412341234567890ab',code:'AB1234',role:'host',player:{playerId:'owner'}});
 assert.equal(receipt.gameId,'12345678-1234-1234-1234-1234567890ab');
 const html=roomPresentation.formatRoomHandshake(receipt);assert.match(html,/GameId:/);assert.match(html,/PlayerId:/);assert.match(html,/JoinCode:/);assert.match(html,/Role: host/);assert.equal((html.match(/data-copy=/g)||[]).length,3);assert.ok(!html.includes('token'));
});
test('room receipts escape text and copy attributes',()=>{
 const html=roomPresentation.formatRoomHandshake({gameId:'<script>"',playerId:'a',joinCode:'123',role:'host'});
 assert.ok(!html.includes('<script>'));assert.match(html,/&lt;script&gt;&quot;/);
});

test('reveal completion gates state updates without delaying chat',async()=>{
 let finish;const completion=new Promise(resolve=>finish=resolve),events=[];
 const gate=revealGateModule.createRevealPresentationGate();
 gate.dispatch('CardRevealed',()=>{events.push('reveal');return completion;});
 gate.dispatch('HandUpdated',()=>events.push('hand'));gate.dispatch('TurnStarted',()=>events.push('turn'));gate.dispatch('ChatMessage',()=>events.push('chat'));
 assert.deepEqual(events,['reveal','chat']);finish();await tick();assert.deepEqual(events,['reveal','chat','hand','turn']);
});
test('a state update between reveals runs in its original position', async () => {
 const gate = revealGateModule.createRevealPresentationGate();
 const events = [];
 let finishFirst, finishSecond;
 gate.dispatch('CardRevealed', () => new Promise(resolve => { finishFirst = resolve; }));
 gate.dispatch('HealthChanged', () => events.push('health'));
 gate.dispatch('CardRevealed', () => {
  events.push('second reveal');
  return new Promise(resolve => { finishSecond = resolve; });
 });
 gate.dispatch('HandUpdated', () => events.push('hand'));
 assert.deepEqual(events, []);
 finishFirst();
 await tick();
 assert.deepEqual(events, ['health', 'second reveal']);
 finishSecond();
 await tick();
 assert.deepEqual(events, ['health', 'second reveal', 'hand']);
});
test('consecutive reveals hold later updates and discard them when the connection goes stale', async () => {
 let current = true;
 const gate = revealGateModule.createRevealPresentationGate(() => current);
 const events = [];
 let finishFirst, finishSecond;
 gate.dispatch('CardRevealed', () => new Promise(resolve => { finishFirst = resolve; }));
 gate.dispatch('CardRevealed', () => {
  events.push('second reveal');
  return new Promise(resolve => { finishSecond = resolve; });
 });
 gate.dispatch('HealthChanged', () => events.push('health'));
 assert.deepEqual(events, []);
 finishFirst();
 await tick();
 assert.deepEqual(events, ['second reveal']);
 current = false;
 finishSecond();
 await tick();
 assert.deepEqual(events, ['second reveal']);
});
test('ordinary game events stay synchronous without a reveal barrier',()=>{const events=[],gate=revealGateModule.createRevealPresentationGate();gate.dispatch('HandUpdated',()=>events.push(1));assert.deepEqual(events,[1]);});
test('dialog emphasis separates failures, successes and ordinary notices',()=>{assert.equal(dialog.alertTone('加载卡牌失败: HTTP 500: Internal Server Error'),'error');assert.equal(dialog.alertTone('卡牌保存成功!'),'success');assert.equal(dialog.alertTone('房间信息已复制'),'success');assert.equal(dialog.alertTone('请选择卡片'),'normal');});

test('catalog style changes presentation without changing identity, stats or abilities',()=>{
 const card={...sample,abilityIds:[7],artPreview:'/base.png'};
 const present=stylePresentation.createCardStylePresentation([{cardId:1,styles:[{id:'alt',appearance:{name:'风格名称',description:'风格描述',artPreview:'/alt.png',id:9,cost:99},bindings:[]}]}],{1:'alt'});
 const shown=present(card);assert.equal(shown.id,1);assert.equal(shown.cost,2);assert.equal(shown.attack,3);assert.equal(shown.abilityIds,card.abilityIds);assert.equal(shown.name,'风格名称');assert.equal(shown.artPreview,'/alt.png');assert.equal(card.name,'测试卡');
 assert.equal(catalog.matchCatalogCard(shown,{...catalog.defaultCatalogFilter(),query:'测试卡'}),true);assert.equal(catalog.matchCatalogCard(shown,{...catalog.defaultCatalogFilter(),query:'风格名称'}),true);
});
test('catalog applies bound card styles and default selection restores original presentation',()=>{
 const configs=[{cardId:1,styles:[{id:'alt',appearance:{name:'主卡'},bindings:[{cardId:2,appearance:{name:'绑定纹章',artPreview:'/bound.png'}}]}]}];
 const bound={...sample,id:2};assert.equal(stylePresentation.createCardStylePresentation(configs,{1:'alt'})(bound).name,'绑定纹章');assert.equal(stylePresentation.createCardStylePresentation(configs,{})(bound),bound);
});
test('surrender is red while cancellation stays the first action',async()=>{
 const result = dialog.confirmDialog('是否投降并返回主菜单？');
 await tick();
 const panel = currentDialog();
 assert.equal(panel.getAttribute('data-tone'), 'danger');
 const firstButton = panel.querySelector('button');
 assert.equal(firstButton.textContent, '取消');
 firstButton.click();
 assert.equal(await result, false);
});
