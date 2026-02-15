import{c as L,a as Be}from"./index-D7YhJMYy.js";import{l as qe,c as ke}from"./cardDefinitionsLoader-BK4UUvCB.js";const X="open",be=[{value:"open",label:"开放环境",description:"所有卡牌集合可用"}],Z=[{value:"forestcraft",label:"妖精"},{value:"swordcraft",label:"皇家护卫"},{value:"runecraft",label:"巫师"},{value:"dragoncraft",label:"龙族"},{value:"abysscraft",label:"梦魇"},{value:"havencraft",label:"主教"},{value:"portalcraft",label:"复仇者"}],ye=[{value:"follower",label:"从者"},{value:"spell",label:"法术"},{value:"amulet",label:"护符"},{value:"crest",label:"纹章"},{value:"leader",label:"主战者"}],ge=[{value:"bronze",label:"青铜"},{value:"silver",label:"白银"},{value:"gold",label:"黄金"},{value:"legend",label:"传说"}],s={decks:[],currentDeck:null,rawCardsCache:null,availableCards:[],cardIndex:new Map,deckCreation:{mode:"create",environment:null,class:null,step:"environment",importName:"",importCode:""},filters:{cost:null,class:"",type:"",rarity:"",set:"",search:""},pagination:{currentPage:1,itemsPerPage:10},newDeckId:null};async function ee(e){const t=await fetch(e,{method:"GET"}),n=await t.json();if(!t.ok){const i=(n==null?void 0:n.message)||(n==null?void 0:n.Message)||t.statusText;throw new Error(i||`HTTP ${t.status}`)}return n}async function te(e,t){const n=await fetch(e,{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify(t??{})}),i=await n.json();if(!n.ok){const r=(i==null?void 0:i.message)||(i==null?void 0:i.Message)||n.statusText;throw new Error(r||`HTTP ${n.status}`)}return i}async function Me(e,t){const n=await fetch(e,{method:"PUT",headers:{"Content-Type":"application/json"},body:JSON.stringify(t??{})});let i=null;try{i=await n.json()}catch{}if(!n.ok){const r=(i==null?void 0:i.message)||(i==null?void 0:i.Message)||n.statusText;throw new Error(r||`HTTP ${n.status}`)}return i}async function Re(e){const t=await fetch(e,{method:"DELETE"});if(!t.ok){const n=await t.json().catch(()=>null),i=(n==null?void 0:n.message)||(n==null?void 0:n.Message)||t.statusText;throw new Error(i||`HTTP ${t.status}`)}}let R=null;async function ot(){if(R=document.getElementById("deck-builder-container"),!R){console.error("DeckBuilder: Container not found!");return}try{await _e(),await Promise.all([P(),Oe()])}catch(e){console.error("DeckBuilder: Failed to load data:",e)}$()}async function _e(){const e=L();try{await te(`/api/decks/bootstrap?${e}`,{})}catch(t){console.warn("DeckBuilder: Bootstrap failed.",t)}}async function P(){const e=L(),t=await ee(`/api/decks?${e}`);s.decks=Array.isArray(t)?t.map(n=>({...n,environment:I(n.environment)})):[]}async function Oe(){const e=await qe({useCache:!0,force:!1,getCached:()=>s.rawCardsCache,setCached:n=>{s.rawCardsCache=n}}),t=Array.isArray(e)?e.filter(n=>((n==null?void 0:n.isCollectible)??(n==null?void 0:n.IsCollectible))!==!0||y((n==null?void 0:n.rarity)??(n==null?void 0:n.Rarity)??"")==="special"?!1:Number(n==null?void 0:n.id)>0&&String((n==null?void 0:n.type)||"").toLowerCase()!=="leader"):[];s.availableCards=t,s.cardIndex=new Map(t.map(n=>[Number(n.id),n]))}function A(e){return e!=null&&e.cards?e.cards.reduce((t,n)=>t+(n.count||0),0):(e==null?void 0:e.cardCount)??0}function _(e){return(e==null?void 0:e.deckId)||(e==null?void 0:e.id)||null}function Y(e,t="New Deck"){const n=e==null?void 0:e.name;return n==null?t:String(n).trim()||t}function He(e){return(Array.isArray(e==null?void 0:e.cards)?e.cards:[]).map(n=>{const i=Number(n==null?void 0:n.cardDefinitionId),r=Number((n==null?void 0:n.count)??0),c=s.cardIndex.get(i)??null,u=Number(c==null?void 0:c.cost);return{cardDefinitionId:i,count:Number.isFinite(r)?r:0,card:c,cost:Number.isFinite(u)?u:99}}).filter(n=>n.count>0)}function je(e,t){var r,c;if(e.cost!==t.cost)return e.cost-t.cost;const n=String(((r=e.card)==null?void 0:r.name)??e.cardDefinitionId??""),i=String(((c=t.card)==null?void 0:c.name)??t.cardDefinitionId??"");return n.localeCompare(i,void 0,{sensitivity:"base"})}function Fe(e){const t=He(e).sort(je),n=Array(9).fill(0),i={follower:0,spell:0,amulet:0};let r=0;return t.forEach(c=>{var v,b,m,f;const u=Math.min(Math.max(c.cost,0),8);n[u]+=c.count,r+=c.count;const o=y(((v=c.card)==null?void 0:v.type)??((b=c.card)==null?void 0:b.Type)??((m=c.card)==null?void 0:m.cardType)??((f=c.card)==null?void 0:f.card_type));(o==="follower"||o==="spell"||o==="amulet")&&(i[o]+=c.count)}),{items:t,curve:n,typeCounts:i,totalCount:r}}function Ue(e){const t=Array.isArray(e)?e:Array(9).fill(0),n=Math.max(...t,1);return t.map((i,r)=>`
        <div class="deck-preview-curve-col" title="${r===8?"8+":r} 费: ${i}">
            <div class="deck-preview-curve-bar" style="height: ${i/n*100}%"></div>
            <div class="deck-preview-curve-label">${r===8?"8+":r}</div>
        </div>
    `).join("")}function ze(e){return!Array.isArray(e)||e.length===0?`
            <div class="deck-preview-empty">
                当前卡组没有可预览的卡牌。
            </div>
        `:e.map(t=>{var m,f,w,l,g;const n=((m=t.card)==null?void 0:m.artPreview)??((f=t.card)==null?void 0:f.ArtPreview)??"",i=n?encodeURI(n).replace(/'/g,"%27"):"",r=i?` style="--deck-preview-art: url('${i}');"`:"",c=ne(t.card),u=`deck-preview-card${i?" has-art":""}${c?` ${c}`:""}`,o=Number.isFinite(t.cost)&&t.cost>=0&&t.cost<99?t.cost:"?",v=((w=t.card)==null?void 0:w.name)??`未知卡牌 #${t.cardDefinitionId}`,b=Ee(((l=t.card)==null?void 0:l.type)??((g=t.card)==null?void 0:g.Type)??"");return`
            <button class="${u}" type="button" data-card-id="${t.cardDefinitionId}"${r}>
                <span class="deck-preview-card-cost">${o}</span>
                <span class="deck-preview-card-count">x${t.count}</span>
                <span class="deck-preview-card-name">${v}</span>
                <span class="deck-preview-card-type">${b||"未知"}</span>
            </button>
        `}).join("")}async function we(e){const t=L(),n=await ee(`/api/decks/${e}?${t}`),i=y((n==null?void 0:n.class)??(n==null?void 0:n.Class)??(n==null?void 0:n.cardClass)??(n==null?void 0:n.card_class));return{...n,environment:I(n==null?void 0:n.environment),class:i,cards:Array.isArray(n==null?void 0:n.cards)?n.cards:[]}}async function Ve(e){const t=L(),n=await ee(`/api/decks/${e}/code?${t}`),i=(n==null?void 0:n.code)??(n==null?void 0:n.Code)??"";if(!i)throw new Error("未获取到卡组代码");return String(i)}async function Ge(e){var i;if((i=navigator==null?void 0:navigator.clipboard)!=null&&i.writeText){await navigator.clipboard.writeText(e);return}const t=document.createElement("textarea");t.value=e,t.setAttribute("readonly","true"),t.style.position="fixed",t.style.top="-1000px",t.style.left="-1000px",document.body.appendChild(t),t.select();const n=document.execCommand("copy");if(document.body.removeChild(t),!n)throw new Error("当前环境不支持自动复制")}function he(e){if(!e)return"Neutral";const t=String(e);return t.charAt(0).toUpperCase()+t.slice(1)}function y(e){return e==null?"":String(e).trim().toLowerCase()}function ne(e){const t=y((e==null?void 0:e.rarity)??(e==null?void 0:e.Rarity)??"");return!t||t==="special"?"":t==="legendary"?"rarity-legend":`rarity-${t}`}function K(e,t=""){return e==null||e===""?t:typeof e=="number"?e===0?X:String(e):y(e)}function I(e){return K(e,X)}function Je(e){if(Array.isArray(e))return e.map(n=>K(n)).filter(Boolean);const t=K(e);return t?[t]:[]}function W(e){const t=I(e),n=be.find(i=>i.value===t);return(n==null?void 0:n.label)||"未知环境"}function Ce(e){const t=y(e),n=Z.find(i=>i.value===t);return(n==null?void 0:n.label)||he(e)}function Ee(e){const t=y(e),n=ye.find(i=>i.value===t);return(n==null?void 0:n.label)||De(t)}function Ye(e){const t=y(e),n=ge.find(i=>i.value===t);return(n==null?void 0:n.label)||De(t)}function De(e){return e?String(e).split("_").filter(Boolean).map(t=>t.charAt(0).toUpperCase()+t.slice(1)).join(" "):""}function se(e){const t=(e==null?void 0:e.card_set)??(e==null?void 0:e.cardSet)??(e==null?void 0:e.card_set_name)??(e==null?void 0:e.cardSetName)??"",n=(e==null?void 0:e.card_set_code)??(e==null?void 0:e.cardSetCode)??"",i=(e==null?void 0:e.card_set_abbr)??(e==null?void 0:e.cardSetAbbr)??"",r=(e==null?void 0:e.card_set_environment)??(e==null?void 0:e.cardSetEnvironment)??(e==null?void 0:e.card_set_environments)??(e==null?void 0:e.cardSetEnvironments)??null;return{name:t==null?"":String(t).trim(),code:n==null?"":String(n).trim(),abbr:i==null?"":String(i).trim(),environments:Je(r)}}function Qe(e){return e&&e.name||""}function Ke(){const e=Array.isArray(s.availableCards)?s.availableCards:[];return s.currentDeck?e.filter(t=>!(!Le(t,s.currentDeck.environment)||!Ie(t,s.currentDeck.class))):e}function Q(e,t,n,i){const r=new Set;e.forEach(o=>{const v=y(t(o));v&&r.add(v)});const c=[];n.forEach(o=>{r.has(o.value)&&(c.push({value:o.value,label:o.label}),r.delete(o.value))});const u=Array.from(r).sort((o,v)=>o.localeCompare(v,void 0,{sensitivity:"base"})).map(o=>({value:o,label:i(o)}));return c.concat(u)}function We(e){const t=new Map;return e.forEach(n=>{const i=se(n),r=i.name;if(!r)return;const c=r.toLowerCase();t.has(c)||t.set(c,{value:r,label:Qe(i)})}),Array.from(t.values()).sort((n,i)=>n.label.localeCompare(i.label,void 0,{numeric:!0,sensitivity:"base"})).map(n=>({value:n.value.toLowerCase(),label:n.label}))}function q(e,t){return e.map(n=>{const i=n.value??"",r=n.label??i;return`<option value="${i}" ${t&&i===t?"selected":""}>${r}</option>`}).join("")}function Xe(){const e=document.getElementById("library-class-filter"),t=document.getElementById("library-type-filter"),n=document.getElementById("library-rarity-filter"),i=document.getElementById("library-set-filter");if(!e||!t||!n||!i)return;const r=Ke(),c=Q(r,l=>(l==null?void 0:l.class)??(l==null?void 0:l.Class)??(l==null?void 0:l.cardClass)??(l==null?void 0:l.card_class),Z,Ce),u=Q(r,l=>(l==null?void 0:l.type)??(l==null?void 0:l.Type)??(l==null?void 0:l.cardType)??(l==null?void 0:l.card_type),ye,Ee),o=Q(r,l=>(l==null?void 0:l.rarity)??(l==null?void 0:l.Rarity),ge,Ye),v=We(r),b=new Set(c.map(l=>l.value)),m=new Set(u.map(l=>l.value)),f=new Set(o.map(l=>l.value)),w=new Set(v.map(l=>l.value));s.filters.class&&!b.has(s.filters.class)&&(s.filters.class=""),s.filters.type&&!m.has(s.filters.type)&&(s.filters.type=""),s.filters.rarity&&!f.has(s.filters.rarity)&&(s.filters.rarity=""),s.filters.set&&!w.has(s.filters.set)&&(s.filters.set=""),e.innerHTML='<option value="">ALL CLASSES</option>'+q(c,s.filters.class),t.innerHTML='<option value="">ALL TYPES</option>'+q(u,s.filters.type),n.innerHTML='<option value="">ALL RARITIES</option>'+q(o,s.filters.rarity),i.innerHTML='<option value="">ALL SETS</option>'+q(v,s.filters.set)}function Le(e,t){const n=I(t);if(n===X)return!0;const i=se(e).environments;return!i||i.length===0?!0:i.includes(n)}function Ie(e,t){const n=y((e==null?void 0:e.class)??(e==null?void 0:e.Class)??(e==null?void 0:e.cardClass)??(e==null?void 0:e.card_class)),i=y(t);return!n||!i||n==="neutral"?!0:n===i}function $(){R.innerHTML=`
        <div class="deck-selection-view">
            <div class="view-header">
                <div class="header-left">
                    <h2>Deck List</h2>
                </div>
                <div class="header-right">
                    <!-- Placeholder for "Edit List" or other actions -->
                </div>
            </div>
            <div class="deck-grid">
                ${s.decks.map(a=>{const d=_(a),p=Y(a),k=a.cardCount??A(a),h=he(a.class),B=k===40;return`
                    <div class="deck-card wide-banner" data-id="${d}">
                        <div class="deck-art-bg"></div> <!-- Placeholder for Character Art -->
                        <div class="deck-content">
                            <div class="deck-info">
                                <div class="deck-class-name">${h}</div>
                                <div class="deck-env-name">${W(a.environment)}</div>
                                <div class="deck-name">${p}</div>
                                <div class="deck-status">
                                    <span class="status-indicator ${B?"valid":"invalid"}">
                                        ${B?"Complete":"Incomplete"}
                                    </span>
                                    <span class="deck-count">${k} / 40</span>
                                </div>
                            </div>
                            <div class="deck-emblem">
                                <div class="emblem-icon"></div>
                            </div>
                        </div>
                        <div class="deck-actions-overlay">
                            <button class="deck-action-btn preview-deck-btn" data-id="${d}">预览</button>
                            <button class="deck-action-btn edit-deck-btn" data-id="${d}">编辑</button>
                            <button class="deck-action-btn delete-deck-btn" data-id="${d}">删除</button>
                        </div>
                    </div>
                `}).join("")}
                
                <!-- New Deck Button as a Grid Item -->
                <div class="deck-card create-new-deck" id="create-deck-card">
                    <div class="create-content">
                        <span class="plus-icon">+</span>
                        <span class="create-text">New Deck</span>
                    </div>
                </div>
            </div>

            <div class="deck-create-modal" id="deck-create-modal" aria-hidden="true">
                <div class="deck-create-dialog" role="dialog" aria-modal="true">
                    <div class="deck-create-header">
                        <div class="deck-create-title">新建卡组</div>
                        <button class="btn-icon deck-create-close" id="deck-create-close" type="button">×</button>
                    </div>
                    <div class="deck-create-mode-switch" role="tablist" aria-label="创建卡组方式">
                        <button class="deck-mode-btn selected" data-mode="create" type="button">新建卡组</button>
                        <button class="deck-mode-btn" data-mode="import" type="button">导入代码</button>
                    </div>

                    <div class="deck-create-panel" data-mode-panel="create">
                        <div class="deck-create-step" data-step="environment">
                            <div class="deck-create-step-title">选择环境</div>
                            <div class="deck-create-options">
                                ${be.map(a=>`
                                    <button class="deck-option" data-env="${a.value}" type="button">
                                        <div class="deck-option-title">${a.label}</div>
                                        <div class="deck-option-desc">${a.description}</div>
                                    </button>
                                `).join("")}
                            </div>
                        </div>
                        <div class="deck-create-step hidden" data-step="class">
                            <div class="deck-create-step-title">选择职业</div>
                            <div class="deck-create-options grid">
                                ${Z.map(a=>`
                                    <button class="deck-option" data-class="${a.value}" type="button">
                                        <div class="deck-option-title">${a.label}</div>
                                    </button>
                                `).join("")}
                            </div>
                        </div>

                        <div class="deck-create-actions" id="deck-create-create-actions">
                            <button class="btn-secondary" id="deck-create-back" type="button">上一步</button>
                            <button class="btn-primary" id="deck-create-next" type="button">下一步</button>
                            <button class="btn-primary" id="deck-create-confirm" type="button">创建</button>
                        </div>
                    </div>

                    <div class="deck-create-panel hidden" data-mode-panel="import">
                        <div class="deck-create-step-title">导入卡组代码</div>
                        <label class="deck-import-field">
                            <span class="deck-import-label">卡组名称（可选）</span>
                            <input id="deck-import-name-input" class="deck-import-input" type="text" maxlength="64" placeholder="Imported Deck">
                        </label>
                        <label class="deck-import-field">
                            <span class="deck-import-label">卡组代码</span>
                            <textarea id="deck-import-code-input" class="deck-import-textarea" rows="5" placeholder="粘贴导出的卡组代码"></textarea>
                        </label>
                        <div class="deck-import-hint">使用导出的代码直接创建卡组，创建后将进入卡组编辑。</div>

                        <div class="deck-create-actions" id="deck-create-import-actions">
                            <button class="btn-primary" id="deck-import-submit-btn" type="button">导入并创建</button>
                        </div>
                    </div>
                </div>
            </div>

            <div class="deck-preview-modal" id="deck-preview-modal" aria-hidden="true">
                <div class="deck-preview-dialog" role="dialog" aria-modal="true" aria-labelledby="deck-preview-title">
                    <div class="deck-preview-toolbar">
                        <div class="deck-preview-toolbar-left">
                            <div class="deck-preview-title-group">
                                <div class="deck-preview-caption">卡组预览</div>
                                <h3 class="deck-preview-title" id="deck-preview-title">-</h3>
                                <div class="deck-preview-meta" id="deck-preview-meta">-</div>
                            </div>
                        </div>
                        <div class="deck-preview-toolbar-actions">
                            <button class="btn-secondary" id="deck-preview-export-btn" type="button">导出卡组代码</button>
                            <button class="btn-secondary" id="deck-preview-close-btn" type="button" aria-label="关闭预览">关闭</button>
                        </div>
                    </div>

                    <div class="deck-preview-summary">
                        <div class="deck-preview-curve" id="deck-preview-curve"></div>
                        <div class="deck-preview-counts" id="deck-preview-counts"></div>
                    </div>

                    <div class="deck-preview-cards" id="deck-preview-cards"></div>
                </div>
            </div>

            <div class="deck-code-modal" id="deck-code-modal" aria-hidden="true">
                <div class="deck-code-dialog" role="dialog" aria-modal="true" aria-labelledby="deck-code-title">
                    <div class="deck-code-header">
                        <div>
                            <div class="deck-code-caption">卡组代码</div>
                            <h3 class="deck-code-title" id="deck-code-title">-</h3>
                        </div>
                        <button class="btn-secondary small" id="deck-code-close-btn" type="button" aria-label="关闭导出">关闭</button>
                    </div>
                    <textarea class="deck-code-output" id="deck-code-output" readonly></textarea>
                    <div class="deck-code-footer">
                        <span class="deck-code-status" id="deck-code-status"></span>
                        <button class="btn-primary" id="deck-code-copy-btn" type="button">复制代码</button>
                    </div>
                </div>
            </div>
        </div>
    `;const e=new Map(s.decks.map(a=>[String(_(a)),Y(a)])),t=document.getElementById("deck-create-modal"),n=document.getElementById("deck-create-close"),i=document.getElementById("deck-create-back"),r=document.getElementById("deck-create-next"),c=document.getElementById("deck-create-confirm"),u=t==null?void 0:t.querySelectorAll(".deck-mode-btn"),o=t==null?void 0:t.querySelector('[data-mode-panel="create"]'),v=t==null?void 0:t.querySelector('[data-mode-panel="import"]'),b=document.getElementById("deck-import-name-input"),m=document.getElementById("deck-import-code-input"),f=document.getElementById("deck-import-submit-btn"),w=t==null?void 0:t.querySelector('[data-step="environment"]'),l=t==null?void 0:t.querySelector('[data-step="class"]'),g=document.getElementById("deck-preview-modal"),ce=document.getElementById("deck-preview-title"),oe=document.getElementById("deck-preview-meta"),le=document.getElementById("deck-preview-curve"),de=document.getElementById("deck-preview-counts"),O=document.getElementById("deck-preview-cards"),H=document.getElementById("deck-preview-close-btn"),j=document.getElementById("deck-preview-back-btn"),F=document.getElementById("deck-preview-export-btn"),C=document.getElementById("deck-code-modal"),U=document.getElementById("deck-code-close-btn"),z=document.getElementById("deck-code-copy-btn"),ue=document.getElementById("deck-code-output"),ve=document.getElementById("deck-code-title"),S=document.getElementById("deck-code-status");let T=null,V="",N="";const pe=()=>{s.deckCreation={mode:"create",environment:null,class:null,step:"environment",importName:"",importCode:""}},D=()=>{var p;if(!t||!w||!l||!i||!r||!c)return;const a=s.deckCreation.mode==="import",d=s.deckCreation.step==="environment";o==null||o.classList.toggle("hidden",a),v==null||v.classList.toggle("hidden",!a),w.classList.toggle("hidden",a||!d),l.classList.toggle("hidden",a||d),i.disabled=a||d,r.style.display=a?"none":d?"inline-flex":"none",c.style.display=a||d?"none":"inline-flex",r.disabled=a||!s.deckCreation.environment,c.disabled=a||!s.deckCreation.class,f&&(f.disabled=!((p=s.deckCreation.importCode)!=null&&p.trim())),b&&b.value!==s.deckCreation.importName&&(b.value=s.deckCreation.importName),m&&m.value!==s.deckCreation.importCode&&(m.value=s.deckCreation.importCode),u==null||u.forEach(k=>{const h=k.getAttribute("data-mode");k.classList.toggle("selected",h===s.deckCreation.mode)}),w.querySelectorAll(".deck-option").forEach(k=>{const h=k.getAttribute("data-env");k.classList.toggle("selected",h===s.deckCreation.environment)}),l.querySelectorAll(".deck-option").forEach(k=>{const h=k.getAttribute("data-class");k.classList.toggle("selected",h===s.deckCreation.class)})},Ae=()=>{t&&(pe(),t.classList.add("open"),t.setAttribute("aria-hidden","false"),D())},G=()=>{g&&(g.classList.remove("open"),g.setAttribute("aria-hidden","true"),T=null)},me=()=>{C&&(C.classList.remove("open"),C.setAttribute("aria-hidden","true"),N="",S&&(S.textContent=""))},J=(a,d=!1)=>{S&&(S.textContent=a,S.classList.toggle("error",!!d))},Pe=(a,d)=>{!C||!ue||!ve||(ve.textContent=a,ue.value=d,N=d,J("代码已生成，可直接复制。"),C.classList.add("open"),C.setAttribute("aria-hidden","false"))},Te=async(a,d="New Deck")=>{const p=e.get(String(a))||d,k=await Ve(a);Pe(p,k)},Ne=async a=>{if(!g||!ce||!oe||!le||!de||!O)return;const d=await we(a),p=Fe(d);T=a,V=Y(d),ce.textContent=V,oe.textContent=`${Ce(d.class)} | ${W(d.environment)}`,le.innerHTML=Ue(p.curve),de.innerHTML=`
            <div class="deck-preview-total">${p.totalCount} / 40</div>
            <div class="deck-preview-type-row">
                <span>随从: ${p.typeCounts.follower}</span>
                <span>法术: ${p.typeCounts.spell}</span>
                <span>护符: ${p.typeCounts.amulet}</span>
            </div>
        `,O.innerHTML=ze(p.items),O.querySelectorAll(".deck-preview-card").forEach(k=>{k.addEventListener("click",h=>{h.stopPropagation();const B=Number(k.getAttribute("data-card-id")),xe=s.cardIndex.get(B);ae(xe)})}),g.classList.add("open"),g.setAttribute("aria-hidden","false")},x=()=>{t&&(t.classList.remove("open"),t.setAttribute("aria-hidden","true"),pe())};document.getElementById("create-deck-card").addEventListener("click",Ae),n==null||n.addEventListener("click",x),t==null||t.addEventListener("click",a=>{a.target===t&&x()}),u==null||u.forEach(a=>{a.addEventListener("click",()=>{const d=a.getAttribute("data-mode");d!=="create"&&d!=="import"||(s.deckCreation.mode=d,D())})}),b==null||b.addEventListener("input",a=>{s.deckCreation.importName=a.target.value}),m==null||m.addEventListener("input",a=>{s.deckCreation.importCode=a.target.value,D()}),g==null||g.addEventListener("click",a=>{a.target===g&&G()}),C==null||C.addEventListener("click",a=>{a.target===C&&me()}),H==null||H.addEventListener("click",G),j==null||j.addEventListener("click",G),U==null||U.addEventListener("click",me),F==null||F.addEventListener("click",()=>{T&&Te(T,V).catch(a=>{alert(`导出失败：${a.message}`)})}),z==null||z.addEventListener("click",()=>{N&&Ge(N).then(()=>J("复制成功。")).catch(a=>J(`复制失败：${a.message}`,!0))}),w==null||w.querySelectorAll(".deck-option").forEach(a=>{a.addEventListener("click",()=>{const d=a.getAttribute("data-env");s.deckCreation.environment=d,D()})}),l==null||l.querySelectorAll(".deck-option").forEach(a=>{a.addEventListener("click",()=>{const d=a.getAttribute("data-class");s.deckCreation.class=d,D()})}),r==null||r.addEventListener("click",()=>{s.deckCreation.environment&&(s.deckCreation.step="class",D())}),i==null||i.addEventListener("click",()=>{s.deckCreation.step="environment",D()}),c==null||c.addEventListener("click",()=>{if(!s.deckCreation.environment||!s.deckCreation.class)return;const a=s.deckCreation.environment,d=s.deckCreation.class;x(),nt(a,d).catch(p=>alert(`创建失败：${p.message}`))}),f==null||f.addEventListener("click",async()=>{var p,k;const a=(p=s.deckCreation.importCode)==null?void 0:p.trim();if(!a)return;const d=((k=s.deckCreation.importName)==null?void 0:k.trim())||"";f.disabled=!0;try{const h=await st(a,d);if(x(),h){await M(h);return}$()}catch(h){alert(`导入失败：${h.message}`),f.disabled=!1,D()}}),document.querySelectorAll(".preview-deck-btn").forEach(a=>{a.addEventListener("click",d=>{d.stopPropagation(),Ne(a.dataset.id).catch(p=>alert(`预览失败：${p.message}`))})}),document.querySelectorAll(".edit-deck-btn").forEach(a=>{a.addEventListener("click",d=>{d.stopPropagation(),M(d.target.dataset.id).catch(p=>alert(`加载失败：${p.message}`))})}),document.querySelectorAll(".delete-deck-btn").forEach(a=>{a.addEventListener("click",d=>{d.stopPropagation(),Se(d.target.dataset.id).catch(p=>alert(`删除失败：${p.message}`))})}),document.querySelectorAll(".deck-card.wide-banner").forEach(a=>{a.addEventListener("click",()=>{M(a.dataset.id).catch(d=>alert(`加载失败：${d.message}`))})})}function Ze(){s.currentDeck&&(R.innerHTML=`
        <div class="deck-builder-view vertical-layout">
            <!-- Top Section: Deck Info & Contents -->
            <div class="deck-section">
                <div class="deck-header-row">
                    <div class="deck-identity">
                        <div class="class-icon-small"></div>
                        <input type="text" class="deck-name-input" value="${s.currentDeck.name}" id="deck-name-input">
                        <button class="btn-icon edit-name-btn">✎</button>
                        <div class="deck-env-tag">${W(s.currentDeck.environment)}</div>
                    </div>
                    
                    <div class="mana-curve-mini" id="mana-curve">
                        <!-- Mana curve bars -->
                    </div>

                    <div class="deck-meta-controls">
                        <div class="deck-counts">
                            <span class="total-count"><span id="deck-count-display">${A(s.currentDeck)}</span> / 40</span>
                            <div class="type-counts">
                                <span class="type-count follower">Follower: 0</span>
                                <span class="type-count spell">Spell: 0</span>
                                <span class="type-count amulet">Amulet: 0</span>
                            </div>
                        </div>
                        <div class="header-actions">
                            <button class="btn-primary" id="save-deck-btn">Confirm Deck</button>
                            <button class="btn-secondary" id="back-to-selection-btn">Back</button>
                        </div>
                    </div>
                </div>

                <!-- Action Bar -->
                <div class="deck-action-bar">
                    <button class="btn-secondary small">Save</button>
                    <button class="btn-secondary small">CRAFT MISSING</button>
                    <button class="btn-secondary small">Auto Edit</button>
                    <div class="spacer"></div>
                    <div class="filter-bar-mini">
                        ${[1,2,3,4,5,6,7,8].map(e=>`
                            <button class="filter-btn cost-btn" data-cost="${e}">${e}</button>
                        `).join("")}
                    </div>
                </div>

                <!-- Horizontal Deck List -->
                <div class="deck-contents-scroll" id="deck-list">
                    <!-- Deck cards injected here -->
                </div>
            </div>

            <!-- Bottom Section: Library -->
            <div class="library-section">
                <div class="library-search-row">
                    <div class="search-section">
                        <input type="text" id="card-search-input" placeholder="SEARCH LIBRARY..." class="search-input tech-input">
                        <div class="filter-section">
                            <select id="library-class-filter" class="filter-select tech-select">
                                <option value="">ALL CLASSES</option>
                            </select>
                            <select id="library-type-filter" class="filter-select tech-select">
                                <option value="">ALL TYPES</option>
                            </select>
                            <select id="library-rarity-filter" class="filter-select tech-select">
                                <option value="">ALL RARITIES</option>
                            </select>
                            <select id="library-set-filter" class="filter-select tech-select">
                                <option value="">ALL SETS</option>
                            </select>
                        </div>
                    </div>
                </div>
                <div class="library-grid" id="library-grid">
                    <!-- Library cards injected here -->
                </div>
                
                <div class="library-footer">
                    <div class="footer-left">
                        <button class="btn-tab active">Owned Cards</button>
                        <button class="btn-tab">Craft Mode</button>
                    </div>
                    <div class="footer-center">
                        <span class="page-indicator">1 / 18</span>
                    </div>
                    <div class="footer-right"></div>
                </div>
            </div>
        </div>
    `,Xe(),E(),ie(),re(),document.getElementById("card-search-input").addEventListener("input",e=>{s.filters.search=e.target.value,s.pagination.currentPage=1,E()}),document.getElementById("library-class-filter").addEventListener("change",e=>{s.filters.class=e.target.value,s.pagination.currentPage=1,E()}),document.getElementById("library-type-filter").addEventListener("change",e=>{s.filters.type=e.target.value,s.pagination.currentPage=1,E()}),document.getElementById("library-rarity-filter").addEventListener("change",e=>{s.filters.rarity=e.target.value,s.pagination.currentPage=1,E()}),document.getElementById("library-set-filter").addEventListener("change",e=>{s.filters.set=e.target.value,s.pagination.currentPage=1,E()}),document.querySelectorAll(".cost-btn").forEach(e=>{e.addEventListener("click",()=>{const t=parseInt(e.dataset.cost);s.filters.cost=s.filters.cost===t?null:t,document.querySelectorAll(".cost-btn").forEach(n=>n.classList.remove("active")),s.filters.cost&&e.classList.add("active"),s.pagination.currentPage=1,E()})}),document.getElementById("back-to-selection-btn").addEventListener("click",()=>{if(!s.currentDeck){$();return}if(A(s.currentDeck)>0){fe().catch(n=>alert(`保存失败：${n.message}`));return}const e=s.currentDeck.deckId||s.currentDeck.id,t=s.currentDeck.isNew===!0;if(s.currentDeck=null,e&&t){Se(e,!0).catch(n=>alert(`删除失败：${n.message}`));return}$()}),document.getElementById("save-deck-btn").addEventListener("click",()=>{fe().catch(e=>alert(`保存失败：${e.message}`))}),document.getElementById("deck-name-input").addEventListener("change",e=>{s.currentDeck.name=e.target.value}))}function et(){return s.availableCards.filter(t=>{if(s.currentDeck&&(!Le(t,s.currentDeck.environment)||!Ie(t,s.currentDeck.class))||s.filters.cost&&t.cost!==s.filters.cost||s.filters.class&&y((t==null?void 0:t.class)??(t==null?void 0:t.Class)??(t==null?void 0:t.cardClass)??(t==null?void 0:t.card_class))!==s.filters.class||s.filters.type&&y((t==null?void 0:t.type)??(t==null?void 0:t.Type)??(t==null?void 0:t.cardType)??(t==null?void 0:t.card_type))!==s.filters.type||s.filters.rarity&&y((t==null?void 0:t.rarity)??(t==null?void 0:t.Rarity)??"")!==s.filters.rarity)return!1;if(s.filters.set){const i=se(t),r=y(i.name);if(!r||r!==s.filters.set)return!1}const n=String(t.name||"").toLowerCase();return!(s.filters.search&&!n.includes(s.filters.search.toLowerCase()))}).sort(ke)}function tt(e){const t=e.querySelector(".library-card");if(!t)return null;const n=window.getComputedStyle(e),i=parseFloat(n.gap||n.rowGap||"0"),r=t.getBoundingClientRect(),c=e.getBoundingClientRect(),u=Math.max(1,Math.floor((c.width+i)/(r.width+i))),o=Math.max(1,Math.floor((c.height+i)/(r.height+i)));return Math.max(1,u*o)}function E(e=!1){const t=document.getElementById("library-grid");if(!t)return;const n=et(),i=Math.ceil(n.length/s.pagination.itemsPerPage)||1;s.pagination.currentPage>i&&(s.pagination.currentPage=i);const r=(s.pagination.currentPage-1)*s.pagination.itemsPerPage,c=n.slice(r,r+s.pagination.itemsPerPage);if(t.innerHTML=c.map(o=>{const v=o.attack??"-",b=o.health??"-",m=o.artPreview??o.ArtPreview??"",f=m?encodeURI(m):"",w=ne(o),l=`library-card${f?" has-art":""}${w?` ${w}`:""}`,g=f?` style="--library-card-art: url('${f}');"`:"";return`
        <div class="${l}" data-card-id="${o.id}"${g}>
            <button class="card-detail-btn" data-card-id="${o.id}" type="button">DETAIL</button>
            <div class="card-cost">${o.cost??"-"}</div>
            <div class="card-name">${o.name??"-"}</div>
            <div class="card-stats">${v}/${b}</div>
            <div class="card-overlay">
                <span class="add-icon">+</span>
            </div>
        </div>
        `}).join(""),!e&&c.length>0){const o=tt(t);if(o&&o!==s.pagination.itemsPerPage){s.pagination.itemsPerPage=o,s.pagination.currentPage=1,E(!0);return}}const u=document.querySelector(".library-footer .footer-center");if(u){u.innerHTML=`
            <button class="btn-secondary small btn-prev" ${s.pagination.currentPage===1?"disabled":""}><</button>
            <span class="page-indicator">${s.pagination.currentPage} / ${i}</span>
            <button class="btn-secondary small btn-next" ${s.pagination.currentPage===i?"disabled":""}>></button>
        `;const o=u.querySelector(".btn-prev"),v=u.querySelector(".btn-next");o==null||o.addEventListener("click",()=>{s.pagination.currentPage<=1||(s.pagination.currentPage-=1,E(!0))}),v==null||v.addEventListener("click",()=>{s.pagination.currentPage>=i||(s.pagination.currentPage+=1,E(!0))})}t.querySelectorAll(".library-card").forEach(o=>{o.addEventListener("click",v=>{v.target.closest(".card-detail-btn")||it(Number(o.dataset.cardId))})}),t.querySelectorAll(".card-detail-btn").forEach(o=>{o.addEventListener("click",v=>{v.stopPropagation();const b=Number(o.dataset.cardId),m=s.cardIndex.get(b);ae(m)})})}function ie(){const e=document.getElementById("deck-list");if(!e)return;const n=(Array.isArray(s.currentDeck.cards)?s.currentDeck.cards:[]).map(i=>({entry:i,card:s.cardIndex.get(Number(i.cardDefinitionId))})).filter(i=>i.card).sort((i,r)=>ke(i.card,r.card));e.innerHTML=n.map(i=>{const r=i.card.artPreview??i.card.ArtPreview??"",c=r?encodeURI(r):"",u=`mini-visual${c?" has-art":""}`,o=c?` style="background-image: url('${c}');"`:"",v=ne(i.card);return`
        <div class="deck-card-mini${v?` ${v}`:""}" data-card-id="${i.entry.cardDefinitionId}">
            <button class="mini-detail-btn" data-card-id="${i.entry.cardDefinitionId}" type="button">DETAIL</button>
            <div class="mini-cost">${i.card.cost??"-"}</div>
            <div class="${u}"${o}></div>
            <div class="mini-name">${i.card.name??"-"}</div>
            <div class="mini-count">x${i.entry.count}</div>
            <div class="mini-overlay">
                <span class="remove-icon">-</span>
            </div>
        </div>
    `}).join(""),e.querySelectorAll(".deck-card-mini").forEach(i=>{i.addEventListener("click",r=>{r.target.closest(".mini-detail-btn")||(r.stopPropagation(),rt(Number(i.dataset.cardId)))})}),e.querySelectorAll(".mini-detail-btn").forEach(i=>{i.addEventListener("click",r=>{r.stopPropagation();const c=Number(i.dataset.cardId),u=s.cardIndex.get(c);ae(u)})})}function re(){const e=document.getElementById("mana-curve");if(!e)return;const t=Array(9).fill(0);(Array.isArray(s.currentDeck.cards)?s.currentDeck.cards:[]).forEach(r=>{const c=s.cardIndex.get(Number(r.cardDefinitionId));if(!c)return;const u=Number(c.cost??0),o=Math.min(u,8);t[o]+=Number(r.count??0)});const i=Math.max(...t,1);e.innerHTML=t.map((r,c)=>`
        <div class="curve-bar-container" title="Cost ${c}: ${r}">
            <div class="curve-bar" style="height: ${r/i*100}%"></div>
            <div class="curve-label">${c===8?"8+":c}</div>
        </div>
    `).join("")}function ae(e){if(!e)return;const t=e.artPreview??e.ArtPreview??"",n=e.artDetail??e.ArtDetail??"",i=t?encodeURI(t).replace(/'/g,"%27"):"",r=n?encodeURI(n).replace(/'/g,"%27"):"";Be(e,{previewUrl:i,detailUrl:r})}function $e(){const e=document.getElementById("deck-count-display");e&&s.currentDeck&&(e.textContent=`${A(s.currentDeck)}`)}async function Se(e,t=!1){if(!e||!t&&!window.confirm("确定要删除该卡组吗？"))return;const n=L();await Re(`/api/decks/${e}?${n}`),await P(),$()}async function nt(e,t){const n=L(),i={name:"New Deck",class:y(t)||"neutral",environment:I(e)},r=await te(`/api/decks?${n}`,i),c=(r==null?void 0:r.deckId)??(r==null?void 0:r.id)??null;s.newDeckId=c,await P(),c&&await M(c)}async function st(e,t=""){const n=L(),i={code:String(e).trim()};t&&String(t).trim()&&(i.name=String(t).trim());const r=await te(`/api/decks/import?${n}`,i),c=_(r);return await P(),c}async function M(e){const t=await we(e),n=_(t)??e,i=s.newDeckId&&n&&String(s.newDeckId)===String(n);s.currentDeck={...t,environment:I(t.environment),class:y(t.class),cards:Array.isArray(t.cards)?t.cards:[]},s.currentDeck.isNew=!!i,i&&(s.newDeckId=null),Ze()}function it(e){if(!s.currentDeck)return;if(A(s.currentDeck)>=40){alert("牌组已满（最多 40 张）");return}const n=s.currentDeck.cards??[],i=n.find(r=>Number(r.cardDefinitionId)===e);if(i&&i.count>=3){alert("同名卡最多 3 张");return}i?i.count+=1:n.push({cardDefinitionId:e,count:1}),s.currentDeck.cards=n,ie(),re(),$e()}function rt(e){if(!s.currentDeck)return;const t=s.currentDeck.cards??[],n=t.findIndex(r=>Number(r.cardDefinitionId)===e);if(n===-1)return;const i=t[n];i.count>1?i.count-=1:t.splice(n,1),s.currentDeck.cards=t,ie(),re(),$e()}async function fe(){if(!s.currentDeck)return;const e=L(),t=s.currentDeck.deckId||s.currentDeck.id,n={name:s.currentDeck.name,class:s.currentDeck.class||"Neutral",environment:I(s.currentDeck.environment),cards:s.currentDeck.cards??[]};await Me(`/api/decks/${t}?${e}`,n),await P(),document.dispatchEvent(new CustomEvent("deckListUpdated")),s.currentDeck=null,$()}export{ot as initializeDeckBuilder};
