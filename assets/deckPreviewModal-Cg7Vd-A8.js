const H={follower:"随从",spell:"法术",amulet:"护符"};function a(n,e=0){const o=Number.parseInt(String(n??""),10);return Number.isFinite(o)?o:e}function b(n,e=0){const o=Number(n);return Number.isFinite(o)?o:e}function C(n){return String(n??"").trim().toLowerCase()}function I(n){return String(n??"").replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/\"/g,"&quot;").replace(/'/g,"&#39;")}function h(n,e){const o=a(e,0);return o<=0?"--":`${(a(n,0)/o*100).toFixed(1)}%`}function F(n){const e=C(n);return H[e]||"未知"}function M(n){const e=C(n);return e?e==="legendary"?"rarity-legendary":`rarity-${e}`:""}function N(n){const e=(n==null?void 0:n.card)??null,o=a((n==null?void 0:n.cardDefinitionId)??(e==null?void 0:e.id)??(e==null?void 0:e.Id)??0,0),r=a(n==null?void 0:n.count,0),i=a((n==null?void 0:n.cost)??(e==null?void 0:e.cost)??(e==null?void 0:e.Cost)??99,99),t=String((n==null?void 0:n.name)??(e==null?void 0:e.name)??(e==null?void 0:e.Name)??`未知卡牌 #${o}`),c=String((n==null?void 0:n.type)??(e==null?void 0:e.type)??(e==null?void 0:e.Type)??"unknown"),s=String((n==null?void 0:n.rarity)??(e==null?void 0:e.rarity)??(e==null?void 0:e.Rarity)??""),y=String((n==null?void 0:n.artPreview)??(e==null?void 0:e.artPreview)??(e==null?void 0:e.ArtPreview)??"");return{cardDefinitionId:o,count:r,cost:i,name:t,type:c,rarity:s,artPreview:y}}function T(n){const e=(Array.isArray(n)?n:[]).map(N).filter(t=>t.cardDefinitionId>0&&t.count>0).sort((t,c)=>{if(t.cost!==c.cost)return t.cost-c.cost;const s=String(t.name||"").localeCompare(String(c.name||""),void 0,{sensitivity:"base"});return s!==0?s:t.cardDefinitionId-c.cardDefinitionId}),o=Array(9).fill(0),r={follower:0,spell:0,amulet:0};let i=0;return e.forEach(t=>{const c=Math.min(Math.max(t.cost,0),8);o[c]+=t.count,i+=t.count;const s=C(t.type);(s==="follower"||s==="spell"||s==="amulet")&&(r[s]+=t.count)}),{items:e,curve:o,typeCounts:r,totalCount:i}}function G(n){const e=Array.isArray(n)?n:Array(9).fill(0),o=Math.max(...e,1);return e.map((r,i)=>`
        <div class="deck-preview-curve-col" title="${i===8?"8+":i} 费: ${r}">
            <div class="deck-preview-curve-bar" style="height: ${r/o*100}%"></div>
            <div class="deck-preview-curve-label">${i===8?"8+":i}</div>
        </div>
    `).join("")}function W(n){return!Array.isArray(n)||n.length===0?'<div class="deck-preview-empty">当前卡组没有可预览的卡牌。</div>':n.map(e=>{const o=e.artPreview?encodeURI(e.artPreview).replace(/'/g,"%27"):"",r=o?` style="--deck-preview-art: url('${o}');"`:"",i=M(e.rarity),t=`deck-preview-card${o?" has-art":""}${i?` ${i}`:""}`,c=Number.isFinite(e.cost)&&e.cost>=0&&e.cost<99?e.cost:"?";return`
            <button class="${t}" type="button" data-card-id="${e.cardDefinitionId}"${r}>
                <span class="deck-preview-card-cost">${c}</span>
                <span class="deck-preview-card-count">x${e.count}</span>
                <span class="deck-preview-card-name">${I(e.name)}</span>
                <span class="deck-preview-card-type">${I(F(e.type))}</span>
            </button>
        `}).join("")}function x(n){const e=a(n==null?void 0:n.totalGames,0),o=a(n==null?void 0:n.totalWins,0),r=a(n==null?void 0:n.goingFirstGames,0),i=a(n==null?void 0:n.goingFirstWins,0),t=a(n==null?void 0:n.goingSecondGames,0),c=a(n==null?void 0:n.goingSecondWins,0);return`
        <div class="deck-preview-toolbar-stats-title">胜率统计</div>
        <div class="deck-preview-toolbar-stats-line primary">
            <span>总对局 ${e}</span>
            <span>总胜率 ${h(o,e)}</span>
        </div>
        <div class="deck-preview-toolbar-stats-line secondary">
            <span>先手 ${r}局 / ${h(i,r)}</span>
            <span>后手 ${t}局 / ${h(c,t)}</span>
        </div>
    `}function R({idPrefix:n,onExport:e=null,onCardClick:o=null,onClose:r=null}={}){const i=u=>document.getElementById(`${n}-${u}`),t=i("modal"),c=i("title"),s=i("meta"),y=i("curve"),A=i("toolbar-stats"),E=i("counts"),f=i("cards"),p=i("export-btn"),L=i("close-btn"),m=!!(t&&c&&s&&y&&E&&f),d=[],S=()=>{m&&(t.classList.remove("open"),t.setAttribute("aria-hidden","true"),typeof r=="function"&&r())},P=()=>!!(m&&t.classList.contains("open"));if(m){const u=l=>{l.target===t&&S()};if(t.addEventListener("click",u),d.push(()=>t.removeEventListener("click",u)),L){const l=()=>S();L.addEventListener("click",l),d.push(()=>L.removeEventListener("click",l))}if(f&&typeof o=="function"){const l=g=>{const k=g.target instanceof Element?g.target.closest(".deck-preview-card[data-card-id]"):null;if(!k)return;g.stopPropagation();const $=a(k.getAttribute("data-card-id"),0);$<=0||o($)};f.addEventListener("click",l),d.push(()=>f.removeEventListener("click",l))}if(p){const l=()=>{p.disabled||typeof e=="function"&&e()};p.addEventListener("click",l),d.push(()=>p.removeEventListener("click",l))}}return{isAvailable:()=>m,isOpen:P,open:({titleText:u="-",metaText:l="-",cards:g=[],stats:k=null,exportEnabled:$=!0,exportVisible:D=!0}={})=>{if(!m)return!1;const v=T(g);return c.textContent=String(u||"-"),s.textContent=String(l||"-"),y.innerHTML=G(v.curve),E.innerHTML=`
            <div class="deck-preview-total">${b(v.totalCount,0)} / 40</div>
            <div class="deck-preview-type-row">
                <span>随从: ${b(v.typeCounts.follower,0)}</span>
                <span>法术: ${b(v.typeCounts.spell,0)}</span>
                <span>护符: ${b(v.typeCounts.amulet,0)}</span>
            </div>
        `,f.innerHTML=W(v.items),A&&(A.innerHTML=x(k)),p&&(p.style.display=D?"":"none",p.disabled=!$||typeof e!="function"),t.classList.add("open"),t.setAttribute("aria-hidden","false"),!0},close:S,dispose:()=>{for(;d.length>0;){const u=d.pop();u==null||u()}}}}export{R as c};
