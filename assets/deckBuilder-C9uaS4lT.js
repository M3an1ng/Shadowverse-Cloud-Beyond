import{c as D,a as Le}from"./index-Ci9bg7n5.js";import{c as Ie}from"./deckPreviewModal-Cg7Vd-A8.js";import{l as $e,c as le}from"./cardDefinitionsLoader-BK4UUvCB.js";const J="open",de=[{value:"open",label:"开放环境",description:"所有卡牌集合可用"}],Y=[{value:"forestcraft",label:"妖精"},{value:"swordcraft",label:"皇家护卫"},{value:"runecraft",label:"巫师"},{value:"dragoncraft",label:"龙族"},{value:"abysscraft",label:"梦魇"},{value:"havencraft",label:"主教"},{value:"portalcraft",label:"复仇者"}],ue=[{value:"follower",label:"从者"},{value:"spell",label:"法术"},{value:"amulet",label:"护符"},{value:"crest",label:"纹章"},{value:"leader",label:"主战者"}],ve=[{value:"bronze",label:"青铜"},{value:"silver",label:"白银"},{value:"gold",label:"黄金"},{value:"legend",label:"传说"}],s={decks:[],currentDeck:null,rawCardsCache:null,availableCards:[],cardIndex:new Map,deckCreation:{mode:"create",environment:null,class:null,step:"environment",importName:"",importCode:""},filters:{cost:null,class:"",type:"",rarity:"",set:"",search:""},pagination:{currentPage:1,itemsPerPage:10},newDeckId:null};async function Q(e){const t=await fetch(e,{method:"GET"}),n=await t.json();if(!t.ok){const i=(n==null?void 0:n.message)||(n==null?void 0:n.Message)||t.statusText;throw new Error(i||`HTTP ${t.status}`)}return n}async function A(e,t){const n=await fetch(e,{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify(t??{})}),i=await n.json();if(!n.ok){const a=(i==null?void 0:i.message)||(i==null?void 0:i.Message)||n.statusText;throw new Error(a||`HTTP ${n.status}`)}return i}async function Ae(e,t){const n=await fetch(e,{method:"PUT",headers:{"Content-Type":"application/json"},body:JSON.stringify(t??{})});let i=null;try{i=await n.json()}catch{}if(!n.ok){const a=(i==null?void 0:i.message)||(i==null?void 0:i.Message)||n.statusText;throw new Error(a||`HTTP ${n.status}`)}return i}async function Pe(e){const t=await fetch(e,{method:"DELETE"});if(!t.ok){const n=await t.json().catch(()=>null),i=(n==null?void 0:n.message)||(n==null?void 0:n.Message)||t.statusText;throw new Error(i||`HTTP ${t.status}`)}}let _=null;async function st(){if(_=document.getElementById("deck-builder-container"),!_){console.error("DeckBuilder: Container not found!");return}try{await Te(),await Promise.all([x(),xe()])}catch(e){console.error("DeckBuilder: Failed to load data:",e)}I()}async function Te(){const e=D();try{await A(`/api/decks/bootstrap?${e}`,{})}catch(t){console.warn("DeckBuilder: Bootstrap failed.",t)}}async function x(){const e=D(),t=await Q(`/api/decks?${e}`);s.decks=Array.isArray(t)?t.map(n=>({...n,environment:S(n.environment)})):[]}async function xe(){const e=await $e({useCache:!0,force:!1,getCached:()=>s.rawCardsCache,setCached:n=>{s.rawCardsCache=n}}),t=Array.isArray(e)?e.filter(n=>((n==null?void 0:n.isCollectible)??(n==null?void 0:n.IsCollectible))!==!0||f((n==null?void 0:n.rarity)??(n==null?void 0:n.Rarity)??"")==="special"?!1:Number(n==null?void 0:n.id)>0&&String((n==null?void 0:n.type)||"").toLowerCase()!=="leader"):[];s.availableCards=t,s.cardIndex=new Map(t.map(n=>[Number(n.id),n]))}function P(e){return e!=null&&e.cards?e.cards.reduce((t,n)=>t+(n.count||0),0):(e==null?void 0:e.cardCount)??0}function L(e,t=0){const n=Number.parseInt(String(e??""),10);return Number.isFinite(n)?n:t}function Ne(e){return{totalGames:Math.max(0,L((e==null?void 0:e.totalGames)??(e==null?void 0:e.TotalGames),0)),totalWins:Math.max(0,L((e==null?void 0:e.totalWins)??(e==null?void 0:e.TotalWins),0)),goingFirstGames:Math.max(0,L((e==null?void 0:e.goingFirstGames)??(e==null?void 0:e.GoingFirstGames),0)),goingFirstWins:Math.max(0,L((e==null?void 0:e.goingFirstWins)??(e==null?void 0:e.GoingFirstWins),0)),goingSecondGames:Math.max(0,L((e==null?void 0:e.goingSecondGames)??(e==null?void 0:e.GoingSecondGames),0)),goingSecondWins:Math.max(0,L((e==null?void 0:e.goingSecondWins)??(e==null?void 0:e.GoingSecondWins),0))}}function Be(e){return s.decks.find(t=>String(T(t))===String(e))??null}function T(e){return(e==null?void 0:e.deckId)||(e==null?void 0:e.id)||null}function U(e,t="New Deck"){const n=e==null?void 0:e.name;return n==null?t:String(n).trim()||t}function qe(e){return(Array.isArray(e==null?void 0:e.cards)?e.cards:[]).map(n=>{const i=Number(n==null?void 0:n.cardDefinitionId),a=Number((n==null?void 0:n.count)??0),c=s.cardIndex.get(i)??null,u=Number(c==null?void 0:c.cost);return{cardDefinitionId:i,count:Number.isFinite(a)?a:0,card:c,cost:Number.isFinite(u)?u:99}}).filter(n=>n.count>0)}function Me(e,t){var a,c;if(e.cost!==t.cost)return e.cost-t.cost;const n=String(((a=e.card)==null?void 0:a.name)??e.cardDefinitionId??""),i=String(((c=t.card)==null?void 0:c.name)??t.cardDefinitionId??"");return n.localeCompare(i,void 0,{sensitivity:"base"})}function Fe(e){const t=qe(e).sort(Me),n=Array(9).fill(0),i={follower:0,spell:0,amulet:0};let a=0;return t.forEach(c=>{var v,g,b,y;const u=Math.min(Math.max(c.cost,0),8);n[u]+=c.count,a+=c.count;const o=f(((v=c.card)==null?void 0:v.type)??((g=c.card)==null?void 0:g.Type)??((b=c.card)==null?void 0:b.cardType)??((y=c.card)==null?void 0:y.card_type));(o==="follower"||o==="spell"||o==="amulet")&&(i[o]+=c.count)}),{items:t,curve:n,typeCounts:i,totalCount:a}}async function me(e){const t=D(),n=await Q(`/api/decks/${e}?${t}`),i=f((n==null?void 0:n.class)??(n==null?void 0:n.Class)??(n==null?void 0:n.cardClass)??(n==null?void 0:n.card_class));return{...n,environment:S(n==null?void 0:n.environment),class:i,cards:Array.isArray(n==null?void 0:n.cards)?n.cards:[]}}async function Re(e){const t=D(),n=await Q(`/api/decks/${e}/code?${t}`),i=(n==null?void 0:n.code)??(n==null?void 0:n.Code)??"";if(!i)throw new Error("未获取到卡组代码");return String(i)}async function _e(e){var i;if((i=navigator==null?void 0:navigator.clipboard)!=null&&i.writeText){await navigator.clipboard.writeText(e);return}const t=document.createElement("textarea");t.value=e,t.setAttribute("readonly","true"),t.style.position="fixed",t.style.top="-1000px",t.style.left="-1000px",document.body.appendChild(t),t.select();const n=document.execCommand("copy");if(document.body.removeChild(t),!n)throw new Error("当前环境不支持自动复制")}function pe(e){if(!e)return"Neutral";const t=String(e);return t.charAt(0).toUpperCase()+t.slice(1)}function f(e){return e==null?"":String(e).trim().toLowerCase()}function fe(e){const t=f((e==null?void 0:e.rarity)??(e==null?void 0:e.Rarity)??"");return!t||t==="special"?"":t==="legendary"?"rarity-legend":`rarity-${t}`}function V(e,t=""){return e==null||e===""?t:typeof e=="number"?e===0?J:String(e):f(e)}function S(e){return V(e,J)}function Oe(e){if(Array.isArray(e))return e.map(n=>V(n)).filter(Boolean);const t=V(e);return t?[t]:[]}function W(e){const t=S(e),n=de.find(i=>i.value===t);return(n==null?void 0:n.label)||"未知环境"}function be(e){const t=f(e),n=Y.find(i=>i.value===t);return(n==null?void 0:n.label)||pe(e)}function Ge(e){const t=f(e),n=ue.find(i=>i.value===t);return(n==null?void 0:n.label)||ye(t)}function je(e){const t=f(e),n=ve.find(i=>i.value===t);return(n==null?void 0:n.label)||ye(t)}function ye(e){return e?String(e).split("_").filter(Boolean).map(t=>t.charAt(0).toUpperCase()+t.slice(1)).join(" "):""}function K(e){const t=(e==null?void 0:e.card_set)??(e==null?void 0:e.cardSet)??(e==null?void 0:e.card_set_name)??(e==null?void 0:e.cardSetName)??"",n=(e==null?void 0:e.card_set_code)??(e==null?void 0:e.cardSetCode)??"",i=(e==null?void 0:e.card_set_abbr)??(e==null?void 0:e.cardSetAbbr)??"",a=(e==null?void 0:e.card_set_environment)??(e==null?void 0:e.cardSetEnvironment)??(e==null?void 0:e.card_set_environments)??(e==null?void 0:e.cardSetEnvironments)??null;return{name:t==null?"":String(t).trim(),code:n==null?"":String(n).trim(),abbr:i==null?"":String(i).trim(),environments:Oe(a)}}function He(e){return e&&e.name||""}function Ue(){const e=Array.isArray(s.availableCards)?s.availableCards:[];return s.currentDeck?e.filter(t=>!(!ge(t,s.currentDeck.environment)||!ke(t,s.currentDeck.class))):e}function z(e,t,n,i){const a=new Set;e.forEach(o=>{const v=f(t(o));v&&a.add(v)});const c=[];n.forEach(o=>{a.has(o.value)&&(c.push({value:o.value,label:o.label}),a.delete(o.value))});const u=Array.from(a).sort((o,v)=>o.localeCompare(v,void 0,{sensitivity:"base"})).map(o=>({value:o,label:i(o)}));return c.concat(u)}function ze(e){const t=new Map;return e.forEach(n=>{const i=K(n),a=i.name;if(!a)return;const c=a.toLowerCase();t.has(c)||t.set(c,{value:a,label:He(i)})}),Array.from(t.values()).sort((n,i)=>n.label.localeCompare(i.label,void 0,{numeric:!0,sensitivity:"base"})).map(n=>({value:n.value.toLowerCase(),label:n.label}))}function F(e,t){return e.map(n=>{const i=n.value??"",a=n.label??i;return`<option value="${i}" ${t&&i===t?"selected":""}>${a}</option>`}).join("")}function Ve(){const e=document.getElementById("library-class-filter"),t=document.getElementById("library-type-filter"),n=document.getElementById("library-rarity-filter"),i=document.getElementById("library-set-filter");if(!e||!t||!n||!i)return;const a=Ue(),c=z(a,l=>(l==null?void 0:l.class)??(l==null?void 0:l.Class)??(l==null?void 0:l.cardClass)??(l==null?void 0:l.card_class),Y,be),u=z(a,l=>(l==null?void 0:l.type)??(l==null?void 0:l.Type)??(l==null?void 0:l.cardType)??(l==null?void 0:l.card_type),ue,Ge),o=z(a,l=>(l==null?void 0:l.rarity)??(l==null?void 0:l.Rarity),ve,je),v=ze(a),g=new Set(c.map(l=>l.value)),b=new Set(u.map(l=>l.value)),y=new Set(o.map(l=>l.value)),h=new Set(v.map(l=>l.value));s.filters.class&&!g.has(s.filters.class)&&(s.filters.class=""),s.filters.type&&!b.has(s.filters.type)&&(s.filters.type=""),s.filters.rarity&&!y.has(s.filters.rarity)&&(s.filters.rarity=""),s.filters.set&&!h.has(s.filters.set)&&(s.filters.set=""),e.innerHTML='<option value="">ALL CLASSES</option>'+F(c,s.filters.class),t.innerHTML='<option value="">ALL TYPES</option>'+F(u,s.filters.type),n.innerHTML='<option value="">ALL RARITIES</option>'+F(o,s.filters.rarity),i.innerHTML='<option value="">ALL SETS</option>'+F(v,s.filters.set)}function ge(e,t){const n=S(t);if(n===J)return!0;const i=K(e).environments;return!i||i.length===0?!0:i.includes(n)}function ke(e,t){const n=f((e==null?void 0:e.class)??(e==null?void 0:e.Class)??(e==null?void 0:e.cardClass)??(e==null?void 0:e.card_class)),i=f(t);return!n||!i||n==="neutral"?!0:n===i}function I(){_.innerHTML=`
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
                ${s.decks.map(r=>{const d=T(r),m=U(r),p=r.cardCount??P(r),k=pe(r.class),ce=p===40;return`
                    <div class="deck-card wide-banner" data-id="${d}">
                        <div class="deck-art-bg"></div> <!-- Placeholder for Character Art -->
                        <div class="deck-content">
                            <div class="deck-info">
                                <div class="deck-class-name">${k}</div>
                                <div class="deck-env-name">${W(r.environment)}</div>
                                <div class="deck-name">${m}</div>
                                <div class="deck-status">
                                    <span class="status-indicator ${ce?"valid":"invalid"}">
                                        ${ce?"Complete":"Incomplete"}
                                    </span>
                                    <span class="deck-count">${p} / 40</span>
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
                                ${de.map(r=>`
                                    <button class="deck-option" data-env="${r.value}" type="button">
                                        <div class="deck-option-title">${r.label}</div>
                                        <div class="deck-option-desc">${r.description}</div>
                                    </button>
                                `).join("")}
                            </div>
                        </div>
                        <div class="deck-create-step hidden" data-step="class">
                            <div class="deck-create-step-title">选择职业</div>
                            <div class="deck-create-options grid">
                                ${Y.map(r=>`
                                    <button class="deck-option" data-class="${r.value}" type="button">
                                        <div class="deck-option-title">${r.label}</div>
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
                        <div class="deck-preview-toolbar-stats" id="deck-preview-toolbar-stats"></div>
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
    `;const e=new Map(s.decks.map(r=>[String(T(r)),U(r)])),t=document.getElementById("deck-create-modal"),n=document.getElementById("deck-create-close"),i=document.getElementById("deck-create-back"),a=document.getElementById("deck-create-next"),c=document.getElementById("deck-create-confirm"),u=t==null?void 0:t.querySelectorAll(".deck-mode-btn"),o=t==null?void 0:t.querySelector('[data-mode-panel="create"]'),v=t==null?void 0:t.querySelector('[data-mode-panel="import"]'),g=document.getElementById("deck-import-name-input"),b=document.getElementById("deck-import-code-input"),y=document.getElementById("deck-import-submit-btn"),h=t==null?void 0:t.querySelector('[data-step="environment"]'),l=t==null?void 0:t.querySelector('[data-step="class"]'),N=(t==null?void 0:t.closest(".deck-selection-view"))??null,w=document.getElementById("deck-code-modal"),O=document.getElementById("deck-code-close-btn"),G=document.getElementById("deck-code-copy-btn"),te=document.getElementById("deck-code-output"),ne=document.getElementById("deck-code-title"),$=document.getElementById("deck-code-status");let B=null,j="",q="";const se=()=>{s.deckCreation={mode:"create",environment:null,class:null,step:"environment",importName:"",importCode:""}},ie=r=>{N&&N.classList.toggle("deck-selection-modal-open",!!r)},E=()=>{var m;if(!t||!h||!l||!i||!a||!c)return;const r=s.deckCreation.mode==="import",d=s.deckCreation.step==="environment";o==null||o.classList.toggle("hidden",r),v==null||v.classList.toggle("hidden",!r),h.classList.toggle("hidden",r||!d),l.classList.toggle("hidden",r||d),i.disabled=r||d,a.style.display=r?"none":d?"inline-flex":"none",c.style.display=r||d?"none":"inline-flex",a.disabled=r||!s.deckCreation.environment,c.disabled=r||!s.deckCreation.class,y&&(y.disabled=!((m=s.deckCreation.importCode)!=null&&m.trim())),g&&g.value!==s.deckCreation.importName&&(g.value=s.deckCreation.importName),b&&b.value!==s.deckCreation.importCode&&(b.value=s.deckCreation.importCode),u==null||u.forEach(p=>{const k=p.getAttribute("data-mode");p.classList.toggle("selected",k===s.deckCreation.mode)}),h.querySelectorAll(".deck-option").forEach(p=>{const k=p.getAttribute("data-env");p.classList.toggle("selected",k===s.deckCreation.environment)}),l.querySelectorAll(".deck-option").forEach(p=>{const k=p.getAttribute("data-class");p.classList.toggle("selected",k===s.deckCreation.class)})},Ce=()=>{t&&(se(),ie(!0),t.classList.add("open"),t.setAttribute("aria-hidden","false"),E())},ae=()=>{w&&(w.classList.remove("open"),w.setAttribute("aria-hidden","true"),q="",$&&($.textContent=""))},H=(r,d=!1)=>{$&&($.textContent=r,$.classList.toggle("error",!!d))},Ee=(r,d)=>{!w||!te||!ne||(ne.textContent=r,te.value=d,q=d,H("代码已生成，可直接复制。"),w.classList.add("open"),w.setAttribute("aria-hidden","false"))},De=async(r,d="New Deck")=>{const m=e.get(String(r))||d,p=await Re(r);Ee(m,p)},re=Ie({idPrefix:"deck-preview",onClose:()=>{B=null},onExport:()=>{B&&De(B,j).catch(r=>{alert(`导出失败：${r.message}`)})},onCardClick:r=>{const d=s.cardIndex.get(Number(r));ee(d)}}),Se=async r=>{if(!re.isAvailable())return;const d=await me(r),m=Fe(d),p=Be(r)??d;B=r,j=U(d),re.open({titleText:j,metaText:`${be(d.class)} | ${W(d.environment)}`,cards:m.items,stats:Ne(p),exportEnabled:!0,exportVisible:!0})},M=()=>{t&&(t.classList.remove("open"),t.setAttribute("aria-hidden","true"),ie(!1),se())};document.getElementById("create-deck-card").addEventListener("click",Ce),n==null||n.addEventListener("click",M),t==null||t.addEventListener("click",r=>{r.target===t&&M()}),u==null||u.forEach(r=>{r.addEventListener("click",()=>{const d=r.getAttribute("data-mode");d!=="create"&&d!=="import"||(s.deckCreation.mode=d,E())})}),g==null||g.addEventListener("input",r=>{s.deckCreation.importName=r.target.value}),b==null||b.addEventListener("input",r=>{s.deckCreation.importCode=r.target.value,E()}),w==null||w.addEventListener("click",r=>{r.target===w&&ae()}),O==null||O.addEventListener("click",ae),G==null||G.addEventListener("click",()=>{q&&_e(q).then(()=>H("复制成功。")).catch(r=>H(`复制失败：${r.message}`,!0))}),h==null||h.querySelectorAll(".deck-option").forEach(r=>{r.addEventListener("click",()=>{const d=r.getAttribute("data-env");s.deckCreation.environment=d,E()})}),l==null||l.querySelectorAll(".deck-option").forEach(r=>{r.addEventListener("click",()=>{const d=r.getAttribute("data-class");s.deckCreation.class=d,E()})}),a==null||a.addEventListener("click",()=>{s.deckCreation.environment&&(s.deckCreation.step="class",E())}),i==null||i.addEventListener("click",()=>{s.deckCreation.step="environment",E()}),c==null||c.addEventListener("click",()=>{if(!s.deckCreation.environment||!s.deckCreation.class)return;const r=s.deckCreation.environment,d=s.deckCreation.class;M(),Qe(r,d).catch(m=>alert(`创建失败：${m.message}`))}),y==null||y.addEventListener("click",async()=>{var m,p;const r=(m=s.deckCreation.importCode)==null?void 0:m.trim();if(!r)return;const d=((p=s.deckCreation.importName)==null?void 0:p.trim())||"";y.disabled=!0;try{const k=await Ke(r,d);if(M(),k){await R(k);return}I()}catch(k){alert(`导入失败：${k.message}`),y.disabled=!1,E()}}),document.querySelectorAll(".preview-deck-btn").forEach(r=>{r.addEventListener("click",d=>{d.stopPropagation(),Se(r.dataset.id).catch(m=>alert(`预览失败：${m.message}`))})}),document.querySelectorAll(".edit-deck-btn").forEach(r=>{r.addEventListener("click",d=>{d.stopPropagation(),R(d.target.dataset.id).catch(m=>alert(`加载失败：${m.message}`))})}),document.querySelectorAll(".delete-deck-btn").forEach(r=>{r.addEventListener("click",d=>{d.stopPropagation(),we(d.target.dataset.id).catch(m=>alert(`删除失败：${m.message}`))})}),document.querySelectorAll(".deck-card.wide-banner").forEach(r=>{r.addEventListener("click",()=>{R(r.dataset.id).catch(d=>alert(`加载失败：${d.message}`))})})}function We(){s.currentDeck&&(_.innerHTML=`
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
                            <span class="total-count"><span id="deck-count-display">${P(s.currentDeck)}</span> / 40</span>
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
    `,Ve(),C(),X(),Z(),document.getElementById("card-search-input").addEventListener("input",e=>{s.filters.search=e.target.value,s.pagination.currentPage=1,C()}),document.getElementById("library-class-filter").addEventListener("change",e=>{s.filters.class=e.target.value,s.pagination.currentPage=1,C()}),document.getElementById("library-type-filter").addEventListener("change",e=>{s.filters.type=e.target.value,s.pagination.currentPage=1,C()}),document.getElementById("library-rarity-filter").addEventListener("change",e=>{s.filters.rarity=e.target.value,s.pagination.currentPage=1,C()}),document.getElementById("library-set-filter").addEventListener("change",e=>{s.filters.set=e.target.value,s.pagination.currentPage=1,C()}),document.querySelectorAll(".cost-btn").forEach(e=>{e.addEventListener("click",()=>{const t=parseInt(e.dataset.cost);s.filters.cost=s.filters.cost===t?null:t,document.querySelectorAll(".cost-btn").forEach(n=>n.classList.remove("active")),s.filters.cost&&e.classList.add("active"),s.pagination.currentPage=1,C()})}),document.getElementById("back-to-selection-btn").addEventListener("click",()=>{if(!s.currentDeck){I();return}if(P(s.currentDeck)>0){oe().catch(n=>alert(`保存失败：${n.message}`));return}const e=s.currentDeck.deckId||s.currentDeck.id,t=s.currentDeck.isNew===!0;if(s.currentDeck=null,e&&t){we(e,!0).catch(n=>alert(`删除失败：${n.message}`));return}I()}),document.getElementById("save-deck-btn").addEventListener("click",()=>{oe().catch(e=>alert(`保存失败：${e.message}`))}),document.getElementById("deck-name-input").addEventListener("change",e=>{s.currentDeck.name=e.target.value}))}function Je(){return s.availableCards.filter(t=>{if(s.currentDeck&&(!ge(t,s.currentDeck.environment)||!ke(t,s.currentDeck.class))||s.filters.cost&&t.cost!==s.filters.cost||s.filters.class&&f((t==null?void 0:t.class)??(t==null?void 0:t.Class)??(t==null?void 0:t.cardClass)??(t==null?void 0:t.card_class))!==s.filters.class||s.filters.type&&f((t==null?void 0:t.type)??(t==null?void 0:t.Type)??(t==null?void 0:t.cardType)??(t==null?void 0:t.card_type))!==s.filters.type||s.filters.rarity&&f((t==null?void 0:t.rarity)??(t==null?void 0:t.Rarity)??"")!==s.filters.rarity)return!1;if(s.filters.set){const i=K(t),a=f(i.name);if(!a||a!==s.filters.set)return!1}const n=String(t.name||"").toLowerCase();return!(s.filters.search&&!n.includes(s.filters.search.toLowerCase()))}).sort(le)}function Ye(e){const t=e.querySelector(".library-card");if(!t)return null;const n=window.getComputedStyle(e),i=parseFloat(n.gap||n.rowGap||"0"),a=t.getBoundingClientRect(),c=e.getBoundingClientRect(),u=Math.max(1,Math.floor((c.width+i)/(a.width+i))),o=Math.max(1,Math.floor((c.height+i)/(a.height+i)));return Math.max(1,u*o)}function C(e=!1){const t=document.getElementById("library-grid");if(!t)return;const n=Je(),i=Math.ceil(n.length/s.pagination.itemsPerPage)||1;s.pagination.currentPage>i&&(s.pagination.currentPage=i);const a=(s.pagination.currentPage-1)*s.pagination.itemsPerPage,c=n.slice(a,a+s.pagination.itemsPerPage);if(t.innerHTML=c.map(o=>{const v=o.attack??"-",g=o.health??"-",b=o.artPreview??o.ArtPreview??"",y=b?encodeURI(b):"",h=fe(o),l=`library-card${y?" has-art":""}${h?` ${h}`:""}`,N=y?` style="--library-card-art: url('${y}');"`:"";return`
        <div class="${l}" data-card-id="${o.id}"${N}>
            <button class="card-detail-btn" data-card-id="${o.id}" type="button">DETAIL</button>
            <div class="card-cost">${o.cost??"-"}</div>
            <div class="card-name">${o.name??"-"}</div>
            <div class="card-stats">${v}/${g}</div>
            <div class="card-overlay">
                <span class="add-icon">+</span>
            </div>
        </div>
        `}).join(""),!e&&c.length>0){const o=Ye(t);if(o&&o!==s.pagination.itemsPerPage){s.pagination.itemsPerPage=o,s.pagination.currentPage=1,C(!0);return}}const u=document.querySelector(".library-footer .footer-center");if(u){u.innerHTML=`
            <button class="btn-secondary small btn-prev" ${s.pagination.currentPage===1?"disabled":""}><</button>
            <span class="page-indicator">${s.pagination.currentPage} / ${i}</span>
            <button class="btn-secondary small btn-next" ${s.pagination.currentPage===i?"disabled":""}>></button>
        `;const o=u.querySelector(".btn-prev"),v=u.querySelector(".btn-next");o==null||o.addEventListener("click",()=>{s.pagination.currentPage<=1||(s.pagination.currentPage-=1,C(!0))}),v==null||v.addEventListener("click",()=>{s.pagination.currentPage>=i||(s.pagination.currentPage+=1,C(!0))})}t.querySelectorAll(".library-card").forEach(o=>{o.addEventListener("click",v=>{v.target.closest(".card-detail-btn")||Xe(Number(o.dataset.cardId))})}),t.querySelectorAll(".card-detail-btn").forEach(o=>{o.addEventListener("click",v=>{v.stopPropagation();const g=Number(o.dataset.cardId),b=s.cardIndex.get(g);ee(b)})})}function X(){const e=document.getElementById("deck-list");if(!e)return;const n=(Array.isArray(s.currentDeck.cards)?s.currentDeck.cards:[]).map(i=>({entry:i,card:s.cardIndex.get(Number(i.cardDefinitionId))})).filter(i=>i.card).sort((i,a)=>le(i.card,a.card));e.innerHTML=n.map(i=>{const a=i.card.artPreview??i.card.ArtPreview??"",c=a?encodeURI(a):"",u=`mini-visual${c?" has-art":""}`,o=c?` style="background-image: url('${c}');"`:"",v=fe(i.card);return`
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
    `}).join(""),e.querySelectorAll(".deck-card-mini").forEach(i=>{i.addEventListener("click",a=>{a.target.closest(".mini-detail-btn")||(a.stopPropagation(),Ze(Number(i.dataset.cardId)))})}),e.querySelectorAll(".mini-detail-btn").forEach(i=>{i.addEventListener("click",a=>{a.stopPropagation();const c=Number(i.dataset.cardId),u=s.cardIndex.get(c);ee(u)})})}function Z(){const e=document.getElementById("mana-curve");if(!e)return;const t=Array(9).fill(0);(Array.isArray(s.currentDeck.cards)?s.currentDeck.cards:[]).forEach(a=>{const c=s.cardIndex.get(Number(a.cardDefinitionId));if(!c)return;const u=Number(c.cost??0),o=Math.min(u,8);t[o]+=Number(a.count??0)});const i=Math.max(...t,1);e.innerHTML=t.map((a,c)=>`
        <div class="curve-bar-container" title="Cost ${c}: ${a}">
            <div class="curve-bar" style="height: ${a/i*100}%"></div>
            <div class="curve-label">${c===8?"8+":c}</div>
        </div>
    `).join("")}function ee(e){if(!e)return;const t=e.artPreview??e.ArtPreview??"",n=e.artDetail??e.ArtDetail??"",i=t?encodeURI(t).replace(/'/g,"%27"):"",a=n?encodeURI(n).replace(/'/g,"%27"):"";Le(e,{previewUrl:i,detailUrl:a})}function he(){const e=document.getElementById("deck-count-display");e&&s.currentDeck&&(e.textContent=`${P(s.currentDeck)}`)}async function we(e,t=!1){if(!e||!t&&!window.confirm("确定要删除该卡组吗？"))return;const n=D();await Pe(`/api/decks/${e}?${n}`),await x(),I()}async function Qe(e,t){const n=D(),i={name:"New Deck",class:f(t)||"neutral",environment:S(e)},a=await A(`/api/decks?${n}`,i),c=(a==null?void 0:a.deckId)??(a==null?void 0:a.id)??null;s.newDeckId=c,await x(),c&&await R(c)}async function Ke(e,t=""){const n=D(),i={code:String(e).trim()};t&&String(t).trim()&&(i.name=String(t).trim());const a=await A(`/api/decks/import?${n}`,i),c=T(a);return await x(),c}async function R(e){const t=await me(e),n=T(t)??e,i=s.newDeckId&&n&&String(s.newDeckId)===String(n);s.currentDeck={...t,environment:S(t.environment),class:f(t.class),cards:Array.isArray(t.cards)?t.cards:[]},s.currentDeck.isNew=!!i,i&&(s.newDeckId=null),We()}function Xe(e){if(!s.currentDeck)return;if(P(s.currentDeck)>=40){alert("牌组已满（最多 40 张）");return}const n=s.currentDeck.cards??[],i=n.find(a=>Number(a.cardDefinitionId)===e);if(i&&i.count>=3){alert("同名卡最多 3 张");return}i?i.count+=1:n.push({cardDefinitionId:e,count:1}),s.currentDeck.cards=n,X(),Z(),he()}function Ze(e){if(!s.currentDeck)return;const t=s.currentDeck.cards??[],n=t.findIndex(a=>Number(a.cardDefinitionId)===e);if(n===-1)return;const i=t[n];i.count>1?i.count-=1:t.splice(n,1),s.currentDeck.cards=t,X(),Z(),he()}async function oe(){if(!s.currentDeck)return;const e=D(),t=s.currentDeck.deckId||s.currentDeck.id,n=s.currentDeck.isNew===!0||!t,i={name:s.currentDeck.name,class:s.currentDeck.class||"Neutral",environment:S(s.currentDeck.environment),cards:s.currentDeck.cards??[]},a=c=>{const u=String((c==null?void 0:c.message)||"").toLowerCase();return u.includes("deck not found")||u.includes("target deck not found")||u.includes("not found")};if(t)try{await Ae(`/api/decks/${t}?${e}`,i)}catch(c){if(!a(c)||!n)throw c;await A(`/api/decks?${e}`,i)}else await A(`/api/decks?${e}`,i);await x(),document.dispatchEvent(new CustomEvent("deckListUpdated")),s.currentDeck=null,I()}export{st as initializeDeckBuilder};
