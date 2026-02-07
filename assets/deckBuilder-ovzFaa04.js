import{d as y,c as Q}from"./index-BTkQcdl_.js";const $="open",R=[{value:"open",label:"开放环境",description:"所有卡牌集合可用"}],A=[{value:"forestcraft",label:"妖精"},{value:"swordcraft",label:"皇家护卫"},{value:"runecraft",label:"巫师"},{value:"dragoncraft",label:"龙族"},{value:"abysscraft",label:"梦魇"},{value:"havencraft",label:"主教"},{value:"portalcraft",label:"复仇者"}],q=[{value:"follower",label:"从者"},{value:"spell",label:"法术"},{value:"amulet",label:"护符"},{value:"crest",label:"纹章"},{value:"leader",label:"主战者"}],_=[{value:"bronze",label:"青铜"},{value:"silver",label:"白银"},{value:"gold",label:"黄金"},{value:"legend",label:"传说"}],s={decks:[],currentDeck:null,availableCards:[],cardIndex:new Map,deckCreation:{environment:null,class:null,step:"environment"},filters:{cost:null,class:"",type:"",rarity:"",set:"",search:""},pagination:{currentPage:1,itemsPerPage:10},newDeckId:null};async function P(e){const n=await fetch(e,{method:"GET"}),t=await n.json();if(!n.ok){const i=(t==null?void 0:t.message)||(t==null?void 0:t.Message)||n.statusText;throw new Error(i||`HTTP ${n.status}`)}return t}async function O(e,n){const t=await fetch(e,{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify(n??{})}),i=await t.json();if(!t.ok){const r=(i==null?void 0:i.message)||(i==null?void 0:i.Message)||t.statusText;throw new Error(r||`HTTP ${t.status}`)}return i}async function K(e,n){const t=await fetch(e,{method:"PUT",headers:{"Content-Type":"application/json"},body:JSON.stringify(n??{})});let i=null;try{i=await t.json()}catch{}if(!t.ok){const r=(i==null?void 0:i.message)||(i==null?void 0:i.Message)||t.statusText;throw new Error(r||`HTTP ${t.status}`)}return i}async function W(e){const n=await fetch(e,{method:"DELETE"});if(!n.ok){const t=await n.json().catch(()=>null),i=(t==null?void 0:t.message)||(t==null?void 0:t.Message)||n.statusText;throw new Error(i||`HTTP ${n.status}`)}}let E=null;async function pe(){if(E=document.getElementById("deck-builder-container"),!E){console.error("DeckBuilder: Container not found!");return}try{await X(),await Promise.all([D(),Z()])}catch(e){console.error("DeckBuilder: Failed to load data:",e)}h()}async function X(){const e=y();try{await O(`/api/decks/bootstrap?${e}`,{})}catch(n){console.warn("DeckBuilder: Bootstrap failed.",n)}}async function D(){const e=y(),n=await P(`/api/decks?${e}`);s.decks=Array.isArray(n)?n.map(t=>({...t,environment:k(t.environment)})):[]}async function Z(){const e=await P("/api/cards"),n=Array.isArray(e)?e.filter(t=>((t==null?void 0:t.isCollectible)??(t==null?void 0:t.IsCollectible))!==!0||f((t==null?void 0:t.rarity)??(t==null?void 0:t.Rarity)??"")==="special"?!1:Number(t==null?void 0:t.id)>0&&String((t==null?void 0:t.type)||"").toLowerCase()!=="leader"):[];s.availableCards=n,s.cardIndex=new Map(n.map(t=>[Number(t.id),t]))}function g(e){return e!=null&&e.cards?e.cards.reduce((n,t)=>n+(t.count||0),0):(e==null?void 0:e.cardCount)??0}function j(e){if(!e)return"Neutral";const n=String(e);return n.charAt(0).toUpperCase()+n.slice(1)}function f(e){return e==null?"":String(e).trim().toLowerCase()}function H(e){const n=f((e==null?void 0:e.rarity)??(e==null?void 0:e.Rarity)??"");return!n||n==="special"?"":n==="legendary"?"rarity-legend":`rarity-${n}`}function I(e,n=""){return e==null||e===""?n:typeof e=="number"?e===0?$:String(e):f(e)}function k(e){return I(e,$)}function ee(e){if(Array.isArray(e))return e.map(t=>I(t)).filter(Boolean);const n=I(e);return n?[n]:[]}function F(e){const n=k(e),t=R.find(i=>i.value===n);return(t==null?void 0:t.label)||"未知环境"}function te(e){const n=f(e),t=A.find(i=>i.value===n);return(t==null?void 0:t.label)||j(e)}function ne(e){const n=f(e),t=q.find(i=>i.value===n);return(t==null?void 0:t.label)||U(n)}function se(e){const n=f(e),t=_.find(i=>i.value===n);return(t==null?void 0:t.label)||U(n)}function U(e){return e?String(e).split("_").filter(Boolean).map(n=>n.charAt(0).toUpperCase()+n.slice(1)).join(" "):""}function T(e){const n=(e==null?void 0:e.card_set)??(e==null?void 0:e.cardSet)??(e==null?void 0:e.card_set_name)??(e==null?void 0:e.cardSetName)??"",t=(e==null?void 0:e.card_set_code)??(e==null?void 0:e.cardSetCode)??"",i=(e==null?void 0:e.card_set_abbr)??(e==null?void 0:e.cardSetAbbr)??"",r=(e==null?void 0:e.card_set_environment)??(e==null?void 0:e.cardSetEnvironment)??(e==null?void 0:e.card_set_environments)??(e==null?void 0:e.cardSetEnvironments)??null;return{name:n==null?"":String(n).trim(),code:t==null?"":String(t).trim(),abbr:i==null?"":String(i).trim(),environments:ee(r)}}function ie(e){return e&&e.name||""}function re(){const e=Array.isArray(s.availableCards)?s.availableCards:[];return s.currentDeck?e.filter(n=>!(!z(n,s.currentDeck.environment)||!V(n,s.currentDeck.class))):e}function L(e,n,t,i){const r=new Set;e.forEach(c=>{const v=f(n(c));v&&r.add(v)});const l=[];t.forEach(c=>{r.has(c.value)&&(l.push({value:c.value,label:c.label}),r.delete(c.value))});const d=Array.from(r).sort((c,v)=>c.localeCompare(v,void 0,{sensitivity:"base"})).map(c=>({value:c,label:i(c)}));return l.concat(d)}function ae(e){const n=new Map;return e.forEach(t=>{const i=T(t),r=i.name;if(!r)return;const l=r.toLowerCase();n.has(l)||n.set(l,{value:r,label:ie(i)})}),Array.from(n.values()).sort((t,i)=>t.label.localeCompare(i.label,void 0,{numeric:!0,sensitivity:"base"})).map(t=>({value:t.value.toLowerCase(),label:t.label}))}function C(e,n){return e.map(t=>{const i=t.value??"",r=t.label??i;return`<option value="${i}" ${n&&i===n?"selected":""}>${r}</option>`}).join("")}function ce(){const e=document.getElementById("library-class-filter"),n=document.getElementById("library-type-filter"),t=document.getElementById("library-rarity-filter"),i=document.getElementById("library-set-filter");if(!e||!n||!t||!i)return;const r=re(),l=L(r,a=>(a==null?void 0:a.class)??(a==null?void 0:a.Class)??(a==null?void 0:a.cardClass)??(a==null?void 0:a.card_class),A,te),d=L(r,a=>(a==null?void 0:a.type)??(a==null?void 0:a.Type)??(a==null?void 0:a.cardType)??(a==null?void 0:a.card_type),q,ne),c=L(r,a=>(a==null?void 0:a.rarity)??(a==null?void 0:a.Rarity),_,se),v=ae(r),b=new Set(l.map(a=>a.value)),m=new Set(d.map(a=>a.value)),o=new Set(c.map(a=>a.value)),u=new Set(v.map(a=>a.value));s.filters.class&&!b.has(s.filters.class)&&(s.filters.class=""),s.filters.type&&!m.has(s.filters.type)&&(s.filters.type=""),s.filters.rarity&&!o.has(s.filters.rarity)&&(s.filters.rarity=""),s.filters.set&&!u.has(s.filters.set)&&(s.filters.set=""),e.innerHTML='<option value="">ALL CLASSES</option>'+C(l,s.filters.class),n.innerHTML='<option value="">ALL TYPES</option>'+C(d,s.filters.type),t.innerHTML='<option value="">ALL RARITIES</option>'+C(c,s.filters.rarity),i.innerHTML='<option value="">ALL SETS</option>'+C(v,s.filters.set)}function z(e,n){const t=k(n);if(t===$)return!0;const i=T(e).environments;return!i||i.length===0?!0:i.includes(t)}function V(e,n){const t=f((e==null?void 0:e.class)??(e==null?void 0:e.Class)??(e==null?void 0:e.cardClass)??(e==null?void 0:e.card_class)),i=f(n);return!t||!i||t==="neutral"?!0:t===i}function h(){E.innerHTML=`
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
                ${s.decks.map(o=>{const u=o.deckId||o.id,a=o.cardCount??g(o),w=j(o.class),x=a===40;return`
                    <div class="deck-card wide-banner" data-id="${u}">
                        <div class="deck-art-bg"></div> <!-- Placeholder for Character Art -->
                        <div class="deck-content">
                            <div class="deck-info">
                                <div class="deck-class-name">${w}</div>
                                <div class="deck-env-name">${F(o.environment)}</div>
                                <div class="deck-name">${o.name}</div>
                                <div class="deck-status">
                                    <span class="status-indicator ${x?"valid":"invalid"}">
                                        ${x?"Complete":"Incomplete"}
                                    </span>
                                    <span class="deck-count">${a} / 40</span>
                                </div>
                            </div>
                            <div class="deck-emblem">
                                <div class="emblem-icon"></div>
                            </div>
                        </div>
                        <div class="deck-actions-overlay">
                            <button class="deck-action-btn edit-deck-btn" data-id="${u}">Edit Deck</button>
                            <button class="deck-action-btn delete-deck-btn" data-id="${u}">Delete</button>
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
                    <div class="deck-create-step" data-step="environment">
                        <div class="deck-create-step-title">选择环境</div>
                        <div class="deck-create-options">
                            ${R.map(o=>`
                                <button class="deck-option" data-env="${o.value}" type="button">
                                    <div class="deck-option-title">${o.label}</div>
                                    <div class="deck-option-desc">${o.description}</div>
                                </button>
                            `).join("")}
                        </div>
                    </div>
                    <div class="deck-create-step hidden" data-step="class">
                        <div class="deck-create-step-title">选择职业</div>
                        <div class="deck-create-options grid">
                            ${A.map(o=>`
                                <button class="deck-option" data-class="${o.value}" type="button">
                                    <div class="deck-option-title">${o.label}</div>
                                </button>
                            `).join("")}
                        </div>
                    </div>
                    <div class="deck-create-actions">
                        <button class="btn-secondary" id="deck-create-back" type="button">上一步</button>
                        <button class="btn-primary" id="deck-create-next" type="button">下一步</button>
                        <button class="btn-primary" id="deck-create-confirm" type="button">创建</button>
                    </div>
                </div>
            </div>
        </div>
    `;const e=document.getElementById("deck-create-modal"),n=document.getElementById("deck-create-close"),t=document.getElementById("deck-create-back"),i=document.getElementById("deck-create-next"),r=document.getElementById("deck-create-confirm"),l=e==null?void 0:e.querySelector('[data-step="environment"]'),d=e==null?void 0:e.querySelector('[data-step="class"]'),c=()=>{s.deckCreation={environment:null,class:null,step:"environment"}},v=()=>{if(!e||!l||!d||!t||!i||!r)return;const o=s.deckCreation.step==="environment";l.classList.toggle("hidden",!o),d.classList.toggle("hidden",o),t.disabled=o,i.style.display=o?"inline-flex":"none",r.style.display=o?"none":"inline-flex",i.disabled=!s.deckCreation.environment,r.disabled=!s.deckCreation.class,l.querySelectorAll(".deck-option").forEach(u=>{const a=u.getAttribute("data-env");u.classList.toggle("selected",a===s.deckCreation.environment)}),d.querySelectorAll(".deck-option").forEach(u=>{const a=u.getAttribute("data-class");u.classList.toggle("selected",a===s.deckCreation.class)})},b=()=>{e&&(c(),e.classList.add("open"),e.setAttribute("aria-hidden","false"),v())},m=()=>{e&&(e.classList.remove("open"),e.setAttribute("aria-hidden","true"),c())};document.getElementById("create-deck-card").addEventListener("click",b),n==null||n.addEventListener("click",m),e==null||e.addEventListener("click",o=>{o.target===e&&m()}),l==null||l.querySelectorAll(".deck-option").forEach(o=>{o.addEventListener("click",()=>{const u=o.getAttribute("data-env");s.deckCreation.environment=u,v()})}),d==null||d.querySelectorAll(".deck-option").forEach(o=>{o.addEventListener("click",()=>{const u=o.getAttribute("data-class");s.deckCreation.class=u,v()})}),i==null||i.addEventListener("click",()=>{s.deckCreation.environment&&(s.deckCreation.step="class",v())}),t==null||t.addEventListener("click",()=>{s.deckCreation.step="environment",v()}),r==null||r.addEventListener("click",()=>{if(!s.deckCreation.environment||!s.deckCreation.class)return;const o=s.deckCreation.environment,u=s.deckCreation.class;m(),de(o,u).catch(a=>alert(`创建失败：${a.message}`))}),document.querySelectorAll(".edit-deck-btn").forEach(o=>{o.addEventListener("click",u=>{u.stopPropagation(),S(u.target.dataset.id).catch(a=>alert(`加载失败：${a.message}`))})}),document.querySelectorAll(".delete-deck-btn").forEach(o=>{o.addEventListener("click",u=>{u.stopPropagation(),Y(u.target.dataset.id).catch(a=>alert(`删除失败：${a.message}`))})}),document.querySelectorAll(".deck-card.wide-banner").forEach(o=>{o.addEventListener("click",()=>{S(o.dataset.id).catch(u=>alert(`加载失败：${u.message}`))})})}function le(){s.currentDeck&&(E.innerHTML=`
        <div class="deck-builder-view vertical-layout">
            <!-- Top Section: Deck Info & Contents -->
            <div class="deck-section">
                <div class="deck-header-row">
                    <div class="deck-identity">
                        <div class="class-icon-small"></div>
                        <input type="text" class="deck-name-input" value="${s.currentDeck.name}" id="deck-name-input">
                        <button class="btn-icon edit-name-btn">✎</button>
                        <div class="deck-env-tag">${F(s.currentDeck.environment)}</div>
                    </div>
                    
                    <div class="mana-curve-mini" id="mana-curve">
                        <!-- Mana curve bars -->
                    </div>

                    <div class="deck-meta-controls">
                        <div class="deck-counts">
                            <span class="total-count"><span id="deck-count-display">${g(s.currentDeck)}</span> / 40</span>
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
    `,ce(),p(),N(),B(),document.getElementById("card-search-input").addEventListener("input",e=>{s.filters.search=e.target.value,s.pagination.currentPage=1,p()}),document.getElementById("library-class-filter").addEventListener("change",e=>{s.filters.class=e.target.value,s.pagination.currentPage=1,p()}),document.getElementById("library-type-filter").addEventListener("change",e=>{s.filters.type=e.target.value,s.pagination.currentPage=1,p()}),document.getElementById("library-rarity-filter").addEventListener("change",e=>{s.filters.rarity=e.target.value,s.pagination.currentPage=1,p()}),document.getElementById("library-set-filter").addEventListener("change",e=>{s.filters.set=e.target.value,s.pagination.currentPage=1,p()}),document.querySelectorAll(".cost-btn").forEach(e=>{e.addEventListener("click",()=>{const n=parseInt(e.dataset.cost);s.filters.cost=s.filters.cost===n?null:n,document.querySelectorAll(".cost-btn").forEach(t=>t.classList.remove("active")),s.filters.cost&&e.classList.add("active"),s.pagination.currentPage=1,p()})}),document.getElementById("back-to-selection-btn").addEventListener("click",()=>{if(!s.currentDeck){h();return}if(g(s.currentDeck)>0){M().catch(t=>alert(`保存失败：${t.message}`));return}const e=s.currentDeck.deckId||s.currentDeck.id,n=s.currentDeck.isNew===!0;if(s.currentDeck=null,e&&n){Y(e,!0).catch(t=>alert(`删除失败：${t.message}`));return}h()}),document.getElementById("save-deck-btn").addEventListener("click",()=>{M().catch(e=>alert(`保存失败：${e.message}`))}),document.getElementById("deck-name-input").addEventListener("change",e=>{s.currentDeck.name=e.target.value}))}function oe(){return s.availableCards.filter(n=>{if(s.currentDeck&&(!z(n,s.currentDeck.environment)||!V(n,s.currentDeck.class))||s.filters.cost&&n.cost!==s.filters.cost||s.filters.class&&f((n==null?void 0:n.class)??(n==null?void 0:n.Class)??(n==null?void 0:n.cardClass)??(n==null?void 0:n.card_class))!==s.filters.class||s.filters.type&&f((n==null?void 0:n.type)??(n==null?void 0:n.Type)??(n==null?void 0:n.cardType)??(n==null?void 0:n.card_type))!==s.filters.type||s.filters.rarity&&f((n==null?void 0:n.rarity)??(n==null?void 0:n.Rarity)??"")!==s.filters.rarity)return!1;if(s.filters.set){const i=T(n),r=f(i.name);if(!r||r!==s.filters.set)return!1}const t=String(n.name||"").toLowerCase();return!(s.filters.search&&!t.includes(s.filters.search.toLowerCase()))}).sort((n,t)=>{const i=Number(n.cost??0),r=Number(t.cost??0);if(i!==r)return i-r;const l=String(n.name??""),d=String(t.name??""),c=l.localeCompare(d,void 0,{numeric:!0,sensitivity:"base"});return c!==0?c:Number(n.id??0)-Number(t.id??0)})}function ue(e){const n=e.querySelector(".library-card");if(!n)return null;const t=window.getComputedStyle(e),i=parseFloat(t.gap||t.rowGap||"0"),r=n.getBoundingClientRect(),l=e.getBoundingClientRect(),d=Math.max(1,Math.floor((l.width+i)/(r.width+i))),c=Math.max(1,Math.floor((l.height+i)/(r.height+i)));return Math.max(1,d*c)}function p(e=!1){const n=document.getElementById("library-grid");if(!n)return;const t=oe(),i=Math.ceil(t.length/s.pagination.itemsPerPage)||1;s.pagination.currentPage>i&&(s.pagination.currentPage=i);const r=(s.pagination.currentPage-1)*s.pagination.itemsPerPage,l=t.slice(r,r+s.pagination.itemsPerPage);if(n.innerHTML=l.map(c=>{const v=c.attack??"-",b=c.health??"-",m=c.artPreview??c.ArtPreview??"",o=m?encodeURI(m):"",u=H(c),a=`library-card${o?" has-art":""}${u?` ${u}`:""}`,w=o?` style="--library-card-art: url('${o}');"`:"";return`
        <div class="${a}" data-card-id="${c.id}"${w}>
            <button class="card-detail-btn" data-card-id="${c.id}" type="button">DETAIL</button>
            <div class="card-cost">${c.cost??"-"}</div>
            <div class="card-name">${c.name??"-"}</div>
            <div class="card-stats">${v}/${b}</div>
            <div class="card-overlay">
                <span class="add-icon">+</span>
            </div>
        </div>
        `}).join(""),!e&&l.length>0){const c=ue(n);if(c&&c!==s.pagination.itemsPerPage){s.pagination.itemsPerPage=c,s.pagination.currentPage=1,p(!0);return}}const d=document.querySelector(".library-footer .footer-center");if(d){d.innerHTML=`
            <button class="btn-secondary small btn-prev" ${s.pagination.currentPage===1?"disabled":""}><</button>
            <span class="page-indicator">${s.pagination.currentPage} / ${i}</span>
            <button class="btn-secondary small btn-next" ${s.pagination.currentPage===i?"disabled":""}>></button>
        `;const c=d.querySelector(".btn-prev"),v=d.querySelector(".btn-next");c==null||c.addEventListener("click",()=>{s.pagination.currentPage<=1||(s.pagination.currentPage-=1,p(!0))}),v==null||v.addEventListener("click",()=>{s.pagination.currentPage>=i||(s.pagination.currentPage+=1,p(!0))})}n.querySelectorAll(".library-card").forEach(c=>{c.addEventListener("click",v=>{v.target.closest(".card-detail-btn")||ve(Number(c.dataset.cardId))})}),n.querySelectorAll(".card-detail-btn").forEach(c=>{c.addEventListener("click",v=>{v.stopPropagation();const b=Number(c.dataset.cardId),m=s.cardIndex.get(b);G(m)})})}function N(){const e=document.getElementById("deck-list");if(!e)return;const t=(Array.isArray(s.currentDeck.cards)?s.currentDeck.cards:[]).map(i=>({entry:i,card:s.cardIndex.get(Number(i.cardDefinitionId))})).filter(i=>i.card).sort((i,r)=>(i.card.cost??0)-(r.card.cost??0));e.innerHTML=t.map(i=>{const r=i.card.artPreview??i.card.ArtPreview??"",l=r?encodeURI(r):"",d=`mini-visual${l?" has-art":""}`,c=l?` style="background-image: url('${l}');"`:"",v=H(i.card);return`
        <div class="deck-card-mini${v?` ${v}`:""}" data-card-id="${i.entry.cardDefinitionId}">
            <button class="mini-detail-btn" data-card-id="${i.entry.cardDefinitionId}" type="button">DETAIL</button>
            <div class="mini-cost">${i.card.cost??"-"}</div>
            <div class="${d}"${c}></div>
            <div class="mini-name">${i.card.name??"-"}</div>
            <div class="mini-count">x${i.entry.count}</div>
            <div class="mini-overlay">
                <span class="remove-icon">-</span>
            </div>
        </div>
    `}).join(""),e.querySelectorAll(".deck-card-mini").forEach(i=>{i.addEventListener("click",r=>{r.target.closest(".mini-detail-btn")||(r.stopPropagation(),fe(Number(i.dataset.cardId)))})}),e.querySelectorAll(".mini-detail-btn").forEach(i=>{i.addEventListener("click",r=>{r.stopPropagation();const l=Number(i.dataset.cardId),d=s.cardIndex.get(l);G(d)})})}function B(){const e=document.getElementById("mana-curve");if(!e)return;const n=Array(9).fill(0);(Array.isArray(s.currentDeck.cards)?s.currentDeck.cards:[]).forEach(r=>{const l=s.cardIndex.get(Number(r.cardDefinitionId));if(!l)return;const d=Number(l.cost??0),c=Math.min(d,8);n[c]+=Number(r.count??0)});const i=Math.max(...n,1);e.innerHTML=n.map((r,l)=>`
        <div class="curve-bar-container" title="Cost ${l}: ${r}">
            <div class="curve-bar" style="height: ${r/i*100}%"></div>
            <div class="curve-label">${l===8?"8+":l}</div>
        </div>
    `).join("")}function G(e){if(!e)return;const n=e.artPreview??e.ArtPreview??"",t=e.artDetail??e.ArtDetail??"",i=n?encodeURI(n).replace(/'/g,"%27"):"",r=t?encodeURI(t).replace(/'/g,"%27"):"";Q(e,{previewUrl:i,detailUrl:r})}function J(){const e=document.getElementById("deck-count-display");e&&s.currentDeck&&(e.textContent=`${g(s.currentDeck)}`)}async function Y(e,n=!1){if(!e||!n&&!window.confirm("确定要删除该卡组吗？"))return;const t=y();await W(`/api/decks/${e}?${t}`),await D(),h()}async function de(e,n){const t=y(),i={name:"New Deck",class:f(n)||"neutral",environment:k(e)},r=await O(`/api/decks?${t}`,i),l=(r==null?void 0:r.deckId)??(r==null?void 0:r.id)??null;s.newDeckId=l,await D(),l&&await S(l)}async function S(e){const n=y(),t=await P(`/api/decks/${e}?${n}`),i=f((t==null?void 0:t.class)??(t==null?void 0:t.Class)??(t==null?void 0:t.cardClass)??(t==null?void 0:t.card_class)),r=(t==null?void 0:t.deckId)??(t==null?void 0:t.id)??e,l=s.newDeckId&&r&&String(s.newDeckId)===String(r);s.currentDeck={...t,environment:k(t.environment),class:i,cards:Array.isArray(t.cards)?t.cards:[]},s.currentDeck.isNew=!!l,l&&(s.newDeckId=null),le()}function ve(e){if(!s.currentDeck)return;if(g(s.currentDeck)>=40){alert("牌组已满（最多 40 张）");return}const t=s.currentDeck.cards??[],i=t.find(r=>Number(r.cardDefinitionId)===e);if(i&&i.count>=3){alert("同名卡最多 3 张");return}i?i.count+=1:t.push({cardDefinitionId:e,count:1}),s.currentDeck.cards=t,N(),B(),J()}function fe(e){if(!s.currentDeck)return;const n=s.currentDeck.cards??[],t=n.findIndex(r=>Number(r.cardDefinitionId)===e);if(t===-1)return;const i=n[t];i.count>1?i.count-=1:n.splice(t,1),s.currentDeck.cards=n,N(),B(),J()}async function M(){if(!s.currentDeck)return;const e=y(),n=s.currentDeck.deckId||s.currentDeck.id,t={name:s.currentDeck.name,class:s.currentDeck.class||"Neutral",environment:k(s.currentDeck.environment),cards:s.currentDeck.cards??[]};await K(`/api/decks/${n}?${e}`,t),await D(),document.dispatchEvent(new CustomEvent("deckListUpdated")),s.currentDeck=null,h()}export{pe as initializeDeckBuilder};
