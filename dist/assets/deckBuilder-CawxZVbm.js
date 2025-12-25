import"./index-whisqKBd.js";const t={decks:[],currentDeck:null,availableCards:[],filters:{cost:null,search:""},pagination:{currentPage:1,itemsPerPage:10}},m=Array.from({length:50},(e,a)=>({uid:`card-${a}`,id:`card-def-${a}`,name:`Aetherial Card ${a+1}`,cost:Math.floor(Math.random()*10)+1,attack:Math.floor(Math.random()*10),health:Math.floor(Math.random()*10),description:"A powerful card from the void.",type:a%3===0?"Spell":"Follower",class:a%2===0?"Neutral":"Runecraft"})),b=[{id:"deck-1",name:"Starter Deck",cards:[]},{id:"deck-2",name:"Control Rune",cards:[]}];let d=null;async function $(){if(console.log("DeckBuilder: Initializing..."),d=document.getElementById("deck-builder-container"),!d){console.error("DeckBuilder: Container not found!");return}t.availableCards=m,t.decks=b,l()}function l(){d.innerHTML=`
        <div class="deck-selection-view">
            <div class="view-header">
                <div class="header-left">
                    <button class="back-button" onclick="window.location.hash='navigation'">← Back</button>
                    <h2>Edit Decks</h2>
                </div>
                <div class="header-right">
                    <!-- Placeholder for "Edit List" or other actions -->
                </div>
            </div>
            <div class="deck-grid">
                ${t.decks.map(e=>`
                    <div class="deck-card wide-banner" data-id="${e.id}">
                        <div class="deck-art-bg"></div> <!-- Placeholder for Character Art -->
                        <div class="deck-content">
                            <div class="deck-info">
                                <div class="deck-class-name">Runecraft</div> <!-- Placeholder Class -->
                                <div class="deck-name">${e.name}</div>
                                <div class="deck-status">
                                    <span class="status-indicator ${e.cards.length===40?"valid":"invalid"}">
                                        ${e.cards.length===40?"Complete":"Incomplete"}
                                    </span>
                                    <span class="deck-count">${e.cards.length} / 40</span>
                                </div>
                            </div>
                            <div class="deck-emblem">
                                <div class="emblem-icon"></div>
                            </div>
                        </div>
                        <div class="deck-actions-overlay">
                            <button class="btn-primary edit-deck-btn" data-id="${e.id}">Edit Deck</button>
                            <button class="btn-icon delete-deck-btn" data-id="${e.id}">Delete</button>
                        </div>
                    </div>
                `).join("")}
                
                <!-- New Deck Button as a Grid Item -->
                <div class="deck-card create-new-deck" id="create-deck-card">
                    <div class="create-content">
                        <span class="plus-icon">+</span>
                        <span class="create-text">New Deck</span>
                    </div>
                </div>
            </div>
        </div>
    `,document.getElementById("create-deck-card").addEventListener("click",g),document.querySelectorAll(".edit-deck-btn").forEach(e=>{e.addEventListener("click",a=>{a.stopPropagation(),o(a.target.dataset.id)})}),document.querySelectorAll(".delete-deck-btn").forEach(e=>{e.addEventListener("click",a=>{a.stopPropagation(),console.log("Delete deck",a.target.dataset.id)})}),document.querySelectorAll(".deck-card.wide-banner").forEach(e=>{e.addEventListener("click",()=>o(e.dataset.id))})}function p(){t.currentDeck&&(d.innerHTML=`
        <div class="deck-builder-view vertical-layout">
            <!-- Top Section: Deck Info & Contents -->
            <div class="deck-section">
                <div class="deck-header-row">
                    <div class="deck-identity">
                        <div class="class-icon-small"></div>
                        <input type="text" class="deck-name-input" value="${t.currentDeck.name}" id="deck-name-input">
                        <button class="btn-icon edit-name-btn">✎</button>
                    </div>
                    
                    <div class="mana-curve-mini" id="mana-curve">
                        <!-- Mana curve bars -->
                    </div>

                    <div class="deck-meta-controls">
                        <div class="deck-counts">
                            <span class="total-count"><span id="deck-count-display">${t.currentDeck.cards.length}</span> / 40</span>
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
                    <button class="btn-secondary small">Craft Missing</button>
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
                    <div class="footer-right">
                        <div class="search-bar">
                            <input type="text" placeholder="Search cards..." id="card-search-input">
                            <span class="search-icon">🔍</span>
                        </div>
                        <button class="btn-secondary">Filter</button>
                    </div>
                </div>
            </div>
        </div>
    `,r(),u(),v(),document.getElementById("card-search-input").addEventListener("input",e=>{t.filters.search=e.target.value,r()}),document.querySelectorAll(".cost-btn").forEach(e=>{e.addEventListener("click",()=>{const a=parseInt(e.dataset.cost);t.filters.cost=t.filters.cost===a?null:a,document.querySelectorAll(".cost-btn").forEach(n=>n.classList.remove("active")),t.filters.cost&&e.classList.add("active"),r()})}),document.getElementById("back-to-selection-btn").addEventListener("click",()=>{t.currentDeck=null,l()}),document.getElementById("save-deck-btn").addEventListener("click",D),document.getElementById("deck-name-input").addEventListener("change",e=>{t.currentDeck.name=e.target.value}))}function f(){return t.availableCards.filter(e=>!(t.filters.cost&&e.cost!==t.filters.cost||t.filters.search&&!e.name.toLowerCase().includes(t.filters.search.toLowerCase())))}function r(){const e=document.getElementById("library-grid");if(!e)return;const a=f(),n=Math.ceil(a.length/t.pagination.itemsPerPage)||1;t.pagination.currentPage>n&&(t.pagination.currentPage=n);const c=(t.pagination.currentPage-1)*t.pagination.itemsPerPage,i=a.slice(c,c+t.pagination.itemsPerPage);e.innerHTML=i.map(s=>`
        <div class="library-card" data-uid="${s.uid}">
            <div class="card-cost">${s.cost}</div>
            <div class="card-name">${s.name}</div>
            <div class="card-stats">${s.attack}/${s.health}</div>
            <div class="card-overlay">
                <span class="add-icon">+</span>
            </div>
        </div>
    `).join("");const k=document.querySelector(".library-footer .footer-center");k&&(k.innerHTML=`
            <button class="btn-secondary small btn-prev" ${t.pagination.currentPage===1?"disabled":""}><</button>
            <span class="page-indicator">${t.pagination.currentPage} / ${n}</span>
            <button class="btn-secondary small btn-next" ${t.pagination.currentPage===n?"disabled":""}>></button>
        `),e.querySelectorAll(".library-card").forEach(s=>{s.addEventListener("click",()=>h(s.dataset.uid))})}function u(){const e=document.getElementById("deck-list");if(!e)return;const a={};t.currentDeck.cards.forEach(c=>{a[c.uid]=(a[c.uid]||0)+1});const n=[...new Set(t.currentDeck.cards.map(c=>c.uid))].map(c=>t.currentDeck.cards.find(i=>i.uid===c)).sort((c,i)=>c.cost-i.cost);e.innerHTML=n.map(c=>`
        <div class="deck-card-mini" data-uid="${c.uid}">
            <div class="mini-cost">${c.cost}</div>
            <div class="mini-visual"></div> <!-- Placeholder art -->
            <div class="mini-count">x${a[c.uid]}</div>
            <div class="mini-overlay">
                <span class="remove-icon">-</span>
            </div>
        </div>
    `).join(""),e.querySelectorAll(".deck-card-mini").forEach(c=>{c.addEventListener("click",i=>{i.stopPropagation(),y(c.dataset.uid)})})}function v(){const e=document.getElementById("mana-curve");if(!e)return;const a=Array(9).fill(0);t.currentDeck.cards.forEach(c=>{const i=Math.min(c.cost,8);a[i]++});const n=Math.max(...a,1);e.innerHTML=a.map((c,i)=>`
        <div class="curve-bar-container" title="Cost ${i}: ${c}">
            <div class="curve-bar" style="height: ${c/n*100}%"></div>
            <div class="curve-label">${i===8?"8+":i}</div>
        </div>
    `).join("")}function g(){const e={id:`deck-${Date.now()}`,name:"New Deck",cards:[]};t.decks.push(e),o(e.id)}function o(e){t.currentDeck=t.decks.find(a=>a.id===e),p()}function h(e){if(!t.currentDeck)return;if(t.currentDeck.cards.length>=40){alert("Deck is full!");return}const a=t.availableCards.find(n=>n.uid===e);a&&(t.currentDeck.cards.push(a),u(),v(),document.getElementById("deck-count-display").textContent=`${t.currentDeck.cards.length} / 40`)}function y(e){if(!t.currentDeck)return;const a=t.currentDeck.cards.findIndex(n=>n.uid===e);a!==-1&&(t.currentDeck.cards.splice(a,1),u(),v(),document.getElementById("deck-count-display").textContent=`${t.currentDeck.cards.length} / 40`)}function D(){console.log("Saving deck:",t.currentDeck),t.currentDeck=null,l()}export{$ as initializeDeckBuilder};
