const __vite__mapDeps=(i,m=__vite__mapDeps,d=(m.f||(m.f=["assets/AbilityEditor-CwU3fQSY.js","assets/DslEditor-DLhydvI_.js","assets/index-CJ3RGFUY.js","assets/index-DExXRLe5.css","assets/cardDefinitionsLoader-BK4UUvCB.js"])))=>i.map(i=>d[i]);
import{s as lt,b as dt,g as X,h as ut,_ as rt,e as pt,a as ht}from"./index-CJ3RGFUY.js";import{D as yt,M as mt,g as j}from"./DslEditor-DLhydvI_.js";import{c as bt,l as vt}from"./cardDefinitionsLoader-BK4UUvCB.js";class ft{constructor(){this.metadataManager=null,this.componentRegistry=new Map,this.isInitialized=!1}async initialize(e){this.isInitialized||(this.metadataManager=e,this.registerDefaultComponents(),this.isInitialized=!0)}registerDefaultComponents(){this.componentRegistry.set("TextInput",this.createTextInput.bind(this)),this.componentRegistry.set("NumberStepper",this.createNumberStepper.bind(this)),this.componentRegistry.set("Checkbox",this.createCheckbox.bind(this)),this.componentRegistry.set("Dropdown",this.createDropdown.bind(this)),this.componentRegistry.set("TextArea",this.createTextArea.bind(this)),this.componentRegistry.set("CardSelector",this.createCardSelector.bind(this)),this.componentRegistry.set("TargetSelector",this.createTargetSelector.bind(this)),this.componentRegistry.set("ValueConfigEditor",this.createValueConfigEditor.bind(this))}renderParameter(e,a=null,i=null){if(!e)return console.error("DynamicUIRenderer: Parameter metadata is required"),this.createErrorElement("参数元数据缺失");const r=this.determineComponentType(e),s=this.componentRegistry.get(r);if(!s)return console.error(`DynamicUIRenderer: Unknown component type "${r}"`),this.createErrorElement(`未知组件类型: ${r}`);try{return s(e,a,i)}catch(n){return console.error("DynamicUIRenderer: Error rendering parameter:",n),this.createErrorElement(`渲染错误: ${n.message}`)}}determineComponentType(e){var r,s,n;if(e.controlType)return this.mapControlTypeToComponent(e.controlType);const a=((r=e.typeName)==null?void 0:r.toLowerCase())||"",i=((s=e.name)==null?void 0:s.toLowerCase())||((n=e.propertyName)==null?void 0:n.toLowerCase())||"";return a==="boolean"||a==="bool"?"Checkbox":a==="int32"||a==="int"||a==="double"||a==="float"||a==="decimal"||a==="single"?"NumberStepper":a==="valueconfig"||i.includes("valueconfig")?"ValueConfigEditor":i.includes("amount")||i.includes("count")||i.includes("value")?"NumberStepper":i.includes("card")||i.includes("carddef")?"CardSelector":i.includes("target")?"TargetSelector":i.includes("expression")||i.includes("formula")?"ValueConfigEditor":e.isEnum||e.options&&e.options.length>0?"Dropdown":a==="string"&&(i.includes("description")||i.includes("text")||i.includes("content"))?"TextArea":"TextInput"}mapControlTypeToComponent(e){return{TextInput:"TextInput",NumberStepper:"NumberStepper",Checkbox:"Checkbox",Dropdown:"Dropdown",TextArea:"TextArea",CardSelector:"CardSelector",TargetSelector:"TargetSelector",DslEditor:"ValueConfigEditor",ValueConfigEditor:"ValueConfigEditor"}[e]||"TextInput"}createTextInput(e,a,i){const r=document.createElement("div");r.className="form-group";const s=document.createElement("label");s.textContent=e.displayName||e.name,s.className="form-label";const n=document.createElement("input");return n.type="text",n.className="form-input",n.value=a||"",n.placeholder=e.description||"",i&&n.addEventListener("input",o=>i(o.target.value)),r.appendChild(s),r.appendChild(n),r}createNumberStepper(e,a,i){const r=document.createElement("div");r.className="form-group";const s=document.createElement("label");s.textContent=e.displayName||e.name,s.className="form-label";const n=document.createElement("div");n.className="number-stepper";const o=document.createElement("input");o.type="number",o.className="form-input number-input",o.value=a||0,o.min=e.min||0,o.max=e.max||999;const l=document.createElement("button");l.type="button",l.className="stepper-btn decrease",l.textContent="-";const p=document.createElement("button");p.type="button",p.className="stepper-btn increase",p.textContent="+";const h=()=>{i&&i(parseInt(o.value)||0)};return o.addEventListener("input",h),l.addEventListener("click",()=>{o.value=Math.max((parseInt(o.value)||0)-1,parseInt(o.min)||0),h()}),p.addEventListener("click",()=>{o.value=Math.min((parseInt(o.value)||0)+1,parseInt(o.max)||999),h()}),n.appendChild(l),n.appendChild(o),n.appendChild(p),r.appendChild(s),r.appendChild(n),r}createCheckbox(e,a,i){const r=document.createElement("div");r.className="form-group checkbox-group";const s=document.createElement("label");s.className="checkbox-label";const n=document.createElement("input");n.type="checkbox",n.className="form-checkbox",n.checked=!!a;const o=document.createElement("span");return o.textContent=e.displayName||e.name,i&&n.addEventListener("change",l=>i(l.target.checked)),s.appendChild(n),s.appendChild(o),r.appendChild(s),r}createDropdown(e,a,i){const r=document.createElement("div");r.className="form-group";const s=document.createElement("label");s.textContent=e.displayName||e.name,s.className="form-label";const n=document.createElement("select");n.className="form-select",n.value=a||"";const o=document.createElement("option");return o.value="",o.textContent="请选择...",n.appendChild(o),e.options&&Array.isArray(e.options)&&e.options.forEach(l=>{const p=document.createElement("option");p.value=l.value||l,p.textContent=l.label||l,a===p.value&&(p.selected=!0),n.appendChild(p)}),i&&n.addEventListener("change",l=>i(l.target.value)),r.appendChild(s),r.appendChild(n),r}createTextArea(e,a,i){const r=document.createElement("div");r.className="form-group";const s=document.createElement("label");s.textContent=e.displayName||e.name,s.className="form-label";const n=document.createElement("textarea");return n.className="form-textarea",n.value=a||"",n.placeholder=e.description||"",n.rows=4,i&&n.addEventListener("input",o=>i(o.target.value)),r.appendChild(s),r.appendChild(n),r}createCardSelector(e,a,i){const r=document.createElement("div");r.className="form-group";const s=document.createElement("label");s.textContent=e.displayName||e.name,s.className="form-label";const n=document.createElement("div");n.className="card-selector-container";const o=document.createElement("input");o.type="text",o.className="card-search-input",o.placeholder="搜索卡牌...";const l=document.createElement("div");l.className="selected-card-display",l.innerHTML=a?`<span class="selected-card-name">${a}</span><button class="clear-selection-btn">×</button>`:'<span class="no-card-selected">未选择卡牌</span>';const p=document.createElement("div");return p.className="card-selector-list",p.style.display="none",o.addEventListener("input",h=>{this.searchCards(h.target.value,p)}),o.addEventListener("focus",()=>{p.style.display="block",this.loadCardOptions(p,i,l)}),document.addEventListener("click",h=>{n.contains(h.target)||(p.style.display="none")}),n.appendChild(o),n.appendChild(l),n.appendChild(p),r.appendChild(s),r.appendChild(n),r}createTargetSelector(e,a,i){const r=document.createElement("div");r.className="form-group";const s=document.createElement("label");s.textContent=e.displayName||e.name,s.className="form-label";const n=document.createElement("div");return n.className="target-selector-placeholder",n.textContent="目标选择器功能正在开发中...",r.appendChild(s),r.appendChild(n),r}createValueConfigEditor(e,a,i){const r=document.createElement("div");r.className="form-group";const s=document.createElement("label");s.textContent=e.displayName||e.name,s.className="form-label";const n=document.createElement("div");n.className="dsl-editor-container",r.appendChild(s),r.appendChild(n);const o=this.getDefaultExpectedType(e);return this.initializeDslEditor(n,a,i,{expectedType:o}),r}async initializeDslEditor(e,a,i,r={}){try{const s=new yt,n=o=>{e&&e.dataset&&(e.dataset.dslExpression=o||""),typeof i=="function"&&i(o)};await s.render(e,a||"",n,r),e._dslEditor=s,this.dslEditors||(this.dslEditors=[]),this.dslEditors.push(s)}catch(s){console.error("DynamicUIRenderer: Failed to initialize DSL editor:",s),e.innerHTML=`
                <div class="error-message">
                    DSL编辑器初始化失败: ${s.message}
                </div>
            `}}getDefaultExpectedType(e){return e&&(e.typeName||e.TypeName)||null}createErrorElement(e){const a=document.createElement("div");a.className="form-group error-group";const i=document.createElement("div");return i.className="error-message",i.textContent=e,a.appendChild(i),a}registerComponent(e,a){this.componentRegistry.set(e,a)}renderEffectParametersForm(e,a={},i=null){if(!this.metadataManager)return console.error("DynamicUIRenderer: MetadataManager not initialized"),this.createErrorElement("元数据管理器未初始化");const r=this.metadataManager.getEffectByType(e);if(!r)return console.error(`DynamicUIRenderer: No metadata found for effect type "${e}"`),this.createErrorElement(`未找到效果类型 "${e}" 的元数据`);const s=document.createElement("div");s.className="effect-parameters-form";let n={...a};if(r.parameters&&r.parameters.length>0)r.parameters.forEach(o=>{const l=n[o.propertyName],p=this.renderParameter(o,l,h=>{n={...n,[o.propertyName]:h},s.dataset.effectParams=JSON.stringify(n),i&&i(n)});p.dataset.paramName=o.propertyName,s.appendChild(p)}),s.dataset.effectParams=JSON.stringify(n);else{const o=document.createElement("div");o.className="no-parameters-message",o.textContent="此效果无需配置参数",s.appendChild(o)}return s}renderDslExpressionEditor(e="",a=null){const i=document.createElement("div");i.className="dsl-expression-editor";const r=document.createElement("label");r.textContent="DSL表达式",r.className="form-label";const s=document.createElement("div");s.className="dsl-editor-container";const n=document.createElement("textarea");n.className="dsl-textarea",n.value=e,n.placeholder="输入DSL表达式，例如：player.health + 5",n.rows=3;const o=document.createElement("div");return o.className="dsl-help-text",o.innerHTML=`
            <small>
                可用的上下文对象：player, opponent, self, target<br>
                可用的函数：count(), sum(), filter() 等
            </small>
        `,a&&n.addEventListener("input",l=>a(l.target.value)),s.appendChild(n),s.appendChild(o),i.appendChild(r),i.appendChild(s),i}async loadCardOptions(e,a,i){try{e.innerHTML='<div class="loading-spinner"></div><p>正在加载卡牌...</p>';const r=await fetch("/api/cards");if(!r.ok)throw new Error(`HTTP ${r.status}: ${r.statusText}`);const s=await r.json();this.renderCardOptions(s,e,a,i)}catch(r){console.error("DynamicUIRenderer: Failed to load cards:",r),e.innerHTML=`
                <div class="error-message">
                    加载卡牌失败: ${r.message}
                </div>
            `}}renderCardOptions(e,a,i,r){if(!Array.isArray(e)||e.length===0){a.innerHTML='<div class="no-cards-message">暂无可用卡牌</div>';return}const s=e.map(o=>`
            <div class="card-option" data-card-id="${o.id}" data-card-name="${o.name}">
                <div class="card-option-info">
                    <span class="card-option-name">${o.name}</span>
                    <span class="card-option-cost">${o.cost}费</span>
                </div>
                <div class="card-option-details">
                    <span class="card-option-type">${o.type}</span>
                    <span class="card-option-class">${o.cardClass}</span>
                </div>
            </div>
        `).join("");a.innerHTML=s,a.querySelectorAll(".card-option").forEach(o=>{o.addEventListener("click",()=>{const l=o.dataset.cardId,p=o.dataset.cardName;r.innerHTML=`
                    <span class="selected-card-name">${p}</span>
                    <button class="clear-selection-btn">×</button>
                `;const h=r.querySelector(".clear-selection-btn");h&&h.addEventListener("click",m=>{m.stopPropagation(),r.innerHTML='<span class="no-card-selected">未选择卡牌</span>',i&&i(null)}),a.style.display="none",i&&i(l)})})}searchCards(e,a){const i=a.querySelectorAll(".card-option"),r=e.toLowerCase();i.forEach(s=>{const o=s.dataset.cardName.toLowerCase().includes(r);s.style.display=o?"block":"none"})}cleanup(){this.dslEditors&&(this.dslEditors.forEach(e=>{try{e.clear&&e.clear()}catch(a){console.error("DynamicUIRenderer: Error cleaning up DSL editor:",a)}}),this.dslEditors=[]),this.metadataManager=null,this.componentRegistry.clear(),this.isInitialized=!1}}const C={cards:null,abilities:null,cardIndex:new Map,abilityIndex:new Map,abilitiesByCard:new Map,cardsPromise:null,abilitiesPromise:null};function Z(t){const e=Number(t);return Number.isFinite(e)&&e>0?e:null}function Ct(t){const e=new Map;return(t||[]).forEach(a=>{const i=Z((a==null?void 0:a.id)??(a==null?void 0:a.Id));i!=null&&e.set(i,a)}),e}function gt(t){const e=new Map;return(t||[]).forEach(a=>{const i=Z((a==null?void 0:a.id)??(a==null?void 0:a.Id));i!=null&&e.set(i,a)}),e}function Et(t){const e=new Map;return(t||[]).forEach(a=>{const i=Z((a==null?void 0:a.cardId)??(a==null?void 0:a.CardId));if(i==null)return;const r=e.get(i)||[];r.push(a),e.set(i,r)}),e}async function wt(){const t=await fetch("/api/cards");if(!t.ok)throw new Error(`HTTP ${t.status}: ${t.statusText}`);const e=await t.json();return Array.isArray(e)?e:[]}async function At(){const t=await fetch("/api/abilities");if(!t.ok)throw new Error(`HTTP ${t.status}: ${t.statusText}`);const e=await t.json();return Array.isArray(e)?e:[]}async function Tt(){return C.cards?C.cards:(C.cardsPromise||(C.cardsPromise=wt().then(t=>(C.cards=t,C.cardIndex=Ct(t),t)).catch(t=>(console.warn("AbilityDataCache: failed to load cards",t),C.cards=[],C.cardIndex=new Map,[])).finally(()=>{C.cardsPromise=null})),await C.cardsPromise)}async function Lt(){return C.abilities?C.abilities:(C.abilitiesPromise||(C.abilitiesPromise=At().then(t=>(C.abilities=t,C.abilityIndex=gt(t),C.abilitiesByCard=Et(t),t)).catch(t=>(console.warn("AbilityDataCache: failed to load abilities",t),C.abilities=[],C.abilityIndex=new Map,C.abilitiesByCard=new Map,[])).finally(()=>{C.abilitiesPromise=null})),await C.abilitiesPromise)}async function st(){return await Promise.all([Tt(),Lt()]),{cards:C.cards||[],abilities:C.abilities||[],cardIndex:C.cardIndex,abilityIndex:C.abilityIndex,abilitiesByCard:C.abilitiesByCard}}function Yt(){return{cards:C.cards||[],abilities:C.abilities||[],cardIndex:C.cardIndex,abilityIndex:C.abilityIndex,abilitiesByCard:C.abilitiesByCard}}function St(){C.cards=null,C.abilities=null,C.cardIndex=new Map,C.abilityIndex=new Map,C.abilitiesByCard=new Map,C.cardsPromise=null,C.abilitiesPromise=null}async function It(){return St(),await st()}function T(t){if(typeof t!="string")return t;const e=t.trim();return e&&(e.includes("_")?e.split("_").map(a=>a&&a[0].toUpperCase()+a.slice(1)).join("_"):e[0].toUpperCase()+e.slice(1))}function xt(t){return String(t).replace(/[.*+?^${}()|[\]\\]/g,"\\$&")}function $t(t){return String(t).replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;").replace(/'/g,"&#39;")}function I(t,e){return typeof t!="string"||typeof e!="string"?t===e:t.toLowerCase()===e.toLowerCase()}let J=null;function Nt(t){return J||(J=pt(["CardRarity","CardClass","CardType"]).then(()=>{t!=null&&t.currentCard&&t.updateCardPreview()}).catch(e=>{console.warn("CardEditor: Failed to load card enum labels.",e)})),J}function kt(t){!t||t._detailHandlerBound||(t._detailHandlerBound=!0,t.addEventListener("click",e=>{if(!e.target.closest(".preview-detail-name"))return;const i=t._detailPayload;i!=null&&i.card&&ht(i.card,{previewUrl:i.previewUrl,detailUrl:i.detailUrl})}))}function Q(t){const e=(t==null?void 0:t.card_set)??(t==null?void 0:t.cardSet)??(t==null?void 0:t.CardSet)??(t==null?void 0:t.card_set_name)??(t==null?void 0:t.cardSetName)??"",a=(t==null?void 0:t.card_set_code)??(t==null?void 0:t.cardSetCode)??"",i=(t==null?void 0:t.card_set_abbr)??(t==null?void 0:t.cardSetAbbr)??"",r=(t==null?void 0:t.card_set_environment)??(t==null?void 0:t.cardSetEnvironment)??(t==null?void 0:t.card_set_environments)??(t==null?void 0:t.cardSetEnvironments)??null;return{name:e==null?"":String(e).trim(),code:a==null?"":String(a).trim(),abbr:i==null?"":String(i).trim(),environments:Array.isArray(r)?r.map(s=>s==null?"":String(s)).filter(Boolean):r==null?[]:[String(r)]}}function Dt(t){return t&&t.name||""}function V(t){const e=(t==null?void 0:t.abilityKeywords)??[],a=[],i=(r,s)=>{if(!r||typeof r!="string")return;const n=a.find(o=>o.keyword.toLowerCase()===r.toLowerCase());if(n){s!=null&&(n.value=s);return}a.push({keyword:r,value:s??null})};return Array.isArray(e)?e.forEach(r=>{if(r){if(typeof r=="string"){i(r);return}if(typeof r=="object"){const s=r.keyword||r.tag||r.Keyword||r.Tag,n=r.value??r.amount??r.cost??r.Value??null;i(s,n)}}}):typeof e=="string"&&i(e),a}function Mt(t){return!Array.isArray(t)||t.length===0?"":t.map(e=>e.value!==null&&e.value!==void 0?`${e.keyword}:${e.value}`:e.keyword).filter(e=>e&&typeof e=="string").join(", ")}function nt(t){const e=V(t);return e.length===0?"":Mt(e)}function Rt(t){return!t||!t.keyword?"":t.value!==null&&t.value!==void 0&&t.value!==""?`${t.keyword}:${t.value}`:t.keyword}const Ft={0:"Triggered",1:"Continuous",2:"Activated"};function qt(t,e){var r;const a=(t==null?void 0:t.abilityType)??(t==null?void 0:t.AbilityType)??(e==null?void 0:e.abilityType),i=T((e==null?void 0:e.type)??(t==null?void 0:t.type)??(t==null?void 0:t.Type)??((r=t==null?void 0:t.jsonConfig)==null?void 0:r.type)??"");if(a!=null&&a!==""){const s=Number(a);if(Number.isFinite(s)){const n=Ft[s];return i&&n&&i.toLowerCase()!==n.toLowerCase()?i:n||i||"Triggered"}if(typeof a=="string")return T(a)||i||"Triggered"}return i||"Triggered"}function ot(t){return String(t??"").trim().toLowerCase()}function at(t){return t?typeof t=="string"||typeof t=="number"?String(t):typeof t=="object"?t.eventType??t.EventType??t.type??t.Type??t.name??t.Name??"":String(t):""}function Bt(t){if(Array.isArray(t))return t.map(a=>at(a)).filter(Boolean);const e=at(t);return e?[e]:[]}function Pt(t=[]){const e=new Map;return(t||[]).forEach(a=>{if(!a)return;const i=ot(a.eventType??a.type??a.name),r=a.displayName??a.name??a.eventType??a.type;i&&r&&!e.has(i)&&e.set(i,String(r))}),e}function Ot(t,e){const a=ot(t);return a&&(e!=null&&e.has(a))?e.get(a):T(t)}function Ht(t,e,a){var l,p;const r=String(e||(t==null?void 0:t.type)||"").trim().toLowerCase()==="continuous",s=Bt(((l=t==null?void 0:t.trigger)==null?void 0:l.eventTypes)??((p=t==null?void 0:t.trigger)==null?void 0:p.EventTypes)),o=s.map(h=>Ot(h,a)).filter(Boolean);return Array.from(new Set(o))}class d{constructor(e={}){this.container=null,this.containerId=e.containerId||"card-editor-container",this.layoutMode=e.layoutMode||"default",this.metadataManager=new mt,this.uiRenderer=new ft,this.currentCard=null,this.isInitialized=!1,this.keywordLabelMap=null,this.artCacheBuster=0,this.cardListScrollTop=0,this.shouldRestoreCardListScroll=!1,this.allCardsData=[],this.filteredCardsData=[],this.cardSetDefinitions=[],this.dataRevision=null,this.cardListCache=null,this.cardDetailCache=new Map,this.abilitiesByCardCache=new Map,this.abilityDetailCache=new Map,this.abilityCountsCache=new Map,this.abilitiesLibraryCache=null}async initialize(){if(!this.isInitialized)try{if(this.container=document.getElementById(this.containerId),!this.container)throw new Error("Card editor container not found");this.showLoading("正在加载元数据..."),await this.metadataManager.initialize(),await this.uiRenderer.initialize(this.metadataManager),await this.loadCardSetDefinitions(),this.createEditorLayout(),this.updateTabAvailability(),await this.populateCardListFilterOptions(),this.bindEventListeners(),await this.loadCardList(),this.isInitialized=!0}catch(e){throw console.error("CardEditor: Initialization failed:",e),this.showError("初始化失败",e.message),e}}showLoading(e="SYSTEM LOADING..."){this.container&&(this.container.innerHTML=`
                <div class="loading-container tech-panel">
                    <div class="loading-spinner-tech"></div>
                    <div class="loading-text neon-text">${e}</div>
                </div>
            `)}showError(e,a){this.container&&(this.container.innerHTML=`
                <div class="error-container tech-panel error-state">
                    <div class="error-icon-tech"></div>
                    <h3 class="error-title neon-text-red">${e}</h3>
                    <p class="error-message">${a}</p>
                    <button class="btn-tech btn-retry" onclick="location.reload()">REBOOT SYSTEM</button>
                </div>
            `)}createEditorLayout(){const e=this.layoutMode==="test"?"card-editor-layout layout-test":"card-editor-layout";this.container.innerHTML=`
            <div class="${e}">
                <div class="editor-grid-bg"></div>
                
                <!-- 左栏：卡牌列表 -->
                <div class="editor-sidebar left-sidebar tech-panel">
                    <div class="sidebar-header">
                        <h3 class="tech-header">DATABASE // CARDS</h3>
                        <button class="btn-tech btn-primary" id="new-card-btn">
                            NEW ENTRY
                        </button>
                    </div>
                    <div class="sidebar-content">
                        <div class="search-section">
                            <input type="text" id="card-search" placeholder="SEARCH DATABASE..." class="search-input tech-input">
                            <div class="filter-section">
                                <select id="class-filter" class="filter-select tech-select">
                                    <option value="">ALL CLASSES</option>
                                </select>
                                <select id="type-filter" class="filter-select tech-select">
                                    <option value="">ALL TYPES</option>
                                </select>
                                <select id="rarity-filter" class="filter-select tech-select">
                                    <option value="">ALL RARITIES</option>
                                </select>
                                <select id="set-filter" class="filter-select tech-select">
                                    <option value="">ALL SETS</option>
                                </select>
                            </div>
                        </div>
                        <div class="card-list custom-scrollbar" id="card-list">
                            <div class="card-list-placeholder">
                                <p class="tech-text-dim">NO DATA FOUND</p>
                                <small class="tech-text-small">INITIATE NEW ENTRY</small>
                            </div>
                        </div>
                    </div>
                </div>

                <!-- 中栏：编辑区 -->
                <div class="editor-main tech-panel">
                    <div class="editor-header">
                        <h3 id="editor-title" class="tech-header-large">AWAITING SELECTION</h3>
                        <div class="editor-actions">
                            <button class="btn-tech btn-secondary" id="save-btn" disabled>SAVE DATA</button>
                            <button class="btn-tech btn-secondary" id="save-as-btn" disabled>SAVE AS</button>
                            <button class="btn-tech btn-danger" id="delete-btn" disabled>DELETE</button>
                        </div>
                    </div>
                    <div class="editor-content">
                        <div class="editor-tabs">
                            <button class="tab-button active" data-tab="basic">BASIC INFO</button>
                            <button class="tab-button" data-tab="abilities">ABILITIES</button>
                            <button class="tab-button" data-tab="assets">ASSETS</button>
                        </div>
                        <div class="tab-content custom-scrollbar">
                            <div class="tab-panel active" id="basic-tab">
                                <div class="form-placeholder">
                                    <p class="tech-text-dim">SELECT A CARD TO EDIT</p>
                                </div>
                            </div>
                            <div class="tab-panel" id="abilities-tab">
                                <div class="abilities-container">
                                    <div class="abilities-header">
                                        <h4 class="tech-header-small">ABILITY CONFIGURATION</h4>
                                        <div class="abilities-header-actions">
                                            <button class="btn-tech btn-small" id="add-ability-btn">ADD ABILITY</button>
                                            <button class="btn-tech btn-small" id="link-ability-btn">COPY EXISTING</button>
                                        </div>
                                    </div>
                                    <div class="abilities-content">
                                        <div class="abilities-list" id="abilities-list">
                                            <div class="abilities-placeholder">
                                                <p class="tech-text-dim">NO ABILITIES LINKED</p>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </div>
                            <div class="tab-panel" id="assets-tab">
                                <div class="assets-container" id="assets-container">
                                    <div class="form-placeholder">
                                        <p class="tech-text-dim">SELECT A CARD TO EDIT</p>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                <!-- 右栏：预览区 -->
                <div class="editor-sidebar right-sidebar tech-panel">
                    <div class="sidebar-header">
                        <h3 class="tech-header">VISUALIZATION</h3>
                    </div>
                    <div class="sidebar-content">
                        <div class="card-preview-container" id="card-preview">
                            <div class="preview-placeholder">
                                <p class="tech-text-dim">AWAITING DATA</p>
                            </div>
                        </div>
                        <div class="preview-info">
                            <h4 class="tech-header-small">DEBUG INFO</h4>
                            <div class="preview-details tech-code-block" id="preview-details">
                                <p>NO DATA</p>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `}bindEventListeners(){this.container.addEventListener("click",l=>{if(l.target.classList.contains("tab-button")){if(l.target.disabled)return;this.switchTab(l.target.dataset.tab)}}),this.container.addEventListener("click",l=>{const p=l.target.closest("[data-preview-token]");if(!p)return;const h=p.dataset.previewToken,m=p.dataset.previewValue;!h||!m||(l.preventDefault(),l.stopPropagation(),lt(p,h,m,{cards:this.allCardsData}))});const e=this.container.querySelector("#save-btn");e&&e.addEventListener("click",async()=>{await this.saveCurrentCard(!1)});const a=this.container.querySelector("#save-as-btn");a&&a.addEventListener("click",async()=>{await this.saveCurrentCard(!0)});const i=this.container.querySelector("#delete-btn");i&&i.addEventListener("click",async()=>{await this.deleteCurrentCard()});const r=this.container.querySelector("#new-card-btn");r&&r.addEventListener("click",()=>this.createNewCard());const s=this.container.querySelector("#card-search");s&&s.addEventListener("input",()=>this.applyCardListFilters()),["#class-filter","#type-filter","#rarity-filter","#set-filter"].forEach(l=>{const p=this.container.querySelector(l);p&&p.addEventListener("change",()=>this.applyCardListFilters())});const n=this.container.querySelector("#add-ability-btn");n&&n.addEventListener("click",()=>this.showAddAbilityModal());const o=this.container.querySelector("#link-ability-btn");o&&o.addEventListener("click",()=>this.showLinkExistingAbilityModal())}switchTab(e){this.container.querySelectorAll(".tab-button").forEach(r=>{r.classList.remove("active")}),this.container.querySelectorAll(".tab-panel").forEach(r=>{r.classList.remove("active")});const a=this.container.querySelector(`[data-tab="${e}"]`),i=this.container.querySelector(`#${e}-tab`);a&&i&&(a.classList.add("active"),i.classList.add("active"),e==="abilities"&&this.currentCard&&this.renderAbilitiesList(),e==="assets"&&this.renderAssetsTab())}async createNewCard(){var r;this.currentCard={id:null,name:"",description:"",cost:0,attack:0,health:1,countdown:0,cardType:"Follower",cardClass:"Neutral",rarity:"Bronze",cardSet:"特殊",keywords:[],tribe:[],canEvolve:!1,evolveId:null,isCollectible:!0,artPreview:null,artDetail:null,isNew:!0},this.updateTabAvailability();const e=this.container.querySelector("#editor-title");e&&(e.textContent="新建卡牌"),this.enableEditorButtons(),await this.renderBasicInfoForm(),this.updateCardPreview(),((r=this.container.querySelector(".tab-button.active"))==null?void 0:r.dataset.tab)==="assets"&&this.renderAssetsTab();const i=this.container.querySelector("#abilities-list");i&&(i.innerHTML=`
                <div class="abilities-placeholder">
                    <p>请先保存卡牌后再配置能力</p>
                </div>
            `)}cleanup(){this.isInitialized=!1,this.currentCard=null,this.container&&(this.container.innerHTML="")}}let G=null;const z=new Map;async function Kt(t={}){const e=t.containerId||"card-editor-container",a=t.layoutMode||"default";let i=z.get(e);return i?i.layoutMode=a:(i=new d({containerId:e,layoutMode:a}),z.set(e,i)),G=i,await i.initialize(),i}function Ut(t="card-editor-container"){return z.get(t)||G}function jt(t="card-editor-container"){const e=z.get(t);e&&(e.cleanup(),z.delete(t),G===e&&(G=null))}d.prototype.getCardIdKey=function(t){return t==null?null:String(t)};d.prototype.getAbilityIdKey=function(t){return t==null?null:String(t)};d.prototype.applyDataRevisionFromResponse=function(t){if(!t||!t.headers)return!1;const e=t.headers.get("X-Data-Revision");return e?this.dataRevision&&this.dataRevision!==e?(this.clearDataCaches(),this.dataRevision=e,this.queueCardListReload(),!0):(this.dataRevision||(this.dataRevision=e),!1):!1};d.prototype.queueCardListReload=function(){this._revisionReloadPending||!this.container||!this.container.querySelector("#card-list")||(this._revisionReloadPending=!0,setTimeout(()=>{this.loadCardList({force:!0}).catch(e=>console.warn("CardEditor: Failed to reload card list after revision change.",e)).finally(()=>{this._revisionReloadPending=!1})},0))};d.prototype.clearDataCaches=function(){this.cardListCache=null,this.cardDetailCache.clear(),this.abilitiesByCardCache.clear(),this.abilityDetailCache.clear(),this.abilityCountsCache.clear(),this.abilitiesLibraryCache=null,this.allCardsData=[],this.filteredCardsData=[]};d.prototype.refreshAbilityDataCacheAfterSave=async function(){try{await It()}catch(t){console.warn("CardEditor: Failed to refresh ability data cache after save.",t)}};d.prototype.invalidateCardListCache=function(){this.cardListCache=null,this.allCardsData=[],this.filteredCardsData=[]};d.prototype.invalidateCardDetailCache=function(t){const e=this.getCardIdKey(t);e&&this.cardDetailCache.delete(e)};d.prototype.invalidateAbilitiesForCard=function(t){const e=this.getCardIdKey(t);e&&(this.abilitiesByCardCache.delete(e),this.abilityCountsCache.delete(e))};d.prototype.invalidateAbilityDetailCache=function(t){const e=this.getAbilityIdKey(t);e&&this.abilityDetailCache.delete(e)};d.prototype.invalidateAbilityLibraryCache=function(){this.abilitiesLibraryCache=null};d.prototype.upsertCardInCache=function(t){const e=this.getCardIdKey((t==null?void 0:t.id)??(t==null?void 0:t.CardId));if(!e)return;const a=Array.isArray(this.cardListCache)?[...this.cardListCache]:[],i=a.findIndex(r=>this.getCardIdKey((r==null?void 0:r.id)??(r==null?void 0:r.CardId))===e);i>=0?a[i]=t:a.push(t),this.cardListCache=a,this.allCardsData=a,this.cardDetailCache.set(e,t)};d.prototype.removeCardFromCache=function(t){const e=this.getCardIdKey(t);e&&(Array.isArray(this.cardListCache)&&(this.cardListCache=this.cardListCache.filter(a=>this.getCardIdKey((a==null?void 0:a.id)??(a==null?void 0:a.CardId))!==e)),Array.isArray(this.cardListCache)?this.allCardsData=this.cardListCache:this.allCardsData=[],this.cardDetailCache.delete(e),this.abilitiesByCardCache.delete(e),this.abilityCountsCache.delete(e))};d.prototype.enableEditorButtons=function(){var i;const t=this.container.querySelector("#save-btn"),e=this.container.querySelector("#save-as-btn"),a=this.container.querySelector("#delete-btn");t&&(t.disabled=!1),e&&(e.disabled=!1),a&&(a.disabled=(i=this.currentCard)==null?void 0:i.isNew)};d.prototype.disableEditorButtons=function(){const t=this.container.querySelector("#save-btn"),e=this.container.querySelector("#save-as-btn"),a=this.container.querySelector("#delete-btn");t&&(t.disabled=!0),e&&(e.disabled=!0),a&&(a.disabled=!0)};d.prototype.generateSelectOptions=function(t,e){return!t||!Array.isArray(t)?'<option value="">加载中...</option>':t.map(a=>{const i=a.value||a,r=a.label||a,s=I(i,e)?"selected":"";return`<option value="${i}" ${s}>${r}</option>`}).join("")};d.prototype.renderOptionButtonGroup=function(t,e){return!t||!Array.isArray(t)||t.length===0?'<div class="option-group-empty">LOADING...</div>':t.map(a=>{const i=a.value||a,r=a.label||a;return`
            <button type="button" class="option-chip${I(i,e)?" is-selected":""}" data-option-value="${i}">
                ${r}
            </button>
        `}).join("")};d.prototype.renderBasicInfoFormCompact=function({cardTypeOptions:t,cardClassOptions:e,cardRarityOptions:a,readOnlyAttr:i,disabledAttr:r}){const s=this.currentCard.artPreview||"",n=s?this.getArtAssetUrl(s):"",o=!this.currentCard.isNew;return`
        <div class="form-container tech-form basic-compact">
            ${this.currentCard.isEvolvedForm?`
                <div class="evolution-form-notice tech-notice">
                    <span class="evolution-notice-icon">EVOLVED</span>
                    <span class="evolution-notice-text">EDITING EVOLVED FORM</span>
                </div>
            `:""}
            <div class="basic-compact-main">
                <div class="basic-compact-fields">
                    <div class="basic-compact-row">
                        <label class="form-label required">NAME</label>
                        <input type="text" id="card-name" class="form-input tech-input" value="${this.currentCard.name}" placeholder="ENTER NAME" ${i}>
                    </div>
                    <div class="basic-compact-row basic-compact-stats">
                        <label class="form-label required">STATS</label>
                        <div class="basic-compact-stats-grid">
                            <div class="form-group">
                                <label class="form-label required">COST</label>
                                <div class="number-stepper tech-stepper">
                                    <button type="button" class="stepper-btn decrease" data-target="card-cost" ${r}>-</button>
                                    <input type="number" id="card-cost" class="form-input number-input tech-input" value="${this.currentCard.cost}" min="0" max="20" ${i}>
                                    <button type="button" class="stepper-btn increase" data-target="card-cost" ${r}>+</button>
                                </div>
                            </div>
                            <div id="stats-container" class="basic-compact-stats">
                                <div class="form-group">
                                    <label class="form-label">ATTACK</label>
                                    <div class="number-stepper tech-stepper">
                                        <button type="button" class="stepper-btn decrease" data-target="card-attack">-</button>
                                        <input type="number" id="card-attack" class="form-input number-input tech-input" value="${this.currentCard.attack}" min="0" max="99">
                                        <button type="button" class="stepper-btn increase" data-target="card-attack">+</button>
                                    </div>
                                </div>
                                <div class="form-group">
                                    <label class="form-label">HEALTH</label>
                                    <div class="number-stepper tech-stepper">
                                        <button type="button" class="stepper-btn decrease" data-target="card-health">-</button>
                                        <input type="number" id="card-health" class="form-input number-input tech-input" value="${this.currentCard.health}" min="1" max="99">
                                        <button type="button" class="stepper-btn increase" data-target="card-health">+</button>
                                    </div>
                                </div>
                            </div>
                            <div id="countdown-container" class="basic-compact-countdown">
                                <div class="form-group">
                                    <label class="form-label">COUNTDOWN</label>
                                    <div class="number-stepper tech-stepper">
                                        <button type="button" class="stepper-btn decrease" data-target="card-countdown">-</button>
                                        <input type="number" id="card-countdown" class="form-input number-input tech-input" value="${this.currentCard.countdown??0}" min="0" max="99">
                                        <button type="button" class="stepper-btn increase" data-target="card-countdown">+</button>
                                    </div>
                                    <div class="form-help">设置倒计时需要添加倒数关键字。</div>
                                </div>
                            </div>
                        </div>
                    </div>
                    <div class="basic-compact-row">
                        <label class="form-label required">TYPE</label>
                        <div class="option-group" data-option-group="type">
                            ${this.renderOptionButtonGroup(t,this.currentCard.cardType)}
                        </div>
                    </div>
                    <div class="basic-compact-row">
                        <label class="form-label required">RARITY</label>
                        <div class="option-group" data-option-group="rarity">
                            ${this.renderOptionButtonGroup(a,this.currentCard.rarity)}
                        </div>
                    </div>
                    <div class="basic-compact-row">
                        <label class="form-label required">CLASS</label>
                        <div class="option-group" data-option-group="class">
                            ${this.renderOptionButtonGroup(e,this.currentCard.cardClass)}
                        </div>
                    </div>
                </div>
                <div class="basic-compact-art">
                    <div class="basic-art-header">
                        <div class="tech-header-small">PREVIEW ART</div>
                        <span class="asset-hint">Used in editor preview</span>
                    </div>
                    <div class="basic-art-preview${n?" has-image":""}">
                        ${n?`<img src="${n}" alt="Preview art">`:'<div class="asset-placeholder">NO PREVIEW IMAGE</div>'}
                    </div>
                    <div class="basic-art-actions">
                        <button type="button" class="btn-tech btn-small" data-upload-kind="preview" ${o?"":"disabled"}>UPLOAD</button>
                    </div>
                    <div class="basic-art-path">${s||"NO FILE"}</div>
                </div>
            </div>
            <div class="basic-compact-extra">
                <div class="form-section tech-section">
                    <h4 class="form-section-title tech-header-small">DESCRIPTION</h4>
                    <div class="form-group">
                        <label class="form-label">DESCRIPTION</label>
                        <textarea id="card-description" class="form-textarea tech-input" placeholder="ENTER DESCRIPTION" ${i}>${this.currentCard.description}</textarea>
                    </div>
                </div>

                <div class="form-section tech-section">
                    <h4 class="form-section-title tech-header-small">TAGS & METADATA</h4>
                    <div class="form-grid form-grid-2">
                        <div class="form-group">
                            <label class="form-label">KEYWORDS</label>
                            <div class="tag-input-container tech-tag-container">
                                <div class="tag-list" id="keywords-list">
                                    ${this.renderTagList(this.currentCard.keywords||[],"keywords")}
                                </div>
                                <div class="tag-input-wrapper">
                                    <select id="keywords-select" class="tag-select tech-select">
                                        <option value="">SELECT KEYWORD</option>
                                    </select>
                                    <button type="button" class="tag-add-btn btn-tech-small" data-target="keywords">ADD</button>
                                </div>
                            </div>
                        </div>
                        <div class="form-group">
                            <label class="form-label">TRIBE</label>
                            <div class="tag-input-container tech-tag-container">
                                <div class="tag-list" id="tribe-list">
                                    ${this.renderTagList(this.currentCard.tribe||[],"tribe")}
                                </div>
                                <div class="tag-input-wrapper">
                                    <input type="text" id="tribe-input" class="tag-input tech-input" placeholder="ENTER TRIBE">
                                    <button type="button" class="tag-add-btn btn-tech-small" data-target="tribe">ADD</button>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                <div class="form-section tech-section">
                    <h4 class="form-section-title tech-header-small">CLASSIFICATION</h4>
                    <div class="form-grid form-grid-2">
                        <div class="form-group">
                            <label class="form-label">SET</label>
                            <input type="text" id="card-set" class="form-input tech-input" value="${this.currentCard.cardSet||""}" placeholder="ENTER SET" list="card-set-options" ${i}>
                            <datalist id="card-set-options"></datalist>
                        </div>
                        <div class="form-group">
                            <label class="form-label">COLLECTIBLE</label>
                            <label class="form-toggle">
                                <input type="checkbox" id="card-collectible" ${this.currentCard.isCollectible?"checked":""}>
                                <span class="form-toggle-text" id="card-collectible-label">${this.currentCard.isCollectible?"YES":"NO"}</span>
                            </label>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    `};d.prototype.populateCardListFilterOptions=async function(){const t=this.container.querySelector("#class-filter"),e=this.container.querySelector("#type-filter"),a=this.container.querySelector("#rarity-filter");if(!t||!e||!a)return;const[i,r,s]=await Promise.all([this.metadataManager.getCardClassOptions(),this.metadataManager.getCardTypeOptions(),this.metadataManager.getCardRarityOptions()]);t.innerHTML='<option value="">ALL CLASSES</option>'+this.generateSelectOptions(i,""),e.innerHTML='<option value="">ALL TYPES</option>'+this.generateSelectOptions(r,""),a.innerHTML='<option value="">ALL RARITIES</option>'+this.generateSelectOptions(s,"")};d.prototype.populateCardSetFilterOptions=function(t){const e=this.container.querySelector("#set-filter");if(!e)return;const a=e.value||"",i=new Map;(Array.isArray(t)?t:[]).forEach(n=>{const o=Q(n),l=o.name;if(!l)return;const p=l.toLowerCase();i.has(p)||i.set(p,{value:l,label:Dt(o)})});const s=Array.from(i.values()).sort((n,o)=>n.label.localeCompare(o.label,void 0,{numeric:!0,sensitivity:"base"}));e.innerHTML='<option value="">ALL SETS</option>'+this.generateSelectOptions(s,a)};d.prototype.loadCardSetDefinitions=async function(){try{const t=await fetch("/api/cardsets");if(!t.ok)throw new Error(`HTTP ${t.status}`);const e=await t.json();this.cardSetDefinitions=Array.isArray(e)?e:[]}catch(t){console.warn("CardEditor: Failed to load card set definitions.",t),this.cardSetDefinitions=[]}this.refreshCardSetDatalist()};d.prototype.refreshCardSetDatalist=function(){var i;const t=(i=this.container)==null?void 0:i.querySelector("#card-set-options");if(!t)return;const e=new Set;(this.cardSetDefinitions||[]).forEach(r=>{const s=(r==null?void 0:r.name)??(r==null?void 0:r.Name)??"";s&&e.add(String(s).trim())});const a=Array.from(e).filter(Boolean).sort((r,s)=>r.localeCompare(s,void 0,{numeric:!0,sensitivity:"base"})).map(r=>`<option value="${$t(r)}"></option>`).join("");t.innerHTML=a};d.prototype.applyCardListFilters=function(){const t=this.container.querySelector("#card-search"),e=this.container.querySelector("#class-filter"),a=this.container.querySelector("#type-filter"),i=this.container.querySelector("#rarity-filter"),r=this.container.querySelector("#set-filter"),s=((t==null?void 0:t.value)||"").trim().toLowerCase(),n=(e==null?void 0:e.value)||"",o=(a==null?void 0:a.value)||"",l=(i==null?void 0:i.value)||"",p=(r==null?void 0:r.value)||"",f=(Array.isArray(this.filteredCardsData)?this.filteredCardsData:[]).filter(y=>{const w=(y.name??y.Name??"").toString().toLowerCase(),v=T(y.class??y.Class??""),L=T(y.type??y.Type??""),c=T(y.rarity??y.Rarity??""),S=Q(y),P=!p||I(S.name,p);return!(s&&!w.includes(s)||n&&!I(v,n)||o&&!I(L,o)||l&&!I(c,l)||!P)}).slice().sort(bt);this.renderCardList(f)};d.prototype.updateCardPreview=function(){const t=this.container.querySelector("#card-preview");if(!t||!this.currentCard)return;const e=this.currentCard,a=I(T(e.cardType),"Follower"),i=e.rarity||"Common",r=e.cardClass||"Neutral",s=e.cardType||"Spell",n=T(i)||"Common",o=T(r)||"Neutral",l=T(s)||"Spell",p=n.toLowerCase(),h=e.artPreview||"",m=h?this.getArtAssetUrl(h):"",f=e.artDetail||"",y=f?this.getArtAssetUrl(f):"",w=e.countdown??e.Countdown??0,v=!a&&w>0,c=(Array.isArray(e.keywords)?e.keywords:Array.isArray(e.Keywords)?e.Keywords:[]).some(q=>I(q,"EarthSigil")),S=c?1:0,P=!a&&c,O=(Array.isArray(e.tribe)?e.tribe:[]).map(q=>(q??"").toString().trim()).filter(Boolean).join("·");Nt(this),t.innerHTML=dt({rarityClass:p,rarityLabel:X("CardRarity",i,n),classLabel:X("CardClass",r,o),typeLabel:X("CardType",s,l),name:e.name||"UNTITLED",costValue:e.cost,isFollower:a,attackValue:e.attack,healthValue:e.health,countdownValue:w,showCountdown:v,earthsigilValue:S,showEarthsigil:P,description:e.description||"",previewUrl:m,detailUrl:y,enableDetail:!0,tribeText:O}),t._detailPayload={card:e,previewUrl:m,detailUrl:y},kt(t),ut(t,{cards:this.allCardsData,fallbackPreviewUrl:m||y});const F=this.container.querySelector("#preview-details");F&&(F.innerHTML=`
            <div class="debug-grid">
                <div class="debug-row">
                    <span class="debug-label">ID</span>
                    <span class="debug-value">${e.id||"N/A"}</span>
                </div>
                <div class="debug-row">
                    <span class="debug-label">INTERNAL NAME</span>
                    <span class="debug-value">${e.name||"N/A"}</span>
                </div>
                <div class="debug-row">
                    <span class="debug-label">KEYWORDS</span>
                    <span class="debug-value">[${(e.keywords||[]).join(", ")}]</span>
                </div>
                <div class="debug-row">
                    <span class="debug-label">TRIBE</span>
                    <span class="debug-value">[${(e.tribe||[]).join(", ")}]</span>
                </div>
                <div class="debug-row">
                    <span class="debug-label">EVOLVE ID</span>
                    <span class="debug-value">${e.evolveId||"None"}</span>
                </div>
                <div class="debug-row">
                    <span class="debug-label">IS NEW</span>
                    <span class="debug-value">${e.isNew?"YES":"NO"}</span>
                </div>
                <div class="debug-row">
                    <span class="debug-label">COLLECTIBLE</span>
                    <span class="debug-value">${e.isCollectible?"YES":"NO"}</span>
                </div>
            </div>
        `)};d.prototype.renderAssetsTab=function(){const t=this.container.querySelector("#assets-container");if(!t)return;if(!this.currentCard){t.innerHTML=`
            <div class="form-placeholder">
                <p class="tech-text-dim">SELECT A CARD TO EDIT</p>
            </div>
        `;return}const e=this.currentCard.artPreview||"",a=this.currentCard.artDetail||"",i=e?this.getArtAssetUrl(e):"",r=a?this.getArtAssetUrl(a):"",s=!this.currentCard.isNew,n=this.layoutMode!=="test",o=n?"assets-grid":"assets-grid assets-grid-single";t.innerHTML=`
        <div class="form-container tech-form">
            <div class="form-section tech-section">
                <h4 class="form-section-title tech-header-small">ART ASSETS</h4>
                <div class="form-grid form-grid-2 ${o}">
                    ${n?`
                        <div class="asset-panel">
                            <div class="asset-header">
                                <h4 class="tech-header-small">PREVIEW ART</h4>
                                <span class="asset-hint">Used in editor preview</span>
                            </div>
                            <div class="asset-preview${i?" has-image":""}">
                                ${i?`<img src="${i}" alt="Preview art">`:'<div class="asset-placeholder">NO PREVIEW IMAGE</div>'}
                            </div>
                            <div class="asset-actions">
                                <button type="button" class="btn-tech btn-small" data-upload-kind="preview" ${s?"":"disabled"}>UPLOAD</button>
                            </div>
                            <div class="asset-path">${e||"NO FILE"}</div>
                        </div>
                    `:""}

                    <div class="asset-panel">
                        <div class="asset-header">
                            <h4 class="tech-header-small">DETAIL ART</h4>
                            <span class="asset-hint">Used in deck detail view</span>
                        </div>
                        <div class="asset-preview${r?" has-image":""}">
                            ${r?`<img src="${r}" alt="Detail art">`:'<div class="asset-placeholder">NO DETAIL IMAGE</div>'}
                        </div>
                        <div class="asset-actions">
                            <button type="button" class="btn-tech btn-small" data-upload-kind="detail" ${s?"":"disabled"}>UPLOAD</button>
                        </div>
                        <div class="asset-path">${a||"NO FILE"}</div>
                    </div>
                </div>
            </div>
            ${s?"":'<div class="asset-note">SAVE CARD BEFORE UPLOADING ART</div>'}
        </div>
    `,s&&this.bindAssetsTabEvents({includePreview:n})};d.prototype.bindAssetsTabEvents=function(t={}){const a=t.includePreview!==!1?this.container.querySelector('[data-upload-kind="preview"]'):null,i=this.container.querySelector('[data-upload-kind="detail"]');a&&a.addEventListener("click",async()=>{this.openArtCropModal("preview")}),i&&i.addEventListener("click",async()=>{this.openArtCropModal("detail")})};d.prototype.openArtCropModal=function(t){if(document.querySelector(".card-art-crop-modal"))return;const e=t==="preview",a=848,i=1024,r=e?a/i:null,s=e?"PREVIEW ART CROP":"DETAIL ART CROP",n=e?"Preview art upload":"Detail art upload",o=document.createElement("div");o.className="card-art-crop-modal",o.innerHTML=`
        <div class="card-art-crop-card">
            <div class="card-art-crop-header">
                <div class="card-art-crop-title">${s}</div>
                <button type="button" class="card-art-crop-close">x</button>
            </div>
            <div class="card-art-crop-stage">
                <img class="card-art-crop-image" alt="Card art preview">
                <div class="card-art-crop-box">
                    <span class="card-art-crop-handle"></span>
                </div>
                <div class="card-art-crop-placeholder">
                    <div class="card-art-crop-placeholder-title">UPLOAD IMAGE</div>
                    <div class="card-art-crop-placeholder-text">Click to select an image</div>
                </div>
            </div>
            <input class="card-art-crop-file" type="file" accept="image/*">
            <div class="card-art-crop-actions">
                <button type="button" class="btn-tech btn-small card-art-crop-cancel">CANCEL</button>
                <button type="button" class="btn-tech btn-small btn-primary card-art-crop-upload">UPLOAD</button>
            </div>
            <div class="card-art-crop-status" aria-live="polite"></div>
        </div>
    `,document.body.appendChild(o);const l=o.querySelector(".card-art-crop-close"),p=o.querySelector(".card-art-crop-cancel"),h=o.querySelector(".card-art-crop-upload"),m=o.querySelector(".card-art-crop-file"),f=o.querySelector(".card-art-crop-status"),y=o.querySelector(".card-art-crop-stage"),w=o.querySelector(".card-art-crop-image"),v=o.querySelector(".card-art-crop-box"),L=o.querySelector(".card-art-crop-handle"),c={objectUrl:null,imageRect:null,naturalWidth:0,naturalHeight:0,crop:{x:0,y:0,width:0,height:0},dragging:!1,resizing:!1,startX:0,startY:0,startCropX:0,startCropY:0,startWidth:0,startHeight:0},S=(u,b=!1)=>{f&&(f.textContent=u||"",f.style.color=b?"rgba(255, 120, 120, 0.9)":"var(--text-dim)")},P=()=>{window.removeEventListener("resize",W),c.objectUrl&&URL.revokeObjectURL(c.objectUrl),o.remove()},k=()=>{P()},O=()=>{if(!y||!w)return;const u=y.getBoundingClientRect(),b=w.getBoundingClientRect();c.imageRect={x:b.left-u.left,y:b.top-u.top,width:b.width,height:b.height}},F=()=>{if(!c.imageRect)return;const u=c.imageRect.width,b=c.imageRect.height;if(r){const D=Math.min(u,b*r),g=Math.min(120,D),A=Math.min(Math.max(c.crop.width,g),D);c.crop.width=A,c.crop.height=A/r}else{const D=Math.min(120,u),g=Math.min(120,b);c.crop.width=Math.min(Math.max(c.crop.width,D),u),c.crop.height=Math.min(Math.max(c.crop.height,g),b)}const E=c.imageRect.x+c.imageRect.width-c.crop.width,$=c.imageRect.y+c.imageRect.height-c.crop.height;c.crop.x=Math.min(Math.max(c.crop.x,c.imageRect.x),E),c.crop.y=Math.min(Math.max(c.crop.y,c.imageRect.y),$)},q=()=>{v&&(v.style.left=`${c.crop.x}px`,v.style.top=`${c.crop.y}px`,v.style.width=`${c.crop.width}px`,v.style.height=`${c.crop.height}px`)},_=()=>{if(c.imageRect){if(r){let u=c.imageRect.width,b=u/r;b>c.imageRect.height&&(b=c.imageRect.height,u=b*r),c.crop.width=u,c.crop.height=b}else c.crop.width=c.imageRect.width,c.crop.height=c.imageRect.height;c.crop.x=c.imageRect.x+(c.imageRect.width-c.crop.width)/2,c.crop.y=c.imageRect.y+(c.imageRect.height-c.crop.height)/2,F(),q()}},Y=()=>{m&&(m.value="",m.click())},W=()=>{!c.imageRect||!(y!=null&&y.classList.contains("has-image"))||(O(),F(),q())};l==null||l.addEventListener("click",k),p==null||p.addEventListener("click",k),o.addEventListener("click",u=>{u.target===o&&k()}),y==null||y.addEventListener("click",()=>{y.classList.contains("has-image")||Y()}),m==null||m.addEventListener("change",()=>{var E;const u=(E=m.files)==null?void 0:E[0];if(!u)return;if(!u.type.startsWith("image/")){S("Unsupported image type.",!0);return}y==null||y.classList.remove("has-image"),v&&(v.style.display="none"),c.objectUrl&&URL.revokeObjectURL(c.objectUrl);const b=URL.createObjectURL(u);c.objectUrl=b,w&&(w.onload=()=>{c.naturalWidth=w.naturalWidth,c.naturalHeight=w.naturalHeight,requestAnimationFrame(()=>{O(),_(),S(""),y==null||y.classList.add("has-image")})},w.onerror=()=>{S("Failed to load image.",!0)},w.src=b)}),v==null||v.addEventListener("pointerdown",u=>{u.target!==L&&(u.preventDefault(),c.dragging=!0,c.startX=u.clientX,c.startY=u.clientY,c.startCropX=c.crop.x,c.startCropY=c.crop.y,v.setPointerCapture(u.pointerId),v.classList.add("dragging"))}),L==null||L.addEventListener("pointerdown",u=>{u.preventDefault(),u.stopPropagation(),c.resizing=!0,c.startX=u.clientX,c.startY=u.clientY,c.startWidth=c.crop.width,c.startHeight=c.crop.height,L.setPointerCapture(u.pointerId),v==null||v.classList.add("resizing")});const H=u=>{if(c.imageRect){if(c.dragging){const b=u.clientX-c.startX,E=u.clientY-c.startY;c.crop.x=c.startCropX+b,c.crop.y=c.startCropY+E,F(),q()}else if(c.resizing){const b=u.clientX-c.startX,E=u.clientY-c.startY;if(r){const $=Math.abs(b)>Math.abs(E)?b:E;c.crop.width=c.startWidth+$,c.crop.height=c.crop.width/r}else c.crop.width=c.startWidth+b,c.crop.height=c.startHeight+E;F(),q()}}},K=u=>{c.dragging&&v&&(c.dragging=!1,v.releasePointerCapture(u.pointerId),v.classList.remove("dragging")),c.resizing&&L&&(c.resizing=!1,L.releasePointerCapture(u.pointerId),v==null||v.classList.remove("resizing"))};y==null||y.addEventListener("pointermove",H),y==null||y.addEventListener("pointerup",K),y==null||y.addEventListener("pointerleave",K),y==null||y.addEventListener("pointercancel",K),window.addEventListener("resize",W),h==null||h.addEventListener("click",async()=>{if(!(w!=null&&w.src)||!c.imageRect||c.crop.width<=0||c.crop.height<=0){S("Select an image first.",!0);return}S(`${n}...`),h.disabled=!0;try{const u=c.naturalWidth/c.imageRect.width,b=c.naturalHeight/c.imageRect.height,E=(c.crop.x-c.imageRect.x)*u,$=(c.crop.y-c.imageRect.y)*b,D=c.crop.width*u,g=c.crop.height*b,A=Math.max(0,Math.min(c.naturalWidth-1,E)),x=Math.max(0,Math.min(c.naturalHeight-1,$)),M=c.naturalWidth-A,R=c.naturalHeight-x,N=Math.max(1,Math.min(D,M)),U=Math.max(1,Math.min(g,R)),B=document.createElement("canvas");e?(B.width=a,B.height=i):(B.width=Math.max(1,Math.round(N)),B.height=Math.max(1,Math.round(U)));const tt=B.getContext("2d");if(!tt)throw new Error("Failed to create output.");tt.drawImage(w,A,x,N,U,0,0,B.width,B.height);const et=await new Promise(it=>{if(e){B.toBlob(it,"image/webp",.9);return}B.toBlob(it,"image/png")});if(!et)throw new Error("Failed to generate output.");const ct=e?"preview.webp":"detail.png";await this.uploadCardArt(t,et,ct,{silent:!0}),S("Upload complete."),setTimeout(k,400)}catch(u){S(`Upload failed: ${u.message}`,!0)}finally{h.disabled=!1}}),v&&(v.style.display="none"),w&&w.addEventListener("load",()=>{v&&(v.style.display="block")},{once:!0})};d.prototype.uploadCardArt=async function(t,e,a,i={}){if(!this.currentCard||!this.currentCard.id)throw i.silent||alert("Save the card before uploading art."),new Error("Card must be saved before uploading art.");try{const r=new FormData;a?r.append("file",e,a):r.append("file",e);const s=t==="preview"?`/api/cards/${this.currentCard.id}/art/preview`:`/api/cards/${this.currentCard.id}/art/detail`,n=await fetch(s,{method:"POST",body:r});if(!n.ok)throw new Error(`HTTP ${n.status}: ${n.statusText}`);const o=await n.json(),l=(o==null?void 0:o.id)??(o==null?void 0:o.CardId)??(o==null?void 0:o.Id);if(l!=null){const p=h=>{if(!Array.isArray(h))return;const m=h.findIndex(f=>((f==null?void 0:f.id)??(f==null?void 0:f.CardId)??(f==null?void 0:f.Id))===l);m>=0&&(h[m]={...h[m],...o})};p(this.allCardsData),p(this.filteredCardsData)}this.artCacheBuster=Date.now(),this.currentCard=this.convertApiCardToEditorFormat(o),this.renderAssetsTab(),this.layoutMode==="test"&&await this.renderBasicInfoForm(),this.updateCardPreview()}catch(r){throw console.error("CardEditor: Failed to upload art:",r),i.silent||alert(`Upload failed: ${r.message}`),r}};d.prototype.getArtAssetUrl=function(t){if(!t)return"";const e=this.artCacheBuster;if(!e)return encodeURI(t);const a=t.includes("?")?"&":"?";return encodeURI(`${t}${a}t=${e}`)};d.prototype.renderBasicInfoForm=async function(){const t=this.container.querySelector("#basic-tab");if(!t){console.error("CardEditor: Basic tab not found");return}try{await this.ensureKeywordLabelMap();const[e,a,i]=await Promise.all([this.metadataManager.getCardTypeOptions(),this.metadataManager.getCardClassOptions(),this.metadataManager.getCardRarityOptions()]),r="",s="",n=this.layoutMode==="test"?this.renderBasicInfoFormCompact({cardTypeOptions:e,cardClassOptions:a,cardRarityOptions:i,readOnlyAttr:r,disabledAttr:s}):`
                <div class="form-container tech-form">
                    ${this.currentCard.isEvolvedForm?`
                        <div class="evolution-form-notice tech-notice">
                            <span class="evolution-notice-icon">EVOLVED</span>
                            <span class="evolution-notice-text">EDITING EVOLVED FORM</span>
                        </div>
                    `:""}
                    <div class="form-section tech-section">
                        <h4 class="form-section-title tech-header-small">BASIC ATTRIBUTES</h4>
                        <div class="form-grid form-grid-2">
                            <div class="form-group">
                                <label class="form-label required">NAME</label>
                                <input type="text" id="card-name" class="form-input tech-input" value="${this.currentCard.name}" placeholder="ENTER NAME" ${r}>
                            </div>
                            <div class="form-group">
                                <label class="form-label required">COST</label>
                                <div class="number-stepper tech-stepper">
                                    <button type="button" class="stepper-btn decrease" data-target="card-cost" ${s}>-</button>
                                    <input type="number" id="card-cost" class="form-input number-input tech-input" value="${this.currentCard.cost}" min="0" max="20" ${r}>
                                    <button type="button" class="stepper-btn increase" data-target="card-cost" ${s}>+</button>
                                </div>
                            </div>
                        </div>
                        
                        <div id="stats-container">
                            <div class="form-grid form-grid-2" style="margin-bottom: var(--space-md);">
                                <div class="form-group">
                                    <label class="form-label">ATTACK</label>
                                    <div class="number-stepper tech-stepper">
                                        <button type="button" class="stepper-btn decrease" data-target="card-attack">-</button>
                                        <input type="number" id="card-attack" class="form-input number-input tech-input" value="${this.currentCard.attack}" min="0" max="99">
                                        <button type="button" class="stepper-btn increase" data-target="card-attack">+</button>
                                    </div>
                                </div>
                                <div class="form-group">
                                    <label class="form-label">HEALTH</label>
                                    <div class="number-stepper tech-stepper">
                                        <button type="button" class="stepper-btn decrease" data-target="card-health">-</button>
                                        <input type="number" id="card-health" class="form-input number-input tech-input" value="${this.currentCard.health}" min="1" max="99">
                                        <button type="button" class="stepper-btn increase" data-target="card-health">+</button>
                                    </div>
                                </div>
                            </div>
                        </div>

                        <div id="countdown-container">
                            <div class="form-grid form-grid-2" style="margin-bottom: var(--space-md);">
                                <div class="form-group">
                                    <label class="form-label">COUNTDOWN</label>
                                    <div class="number-stepper tech-stepper">
                                        <button type="button" class="stepper-btn decrease" data-target="card-countdown">-</button>
                                        <input type="number" id="card-countdown" class="form-input number-input tech-input" value="${this.currentCard.countdown??0}" min="0" max="99">
                                        <button type="button" class="stepper-btn increase" data-target="card-countdown">+</button>
                                    </div>
                                    <div class="form-help">设置倒计时需要添加倒数关键字。</div>
                                </div>
                            </div>
                        </div>

                        <div class="form-group">
                            <label class="form-label">DESCRIPTION</label>
                            <textarea id="card-description" class="form-textarea tech-input" placeholder="ENTER DESCRIPTION" ${r}>${this.currentCard.description}</textarea>
                        </div>
                    </div>

                    <div class="form-section tech-section">
                        <h4 class="form-section-title tech-header-small">TAGS & METADATA</h4>
                        <div class="form-grid form-grid-2">
                            <div class="form-group">
                                <label class="form-label">KEYWORDS</label>
                                <div class="tag-input-container tech-tag-container">
                                    <div class="tag-list" id="keywords-list">
                                        ${this.renderTagList(this.currentCard.keywords||[],"keywords")}
                                    </div>
                                    <div class="tag-input-wrapper">
                                        <select id="keywords-select" class="tag-select tech-select">
                                            <option value="">SELECT KEYWORD</option>
                                        </select>
                                        <button type="button" class="tag-add-btn btn-tech-small" data-target="keywords">ADD</button>
                                    </div>
                                </div>
                            </div>
                            <div class="form-group">
                                <label class="form-label">TRIBE</label>
                                <div class="tag-input-container tech-tag-container">
                                    <div class="tag-list" id="tribe-list">
                                        ${this.renderTagList(this.currentCard.tribe||[],"tribe")}
                                    </div>
                                    <div class="tag-input-wrapper">
                                        <input type="text" id="tribe-input" class="tag-input tech-input" placeholder="ENTER TRIBE">
                                        <button type="button" class="tag-add-btn btn-tech-small" data-target="tribe">ADD</button>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>

                    <div class="form-section tech-section">
                        <h4 class="form-section-title tech-header-small">CLASSIFICATION</h4>
                        <div class="form-grid form-grid-3">
                            <div class="form-group">
                                <label class="form-label required">TYPE</label>
                                <div class="option-group" data-option-group="type">
                                    ${this.renderOptionButtonGroup(e,this.currentCard.cardType)}
                                </div>
                            </div>
                            <div class="form-group">
                                <label class="form-label required">CLASS</label>
                                <div class="option-group" data-option-group="class">
                                    ${this.renderOptionButtonGroup(a,this.currentCard.cardClass)}
                                </div>
                            </div>
                            <div class="form-group">
                                <label class="form-label required">RARITY</label>
                                <div class="option-group" data-option-group="rarity">
                                    ${this.renderOptionButtonGroup(i,this.currentCard.rarity)}
                                </div>
                            </div>
                            <div class="form-group">
                            <label class="form-label">SET</label>
                                <input type="text" id="card-set" class="form-input tech-input" value="${this.currentCard.cardSet||""}" placeholder="ENTER SET" list="card-set-options" ${r}>
                                <datalist id="card-set-options"></datalist>
                            </div>
                            <div class="form-group">
                                <label class="form-label">COLLECTIBLE</label>
                                <label class="form-toggle">
                                    <input type="checkbox" id="card-collectible" ${this.currentCard.isCollectible?"checked":""}>
                                    <span class="form-toggle-text" id="card-collectible-label">${this.currentCard.isCollectible?"YES":"NO"}</span>
                                </label>
                            </div>
                        </div>
                    </div>

                </div>
            `;t.innerHTML=n,this.refreshCardSetDatalist(),this.bindBasicFormEvents(),this.bindTagInputEvents(),this.bindFormValidationEvents(),this.updateStatsVisibility()}catch(e){console.error("CardEditor: Failed to render basic info form:",e),t.innerHTML=`
            <div class="error-container">
                <div class="error-icon">⚠️</div>
                <h3 class="error-title">表单加载失败</h3>
                <p class="error-message">${e.message}</p>
            </div>
        `}};d.prototype.renderCardList=function(t){const e=this.container.querySelector("#card-list");if(!e||!t)return;if(!Array.isArray(t)||t.length===0){e.innerHTML=`
            <div class="card-list-empty">
                <p>暂无卡牌数据</p>
                <small>点击"新建卡牌"开始创建</small>
            </div>
        `;return}const a=t.map(i=>{const s=this.findEvolvedFormsForCard(i).length>0;return`
            <div class="card-list-item tech-list-item" data-card-id="${i.id!==void 0?i.id:i.CardId}">
                <div class="card-item-header">
                    <span class="card-item-name tech-text-bold">${i.name||i.Name||"UNTITLED"}</span>
                    <div class="card-item-indicators">
                        ${s?'<div class="indicator-icon evolve" title="EVOLVE"></div>':""}
                        <span class="card-item-cost tech-badge">${i.cost??i.Cost??0}</span>
                    </div>
                </div>
                <div class="card-item-details">
                    <span class="card-item-type tech-tag">${T(i.type??i.Type??"UNKNOWN")}</span>
                    <span class="card-item-class tech-tag">${T(i.class??i.Class??"NEUTRAL")}</span>
                    <span class="card-item-rarity tech-tag rarity-${T(i.rarity??i.Rarity??"Common").toLowerCase()}">${T(i.rarity??i.Rarity??"COMMON")}</span>
                </div>
                ${I(T(i.type||i.Type),"Follower")?`
                    <div class="card-item-stats">
                        <span class="stat-attack">ATK ${i.attack??i.Attack??0}</span>
                        <span class="stat-health">HP ${i.health??i.Health??1}</span>
                    </div>
                `:""}
                <div class="tech-corner-decoration"></div>
            </div>
        `}).join("");if(e.innerHTML=a,this.shouldRestoreCardListScroll){const i=Math.max(0,e.scrollHeight-e.clientHeight);e.scrollTop=Math.min(this.cardListScrollTop,i),this.shouldRestoreCardListScroll=!1}this.bindCardListEvents()};d.prototype.captureCardListScroll=function(){const t=this.container.querySelector("#card-list");t&&(this.cardListScrollTop=t.scrollTop,this.shouldRestoreCardListScroll=!0)};d.prototype.highlightCardInList=function(t){this.container.querySelectorAll(".card-list-item").forEach(a=>{a.classList.remove("selected")});const e=this.container.querySelector(`[data-card-id="${t}"]`);e&&e.classList.add("selected")};d.prototype.bindCardListEvents=function(){this.container.querySelectorAll(".card-list-item").forEach(e=>{e.addEventListener("click",()=>{const a=e.dataset.cardId;this.selectCard(a)})})};d.prototype.bindBasicFormEvents=function(){const t=this.container.querySelector("#basic-tab");if(!t)return;const e=t.querySelector("#card-name"),a=t.querySelector("#card-description");e&&e.addEventListener("input",h=>{this.currentCard.name=h.target.value,this.updateCardPreview()}),a&&a.addEventListener("input",h=>{this.currentCard.description=h.target.value,this.updateCardPreview()});const i=t.querySelector("#card-set");i&&i.addEventListener("input",h=>{this.updateCardProperty("set",h.target.value)}),t.querySelectorAll(".stepper-btn").forEach(h=>{h.addEventListener("click",m=>{const f=m.target.dataset.target,y=t.querySelector(`#${f}`);if(!y)return;const w=m.target.classList.contains("increase"),v=parseInt(y.value)||0,L=parseInt(y.min)||0,c=parseInt(y.max)||99;let S=w?v+1:v-1;S=Math.max(L,Math.min(c,S)),y.value=S,this.updateCardProperty(f.replace("card-",""),S)})}),t.querySelectorAll('input[type="number"]').forEach(h=>{h.addEventListener("input",m=>{const f=m.target.id.replace("card-",""),y=parseInt(m.target.value)||0;this.updateCardProperty(f,y)})}),t.querySelectorAll("select").forEach(h=>{h.addEventListener("change",m=>{const f=m.target.id.replace("card-","");this.updateCardProperty(f,m.target.value),f==="type"&&this.updateStatsVisibility()})}),t.querySelectorAll("[data-option-group]").forEach(h=>{h.addEventListener("click",m=>{const f=m.target.closest(".option-chip");if(!f)return;const y=h.dataset.optionGroup,w=f.dataset.optionValue;!y||w===void 0||(h.querySelectorAll(".option-chip").forEach(v=>{v.classList.toggle("is-selected",v===f)}),this.updateCardProperty(y,w),y==="type"&&this.updateStatsVisibility(),this.validateForm())})});const l=t.querySelector("#card-collectible");l&&l.addEventListener("change",h=>{const m=!!h.target.checked;this.updateCardProperty("isCollectible",m);const f=t.querySelector("#card-collectible-label");f&&(f.textContent=m?"YES":"NO")});const p=t.querySelector('[data-upload-kind="preview"]');p&&p.addEventListener("click",()=>{this.openArtCropModal("preview")})};d.prototype.updateCardProperty=function(t,e){if(!this.currentCard)return;const i={name:"name",description:"description",cost:"cost",attack:"attack",health:"health",countdown:"countdown",type:"cardType",class:"cardClass",rarity:"rarity",set:"cardSet",isCollectible:"isCollectible"}[t]||t;this.currentCard[i]=e,this.updateCardPreview()};d.prototype.updateStatsVisibility=function(){const t=this.container.querySelector("#stats-container"),e=this.container.querySelector("#countdown-container");if(!t||!e||!this.currentCard)return;const a=T(this.currentCard.cardType),i=I(a,"Follower"),r=I(a,"Amulet")||I(a,"Crest"),s=this.layoutMode==="test"?"flex":"block";t.style.display=i?s:"none",e.style.display=r?s:"none"};d.prototype.buildCardUpsertDto=function(t){var n;if(!this.currentCard)return null;const e=T(this.currentCard.cardType),a=I(e,"Follower"),i=I(e,"Amulet")||I(e,"Crest"),r=(this.currentCard.keywords||[]).some(o=>I(o,"Countdown")),s=(this.currentCard.cardSet||"").trim();return{id:t?null:this.currentCard.id,name:((n=this.currentCard.name)==null?void 0:n.trim())||"",type:e,rarity:T(this.currentCard.rarity),card_set:s||"特殊",class:T(this.currentCard.cardClass),cost:Number(this.currentCard.cost)||0,attack:a?Number(this.currentCard.attack)||0:null,health:a?Number(this.currentCard.health)||1:null,countdown:i&&r?Number(this.currentCard.countdown)||0:null,description:this.currentCard.description||"",artPreview:this.currentCard.artPreview||null,artDetail:this.currentCard.artDetail||null,isCollectible:this.currentCard.isCollectible??!0,can_evolve:!!this.currentCard.canEvolve,evolve_id:this.currentCard.evolveId??null,keywords:Array.isArray(this.currentCard.keywords)?this.currentCard.keywords:[],tribe:Array.isArray(this.currentCard.tribe)?this.currentCard.tribe:[]}};d.prototype.isKnownCardSetName=function(t){const e=(t||"").trim();if(!e)return!0;const a=Array.isArray(this.cardSetDefinitions)?this.cardSetDefinitions:[];if(a.length===0)return!0;const i=e.toLowerCase();return a.some(r=>{const s=(r==null?void 0:r.name)??(r==null?void 0:r.Name)??"";return s?String(s).trim().toLowerCase()===i:!1})};d.prototype.saveCurrentCard=async function(t){var n,o;if(!this.currentCard){alert("请先选择或创建一张卡牌");return}if(!this.validateForm()){alert("请先修正表单错误后再保存");return}const e=(this.currentCard.cardSet||"").trim();if(e&&!this.isKnownCardSetName(e)&&!confirm(`卡包 "${e}" 不存在，保存后将自动注册为新卡包。是否继续？`))return;const a=!!this.currentCard.isNew||!!t,i=this.buildCardUpsertDto(a),r=((n=this.container)==null?void 0:n.querySelector(".card-editor-layout"))||this.container,s=r?j(r):null;s==null||s.show("正在保存卡牌...");try{const l=a?"/api/cards":`/api/cards/${this.currentCard.id}`,h=await fetch(l,{method:a?"POST":"PUT",headers:{"Content-Type":"application/json"},body:JSON.stringify(i)});if(!h.ok)throw new Error(`HTTP ${h.status}: ${h.statusText}`);this.applyDataRevisionFromResponse(h);const m=await h.json(),f=(m==null?void 0:m.id)??(m==null?void 0:m.cardId)??(m==null?void 0:m.CardId);if(f!=null){this.currentCard=this.convertApiCardToEditorFormat(m),this.currentCard.isNew=!1,this.updateTabAvailability(),this.upsertCardInCache(m);const y=this.container.querySelector("#editor-title");y&&(y.textContent=`编辑卡牌: ${this.currentCard.name}`),await this.renderBasicInfoForm(),this.updateCardPreview();const w=(o=this.container.querySelector(".tab-button.active"))==null?void 0:o.dataset.tab;w==="abilities"?await this.renderAbilitiesList():w==="assets"&&this.renderAssetsTab()}await this.loadCardList(),await this.loadCardSetDefinitions(),await this.refreshAbilityDataCacheAfterSave(),f!=null&&this.highlightCardInList(f),alert(a?"卡牌创建成功！":"卡牌保存成功！")}catch(l){console.error("CardEditor: Failed to save card:",l),alert(`保存失败: ${l.message}`)}finally{s==null||s.hide()}};d.prototype.deleteCurrentCard=async function(){if(!(!this.currentCard||this.currentCard.isNew)&&confirm(`确定要删除卡牌 "${this.currentCard.name}" 吗？此操作不可撤销。`))try{const t=this.currentCard.id,e=await fetch(`/api/cards/${this.currentCard.id}`,{method:"DELETE"});if(!e.ok&&e.status!==204)throw new Error(`HTTP ${e.status}: ${e.statusText}`);this.applyDataRevisionFromResponse(e),this.removeCardFromCache(t),this.currentCard=null,this.disableEditorButtons(),this.updateTabAvailability();const a=this.container.querySelector("#editor-title");a&&(a.textContent="AWAITING SELECTION");const i=this.container.querySelector("#basic-tab");i&&(i.innerHTML=`
                <div class="form-placeholder">
                    <p class="tech-text-dim">SELECT A CARD TO EDIT</p>
                </div>
            `);const r=this.container.querySelector("#card-preview");r&&(r.innerHTML=`
                <div class="preview-placeholder">
                    <p class="tech-text-dim">AWAITING DATA</p>
                </div>
            `),await this.loadCardList(),alert("卡牌已删除")}catch(t){console.error("CardEditor: Failed to delete card:",t),alert(`删除失败: ${t.message}`)}};d.prototype.loadCardList=async function(t={}){const e=this.container.querySelector("#card-list");if(!e){console.error("CardEditor: Card list container not found");return}try{this.captureCardListScroll(),e.innerHTML=`
            <div class="card-list-loading">
                <div class="loading-spinner"></div>
                <p>正在加载卡牌列表...</p>
            </div>
        `;const a=await vt({useCache:!t.force,force:!!t.force,getCached:()=>this.cardListCache,setCached:r=>{this.cardListCache=r},onResponse:r=>{this.applyDataRevisionFromResponse(r)}}),i=this.filterCardsForList(a);this.allCardsData=a,this.filteredCardsData=i,this.populateCardSetFilterOptions(this.filteredCardsData),this.applyCardListFilters()}catch(a){console.error("CardEditor: Failed to load card list:",a),e.innerHTML=`
            <div class="card-list-error">
                <div class="error-icon">⚠️</div>
                <p>加载卡牌列表失败</p>
                <small>${a.message}</small>
                <button class="retry-btn" onclick="cardEditorInstance.loadCardList({ force: true })">重试</button>
            </div>
        `}};d.prototype.filterCardsForList=function(t){return Array.isArray(t)?t.filter(e=>{const a=e.id!==void 0?e.id:e.CardId,i=e.type??e.Type??"",r=T(i),s=T(e.rarity??e.Rarity??""),n=e.name??e.Name??"";return!(I(r,"Leader")||i===0||a===0||I(n,"主战者")||I(s,"Special"))}):[]};d.prototype.findEvolvedFormsForCard=function(t){if(!this.allCardsData||!Array.isArray(this.allCardsData))return[];const e=t.evolve_id||t.EvolveId||t.evolveId;return e?this.allCardsData.filter(i=>(i.id!==void 0?i.id:i.CardId)===e):[]};d.prototype.selectCard=async function(t){var e,a,i;try{const r=((e=this.container)==null?void 0:e.querySelector(".card-editor-layout"))||this.container,s=r?j(r):null;s==null||s.show("正在加载卡牌信息..."),this.container.querySelectorAll(".card-list-item").forEach(m=>{m.classList.remove("selected")});const n=this.container.querySelector(`[data-card-id="${t}"]`);n&&n.classList.add("selected");const o=this.getCardIdKey(t);let l=o?this.cardDetailCache.get(o):null;if(!l){const m=await fetch(`/api/cards/${t}`);if(!m.ok)throw new Error(`HTTP ${m.status}: ${m.statusText}`);this.applyDataRevisionFromResponse(m),l=await m.json(),o&&this.cardDetailCache.set(o,l)}this.currentCard=this.convertApiCardToEditorFormat(l),this.updateTabAvailability();const p=this.container.querySelector("#editor-title");p&&(p.textContent=`编辑卡牌: ${this.currentCard.name}`),this.enableEditorButtons(),await this.renderBasicInfoForm();const h=(a=this.container.querySelector(".tab-button.active"))==null?void 0:a.dataset.tab;h==="abilities"&&await this.renderAbilitiesList(),h==="assets"&&this.renderAssetsTab(),this.updateCardPreview(),this.loadEvolvedForms()}catch(r){console.error("CardEditor: Failed to select card:",r),alert(`加载卡牌失败: ${r.message}`)}finally{const r=((i=this.container)==null?void 0:i.querySelector(".card-editor-layout"))||this.container,s=r?j(r):null;s==null||s.hide()}};d.prototype.updateTabAvailability=function(){const t=this.container.querySelector('.tab-button[data-tab="abilities"]'),e=this.container.querySelector('.tab-button[data-tab="assets"]'),a=!!this.currentCard&&!this.currentCard.isNew;if([t,e].forEach(i=>{i&&(i.disabled=!a,i.classList.toggle("is-disabled",!a))}),!a){const i=this.container.querySelector(".tab-button.active");i&&i.dataset.tab!=="basic"&&this.switchTab("basic")}};d.prototype.loadEvolvedForms=function(){if(!this.currentCard)return;const t=this.findEvolvedFormsForCard(this.currentCard);this.currentCardEvolvedForms=t,this.currentEvolvedFormIndex=-1,this.updateEvolvedFormsNavigation()};d.prototype.updateEvolvedFormsNavigation=function(){let t=this.container.querySelector("#evolution-navigation");if(!this.currentCardEvolvedForms||this.currentCardEvolvedForms.length===0){t&&(t.style.display="none");return}if(!t){const e=this.container.querySelector(".editor-main");e&&(e.insertAdjacentHTML("afterbegin",`
                <div id="evolution-navigation" class="evolution-navigation">
                    <div class="evolution-nav-header">
                        <span class="evolution-nav-title">卡牌形态</span>
                        <div class="evolution-nav-controls">
                            <button id="prev-evolution" class="evolution-nav-btn" title="上一个形态">◀</button>
                            <span id="evolution-indicator" class="evolution-indicator-text">原始形态</span>
                            <button id="next-evolution" class="evolution-nav-btn" title="下一个形态">▶</button>
                        </div>
                    </div>
                </div>
            `),t=this.container.querySelector("#evolution-navigation"),this.bindEvolutionNavigationEvents())}t&&(t.style.display="block",this.updateEvolutionIndicator())};d.prototype.bindEvolutionNavigationEvents=function(){const t=this.container.querySelector("#prev-evolution"),e=this.container.querySelector("#next-evolution");t&&t.addEventListener("click",()=>{this.showPreviousEvolution()}),e&&e.addEventListener("click",()=>{this.showNextEvolution()})};d.prototype.showPreviousEvolution=function(){if(!this.currentCardEvolvedForms)return;const t=this.currentCardEvolvedForms.length+1;this.currentEvolvedFormIndex=(this.currentEvolvedFormIndex-1+t)%t-1,this.switchToEvolutionForm()};d.prototype.showNextEvolution=function(){if(!this.currentCardEvolvedForms)return;const t=this.currentCardEvolvedForms.length+1;this.currentEvolvedFormIndex=(this.currentEvolvedFormIndex+1)%t,this.currentEvolvedFormIndex>=this.currentCardEvolvedForms.length&&(this.currentEvolvedFormIndex=-1),this.switchToEvolutionForm()};d.prototype.switchToEvolutionForm=function(){if(this.currentEvolvedFormIndex===-1)this.loadOriginalCardForm();else{const t=this.currentCardEvolvedForms[this.currentEvolvedFormIndex];this.loadEvolvedCardForm(t)}this.updateEvolutionIndicator()};d.prototype.loadOriginalCardForm=async function(){try{const t=await fetch(`/api/cards/${this.currentCard.id}`);if(!t.ok)throw new Error(`HTTP ${t.status}: ${t.statusText}`);const e=await t.json();this.currentCard=this.convertApiCardToEditorFormat(e),await this.renderBasicInfoForm(),this.updateCardPreview()}catch(t){console.error("CardEditor: Failed to load original card form:",t),alert(`加载原始卡牌失败: ${t.message}`)}};d.prototype.loadEvolvedCardForm=function(t){this.currentCard=this.convertApiCardToEditorFormat(t),this.currentCard.isEvolvedForm=!0,this.renderBasicInfoForm(),this.updateCardPreview()};d.prototype.updateEvolutionIndicator=function(){const t=this.container.querySelector("#evolution-indicator");if(t)if(this.currentEvolvedFormIndex===-1)t.textContent="原始形态";else{const e=this.currentCardEvolvedForms[this.currentEvolvedFormIndex],a=e.name||e.Name||"进化形态";t.textContent=a}};d.prototype.convertApiCardToEditorFormat=function(t){const e=t.data||t;return{id:e.id!==void 0?e.id:e.CardId,name:e.name??e.Name??"",description:e.description??e.Description??"",artPreview:e.artPreview??e.ArtPreview??null,artDetail:e.artDetail??e.ArtDetail??null,cost:e.cost??e.Cost??0,attack:e.attack??e.Attack??0,health:e.health??e.Health??1,countdown:e.countdown??e.Countdown??0,cardType:T(e.type??e.Type??"Follower"),cardClass:T(e.class??e.Class??"Neutral"),rarity:T(e.rarity??e.Rarity??"Bronze"),cardSet:Q(e).name||"特殊",keywords:e.keywords||e.Keywords||[],tribe:e.tribe||e.Tribe||[],canEvolve:e.canEvolve??e.CanEvolve??!1,evolveId:e.evolve_id??e.evolveId??e.EvolveId??null,isCollectible:e.isCollectible??e.IsCollectible??!0,isNew:!1}};d.prototype.ensureKeywordLabelMap=async function(){if(this.keywordLabelMap)return this.keywordLabelMap;try{this.keywordLabelMap=await this.metadataManager.getEnum("CardKeyword")}catch(t){console.warn("CardEditor: Failed to load keyword labels:",t),this.keywordLabelMap={}}return this.keywordLabelMap};d.prototype.getTagLabel=function(t,e){if(e!=="keywords"||!this.keywordLabelMap||!t)return t;if(this.keywordLabelMap[t])return this.keywordLabelMap[t];const a=String(t).toLowerCase(),i=Object.keys(this.keywordLabelMap).find(r=>r.toLowerCase()===a);return i?this.keywordLabelMap[i]:t};d.prototype.renderTagList=function(t,e){return!Array.isArray(t)||t.length===0?'<div class="tag-list-empty">暂无标签</div>':t.map(a=>`
        <div class="tag-item" data-tag="${a}" data-type="${e}">
            <span class="tag-text">${this.getTagLabel(a,e)}</span>
            <button type="button" class="tag-remove-btn" data-tag="${a}" data-type="${e}">×</button>
        </div>
    `).join("")};d.prototype.bindTagInputEvents=function(){const t=this.container.querySelector("#basic-tab");if(!t)return;const e=t.querySelector("#keywords-select"),a=t.querySelector('[data-target="keywords"]');e&&(this.loadKeywordOptions(e),e.addEventListener("change",s=>{const n=s.target.value.trim();n&&(this.addTag("keywords",n),s.target.value="")})),a&&a.addEventListener("click",()=>{const s=e.value.trim();s&&(this.addTag("keywords",s),e.value="")});const i=t.querySelector("#tribe-input"),r=t.querySelector('[data-target="tribe"]');i&&i.addEventListener("keypress",s=>{s.key==="Enter"&&(s.preventDefault(),this.addTag("tribe",i.value.trim()),i.value="")}),r&&r.addEventListener("click",()=>{const s=i.value.trim();s&&(this.addTag("tribe",s),i.value="")}),t.addEventListener("click",s=>{if(s.target.classList.contains("tag-remove-btn")){const n=s.target.dataset.tag,o=s.target.dataset.type;this.removeTag(o,n)}})};d.prototype.addTag=function(t,e){if(!(!this.currentCard||!e)){if(this.currentCard[t]||(this.currentCard[t]=[]),this.currentCard[t].includes(e)){alert(`标签 "${e}" 已存在`);return}this.currentCard[t].push(e),this.updateTagList(t),this.updateCardPreview()}};d.prototype.removeTag=function(t,e){if(!this.currentCard||!this.currentCard[t])return;const a=this.currentCard[t].indexOf(e);a>-1&&(this.currentCard[t].splice(a,1),this.updateTagList(t),this.updateCardPreview())};d.prototype.updateTagList=function(t){const e=this.container.querySelector(`#${t}-list`);if(!e)return;const a=this.currentCard[t]||[];e.innerHTML=this.renderTagList(a,t)};d.prototype.bindFormValidationEvents=function(){const t=this.container.querySelector("#basic-tab");if(!t)return;const e=t.querySelector("#card-name"),a=t.querySelector("#card-cost"),i=t.querySelector("#card-attack"),r=t.querySelector("#card-health"),s=t.querySelector("#card-countdown"),n=t.querySelector("#card-type"),o=t.querySelector("#card-class"),l=t.querySelector("#card-rarity");[e,a,i,r,s,n,o,l].forEach(h=>{h&&(h.addEventListener("input",()=>this.validateForm()),h.addEventListener("change",()=>this.validateForm()))}),t.querySelectorAll("[data-option-group]").forEach(h=>{h.addEventListener("click",()=>this.validateForm())}),this.validateForm()};d.prototype.validateForm=function(){if(!this.currentCard)return;const t=[];(!this.currentCard.name||this.currentCard.name.trim()==="")&&t.push("卡牌名称不能为空"),this.currentCard.cardType||t.push("请选择卡牌类型"),this.currentCard.cardClass||t.push("请选择卡牌职业"),this.currentCard.rarity||t.push("请选择卡牌稀有度"),(this.currentCard.cost<0||this.currentCard.cost>20)&&t.push("费用必须在0-20之间");const e=T(this.currentCard.cardType),a=(this.currentCard.keywords||[]).some(i=>I(i,"Countdown"));return I(e,"Follower")?((this.currentCard.attack<0||this.currentCard.attack>99)&&t.push("攻击力必须在0-99之间"),(this.currentCard.health<1||this.currentCard.health>99)&&t.push("生命值必须在1-99之间")):(I(e,"Amulet")||I(e,"Crest"))&&a&&(this.currentCard.countdown<0||this.currentCard.countdown>99)&&t.push("倒计时必须在0-99之间"),this.updateSaveButtonState(t.length===0),this.displayValidationErrors(t),t.length===0};d.prototype.updateSaveButtonState=function(t){const e=this.container.querySelector("#save-btn"),a=this.container.querySelector("#save-as-btn");e&&(e.disabled=!t||!this.currentCard,e.title=t?"保存卡牌":"请修正表单错误后再保存"),a&&(a.disabled=!t||!this.currentCard,a.title=t?"另存为新卡牌":"请修正表单错误后再保存")};d.prototype.displayValidationErrors=function(t){if(this.container.querySelectorAll(".validation-error").forEach(i=>i.remove()),t.length===0)return;const a=this.container.querySelector("#basic-tab .form-container");if(a){const i=document.createElement("div");i.className="validation-error",i.innerHTML=`
            <div class="error-header">
                <span class="error-icon">⚠️</span>
                <span class="error-title">表单验证失败</span>
            </div>
            <ul class="error-list">
                ${t.map(r=>`<li>${r}</li>`).join("")}
            </ul>
        `,a.insertBefore(i,a.firstChild)}};d.prototype.renderAbilitiesList=async function(t={}){const e=this.container.querySelector("#abilities-list");if(!(!e||!this.currentCard))try{const a=this.getCardIdKey(this.currentCard.id),r=!t.force&&a?this.abilitiesByCardCache.get(a):null;if(Array.isArray(r)){this.renderAbilitiesListContent(r);return}e.innerHTML=`
            <div class="abilities-loading">
                <div class="loading-spinner"></div>
                <p>正在加载能力列表...</p>
            </div>
        `;const s=await fetch(`/api/cards/${this.currentCard.id}/abilities`);if(!s.ok)throw new Error(`HTTP ${s.status}: ${s.statusText}`);this.applyDataRevisionFromResponse(s);const n=await s.json();a&&this.abilitiesByCardCache.set(a,Array.isArray(n)?n:[]),this.renderAbilitiesListContent(n)}catch(a){console.error("CardEditor: Failed to load abilities:",a),e.innerHTML=`
            <div class="abilities-error">
                <div class="error-icon">⚠️</div>
                <p>加载能力列表失败</p>
                <small>${a.message}</small>
                <button class="retry-btn" onclick="cardEditorInstance.renderAbilitiesList({ force: true })">重试</button>
            </div>
        `}};d.prototype.renderAbilitiesListContent=function(t){var s,n,o;const e=this.container.querySelector("#abilities-list");if(!e)return;this.currentCardAbilities=Array.isArray(t)?t:[],Array.isArray(t)&&t.forEach(l=>{const p=this.getAbilityIdKey((l==null?void 0:l.id)??(l==null?void 0:l.Id));p&&this.abilityDetailCache.set(p,l)});const a=this.getCardIdKey((s=this.currentCard)==null?void 0:s.id);if(a&&this.abilityCountsCache.set(a,this.currentCardAbilities.length),!Array.isArray(t)||t.length===0){e.innerHTML=`
            <div class="abilities-placeholder">
                <p>暂无关联能力</p>
                <small>点击"添加能力"开始配置</small>
            </div>
        `;return}let i=new Map;try{i=Pt(((o=(n=this.metadataManager)==null?void 0:n.getEvents)==null?void 0:o.call(n))??[])}catch{i=new Map}const r=t.map(l=>{const p=this.parseAbilityConfig(l),h=qt(l,p),f=Ht(p,h,i).map(v=>v?`<span class="ability-tag tech-tag">${v}</span>`:"").filter(Boolean).join(""),w=V(p).map(v=>{const L=Rt(v);return L?`<span class="config-item tech-tag">${L}</span>`:""}).filter(Boolean).join("");return`
        <div class="ability-item tech-panel-item" data-ability-id="${l.id}">
            <div class="ability-header">
                <div class="ability-info">
                    <span class="ability-name tech-text-bold">${l.name||"未命名能力"}</span>
                    ${f}
                </div>
                <div class="ability-actions">
                    <button class="btn-tech-small edit-ability-btn" data-ability-id="${l.id}">EDIT</button>
                    <button class="btn-tech-small btn-danger remove-ability-btn" data-ability-id="${l.id}">DELETE</button>
                </div>
            </div>
            <div class="ability-details">
                <div class="ability-description tech-text-dim">${l.description||"暂无描述"}</div>
                <div class="ability-config">
                    <span class="config-item tech-tag">TYPE: ${h}</span>
                    ${w}
                    <span class="config-item tech-tag ${l.isActive?"status-active":"status-inactive"}">
                        ${l.isActive?"ACTIVE":"INACTIVE"}
                    </span>
                </div>
            </div>
        </div>
    `}).join("");e.innerHTML=r,this.bindAbilityItemEvents()};d.prototype.bindAbilityItemEvents=function(){const t=this.container.querySelector("#abilities-list");if(!t)return;t.querySelectorAll(".edit-ability-btn").forEach(r=>{r.addEventListener("click",s=>{s.stopPropagation();const n=r.dataset.abilityId;this.editAbility(n)})}),t.querySelectorAll(".remove-ability-btn").forEach(r=>{r.addEventListener("click",s=>{s.stopPropagation();const n=r.dataset.abilityId;this.removeAbility(n)})}),t.querySelectorAll(".ability-item").forEach(r=>{r.addEventListener("click",()=>{r.classList.toggle("expanded")})})};d.prototype.showAddAbilityModal=async function(){if(!this.currentCard){alert("请先选择一张卡牌");return}try{this.showAddAbilityTypeModal()}catch(t){console.error("CardEditor: Failed to show add ability modal:",t),alert(`打开添加能力窗口失败: ${t.message}`)}};d.prototype.showAddAbilityTypeModal=function(){document.body.insertAdjacentHTML("beforeend",`
        <div class="modal-overlay" id="add-ability-type-modal">
            <div class="modal-container ability-edit-container ability-type-modal">
                <div class="modal-header">
                    <h3>选择能力类型</h3>
                    <button class="modal-close-btn" id="close-add-ability-type-modal">×</button>
                </div>
                <div class="modal-content">
                    <div class="ability-mode-selector">
                        <div class="btn-group-row" id="add-ability-mode-options">
                            <button type="button" class="btn-option" data-mode="effect">触发式能力</button>
                            <button type="button" class="btn-option" data-mode="aura">持久式能力</button>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    `);const e=document.getElementById("add-ability-type-modal"),a=document.getElementById("close-add-ability-type-modal"),i=document.getElementById("add-ability-mode-options"),r=()=>{e&&e.remove()};a&&a.addEventListener("click",r),i&&i.addEventListener("click",async s=>{const n=s.target.closest("button[data-mode]");if(!n)return;const o=n.dataset.mode;r(),await this.showAddAbilityEditorModal(o)})};d.prototype.showAddAbilityEditorModal=async function(t){document.body.insertAdjacentHTML("beforeend",`
        <div class="modal-overlay" id="add-ability-modal">
            <div class="modal-container ability-edit-container">
                <div class="modal-header">
                    <h3>添加能力</h3>
                    <button class="modal-close-btn" id="close-add-ability-modal">×</button>
                </div>
                <div class="modal-content" id="add-ability-editor-container">
                    <!-- 能力编辑器将在这里渲染 -->
                </div>
            </div>
        </div>
    `);const a=document.getElementById("add-ability-editor-container"),{AbilityEditor:i}=await rt(async()=>{const{AbilityEditor:o}=await import("./AbilityEditor-CwU3fQSY.js");return{AbilityEditor:o}},__vite__mapDeps([0,1,2,3,4]));let r=new i;const s={cardId:this.currentCard.id,cardName:this.currentCard.name||"",abilitySequence:this.getNextAbilitySequence(),name:"",description:"",abilityKeywords:[],abilityType:1,priority:0,isActive:!0,type:t==="aura"?"Continuous":"Triggered",trigger:{eventTypes:[],relationType:"Any",timing:"On",validZones:[]},conditions:[],targets:[],effects:[],counterRegistrations:[]};await r.render(a,s,async o=>{o===null?this.closeAddAbilityModal():(await this.createNewAbilityFromConfig(o),this.closeAddAbilityModal())},{mode:t});const n=document.getElementById("close-add-ability-modal");n&&n.addEventListener("click",()=>this.closeAddAbilityModal())};d.prototype.createNewAbilityFromConfig=async function(t){try{const e={cardId:this.currentCard.id,name:t.name,description:t.description,abilityKeywords:t.abilityKeywords||[],abilityType:t.abilityType||1,priority:t.priority||0,isActive:t.isActive,jsonConfig:t.jsonConfig},a=await fetch("/api/abilities",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify(e)});if(!a.ok)throw new Error(`HTTP ${a.status}: ${a.statusText}`);this.applyDataRevisionFromResponse(a);const i=await a.json();this.invalidateAbilitiesForCard(this.currentCard.id),this.invalidateAbilityLibraryCache();const r=this.getAbilityIdKey((i==null?void 0:i.id)??(i==null?void 0:i.Id));r&&this.abilityDetailCache.set(r,i),await this.renderAbilitiesList(),await this.refreshAbilityDataCacheAfterSave(),alert("能力添加成功！")}catch(e){console.error("CardEditor: Failed to create ability:",e),alert(`添加能力失败: ${e.message}`)}};d.prototype.bindAddAbilityModalEvents=function(){const t=document.getElementById("add-ability-modal");if(!t)return;const e=t.querySelector("#close-add-ability-modal"),a=t.querySelector("#cancel-add-ability");[e,a].forEach(s=>{s&&s.addEventListener("click",()=>this.closeAddAbilityModal())}),t.querySelectorAll(".modal-tab-btn").forEach(s=>{s.addEventListener("click",()=>{const n=s.dataset.tab;this.switchAddAbilityTab(n)})});const r=t.querySelector("#confirm-add-ability");r&&r.addEventListener("click",async()=>{r.disabled=!0,r.textContent="PROCESSING...";try{await this.confirmAddAbility()}catch(s){console.error("Add ability failed:",s)}finally{r.disabled=!1,r.textContent="确认添加"}}),this.loadExistingAbilities()};d.prototype.closeAddAbilityModal=function(){const t=document.getElementById("add-ability-modal");t&&t.remove()};d.prototype.getNextAbilitySequence=function(){var r;const t=(((r=this.currentCard)==null?void 0:r.name)||"").trim(),e=Array.isArray(this.currentCardAbilities)?this.currentCardAbilities:[];if(!t)return e.length+1;const a=new RegExp(`^${xt(t)}(\\d+)$`);let i=0;return e.forEach(s=>{const o=((s==null?void 0:s.name)??(s==null?void 0:s.Name)??"").toString().trim().match(a);if(!o)return;const l=parseInt(o[1],10);!Number.isNaN(l)&&l>i&&(i=l)}),i+1};d.prototype.showLinkExistingAbilityModal=async function(){if(!this.currentCard||!this.currentCard.id){alert("请先选择并保存一张卡牌");return}try{document.body.insertAdjacentHTML("beforeend",`
            <div class="modal-overlay" id="link-ability-modal">
                <div class="modal-container ability-edit-container ability-copy-container">
                    <div class="modal-header">
                        <h3>复制现有能力</h3>
                        <button class="modal-close-btn" id="close-link-ability-modal">×</button>
                    </div>
                    <div class="modal-content ability-copy-modal">
                        <div class="ability-copy-filters">
                            <div class="ability-copy-search">
                                <label class="form-label" for="ability-copy-search">筛选</label>
                                <input class="tech-input" type="search" id="ability-copy-search" placeholder="搜索卡牌名称或ID">
                            </div>
                            <div class="ability-copy-stats tech-text-dim" id="ability-copy-stats">卡牌 0 / 能力 0</div>
                        </div>
                        <div class="tech-text-dim ability-copy-hint">点击卡牌展开能力后选择复制</div>
                        <div class="ability-copy-list custom-scrollbar" id="link-abilities-list">
                            <div class="loading-spinner"></div>
                            <p>正在加载卡牌列表...</p>
                        </div>
                        <div class="ability-copy-pagination">
                            <button class="btn-tech btn-secondary btn-small" id="ability-copy-prev" disabled>上一页</button>
                            <div class="ability-copy-page-info tech-text-dim" id="ability-copy-page-info">1 / 1</div>
                            <button class="btn-tech btn-secondary btn-small" id="ability-copy-next" disabled>下一页</button>
                        </div>
                        <div class="modal-actions ability-copy-actions">
                            <button class="btn-tech btn-secondary" id="cancel-link-ability">取消</button>
                            <button class="btn-tech btn-primary" id="confirm-link-ability" disabled>确认复制</button>
                        </div>
                    </div>
                </div>
            </div>
        `);const e=document.getElementById("link-ability-modal"),a=e==null?void 0:e.querySelector(".ability-copy-container"),i=a?j(a):null,r=document.getElementById("close-link-ability-modal"),s=document.getElementById("cancel-link-ability"),n=document.getElementById("confirm-link-ability"),o=document.getElementById("link-abilities-list"),l=()=>{i==null||i.hide(),this.closeLinkExistingAbilityModal()};r&&r.addEventListener("click",l),s&&s.addEventListener("click",l),e&&e.addEventListener("click",u=>{u.target===e&&l()});const p=document.getElementById("ability-copy-search"),h=document.getElementById("ability-copy-stats"),m=document.getElementById("ability-copy-page-info"),f=document.getElementById("ability-copy-prev"),y=document.getElementById("ability-copy-next"),w=new Set,v=12;let L=1,c=[],S=[];this.selectedExistingAbilityId=null;const P=u=>u==null?null:String(u);let k=Array.isArray(this.cardListCache)?this.cardListCache:null;(!Array.isArray(k)||k.length===0)&&(k=Array.isArray(this.allCardsData)?this.allCardsData:null),i==null||i.show("正在加载能力库...");try{const u=await st();k=Array.isArray(u.cards)?u.cards:[],this.cardListCache=Array.isArray(k)?k:[],this.allCardsData=this.cardListCache;const b=new Map,E=this.abilityCountsCache;E.clear(),this.cardListCache.forEach($=>{var M,R;const D=($==null?void 0:$.id)??($==null?void 0:$.CardId),g=P(D);if(!g)return;const A=((R=(M=u.abilitiesByCard)==null?void 0:M.get)==null?void 0:R.call(M,Number(D)))||[],x=Array.isArray(A)?A:[];b.set(g,x),E.set(g,x.length),x.forEach(N=>{const U=this.getAbilityIdKey((N==null?void 0:N.id)??(N==null?void 0:N.Id));U&&this.abilityDetailCache.set(U,N)})}),this.abilitiesByCardCache=b}catch{i==null||i.hide(),o.innerHTML='<div class="abilities-placeholder"><p>加载能力失败</p></div>';return}finally{i==null||i.hide()}const O=this.abilitiesByCardCache,F=this.abilityCountsCache;if(!Array.isArray(k)||k.length===0){o.innerHTML='<div class="abilities-placeholder"><p>暂无可用卡牌</p></div>';return}this.allCardsData=Array.isArray(k)?k:[],c=this.filterCardsForList(k).map(u=>{const b=P((u==null?void 0:u.id)??(u==null?void 0:u.CardId));if(!b)return null;const E=(u.name??u.Name??`Card ${b}`).toString().trim()||`Card ${b}`,$=u.cost??u.Cost??0,D=T(u.type??u.Type??"UNKNOWN"),g=T(u.class??u.Class??"NEUTRAL"),A=T(u.rarity??u.Rarity??"COMMON"),x=O.get(b)||[],M=Array.isArray(x)?x.map(N=>((N==null?void 0:N.name)??(N==null?void 0:N.Name)??"").toString().trim()).join(" "):"",R=[E,b,$,D,g,A,M].join(" ").toLowerCase();return{cardId:b,cardName:E,cardCost:$,cardType:D,cardClass:g,cardRarity:A,cardSearchText:R}}).filter(Boolean).sort((u,b)=>u.cardName.localeCompare(b.cardName,void 0,{numeric:!0,sensitivity:"base"}));const _=(u,b)=>{h&&(h.textContent=`卡牌 ${u} / 能力 ${b}`)},Y=u=>{const b=Math.max(u,1);m&&(m.textContent=`${L} / ${b}`),f&&(f.disabled=L<=1),y&&(y.disabled=L>=b)},W=u=>{const b=this.selectedExistingAbilityId?String(this.selectedExistingAbilityId):null;return u.map(E=>{const $=String(E.id??""),D=(E==null?void 0:E.name)??(E==null?void 0:E.Name)??"未命名能力",g=(E==null?void 0:E.description)??(E==null?void 0:E.Description)??"",A=nt(E);return`
                    <button type="button" class="ability-copy-ability${b===$?" selected":""}" data-ability-id="${$}">
                        <div class="ability-copy-ability-header">
                            <span class="ability-copy-ability-name tech-text-bold">${D}</span>
                            ${A?`<span class="ability-copy-ability-tag tech-tag">${A}</span>`:""}
                        </div>
                        ${g?`<div class="ability-copy-ability-desc tech-text-dim">${g}</div>`:""}
                    </button>
                `}).join("")},H=()=>{const u=Math.max(1,Math.ceil(S.length/v));if(L=Math.min(L,u),S.length===0){o.innerHTML='<div class="abilities-placeholder"><p>没有匹配的卡牌</p></div>',_(0,0),Y(0);return}const b=(L-1)*v,E=S.slice(b,b+v),$=g=>{const A=F.get(g);if(Number.isFinite(A))return A;const x=O.get(g);return Array.isArray(x)?x.length:0},D=S.reduce((g,A)=>g+$(A.cardId),0);o.innerHTML=E.map(g=>{const A=O.get(g.cardId),x=Array.isArray(A)?A:[],M=$(g.cardId),R=w.has(g.cardId);let N="";return R&&(x.length===0?N='<div class="abilities-placeholder"><p>暂无可复制能力</p></div>':N=`<div class="ability-copy-abilities">${W(x)}</div>`),`
                    <div class="ability-copy-card${R?" expanded":""}" data-card-id="${g.cardId}">
                        <button type="button" class="ability-copy-card-header" aria-expanded="${R}">
                            <div class="ability-copy-card-main">
                                <span class="ability-copy-card-name tech-text-bold">${g.cardName}</span>
                                <span class="ability-copy-card-id tech-text-dim">#${g.cardId}</span>
                            </div>
                            <div class="ability-copy-card-meta">
                                <span class="tech-badge">COST ${g.cardCost}</span>
                                <span class="tech-tag">${g.cardType}</span>
                                <span class="tech-tag">${g.cardClass}</span>
                                <span class="tech-tag">ABILITIES ${M}</span>
                            </div>
                            <span class="ability-copy-card-toggle" aria-hidden="true">></span>
                        </button>
                        <div class="ability-copy-card-body">
                            ${N}
                        </div>
                    </div>
                `}).join(""),o.querySelectorAll(".ability-copy-card").forEach(g=>{const A=g.querySelector(".ability-copy-card-header"),x=g.dataset.cardId;!A||!x||A.addEventListener("click",()=>{const M=g.classList.toggle("expanded");A.setAttribute("aria-expanded",M?"true":"false"),M?w.add(x):w.delete(x),H()})}),o.querySelectorAll(".ability-copy-ability").forEach(g=>{const A=g.dataset.abilityId;A&&g.addEventListener("click",x=>{x.stopPropagation(),o.querySelectorAll(".ability-copy-ability").forEach(M=>M.classList.remove("selected")),g.classList.add("selected"),this.selectedExistingAbilityId=A,n&&(n.disabled=!1)})}),_(S.length,D),Y(u)},K=(u="")=>{const b=u.trim().toLowerCase();S=c.filter(E=>b?E.cardSearchText.includes(b):!0),L=1,H()};p&&p.addEventListener("input",()=>K(p.value)),f&&f.addEventListener("click",()=>{L>1&&(L-=1,H())}),y&&y.addEventListener("click",()=>{const u=Math.max(1,Math.ceil(S.length/v));L<u&&(L+=1,H())}),K(""),n&&n.addEventListener("click",async()=>{if(this.selectedExistingAbilityId){n.disabled=!0,i==null||i.show("复制能力中...");try{await this.copyExistingAbilityById(this.selectedExistingAbilityId),await this.renderAbilitiesList(),l(),alert("能力复制成功！")}catch(u){console.error("CardEditor: Failed to copy ability:",u),alert(`复制能力失败: ${u.message}`)}finally{i==null||i.hide(),n.disabled=!1}}})}catch(t){console.error("CardEditor: Failed to show copy ability modal:",t),alert(`打开复制窗口失败: ${t.message}`),this.closeLinkExistingAbilityModal()}};d.prototype.closeLinkExistingAbilityModal=function(){const t=document.getElementById("link-ability-modal");t&&t.remove()};d.prototype.switchAddAbilityTab=function(t){const e=document.getElementById("add-ability-modal");if(!e)return;e.querySelectorAll(".modal-tab-btn").forEach(r=>{r.classList.remove("active")}),e.querySelectorAll(".modal-tab-panel").forEach(r=>{r.classList.remove("active")});const a=e.querySelector(`[data-tab="${t}"]`),i=e.querySelector(`#${t}-ability-panel`);a&&i&&(a.classList.add("active"),i.classList.add("active"))};d.prototype.loadExistingAbilities=async function(){const t=document.getElementById("existing-abilities-list");if(t)try{if(Array.isArray(this.abilitiesLibraryCache)){this.renderExistingAbilitiesList(this.abilitiesLibraryCache);return}const e=await fetch("/api/abilities");if(!e.ok)throw new Error(`HTTP ${e.status}: ${e.statusText}`);this.applyDataRevisionFromResponse(e);const a=await e.json();this.abilitiesLibraryCache=Array.isArray(a)?a:[],Array.isArray(a)&&a.forEach(i=>{const r=this.getAbilityIdKey((i==null?void 0:i.id)??(i==null?void 0:i.Id));r&&this.abilityDetailCache.set(r,i)}),this.renderExistingAbilitiesList(a)}catch(e){console.error("CardEditor: Failed to load existing abilities:",e),t.innerHTML=`
            <div class="error-container">
                <div class="error-icon">⚠️</div>
                <p>加载能力库失败</p>
                <small>${e.message}</small>
            </div>
        `}};d.prototype.renderExistingAbilitiesList=function(t){const e=document.getElementById("existing-abilities-list");if(!e)return;if(!Array.isArray(t)||t.length===0){e.innerHTML=`
            <div class="empty-state">
                <p>暂无可用能力</p>
                <small>请先创建一些能力</small>
            </div>
        `;return}const a=t.map(r=>{const s=nt(r);return`
        <div class="existing-ability-item" data-ability-id="${r.id}">
            <div class="ability-info">
                <div class="ability-name">${r.name||"未命名能力"}</div>
                ${s?`<div class="ability-tag">${s}</div>`:""}
                <div class="ability-description">${r.description||"暂无描述"}</div>
            </div>
            <button class="btn-secondary select-ability-btn" data-ability-id="${r.id}">选择</button>
        </div>
    `}).join("");e.innerHTML=a,e.querySelectorAll(".select-ability-btn").forEach(r=>{r.addEventListener("click",()=>{const s=r.dataset.abilityId;this.selectExistingAbility(s)})})};d.prototype.selectExistingAbility=function(t){const e=document.getElementById("existing-abilities-list");if(e){e.querySelectorAll(".existing-ability-item").forEach(i=>{i.classList.remove("selected")});const a=e.querySelector(`[data-ability-id="${t}"]`);a&&a.classList.add("selected")}this.selectedExistingAbilityId=t};d.prototype.confirmAddAbility=async function(){const t=document.getElementById("add-ability-modal");if(!t)return;const e=t.querySelector(".modal-tab-btn.active");if(!e)return;const a=e.dataset.tab;try{a==="create"?await this.createNewAbility():a==="existing"&&await this.linkExistingAbility(),this.closeAddAbilityModal(),await this.renderAbilitiesList()}catch(i){console.error("CardEditor: Failed to add ability:",i),alert(`添加能力失败: ${i.message}`)}};d.prototype.createNewAbility=async function(){const t=document.getElementById("add-ability-modal");if(!t)return;const e=t.querySelector("#ability-name").value.trim(),a=t.querySelector("#ability-description").value.trim(),i=t.querySelector("#ability-tag").value,r=t.querySelector("#ability-type").value,s=parseInt(t.querySelector("#ability-priority").value)||0;if(!e){alert("请输入能力名称");return}if(!i){alert("请选择能力标签");return}const n={cardId:this.currentCard.id,name:e,description:a,abilityKeywords:[{keyword:i}],abilityType:r?parseInt(r):1,priority:s,isActive:!0,jsonConfig:JSON.stringify({name:e,description:a,abilityKeywords:[{keyword:i}],type:r||"Triggered",trigger:{eventTypes:["CardPlayed"],timing:"On",validZones:[]},conditions:[],targets:{},effects:[]})},o=await fetch("/api/abilities",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify(n)});if(!o.ok)throw new Error(`HTTP ${o.status}: ${o.statusText}`);this.applyDataRevisionFromResponse(o);const l=await o.json();this.invalidateAbilitiesForCard(this.currentCard.id),this.invalidateAbilityLibraryCache();const p=this.getAbilityIdKey((l==null?void 0:l.id)??(l==null?void 0:l.Id));p&&this.abilityDetailCache.set(p,l),await this.refreshAbilityDataCacheAfterSave()};d.prototype.linkExistingAbility=async function(){if(!this.selectedExistingAbilityId){alert("请选择一个现有能力");return}await this.copyExistingAbilityById(this.selectedExistingAbilityId),this.selectedExistingAbilityId=null};d.prototype.copyExistingAbilityById=async function(t){if(!this.currentCard||!this.currentCard.id)throw new Error("请先选择并保存一张卡牌");const e=this.getAbilityIdKey(t);let a=e?this.abilityDetailCache.get(e):null;if(!a){const o=await fetch(`/api/abilities/${t}`);if(!o.ok)throw new Error(`HTTP ${o.status}: ${o.statusText}`);this.applyDataRevisionFromResponse(o),a=await o.json(),e&&this.abilityDetailCache.set(e,a)}const i={cardId:this.currentCard.id,name:a.name,description:a.description,abilityKeywords:a.abilityKeywords||V(a),abilityType:a.abilityType,priority:a.priority,isActive:a.isActive,jsonConfig:a.jsonConfig},r=await fetch("/api/abilities",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify(i)});if(!r.ok)throw new Error(`HTTP ${r.status}: ${r.statusText}`);this.applyDataRevisionFromResponse(r);const s=await r.json();this.invalidateAbilitiesForCard(this.currentCard.id),this.invalidateAbilityLibraryCache();const n=this.getAbilityIdKey((s==null?void 0:s.id)??(s==null?void 0:s.Id));return n&&this.abilityDetailCache.set(n,s),await this.refreshAbilityDataCacheAfterSave(),s};d.prototype.editAbility=async function(t){try{const e=this.getAbilityIdKey(t);let a=e?this.abilityDetailCache.get(e):null;if(!a){const i=await fetch(`/api/abilities/${t}`);if(!i.ok)throw new Error(`HTTP ${i.status}: ${i.statusText}`);this.applyDataRevisionFromResponse(i),a=await i.json(),e&&this.abilityDetailCache.set(e,a)}this.showAbilityEditModal(a)}catch(e){console.error("CardEditor: Failed to load ability for editing:",e),alert(`加载能力失败: ${e.message}`)}};d.prototype.removeAbility=async function(t){var e;if(confirm("确定要移除这个能力吗？"))try{const a=((e=this.container)==null?void 0:e.querySelector(".card-editor-layout"))||this.container,i=a?j(a):null;i==null||i.show("正在移除能力...");try{const r=await fetch(`/api/cards/${this.currentCard.id}/abilities/${t}`,{method:"DELETE"});if(!r.ok)throw new Error(`HTTP ${r.status}: ${r.statusText}`);this.applyDataRevisionFromResponse(r),this.invalidateAbilitiesForCard(this.currentCard.id),this.invalidateAbilityDetailCache(t),this.invalidateAbilityLibraryCache(),await this.renderAbilitiesList()}finally{i==null||i.hide()}}catch(a){console.error("CardEditor: Failed to remove ability:",a),alert(`移除能力失败: ${a.message}`)}};d.prototype.showAbilityEditModal=async function(t){try{const e=this.parseAbilityConfig(t),a=`
            <div class="modal-overlay" id="ability-edit-modal">
                <div class="modal-container ability-edit-container">
                    <div class="modal-header">
                        <h3>编辑能力: ${t.name}</h3>
                        <button class="modal-close-btn" id="close-ability-edit-modal">×</button>
                    </div>
                    <div class="modal-content" id="ability-editor-container">
                        <!-- 能力编辑器将在这里渲染 -->
                    </div>
                </div>
            </div>
        `;document.body.insertAdjacentHTML("beforeend",a);const i=document.getElementById("ability-editor-container"),{AbilityEditor:r}=await rt(async()=>{const{AbilityEditor:n}=await import("./AbilityEditor-CwU3fQSY.js");return{AbilityEditor:n}},__vite__mapDeps([0,1,2,3,4]));await new r().render(i,e,async n=>{n===null?this.closeAbilityEditModal():(await this.saveAbilityChanges(t,n),this.closeAbilityEditModal())}),this.bindAbilityEditModalCloseEvents()}catch(e){console.error("CardEditor: Failed to show ability edit modal:",e),alert(`打开能力编辑器失败: ${e.message}`)}};d.prototype.parseAbilityConfig=function(t){var a;let e={id:t.id,name:t.name||"",description:t.description||"",cardName:((a=this.currentCard)==null?void 0:a.name)||"",abilityKeywords:V(t),abilityType:t.abilityType||1,priority:t.priority||0,isActive:t.isActive!==!1,type:t.type||t.Type||"Triggered",trigger:{eventTypes:[],relationType:"",timing:"On",validZones:[]},conditions:[],targets:[],effects:[],auras:[],counterRegistrations:[]};if(t.jsonConfig)try{let i;typeof t.jsonConfig=="string"?i=JSON.parse(t.jsonConfig):i=t.jsonConfig,i.trigger&&(e.trigger={...e.trigger,...i.trigger}),i.abilityKeywords&&(e.abilityKeywords=V({abilityKeywords:i.abilityKeywords})),i.conditions&&(e.conditions=i.conditions),i.targets&&(e.targets=i.targets),i.effects&&(e.effects=i.effects),i.auras&&(e.auras=i.auras),i.type?e.type=i.type:i.executionKind&&String(i.executionKind).toLowerCase()==="aura"&&(e.type="Continuous"),i.counterRegistrations&&(e.counterRegistrations=i.counterRegistrations)}catch(i){console.warn("CardEditor: Failed to parse ability JSON config:",i)}return e};d.prototype.bindAbilityEditModalCloseEvents=function(){const t=document.getElementById("ability-edit-modal");if(!t)return;const e=t.querySelector("#close-ability-edit-modal");e&&e.addEventListener("click",()=>{this.closeAbilityEditModal()});const a=i=>{i.key==="Escape"&&(this.closeAbilityEditModal(),document.removeEventListener("keydown",a))};document.addEventListener("keydown",a)};d.prototype.closeAbilityEditModal=function(){const t=document.getElementById("ability-edit-modal");t&&t.remove()};d.prototype.saveAbilityChanges=async function(t,e){var a;try{const i=await fetch(`/api/abilities/${t.id}`,{method:"PUT",headers:{"Content-Type":"application/json"},body:JSON.stringify(e)});if(!i.ok)throw new Error(`HTTP ${i.status}: ${i.statusText}`);this.applyDataRevisionFromResponse(i);const r=await i.json(),s=this.getAbilityIdKey((t==null?void 0:t.id)??(r==null?void 0:r.id));s&&this.abilityDetailCache.set(s,r);const n=((a=this.currentCard)==null?void 0:a.id)??(r==null?void 0:r.cardId)??(t==null?void 0:t.cardId);n&&this.invalidateAbilitiesForCard(n),this.invalidateAbilityLibraryCache(),await this.renderAbilitiesList(),await this.refreshAbilityDataCacheAfterSave(),alert("能力保存成功！")}catch(i){throw console.error("CardEditor: Failed to save ability changes:",i),alert(`保存能力失败: ${i.message}`),i}};d.prototype.loadKeywordOptions=async function(t){try{const e=await this.metadataManager.getEnum("CardKeyword");this.keywordLabelMap=e,t.innerHTML='<option value="">选择关键字</option>',Object.entries(e).forEach(([a,i])=>{if(a!=="None"){const r=document.createElement("option");r.value=a,r.textContent=i,t.appendChild(r)}})}catch(e){console.error("Failed to load keyword options:",e);const a={Storm:"疾走",Ward:"守护",Bane:"必杀",Rush:"突进",Ambush:"潜行",Drain:"吸血"};this.keywordLabelMap=a,t.innerHTML='<option value="">选择关键字</option>',Object.entries(a).forEach(([i,r])=>{const s=document.createElement("option");s.value=i,s.textContent=r,t.appendChild(s)})}};const Wt=Object.freeze(Object.defineProperty({__proto__:null,cleanupCardEditor:jt,getCardEditor:Ut,initializeCardEditor:Kt},Symbol.toStringTag,{value:"Module"}));export{st as a,Wt as c,Tt as e,Yt as g};
