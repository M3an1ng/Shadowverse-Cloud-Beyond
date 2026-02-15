import{_ as c}from"./index-D7YhJMYy.js";import{D as v}from"./DslEditor-tVyH63ZL.js";class m extends v{constructor(){super(),this.layoutMode="v2",this.isEmbedded=!1}async render(e,t="",s=()=>{},a={}){await this.initialize(),this.container=e,this.expression=t,this.onChange=s,this.isEmbedded=!!(a.embedded||a.mode==="embedded"||a.layout==="embedded"),t?await this.loadExpressionWithParseApi(t):this.initializeWorkspace(),this.renderV2Layout()}renderV2Layout(){const e=this.isEmbedded?" is-embedded":"";this.container.innerHTML=`
            <div class="dsl-v2-container${e}">
                <!-- Top Bar: Navigation & Actions -->
                <header class="dsl-v2-header">
                    <div class="header-left">
                        <div class="back-button" onclick="history.back()">
                            <span class="icon">«</span>
                        </div>
                        <h1 class="page-title">DSL EDITOR <span class="version-tag">V2.0</span></h1>
                    </div>
                    <div class="header-right">
                        <button class="btn-tech-v2" id="v2-save-btn">SAVE CONFIG</button>
                        <button class="btn-tech-v2 btn-danger" id="v2-clear-btn">CLEAR</button>
                    </div>
                </header>

                <!-- Main Content Grid -->
                <main class="dsl-v2-grid">
                    <!-- Left Panel: Toolbox -->
                    <aside class="dsl-v2-sidebar left">
                        <div class="sidebar-header">
                            <h3>TOOLBOX</h3>
                        </div>
                        <div class="sidebar-content custom-scrollbar">
                            <div class="toolbox-category">
                                <h4>LOGIC</h4>
                                <div class="toolbox-items">
                                    <div class="toolbox-item" draggable="true" data-type="operator" data-value="&&">AND</div>
                                    <div class="toolbox-item" draggable="true" data-type="operator" data-value="||">OR</div>
                                    <div class="toolbox-item" draggable="true" data-type="operator" data-value="!">NOT</div>
                                </div>
                            </div>
                            <div class="toolbox-category">
                                <h4>COMPARISON</h4>
                                <div class="toolbox-items">
                                    <div class="toolbox-item" draggable="true" data-type="operator" data-value="==">EQUALS</div>
                                    <div class="toolbox-item" draggable="true" data-type="operator" data-value=">">GREATER</div>
                                    <div class="toolbox-item" draggable="true" data-type="operator" data-value="<">LESS</div>
                                </div>
                            </div>
                            <div class="toolbox-category">
                                <h4>SET</h4>
                                <div class="toolbox-items">
                                    <div class="toolbox-item" draggable="true" data-type="operator" data-value="&">INTERSECT</div>
                                </div>
                            </div>
                            <div class="toolbox-category">
                                <h4>MATH</h4>
                                <div class="toolbox-items">
                                    <div class="toolbox-item" draggable="true" data-type="operator" data-value="\\">CEIL DIV</div>
                                </div>
                            </div>
                            <!-- More categories can be added here -->
                        </div>
                    </aside>

                    <!-- Center Panel: Canvas -->
                    <section class="dsl-v2-canvas-area">
                        <div class="canvas-header">
                            <div class="breadcrumbs">
                                <span class="crumb">ROOT</span>
                                <span class="separator">/</span>
                                <span class="crumb active">MAIN EXPRESSION</span>
                            </div>
                            <div class="zoom-controls">
                                <button class="icon-btn">-</button>
                                <span class="zoom-level">100%</span>
                                <button class="icon-btn">+</button>
                            </div>
                        </div>
                        
                        <div class="canvas-viewport custom-scrollbar" id="dsl-canvas-viewport">
                            <div class="dsl-v2-workspace" id="dsl-v2-workspace">
                                <!-- DSL Nodes will be rendered here -->
                            </div>
                        </div>
                    </section>

                    <!-- Right Panel: Properties -->
                    <aside class="dsl-v2-sidebar right">
                        <div class="sidebar-header">
                            <h3>INSPECTOR</h3>
                        </div>
                        <div class="sidebar-content custom-scrollbar" id="dsl-v2-inspector">
                            <div class="empty-state">
                                <p>SELECT A NODE TO EDIT</p>
                            </div>
                        </div>
                    </aside>
                </main>

                <!-- Bottom Panel: Status & Debug -->
                <footer class="dsl-v2-footer">
                    <div class="status-bar">
                        <span class="status-indicator ready">READY</span>
                        <span class="status-message">System initialized. Waiting for input.</span>
                    </div>
                </footer>
            </div>
        `,this.renderEditor()}renderEditor(){this.container.querySelector(".dsl-v2-main-expression")||this.container.querySelector("#dsl-v2-workspace");const e=this.container.querySelector("#dsl-v2-workspace");e&&(e.innerHTML="",c(async()=>{const{renderMainExpressionUI:t}=await import("./ui-main-v2-DU5N-WRT.js");return{renderMainExpressionUI:t}},[]).then(({renderMainExpressionUI:t})=>{t(this,e),this.renderV2WorkspaceAST(e)})),this.renderToolbox(),this.renderInspector()}renderV2WorkspaceAST(e){const t=document.createElement("div");if(t.className="dsl-v2-ast-container",this.astRoot)this.renderASTNodeRecursive(this.astRoot,t);else{const s=document.createElement("div");s.className="dsl-v2-placeholder",s.innerHTML=`
                <div class="placeholder-icon">+</div>
                <div class="placeholder-text">Select an option from the Toolbox to start</div>
             `,s.onclick=()=>{},t.appendChild(s)}e.appendChild(t)}renderToolbox(){const e=this.container.querySelector(".dsl-v2-sidebar.left .sidebar-content");if(!e)return;e.innerHTML="";const t=this.getCurrentEditingOptions()||[],s=this.groupOptionsByType(t);if(Object.keys(s).length===0){e.innerHTML='<div class="empty-state">NO OPTIONS AVAILABLE</div>';return}Object.entries(s).forEach(([a,i])=>{const d=document.createElement("div");d.className="toolbox-category";const l=document.createElement("h4");l.textContent=a,d.appendChild(l);const n=document.createElement("div");n.className="toolbox-items",i.forEach(r=>{const o=document.createElement("div");o.className="toolbox-item",o.textContent=r.label,o.title=r.description||"",o.onclick=()=>{this.addNode(r)},n.appendChild(o)}),d.appendChild(n),e.appendChild(d)})}renderInspector(){const e=this.container.querySelector("#dsl-v2-inspector");if(!e)return;e.innerHTML="";const t=this.selectedNode;if(!t){e.innerHTML='<div class="empty-state"><p>SELECT A NODE TO EDIT</p></div>';return}const s=document.createElement("div");s.className="inspector-section",s.innerHTML=`
            <div class="inspector-row">
                <label>TYPE</label>
                <span>${t.type}</span>
            </div>
            <div class="inspector-row">
                <label>RETURN</label>
                <span>${this.getSemanticNodeType(t)||"Object"}</span>
            </div>
        `,e.appendChild(s);const a=document.createElement("div");a.className="inspector-section";const i=document.createElement("button");i.className="btn-tech-v2 btn-danger full-width",i.textContent="DELETE NODE",i.onclick=()=>{confirm("Delete this node?")&&this.clearWorkspace()},a.appendChild(i),e.appendChild(a)}}export{m as DslEditorV2};
