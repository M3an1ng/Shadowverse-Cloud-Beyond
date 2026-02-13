import { MetadataDrivenComponent } from "./MetadataDrivenComponent.js";
import { DslEditor } from "./DslEditor.js";
import "./DslEditorV2.css";

/**
 * DSL Editor V2 - Redesigned UI
 * Inherits core logic from DslEditor but implements a completely new UI.
 */
export class DslEditorV2 extends DslEditor {
    constructor() {
        super();
        this.layoutMode = 'v2';
        this.isEmbedded = false;
    }

    /**
     * Override render to use the new V2 layout
     */
    async render(container, expression = "", onChange = () => { }, options = {}) {
        await this.initialize();
        this.container = container;
        this.expression = expression;
        this.onChange = onChange;
        this.isEmbedded = Boolean(options.embedded || options.mode === 'embedded' || options.layout === 'embedded');

        // Initialize workspace if empty
        if (!expression) {
            this.initializeWorkspace();
        } else {
            await this.loadExpressionWithParseApi(expression);
        }

        this.renderV2Layout();
    }

    renderV2Layout() {
        const embeddedClass = this.isEmbedded ? " is-embedded" : "";
        this.container.innerHTML = `
            <div class="dsl-v2-container${embeddedClass}">
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
        `;

        // Render the actual DSL content into the workspace
        this.renderEditor();
    }

    /**
     * Override renderEditor to update V2 components
     */
    renderEditor() {
        // 1. Render Main Expression
        const mainContainer = this.container.querySelector('.dsl-v2-main-expression')
            || this.container.querySelector('#dsl-v2-workspace'); // Fallback

        // If main expression container doesn't exist yet (first render), renderMainExpressionUI will create it
        // We need to clear previous main expression if it exists to avoid duplicates or just let the renderer handle it
        // Actually ui-main-v2 appends. Let's clear the workspace first if we are re-rendering everything.

        const workspace = this.container.querySelector('#dsl-v2-workspace');
        if (workspace) {
            workspace.innerHTML = ''; // Clear workspace

            // Re-render Main Expression
            import("./dsl_editor/ui-main-v2.js").then(({ renderMainExpressionUI }) => {
                renderMainExpressionUI(this, workspace);

                // 2. Render Workspace AST (The editing area)
                this.renderV2WorkspaceAST(workspace);
            });
        }

        // 3. Render Toolbox (Context aware)
        this.renderToolbox();

        // 4. Render Inspector
        this.renderInspector();
    }

    renderV2WorkspaceAST(container) {
        const astContainer = document.createElement('div');
        astContainer.className = 'dsl-v2-ast-container';

        // Use the existing recursive renderer
        if (!this.astRoot) {
            const placeholder = document.createElement("div");
            placeholder.className = "dsl-v2-placeholder";
            placeholder.innerHTML = `
                <div class="placeholder-icon">+</div>
                <div class="placeholder-text">Select an option from the Toolbox to start</div>
             `;
            placeholder.onclick = () => {
                // Maybe highlight toolbox?
            };
            astContainer.appendChild(placeholder);
        } else {
            this.renderASTNodeRecursive(this.astRoot, astContainer);
        }

        // Add "Add" button if needed, or rely on Toolbox
        // For V2, let's add a subtle "Append" zone or just rely on the structure
        // If the AST is incomplete, it usually has placeholders.

        container.appendChild(astContainer);
    }

    renderToolbox() {
        const toolboxContainer = this.container.querySelector('.dsl-v2-sidebar.left .sidebar-content');
        if (!toolboxContainer) return;

        toolboxContainer.innerHTML = ''; // Clear current

        // Get context-sensitive options
        const options = this.getCurrentEditingOptions() || [];
        const grouped = this.groupOptionsByType(options);

        if (Object.keys(grouped).length === 0) {
            toolboxContainer.innerHTML = '<div class="empty-state">NO OPTIONS AVAILABLE</div>';
            return;
        }

        Object.entries(grouped).forEach(([groupName, groupOptions]) => {
            const category = document.createElement('div');
            category.className = 'toolbox-category';

            const title = document.createElement('h4');
            title.textContent = groupName;
            category.appendChild(title);

            const itemsGrid = document.createElement('div');
            itemsGrid.className = 'toolbox-items';

            groupOptions.forEach(option => {
                const item = document.createElement('div');
                item.className = 'toolbox-item';
                item.textContent = option.label;
                item.title = option.description || '';

                // Click to add
                item.onclick = () => {
                    this.addNode(option);
                };

                itemsGrid.appendChild(item);
            });

            category.appendChild(itemsGrid);
            toolboxContainer.appendChild(category);
        });
    }

    renderInspector() {
        const inspectorContainer = this.container.querySelector('#dsl-v2-inspector');
        if (!inspectorContainer) return;

        inspectorContainer.innerHTML = '';

        const node = this.selectedNode;
        if (!node) {
            inspectorContainer.innerHTML = '<div class="empty-state"><p>SELECT A NODE TO EDIT</p></div>';
            return;
        }

        // Render Node Info
        const infoSection = document.createElement('div');
        infoSection.className = 'inspector-section';
        infoSection.innerHTML = `
            <div class="inspector-row">
                <label>TYPE</label>
                <span>${node.type}</span>
            </div>
            <div class="inspector-row">
                <label>RETURN</label>
                <span>${this.getSemanticNodeType(node) || "Object"}</span>
            </div>
        `;
        inspectorContainer.appendChild(infoSection);

        // Render Actions
        const actionsSection = document.createElement('div');
        actionsSection.className = 'inspector-section';

        // Delete Button
        const deleteBtn = document.createElement('button');
        deleteBtn.className = 'btn-tech-v2 btn-danger full-width';
        deleteBtn.textContent = 'DELETE NODE';
        deleteBtn.onclick = () => {
            // Implement delete logic (reuse clearWorkspace or specific delete)
            if (confirm('Delete this node?')) {
                // Simple clear for now, ideally should be context aware delete
                this.clearWorkspace();
            }
        };
        actionsSection.appendChild(deleteBtn);

        inspectorContainer.appendChild(actionsSection);
    }
}
