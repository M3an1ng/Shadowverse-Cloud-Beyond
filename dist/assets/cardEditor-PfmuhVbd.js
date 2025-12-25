class U{constructor(){this.metadata=null,this.isLoaded=!1,this.loadPromise=null}async initialize(){return this.isLoaded?this.metadata:this.loadPromise?await this.loadPromise:(this.loadPromise=this.loadMetadata(),await this.loadPromise)}async loadMetadata(){try{console.log("MetadataManager: Loading metadata from API...");const e=await fetch("/api/AbilityEditorMetadata");if(!e.ok)throw new Error(`HTTP ${e.status}: ${e.statusText}`);return this.metadata=await e.json(),this.isLoaded=!0,console.log("MetadataManager: Metadata loaded successfully:",this.metadata),this.validateMetadata(),this.metadata}catch(e){throw console.error("MetadataManager: Failed to load metadata:",e),new Error(`无法加载元数据: ${e.message}`)}}validateMetadata(){if(!this.metadata)throw new Error("元数据为空");const e=["effects","events","dsl","queryOperations"];for(const t of e)t in this.metadata||console.warn(`MetadataManager: Missing field "${t}" in metadata`);console.log("MetadataManager: Metadata validation passed")}getEffects(){return this.ensureLoaded(),this.metadata.effects||[]}getEffectTypes(){return(this.getEffects()||[]).map(t=>({typeName:t.effectTypeName||t.typeName||t.name,displayName:t.displayName||t.effectTypeName||t.typeName||t.name,description:t.description||"",parameters:t.parameters||[]}))}getEvents(){return this.ensureLoaded(),this.metadata.events||[]}getDsl(){return this.ensureLoaded(),this.metadata.dsl||{contextObjects:[],functions:[]}}getQueryOperations(){return this.ensureLoaded(),this.metadata.queryOperations||[]}getEffectByType(e){return this.getEffects().find(i=>i.effectTypeName===e)}getEventByType(e){return this.getEvents().find(i=>i.eventType===e)}getDslContextObjects(){return this.getDsl().contextObjects||[]}getDslFunctions(){return this.getDsl().functions||[]}getDslContextObjectByName(e){return this.getDslContextObjects().find(i=>i.name===e)}getDslFunctionByName(e){return this.getDslFunctions().find(i=>i.name===e)}async getEnumOptions(e){try{const t=await fetch(`/api/localization/enums/${e}`);if(!t.ok)throw new Error(`HTTP ${t.status}: ${t.statusText}`);const i=await t.json();return Object.entries(i).map(([r,s])=>({value:r,label:s}))}catch(t){return console.error(`MetadataManager: Failed to load enum ${e}:`,t),[]}}async getEnum(e){const t=await fetch(`/api/localization/enums/${e}`);if(!t.ok)throw new Error(`HTTP ${t.status}: ${t.statusText}`);return await t.json()}async getCardTypeOptions(){return await this.getEnumOptions("CardType")}async getCardRarityOptions(){return await this.getEnumOptions("CardRarity")}async getCardClassOptions(){return await this.getEnumOptions("CardClass")}async getAbilityTypeOptions(){return await this.getEnumOptions("AbilityType")}async getAbilityTagOptions(){return await this.getEnumOptions("AbilityTag")}ensureLoaded(){if(!this.isLoaded)throw new Error("元数据尚未加载，请先调用 initialize()")}async reload(){return this.isLoaded=!1,this.loadPromise=null,this.metadata=null,await this.initialize()}getMetadata(){return this.ensureLoaded(),this.metadata}isMetadataLoaded(){return this.isLoaded}cleanup(){this.metadata=null,this.isLoaded=!1,this.loadPromise=null,console.log("MetadataManager: Cleaned up")}}const K=new U;class L{constructor(){this.metadata=null,this.isInitialized=!1}getDisplayTypeName(e){var n,o,l;if(!e)return"未知类型";const t=String(e).trim();if(!t)return"未知类型";const i=t.match(/^(List|IEnumerable)<(.+)>$/);if(i){const c=i[2];return`${this.getDisplayTypeName(c)}列表`}const r=(l=(o=(n=this.metadata)==null?void 0:n.dsl)==null?void 0:o.typeMetadata)==null?void 0:l[t];return r!=null&&r.displayName?r.displayName:{Int32:"整数",Int64:"整数",Number:"数字",Single:"数字",Double:"数字",Decimal:"数字",Boolean:"布尔",String:"文本",Guid:"ID",DateTime:"时间",CardInstance:"卡牌",PlayerState:"玩家",TargetInfo:"目标",GameState:"对局状态",IAbility:"能力",Ability:"能力",Effect:"效果",IGameEvent:"事件"}[t]||t}async initialize(){if(!this.isInitialized)try{this.metadata=await K.initialize(),this.isInitialized=!0,console.log("MetadataDrivenComponent: Initialized with metadata")}catch(e){throw console.error("MetadataDrivenComponent: Failed to initialize:",e),e}}ensureInitialized(){if(!this.isInitialized)throw new Error("组件尚未初始化，请先调用 initialize()")}createDropdown(e,t,i,r="请选择..."){const s=document.createElement("select");if(s.className="form-select",r){const n=document.createElement("option");n.value="",n.textContent=r,n.disabled=!0,t||(n.selected=!0),s.appendChild(n)}return e.forEach(n=>{const o=document.createElement("option");o.value=n.value,o.textContent=n.label,n.description&&(o.title=n.description),n.value===t&&(o.selected=!0),s.appendChild(o)}),s.addEventListener("change",n=>{i(n.target.value)}),s}createMultiSelect(e,t=[],i){const r=document.createElement("div");return r.className="multi-select-container",e.forEach(s=>{const n=document.createElement("label");n.className="multi-select-item";const o=document.createElement("input");o.type="checkbox",o.value=s.value,o.checked=t.includes(s.value);const l=document.createElement("span");l.textContent=s.label,s.description&&(l.title=s.description),n.appendChild(o),n.appendChild(l),r.appendChild(n),o.addEventListener("change",()=>{const c=Array.from(r.querySelectorAll("input:checked")).map(p=>p.value);i(c)})}),r}createNumberInput(e,t,i={}){const r=document.createElement("input");return r.type="number",r.className="form-input",r.value=e||"",i.min!==void 0&&(r.min=i.min),i.max!==void 0&&(r.max=i.max),i.step!==void 0&&(r.step=i.step),i.placeholder&&(r.placeholder=i.placeholder),r.addEventListener("change",s=>{const n=parseFloat(s.target.value);t(isNaN(n)?null:n)}),r}createTextInput(e,t,i={}){const r=i.multiline?document.createElement("textarea"):document.createElement("input");return i.multiline||(r.type="text"),r.className="form-input",r.value=e||"",i.placeholder&&(r.placeholder=i.placeholder),r.addEventListener("input",s=>{t(s.target.value)}),r}createCheckbox(e,t,i={}){const r=document.createElement("div");r.className="checkbox-container";const s=document.createElement("input");if(s.type="checkbox",s.checked=!!e,i.label){const n=document.createElement("label");n.className="checkbox-label",n.appendChild(s);const o=document.createElement("span");o.textContent=i.label,n.appendChild(o),r.appendChild(n)}else r.appendChild(s);return s.addEventListener("change",n=>{t(n.target.checked)}),r}createFormGroup(e,t,i=null){const r=document.createElement("div");if(r.className="form-group",e){const s=document.createElement("label");s.className="form-label",e instanceof HTMLElement?s.appendChild(e):s.textContent=e,r.appendChild(s)}if(r.appendChild(t),i){const s=document.createElement("small");s.className="form-description",s.textContent=i,r.appendChild(s)}return r}createButton(e,t,i="primary"){const r=document.createElement("button");return r.className=`btn-${i}`,r.textContent=e,r.addEventListener("click",t),r}createCollapsiblePanel(e,t,i=!0){const r=document.createElement("div");r.className="collapsible-panel";const s=document.createElement("div");s.className="panel-header",s.textContent=e,s.style.cursor="pointer";const n=document.createElement("div");return n.className="panel-body",n.style.display=i?"block":"none",n.appendChild(t),s.addEventListener("click",()=>{const o=n.style.display!=="none";n.style.display=o?"none":"block",s.classList.toggle("collapsed",o)}),r.appendChild(s),r.appendChild(n),r}showError(e,t){const i=document.createElement("div");i.className="error-message",i.textContent=e,t.appendChild(i)}clearContainer(e){if(e)for(;e.firstChild;)e.removeChild(e.firstChild)}}function J(a,e){Array.isArray(a)&&a.push(e)}function Y(a){if(!(!Array.isArray(a)||a.length===0))return a.pop()}function X(a){if(!(!Array.isArray(a)||a.length===0))return a[a.length-1]}function ee(a){Array.isArray(a)&&(a.length=0)}function te(a,e,t){if(!a||!t)return!1;if(a.type==="virtualRoot"&&e==="main")return a.main=t,!0;const i=`set${e.charAt(0).toUpperCase()+e.slice(1)}`;return typeof a[i]=="function"?(a[i](t),!0):e==="left"&&typeof a.setLeftOperand=="function"?(a.setLeftOperand(t),!0):e==="right"&&typeof a.setRightOperand=="function"?(a.setRightOperand(t),!0):(console.error(`writeBackToParentSlot Error: Setter method '${i}' not found on parentNode`,a),!1)}function G(a,e){var i,r;let t=[];if(a.isNumericType(e)){const s=a.getSmartOperators(e)||[];t.push(...s);const o=(((r=(i=a.metadata)==null?void 0:i.dsl)==null?void 0:r.functions)||[]).filter(l=>a.isFunctionCompatibleWithType(l,e)).map(l=>({type:"function",value:l.name,label:l.name,description:l.description,returnType:l.returnType||l.returnTypeName||"Object",meta:l}));t.push(...o)}else if(a.isTextType(e)){const s=a.getTextOperators()||[];t.push(...s);const n=a.getTextFunctions()||[];t.push(...n)}else if(e==="Boolean"){const s=a.getSmartOperators(e)||[];t.push(...s)}else if(a.isCollectionType(e)){const s=a.getCollectionFunctions()||[];t.push(...s),t.push({type:"listQuery",value:".query",label:"筛选 (.query)",description:"筛选集合中的元素"}),t.push({type:"listSort",value:".orderBy",label:"排序 (.orderBy)",description:"对集合中的元素进行排序"}),t.push({type:"listTake",value:".take",label:"获取 (.take)",description:"从集合中获取指定数量的元素"})}else if(a.isObjectType(e))if(e==="Object")t.push({type:"operator",value:"==",label:"等于 (==)",description:"相等比较"},{type:"operator",value:"!=",label:"不等于 (!=)",description:"不等比较"});else{const s=a.getObjectProperties(e)||[];t.push(...s)}return t}function ie(a,e){const t=[];return a.isNumericType(e)?t.push({type:"operator",value:"+",label:"加法 (+)",description:"数值加法运算",priority:110},{type:"operator",value:"-",label:"减法 (-)",description:"数值减法运算",priority:110},{type:"operator",value:"*",label:"乘法 (*)",description:"数值乘法运算",priority:115},{type:"operator",value:"/",label:"除法 (/)",description:"数值除法运算",priority:115},{type:"operator",value:"==",label:"等于 (==)",description:"数值相等比较",priority:100},{type:"operator",value:"!=",label:"不等于 (!=)",description:"数值不等比较",priority:95},{type:"operator",value:">",label:"大于 (>)",description:"数值大小比较",priority:90},{type:"operator",value:">=",label:"大于等于 (>=)",description:"数值大小比较",priority:85},{type:"operator",value:"<",label:"小于 (<)",description:"数值大小比较",priority:90},{type:"operator",value:"<=",label:"小于等于 (<=)",description:"数值大小比较",priority:85}):e==="Boolean"?t.push({type:"operator",value:"==",label:"等于 (==)",description:"布尔值相等比较",priority:100},{type:"operator",value:"!=",label:"不等于 (!=)",description:"布尔值不等比较",priority:95},{type:"operator",value:"&&",label:"逻辑与 (&&)",description:"逻辑AND操作",priority:80},{type:"operator",value:"||",label:"逻辑或 (||)",description:"逻辑OR操作",priority:75}):a.isTextType(e)?(t.push({type:"operator",value:"==",label:"等于 (==)",description:"文本相等比较",priority:100},{type:"operator",value:"!=",label:"不等于 (!=)",description:"文本不等比较",priority:95}),e==="String"&&t.push({type:"operator",value:"contains",label:"包含",description:"字符串包含检查",priority:85},{type:"operator",value:"startsWith",label:"开始于",description:"字符串前缀检查",priority:80},{type:"operator",value:"endsWith",label:"结束于",description:"字符串后缀检查",priority:80})):a.isBusinessObjectType(e)?t.push({type:"operator",value:"==",label:"等于 (==)",description:"对象相等比较",priority:100},{type:"operator",value:"!=",label:"不等于 (!=)",description:"对象不等比较",priority:95}):t.push({type:"operator",value:"==",label:"等于 (==)",description:"相等比较",priority:100},{type:"operator",value:"!=",label:"不等于 (!=)",description:"不等比较",priority:95}),t.sort((i,r)=>r.priority-i.priority).map(i=>({type:i.type,value:i.value,label:i.label,description:i.description}))}function re(){return[{type:"operator",value:"==",label:"等于 (==)",description:"文本相等比较",priority:100},{type:"operator",value:"!=",label:"不等于 (!=)",description:"文本不等比较",priority:95}].sort((a,e)=>e.priority-a.priority).map(a=>({type:a.type,value:a.value,label:a.label,description:a.description}))}function se(a,e){if(!a.virtualRoot||!a.virtualRoot.main)return;const t=document.createElement("div");t.className="main-expression",t.style.cssText=`
    background: #e9ecef;
    border: 1px solid #ced4da;
    border-radius: 4px;
    padding: 8px 12px;
    margin-bottom: 12px;
  `;const i=document.createElement("div");i.style.cssText="display:flex; align-items:center; gap:8px; margin-bottom:6px;";const r=document.createElement("span");r.textContent="已完成表达式",r.style.cssText="font-weight:600;color:#495057;",i.appendChild(r);const s=document.createElement("div");s.className="expression-content",s.style.cssText=`
    background: #f8f9fa;
    border: 1px dashed #ced4da;
    border-radius: 4px;
    padding: 6px;
    color: #212529;
    cursor: pointer;
  `;const n=a.virtualRoot.main;a.renderASTNodeRecursive(n,s),s.addEventListener("click",o=>{o.target===s&&a.expandMainExpression()}),t.appendChild(i),t.appendChild(s),e.appendChild(t)}function ae(a,e){a.renderWorkspaceHeader(e);const t=document.createElement("div");t.className="dsl-workspace",t.style.cssText=`
    border: 1px solid #ffc107;
    border-radius: 4px;
    padding: 12px;
    background: #fff3cd;
    font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
    margin-bottom: 8px;
  `;const i=document.createElement("div");i.className="dsl-nodes-container",t.appendChild(i),ne(a,i),oe(a,i),le(a,t),e.appendChild(t)}function ne(a,e){if(a.astRoot)a.renderASTNodeRecursive.call(a,a.astRoot,e);else{const t=document.createElement("span");t.className="dsl-placeholder",t.textContent="点击 [+] 开始构建表达式",t.style.color="#6c757d",t.style.fontSize="12px",t.style.fontWeight="500",e.appendChild(t)}}function oe(a,e){const t=document.createElement("div");t.style.cssText="display: inline-flex; gap: 4px; margin-left: 8px;";const i=document.createElement("button");if(i.className="dsl-add-btn",i.textContent="[+]",i.title="添加节点",i.addEventListener("click",r=>{r.preventDefault(),a.showAddNodeMenu(i)}),t.appendChild(i),a.astRoot||a.awaitingRightOperand){const r=document.createElement("button");r.className="dsl-clear-btn",r.textContent="[×]",r.title=a.awaitingRightOperand?"取消操作符，返回上一级":"清空当前表达式",r.addEventListener("click",s=>{s.preventDefault(),a.clearWorkspace()}),t.appendChild(r)}if(a.astRoot||a.isSubEditor){const r=document.createElement("button");r.className="dsl-back-btn",r.textContent="[−]",r.title="退回上级",a.isSubEditor&&!a.astRoot?(r.title="退出筛选编辑器",r.addEventListener("click",s=>{s.preventDefault(),a.exitSubEditor()})):r.addEventListener("click",s=>{s.preventDefault(),a.goBack()}),t.appendChild(r)}if(a.astRoot){if(a.workspaceAST&&a.workspaceAST.type==="value"){const s=document.createElement("button");s.className="dsl-elevate-btn",s.textContent="()",s.title="将当前项创建为独立表达式",s.addEventListener("click",n=>{n.preventDefault(),a.handleElevateExpression(a.workspaceAST)}),t.appendChild(s)}const r=document.createElement("button");r.className="dsl-complete-btn",r.textContent="[✓]",r.title="保存当前层（封装并返回上一级）",r.addEventListener("click",s=>{s.preventDefault(),a.saveWorkspace()}),t.appendChild(r)}e.appendChild(t)}function le(a,e){const t=document.createElement("div");t.className="expression-preview";const i=document.createElement("label");i.textContent="DSL表达式:",i.style.cssText="font-size: 12px; color: #2c3e50; font-weight: 600; margin-right: 8px;";const r=document.createElement("code");r.textContent=a.expression||"(空)",r.style.cssText="font-size: 12px; color: #495057; background: #f8f9fa; padding: 2px 4px; border-radius: 3px;",t.appendChild(i),t.appendChild(r),e.appendChild(t);const s=document.createElement("div");s.className="expression-readable-preview",s.style.cssText="margin-top: 6px; font-size: 12px; color: #495057;";const n=document.createElement("span");n.textContent="可读化预览: ",n.style.cssText="font-weight: 600; margin-right: 6px; color: #2c3e50;";const o=document.createElement("span");o.textContent=typeof a.getReadablePreviewText=="function"?a.getReadablePreviewText():"(空)",s.appendChild(n),s.appendChild(o),e.appendChild(s)}function F(a){if(!a)return"未知类型";const e=String(a).trim();if(!e)return"未知类型";const t=e.match(/^(List|IEnumerable)<(.+)>$/);return t?`${F(t[2])}列表`:{Int32:"整数",Int64:"整数",Number:"数字",Single:"数字",Double:"数字",Decimal:"数字",Boolean:"布尔",String:"文本",Guid:"ID",DateTime:"时间",CardInstance:"卡牌",PlayerState:"玩家",TargetInfo:"目标",GameState:"对局状态",IAbility:"能力",Ability:"能力",Effect:"效果",IGameEvent:"事件"}[e]||e}class O{constructor(e,t="Object"){this.type=e,this.returnType=t,this.parent=null,this.isComplete=!1}updateCompleteness(){throw new Error("updateCompleteness must be implemented by subclass")}toExpression(){throw new Error("toExpression must be implemented by subclass")}validate(){throw new Error("validate must be implemented by subclass")}clone(){throw new Error("clone must be implemented by subclass")}}class B extends O{constructor(e,t="Object",i=[]){super("functionCall",t),this.functionName=e,this.parameters=i,this.args=new Array(i.length).fill(null),this.updateCompleteness()}setArg(e,t){e<0||e>=this.args.length||(this.args[e]=t,t&&(t.parent=this),this.updateCompleteness())}updateCompleteness(){const e=this.parameters.some((i,r)=>!i.isOptional&&(!this.args[r]||!this.args[r].isComplete)),t=this.args.some((i,r)=>{var s;return i&&!i.isComplete&&(((s=this.parameters[r])==null?void 0:s.isOptional)??!1)});this.isComplete=!e&&!t}toExpression(){const e=this.args.map(t=>t?t.toExpression():"");return`${this.functionName}(${e.join(", ")})`}validate(){const e=[];return this.parameters.forEach((t,i)=>{const r=this.args[i];if(!t.isOptional&&!r){e.push(`函数 ${this.functionName} 缺少参数: ${t.name}`);return}if(r){const s=r.validate();s.isValid||e.push(...s.errors)}}),{isValid:e.length===0,errors:e}}clone(){const e=new B(this.functionName,this.returnType,Array.isArray(this.parameters)?[...this.parameters]:[]);return e.args=this.args.map(t=>t?t.clone():null),e.args.forEach(t=>{t&&(t.parent=e)}),e.updateCompleteness(),e}}class P extends O{constructor(e,t,i={}){super("value",t),this.value=e,this.metadata=i,this.updateCompleteness()}updateCompleteness(){this.metadata.type==="staticValue"&&this.metadata.isAwaitingInput?this.isComplete=!1:this.isComplete=!0}toExpression(){return this.metadata&&this.metadata.type==="encapsulated"?this.metadata.displayValue||this.value:this.value}validate(){return{isValid:!0,errors:[]}}clone(){return new P(this.value,this.returnType,{...this.metadata})}}class V extends O{constructor(e,t=null){super("operator",t),this.operator=e,this.leftOperand=null,this.rightOperand=null}setLeftOperand(e){this.leftOperand=e,e&&(e.parent=this),this.updateCompleteness()}setRightOperand(e){this.rightOperand=e,e&&(e.parent=this),this.updateCompleteness()}updateCompleteness(){this.isComplete=this.leftOperand&&this.rightOperand&&this.leftOperand.isComplete&&this.rightOperand.isComplete}toExpression(){const e=this.leftOperand,t=this.rightOperand,i=!e||e.metadata&&e.metadata.type==="empty",r=!t||t.metadata&&t.metadata.type==="empty",s=i?"()":e.toExpression(),n=r?"()":t.toExpression();return`${s} ${this.operator} ${n}`}validate(){const e=[];if(!this.leftOperand)e.push("操作符缺少左操作数");else{const t=this.leftOperand.validate();t.isValid||e.push(...t.errors)}if(!this.rightOperand)e.push("操作符缺少右操作数");else{const t=this.rightOperand.validate();if(t.isValid||e.push(...t.errors),this.leftOperand&&this.rightOperand){const i=this.validateOperandTypes();i&&e.push(i)}}return{isValid:e.length===0,errors:e}}validateOperandTypes(){const e=this.leftOperand.returnType,t=this.rightOperand.returnType;if(["&&","||"].includes(this.operator)){if(e!=="Boolean")return`逻辑操作符 ${this.operator} 的左操作数必须是布尔表达式，当前类型：${F(e)}`;if(t!=="Boolean")return`逻辑操作符 ${this.operator} 的右操作数必须是布尔表达式，当前类型：${F(t)}`}return["==","!=",">","<",">=","<="].includes(this.operator)&&e!==t&&e!=="Object"&&t!=="Object"?`比较操作符 ${this.operator} 的操作数类型不兼容：${e} 与 ${t}`:null}clone(){const e=new V(this.operator,this.returnType);return this.leftOperand&&e.setLeftOperand(this.leftOperand.clone()),this.rightOperand&&e.setRightOperand(this.rightOperand.clone()),e}}class H extends O{constructor(e){super("listQuery",e.returnType),this.source=e,this.filterExpression=null,e&&(e.parent=this),this.updateCompleteness()}setFilterExpression(e){this.filterExpression=e,e&&(e.parent=this),this.updateCompleteness()}updateCompleteness(){this.isComplete=this.source&&this.filterExpression&&this.filterExpression.isComplete}toExpression(){const e=this.source?this.source.toExpression():"",t=this.filterExpression?this.filterExpression.toExpression():"";return`${e}.query(${t})`}validate(){const e=[];if(this.source||e.push("Query操作缺少源集合"),!this.filterExpression)e.push("Query操作缺少筛选表达式");else{const t=this.filterExpression.validate();t.isValid||e.push(...t.errors),this.filterExpression.returnType!=="Boolean"&&e.push(`Query筛选表达式必须返回布尔值，当前返回: ${F(this.filterExpression.returnType)}`)}return{isValid:e.length===0,errors:e}}clone(){const e=new H(this.source.clone());return this.filterExpression&&e.setFilterExpression(this.filterExpression.clone()),e}}class z extends O{constructor(e){super("listSort",e.returnType),this.source=e,this.sortExpression=null,e&&(e.parent=this),this.updateCompleteness()}setSortExpression(e){this.sortExpression=e,e&&(e.parent=this),this.updateCompleteness()}updateCompleteness(){this.isComplete=this.source&&this.sortExpression&&this.sortExpression.isComplete}toExpression(){const e=this.source?this.source.toExpression():"",t=this.sortExpression?this.sortExpression.toExpression():"";return`${e}.orderBy(${t})`}validate(){return{isValid:!0,errors:[]}}clone(){const e=new z(this.source.clone());return this.sortExpression&&e.setSortExpression(this.sortExpression.clone()),e}}class j extends O{constructor(e){super("listTake",e.returnType),this.source=e,this.countExpression=null,e&&(e.parent=this),this.updateCompleteness()}setCountExpression(e){this.countExpression=e,e&&(e.parent=this),this.updateCompleteness()}updateCompleteness(){this.isComplete=this.source&&this.countExpression&&this.countExpression.isComplete}toExpression(){const e=this.source?this.source.toExpression():"",t=this.countExpression?this.countExpression.toExpression():"";return`${e}.take(${t})`}validate(){return{isValid:!0,errors:[]}}clone(){const e=new j(this.source.clone());return this.countExpression&&e.setCountExpression(this.countExpression.clone()),e}}function _(a){return new z(a)}function W(a){return new j(a)}function C(a,e,t={}){return new P(a,e,t)}function A(a,e=null){return new V(a,e)}function I(a,e="Object",t=[]){return new B(a,e,t)}function q(a){return new H(a)}function ce(a){return["&&","||"].includes(a)||["==","!=",">","<",">=","<=","contains","startsWith","endsWith"].includes(a)?"Boolean":["+","-","*","/"].includes(a)?"Number":"Object"}function D(a){return a?a.validate():{isValid:!0,errors:[]}}function S(a){if(!a)return null;if(!a.isComplete)return a;if(a.type==="operator"){const e=S(a.leftOperand);if(e)return e;const t=S(a.rightOperand);if(t)return t}if(a.type==="functionCall")for(const e of a.args||[]){const t=S(e);if(t)return t}if(a.type==="listQuery"){const e=S(a.source);if(e)return e;const t=S(a.filterExpression);if(t)return t}if(a.type==="listSort"){const e=S(a.source);if(e)return e;const t=S(a.sortExpression);if(t)return t}if(a.type==="listTake"){const e=S(a.source);if(e)return e;const t=S(a.countExpression);if(t)return t}return null}function R(a){if(!a)return null;if(a.type==="value")return a;if(a.type==="functionCall"){const e=a.args||[];for(let t=e.length-1;t>=0;t--)if(e[t])return R(e[t]);return a}if(a.type==="operator"){if(a.rightOperand)return R(a.rightOperand);if(a.leftOperand)return R(a.leftOperand)}return a}class k extends L{constructor(){super(),this.expression="",this.virtualRoot={type:"virtualRoot",main:null},this.workspaceAST=null,this.astRoot=null,this.isInitialized=!1,this.currentNode=null,this.selectedNode=null,this.editingPath=[],this.isExpressionCompleted=!1,this.onChange=null,this.container=null,this.currentOptions=[],this.optionsContainer=null,this.editorContainer=null,this.expressionContainer=null,this.mainExpressionContainer=null,this.lastEmittedExpression=null}normalizeTypeName(e){if(!e)return null;const t=String(e).trim();if(!t)return null;const i=t.match(/^Nullable<(.+)>$/);return i?i[1]:t==="List"?"List<Object>":t==="IEnumerable"?"IEnumerable<Object>":t}pushEditingPath(e){this.editingPath||(this.editingPath=[]);try{J(this.editingPath,e)}catch{this.editingPath.push(e)}console.log("DslEditor: pushEditingPath ->",e)}popEditingPath(){this.editingPath||(this.editingPath=[]);let e;try{e=Y(this.editingPath)}catch{e=this.editingPath.length>0?this.editingPath.pop():void 0}return console.log("DslEditor: popEditingPath <-",e),e}getHighlightTargetNode(){const e=this.peekEditingPath?this.peekEditingPath():null;return!e||!e.parentNode?null:e.slot==="left"?e.parentNode.leftOperand||null:e.slot==="right"?e.parentNode.rightOperand||null:e.parentNode||null}peekEditingPath(){this.editingPath||(this.editingPath=[]);try{return X(this.editingPath)}catch{return this.editingPath.length===0?void 0:this.editingPath[this.editingPath.length-1]}}clearEditingPath(){try{ee(this.editingPath)}catch{this.editingPath=[]}console.log("DslEditor: clearEditingPath")}ensureAstRootSync(){this.astRoot!==this.workspaceAST&&(this.astRoot=this.workspaceAST,console.log("DslEditor: ensureAstRootSync -> astRoot reassigned to workspaceAST"))}writeBackToParentSlot(e,t,i){try{return te(e,t,i)}catch{return!e||!i?!1:t==="left"&&typeof e.setLeftOperand=="function"?(e.setLeftOperand(i),!0):t==="right"&&typeof e.setRightOperand=="function"?(e.setRightOperand(i),!0):t==="self"?(console.warn("DslEditor: writeBackToParentSlot(slot=self) requires upper-level path context."),!1):(console.warn("DslEditor: writeBackToParentSlot failed: invalid slot or parentNode"),!1)}}initializeWorkspace(){const e=this.createEncapsulatedFrom(C("","Void",{type:"empty"}));this.virtualRoot.main=e,console.log("DslEditor: Workspace initialized with a virtual root."),this.handleEnterEditModeForSlot(this.virtualRoot,"main")}getCurrentEditingOptions(){var i,r,s;if(console.log("DslEditor: getCurrentEditingOptions called"),console.log("DslEditor: astRoot:",this.astRoot),console.log("DslEditor: isExpressionCompleted:",this.isExpressionCompleted),!this.astRoot||this.astRoot.type==="value"&&((i=this.astRoot.metadata)==null?void 0:i.type)==="empty"){console.log("DslEditor: No AST root or empty node, returning data source options");const n=this.getDataSourceOptions();return this.slotExpectedType?n.filter(o=>o.type==="staticValue"?!0:o.returnType?this.areTypesCompatible(o.returnType,this.slotExpectedType):!1):n}const e=S(this.astRoot);if(console.log("DslEditor: Found incomplete node:",e),e){const n=this.getOptionsForIncompleteNode(e);return console.log("DslEditor: Returning options for incomplete node:",n),n}console.log("DslEditor: Using root node for options:",this.astRoot),console.log("DslEditor: Root node structure:",{type:this.astRoot.type,value:this.astRoot.value,returnType:this.astRoot.returnType,metadata:this.astRoot.metadata,expression:((s=(r=this.astRoot).toExpression)==null?void 0:s.call(r))||null});const t=this.getOptionsForCompleteNode(this.astRoot);return console.log("DslEditor: Returning options for complete expression:",t),t}getOptionsForIncompleteNode(e){var t;if(e.type==="functionCall"){const i=e.parameters||[],r=e.args||[],s=i.findIndex((n,o)=>!(n!=null&&n.isOptional)&&!r[o]);return s>=0&&(this.slotExpectedType=this.normalizeTypeName((t=i[s])==null?void 0:t.typeName)||null),this.getDataSourceOptions()}return e.type==="operator"&&!e.rightOperand?this.isLogicalOperator(e.operator)?this.getBooleanExpressionOptions():this.getDataSourceOptions():this.getDataSourceOptions()}getOptionsForCompleteNode(e){console.log("DslEditor: getOptionsForCompleteNode called with node:",e);const t=this.findLastNodeInExpression(e);if(console.log("DslEditor: Found last node:",t),t&&t!==e){const s=this.getNodeActualReturnType(t);return console.log("DslEditor: Last node type:",s),console.log("DslEditor: Providing options based on last node type:",s),this.getOptionsForType(s)}const i=e.metadata&&e.metadata.type==="encapsulated",r=e.type==="operator";if(console.log("DslEditor: Node is encapsulated:",i,"isOperatorNode:",r),i||r){const s=this.getNodeActualReturnType(e);return console.log("DslEditor: Encapsulated/Operator expression return type:",s),console.log("DslEditor: Providing options based on actual return type:",s),this.getOptionsForType(s)}else{const s=this.getNodeActualReturnType(e);console.log("DslEditor: Non-encapsulated expression return type:",s);const n=G(this,s);if(this.isCollectionType(s))return console.log("DslEditor: Returning all options for collection type:",n),n;{const o=n.filter(l=>l.type==="property"||l.type==="function");return console.log("DslEditor: Filtered options (properties/functions only):",o),o}}}findLastNodeInExpression(e){if(!e)return null;if(console.log("DslEditor: Finding last node in expression:",e),e.metadata&&e.metadata.type==="encapsulated"&&!e.parent)return console.log("DslEditor: Complete encapsulated expression, returning itself:",e),e;if(e.type==="operator"){if(e.rightOperand)return console.log("DslEditor: Operator node, checking right operand:",e.rightOperand),this.findLastNodeInExpression(e.rightOperand);if(e.leftOperand)return console.log("DslEditor: Operator node, no right operand, checking left operand:",e.leftOperand),this.findLastNodeInExpression(e.leftOperand)}return e.metadata&&e.metadata.type==="encapsulated"&&e.metadata.innerAST?(console.log("DslEditor: Encapsulated node with parent, checking inner AST:",e.metadata.innerAST),this.findLastNodeInExpression(e.metadata.innerAST)):(console.log("DslEditor: Found last node (value node):",e),e)}getOptionsForType(e){console.log("DslEditor: Getting options for type:",e);const t=[];if(e==="Int32")t.push({type:"operator",operator:"+",label:"加法 (+)",description:"数值相加"},{type:"operator",operator:"-",label:"减法 (-)",description:"数值相减"},{type:"operator",operator:"*",label:"乘法 (*)",description:"数值相乘"},{type:"operator",operator:"/",label:"除法 (/)",description:"数值相除"},{type:"operator",operator:">",label:"大于 (>)",description:"比较两个数值"},{type:"operator",operator:"<",label:"小于 (<)",description:"比较两个数值"},{type:"operator",operator:">=",label:"大于等于 (>=)",description:"比较两个数值"},{type:"operator",operator:"<=",label:"小于等于 (<=)",description:"比较两个数值"},{type:"operator",operator:"==",label:"等于 (==)",description:"比较两个值是否相等"},{type:"operator",operator:"!=",label:"不等于 (!=)",description:"比较两个值是否不相等"});else if(e==="Boolean")t.push({type:"operator",operator:"&&",label:"逻辑与 (&&)",description:"两个条件都为真"},{type:"operator",operator:"||",label:"逻辑或 (||)",description:"至少一个条件为真"});else{const r=G(this,e).filter(s=>s.type==="operator"||s.type==="function"||s.type==="query"||s.type==="listQuery");t.push(...r)}return console.log("DslEditor: Generated options for type",e,":",t),t}getNodeActualReturnType(e){return e?e.metadata&&e.metadata.type==="encapsulated"&&e.metadata.innerAST?this.calculateASTReturnType(e.metadata.innerAST):this.calculateASTReturnType(e):"Object"}calculateNodeTypeMatch(e,t=null){const i={expectedType:null,actualType:null,isTypeMatch:!0,needsCorrection:!1};if(!e)return console.log("DslEditor: calculateNodeTypeMatch - node is null"),i;if(console.log("DslEditor: calculateNodeTypeMatch - analyzing node:",{type:e.type,value:e.value,parent:e.parent?{type:e.parent.type,operator:e.parent.operator}:null}),i.actualType=this.calculateASTReturnType(e),console.log("DslEditor: calculateNodeTypeMatch - actual type:",i.actualType),e.parent&&e.parent.type==="operator"){const r=e.parent.operator,s=e.parent.rightOperand===e;if(console.log("DslEditor: calculateNodeTypeMatch - parent context:",{parentOperator:r,isRightOperand:s,hasLeftOperand:!!e.parent.leftOperand}),["&&","||"].includes(r))i.expectedType="Boolean",console.log("DslEditor: calculateNodeTypeMatch - logical operator expects Boolean");else if([">","<",">=","<="].includes(r)){if(s&&e.parent.leftOperand){const n=this.calculateASTReturnType(e.parent.leftOperand);(n==="Int32"||n==="Number")&&(i.expectedType=n),console.log("DslEditor: calculateNodeTypeMatch - comparison operator expects:",i.expectedType)}}else["==","!="].includes(r)&&s&&e.parent.leftOperand&&(i.expectedType=this.calculateASTReturnType(e.parent.leftOperand),console.log("DslEditor: calculateNodeTypeMatch - equality operator expects:",i.expectedType))}else if(e.parent&&e.parent.type==="functionCall"){const r=e.parent,n=(r.args||[]).indexOf(e),o=(r.parameters||[])[n];o!=null&&o.typeName&&(i.expectedType=this.normalizeTypeName(o.typeName),console.log("DslEditor: calculateNodeTypeMatch - functionCall arg expects:",i.expectedType))}else console.log("DslEditor: calculateNodeTypeMatch - no parent or parent is not operator");return t&&!e.parent&&(i.expectedType=t,console.log("DslEditor: calculateNodeTypeMatch - using external expected type:",i.expectedType)),i.expectedType&&!this.areTypesCompatible(i.actualType,i.expectedType)&&(i.isTypeMatch=!1,i.needsCorrection=!0),i}findAllTypeIssuesInAST(e){var i,r;const t=[];if(!e)return t;if(e._typeMatchInfo){const s=e._typeMatchInfo;!s.isTypeMatch&&s.needsCorrection&&t.push(`类型不匹配：期望 ${s.expectedType}，实际 ${s.actualType}`)}if(e.type==="operator"&&(e.leftOperand&&t.push(...this.findAllTypeIssuesInAST(e.leftOperand)),e.rightOperand&&t.push(...this.findAllTypeIssuesInAST(e.rightOperand))),e.type==="functionCall"){const s=e.parameters||[],n=e.args||[];for(let o=0;o<n.length;o++){const l=n[o];if(!l)continue;const c=this.validateExpressionSemantics(l);c.hasSemanticIssues&&(result.hasSemanticIssues=!0,result.issues.push(...c.issues));const p=(i=s[o])!=null&&i.typeName?this.normalizeTypeName(s[o].typeName):null;if(p){const d=this.calculateNodeTypeMatch(l,p);d&&d.isTypeMatch===!1&&(result.hasSemanticIssues=!0,result.issues.push(`函数 ${e.functionName} 参数 ${((r=s[o])==null?void 0:r.name)||o}: 类型不匹配（期望 ${p}，实际 ${d.actualType}）`))}}}return e.metadata&&e.metadata.innerAST&&t.push(...this.findAllTypeIssuesInAST(e.metadata.innerAST)),t}validateExpressionSemantics(e){const t={hasSemanticIssues:!1,issues:[],fixSuggestions:[]},i=this.calculateNodeTypeMatch(e);if(i.isTypeMatch||(t.hasSemanticIssues=!0,t.issues.push(`类型不匹配：期望 ${i.expectedType}，实际 ${i.actualType}`)),e.type==="operator"){if(e.leftOperand){const r=this.validateExpressionSemantics(e.leftOperand);r.hasSemanticIssues&&(t.hasSemanticIssues=!0,t.issues.push(...r.issues))}if(e.rightOperand){const r=this.validateExpressionSemantics(e.rightOperand);r.hasSemanticIssues&&(t.hasSemanticIssues=!0,t.issues.push(...r.issues))}}if(e.metadata&&e.metadata.type==="encapsulated"&&e.metadata.innerAST){const r=this.validateExpressionSemantics(e.metadata.innerAST);r.hasSemanticIssues&&(t.hasSemanticIssues=!0,t.issues.push(...r.issues))}return t}calculateASTReturnType(e){if(!e)return"Object";if(console.log("DslEditor: Calculating return type for node:",e),e.type==="value"){if(e.metadata&&e.metadata.type==="encapsulated"&&e.metadata.innerAST){const i=this.calculateASTReturnType(e.metadata.innerAST);return console.log("DslEditor: Encapsulated expression inner return type:",i),i}const t=e.returnType||"Object";return console.log("DslEditor: Value node return type:",t),t}if(e.type==="operator"){const t=ce(e.operator);return console.log("DslEditor: Operator",e.operator,"return type:",t),t}else{if(e.type==="functionCall")return e.returnType||"Object";if((e.type==="listQuery"||e.type==="listSort"||e.type==="listTake")&&e.source){const t=this.calculateASTReturnType(e.source);return console.log(`DslEditor: List operation node (${e.type}) return type is same as source:`,t),t}}return console.log("DslEditor: Unknown node type, defaulting to Object"),"Object"}async render(e,t="",i=()=>{},r={}){try{if(await this.initialize(),this.container=e,this.expression=t,this.onChange=i,this.expectedType=r.expectedType||null,this.triggeringEventContext=r.triggeringEventContext||null,this.clearContainer(e),console.log("DslEditor: Starting render with expression:",t),!t)this.initializeWorkspace();else{if(typeof this.parseExpression!="function")throw new Error("this.parseExpression is not a function");this.parseExpression(t),this.renderEditor()}console.log("DslEditor: Render completed successfully"),this.emitExpressionChange(this.expression||"")}catch(s){console.error("DslEditor: Render failed:",s),this.showError(`渲染DSL编辑器失败: ${s.message}`,e)}}parseExpression(e){if(console.log(`DslEditor: Parsing expression string V2: "${e}"`),!e){this.initializeWorkspace();return}const t=e.match(/^(.*)\.query\((.*)\)$/);let i;if(t){const s=t[1],n=t[2];console.log(`DslEditor: Matched query. Source: "${s}", Filter: "${n}"`);const o=s.split(".");let l=C(o[0],"Object",{type:"context"});for(let u=1;u<o.length;u++){const m=o[u];l=C(`${l.value}.${m}`,"Object",{type:"property",fullPath:`${l.value}.${m}`})}const c=q(l),p=C(n,"Boolean",{type:"context"}),d=this.createEncapsulatedFrom(p);c.setFilterExpression(d),i=c}else{const s=e.split(".");let n=C(s[0],"Object",{type:"context"});for(let o=1;o<s.length;o++){const l=s[o];n=C(`${n.value}.${l}`,"Object",{type:"property",fullPath:`${n.value}.${l}`})}i=n}const r=this.createEncapsulatedFrom(i);this.virtualRoot.main=r,this.workspaceAST=this.cloneAST(r),this.astRoot=this.workspaceAST,console.log("DslEditor: Parsed expression into AST:",this.virtualRoot.main)}renderEditor(){let e=this.container.querySelector(".dsl-editor");e||(e=document.createElement("div"),e.className="dsl-editor",this.container.appendChild(e)),e.innerHTML="",se(this,e),ae(this,e)}handleElevateExpression(e){if(console.log("DslEditor: Elevating expression for node:",e),!e){console.warn("DslEditor: handleElevateExpression called with no node to wrap.");return}const t=this.createEncapsulatedFrom(e);this.workspaceAST=t,this.astRoot=t,this.updateExpressionFromAST(),this.renderEditor()}renderWorkspaceHeader(e){const t=document.createElement("div");t.className="workspace-header",t.style.cssText=`
            background: #fff3cd;
            border: 1px solid #ffeaa7;
            border-radius: 4px;
            padding: 8px 12px;
            margin-bottom: 12px;
            display: flex;
            align-items: center;
            gap: 8px;
        `;const i=document.createElement("span");i.textContent="🔧",i.style.fontSize="16px";const r=document.createElement("span");r.textContent="工作区：构建新表达式",r.style.cssText=`
            font-weight: 500;
            color: #856404;
            font-size: 14px;
        `;const s=document.createElement("span");s.textContent="完成后将添加到上方的已完成表达式中",s.style.cssText=`
            font-size: 12px;
            color: #856404;
            margin-left: auto;
        `,t.appendChild(i),t.appendChild(r),t.appendChild(s),e.appendChild(t)}renderClickableMainExpression(e,t){if(e){if(e.type==="value"){const i=document.createElement("span");if(e.metadata&&e.metadata.type==="encapsulated"){this.renderEncapsulatedExpression(e,t);return}i.textContent=e.value,i.style.cssText=`
                cursor: pointer;
                padding: 2px 4px;
                border-radius: 3px;
                transition: background-color 0.2s;
                display: inline-block;
            `,this.selectedNode===e&&(i.style.border="3px solid #27ae60",i.style.fontWeight="700",i.style.borderRadius="4px"),i.addEventListener("mouseenter",()=>{i.style.backgroundColor="#e3f2fd"}),i.addEventListener("mouseleave",()=>{i.style.backgroundColor="transparent"}),t.appendChild(i)}else if(e.type==="operator"){e.leftOperand?this.renderClickableMainExpression(e.leftOperand,t):this.renderEmptySlotPlaceholder(e,"left",t);const i=document.createElement("span");i.textContent=` ${e.operator} `,i.style.cssText="color: #666; font-weight: bold; margin: 0 4px;",t.appendChild(i),e.rightOperand?this.renderClickableMainExpression(e.rightOperand,t):this.renderEmptySlotPlaceholder(e,"right",t)}}}expandMainExpression(){console.log("DslEditor: Expanding main expression"),this.clearEditingPath(),this.pushEditingPath({parentNode:this.virtualRoot,slot:"main",descriptor:"扩展主表达式"}),this.workspaceAST=this.cloneAST(this.virtualRoot.main),this.astRoot=this.workspaceAST,this.isExpressionCompleted=!1,this.renderEditor(),console.log("DslEditor: Main expression loaded into workspace for expansion.")}renderASTNodeRecursive(e,t){var i,r,s;if(e){if(console.log("DslEditor: renderASTNodeRecursive called for:",{type:e.type,value:e.value,metadataType:(i=e.metadata)==null?void 0:i.type}),e.type==="value"&&(((r=e.metadata)==null?void 0:r.type)==="property"||((s=e.metadata)==null?void 0:s.type)==="context"&&e.value.includes("."))){this.renderPropertyChainUI(e,t);return}if(e.isCompletedExpression){const n=document.createElement("span");n.className="dsl-completed-expression",n.style.cssText=`
                display: inline-block;
                background: linear-gradient(135deg, #e8f5e8 0%, #f0f8f0 100%);
                border: 2px solid #27ae60;
                border-radius: 6px;
                padding: 4px 8px;
                margin: 2px;
                box-shadow: 0 2px 4px rgba(39, 174, 96, 0.2);
                position: relative;
            `;const o=document.createElement("span");o.textContent="(",o.style.cssText="font-weight: bold; color: #27ae60; margin-right: 4px;",n.appendChild(o);const l=document.createElement("span");this.renderNodeContent(e,l),n.appendChild(l);const c=document.createElement("span");c.textContent=")",c.style.cssText="font-weight: bold; color: #27ae60; margin-left: 4px;",n.appendChild(c);const p=document.createElement("span");p.textContent="✓",p.style.cssText=`
                position: absolute;
                top: -8px;
                right: -8px;
                background: #27ae60;
                color: white;
                border-radius: 50%;
                width: 16px;
                height: 16px;
                font-size: 10px;
                display: flex;
                align-items: center;
                justify-content: center;
                font-weight: bold;
            `,n.appendChild(p),t.appendChild(n)}else this.renderNodeContent(e,t)}}renderNodeContent(e,t){e.type==="value"?this.renderValueNode(e,t):e.type==="operator"?this.renderOperatorNodeAST(e,t):e.type==="functionCall"?this.renderFunctionCallNodeAST(e,t):(e.type==="listQuery"||e.type==="listSort"||e.type==="listTake")&&this.renderListQueryNodeAST(e,t)}renderFunctionCallNodeAST(e,t){const i=document.createElement("span");i.className="dsl-node dsl-function-call",i.style.cssText=`
            background: #e8f5e8;
            color: #388e3c;
            padding: 4px 8px;
            border-radius: 4px;
            font-weight: 600;
            margin: 2px;
            display: inline-flex;
            align-items: center;
            gap: 2px;
        `;const r=document.createElement("span");r.textContent=e.functionName,i.appendChild(r);const s=document.createElement("span");s.textContent="(",i.appendChild(s);const n=e.args||[];for(let l=0;l<n.length;l++){if(l>0){const p=document.createElement("span");p.textContent=", ",i.appendChild(p)}const c=n[l];c?this.renderASTNodeRecursive(c,i):this.renderEmptySlotPlaceholder(e,"arg",i,l)}const o=document.createElement("span");o.textContent=")",i.appendChild(o),t.appendChild(i)}renderValueNode(e,t){var r;if(console.log("DslEditor: renderValueNode called for:",{type:e.type,value:e.value,hasMetadata:!!e.metadata,metadataType:(r=e.metadata)==null?void 0:r.type,isEncapsulated:e.metadata&&e.metadata.type==="encapsulated"}),e.metadata&&e.metadata.type==="encapsulated"){console.log("DslEditor: Rendering as encapsulated expression"),this.renderEncapsulatedExpression(e,t);return}const i=document.createElement("span");if(i.className="dsl-node dsl-value-node",e.metadata&&e.metadata.type==="context")i.textContent=e.value,i.style.cssText=`
                background: #e3f2fd;
                color: #1976d2;
                padding: 4px 8px;
                border-radius: 4px;
                font-weight: 500;
                margin: 2px;
                display: inline-block;
            `;else if(e.metadata.type==="property")i.textContent=e.value,i.style.cssText=`
                background: #f3e5f5;
                color: #7b1fa2;
                padding: 4px 8px;
                border-radius: 4px;
                font-weight: 500;
                margin: 2px;
                display: inline-block;
            `;else if(e.metadata.type==="staticValue")if(e.metadata.isAwaitingInput){this.renderStaticValueInput(e,t);return}else i.textContent=e.value,i.style.cssText=`
                    background: #fff3e0;
                    color: #f57c00;
                    padding: 4px 8px;
                    border-radius: 4px;
                    font-weight: 500;
                    margin: 2px;
                    display: inline-block;
                `;else e.metadata.type==="function"?(i.textContent=e.value,i.style.cssText=`
                background: #e8f5e8;
                color: #388e3c;
                padding: 4px 8px;
                border-radius: 4px;
                font-weight: 500;
                margin: 2px;
                display: inline-block;
            `):e.metadata.type==="eventField"&&(i.textContent=e.metadata.displayValue||e.value,i.style.cssText=`
                background: #ede7f6;
                color: #5e35b1;
                padding: 4px 8px;
                border-radius: 4px;
                font-weight: 600;
                margin: 2px;
                display: inline-block;
            `);t.appendChild(i)}renderEncapsulatedExpression(e,t){var l;if(e.metadata.innerAST&&((l=e.metadata.innerAST.metadata)==null?void 0:l.type)==="empty"){this.renderPlaceholderCapsule(e,t);return}const i=document.createElement("span");i.className="dsl-encapsulated-expression";const r=this.getEncapsulationStyle(e),s=this.selectedNode===e;i.style.cssText=`
            display: inline-flex;
            align-items: center;
            background: ${r.background};
            border: ${s?"3px":"2px"} solid ${r.borderColor};
            border-radius: 20px;
            padding: 6px 12px;
            margin: 2px;
            font-weight: ${s?"900":"500"};
            color: ${r.color};
            box-shadow: ${r.boxShadow};
            position: relative;
            cursor: pointer;
            transition: all 0.2s ease;
        `,i.addEventListener("mouseenter",()=>{i.style.transform="scale(1.02)",i.style.boxShadow=r.hoverBoxShadow}),i.addEventListener("mouseleave",()=>{i.style.transform="scale(1)",i.style.boxShadow=r.boxShadow});const n=document.createElement("span");n.style.cssText=`color: ${r.color}; font-weight: ${s?"900":"500"};`,e.metadata.innerAST?this.renderClickableSubExpressions(e.metadata.innerAST,n,e,s):n.textContent=this.removeBrackets(e.value),i.appendChild(n);const o=this.createStatusBadge(r);o&&i.appendChild(o),i.addEventListener("click",c=>{c.stopPropagation(),console.log("DslEditor: Clicked main encapsulated expression. Entering edit mode for virtualRoot's main slot."),this.selectedNode=e,this.handleEnterEditModeForSlot(this.virtualRoot,"main")}),t.appendChild(i)}renderPlaceholderCapsule(e,t){const i=document.createElement("span");i.className="dsl-placeholder-capsule";const r=this.selectedNode===e;if(i.style.cssText=`
            display: inline-flex;
            align-items: center;
            background: linear-gradient(135deg, #fff3cd 0%, #ffeaa7 100%);
            border: ${r?"3px":"2px"} solid #ffc107;
            border-radius: 20px;
            padding: 6px 12px;
            margin: 2px;
            font-weight: 700;
            color: #856404;
            box-shadow: 0 0 8px rgba(255, 193, 7, 0.6);
            cursor: pointer;
            transition: all 0.2s ease;
            position: relative;
            animation: pulse 2s infinite;
        `,!document.getElementById("placeholder-pulse-style")){const s=document.createElement("style");s.id="placeholder-pulse-style",s.textContent=`
                @keyframes pulse {
                    0% { box-shadow: 0 0 8px rgba(255, 193, 7, 0.6); }
                    50% { box-shadow: 0 0 16px rgba(255, 193, 7, 0.9); }
                    100% { box-shadow: 0 0 8px rgba(255, 193, 7, 0.6); }
                }
            `,document.head.appendChild(s)}i.textContent="?",i.addEventListener("click",s=>{s.stopPropagation(),this.selectedNode=e,this.handleEnterEditModeForSlot(this.virtualRoot,"main")}),t.appendChild(i)}getEncapsulationStyle(e){var s;const t=((s=e.metadata)==null?void 0:s.innerAST)||e,i=t._typeMatchInfo||this.calculateNodeTypeMatch(t);return!i.isTypeMatch&&i.needsCorrection?{background:"linear-gradient(135deg, #ffebee 0%, #ffcdd2 100%)",borderColor:"#e74c3c",color:"#c62828",boxShadow:"0 2px 4px rgba(231, 76, 60, 0.2)",hoverBoxShadow:"0 4px 8px rgba(231, 76, 60, 0.3)",badgeSymbol:"×",badgeColor:"#e74c3c",badgeTitle:`类型不匹配：期望 ${i.expectedType}，实际 ${i.actualType}`,hasIssue:!0}:{background:"linear-gradient(135deg, #e8f5e8 0%, #f0f8f0 100%)",borderColor:"#27ae60",color:"#2c3e50",boxShadow:"0 2px 4px rgba(39, 174, 96, 0.2)",hoverBoxShadow:"0 4px 8px rgba(39, 174, 96, 0.3)",badgeSymbol:"✓",badgeColor:"#27ae60",badgeTitle:"表达式已完成",hasIssue:!1}}createStatusBadge(e){const t=document.createElement("span");return t.textContent=e.badgeSymbol,t.title=e.badgeTitle,t.style.cssText=`
            position: absolute;
            top: -6px;
            right: -6px;
            background: ${e.badgeColor};
            color: white;
            border-radius: 50%;
            width: 16px;
            height: 16px;
            font-size: 10px;
            display: flex;
            align-items: center;
            justify-content: center;
            font-weight: bold;
            border: 2px solid white;
            ${e.hasIssue?"animation: pulse 2s infinite;":""}
        `,t}renderNestedEncapsulatedExpression(e,t,i){var d,u;const r=document.createElement("span");r.className="dsl-nested-encapsulated-expression";const s=this.selectedNode===e;r.style.cssText=`
            display: inline-flex;
            align-items: center;
            background: linear-gradient(135deg, #e8f5e8 0%, #f0f8f0 100%);
            border: ${s?"3px":"2px"} solid #27ae60;
            border-radius: 20px;
            padding: 6px 12px;
            margin: 2px;
            font-weight: ${s?"900":"500"};
            color: #2c3e50;
            box-shadow: 0 2px 4px rgba(39, 174, 96, 0.2);
            cursor: pointer;
            transition: all 0.2s ease;
            position: relative;
        `,r.addEventListener("mouseenter",()=>{this.selectedNode!==e&&(r.style.transform="scale(1.02)",r.style.boxShadow="0 4px 8px rgba(39, 174, 96, 0.3)")}),r.addEventListener("mouseleave",()=>{this.selectedNode!==e&&(r.style.transform="scale(1)",r.style.boxShadow="0 2px 4px rgba(39, 174, 96, 0.2)")}),r.addEventListener("click",m=>{m.stopPropagation(),console.log("点击嵌套封装表达式:",e),this.selectedNode=e,this.renderEditor(),this.editSubExpression(e)});const n=document.createElement("span");if(n.style.cssText=`color: #2c3e50; font-weight: ${s?"900":"500"};`,e.metadata.innerAST)this.renderClickableSubExpressions(e.metadata.innerAST,n,e,s);else{const m=e.metadata.displayValue||e.value;n.textContent=this.removeBrackets(m)}r.appendChild(n);let o=!1,l=null;if(console.log("DslEditor: Nested encapsulated expression analysis:",{nodeValue:e.value,nodeType:e.type,hasParent:!!e.parent,parentOperator:(d=e.parent)==null?void 0:d.operator,hasTypeMatchInfo:!!e._typeMatchInfo,hasMetadata:!!e.metadata,metadataType:(u=e.metadata)==null?void 0:u.type}),e._typeMatchInfo){const m=e._typeMatchInfo;o=!m.isTypeMatch&&m.needsCorrection,o&&(l=`类型不匹配：期望 ${m.expectedType}，实际 ${m.actualType}`),console.log("DslEditor: Nested encapsulated expression type check:",{nodeValue:e.value,expectedType:m.expectedType,actualType:m.actualType,isTypeMatch:m.isTypeMatch,needsCorrection:m.needsCorrection,hasTypeIssue:o})}else console.log("DslEditor: No type match info for nested encapsulated expression");const c=document.createElement("span");c.textContent=o?"×":"✓";const p=o?"#e74c3c":"#27ae60";if(c.style.cssText=`
            position: absolute;
            top: -6px;
            right: -6px;
            background: ${p};
            color: white;
            border-radius: 50%;
            width: 16px;
            height: 16px;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 10px;
            font-weight: bold;
            box-shadow: 0 1px 2px rgba(0,0,0,0.2);
            ${o?"animation: pulse 2s infinite;":""}
        `,o&&(c.title=l,!document.getElementById("pulse-animation-style"))){const m=document.createElement("style");m.id="pulse-animation-style",m.textContent=`
                    @keyframes pulse {
                        0% { box-shadow: 0 0 0 0 rgba(231, 76, 60, 0.7); }
                        70% { box-shadow: 0 0 0 10px rgba(231, 76, 60, 0); }
                        100% { box-shadow: 0 0 0 0 rgba(231, 76, 60, 0); }
                    }
                `,document.head.appendChild(m)}r.appendChild(c),t.appendChild(r)}removeBrackets(e){return typeof e!="string"?e:e.startsWith("(")&&e.endsWith(")")?e.slice(1,-1):e}renderClickableSubExpressions(e,t,i,r=!1){if(!e){const s=i.metadata.displayValue||i.value;t.textContent=this.removeBrackets(s);return}e.type==="value"&&e.metadata&&e.metadata.type==="encapsulated"?this.renderNestedEncapsulatedExpression(e,t,i):this.renderClickableASTNode(e,t,i,r)}renderClickableASTNode(e,t,i,r=!1){var n,o;if(!e)return;const s=this.getHighlightTargetNode?this.getHighlightTargetNode():null;if(e.type==="value"){if(((n=e.metadata)==null?void 0:n.type)==="empty"){this.renderEmptySlotPlaceholder(e.parent,((o=e.parent)==null?void 0:o.leftOperand)===e?"left":"right",t);return}if(e.metadata&&e.metadata.type==="encapsulated"){this.renderNestedEncapsulatedExpression(e,t,i);return}const l=document.createElement("span");l.textContent=e.value,l.style.cssText=`
                cursor: pointer;
                padding: 2px 4px;
                border-radius: 3px;
                transition: background-color 0.2s;
            `,s===e&&(l.style.outline="2px solid #0d6efd",l.style.outlineOffset="2px"),this.selectedNode===e?(l.style.border="3px solid #27ae60",l.style.fontWeight="900",l.style.borderRadius="4px"):r&&(l.style.fontWeight="900"),l.addEventListener("mouseenter",()=>{l.style.backgroundColor="#e3f2fd"}),l.addEventListener("mouseleave",()=>{l.style.backgroundColor="transparent"}),t.appendChild(l)}else if(e.type==="operator"){e.leftOperand&&this.renderClickableASTNode(e.leftOperand,t,i);const l=document.createElement("span");l.textContent=` ${e.operator} `,l.style.cssText="color: #666; font-weight: bold; margin: 0 4px;",(this.selectedNode===e||r)&&(l.style.fontWeight="900",l.style.color="#27ae60"),t.appendChild(l),e.rightOperand?this.renderClickableASTNode(e.rightOperand,t,i):this.renderEmptySlotPlaceholder(e,"right",t)}else(e.type==="listQuery"||e.type==="listSort"||e.type==="listTake")&&this.renderClickableListNode(e,t,i)}editSubExpression(e){if(console.log("DslEditor: Editing sub-expression:",e),this.selectedNode=e,!e.parent){console.warn("DslEditor: editSubExpression called on a node with no parent. Cannot determine path.");return}let t;if(e.parent.leftOperand===e)t="left";else if(e.parent.rightOperand===e)t="right";else if(e.parent.metadata&&e.parent.metadata.innerAST===e)t="innerAST";else if(e.parent.filterExpression===e)t="filterExpression";else if(e.parent.sortExpression===e)t="sortExpression";else if(e.parent.countExpression===e)t="countExpression";else{console.error("DslEditor: Cannot determine slot for subNode in its parent.");return}this.clearEditingPath(),this.pushEditingPath({parentNode:e.parent,slot:t,descriptor:"编辑子表达式"});const i=e.metadata&&e.metadata.innerAST?e.metadata.innerAST:e;this.workspaceAST=this.cloneAST(i),this.astRoot=this.workspaceAST,this.isExpressionCompleted=!1,this.renderEditor(),console.log("DslEditor: Sub-expression editing started with editingPath.")}isSimpleValueNode(e){if(!e.metadata||!e.metadata.innerAST)return!1;const t=e.metadata.innerAST;return t.type==="value"&&(!t.metadata||t.metadata.type!=="encapsulated"&&t.metadata.type!=="operator")}isLeafExpression(e){if(!e.metadata||!e.metadata.innerAST)return!0;const t=e.metadata.innerAST;return t.type==="value"&&(!t.metadata||t.metadata.type!=="encapsulated")?!0:!(t.type==="operator"||t.type==="value"&&t.metadata&&t.metadata.type==="encapsulated")}cloneAST(e){if(!e)return null;let t={};if(e.metadata){for(const i in e.metadata)if(Object.prototype.hasOwnProperty.call(e.metadata,i))if(i==="innerAST")t[i]=this.cloneAST(e.metadata[i]);else if(typeof e.metadata[i]=="object"&&e.metadata[i]!==null)try{t[i]=JSON.parse(JSON.stringify(e.metadata[i]))}catch{t[i]=e.metadata[i]}else t[i]=e.metadata[i]}if(e.type==="value"){const i=C(e.value,e.returnType,t);return i.isComplete=e.isComplete,i}else if(e.type==="operator"){const i=A(e.operator,e.returnType);return e.leftOperand&&i.setLeftOperand(this.cloneAST(e.leftOperand)),e.rightOperand&&i.setRightOperand(this.cloneAST(e.rightOperand)),i.isComplete=e.isComplete,i}else if(e.type==="functionCall"){const i=I(e.functionName,e.returnType,Array.isArray(e.parameters)?[...e.parameters]:[]);return(e.args||[]).forEach((r,s)=>{r&&i.setArg(s,this.cloneAST(r))}),i.isComplete=e.isComplete,i}else if(e.type==="listQuery"){const i=q(this.cloneAST(e.source));return e.filterExpression&&i.setFilterExpression(this.cloneAST(e.filterExpression)),i}else if(e.type==="listSort"){const i=_(this.cloneAST(e.source));return e.sortExpression&&i.setSortExpression(this.cloneAST(e.sortExpression)),i}else if(e.type==="listTake"){const i=W(this.cloneAST(e.source));return e.countExpression&&i.setCountExpression(this.cloneAST(e.countExpression)),i}return null}renderClickableListNode(e,t,i){var l;e.source&&this.renderClickableASTNode(e.source,t,i);const r=document.createElement("span");let s=e.type.replace("list","").toLowerCase();e.type==="listSort"&&(s="orderBy"),r.textContent=`.${s}`,r.style.cssText="",t.appendChild(r);let n=null,o="";e.type==="listQuery"?(n=e.filterExpression,o="filterExpression"):e.type==="listSort"?(n=e.sortExpression,o="sortExpression"):e.type==="listTake"&&(n=e.countExpression,o="countExpression"),n&&((l=n.metadata)==null?void 0:l.type)!=="empty"?this.renderClickableASTNode(n,t,i):this.renderEmptySlotPlaceholder(e,o,t)}renderListQueryNodeAST(e,t){const i=document.createElement("div");i.className="dsl-list-query-container",i.style.cssText=`
            display: inline-flex;
            align-items: center;
            margin: 2px;
        `,e.source&&this.renderASTNodeRecursive(e.source,i);const r=document.createElement("div");r.style.cssText=`
            display: flex;
            align-items: center;
            position: relative;
            background: #e1f5fe;
            color: #0277bd;
            padding: 4px 12px;
            font-weight: 500;
            margin: 0 12px; /* 为两侧箭头留出空间 */
        `;let s=e.type.replace("list","").toLowerCase();e.type==="listSort"&&(s="orderBy"),r.textContent=`.${s}`;const n=document.createElement("div");n.style.cssText=`
            position: absolute;
            left: -10px;
            top: 50%;
            transform: translateY(-50%);
            width: 0;
            height: 0;
            border-top: 10px solid transparent;
            border-bottom: 10px solid transparent;
            border-right: 10px solid #e1f5fe;
        `;const o=document.createElement("div");o.style.cssText=`
            position: absolute;
            right: -10px;
            top: 50%;
            transform: translateY(-50%);
            width: 0;
            height: 0;
            border-top: 10px solid transparent;
            border-bottom: 10px solid transparent;
            border-left: 10px solid #e1f5fe;
        `,r.appendChild(n),r.appendChild(o),i.appendChild(r);let l=null;e.type==="listQuery"?l=e.filterExpression:e.type==="listSort"?l=e.sortExpression:e.type==="listTake"&&(l=e.countExpression),this.renderASTNodeRecursive(l,i),t.appendChild(i)}renderOperatorNodeAST(e,t){var c;const i=document.createElement("div");i.className="dsl-operator-container",i.style.cssText=`
            display: inline-flex;
            align-items: center;
            gap: 8px;
            margin: 2px;
        `;const r=e.source||e.leftOperand;r&&this.renderASTNodeRecursive(r,i);const s=document.createElement("span");s.className="dsl-operator",e.type==="operator"?(s.textContent=e.operator,s.style.cssText=`
                background: #ffebee;
                color: #c62828;
                padding: 4px 8px;
                border-radius: 4px;
                font-weight: bold;
                margin: 0 4px;
            `):(s.textContent=`.${e.type.replace("list","").toLowerCase()}`,s.style.cssText=`
                background: #e0f7fa;
                color: #00796b;
                padding: 4px 8px;
                border-radius: 4px;
                font-weight: bold;
            `),i.appendChild(s);const n=document.createElement("span");n.textContent="(",i.appendChild(n);let o=null;e.type==="operator"?o=e.rightOperand:e.type==="listQuery"?o=e.filterExpression:e.type==="listSort"?o=e.sortExpression:e.type==="listTake"&&(o=e.countExpression),o&&((c=o.metadata)==null?void 0:c.type)!=="empty"&&this.renderASTNodeRecursive(o,i);const l=document.createElement("span");l.textContent=")",i.appendChild(l),t.appendChild(i)}renderStaticValueInput(e,t){const i=document.createElement("div");i.className="dsl-static-input-container",i.style.cssText=`
            display: inline-block;
            margin: 2px;
        `;const r=document.createElement("input");r.type="text",r.className="dsl-static-input",r.placeholder="输入值",r.value=e.metadata.staticValue||"",r.style.cssText=`
            border: 1px solid #ddd;
            border-radius: 4px;
            padding: 4px 8px;
            font-size: 12px;
            width: 80px;
            background: #fff3e0;
        `,r.addEventListener("input",s=>{const n=s.target.value.trim();e.metadata.staticValue=n,n?(e.value=this.formatStaticValue(n,this.inferStaticValueType(n)),e.returnType=this.inferStaticValueType(n),e.metadata.isAwaitingInput=!1):(e.value="[输入值]",e.returnType="Object",e.metadata.isAwaitingInput=!0),e.updateCompleteness(),e.parent&&e.parent.updateCompleteness(),this.updateExpressionFromAST(),this.onChange&&this.onChange(this.expression)}),i.appendChild(r),t.appendChild(i),setTimeout(()=>r.focus(),100)}exitSubEditor(){this.isSubEditor&&this.parentEditor&&(console.log("DslEditor: Exiting sub-editor, returning to parent"),this.onChange&&this.onChange(""),this.parentEditor.renderEditor())}goBack(){if(this.isSubEditor&&!this.astRoot){this.exitSubEditor();return}if(!this.astRoot)return;console.log("DslEditor: Going back from AST:",this.astRoot),this.isExpressionCompleted=!1;const e=this.performASTGoBack();e.success&&(this.astRoot=e.newRoot,this.isSubEditor&&this.astRoot),console.log("DslEditor: After going back, new AST root:",this.astRoot),console.log("DslEditor: Is sub-editor:",this.isSubEditor),this.updateExpressionFromAST(),this.renderEditor()}performASTGoBack(){if(!this.astRoot)return{success:!1,newRoot:null};if(this.astRoot.type==="value")return this.astRoot.metadata&&this.astRoot.metadata.type==="encapsulated"&&this.astRoot.metadata.innerAST?(console.log("DslEditor: Unencapsulating expression:",this.astRoot.metadata.innerAST),this.isExpressionCompleted=!1,{success:!0,newRoot:this.astRoot.metadata.innerAST}):{success:!0,newRoot:null};if(this.astRoot.type==="operator"){if(this.astRoot.rightOperand)return this.astRoot.setRightOperand(null),{success:!0,newRoot:this.astRoot};if(this.astRoot.leftOperand){const e=this.astRoot.leftOperand;return e.parent=null,{success:!0,newRoot:e}}return{success:!0,newRoot:null}}return{success:!1,newRoot:this.astRoot}}showAddNodeMenu(e){const t=document.querySelector(".dsl-add-menu");t&&t.remove();const i=document.createElement("div");i.className="dsl-options-dropdown";const r=this.getCurrentEditingOptions()||[];if(console.log("DslEditor: Showing options menu with",r.length,"options:",r),r.length===0){const m=document.createElement("div");m.className="dsl-option",m.innerHTML='<span class="dsl-option-label">暂无可用选项</span>',i.appendChild(m)}else{const m=this.groupOptionsByType(r);console.log("DslEditor: Grouped options:",m),Object.entries(m).forEach(([h,y])=>{if(console.log(`DslEditor: Rendering group "${h}" with ${y.length} options:`,y),y.length>0){const g=document.createElement("div");g.className="dsl-option-group",g.style.cssText=`
                        position: relative;
                        padding: 12px 16px;
                        cursor: pointer;
                        border-bottom: 1px solid #e9ecef;
                        background: #f8f9fa;
                        font-weight: 600;
                        color: #495057;
                        display: flex;
                        justify-content: space-between;
                        align-items: center;
                        transition: background-color 0.15s ease;
                        font-size: 14px;
                    `;const b=document.createElement("span");b.textContent=h,g.appendChild(b);const T=document.createElement("span");T.textContent="▶",T.style.cssText="font-size: 12px; color: #495057; margin-left: 8px;",g.appendChild(T),g.addEventListener("mouseenter",()=>{g.style.background="#e9ecef"}),g.addEventListener("mouseleave",()=>{g.style.background="#f8f9fa"});let v,E=null;const $=()=>{clearTimeout(v),E&&E.remove(),E=this.createSubmenu(y,g,i),document.body.appendChild(E),E.addEventListener("mouseenter",()=>{clearTimeout(v)}),E.addEventListener("mouseleave",()=>{v=setTimeout(()=>{E&&(E.remove(),E=null)},300)})},Z=()=>{v=setTimeout(()=>{E&&(E.remove(),E=null)},300)};g.addEventListener("mouseenter",$),g.addEventListener("mouseleave",Z),g._submenuCleanup=()=>{clearTimeout(v),E&&(E.remove(),E=null)},i.appendChild(g)}})}const s=e.getBoundingClientRect(),n=window.innerHeight,o=window.innerWidth;let l=s.bottom+5,c=s.left;const p=250,d=Math.min(400,n-s.bottom-20);c+p>o&&(c=o-p-10),l+d>n&&(l=s.top-d-5,l<10&&(l=10)),i.style.cssText=`
            position: fixed;
            left: ${c}px;
            top: ${l}px;
            z-index: 1000;
            max-height: ${d}px;
            overflow-y: auto;
            overflow-x: hidden;
            background: #ffffff;
            border: 1px solid #bdc3c7;
            border-radius: 6px;
            box-shadow: 0 6px 16px rgba(0,0,0,0.2);
            min-width: 200px;
            max-width: 300px;
        `,this.addScrollbarStyles(i),document.body.appendChild(i);const u=m=>{!i.contains(m.target)&&m.target!==e&&(i.querySelectorAll(".dsl-option-group").forEach(y=>{y._submenuCleanup&&y._submenuCleanup()}),document.querySelectorAll(".dsl-submenu").forEach(y=>{y.remove()}),i.remove(),document.removeEventListener("click",u))};setTimeout(()=>document.addEventListener("click",u),0)}createSubmenu(e,t,i){const r=document.createElement("div");r.className="dsl-submenu";const s=t.getBoundingClientRect(),n=i.getBoundingClientRect(),o=window.innerHeight,l=window.innerWidth;let c=n.right+5,p=s.top;const d=250,u=Math.min(350,o-s.top-20);return c+d>l&&(c=n.left-d-5,c<10&&(c=10)),p+u>o&&(p=o-u-10,p<10&&(p=10)),r.style.cssText=`
            position: fixed;
            left: ${c}px;
            top: ${p}px;
            min-width: 200px;
            max-width: 300px;
            max-height: ${u}px;
            overflow-y: auto;
            overflow-x: hidden;
            background: #ffffff;
            border: 1px solid #bdc3c7;
            border-radius: 6px;
            box-shadow: 0 6px 16px rgba(0,0,0,0.2);
            z-index: 1002;
            padding: 6px 0;
            font-size: 14px;
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
        `,e.forEach(m=>{const h=document.createElement("div");h.className="dsl-option",h.style.cssText=`
                padding: 10px 16px;
                cursor: pointer;
                border-bottom: none;
                background: #ffffff;
                transition: background-color 0.15s ease;
                border-radius: 3px;
                margin: 2px 4px;
            `;const y=document.createElement("span");if(y.className="dsl-option-label",y.textContent=m.label,y.style.cssText=`
                font-weight: 600;
                color: #1a1a1a;
                font-size: 14px;
                display: block;
            `,h.appendChild(y),m.description){const g=document.createElement("div");g.className="dsl-option-description",g.textContent=m.description,g.style.cssText=`
                    font-size: 12px;
                    color: #4a5568;
                    margin-top: 3px;
                    line-height: 1.3;
                `,h.appendChild(g)}h.addEventListener("mouseenter",()=>{if(h.style.background="#2196f3",h.style.borderRadius="4px",y.style.color="#ffffff",m.description){const g=h.querySelector(".dsl-option-description");g&&(g.style.color="#e3f2fd")}}),h.addEventListener("mouseleave",()=>{if(h.style.background="#ffffff",y.style.color="#1a1a1a",m.description){const g=h.querySelector(".dsl-option-description");g&&(g.style.color="#4a5568")}}),h.addEventListener("click",()=>{console.log("DslEditor: Option selected:",m),this.addNode(m),i.remove(),r.remove()}),r.appendChild(h)}),this.addScrollbarStyles(r),r}addScrollbarStyles(e){const t=document.createElement("style"),i=`dsl-menu-${Date.now()}-${Math.random().toString(36).substring(2,11)}`;e.classList.add(i),t.textContent=`
            .${i}::-webkit-scrollbar {
                width: 8px;
            }

            .${i}::-webkit-scrollbar-track {
                background: #f1f1f1;
                border-radius: 4px;
            }

            .${i}::-webkit-scrollbar-thumb {
                background: #c1c1c1;
                border-radius: 4px;
                transition: background 0.2s ease;
            }

            .${i}::-webkit-scrollbar-thumb:hover {
                background: #a1a1a1;
            }

            /* Firefox滚动条样式 */
            .${i} {
                scrollbar-width: thin;
                scrollbar-color: #c1c1c1 #f1f1f1;
            }
        `,document.head.appendChild(t);const r=new MutationObserver(s=>{s.forEach(n=>{n.removedNodes.forEach(o=>{o===e&&(t.remove(),r.disconnect())})})});r.observe(document.body,{childList:!0,subtree:!0})}groupOptionsByType(e){const t={数据源:[],属性:[],函数:[],操作符:[],筛选:[]};e.forEach(r=>{switch(r.type){case"context":case"staticValue":t.数据源.push(r);break;case"property":t.属性.push(r);break;case"function":t.函数.push(r);break;case"operator":t.操作符.push(r);break;case"listQuery":case"listSort":case"listTake":t.筛选.push(r);break;default:t.其他=t.其他||[],t.其他.push(r);break}});const i={};return Object.entries(t).forEach(([r,s])=>{s.length>0&&(i[r]=s)}),i}isFunctionCompatibleWithType(e,t){if(!e.parameters||e.parameters.length===0)return!1;const i=e.parameters[0];if(i.typeName===t||i.typeName==="Object")return!0;if(this.isBaseType(t)&&this.isBaseType(i.typeName))return this.areBaseTypesCompatible(t,i.typeName);if(this.isCollectionType(t)&&this.isCollectionType(i.typeName)){const r=this.getCollectionInnerType(t),s=this.getCollectionInnerType(i.typeName);return r===s||s==="Object"}return this.isTypeInheritanceCompatible(t,i.typeName)}addNode(e){var t;if(console.log("DslEditor: Adding node (AST):",e),this.selectedNode=null,e.type==="complete"){this.handleCompleteExpression();return}!this.astRoot||this.astRoot.type==="value"&&((t=this.astRoot.metadata)==null?void 0:t.type)==="empty"?this.createRootNode(e):this.extendAST(e),this.triggerStateUpdate()}handleStaticValueInput(e){console.log("DslEditor: Handling static value input:",e);let t=null;if(this.astRoot&&this.astRoot.metadata&&this.astRoot.metadata.type==="staticValue"&&this.astRoot.metadata.isAwaitingInput&&(t=this.astRoot),!t&&this.editingPath&&this.editingPath.length>0){const i=this.editingPath[this.editingPath.length-1];if(i.slot==="right"&&i.parentNode&&i.parentNode.rightOperand){const r=i.parentNode.rightOperand;r.metadata&&r.metadata.type==="staticValue"&&r.metadata.isAwaitingInput&&(t=r)}}t?(t.value=e,t.metadata.staticValue=e,t.metadata.isAwaitingInput=!1,t.updateCompleteness(),t.parent&&t.parent.updateCompleteness(),this.workspaceAST=t,this.astRoot=t,this.updateExpressionFromAST(),console.log("DslEditor: Static value updated:",t)):console.warn("DslEditor: No awaiting static value node found")}handleCompleteExpression(){if(!this.astRoot){this.showExpressionError("没有表达式可以完成");return}const e=this.validateExpressionForCompletion();if(!e.isValid){this.showExpressionError(e.error);return}this.encapsulateCurrentExpression(),this.renderEditor(),console.log("DslEditor: Expression encapsulated as completed sub-expression")}saveWorkspace(){if(console.log("DslEditor: Saving workspace content."),!this.workspaceAST){console.warn("DslEditor: saveWorkspace called with empty workspaceAST.");return}this.editingPath&&this.editingPath.length>0?this.saveCurrentLayer():console.warn("DslEditor: saveWorkspace called with no editing path.")}saveCurrentLayer(){const e=this.peekEditingPath();if(!e){console.warn("DslEditor: saveCurrentLayer called with empty editingPath");return}if(!this.workspaceAST||S(this.workspaceAST)){this.showExpressionError("工作区表达式不完整，无法保存。"),console.warn("DslEditor: saveCurrentLayer failed: workspaceAST is null or incomplete");return}const t=this.workspaceAST,{parentNode:i,slot:r,index:s}=e;if(!i){console.error("DslEditor: saveCurrentLayer failed: parentNode from editingPath is null."),this.showExpressionError("保存失败：编辑上下文丢失。");return}console.log("DslEditor: saveCurrentLayer - Found target parent node from editing path:",i);const n=this.createEncapsulatedFrom(t);let o=!1;if(i.type==="functionCall"&&r==="arg"){const l=typeof s=="number"?s:0,c=(i.parameters||[])[l];if(c!=null&&c.typeName){const p=this.normalizeTypeName(c.typeName),d=this.calculateNodeTypeMatch(t,p);if(p&&d&&d.isTypeMatch===!1){this.showExpressionError(`类型不匹配：期望 ${p}，实际 ${d.actualType}`);return}}typeof i.setArg=="function"&&(i.setArg(l,n),o=!0)}else o=this.writeBackToParentSlot(i,r,n);if(!o){console.warn("DslEditor: writeBackToParentSlot failed unexpectedly.");return}this.popEditingPath(),this.ascend(i)}ascend(e){console.log("DslEditor: Ascending to parent node:",e),e.type==="virtualRoot"?(console.log("DslEditor: Ascended to virtual root. Previewing main expression."),this.workspaceAST=this.cloneAST(this.virtualRoot.main),this.astRoot=this.workspaceAST,this.clearEditingPath()):(this.workspaceAST=this.cloneAST(e),this.astRoot=this.workspaceAST),this.updateExpressionFromAST(),this.renderEditor()}createEncapsulatedFrom(e){var r;const t=e&&e.toExpression?e.toExpression():"",i=`(${t})`;return C(i,(e==null?void 0:e.returnType)||"Object",{type:"encapsulated",innerAST:this.cloneAST(e),isCompleted:e&&((r=e.metadata)==null?void 0:r.type)!=="empty"?D(e).isValid:!1,displayValue:i,originalExpression:t})}getCurrentEditingExpression(){if(!this.astRoot)return null;const e=S(this.astRoot);if(e)return e.type==="operator"?e.rightOperand?e.rightOperand:null:e;const t=R(this.astRoot);return t&&t.metadata&&t.metadata.type==="encapsulated"?this.astRoot:t}encapsulateExpressionNode(e){if(!e)return;const t=this.getNodeExpression(e),i=`(${t})`,r=C(i,e.returnType,{type:"encapsulated",innerAST:e,isCompleted:!0,displayValue:i,originalExpression:t});this.replaceNodeInAST(e,r)}getNodeExpression(e){return e?e.toExpression?e.toExpression():e.value||"":""}replaceNodeInAST(e,t,i=null){const r=i||this.astRoot;if(r===e){i?this.fullExpressionAST=t:(this.astRoot=t,this.workspaceAST=t);return}this.replaceNodeRecursive(r,e,t)}replaceNodeRecursive(e,t,i){if(!e)return!1;if(e.type==="operator"){if(e.leftOperand===t)return e.setLeftOperand(i),!0;if(e.rightOperand===t)return e.setRightOperand(i),!0;if(this.replaceNodeRecursive(e.leftOperand,t,i)||this.replaceNodeRecursive(e.rightOperand,t,i))return!0}return!1}validateExpressionForCompletion(){if(S(this.astRoot))return{isValid:!1,error:"表达式不完整，请先完成所有必需的部分"};const t=D(this.astRoot);return t.isValid?{isValid:!0}:{isValid:!1,error:`表达式验证失败：${t.errors.join(", ")}`}}showExpressionError(e){const t=this.container.querySelector(".dsl-expression-error");t&&t.remove();const i=document.createElement("div");i.className="dsl-expression-error",i.style.cssText=`
            background: #ffebee;
            border: 1px solid #f44336;
            border-radius: 4px;
            padding: 8px 12px;
            margin: 8px 0;
            color: #c62828;
            font-size: 12px;
            display: flex;
            align-items: center;
            gap: 8px;
        `;const r=document.createElement("span");r.textContent="⚠️",i.appendChild(r);const s=document.createElement("span");s.textContent=e,i.appendChild(s);const n=document.createElement("button");n.textContent="×",n.style.cssText=`
            background: none;
            border: none;
            color: #c62828;
            font-size: 16px;
            cursor: pointer;
            margin-left: auto;
        `,n.addEventListener("click",()=>i.remove()),i.appendChild(n);const o=this.container.querySelector(".dsl-editor");o&&o.insertBefore(i,o.firstChild),setTimeout(()=>{i.parentNode&&i.remove()},3e3)}markASTAsCompleted(e){e&&(e.isCompletedExpression=!0,e.leftOperand&&this.markASTAsCompleted(e.leftOperand),e.rightOperand&&this.markASTAsCompleted(e.rightOperand))}createRootNode(e){var t,i,r,s,n,o,l,c;if(e.type==="context")this.astRoot=C(e.value,e.returnType,{type:"context",name:e.value,displayName:e.label});else if(e.type==="staticValue")this.astRoot=C("[输入值]","Object",{type:"staticValue",isAwaitingInput:!0,staticValue:""}),this.currentNode=this.astRoot;else if(e.type==="function"){const p=e.meta||{},d=p.parameters||p.Parameters||[];this.astRoot=I(e.value,e.returnType||p.returnType||p.returnTypeName||"Object",d)}else e.type==="eventField"&&(this.astRoot=C(e.value,e.returnType,{type:"eventField",displayValue:((t=e.meta)==null?void 0:t.displayValue)||e.label,eventType:(i=e.meta)==null?void 0:i.eventType,propertyName:(r=e.meta)==null?void 0:r.propertyName,scope:(s=e.meta)==null?void 0:s.scope}));this.workspaceAST=this.astRoot,console.log("DslEditor: Created root node, synced workspaceAST:",{astRoot:((o=(n=this.astRoot)==null?void 0:n.toExpression)==null?void 0:o.call(n))||null,workspaceAST:((c=(l=this.workspaceAST)==null?void 0:l.toExpression)==null?void 0:c.call(l))||null}),this.triggerStateUpdate()}extendAST(e){this.editingPath.length===0&&this.workspaceAST&&D(this.workspaceAST).isValid&&(console.log("DslEditor: Auto-expanding main expression before extending."),this.expandMainExpression());const t=S(this.astRoot);if(this.editingPath.length===0&&this.workspaceAST&&D(this.workspaceAST).isValid){console.log("DslEditor: Auto-expanding main expression before extending."),this.expandMainExpression();return}t&&t.type==="operator"?this.addRightOperandToOperator(t,e):t&&t.type==="functionCall"?this.addArgToFunctionCall(t,e):e.type==="operator"?this.createOperatorFromCurrent(e):e.type==="property"?this.extendPropertyAccess(e):e.type==="listQuery"?this.createQueryNodeFromCurrent(e):e.type==="listSort"?this.createSortNodeFromCurrent(e):e.type==="listTake"?this.createTakeNodeFromCurrent(e):e.type==="function"&&this.createFunctionCallFromCurrent(e)}createFunctionCallFromCurrent(e){if(!this.workspaceAST)return;const t=e.meta||{},i=t.parameters||t.Parameters||[],r=e.returnType||t.returnType||t.returnTypeName||"Object",s=I(e.value,r,i);i.length>0&&s.setArg(0,this.cloneAST(this.workspaceAST)),this.workspaceAST=s,this.astRoot=s;const n=i.findIndex((o,l)=>!(o!=null&&o.isOptional)&&!s.args[l]);n>=0&&this.handleEnterEditModeForSlot(s,"arg",n)}addArgToFunctionCall(e,t){const i=e.parameters||[],r=e.args||[],s=i.findIndex((l,c)=>!(l!=null&&l.isOptional)&&!r[c]);if(s<0){this.showExpressionError("函数参数已齐全或不可编辑");return}const o=(l=>{var c,p,d,u;if(l.type==="context")return C(l.value,l.returnType,{type:"context",name:l.value,displayName:l.label});if(l.type==="property")return C(l.value,l.returnType,{type:"property"});if(l.type==="staticValue")return C("[输入值]","Object",{type:"staticValue",isAwaitingInput:!0,staticValue:""});if(l.type==="function"){const m=l.meta||{},h=m.parameters||m.Parameters||[];return I(l.value,l.returnType||m.returnType||m.returnTypeName||"Object",h)}return l.type==="eventField"?C(l.value,l.returnType,{type:"eventField",displayValue:((c=l.meta)==null?void 0:c.displayValue)||l.label,eventType:(p=l.meta)==null?void 0:p.eventType,propertyName:(d=l.meta)==null?void 0:d.propertyName,scope:(u=l.meta)==null?void 0:u.scope}):null})(t);o&&(e.setArg(s,o),this.astRoot=this.astRoot,this.workspaceAST=this.astRoot)}addRightOperandFromWorkspace(e){var t,i,r,s;if(console.log("DslEditor: Adding right operand from workspace:",e),e.type==="context")this.astRoot=C(e.value,e.returnType,{type:"context",name:e.value,displayName:e.label});else if(e.type==="staticValue")this.astRoot=C("[输入值]","Object",{type:"staticValue",isAwaitingInput:!0,staticValue:""}),this.currentNode=this.astRoot;else if(e.type==="function"){const n=e.meta||{},o=n.parameters||n.Parameters||[];this.astRoot=I(e.value,e.returnType||n.returnType||n.returnTypeName||"Object",o)}else e.type==="eventField"&&(this.astRoot=C(e.value,e.returnType,{type:"eventField",displayValue:((t=e.meta)==null?void 0:t.displayValue)||e.label,eventType:(i=e.meta)==null?void 0:i.eventType,propertyName:(r=e.meta)==null?void 0:r.propertyName,scope:(s=e.meta)==null?void 0:s.scope}));this.workspaceAST=this.astRoot,console.log("DslEditor: Right operand created in workspace, awaiting completion")}addRightOperandToOperator(e,t){let i;t.type==="context"?i=C(t.value,t.returnType,{type:"context",name:t.value,displayName:t.label}):t.type==="staticValue"?(i=C("[输入值]","Object",{type:"staticValue",isAwaitingInput:!0,staticValue:""}),this.currentNode=i):t.type==="function"&&(i=C(`${t.value}()`,t.returnType,{type:"function",functionName:t.value,arguments:[]})),i&&(e.setRightOperand(i),this.workspaceAST=e,this.astRoot=e,console.log("DslEditor: addRightOperandToOperator -> right operand set, operator remains in workspace"))}createOperatorFromCurrent(e){const t=e.operator||e.value;if(!this.workspaceAST){console.error("DslEditor: Cannot create operator, workspace is empty.");return}const i=this.peekEditingPath();if(!i||!i.parentNode){console.error("DslEditor: Cannot create operator, no editing context found for auto-save."),this.showExpressionError("无法添加操作符：编辑上下文丢失。");return}const r=this.cloneAST(this.workspaceAST);this.writeBackToParentSlot(i.parentNode,i.slot,r),console.log("DslEditor: Step A - Auto-saved workspace to main AST.",r);const s=C("","Void",{type:"empty"}),n=A(t);n.setLeftOperand(r),n.setRightOperand(s),console.log("DslEditor: Step B - Created new operator node.",n);const o=this.createEncapsulatedFrom(n);console.log("DslEditor: Step C - Encapsulated the new operator node.",o),this.writeBackToParentSlot(i.parentNode,i.slot,o),console.log("DslEditor: Step D - Wrote back the final encapsulated operator node to main AST.");const l=o.metadata.innerAST;this.handleEnterEditModeForSlot(l,"right"),console.log("DslEditor: Step D - Switched to edit mode for the new placeholder.")}createQueryNodeFromCurrent(e){if(!this.workspaceAST)return;const t=this.peekEditingPath();if(!t||!t.parentNode){this.showExpressionError("无法添加筛选：编辑上下文丢失。");return}const i=this.cloneAST(this.workspaceAST);this.writeBackToParentSlot(t.parentNode,t.slot,i);const r=q(i),s=C("","Void",{type:"empty"});r.setFilterExpression(s);const n=this.createEncapsulatedFrom(r);this.writeBackToParentSlot(t.parentNode,t.slot,n);const o=n.metadata.innerAST;this.handleEnterEditModeForSlot(o,"filterExpression")}createSortNodeFromCurrent(e){if(!this.workspaceAST)return;const t=this.peekEditingPath();if(!t||!t.parentNode){this.showExpressionError("无法添加排序：编辑上下文丢失。");return}const i=this.cloneAST(this.workspaceAST);this.writeBackToParentSlot(t.parentNode,t.slot,i);const r=_(i),s=C("","Void",{type:"empty"});r.setSortExpression(s);const n=this.createEncapsulatedFrom(r);this.writeBackToParentSlot(t.parentNode,t.slot,n);const o=n.metadata.innerAST;this.handleEnterEditModeForSlot(o,"sortExpression")}createTakeNodeFromCurrent(e){if(!this.workspaceAST)return;const t=this.peekEditingPath();if(!t||!t.parentNode){this.showExpressionError("无法添加获取：编辑上下文丢失。");return}const i=this.cloneAST(this.workspaceAST);this.writeBackToParentSlot(t.parentNode,t.slot,i);const r=W(i),s=C("","Void",{type:"empty"});r.setCountExpression(s);const n=this.createEncapsulatedFrom(r);this.writeBackToParentSlot(t.parentNode,t.slot,n);const o=n.metadata.innerAST;this.handleEnterEditModeForSlot(o,"countExpression")}addOperatorToASTStructure(e){console.log("DslEditor: Adding operator to AST structure:",{operator:e,currentWorkspaceAST:this.astRoot,mainExpressionAST:this.mainExpressionAST});const t=this.analyzeCurrentASTStructure();console.log("DslEditor: Current AST structure analysis:",t);const i=this.determineOperatorAdditionStrategy(t,e);return console.log("DslEditor: Operator addition strategy:",i),this.executeOperatorAdditionStrategy(t,e,i)}analyzeCurrentASTStructure(){var i,r;console.log("DslEditor: Analyzing AST structure:",{mainExpressionAST:this.virtualRoot.main,astRoot:this.astRoot});const e=this.getCurrentCompleteExpression();console.log("DslEditor: Current complete expression:",e);const t={hasMainExpression:!!this.virtualRoot.main,hasWorkspaceExpression:!!this.astRoot,currentCompleteExpression:e,currentExpressionType:(e==null?void 0:e.type)||null,isEditingComplete:this.isCurrentExpressionComplete(),canAddOperator:this.canAddOperatorToCurrentExpression()};return e&&(e.type==="operator"?(t.currentOperator=e.operator,t.operatorComplete=!!(e.leftOperand&&e.rightOperand)):((i=e.metadata)==null?void 0:i.type)==="encapsulated"&&(t.isEncapsulated=!0,t.innerAST=e.metadata.innerAST,((r=t.innerAST)==null?void 0:r.type)==="operator"&&(t.innerOperator=t.innerAST.operator,t.innerOperatorComplete=!!(t.innerAST.leftOperand&&t.innerAST.rightOperand)))),console.log("DslEditor: AST structure analysis result:",t),t}getCurrentCompleteExpression(){return this.astRoot&&this.isASTComplete(this.astRoot)?this.astRoot:this.virtualRoot.main?this.virtualRoot.main:null}isCurrentExpressionComplete(){const e=this.getCurrentCompleteExpression();return e?this.isASTComplete(e):!1}canAddOperatorToCurrentExpression(){const e=this.getCurrentCompleteExpression();return e?this.isASTComplete(e)?!0:!(e.type==="operator"&&(!e.leftOperand||!e.rightOperand)):!1}isASTComplete(e){return e?e.type==="value"?!0:e.type==="operator"?!!(e.leftOperand&&e.rightOperand):!1:!1}determineOperatorAdditionStrategy(e,t){var r;if(console.log("DslEditor: Determining strategy for operator:",t,"with analysis:",e),!e.canAddOperator)return console.error("DslEditor: Cannot add operator to incomplete expression"),"error_incomplete_expression";if(!e.currentCompleteExpression)return"create_new_main_expression";const i=e.currentCompleteExpression;if(i.type==="operator"){const s=this.needsNestedStructure(i.operator,t);return console.log("DslEditor: Current operator:",i.operator,"new operator:",t,"needs nesting:",s),s?"encapsulate_current_and_extend":"extend_operator_chain"}else return((r=i.metadata)==null?void 0:r.type)==="encapsulated"?"extend_encapsulated_expression":"create_operator_from_value"}executeOperatorAdditionStrategy(e,t,i){switch(console.log("DslEditor: Executing operator addition strategy:",i),i){case"create_new_main_expression":return this.createNewMainExpression(t);case"extend_encapsulated_expression":return this.extendEncapsulatedExpression(e.currentCompleteExpression,t);case"extend_operator_chain":return this.extendOperatorChain(e.currentCompleteExpression,t);case"encapsulate_current_and_extend":return this.encapsulateCurrentAndExtend(e.currentCompleteExpression,t);case"create_operator_from_value":return this.createOperatorFromValue(e.currentCompleteExpression,t);case"error_incomplete_expression":return{success:!1,error:"Cannot add operator to incomplete expression"};default:return{success:!1,error:`Unknown strategy: ${i}`}}}extendEncapsulatedExpression(e,t){console.log("DslEditor: Extending encapsulated expression:",e.value,"with operator:",t);const i=A(t);return i.setLeftOperand(e),{success:!0,newMainAST:i,operatorNode:i}}extendOperatorChain(e,t){console.log("DslEditor: Extending operator chain:",e.operator,"with operator:",t);const i=A(t);return i.setLeftOperand(e),{success:!0,newMainAST:i,operatorNode:i}}encapsulateCurrentAndExtend(e,t){console.log("DslEditor: Encapsulating current expression and extending with operator:",t);const i=A(t),r=this.createEncapsulatedFrom(e);return i.setLeftOperand(r),{success:!0,newMainAST:this.createEncapsulatedFrom(i),operatorNode:i}}createOperatorFromValue(e,t){console.log("DslEditor: Creating operator from value:",e.value,"with operator:",t);const i=A(t);return i.setLeftOperand(e),{success:!0,newMainAST:this.createEncapsulatedFrom(i),operatorNode:i}}createNewMainExpression(e){var r;console.log("DslEditor: Creating new main expression with operator:",e);const t=A(e);if(this.astRoot){const s=((r=this.astRoot.metadata)==null?void 0:r.type)==="encapsulated"?this.astRoot:this.createEncapsulatedFrom(this.astRoot);t.setLeftOperand(s)}return{success:!0,newMainAST:this.createEncapsulatedFrom(t),operatorNode:t}}needsNestedStructure(e,t){const i={"||":1,"&&":2,"==":3,"!=":3,">":4,"<":4,">=":4,"<=":4,"+":5,"-":5,"*":6,"/":6},r=i[e]||0;return(i[t]||0)<r}extendPropertyAccess(e){const t=R(this.astRoot);if(t&&t.type==="value"&&t.metadata.type==="context"){const i=`${t.value}.${e.value}`,r=C(i,e.returnType,{type:"property",contextObject:t.value,property:e.value,fullPath:i});this.replaceNodeInAST(t,r)}else if(t&&t.type==="value"&&t.metadata.type==="property"){const i=`${t.value}.${e.value}`,r=C(i,e.returnType,{type:"property",fullPath:i,isNested:!0});this.replaceNodeInAST(t,r),this.astRoot===t&&(this.astRoot=r,this.workspaceAST=r)}}maintainASTRelationships(e,t=null){var r;if(!e)return;e.parent=t;let i=null;e===this.virtualRoot.main&&(i=this.expectedType),e._typeMatchInfo=this.calculateNodeTypeMatch(e,i),console.log("DslEditor: Maintaining AST relationships for node:",{type:e.type,value:e.value,hasParent:!!e.parent,parentOperator:(r=e.parent)==null?void 0:r.operator,typeMatchInfo:e._typeMatchInfo}),e.type==="operator"&&(e.leftOperand&&this.maintainASTRelationships(e.leftOperand,e),e.rightOperand&&this.maintainASTRelationships(e.rightOperand,e)),e.metadata&&e.metadata.innerAST&&this.maintainASTRelationships(e.metadata.innerAST,null)}updateExpressionFromAST(){if(console.log("DslEditor: updateExpressionFromAST called"),this.astRoot){console.log("DslEditor: Maintaining AST relationships for astRoot");try{this.maintainASTRelationships(this.astRoot)}catch(e){console.error("DslEditor: Error maintaining AST relationships for astRoot:",e)}}if(this.virtualRoot.main){console.log("DslEditor: Maintaining AST relationships for mainExpressionAST");try{this.maintainASTRelationships(this.virtualRoot.main)}catch(e){console.error("DslEditor: Error maintaining AST relationships for mainExpressionAST:",e)}}this.expression=this.astRoot?this.astRoot.toExpression():"",console.log("DslEditor: Expression updated from AST:",this.expression),this.triggerStateUpdate(),this.emitExpressionChange(this.expression)}triggerStateUpdate(){var e,t,i,r,s,n,o;window.DEBUG_DSL&&console.log("DslEditor: Current state:",{expression:this.expression,astRoot:((t=(e=this.astRoot)==null?void 0:e.toExpression)==null?void 0:t.call(e))||null,workspaceAST:((r=(i=this.workspaceAST)==null?void 0:i.toExpression)==null?void 0:r.call(i))||null,mainExpressionAST:((n=(s=this.virtualRoot.main)==null?void 0:s.toExpression)==null?void 0:n.call(s))||null,editingPathDepth:((o=this.editingPath)==null?void 0:o.length)||0,isExpressionCompleted:this.isExpressionCompleted}),this.renderEditor(),typeof window.forceUpdateDebug=="function"&&window.forceUpdateDebug()}emitExpressionChange(e){if(this.onChange&&this.lastEmittedExpression!==e){this.lastEmittedExpression=e;try{this.onChange(e)}catch(t){console.error("DslEditor: onChange handler threw an error:",t)}}}isLogicalOperator(e){return["&&","||"].includes(e)}getDslContextObject(e){var r,s,n;if(this.contextOverride&&this.contextOverride.name===e)return console.log(`DslEditor: Using context override for "${e}":`,this.contextOverride),{name:this.contextOverride.name,displayName:this.contextOverride.displayName,typeName:this.contextOverride.typeName,properties:((r=this.getTypeMetadata(this.contextOverride.typeName))==null?void 0:r.properties)||[]};const i=(((n=(s=this.metadata)==null?void 0:s.dsl)==null?void 0:n.contextObjects)||[]).find(o=>o.name===e);return console.log(`DslEditor: Looking for context object "${e}", found:`,i),i}getTypeMetadata(e){var i,r;if(!e||!((r=(i=this.metadata)==null?void 0:i.dsl)!=null&&r.typeMetadata))return null;console.log(`DslEditor: Getting type metadata for: "${e}"`);let t=this.metadata.dsl.typeMetadata[e];if(t)return console.log(`DslEditor: Found direct match for "${e}":`,t),t;if(e.includes("<")&&e.includes(">")){const s=e.split("<")[1].replace(">","");return t=this.metadata.dsl.typeMetadata[s],console.log(`DslEditor: Extracted inner type "${s}" from "${e}":`,t),t}return console.log(`DslEditor: No metadata found for type: "${e}"`),null}clear(){this.clearWorkspace()}isBaseType(e){return["Int32","Boolean","String","Guid","Single","Double","Int64"].includes(e)}isCollectionType(e){const t=this.normalizeTypeName(e);return t?t.startsWith("List<")||t.startsWith("IEnumerable<")||t==="List"||t==="IEnumerable":!1}getCollectionInnerType(e){const t=this.normalizeTypeName(e);if(!this.isCollectionType(t))return null;const i=t.match(/<(.*)>/);return i?i[1]:"Object"}areBaseTypesCompatible(e,t){const i=["Int32","Int64","Single","Double"];return!!(i.includes(e)&&i.includes(t)||e==="String"&&t==="String"||e==="Boolean"&&t==="Boolean"||e==="Guid"&&t==="Guid")}isTypeInheritanceCompatible(e,t){const i=this.getTypeMetadata(e),r=this.getTypeMetadata(t);return!i||!r?!1:!!(e.includes(t)||t.includes(e))}areTypesCompatible(e,t){const i=this.normalizeTypeName(e),r=this.normalizeTypeName(t);if(!i||!r)return!1;if(i===r)return!0;const s=["Int32","Number","Single","Double","Int64"];if(s.includes(i)&&s.includes(r))return!0;if(this.isCollectionType(i)&&this.isCollectionType(r)){const n=this.getCollectionInnerType(i),o=this.getCollectionInnerType(r);return o==="Object"||n===o}return!1}getSmartOperators(e){return ie(this,e)}isNumericType(e){return["Int32","Int64","Single","Double"].includes(e)}isBusinessObjectType(e){return["PlayerState","CardInstance","CardDefinition","EffectInstance","EventInstance"].includes(e)}isTextType(e){return["String","Guid","CardType","Rarity","Craft","CardKeyword"].includes(e)}isObjectType(e){return!this.isBaseType(e)&&!this.isTextType(e)&&e!=="Boolean"&&!this.isCollectionType(e)}getEnumValues(e){var r,s;const i=(((s=(r=this.metadata)==null?void 0:r.dsl)==null?void 0:s.enumTypes)||[]).find(n=>n.name===e);return(i==null?void 0:i.values)||[]}getTextOperators(){return re()}getTextFunctions(){var t,i;return(((i=(t=this.metadata)==null?void 0:t.dsl)==null?void 0:i.functions)||[]).filter(r=>r.name==="contains"||r.name==="isEmpty").map(r=>({type:"function",value:r.name,label:r.name,description:r.description,meta:r}))}getCollectionFunctions(){var i,r;const e=((r=(i=this.metadata)==null?void 0:i.dsl)==null?void 0:r.functions)||[],t=["count","sum","min","max","contains","isEmpty"];return e.filter(s=>t.includes(s.name)).map(s=>({type:"function",value:s.name,label:s.name,description:s.description,meta:s}))}getListQueryOperations(){return[{type:"listQuery",value:".query",label:"筛选 (.query)",description:"筛选集合元素"}]}getBooleanExpressionOptions(){var s,n,o,l;let e=[];console.log("DslEditor: Getting boolean expression options");const t=((n=(s=this.metadata)==null?void 0:s.dsl)==null?void 0:n.contextObjects)||[];e.push(...t.map(c=>({type:"context",value:c.name,label:c.displayName,description:c.description,returnType:c.typeName}))),e.push({type:"staticValue",value:"input",label:"输入值",description:"输入常量值 (数值、文本、布尔值等)"}),e.push(...this.getTriggeringEventFieldOptions());const r=(((l=(o=this.metadata)==null?void 0:o.dsl)==null?void 0:l.functions)||[]).filter(c=>c.returnType==="Boolean").map(c=>({type:"function",value:c.name,label:c.name,description:c.description,returnType:c.returnType,meta:c}));return e.push(...r),e}getDataSourceOptions(){var i,r;let e=[];console.log("DslEditor: Getting data source options");const t=((r=(i=this.metadata)==null?void 0:i.dsl)==null?void 0:r.contextObjects)||[];return e.push(...t.map(s=>({type:"context",value:s.name,label:s.displayName,description:s.description,returnType:s.typeName}))),e.push({type:"staticValue",value:"input",label:"输入值",description:"输入常量值 (数值、文本、布尔值等)"}),e.push(...this.getTriggeringEventFieldOptions()),e}getTriggeringEventFieldOptions(){var m;const e=this.triggeringEventContext,t=(e==null?void 0:e.eventTypes)||[];if(!Array.isArray(t)||t.length===0)return[];const r=((e==null?void 0:e.timing)||"On").toString().toLowerCase()==="after",s=t[0],n=(((m=this.metadata)==null?void 0:m.events)||[]).find(h=>String(h.eventType??h.EventType??"").toLowerCase()===String(s).toLowerCase());if(!n)return[];const o=n.displayName||n.DisplayName||String(s),l=n.parameters||n.Parameters||[],c=h=>String(h||"both").toLowerCase(),p=h=>{const y=c(h);return y==="both"||y==="input"?!0:y==="output"?r:!0},d=h=>this.normalizeTypeName(h)||"Object",u=h=>{const y=d(h);return y==="Boolean"?"eventBool":y==="String"?"eventString":y==="Guid"?"eventGuid":"eventInt"};return l.filter(h=>p(h.scope??h.Scope)).map(h=>{const y=h.propertyName||h.PropertyName||h.name,g=h.displayName||h.DisplayName||y,b=d(h.typeName||h.TypeName||h.type),T=c(h.scope||h.Scope);return{type:"eventField",value:`${u(b)}("${y}")`,label:`${o}.${g}`,description:`${h.description||h.Description||""}（${T}，${this.getDisplayTypeName(b)}）`,returnType:b,meta:{eventType:s,propertyName:y,displayValue:`${o}.${g}`,scope:T}}})}formatStaticValue(e,t){return e?this.isNumericType(t)||t==="Boolean"?e:t==="String"?e.startsWith('"')&&e.endsWith('"')||e.startsWith("'")&&e.endsWith("'")?e:`"${e}"`:`"${e}"`:'""'}inferStaticValueType(e){return e?e==="true"||e==="false"?"Boolean":/^-?\d+$/.test(e)?"Int32":/^-?\d*\.\d+$/.test(e)?"Double":(e.startsWith('"')&&e.endsWith('"')||e.startsWith("'")&&e.endsWith("'"),"String"):"Object"}getObjectProperties(e){const t=this.getTypeMetadata(e);if(!t||!t.properties)return[];const i=this.getDisplayTypeName(e);return t.properties.map(r=>({type:"property",value:r.name,label:r.displayName||r.name,description:r.description||`${i}的${r.name}属性`,returnType:r.typeName}))}clearWorkspace(e=!1){console.log(`DslEditor: Clearing workspace (preserveMainExpression: ${e})`),e||(this.virtualRoot.main=null),this.initializeWorkspace(),this.updateExpressionFromAST(),this.renderEditor(),console.log("DslEditor: Workspace cleared and re-initialized")}handleEnterEditModeForSlot(e,t,i=null){var s;console.log(`DslEditor: Entering edit mode for slot "${t}"${i!==null?` (index ${i})`:""} of node:`,e),this.contextOverride=null,this.slotExpectedType=null,this.clearEditingPath(),this.pushEditingPath({parentNode:e,slot:t,descriptor:`编辑 ${t}`,index:i});let r=null;if(e.type==="virtualRoot")r=e.main;else if(t==="left")r=e.leftOperand;else if(t==="right")r=e.rightOperand;else if(e.type==="functionCall"&&t==="arg"){const n=typeof i=="number"?i:0;r=(e.args||[])[n]||null;const o=(e.parameters||[])[n];o!=null&&o.typeName&&(this.slotExpectedType=this.normalizeTypeName(o.typeName))}else if(e.type==="listQuery"&&t==="filterExpression"){r=e.filterExpression;const n=e.source.returnType,o=this.getCollectionInnerType(n);o&&(this.contextOverride={name:"self",displayName:"当前项 (self)",typeName:o},console.log(`DslEditor: Context override set for query. 'self' is now '${o}'`))}else if(e.type==="listSort"&&t==="sortExpression"){r=e.sortExpression;const n=e.source.returnType,o=this.getCollectionInnerType(n);o&&(this.contextOverride={name:"self",displayName:"当前项 (self)",typeName:o},console.log(`DslEditor: Context override set for sort. 'self' is now '${o}'`))}else e.type==="listTake"&&t==="countExpression"&&(r=e.countExpression);r&&((s=r.metadata)==null?void 0:s.type)==="encapsulated"?this.workspaceAST=this.cloneAST(r.metadata.innerAST):this.workspaceAST=this.cloneAST(r),this.astRoot=this.workspaceAST,this.isExpressionCompleted=!1,this.renderEditor(),console.log("DslEditor: Workspace is ready for editing the specified slot.")}renderEmptySlotPlaceholder(e,t,i,r=null){const s=document.createElement("span");if(s.className="dsl-placeholder-capsule",s.style.cssText=`
            display: inline-flex;
            align-items: center;
            background: linear-gradient(135deg, #fff3cd 0%, #ffeaa7 100%);
            border: 3px solid #ffc107;
            border-radius: 20px;
            padding: 6px 12px;
            margin: 2px;
            font-weight: 700;
            color: #856404;
            box-shadow: 0 0 8px rgba(255, 193, 7, 0.6);
            cursor: pointer;
            transition: all 0.2s ease;
            position: relative;
            animation: pulse 2s infinite;
        `,!document.getElementById("placeholder-pulse-style")){const n=document.createElement("style");n.id="placeholder-pulse-style",n.textContent=`
                @keyframes pulse {
                    0% { box-shadow: 0 0 8px rgba(255, 193, 7, 0.6); }
                    50% { box-shadow: 0 0 16px rgba(255, 193, 7, 0.9); }
                    100% { box-shadow: 0 0 8px rgba(255, 193, 7, 0.6); }
                }
            `,document.head.appendChild(n)}s.textContent="?",s.addEventListener("click",n=>{n.stopPropagation(),console.log(`DslEditor: Clicked empty placeholder for ${t}${r!==null?`[${r}]`:""} operand of:`,e),this.handleEnterEditModeForSlot(e,t,r)}),i.appendChild(s)}renderPropertyChainUI(e,t){const i=document.createElement("div"),r=this.calculateASTReturnType(e),s=this.getStyleForValueType(r);i.style.cssText=`
            display: inline-flex;
            align-items: center;
            background: ${s.background};
            color: ${s.color};
            padding: 4px 8px;
            border-radius: 4px;
            font-weight: 500;
            margin: 2px;
        `;const n=e.value.split(".");n.forEach((o,l)=>{const c=document.createElement("span");if(c.textContent=o,i.appendChild(c),l<n.length-1){const p=document.createElement("span");p.textContent=".",p.style.cssText="font-weight: bold; margin: 0 2px;",i.appendChild(p)}}),t.appendChild(i)}getStyleForValueType(e){let t={background:"#f3e5f5",color:"#7b1fa2"};return this.isNumericType(e)?t={background:"#fff3e0",color:"#f57c00"}:e==="Boolean"?t={background:"#e8f5e8",color:"#388e3c"}:e==="String"||this.isTextType(e)?t={background:"#f3e5f5",color:"#7b1fa2"}:e==="context"&&(t={background:"#e3f2fd",color:"#1976d2"}),t}getReadablePreviewText(){var t;const e=((t=this.virtualRoot)==null?void 0:t.main)||null;return e?this.toReadableText(e):"(空)"}toReadableText(e){var t,i;if(!e)return"?";if(e.type==="value")return((t=e.metadata)==null?void 0:t.type)==="encapsulated"&&e.metadata.innerAST?this.toReadableText(e.metadata.innerAST):((i=e.metadata)==null?void 0:i.type)==="eventField"?e.metadata.displayValue||e.value||"?":e.value||"?";if(e.type==="operator"){const r={"&&":"且","||":"或","==":"等于","!=":"不等于",">":"大于",">=":"大于等于","<":"小于","<=":"小于等于","+":"加","-":"减","*":"乘","/":"除"},s=this.toReadableText(e.leftOperand),n=this.toReadableText(e.rightOperand),o=r[e.operator]||e.operator;return`(${s} ${o} ${n})`}if(e.type==="functionCall"){const r=(e.args||[]).map(s=>s?this.toReadableText(s):"?");return`${e.functionName}(${r.join(", ")})`}if(e.type==="listQuery"){const r=this.toReadableText(e.source),s=this.toReadableText(e.filterExpression);return`${r} 筛选(${s})`}if(e.type==="listSort"){const r=this.toReadableText(e.source),s=this.toReadableText(e.sortExpression);return`${r} 排序(${s})`}if(e.type==="listTake"){const r=this.toReadableText(e.source),s=this.toReadableText(e.countExpression);return`${r} 取前(${s})`}return"?"}}class de{constructor(){this.metadataManager=null,this.componentRegistry=new Map,this.isInitialized=!1}async initialize(e){this.isInitialized||(this.metadataManager=e,this.registerDefaultComponents(),this.isInitialized=!0,console.log("DynamicUIRenderer: Initialized successfully"))}registerDefaultComponents(){this.componentRegistry.set("TextInput",this.createTextInput.bind(this)),this.componentRegistry.set("NumberStepper",this.createNumberStepper.bind(this)),this.componentRegistry.set("Checkbox",this.createCheckbox.bind(this)),this.componentRegistry.set("Dropdown",this.createDropdown.bind(this)),this.componentRegistry.set("TextArea",this.createTextArea.bind(this)),this.componentRegistry.set("CardSelector",this.createCardSelector.bind(this)),this.componentRegistry.set("TargetSelector",this.createTargetSelector.bind(this)),this.componentRegistry.set("ValueConfigEditor",this.createValueConfigEditor.bind(this)),console.log("DynamicUIRenderer: Default components registered")}renderParameter(e,t=null,i=null){if(!e)return console.error("DynamicUIRenderer: Parameter metadata is required"),this.createErrorElement("参数元数据缺失");const r=this.determineComponentType(e),s=this.componentRegistry.get(r);if(!s)return console.error(`DynamicUIRenderer: Unknown component type "${r}"`),this.createErrorElement(`未知组件类型: ${r}`);try{return s(e,t,i)}catch(n){return console.error("DynamicUIRenderer: Error rendering parameter:",n),this.createErrorElement(`渲染错误: ${n.message}`)}}determineComponentType(e){var r,s,n;if(e.controlType)return this.mapControlTypeToComponent(e.controlType);const t=((r=e.typeName)==null?void 0:r.toLowerCase())||"",i=((s=e.name)==null?void 0:s.toLowerCase())||((n=e.propertyName)==null?void 0:n.toLowerCase())||"";return t==="boolean"||t==="bool"?"Checkbox":t==="int32"||t==="int"||t==="double"||t==="float"||t==="decimal"||t==="single"?"NumberStepper":t==="valueconfig"||i.includes("valueconfig")?"ValueConfigEditor":i.includes("amount")||i.includes("count")||i.includes("value")?"NumberStepper":i.includes("card")||i.includes("carddef")?"CardSelector":i.includes("target")?"TargetSelector":i.includes("expression")||i.includes("formula")?"ValueConfigEditor":e.isEnum||e.options&&e.options.length>0?"Dropdown":t==="string"&&(i.includes("description")||i.includes("text")||i.includes("content"))?"TextArea":"TextInput"}mapControlTypeToComponent(e){return{TextInput:"TextInput",NumberStepper:"NumberStepper",Checkbox:"Checkbox",Dropdown:"Dropdown",TextArea:"TextArea",CardSelector:"CardSelector",TargetSelector:"TargetSelector",DslEditor:"ValueConfigEditor",ValueConfigEditor:"ValueConfigEditor"}[e]||"TextInput"}createTextInput(e,t,i){const r=document.createElement("div");r.className="form-group";const s=document.createElement("label");s.textContent=e.displayName||e.name,s.className="form-label";const n=document.createElement("input");return n.type="text",n.className="form-input",n.value=t||"",n.placeholder=e.description||"",i&&n.addEventListener("input",o=>i(o.target.value)),r.appendChild(s),r.appendChild(n),r}createNumberStepper(e,t,i){const r=document.createElement("div");r.className="form-group";const s=document.createElement("label");s.textContent=e.displayName||e.name,s.className="form-label";const n=document.createElement("div");n.className="number-stepper";const o=document.createElement("input");o.type="number",o.className="form-input number-input",o.value=t||0,o.min=e.min||0,o.max=e.max||999;const l=document.createElement("button");l.type="button",l.className="stepper-btn decrease",l.textContent="-";const c=document.createElement("button");c.type="button",c.className="stepper-btn increase",c.textContent="+";const p=()=>{i&&i(parseInt(o.value)||0)};return o.addEventListener("input",p),l.addEventListener("click",()=>{o.value=Math.max((parseInt(o.value)||0)-1,parseInt(o.min)||0),p()}),c.addEventListener("click",()=>{o.value=Math.min((parseInt(o.value)||0)+1,parseInt(o.max)||999),p()}),n.appendChild(l),n.appendChild(o),n.appendChild(c),r.appendChild(s),r.appendChild(n),r}createCheckbox(e,t,i){const r=document.createElement("div");r.className="form-group checkbox-group";const s=document.createElement("label");s.className="checkbox-label";const n=document.createElement("input");n.type="checkbox",n.className="form-checkbox",n.checked=!!t;const o=document.createElement("span");return o.textContent=e.displayName||e.name,i&&n.addEventListener("change",l=>i(l.target.checked)),s.appendChild(n),s.appendChild(o),r.appendChild(s),r}createDropdown(e,t,i){const r=document.createElement("div");r.className="form-group";const s=document.createElement("label");s.textContent=e.displayName||e.name,s.className="form-label";const n=document.createElement("select");n.className="form-select",n.value=t||"";const o=document.createElement("option");return o.value="",o.textContent="请选择...",n.appendChild(o),e.options&&Array.isArray(e.options)&&e.options.forEach(l=>{const c=document.createElement("option");c.value=l.value||l,c.textContent=l.label||l,t===c.value&&(c.selected=!0),n.appendChild(c)}),i&&n.addEventListener("change",l=>i(l.target.value)),r.appendChild(s),r.appendChild(n),r}createTextArea(e,t,i){const r=document.createElement("div");r.className="form-group";const s=document.createElement("label");s.textContent=e.displayName||e.name,s.className="form-label";const n=document.createElement("textarea");return n.className="form-textarea",n.value=t||"",n.placeholder=e.description||"",n.rows=4,i&&n.addEventListener("input",o=>i(o.target.value)),r.appendChild(s),r.appendChild(n),r}createCardSelector(e,t,i){const r=document.createElement("div");r.className="form-group";const s=document.createElement("label");s.textContent=e.displayName||e.name,s.className="form-label";const n=document.createElement("div");n.className="card-selector-container";const o=document.createElement("input");o.type="text",o.className="card-search-input",o.placeholder="搜索卡牌...";const l=document.createElement("div");l.className="selected-card-display",l.innerHTML=t?`<span class="selected-card-name">${t}</span><button class="clear-selection-btn">×</button>`:'<span class="no-card-selected">未选择卡牌</span>';const c=document.createElement("div");return c.className="card-selector-list",c.style.display="none",o.addEventListener("input",p=>{this.searchCards(p.target.value,c)}),o.addEventListener("focus",()=>{c.style.display="block",this.loadCardOptions(c,i,l)}),document.addEventListener("click",p=>{n.contains(p.target)||(c.style.display="none")}),n.appendChild(o),n.appendChild(l),n.appendChild(c),r.appendChild(s),r.appendChild(n),r}createTargetSelector(e,t,i){const r=document.createElement("div");r.className="form-group";const s=document.createElement("label");s.textContent=e.displayName||e.name,s.className="form-label";const n=document.createElement("div");return n.className="target-selector-placeholder",n.textContent="目标选择器功能正在开发中...",r.appendChild(s),r.appendChild(n),r}createValueConfigEditor(e,t,i){const r=document.createElement("div");r.className="form-group";const s=document.createElement("label");s.textContent=e.displayName||e.name,s.className="form-label";const n=document.createElement("div");return n.className="dsl-editor-container",r.appendChild(s),r.appendChild(n),this.initializeDslEditor(n,t,i),r}async initializeDslEditor(e,t,i){try{const r=new k,s=n=>{e&&e.dataset&&(e.dataset.dslExpression=n||""),typeof i=="function"&&i(n)};await r.render(e,t||"",s),e._dslEditor=r,this.dslEditors||(this.dslEditors=[]),this.dslEditors.push(r)}catch(r){console.error("DynamicUIRenderer: Failed to initialize DSL editor:",r),e.innerHTML=`
                <div class="error-message">
                    DSL编辑器初始化失败: ${r.message}
                </div>
            `}}createErrorElement(e){const t=document.createElement("div");t.className="form-group error-group";const i=document.createElement("div");return i.className="error-message",i.textContent=e,t.appendChild(i),t}registerComponent(e,t){this.componentRegistry.set(e,t),console.log(`DynamicUIRenderer: Registered custom component "${e}"`)}renderEffectParametersForm(e,t={},i=null){if(!this.metadataManager)return console.error("DynamicUIRenderer: MetadataManager not initialized"),this.createErrorElement("元数据管理器未初始化");const r=this.metadataManager.getEffectByType(e);if(!r)return console.error(`DynamicUIRenderer: No metadata found for effect type "${e}"`),this.createErrorElement(`未找到效果类型 "${e}" 的元数据`);const s=document.createElement("div");s.className="effect-parameters-form";let n={...t};if(r.parameters&&r.parameters.length>0)r.parameters.forEach(o=>{const l=n[o.propertyName],c=this.renderParameter(o,l,p=>{n={...n,[o.propertyName]:p},s.dataset.effectParams=JSON.stringify(n),i&&i(n)});c.dataset.paramName=o.propertyName,s.appendChild(c)}),s.dataset.effectParams=JSON.stringify(n);else{const o=document.createElement("div");o.className="no-parameters-message",o.textContent="此效果无需配置参数",s.appendChild(o)}return s}renderDslExpressionEditor(e="",t=null){const i=document.createElement("div");i.className="dsl-expression-editor";const r=document.createElement("label");r.textContent="DSL表达式",r.className="form-label";const s=document.createElement("div");s.className="dsl-editor-container";const n=document.createElement("textarea");n.className="dsl-textarea",n.value=e,n.placeholder="输入DSL表达式，例如：player.health + 5",n.rows=3;const o=document.createElement("div");return o.className="dsl-help-text",o.innerHTML=`
            <small>
                可用的上下文对象：player, opponent, self, target<br>
                可用的函数：count(), sum(), filter() 等
            </small>
        `,t&&n.addEventListener("input",l=>t(l.target.value)),s.appendChild(n),s.appendChild(o),i.appendChild(r),i.appendChild(s),i}async loadCardOptions(e,t,i){try{e.innerHTML='<div class="loading-spinner"></div><p>正在加载卡牌...</p>';const r=await fetch("/api/cards");if(!r.ok)throw new Error(`HTTP ${r.status}: ${r.statusText}`);const s=await r.json();this.renderCardOptions(s,e,t,i)}catch(r){console.error("DynamicUIRenderer: Failed to load cards:",r),e.innerHTML=`
                <div class="error-message">
                    加载卡牌失败: ${r.message}
                </div>
            `}}renderCardOptions(e,t,i,r){if(!Array.isArray(e)||e.length===0){t.innerHTML='<div class="no-cards-message">暂无可用卡牌</div>';return}const s=e.map(o=>`
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
        `).join("");t.innerHTML=s,t.querySelectorAll(".card-option").forEach(o=>{o.addEventListener("click",()=>{const l=o.dataset.cardId,c=o.dataset.cardName;r.innerHTML=`
                    <span class="selected-card-name">${c}</span>
                    <button class="clear-selection-btn">×</button>
                `;const p=r.querySelector(".clear-selection-btn");p&&p.addEventListener("click",d=>{d.stopPropagation(),r.innerHTML='<span class="no-card-selected">未选择卡牌</span>',i&&i(null)}),t.style.display="none",i&&i(l)})})}searchCards(e,t){const i=t.querySelectorAll(".card-option"),r=e.toLowerCase();i.forEach(s=>{const o=s.dataset.cardName.toLowerCase().includes(r);s.style.display=o?"block":"none"})}cleanup(){this.dslEditors&&(this.dslEditors.forEach(e=>{try{e.clear&&e.clear()}catch(t){console.error("DynamicUIRenderer: Error cleaning up DSL editor:",t)}}),this.dslEditors=[]),this.metadataManager=null,this.componentRegistry.clear(),this.isInitialized=!1,console.log("DynamicUIRenderer: Cleaned up")}}class pe extends L{constructor(){super(),this.config=null,this.onChange=null,this.container=null}async render(e,t={},i=()=>{}){await this.initialize(),this.container=e,this.config=t,this.onChange=i,this.clearContainer(e);try{const r=document.createElement("div");r.className="step-content-wrapper trigger-step";const s=document.createElement("div");s.className="step-header",s.innerHTML=`
                <h4>触发时机配置</h4>
                <p class="step-intro">定义能力在何时被激活。准确的触发时机是卡牌逻辑的关键。</p>
            `,r.appendChild(s);const n=document.createElement("div");n.className="form-grid-layout";const o=await this.createEventTypesSelector();n.appendChild(o);const l=await this.createTimingSelector();n.appendChild(l);const c=await this.createRelationTypeSelector();n.appendChild(c);const p=await this.createValidZonesSelector();n.appendChild(p),r.appendChild(n),e.appendChild(r)}catch(r){console.error("TriggerConfigEditor: Render failed:",r),this.showError(`渲染触发器配置失败: ${r.message}`,e)}}async createEventTypesSelector(){const e=this.metadata.events||[],t=new Set(this.config.eventTypes||[]),i=document.createElement("div");i.className="event-selector-container",i.style.position="relative";const r=document.createElement("div");r.className="multi-select-grid custom-scrollbar";const s=()=>{let d=document.getElementById("global-event-tooltip");return d||(d=document.createElement("div"),d.id="global-event-tooltip",d.className="event-info-tooltip",d.style.position="fixed",d.style.zIndex="9999",d.style.display="none",d.style.pointerEvents="none",document.body.appendChild(d)),d},n=d=>{if(typeof d=="string")return d;if(d&&typeof d=="object"){const u=d.propertyName||d.PropertyName||d.name,m=d.displayName||d.DisplayName||u,h=d.typeName||d.TypeName||d.type,y=h?this.getDisplayTypeName(h):null,g=d.description,b=d.scope||d.Scope,T=String(b||"Input").toLowerCase()==="output"?"输出":"输入";let v=`<span class="param-name">${m||"Unknown"}</span>`;return y&&(v+=` <span class="param-type">(${y})</span>`),T&&(v+=` <span class="param-scope">[${T}]</span>`),g&&(v+=`: <span class="param-desc">${g}</span>`),v}return String(d)},o=(d,u)=>{const m=s();let h="";const y=d.contextVariables||d.parameters;y&&y.length>0&&(h=`
                    <div class="event-params">
                        <span class="params-label">可用参数:</span>
                        <ul class="params-list-vertical">
                            ${y.map(g=>`<li>${n(g)}</li>`).join("")}
                        </ul>
                    </div>
                `),m.innerHTML=`
                <div class="event-info-header">
                    <span class="event-name">${d.displayName}</span>
                </div>
                <div class="event-body">
                    <p class="event-description">${d.description||"暂无描述"}</p>
                    ${h}
                </div>
            `,m.style.display="block",l(u)},l=d=>{const u=s(),m=320,h=u.offsetHeight||200;let y=d.clientX+15,g=d.clientY+15;const b=window.innerWidth,T=window.innerHeight;y+m>b&&(y=d.clientX-m-15),g+h>T&&(g=d.clientY-h-15),u.style.left=`${y}px`,u.style.top=`${g}px`},c=()=>{const d=s();d&&(d.style.display="none")};e.forEach(d=>{const u=document.createElement("label");u.className="multi-select-item";const m=document.createElement("input");m.type="checkbox",m.value=d.eventType,m.checked=t.has(d.eventType);const h=document.createElement("span");h.textContent=d.displayName,u.appendChild(m),u.appendChild(h),m.addEventListener("change",y=>{y.target.checked?(t.add(d.eventType),u.classList.add("selected")):(t.delete(d.eventType),u.classList.remove("selected")),this.updateConfig({eventTypes:Array.from(t)})}),t.has(d.eventType)&&u.classList.add("selected"),u.addEventListener("mouseenter",y=>o(d,y)),u.addEventListener("mousemove",y=>l(y)),u.addEventListener("mouseleave",()=>c()),r.appendChild(u)}),i.appendChild(r);const p=this.createFormGroup("监听事件",i,"选择触发此能力的游戏事件（可多选）");return p.className+=" full-width",p}async createTimingSelector(){const e=[{value:"Before",label:"事件发生前 (Before)"},{value:"On",label:"事件发生时 (On)"},{value:"After",label:"事件发生后 (After)"}],t=this.createDropdown(e,this.config.timing||"On",i=>this.updateConfig({timing:i}),"选择时序...");return t.className="form-select",this.createFormGroup("触发时序",t,"决定能力结算的优先级顺序")}async createRelationTypeSelector(){const t=(this.metadata.relationTypes||[]).map(r=>({value:r.value,label:r.displayName}));t.length===0&&t.push({value:"Any",label:"加载中..."});const i=this.createDropdown(t,this.config.relationType,r=>this.updateConfig({relationType:r}),"选择关联方...");return i.className="form-select",this.createFormGroup("事件关联方",i,"谁触发了事件？（例如：仅限己方随从）")}async createValidZonesSelector(){const e=[{value:"Hand",label:"手牌 (Hand)"},{value:"Field",label:"战场 (Field)"},{value:"Deck",label:"牌库 (Deck)"},{value:"Graveyard",label:"墓地 (Graveyard)"},{value:"Banished",label:"放逐区 (Banished)"}],t=this.config.validZones||[],i=this.createMultiSelect(e,t,s=>this.updateConfig({validZones:s}));i.className="form-select multi-select";const r=this.createFormGroup("有效区域",i,"能力仅在卡牌位于这些区域时生效（留空则无限制）");return r.className+=" full-width",r}updateConfig(e){this.config={...this.config,...e},this.onChange&&this.onChange(this.config)}validateConfig(){const e=[];return(!this.config.eventTypes||this.config.eventTypes.length===0)&&e.push("触发器: 必须选择至少一个监听事件"),this.config.relationType||e.push("触发器: 必须选择事件关联方"),e}}class ue extends L{constructor(){super(),this.conditions=[],this.onChange=null,this.container=null,this.dslEditors=new Map,this.options={}}async render(e,t=[],i=()=>{},r={}){await this.initialize(),this.container=e,this.conditions=t,this.onChange=i,this.options=r||{},this.clearContainer(e);try{const s=document.createElement("div");s.className="step-content-wrapper conditions-step";const n=document.createElement("div");n.className="step-header",n.innerHTML=`
                <div class="header-with-help">
                    <h4>发动条件配置</h4>
                    <div class="help-icon-wrapper">
                        <button class="btn-icon help-btn" id="condition-help-btn">?</button>
                        <div class="info-tooltip" id="condition-help-tooltip">
                            <h6 class="tooltip-title">执行逻辑</h6>
                            <div class="tooltip-section">
                                <div class="tooltip-item">
                                    <span class="label">无条件</span>
                                    <span class="desc">能力将在触发时直接执行</span>
                                </div>
                                <div class="tooltip-item">
                                    <span class="label">单个条件</span>
                                    <span class="desc">条件为 true 时执行能力</span>
                                </div>
                                <div class="tooltip-item">
                                    <span class="label">多个条件</span>
                                    <span class="desc">所有启用的条件都为 true 时才执行 (AND)</span>
                                </div>
                            </div>
                            <h6 class="tooltip-title">常用示例</h6>
                            <ul class="tooltip-list">
                                <li><code>player.pp >= 5</code> <span class="desc">PP大于等于5</span></li>
                                <li><code>Count(player.hand) > 3</code> <span class="desc">手牌数大于3</span></li>
                                <li><code>target.is_follower</code> <span class="desc">目标是随从</span></li>
                            </ul>
                        </div>
                    </div>
                </div>
                <p class="step-intro">设置能力发动的限制条件。只有满足所有启用条件时，能力才会执行。</p>
            `,s.appendChild(n),this.bindHelpTooltip(n);const o=document.createElement("div");o.className="conditions-list",o.id="conditions-list-container",s.appendChild(o),this.renderConditionsList(o);const l=document.createElement("div");l.className="add-condition-container";const c=this.createButton("添加新条件",()=>this.addCondition(),"primary");l.appendChild(c),s.appendChild(l),e.appendChild(s)}catch(s){console.error("ConditionConfigEditor: Render failed:",s),this.showError(`渲染条件配置失败: ${s.message}`,e)}}bindHelpTooltip(e){const t=e.querySelector("#condition-help-btn"),i=e.querySelector("#condition-help-tooltip");!t||!i||(t.addEventListener("mouseenter",()=>{i.classList.add("visible")}),t.addEventListener("mouseleave",()=>{i.classList.remove("visible")}),i.addEventListener("mouseenter",()=>{i.classList.add("visible")}),i.addEventListener("mouseleave",()=>{i.classList.remove("visible")}))}renderConditionsList(e){if(this.clearContainer(e),this.conditions.length===0){const t=document.createElement("div");t.className="empty-state",t.innerHTML=`
                <div class="empty-state-visual">
                    <div class="target-ring-outer condition-ring">
                        <div class="target-ring-inner">
                            <div class="target-dot condition-dot"></div>
                        </div>
                    </div>
                </div>
                <h5 class="empty-title">暂无条件配置</h5>
                <p class="empty-desc">能力将在触发时无条件执行<br>点击下方按钮添加限制条件</p>
            `,e.appendChild(t)}else this.conditions.forEach((t,i)=>{const r=this.createConditionNode(t,i);e.appendChild(r)})}createConditionNode(e,t){const i=document.createElement("div");i.className="condition-node accordion-item",i.dataset.conditionIndex=t;const r=document.createElement("div");r.className="accordion-header";const s=e.isEnabled!==!1?"status-enabled":"status-disabled",n=e.isEnabled!==!1?"启用":"禁用";r.innerHTML=`
            <div class="header-info">
                <span class="item-index">#${t+1}</span>
                <span class="item-title">条件配置</span>
                <span class="item-status ${s}">${n}</span>
            </div>
            <div class="header-actions">
                <button class="btn-icon delete-btn" title="删除">×</button>
                <button class="btn-icon expand-btn" title="展开/折叠">▼</button>
            </div>
        `;const o=r.querySelector(".expand-btn");r.querySelector(".delete-btn").addEventListener("click",h=>{h.stopPropagation(),this.removeCondition(t)});const c=document.createElement("div");c.className="accordion-content collapsed";const p=document.createElement("div");p.className="config-section-header";const d=this.createToggleSwitch(e.isEnabled!==!1,h=>{this.updateCondition(t,{isEnabled:h});const y=r.querySelector(".item-status");h?(y.textContent="启用",y.className="item-status status-enabled"):(y.textContent="禁用",y.className="item-status status-disabled")},"启用此条件");p.appendChild(d),c.appendChild(p);const u=this.createConditionExpressionEditor(e,t);c.appendChild(u);const m=this.createConditionDescription(e,t);return c.appendChild(m),i.appendChild(r),i.appendChild(c),r.addEventListener("click",()=>{c.classList.contains("collapsed")?(c.classList.remove("collapsed"),o.innerHTML="▲",i.classList.add("expanded")):(c.classList.add("collapsed"),o.innerHTML="▼",i.classList.remove("expanded"))}),i}createConditionExpressionEditor(e,t){var o,l,c,p;const i=document.createElement("div");i.className="config-section";const r=new k,s=document.createElement("div");s.className="dsl-editor-wrapper";const n=`condition_${t}`;return this.dslEditors.set(n,r),r.render(s,e.expression||"",d=>{this.updateCondition(t,{expression:d})},{expectedType:"Boolean",triggeringEventContext:{eventTypes:((l=(o=this.options)==null?void 0:o.trigger)==null?void 0:l.eventTypes)||[],timing:((p=(c=this.options)==null?void 0:c.trigger)==null?void 0:p.timing)||"On"}}).catch(d=>{console.error("ConditionConfigEditor: Failed to render DSL editor:",d),s.innerHTML=`<div class="error">DSL编辑器加载失败: ${d.message}</div>`}),i.appendChild(this.createFormGroup("条件表达式",s,"构建一个返回 true 或 false 的逻辑表达式")),i}createConditionDescription(e,t){const i=document.createElement("div");i.className="config-section";const r=this.createTextInput(e.description||"",s=>this.updateCondition(t,{description:s}),{placeholder:"为这个条件添加描述（可选）",multiline:!1});return r.className="form-input",i.appendChild(this.createFormGroup("条件描述",r,"为条件添加易于理解的描述文本")),i}createToggleSwitch(e,t,i){const r=document.createElement("label");r.className="toggle-switch-wrapper";const s=document.createElement("input");s.type="checkbox",s.checked=e,s.addEventListener("change",l=>t(l.target.checked));const n=document.createElement("span");n.className="toggle-slider";const o=document.createElement("span");return o.className="toggle-label",o.textContent=i,r.appendChild(s),r.appendChild(n),r.appendChild(o),r}addCondition(){const e={id:this.generateConditionId(),expression:"",description:"",isEnabled:!0};this.conditions.push(e),this.updateConditionsList(),this.notifyChange()}removeCondition(e){if(confirm("确定要删除这个条件吗？")){const t=`condition_${e}`;this.dslEditors.delete(t),this.conditions.splice(e,1),this.updateConditionsList(),this.notifyChange()}}updateCondition(e,t){e>=0&&e<this.conditions.length&&(this.conditions[e]={...this.conditions[e],...t},this.notifyChange())}updateConditionsList(){const e=this.container.querySelector("#conditions-list-container");e&&this.renderConditionsList(e)}generateConditionId(){return`condition_${Date.now()}_${Math.random().toString(36).substr(2,9)}`}notifyChange(){this.onChange&&this.onChange(this.conditions)}validateConditions(){const e=[];return this.conditions.forEach((t,i)=>{t.isEnabled!==!1&&((!t.expression||t.expression.trim()==="")&&e.push(`条件 ${i+1}: 表达式不能为空`),t.expression&&!this.isValidExpression(t.expression)&&e.push(`条件 ${i+1}: 表达式语法可能有误`))}),e}isValidExpression(e){return!e||e.trim()===""?!1:e.length>0}cleanup(){this.dslEditors.clear()}}class he extends L{constructor(){super(),this.targets=[],this.onChange=null,this.container=null,this.dslEditors=new Map,this.options={}}async render(e,t=[],i=()=>{},r={}){await this.initialize(),this.container=e,this.targets=t,this.onChange=i,this.options=r||{},this.clearContainer(e);try{const s=document.createElement("div");s.className="step-content-wrapper targets-step";const n=document.createElement("div");n.className="step-header",n.innerHTML=`
                <h4>目标选择配置</h4>
                <p class="step-intro">定义能力将影响哪些对象。你可以配置多个目标，并为每个目标指定筛选条件。</p>
            `,s.appendChild(n);const o=document.createElement("div");o.className="targets-list",o.id="targets-list-container",s.appendChild(o),this.renderTargetsList(o);const l=document.createElement("div");l.className="add-target-container";const c=this.createButton("添加新目标",()=>this.addTarget(),"primary");l.appendChild(c),s.appendChild(l),e.appendChild(s)}catch(s){console.error("TargetConfigEditor: Render failed:",s),this.showError(`渲染目标配置失败: ${s.message}`,e)}}renderTargetsList(e){if(this.clearContainer(e),this.targets.length===0){const t=document.createElement("div");t.className="empty-state",t.innerHTML=`
                <div class="empty-state-visual">
                    <div class="target-ring-outer">
                        <div class="target-ring-inner">
                            <div class="target-dot"></div>
                        </div>
                    </div>
                </div>
                <h5 class="empty-title">暂无目标配置</h5>
                <p class="empty-desc">定义能力将影响哪些对象<br>点击下方按钮添加第一个目标</p>
            `,e.appendChild(t);return}this.targets.forEach((t,i)=>{const r=this.createTargetNode(t,i);e.appendChild(r)})}createTargetNode(e,t){const i=document.createElement("div");i.className="target-node accordion-item",i.dataset.targetIndex=t;const r=document.createElement("div");r.className="accordion-header",r.innerHTML=`
            <div class="header-info">
                <span class="item-index">#${t+1}</span>
                <span class="item-title">目标配置</span>
            </div>
            <div class="header-actions">
                <button class="btn-icon delete-btn" title="删除">×</button>
                <button class="btn-icon expand-btn" title="展开/折叠">▼</button>
            </div>
        `;const s=r.querySelector(".expand-btn");r.querySelector(".delete-btn").addEventListener("click",d=>{d.stopPropagation(),this.removeTarget(t)});const o=document.createElement("div");o.className="accordion-content collapsed";const l=this.createQueryEditor(e,t);o.appendChild(l);const c=this.createValidationEditor(e,t);o.appendChild(c);const p=this.createPlayerChoiceEditor(e,t);return o.appendChild(p),i.appendChild(r),i.appendChild(o),r.addEventListener("click",()=>{o.classList.contains("collapsed")?(o.classList.remove("collapsed"),s.innerHTML="▲",i.classList.add("expanded")):(o.classList.add("collapsed"),s.innerHTML="▼",i.classList.remove("expanded"))}),i}createQueryEditor(e,t){const i=document.createElement("div");i.className="config-section",i.innerHTML='<h5 class="section-title">查询逻辑</h5>';const r=this.createSourcePoolEditor(e,t);i.appendChild(r);const s=this.createOperationsEditor(e,t);return i.appendChild(s),i}createSourcePoolEditor(e,t){var n,o,l,c,p;const i=document.createElement("div");i.className="dsl-editor-wrapper";const r=new k,s=`sourcePool_${t}`;return this.dslEditors.set(s,r),r.render(i,((n=e.query)==null?void 0:n.sourcePoolExpression)||"",d=>{this.updateTargetQuery(t,{sourcePoolExpression:d})},{expectedType:"List<CardInstance>",triggeringEventContext:{eventTypes:((l=(o=this.options)==null?void 0:o.trigger)==null?void 0:l.eventTypes)||[],timing:((p=(c=this.options)==null?void 0:c.trigger)==null?void 0:p.timing)||"On"}}),this.createFormGroup("起始卡池",i,"选择查询的起点（如：所有敌方随从）")}createOperationsEditor(e,t){var o;const i=document.createElement("div");i.className="operations-container";const r=document.createElement("div");r.className="operations-list";const s=((o=e.query)==null?void 0:o.operations)||[];s.length===0?r.innerHTML='<div class="text-muted">无额外操作</div>':s.forEach((l,c)=>{const p=this.createOperationNode(l,t,c);r.appendChild(p)}),i.appendChild(r);const n=this.createButton("+ 添加操作",()=>this.showAddOperationMenu(t),"secondary small");return i.appendChild(n),this.createFormGroup("处理流程",i,"对卡池进行筛选、排序或随机抽取")}createOperationNode(e,t,i){const r=document.createElement("div");r.className="operation-item";const s=this.getOperationMetadata(e.$type),n=s?s.displayName:e.$type;r.innerHTML=`
            <div class="op-header">
                <span class="op-type">${n}</span>
                <button class="btn-icon-small delete-op">×</button>
            </div>
            <div class="op-body"></div>
        `,r.querySelector(".delete-op").addEventListener("click",()=>{this.removeOperation(t,i)});const o=r.querySelector(".op-body");return this.renderOperationParams(o,e,t,i),r}renderOperationParams(e,t,i,r){switch(t.$type){case"FilterOperation":(t.filters||[]).forEach((o,l)=>{var d,u,m,h,y;const c=document.createElement("div");c.className="dsl-mini-wrapper",new k().render(c,((d=o.valueSource)==null?void 0:d.formula)||"",g=>{this.updateOperationFilter(i,r,l,g)},{expectedType:"Boolean",triggeringEventContext:{eventTypes:((m=(u=this.options)==null?void 0:u.trigger)==null?void 0:m.eventTypes)||[],timing:((y=(h=this.options)==null?void 0:h.trigger)==null?void 0:y.timing)||"On"}}),e.appendChild(c)});const s=this.createButton("添加条件",()=>this.addFilter(i,r),"secondary tiny");e.appendChild(s);break;case"TakeOperation":case"RandomOperation":const n=this.createNumberInput(t.count||1,o=>{this.updateOperation(i,r,{count:o})},{min:1});n.className="form-input small",e.appendChild(this.createFormGroup("数量",n));break}}createValidationEditor(e,t){const i=document.createElement("div");i.className="config-section",i.innerHTML='<h5 class="section-title">验证规则</h5>';const r=document.createElement("div");r.className="form-grid-layout";const s=[{value:"AbortAbility",label:"中止能力"},{value:"SkipEffect",label:"跳过效果"},{value:"LogWarning",label:"仅记录警告"}],n=this.createDropdown(s,e.onFailure||"AbortAbility",l=>this.updateTarget(t,{onFailure:l}));n.className="form-select",r.appendChild(this.createFormGroup("若无目标",n));const o=this.createToggleSwitch(e.requireValidTarget||!1,l=>this.updateTarget(t,{requireValidTarget:l}),"必须存在有效目标才能发动");return r.appendChild(this.createFormGroup("发动限制",o)),i.appendChild(r),i}createPlayerChoiceEditor(e,t){const i=document.createElement("div");i.className="config-section";const r=document.createElement("div");r.className="section-header-with-toggle";const s=e.playerChoiceOptions!=null,n=this.createToggleSwitch(s,o=>{o?this.updateTarget(t,{playerChoiceOptions:{isRequired:!0,minCount:1,maxCount:1}}):this.updateTarget(t,{playerChoiceOptions:null}),this.updateTargetNode(t)},"启用玩家手动选择");if(r.appendChild(n),i.appendChild(r),s){const o=e.playerChoiceOptions,l=document.createElement("div");l.className="form-grid-layout";const c=this.createTextInput(o.prompt||"",m=>{this.updatePlayerChoiceOption(t,{prompt:m})},{placeholder:"例如：选择一个敌方随从"});c.className="form-input",l.appendChild(this.createFormGroup("提示文本",c));const p=document.createElement("div");p.className="input-group-row";const d=this.createNumberInput(o.minCount||1,m=>{this.updatePlayerChoiceOption(t,{minCount:m})},{min:0});d.className="form-input small";const u=this.createNumberInput(o.maxCount||1,m=>{this.updatePlayerChoiceOption(t,{maxCount:m})},{min:1});u.className="form-input small",p.appendChild(d),p.appendChild(document.createTextNode(" 至 ")),p.appendChild(u),l.appendChild(this.createFormGroup("选择数量",p)),i.appendChild(l)}return i}createToggleSwitch(e,t,i){const r=document.createElement("label");r.className="toggle-switch-wrapper";const s=document.createElement("input");s.type="checkbox",s.checked=e,s.addEventListener("change",l=>t(l.target.checked));const n=document.createElement("span");n.className="toggle-slider";const o=document.createElement("span");return o.className="toggle-label",o.textContent=i,r.appendChild(s),r.appendChild(n),r.appendChild(o),r}updateTargetNode(e){const t=this.container.querySelector("#targets-list-container");if(!t)return;const i=t.querySelector(`[data-target-index="${e}"]`);if(!i)return;const r=i.classList.contains("expanded"),s=this.createTargetNode(this.targets[e],e);r&&(s.classList.add("expanded"),s.querySelector(".accordion-content").classList.remove("collapsed"),s.querySelector(".expand-btn").innerHTML="▲"),i.replaceWith(s)}addTarget(){this.targets.push({query:{sourcePoolExpression:"",operations:[]},onFailure:"AbortAbility"}),this.renderTargetsList(this.container.querySelector("#targets-list-container")),this.notifyChange()}removeTarget(e){confirm("确定删除此目标？")&&(this.targets.splice(e,1),this.renderTargetsList(this.container.querySelector("#targets-list-container")),this.notifyChange())}updateTarget(e,t){this.targets[e]={...this.targets[e],...t},this.notifyChange()}updateTargetQuery(e,t){this.targets[e].query={...this.targets[e].query,...t},this.notifyChange()}updatePlayerChoiceOption(e,t){this.targets[e].playerChoiceOptions={...this.targets[e].playerChoiceOptions,...t},this.notifyChange()}showAddOperationMenu(e){const t=prompt("输入操作类型 (FilterOperation, TakeOperation, RandomOperation, SortOperation):","FilterOperation");t&&this.addOperation(e,t)}addOperation(e,t){const i={$type:t};t==="FilterOperation"&&(i.filters=[]),(t==="TakeOperation"||t==="RandomOperation")&&(i.count=1),this.targets[e].query.operations.push(i),this.updateTargetNode(e),this.notifyChange()}removeOperation(e,t){this.targets[e].query.operations.splice(t,1),this.updateTargetNode(e),this.notifyChange()}addFilter(e,t){this.targets[e].query.operations[t].filters.push({valueSource:{formula:""}}),this.updateTargetNode(e),this.notifyChange()}updateOperationFilter(e,t,i,r){this.targets[e].query.operations[t].filters[i].valueSource.formula=r,this.notifyChange()}updateOperation(e,t,i){const r=this.targets[e].query.operations[t];this.targets[e].query.operations[t]={...r,...i},this.notifyChange()}getOperationMetadata(e){return(this.metadata.queryOperations||[]).find(t=>t.typeName===e)}notifyChange(){this.onChange&&this.onChange(this.targets)}}class me extends L{constructor(){super(),this.valueConfig=null,this.onChange=null,this.container=null}normalizeExpectedType(e){if(!e)return null;const t=String(e).trim();if(!t)return null;const i=t.match(/^Nullable<(.+)>$/);return i?i[1]:t}getStaticInputKindForType(e){const t=this.normalizeExpectedType(e);return t?["Int32","Int64","Single","Double","Decimal","Number"].includes(t)?"number":t==="Boolean"?"boolean":"string":null}render(e,t=null,i=()=>{},r={}){if(this.container=e,this.valueConfig=this.normalizeValueConfig(t),this.onChange=i,this.options=r,this.expectedType=this.normalizeExpectedType(r.parameterType||r.expectedType),this.triggeringEventContext=r.triggeringEventContext||null,this.container)try{this.container.dataset.valueConfig=JSON.stringify(this.valueConfig),this.container._valueConfig=this.valueConfig}catch{}console.log("ValueConfigEditor: Rendering with valueConfig:",{original:t,normalized:this.valueConfig}),this.clearContainer(e),this.renderEditor()}createDefaultValueConfig(){return{$type:"StaticValueConfig",Value:0}}normalizeValueConfig(e){return e?e.value!==void 0&&!e.$type?{$type:"StaticValueConfig",Value:e.value}:e.$type?e:this.createDefaultValueConfig():this.createDefaultValueConfig()}renderEditor(){const e=document.createElement("div");e.className="value-config-editor";const t=this.createTypeSelector(),i=this.createFormGroup("值类型",t);e.appendChild(i);const r=this.createValueEditor();e.appendChild(r),this.container.appendChild(e)}createTypeSelector(){const e=[{value:"StaticValueConfig",label:"静态值",description:"固定的数值或文本"},{value:"FormulaValueConfig",label:"公式值",description:"基于DSL表达式计算的动态值"}],t=this.valueConfig.$type||"StaticValueConfig";return this.createDropdown(e,t,i=>{this.changeValueType(i)},"选择值类型")}createValueEditor(){const e=document.createElement("div");e.className="value-editor-container";const t=this.valueConfig.$type||"StaticValueConfig";return t==="StaticValueConfig"?e.appendChild(this.createStaticValueEditor()):t==="FormulaValueConfig"&&e.appendChild(this.createFormulaValueEditor()),e}createStaticValueEditor(){const e=document.createElement("div");e.className="static-value-editor";const t=this.valueConfig.Value,i=this.getStaticInputKindForType(this.expectedType),r=i||typeof t;if(!i){const o=[{value:"number",label:"数字"},{value:"string",label:"文本"},{value:"boolean",label:"布尔值"}],l=this.createDropdown(o,r,p=>{this.changeStaticValueType(p)},"选择数据类型"),c=this.createFormGroup("数据类型",l);e.appendChild(c)}const s=this.createStaticValueInput(t,r),n=this.createFormGroup("值",s);return e.appendChild(n),e}createStaticValueInput(e,t){return t==="number"?this.createNumberInput(e,i=>{this.updateValueConfig({Value:i})}):t==="boolean"?this.createCheckbox(e,i=>{this.updateValueConfig({Value:i})}):this.createTextInput(e,i=>{this.updateValueConfig({Value:i})})}createFormulaValueEditor(){const e=document.createElement("div");e.className="formula-value-editor";const t=this.valueConfig.Formula||"",i=document.createElement("div");i.className="dsl-editor-wrapper ability-editor";const r=new k;r.render(i,t,o=>{i.dataset.dslExpression=o||"",this.updateValueConfig({Formula:o})},{expectedType:this.expectedType||"Int32",triggeringEventContext:this.triggeringEventContext}),i._dslEditor=r;const s=this.createFormGroup("DSL公式",i);e.appendChild(s);const n=document.createElement("div");return n.className="formula-help-text",n.innerHTML=`
            <small class="help-text">
                使用可视化编辑器构建表达式，或直接在上方输入。
            </small>
        `,e.appendChild(n),e}changeValueType(e){let t;if(e==="StaticValueConfig"){const i=this.getStaticInputKindForType(this.expectedType);t={$type:"StaticValueConfig",Value:i==="boolean"?!1:i==="string"?"":0}}else e==="FormulaValueConfig"&&(t={$type:"FormulaValueConfig",Formula:""});this.valueConfig=t,this.notifyChange(),this.renderEditor()}changeStaticValueType(e){let t;e==="number"?t=0:e==="boolean"?t=!1:t="",this.updateValueConfig({Value:t}),this.renderEditor()}updateValueConfig(e){this.valueConfig={...this.valueConfig,...e},this.notifyChange()}notifyChange(){if(console.log("ValueConfigEditor: Value changed:",this.valueConfig),this.container)try{this.container.dataset.valueConfig=JSON.stringify(this.valueConfig),this.container._valueConfig=this.valueConfig}catch(e){console.warn("ValueConfigEditor: Failed to serialize valueConfig",e)}this.onChange&&this.onChange(this.valueConfig)}getCurrentDisplayValue(){return this.valueConfig?this.valueConfig.$type==="StaticValueConfig"?String(this.valueConfig.Value||""):this.valueConfig.$type==="FormulaValueConfig"&&this.valueConfig.Formula||"":""}}class fe extends L{constructor(){super(),this.effects=[],this.onChange=null,this.container=null,this.options={}}async render(e,t=[],i=()=>{},r={}){await this.initialize(),this.container=e,this.effects=t,this.onChange=i,this.options=r||{},this.clearContainer(e);try{const s=document.createElement("div");s.className="step-content-wrapper effects-step";const n=document.createElement("div");n.className="step-header",n.innerHTML=`
                <h4>效果配置</h4>
                <p class="step-intro">定义能力触发后具体发生的事情。你可以组合多个效果来实现复杂的逻辑。</p>
            `,s.appendChild(n);const o=document.createElement("div");o.className="effects-list",o.id="effects-list-container",s.appendChild(o),this.renderEffectsList(o);const l=document.createElement("div");l.className="add-effect-container";const c=this.createButton("添加新效果",()=>this.addEffect(),"primary");l.appendChild(c),s.appendChild(l),e.appendChild(s)}catch(s){console.error("EffectConfigEditor: Render failed:",s),this.showError(`渲染效果配置失败: ${s.message}`,e)}}renderEffectsList(e){if(this.clearContainer(e),this.effects.length===0){const t=document.createElement("div");t.className="empty-state",t.innerHTML=`
                <div class="empty-state-visual">
                    <div class="target-ring-outer effect-ring">
                        <div class="target-ring-inner">
                            <div class="target-dot effect-dot"></div>
                        </div>
                    </div>
                </div>
                <h5 class="empty-title">暂无效果配置</h5>
                <p class="empty-desc">定义能力触发后具体发生的事情<br>点击下方按钮添加第一个效果</p>
            `,e.appendChild(t);return}this.effects.forEach((t,i)=>{const r=this.createEffectNode(t,i);e.appendChild(r)})}createEffectNode(e,t){const i=document.createElement("div");i.className="effect-node accordion-item",i.dataset.effectIndex=t;const r=this.getEffectMetadata(e.type),s=r?r.displayName:e.type||"未配置效果",n=document.createElement("div");n.className="accordion-header",n.innerHTML=`
            <div class="header-info">
                <span class="item-index">#${t+1}</span>
                <span class="item-title">${s}</span>
            </div>
            <div class="header-actions">
                <button class="btn-icon delete-btn" title="删除">×</button>
                <button class="btn-icon expand-btn" title="展开/折叠">▼</button>
            </div>
        `;const o=n.querySelector(".expand-btn");n.querySelector(".delete-btn").addEventListener("click",u=>{u.stopPropagation(),this.removeEffect(t)});const c=document.createElement("div");c.className="accordion-content collapsed";const p=this.createEffectTypeSelector(e.type,u=>{this.updateEffect(t,{type:u,parameters:{}})}),d=this.createFormGroup("效果类型",p,"选择要执行的具体操作");if(c.appendChild(d),r&&e.type){const u=this.createParametersPanel(r,e.parameters||{},m=>{this.updateEffect(t,{parameters:m})});c.appendChild(u)}return i.appendChild(n),i.appendChild(c),n.addEventListener("click",()=>{c.classList.contains("collapsed")?(c.classList.remove("collapsed"),o.innerHTML="▲",i.classList.add("expanded")):(c.classList.add("collapsed"),o.innerHTML="▼",i.classList.remove("expanded"))}),i}createEffectTypeSelector(e,t){const r=(this.metadata.effects||[]).map(n=>({value:n.effectTypeName,label:n.displayName,description:n.description}));r.length===0&&r.push({value:"",label:"加载中..."});const s=this.createDropdown(r,e,t,"选择效果类型...");return s.className="form-select",s}createParametersPanel(e,t,i){const r=document.createElement("div");if(r.className="parameters-panel",!e.parameters||e.parameters.length===0)return r.innerHTML='<div class="text-muted">此效果无需额外参数</div>',r;r.innerHTML='<h5 class="section-title">参数配置</h5>';const s=document.createElement("div");return s.className="form-grid-layout",e.parameters.forEach(n=>{const o=n.propertyName||n.PropertyName||n.name,l=n.displayName||n.DisplayName,c=n.description||n.Description,p=(n.controlType||n.ControlType||"").toString(),d=n.typeName||n.TypeName||n.type||n.TypeName;let u=t[o];if(u===void 0){const y=o.toLowerCase();if(u=t[y],u===void 0){const g=o.charAt(0).toLowerCase()+o.slice(1);u=t[g]}}const m=document.createElement("div");m.className="parameter-control",this.renderParameterControl(m,{controlType:p,typeName:d,paramName:o,displayName:l,description:c,paramMeta:n,paramValue:u,parameters:t,onChange:i});const h=this.createFormGroup(l,m,c);["CardFilter","EffectList"].includes(n.typeName)&&(h.className+=" full-width"),s.appendChild(h)}),r.appendChild(s),r}unwrapStaticValueConfig(e){return e?typeof e!="object"?{ok:!1,value:null}:e.$type==="StaticValueConfig"&&Object.prototype.hasOwnProperty.call(e,"Value")?{ok:!0,value:e.Value}:Object.prototype.hasOwnProperty.call(e,"value")?{ok:!0,value:e.value}:{ok:!1,value:null}:{ok:!1,value:null}}toStaticValueConfig(e){return{$type:"StaticValueConfig",Value:e}}async populateEnumDropdown(e,t,i){try{const r=await fetch(`/api/localization/enums/${encodeURIComponent(t)}`);if(!r.ok)throw new Error(`HTTP ${r.status}`);const s=await r.json();e.innerHTML="",Object.entries(s).forEach(([n,o])=>{const l=document.createElement("option");l.value=n,l.textContent=o,e.appendChild(l)}),i!=null&&String(i)!==""&&(e.value=String(i))}catch(r){console.warn("EffectConfigEditor: failed to load enum options",t,r),e.disabled=!0,e.innerHTML="";const s=document.createElement("option");s.value="",s.textContent="加载失败",e.appendChild(s)}}async populateOptionsProviderDropdown(e,t,i){try{const r=await fetch(`/api/abilityEditorOptions/${encodeURIComponent(t)}`);if(!r.ok)throw new Error(`HTTP ${r.status}`);const s=await r.json();e.innerHTML="";const n=Object.entries(s||{});if(n.length===0){const o=document.createElement("option");o.value="",o.textContent="无可用选项",e.appendChild(o);return}n.forEach(([o,l])=>{const c=document.createElement("option");c.value=o,c.textContent=l,e.appendChild(c)}),i!=null&&String(i)!==""&&(e.value=String(i))}catch(r){console.warn("EffectConfigEditor: failed to load option provider",t,r),e.disabled=!0,e.innerHTML="";const s=document.createElement("option");s.value="",s.textContent="加载失败",e.appendChild(s)}}renderParameterControl(e,t){var y,g,b,T;const{controlType:i,paramName:r,typeName:s,paramMeta:n,paramValue:o,parameters:l,onChange:c}=t,p=n.enumName||n.EnumName,d=n.optionsProviderKey||n.OptionsProviderKey,u=v=>{const E={...l,[r]:v};c(E)},m=this.unwrapStaticValueConfig(o),h=m.ok?m.value:o;switch(i){case"valueConfigEditor":{const v=o&&typeof o=="object"&&o.$type?o:this.toStaticValueConfig(o??n.defaultValue??0);new me().render(e,v,$=>u($),{parameterName:r,parameterType:s,triggeringEventContext:{eventTypes:((g=(y=this.options)==null?void 0:y.trigger)==null?void 0:g.eventTypes)||[],timing:((T=(b=this.options)==null?void 0:b.trigger)==null?void 0:T.timing)||"On"}});return}case"checkbox":{const v=document.createElement("input");v.type="checkbox",v.checked=!!(h??n.defaultValue),v.addEventListener("change",E=>u(!!E.target.checked)),e.appendChild(v);return}case"numberStepper":{const v=this.createNumberInput(h??n.defaultValue??0,E=>u(E),{min:n.min,max:n.max});v.className="form-input",e.appendChild(v);return}case"textArea":{const v=this.createTextInput(h??n.defaultValue??"",E=>u(E),{multiline:!0});v.className="form-textarea",e.appendChild(v);return}case"textInput":{const v=this.createTextInput(h??n.defaultValue??"",E=>u(E),{multiline:!1});v.className="form-input",e.appendChild(v);return}case"dropdown":{const v=document.createElement("select");v.className="form-select",v.innerHTML='<option value="">加载中...</option>',v.disabled=!0;const E=h??n.defaultValue??"";p?this.populateEnumDropdown(v,p,E).finally(()=>{v.disabled=!1}):d?this.populateOptionsProviderDropdown(v,d,E).finally(()=>{v.disabled=!1}):(v.innerHTML='<option value="">缺少选项源</option>',v.disabled=!0),v.addEventListener("change",$=>u($.target.value)),e.appendChild(v);return}case"targetSelector":{const v=this.createTextInput(h??n.defaultValue??"",E=>u(E),{multiline:!1,placeholder:"TODO: TargetSelector"});v.className="form-input",e.appendChild(v);return}default:{const v=document.createElement("div");v.className="text-muted",v.textContent=`未支持的控件类型: ${i||"(空)"}，请使用 ValueConfigEditor 或调整元数据。`,e.appendChild(v);return}}}addEffect(){this.effects.push({type:"",parameters:{}}),this.renderEffectsList(this.container.querySelector("#effects-list-container")),this.notifyChange()}removeEffect(e){confirm("确定删除此效果？")&&(this.effects.splice(e,1),this.renderEffectsList(this.container.querySelector("#effects-list-container")),this.notifyChange())}updateEffect(e,t){this.effects[e]={...this.effects[e],...t},t.type&&this.updateEffectNode(e),this.notifyChange()}updateEffectNode(e){const t=this.container.querySelector("#effects-list-container");if(!t)return;const i=t.querySelector(`[data-effect-index="${e}"]`);if(!i)return;const r=i.classList.contains("expanded"),s=this.createEffectNode(this.effects[e],e);r&&(s.classList.add("expanded"),s.querySelector(".accordion-content").classList.remove("collapsed"),s.querySelector(".expand-btn").innerHTML="▲"),i.replaceWith(s)}getEffectMetadata(e){return(this.metadata.effects||[]).find(t=>t.effectTypeName===e)}notifyChange(){this.onChange&&this.onChange(this.effects)}validateEffects(){const e=[];return this.effects.forEach((t,i)=>{if(!t.type)e.push(`效果 #${i+1}: 必须选择效果类型`);else{const r=this.getEffectMetadata(t.type);r&&r.parameters&&r.parameters.forEach(s=>{!s.isOptional&&t.parameters[s.name]})}}),e}}function M(a,e,t={}){var d;const i=t.expectedType||null,r=t.allowEmpty===!0,s=t.contextOverride||null,n=(e??"").trim();if(!n)return{ast:null,issues:r?[]:[{severity:"error",message:"DSL 表达式不能为空"}]};const o=new k;o.metadata=a,o.isInitialized=!0,o.expectedType=i,o.contextOverride=s;try{o.parseExpression(n)}catch(u){return{ast:null,issues:[{severity:"error",message:`DSL 解析失败: ${(u==null?void 0:u.message)||u}`}]}}const l=o.astRoot;if(!l)return{ast:null,issues:[{severity:"error",message:"DSL 解析失败: AST 为空"}]};const c=[],p=D(l);if(!p.isValid)for(const u of p.errors||[])c.push({severity:"error",message:String(u)});if(typeof o.validateExpressionSemantics=="function"){const u=o.validateExpressionSemantics(l);for(const m of(u==null?void 0:u.issues)||[])c.push({severity:"error",message:String(m)})}if(i&&typeof o.calculateNodeTypeMatch=="function"){const u=o.calculateNodeTypeMatch(l,i);if((d=u==null?void 0:u.issues)!=null&&d.length)for(const m of u.issues)c.push({severity:"error",message:String(m)})}return{ast:l,issues:c}}class Q extends L{constructor(){super(),this.ability=null,this.onChange=null,this.container=null,this.triggerEditor=new pe,this.conditionEditor=new ue,this.targetEditor=new he,this.effectEditor=new fe,this.currentStepIndex=0,this.steps=[{id:"basic",label:"基础信息",description:"定义名称与类型"},{id:"trigger",label:"触发时机",description:"何时发动能力"},{id:"targets",label:"目标选择",description:"对谁生效"},{id:"conditions",label:"发动条件",description:"额外的限制"},{id:"effects",label:"效果配置",description:"具体产生的影响"}]}async render(e,t={},i=()=>{}){await this.initialize(),this.container=e,this.ability=this.normalizeAbility(t),this.onChange=i,this.clearContainer(e);try{this.renderEditorStructure(),await this.renderCurrentStep()}catch(r){console.error("AbilityEditor: Render failed:",r),this.showError(`渲染能力编辑器失败: ${r.message}`,e)}}normalizeAbility(e){return{id:e.id||"",name:e.name||"",description:e.description||"",abilityTag:e.abilityTag||"",abilityType:e.abilityType||1,priority:e.priority||0,isActive:e.isActive!==!1,trigger:e.trigger||{eventTypes:[],relationType:"",timing:"On",validZones:[]},conditions:e.conditions||[],targets:e.targets||[],effects:e.effects||[]}}renderEditorStructure(){const e=`
            <div class="ability-editor wizard-mode">
                <div class="wizard-header">
                    <div class="wizard-title">
                        <h3>能力编辑器</h3>
                        <span class="wizard-subtitle">创建新的卡牌能力</span>
                    </div>
                    <div class="wizard-progress">
                        ${this.steps.map((t,i)=>`
                            <div class="progress-step ${i===0?"active":""}" data-step="${i}">
                                <div class="step-indicator">${i+1}</div>
                                <div class="step-label">
                                    <span class="step-name">${t.label}</span>
                                    <span class="step-desc">${t.description}</span>
                                </div>
                            </div>
                        `).join("")}
                    </div>
                </div>

                <div class="wizard-content custom-scrollbar" id="wizard-content-area">
                    <!-- 动态内容区域 -->
                </div>

                <div class="wizard-footer">
                    <div class="footer-left">
                        <button class="btn-secondary" id="wizard-cancel">取消</button>
                    </div>
                    <div class="footer-right">
                        <button class="btn-secondary" id="wizard-back" disabled>上一步</button>
                        <button class="btn-primary" id="wizard-next">下一步</button>
                        <button class="btn-success" id="wizard-finish" style="display: none;">完成</button>
                    </div>
                </div>
            </div>
        `;this.container.innerHTML=e,this.bindNavigationEvents()}bindNavigationEvents(){const e=this.container.querySelector("#wizard-back"),t=this.container.querySelector("#wizard-next"),i=this.container.querySelector("#wizard-finish"),r=this.container.querySelector("#wizard-cancel");e.addEventListener("click",()=>this.handleBack()),t.addEventListener("click",()=>this.handleNext()),i.addEventListener("click",()=>this.handleFinish()),r.addEventListener("click",()=>this.handleCancel())}async renderCurrentStep(){const e=this.container.querySelector("#wizard-content-area"),t=this.steps[this.currentStepIndex].id;switch(e.classList.add("fade-out"),await new Promise(i=>setTimeout(i,200)),this.clearContainer(e),e.classList.remove("fade-out"),e.classList.add("fade-in"),t){case"basic":await this.renderBasicInfoStep(e);break;case"trigger":await this.triggerEditor.render(e,this.ability.trigger,i=>this.updateAbility({trigger:i}));break;case"targets":await this.targetEditor.render(e,this.ability.targets,i=>this.updateAbility({targets:i}),{trigger:this.ability.trigger});break;case"conditions":await this.conditionEditor.render(e,this.ability.conditions,i=>this.updateAbility({conditions:i}),{trigger:this.ability.trigger});break;case"effects":await this.effectEditor.render(e,this.ability.effects,i=>this.updateAbility({effects:i}),{trigger:this.ability.trigger});break}this.updateWizardUI(),setTimeout(()=>e.classList.remove("fade-in"),300)}updateWizardUI(){this.container.querySelectorAll(".progress-step").forEach((s,n)=>{s.classList.remove("active","completed"),n===this.currentStepIndex?s.classList.add("active"):n<this.currentStepIndex&&s.classList.add("completed")});const t=this.container.querySelector("#wizard-back"),i=this.container.querySelector("#wizard-next"),r=this.container.querySelector("#wizard-finish");t.disabled=this.currentStepIndex===0,this.currentStepIndex===this.steps.length-1?(i.style.display="none",r.style.display="inline-block"):(i.style.display="inline-block",r.style.display="none")}async handleNext(){const e=this.validateCurrentStep();if(e.length>0){alert(`请修正以下问题后再继续:
`+e.join(`
`));return}this.currentStepIndex<this.steps.length-1&&(this.currentStepIndex++,await this.renderCurrentStep())}async handleBack(){this.currentStepIndex>0&&(this.currentStepIndex--,await this.renderCurrentStep())}async handleFinish(){const e=this.validateAbilityBeforeSave();if(e.errors.length>0){alert(`请修正以下问题后再完成:
`+e.errors.map(t=>`- ${t.message}`).join(`
`));return}if(!(e.warns.length>0&&!confirm(`存在警告，仍要继续保存吗？
`+e.warns.map(i=>`- ${i.message}`).join(`
`))))try{const t=this.buildFinalConfig();this.onChange&&await this.onChange(t)}catch(t){console.error("AbilityEditor: Save failed:",t),alert(`保存失败: ${t.message}`)}}handleCancel(){confirm("确定要取消编辑吗？未保存的更改将丢失。")&&this.onChange&&this.onChange(null)}validateCurrentStep(){const e=this.steps[this.currentStepIndex].id,t=[];switch(e){case"basic":(!this.ability.name||this.ability.name.trim()==="")&&t.push("能力名称不能为空");break;case"trigger":t.push(...this.triggerEditor.validateConfig());break;case"targets":break;case"conditions":t.push(...this.conditionEditor.validateConditions());break;case"effects":t.push(...this.effectEditor.validateEffects());break}return t}validateAbilityBeforeSave(){const e={errors:[],warns:[],infos:[]},t=s=>e.errors.push({severity:"error",message:s}),i=s=>e.warns.push({severity:"warn",message:s});(!this.ability.name||this.ability.name.trim()==="")&&t("能力名称不能为空");for(const s of this.triggerEditor.validateConfig())t(s);return(this.ability.conditions||[]).forEach((s,n)=>{if((s==null?void 0:s.isEnabled)===!1)return;const o=(s==null?void 0:s.expression)||"",l=M(this.metadata,o,{expectedType:"Boolean",allowEmpty:!1});for(const c of l.issues)t(`条件 #${n+1}: ${c.message}`)}),(this.ability.targets||[]).forEach((s,n)=>{var c,p;const o=((c=s==null?void 0:s.query)==null?void 0:c.sourcePoolExpression)||"";if(o.trim()){const d=M(this.metadata,o,{allowEmpty:!1});for(const u of d.issues)t(`目标 #${n+1} 起始卡池: ${u.message}`)}else s!=null&&s.query&&i(`目标 #${n+1}: 未配置起始卡池表达式`);(((p=s==null?void 0:s.query)==null?void 0:p.operations)||[]).forEach((d,u)=>{(d==null?void 0:d.$type)==="FilterOperation"&&(d.filters||[]).forEach((m,h)=>{var b;const y=((b=m==null?void 0:m.valueSource)==null?void 0:b.formula)||"",g=M(this.metadata,y,{expectedType:"Boolean",allowEmpty:!1});for(const T of g.issues)t(`目标 #${n+1} 筛选 #${u+1}.${h+1}: ${T.message}`)})})}),(this.ability.effects||[]).forEach((s,n)=>{var p;if(!(s!=null&&s.type)){t(`效果 #${n+1}: 必须选择效果类型`);return}const o=(((p=this.metadata)==null?void 0:p.effects)||[]).find(d=>d.effectTypeName===s.type),l=(o==null?void 0:o.parameters)||[],c=s.parameters||{};for(const[d,u]of Object.entries(c)){if(!u||typeof u!="object"||u.$type!=="FormulaValueConfig")continue;const m=u.Formula||"",h=l.find(b=>{const T=b.propertyName||b.PropertyName||b.name;return String(T)===String(d)}),y=(h==null?void 0:h.typeName)||(h==null?void 0:h.TypeName)||null,g=M(this.metadata,m,{expectedType:y,allowEmpty:!1});for(const b of g.issues)t(`效果 #${n+1} 参数 ${d} 公式: ${b.message}`)}}),e}async renderBasicInfoStep(e){const t=document.createElement("div");t.className="step-content-wrapper basic-info-step";const i=document.createElement("div");i.className="step-header",i.innerHTML=`
            <h4>基础信息配置</h4>
            <p class="step-intro">首先，让我们定义这个能力的基本属性。这些信息将帮助玩家在游戏中识别和理解这个能力。</p>
        `,t.appendChild(i);const r=document.createElement("div");r.className="form-grid-layout";const s=this.createTextInput(this.ability.name,b=>this.updateAbility({name:b}),{placeholder:"例如：火球术、神圣治愈..."});s.className="form-input large-input";const n=document.createElement("span");n.innerHTML='能力名称 <span class="required-mark">*</span>';const o=this.createFormGroup(n,s,"给能力起一个响亮的名字");o.className+=" full-width",r.appendChild(o);const l=await this.loadAbilityTagOptions();l.some(b=>b.value==="")||l.unshift({value:"",label:"无 / 默认"});const c=this.createDropdown(l,this.ability.abilityTag,b=>this.updateAbility({abilityTag:b}),"选择分类...");c.className="form-select";const p=this.createFormGroup("能力类型",c,"决定能力的触发机制（可选）");r.appendChild(p);const d=this.createNumberInput(this.ability.priority,b=>this.updateAbility({priority:b}),{min:0,max:100,placeholder:"0"});d.className="form-input";const u=this.createFormGroup("优先级",d,"数值越高越先结算");r.appendChild(u);const m=this.createTextInput(this.ability.description,b=>this.updateAbility({description:b}),{placeholder:"详细描述能力的效果...",multiline:!0});m.className="form-textarea";const h=this.createFormGroup("能力描述",m,"这段文字将显示在卡牌详情中");h.className+=" full-width",r.appendChild(h);const y=this.createToggleSwitch(this.ability.isActive,b=>this.updateAbility({isActive:b}),"立即启用此能力"),g=this.createFormGroup("状态",y);g.className+=" full-width",r.appendChild(g),t.appendChild(r),e.appendChild(t)}createToggleSwitch(e,t,i){const r=document.createElement("label");r.className="toggle-switch-wrapper";const s=document.createElement("input");s.type="checkbox",s.checked=e,s.addEventListener("change",l=>t(l.target.checked));const n=document.createElement("span");n.className="toggle-slider";const o=document.createElement("span");return o.className="toggle-label",o.textContent=i,r.appendChild(s),r.appendChild(n),r.appendChild(o),r}updateAbility(e){this.ability={...this.ability,...e}}async loadAbilityTagOptions(){try{if(this._abilityTagOptionsCache)return this._abilityTagOptionsCache;const e=await fetch("/api/localization/enums/AbilityTag");if(!e.ok)throw new Error(`HTTP ${e.status}`);const t=await e.json(),i=Object.entries(t).map(([r,s])=>({value:r,label:s}));return i.sort((r,s)=>r.label.localeCompare(s.label,"zh-CN")),this._abilityTagOptionsCache=i,i}catch(e){return console.error("AbilityEditor: Failed to load tags, using defaults.",e),[{value:"Fanfare",label:"入场曲"},{value:"Lastword",label:"谢幕曲"},{value:"AttackEffect",label:"攻击时"},{value:"FightEffect",label:"交战时"},{value:"EvolveEffect",label:"进化时"},{value:"Combo",label:"连击"},{value:"Rally",label:"协作"},{value:"SpellBoost",label:"魔力增幅"},{value:"Awake",label:"觉醒"},{value:"Necromance",label:"死灵术"},{value:"Venge",label:"复仇"},{value:"Reson",label:"共鸣"},{value:"Eager",label:"渴望"},{value:"EarthRitual",label:"土之秘术"},{value:"Enhance",label:"爆能强化"},{value:"Bury",label:"葬送"},{value:"Reanimate",label:"亡者召还"},{value:"Accelerate",label:"激奏"},{value:"Crystalize",label:"结晶"},{value:"Travel",label:"漫游"},{value:"Intromezzo",label:"间奏"},{value:"Wrath",label:"狂乱"},{value:"Choose",label:"抉择"}]}}buildFinalConfig(){return{...this.ability,jsonConfig:JSON.stringify({name:this.ability.name,description:this.ability.description,abilityTag:this.ability.abilityTag,type:"Triggered",trigger:this.ability.trigger,conditions:this.ability.conditions,targets:this.ability.targets,effects:this.ability.effects,isActive:this.ability.isActive,version:"1.0"})}}cleanup(){this.conditionEditor&&this.conditionEditor.cleanup()}}function x(a){if(typeof a!="string")return a;const e=a.trim();return e&&(e.includes("_")?e.split("_").map(t=>t&&t[0].toUpperCase()+t.slice(1)).join("_"):e[0].toUpperCase()+e.slice(1))}function w(a,e){return typeof a!="string"||typeof e!="string"?a===e:a.toLowerCase()===e.toLowerCase()}class f{constructor(){this.container=null,this.metadataManager=new U,this.uiRenderer=new de,this.currentCard=null,this.isInitialized=!1}async initialize(){if(!this.isInitialized){console.log("CardEditor: Initializing...");try{if(this.container=document.getElementById("card-editor-container"),!this.container)throw new Error("Card editor container not found");this.showLoading("正在加载元数据..."),await this.metadataManager.initialize(),await this.uiRenderer.initialize(this.metadataManager),this.createEditorLayout(),await this.populateCardListFilterOptions(),this.bindEventListeners(),await this.loadCardList(),this.isInitialized=!0,console.log("CardEditor: Initialization complete")}catch(e){throw console.error("CardEditor: Initialization failed:",e),this.showError("初始化失败",e.message),e}}}showLoading(e="SYSTEM LOADING..."){this.container&&(this.container.innerHTML=`
                <div class="loading-container tech-panel">
                    <div class="loading-spinner-tech"></div>
                    <div class="loading-text neon-text">${e}</div>
                </div>
            `)}showError(e,t){this.container&&(this.container.innerHTML=`
                <div class="error-container tech-panel error-state">
                    <div class="error-icon-tech"></div>
                    <h3 class="error-title neon-text-red">${e}</h3>
                    <p class="error-message">${t}</p>
                    <button class="btn-tech btn-retry" onclick="location.reload()">REBOOT SYSTEM</button>
                </div>
            `)}createEditorLayout(){this.container.innerHTML=`
            <div class="card-editor-layout">
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
                                            <button class="btn-tech btn-small" id="link-ability-btn">LINK EXISTING</button>
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
                                <div class="form-placeholder">
                                    <p class="tech-text-dim">ASSET MANAGEMENT // UNDER CONSTRUCTION</p>
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
        `}bindEventListeners(){this.container.addEventListener("click",l=>{l.target.classList.contains("tab-button")&&this.switchTab(l.target.dataset.tab)});const e=this.container.querySelector("#save-btn");e&&e.addEventListener("click",async()=>{await this.saveCurrentCard(!1)});const t=this.container.querySelector("#save-as-btn");t&&t.addEventListener("click",async()=>{await this.saveCurrentCard(!0)});const i=this.container.querySelector("#delete-btn");i&&i.addEventListener("click",async()=>{await this.deleteCurrentCard()});const r=this.container.querySelector("#new-card-btn");r&&r.addEventListener("click",()=>this.createNewCard());const s=this.container.querySelector("#card-search");s&&s.addEventListener("input",()=>this.applyCardListFilters()),["#class-filter","#type-filter","#rarity-filter"].forEach(l=>{const c=this.container.querySelector(l);c&&c.addEventListener("change",()=>this.applyCardListFilters())});const n=this.container.querySelector("#add-ability-btn");n&&n.addEventListener("click",()=>this.showAddAbilityModal());const o=this.container.querySelector("#link-ability-btn");o&&o.addEventListener("click",()=>this.showLinkExistingAbilityModal())}switchTab(e){this.container.querySelectorAll(".tab-button").forEach(r=>{r.classList.remove("active")}),this.container.querySelectorAll(".tab-panel").forEach(r=>{r.classList.remove("active")});const t=this.container.querySelector(`[data-tab="${e}"]`),i=this.container.querySelector(`#${e}-tab`);t&&i&&(t.classList.add("active"),i.classList.add("active"),e==="abilities"&&this.currentCard&&this.renderAbilitiesList())}async createNewCard(){console.log("CardEditor: Creating new card..."),this.currentCard={id:null,name:"",description:"",cost:0,attack:0,health:1,cardType:"Follower",cardClass:"Neutral",rarity:"Bronze",keywords:[],tribe:[],canEvolve:!1,evolveId:null,isNew:!0};const e=this.container.querySelector("#editor-title");e&&(e.textContent="新建卡牌"),this.enableEditorButtons(),await this.renderBasicInfoForm(),this.updateCardPreview();const t=this.container.querySelector("#abilities-list");t&&(t.innerHTML=`
                <div class="abilities-placeholder">
                    <p>请先保存卡牌后再配置能力</p>
                </div>
            `)}cleanup(){console.log("CardEditor: Cleaning up..."),this.isInitialized=!1,this.currentCard=null,this.container&&(this.container.innerHTML="")}}let N=null;async function ye(){return N||(N=new f),await N.initialize(),N}function ge(){return N}function ve(){N&&N.cleanup()}f.prototype.enableEditorButtons=function(){var i;const a=this.container.querySelector("#save-btn"),e=this.container.querySelector("#save-as-btn"),t=this.container.querySelector("#delete-btn");a&&(a.disabled=!1),e&&(e.disabled=!1),t&&(t.disabled=(i=this.currentCard)==null?void 0:i.isNew)};f.prototype.disableEditorButtons=function(){const a=this.container.querySelector("#save-btn"),e=this.container.querySelector("#save-as-btn"),t=this.container.querySelector("#delete-btn");a&&(a.disabled=!0),e&&(e.disabled=!0),t&&(t.disabled=!0)};f.prototype.generateSelectOptions=function(a,e){return!a||!Array.isArray(a)?'<option value="">加载中...</option>':a.map(t=>{const i=t.value||t,r=t.label||t,s=w(i,e)?"selected":"";return`<option value="${i}" ${s}>${r}</option>`}).join("")};f.prototype.populateCardListFilterOptions=async function(){const a=this.container.querySelector("#class-filter"),e=this.container.querySelector("#type-filter"),t=this.container.querySelector("#rarity-filter");if(!a||!e||!t)return;const[i,r,s]=await Promise.all([this.metadataManager.getCardClassOptions(),this.metadataManager.getCardTypeOptions(),this.metadataManager.getCardRarityOptions()]);a.innerHTML='<option value="">ALL CLASSES</option>'+this.generateSelectOptions(i,""),e.innerHTML='<option value="">ALL TYPES</option>'+this.generateSelectOptions(r,""),t.innerHTML='<option value="">ALL RARITIES</option>'+this.generateSelectOptions(s,"")};f.prototype.applyCardListFilters=function(){const a=this.container.querySelector("#card-search"),e=this.container.querySelector("#class-filter"),t=this.container.querySelector("#type-filter"),i=this.container.querySelector("#rarity-filter"),r=((a==null?void 0:a.value)||"").trim().toLowerCase(),s=(e==null?void 0:e.value)||"",n=(t==null?void 0:t.value)||"",o=(i==null?void 0:i.value)||"",c=(Array.isArray(this.filteredCardsData)?this.filteredCardsData:Array.isArray(this.allCardsData)?this.allCardsData:[]).filter(p=>{const d=(p.name??p.Name??"").toString().toLowerCase(),u=x(p.class??p.Class??""),m=x(p.type??p.Type??""),h=x(p.rarity??p.Rarity??"");return!(r&&!d.includes(r)||s&&!w(u,s)||n&&!w(m,n)||o&&!w(h,o))});this.renderCardList(c)};f.prototype.updateCardPreview=function(){const a=this.container.querySelector("#card-preview");!a||!this.currentCard||(a.innerHTML=`
        <div class="card-preview-content">
            <div class="card-preview-header">
                <div class="card-name">${this.currentCard.name||"未命名卡牌"}</div>
                <div class="card-cost">${this.currentCard.cost}</div>
            </div>
            <div class="card-preview-body">
                <div class="card-type">${this.currentCard.cardType} - ${this.currentCard.cardClass}</div>
                <div class="card-description">${this.currentCard.description||"暂无描述"}</div>
                ${w(x(this.currentCard.cardType),"Follower")?`
                    <div class="card-stats">
                        <span class="attack">${this.currentCard.attack}</span>
                        <span class="health">${this.currentCard.health}</span>
                    </div>
                `:""}
            </div>
            <div class="card-preview-footer">
                <div class="card-rarity">${this.currentCard.rarity}</div>
            </div>
        </div>
    `)};f.prototype.renderBasicInfoForm=async function(){const a=this.container.querySelector("#basic-tab");if(!a){console.error("CardEditor: Basic tab not found");return}try{const[e,t,i]=await Promise.all([this.metadataManager.getCardTypeOptions(),this.metadataManager.getCardClassOptions(),this.metadataManager.getCardRarityOptions()]),r="",s="";a.innerHTML=`
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
                            <select id="card-type" class="form-select tech-select" ${s}>
                                ${this.generateSelectOptions(e,this.currentCard.cardType)}
                            </select>
                        </div>
                        <div class="form-group">
                            <label class="form-label required">CLASS</label>
                            <select id="card-class" class="form-select tech-select" ${s}>
                                ${this.generateSelectOptions(t,this.currentCard.cardClass)}
                            </select>
                        </div>
                        <div class="form-group">
                            <label class="form-label required">RARITY</label>
                            <select id="card-rarity" class="form-select tech-select" ${s}>
                                ${this.generateSelectOptions(i,this.currentCard.rarity)}
                            </select>
                        </div>
                    </div>
                </div>

            </div>
        `,this.bindBasicFormEvents(),this.bindTagInputEvents(),this.bindFormValidationEvents(),this.updateStatsVisibility()}catch(e){console.error("CardEditor: Failed to render basic info form:",e),a.innerHTML=`
            <div class="error-container">
                <div class="error-icon">⚠️</div>
                <h3 class="error-title">表单加载失败</h3>
                <p class="error-message">${e.message}</p>
            </div>
        `}};f.prototype.renderCardList=function(a){const e=this.container.querySelector("#card-list");if(!e||!a)return;if(!Array.isArray(a)||a.length===0){e.innerHTML=`
            <div class="card-list-empty">
                <p>暂无卡牌数据</p>
                <small>点击"新建卡牌"开始创建</small>
            </div>
        `;return}const t=a.map(i=>{const s=this.findEvolvedFormsForCard(i).length>0;return`
            <div class="card-list-item tech-list-item" data-card-id="${i.id!==void 0?i.id:i.CardId}">
                <div class="card-item-header">
                    <span class="card-item-name tech-text-bold">${i.name||i.Name||"UNTITLED"}</span>
                    <div class="card-item-indicators">
                        ${s?'<div class="indicator-icon evolve" title="EVOLVE"></div>':""}
                        <span class="card-item-cost tech-badge">${i.cost??i.Cost??0}</span>
                    </div>
                </div>
                <div class="card-item-details">
                    <span class="card-item-type tech-tag">${x(i.type??i.Type??"UNKNOWN")}</span>
                    <span class="card-item-class tech-tag">${x(i.class??i.Class??"NEUTRAL")}</span>
                    <span class="card-item-rarity tech-tag rarity-${x(i.rarity??i.Rarity??"Common").toLowerCase()}">${x(i.rarity??i.Rarity??"COMMON")}</span>
                </div>
                ${w(x(i.type||i.Type),"Follower")?`
                    <div class="card-item-stats">
                        <span class="stat-attack">ATK ${i.attack??i.Attack??0}</span>
                        <span class="stat-health">HP ${i.health??i.Health??1}</span>
                    </div>
                `:""}
                <div class="tech-corner-decoration"></div>
            </div>
        `}).join("");e.innerHTML=t,this.bindCardListEvents()};f.prototype.bindCardListEvents=function(){this.container.querySelectorAll(".card-list-item").forEach(e=>{e.addEventListener("click",()=>{const t=e.dataset.cardId;this.selectCard(t)})})};f.prototype.bindBasicFormEvents=function(){const a=this.container.querySelector("#basic-tab");if(!a)return;const e=a.querySelector("#card-name"),t=a.querySelector("#card-description");e&&e.addEventListener("input",n=>{this.currentCard.name=n.target.value,this.updateCardPreview()}),t&&t.addEventListener("input",n=>{this.currentCard.description=n.target.value,this.updateCardPreview()}),a.querySelectorAll(".stepper-btn").forEach(n=>{n.addEventListener("click",o=>{const l=o.target.dataset.target,c=a.querySelector(`#${l}`);if(!c)return;const p=o.target.classList.contains("increase"),d=parseInt(c.value)||0,u=parseInt(c.min)||0,m=parseInt(c.max)||99;let h=p?d+1:d-1;h=Math.max(u,Math.min(m,h)),c.value=h,this.updateCardProperty(l.replace("card-",""),h)})}),a.querySelectorAll('input[type="number"]').forEach(n=>{n.addEventListener("input",o=>{const l=o.target.id.replace("card-",""),c=parseInt(o.target.value)||0;this.updateCardProperty(l,c)})}),a.querySelectorAll("select").forEach(n=>{n.addEventListener("change",o=>{const l=o.target.id.replace("card-","");this.updateCardProperty(l,o.target.value),l==="type"&&this.updateStatsVisibility()})})};f.prototype.updateCardProperty=function(a,e){if(!this.currentCard)return;const i={name:"name",description:"description",cost:"cost",attack:"attack",health:"health",type:"cardType",class:"cardClass",rarity:"rarity"}[a]||a;this.currentCard[i]=e,this.updateCardPreview(),console.log(`CardEditor: Updated ${i} to ${e}`)};f.prototype.updateStatsVisibility=function(){const a=this.container.querySelector("#stats-container");if(!a||!this.currentCard)return;const e=w(x(this.currentCard.cardType),"Follower");a.style.display=e?"block":"none"};f.prototype.buildCardUpsertDto=function(a){var i;if(!this.currentCard)return null;const e=x(this.currentCard.cardType),t=w(e,"Follower");return{id:a?null:this.currentCard.id,name:((i=this.currentCard.name)==null?void 0:i.trim())||"",type:e,rarity:x(this.currentCard.rarity),card_set:"Basic",class:x(this.currentCard.cardClass),cost:Number(this.currentCard.cost)||0,attack:t?Number(this.currentCard.attack)||0:null,health:t?Number(this.currentCard.health)||1:null,description:this.currentCard.description||"",can_evolve:!!this.currentCard.canEvolve,evolve_id:this.currentCard.evolveId??null,keywords:Array.isArray(this.currentCard.keywords)?this.currentCard.keywords:[],tribe:Array.isArray(this.currentCard.tribe)?this.currentCard.tribe:[]}};f.prototype.saveCurrentCard=async function(a){if(!this.currentCard){alert("请先选择或创建一张卡牌");return}if(!this.validateForm()){alert("请先修正表单错误后再保存");return}const e=!!this.currentCard.isNew||!!a,t=this.buildCardUpsertDto(e);try{const i=e?"/api/cards":`/api/cards/${this.currentCard.id}`,s=await fetch(i,{method:e?"POST":"PUT",headers:{"Content-Type":"application/json"},body:JSON.stringify(t)});if(!s.ok)throw new Error(`HTTP ${s.status}: ${s.statusText}`);const n=await s.json(),o=(n==null?void 0:n.id)??(n==null?void 0:n.cardId)??(n==null?void 0:n.CardId);await this.loadCardList(),o!=null&&await this.selectCard(o),alert(e?"卡牌创建成功！":"卡牌保存成功！")}catch(i){console.error("CardEditor: Failed to save card:",i),alert(`保存失败: ${i.message}`)}};f.prototype.deleteCurrentCard=async function(){if(!(!this.currentCard||this.currentCard.isNew)&&confirm(`确定要删除卡牌 "${this.currentCard.name}" 吗？此操作不可撤销。`))try{const a=await fetch(`/api/cards/${this.currentCard.id}`,{method:"DELETE"});if(!a.ok&&a.status!==204)throw new Error(`HTTP ${a.status}: ${a.statusText}`);this.currentCard=null,this.disableEditorButtons();const e=this.container.querySelector("#editor-title");e&&(e.textContent="AWAITING SELECTION");const t=this.container.querySelector("#basic-tab");t&&(t.innerHTML=`
                <div class="form-placeholder">
                    <p class="tech-text-dim">SELECT A CARD TO EDIT</p>
                </div>
            `);const i=this.container.querySelector("#card-preview");i&&(i.innerHTML=`
                <div class="preview-placeholder">
                    <p class="tech-text-dim">AWAITING DATA</p>
                </div>
            `),await this.loadCardList(),alert("卡牌已删除")}catch(a){console.error("CardEditor: Failed to delete card:",a),alert(`删除失败: ${a.message}`)}};f.prototype.loadCardList=async function(){const a=this.container.querySelector("#card-list");if(!a){console.error("CardEditor: Card list container not found");return}try{a.innerHTML=`
            <div class="card-list-loading">
                <div class="loading-spinner"></div>
                <p>正在加载卡牌列表...</p>
            </div>
        `,console.log("CardEditor: Loading card list from API...");const e=await fetch("/api/cards");if(!e.ok)throw new Error(`HTTP ${e.status}: ${e.statusText}`);const t=await e.json();console.log("CardEditor: Loaded all cards:",t);const i=this.filterCardsForList(t);console.log("CardEditor: Filtered cards for list:",i),console.log("CardEditor: Original card count:",t.length,"Filtered count:",i.length),this.allCardsData=t,this.filteredCardsData=i,this.applyCardListFilters()}catch(e){console.error("CardEditor: Failed to load card list:",e),a.innerHTML=`
            <div class="card-list-error">
                <div class="error-icon">⚠️</div>
                <p>加载卡牌列表失败</p>
                <small>${e.message}</small>
                <button class="retry-btn" onclick="cardEditorInstance.loadCardList()">重试</button>
            </div>
        `}};f.prototype.filterCardsForList=function(a){if(!Array.isArray(a))return[];const e=new Set;return a.forEach(t=>{const i=t.evolve_id||t.EvolveId;i&&e.add(i)}),a.filter(t=>{const i=t.id!==void 0?t.id:t.CardId,r=x(t.type||t.Type||""),s=e.has(i)&&(t.name||"").includes("进化"),n=w(r,"Leader");return!s||n})};f.prototype.findEvolvedFormsForCard=function(a){if(console.log("CardEditor: Finding evolved forms for card:",a),!this.allCardsData||!Array.isArray(this.allCardsData))return console.log("CardEditor: No allCardsData available"),[];const e=a.evolve_id||a.EvolveId||a.evolveId;if(console.log("CardEditor: Base card evolve_id:",e),!e)return console.log("CardEditor: No evolve_id found, card cannot evolve"),[];const t=this.allCardsData.filter(i=>{const s=(i.id!==void 0?i.id:i.CardId)===e;return s&&console.log("CardEditor: Found evolved form:",i),s});return console.log("CardEditor: Total evolved forms found:",t.length),t};f.prototype.selectCard=async function(a){try{console.log("CardEditor: Selecting card:",a),this.container.querySelectorAll(".card-list-item").forEach(s=>{s.classList.remove("selected")});const e=this.container.querySelector(`[data-card-id="${a}"]`);e&&e.classList.add("selected");const t=await fetch(`/api/cards/${a}`);if(!t.ok)throw new Error(`HTTP ${t.status}: ${t.statusText}`);const i=await t.json();console.log("CardEditor: Loaded card details:",i),this.currentCard=this.convertApiCardToEditorFormat(i);const r=this.container.querySelector("#editor-title");r&&(r.textContent=`编辑卡牌: ${this.currentCard.name}`),this.enableEditorButtons(),await this.renderBasicInfoForm(),this.updateCardPreview(),this.loadEvolvedForms()}catch(e){console.error("CardEditor: Failed to select card:",e),alert(`加载卡牌失败: ${e.message}`)}};f.prototype.loadEvolvedForms=function(){if(!this.currentCard){console.log("CardEditor: No current card for evolution check");return}console.log("CardEditor: Checking evolved forms for card:",this.currentCard),console.log("CardEditor: Card evolve_id:",this.currentCard.evolveId);const a=this.findEvolvedFormsForCard(this.currentCard);this.currentCardEvolvedForms=a,this.currentEvolvedFormIndex=-1,console.log("CardEditor: Found evolved forms for current card:",a),console.log("CardEditor: Evolved forms count:",a.length),this.updateEvolvedFormsNavigation()};f.prototype.updateEvolvedFormsNavigation=function(){console.log("CardEditor: Updating evolution navigation...");let a=this.container.querySelector("#evolution-navigation");if(console.log("CardEditor: Existing evolution nav:",a),!this.currentCardEvolvedForms||this.currentCardEvolvedForms.length===0){console.log("CardEditor: No evolved forms, hiding navigation"),a&&(a.style.display="none");return}if(console.log("CardEditor: Has evolved forms, showing navigation"),!a){const e=this.container.querySelector(".editor-main");console.log("CardEditor: Editor main element:",e),e&&(e.insertAdjacentHTML("afterbegin",`
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
            `),a=this.container.querySelector("#evolution-navigation"),this.bindEvolutionNavigationEvents())}a&&(a.style.display="block",this.updateEvolutionIndicator())};f.prototype.bindEvolutionNavigationEvents=function(){const a=this.container.querySelector("#prev-evolution"),e=this.container.querySelector("#next-evolution");a&&a.addEventListener("click",()=>{this.showPreviousEvolution()}),e&&e.addEventListener("click",()=>{this.showNextEvolution()})};f.prototype.showPreviousEvolution=function(){if(!this.currentCardEvolvedForms)return;const a=this.currentCardEvolvedForms.length+1;this.currentEvolvedFormIndex=(this.currentEvolvedFormIndex-1+a)%a-1,this.switchToEvolutionForm()};f.prototype.showNextEvolution=function(){if(!this.currentCardEvolvedForms)return;const a=this.currentCardEvolvedForms.length+1;this.currentEvolvedFormIndex=(this.currentEvolvedFormIndex+1)%a,this.currentEvolvedFormIndex>=this.currentCardEvolvedForms.length&&(this.currentEvolvedFormIndex=-1),this.switchToEvolutionForm()};f.prototype.switchToEvolutionForm=function(){if(this.currentEvolvedFormIndex===-1)this.loadOriginalCardForm();else{const a=this.currentCardEvolvedForms[this.currentEvolvedFormIndex];this.loadEvolvedCardForm(a)}this.updateEvolutionIndicator()};f.prototype.loadOriginalCardForm=async function(){try{const a=await fetch(`/api/cards/${this.currentCard.id}`);if(!a.ok)throw new Error(`HTTP ${a.status}: ${a.statusText}`);const e=await a.json();this.currentCard=this.convertApiCardToEditorFormat(e),await this.renderBasicInfoForm(),this.updateCardPreview()}catch(a){console.error("CardEditor: Failed to load original card form:",a),alert(`加载原始卡牌失败: ${a.message}`)}};f.prototype.loadEvolvedCardForm=function(a){this.currentCard=this.convertApiCardToEditorFormat(a),this.currentCard.isEvolvedForm=!0,this.renderBasicInfoForm(),this.updateCardPreview()};f.prototype.updateEvolutionIndicator=function(){const a=this.container.querySelector("#evolution-indicator");if(a)if(this.currentEvolvedFormIndex===-1)a.textContent="原始形态";else{const e=this.currentCardEvolvedForms[this.currentEvolvedFormIndex],t=e.name||e.Name||"进化形态";a.textContent=t}};f.prototype.convertApiCardToEditorFormat=function(a){const e=a.data||a;return{id:e.id!==void 0?e.id:e.CardId,name:e.name??e.Name??"",description:e.description??e.Description??"",cost:e.cost??e.Cost??0,attack:e.attack??e.Attack??0,health:e.health??e.Health??1,cardType:x(e.type??e.Type??"Follower"),cardClass:x(e.class??e.Class??"Neutral"),rarity:x(e.rarity??e.Rarity??"Bronze"),keywords:e.keywords||e.Keywords||[],tribe:e.tribe||e.Tribe||[],canEvolve:e.canEvolve??e.CanEvolve??!1,evolveId:e.evolve_id??e.evolveId??e.EvolveId??null,isNew:!1}};f.prototype.renderTagList=function(a,e){return!Array.isArray(a)||a.length===0?'<div class="tag-list-empty">暂无标签</div>':a.map(t=>`
        <div class="tag-item" data-tag="${t}" data-type="${e}">
            <span class="tag-text">${t}</span>
            <button type="button" class="tag-remove-btn" data-tag="${t}" data-type="${e}">×</button>
        </div>
    `).join("")};f.prototype.bindTagInputEvents=function(){const a=this.container.querySelector("#basic-tab");if(!a)return;const e=a.querySelector("#keywords-select"),t=a.querySelector('[data-target="keywords"]');e&&(this.loadKeywordOptions(e),e.addEventListener("change",s=>{const n=s.target.value.trim();n&&(this.addTag("keywords",n),s.target.value="")})),t&&t.addEventListener("click",()=>{const s=e.value.trim();s&&(this.addTag("keywords",s),e.value="")});const i=a.querySelector("#tribe-input"),r=a.querySelector('[data-target="tribe"]');i&&i.addEventListener("keypress",s=>{s.key==="Enter"&&(s.preventDefault(),this.addTag("tribe",i.value.trim()),i.value="")}),r&&r.addEventListener("click",()=>{const s=i.value.trim();s&&(this.addTag("tribe",s),i.value="")}),a.addEventListener("click",s=>{if(s.target.classList.contains("tag-remove-btn")){const n=s.target.dataset.tag,o=s.target.dataset.type;this.removeTag(o,n)}})};f.prototype.addTag=function(a,e){if(!(!this.currentCard||!e)){if(this.currentCard[a]||(this.currentCard[a]=[]),this.currentCard[a].includes(e)){alert(`标签 "${e}" 已存在`);return}this.currentCard[a].push(e),this.updateTagList(a),this.updateCardPreview(),console.log(`CardEditor: Added ${a} tag: ${e}`)}};f.prototype.removeTag=function(a,e){if(!this.currentCard||!this.currentCard[a])return;const t=this.currentCard[a].indexOf(e);t>-1&&(this.currentCard[a].splice(t,1),this.updateTagList(a),this.updateCardPreview(),console.log(`CardEditor: Removed ${a} tag: ${e}`))};f.prototype.updateTagList=function(a){const e=this.container.querySelector(`#${a}-list`);if(!e)return;const t=this.currentCard[a]||[];e.innerHTML=this.renderTagList(t,a)};f.prototype.bindFormValidationEvents=function(){const a=this.container.querySelector("#basic-tab");if(!a)return;const e=a.querySelector("#card-name"),t=a.querySelector("#card-cost"),i=a.querySelector("#card-attack"),r=a.querySelector("#card-health"),s=a.querySelector("#card-type"),n=a.querySelector("#card-class"),o=a.querySelector("#card-rarity");[e,t,i,r,s,n,o].forEach(l=>{l&&(l.addEventListener("input",()=>this.validateForm()),l.addEventListener("change",()=>this.validateForm()))}),this.validateForm()};f.prototype.validateForm=function(){if(!this.currentCard)return;const a=[];return(!this.currentCard.name||this.currentCard.name.trim()==="")&&a.push("卡牌名称不能为空"),this.currentCard.cardType||a.push("请选择卡牌类型"),this.currentCard.cardClass||a.push("请选择卡牌职业"),this.currentCard.rarity||a.push("请选择卡牌稀有度"),(this.currentCard.cost<0||this.currentCard.cost>20)&&a.push("费用必须在0-20之间"),w(x(this.currentCard.cardType),"Follower")&&((this.currentCard.attack<0||this.currentCard.attack>99)&&a.push("攻击力必须在0-99之间"),(this.currentCard.health<1||this.currentCard.health>99)&&a.push("生命值必须在1-99之间")),this.updateSaveButtonState(a.length===0),this.displayValidationErrors(a),a.length===0};f.prototype.updateSaveButtonState=function(a){const e=this.container.querySelector("#save-btn"),t=this.container.querySelector("#save-as-btn");e&&(e.disabled=!a||!this.currentCard,e.title=a?"保存卡牌":"请修正表单错误后再保存"),t&&(t.disabled=!a||!this.currentCard,t.title=a?"另存为新卡牌":"请修正表单错误后再保存")};f.prototype.displayValidationErrors=function(a){if(this.container.querySelectorAll(".validation-error").forEach(i=>i.remove()),a.length===0)return;const t=this.container.querySelector("#basic-tab .form-container");if(t){const i=document.createElement("div");i.className="validation-error",i.innerHTML=`
            <div class="error-header">
                <span class="error-icon">⚠️</span>
                <span class="error-title">表单验证失败</span>
            </div>
            <ul class="error-list">
                ${a.map(r=>`<li>${r}</li>`).join("")}
            </ul>
        `,t.insertBefore(i,t.firstChild)}};f.prototype.renderAbilitiesList=async function(){const a=this.container.querySelector("#abilities-list");if(!(!a||!this.currentCard))try{a.innerHTML=`
            <div class="abilities-loading">
                <div class="loading-spinner"></div>
                <p>正在加载能力列表...</p>
            </div>
        `,console.log("CardEditor: Loading abilities for card:",this.currentCard.id);const e=await fetch(`/api/cards/${this.currentCard.id}/abilities`);if(!e.ok)throw new Error(`HTTP ${e.status}: ${e.statusText}`);const t=await e.json();console.log("CardEditor: Loaded abilities:",t),this.renderAbilitiesListContent(t)}catch(e){console.error("CardEditor: Failed to load abilities:",e),a.innerHTML=`
            <div class="abilities-error">
                <div class="error-icon">⚠️</div>
                <p>加载能力列表失败</p>
                <small>${e.message}</small>
                <button class="retry-btn" onclick="cardEditorInstance.renderAbilitiesList()">重试</button>
            </div>
        `}};f.prototype.renderAbilitiesListContent=function(a){const e=this.container.querySelector("#abilities-list");if(!e)return;if(!Array.isArray(a)||a.length===0){e.innerHTML=`
            <div class="abilities-placeholder">
                <p>暂无关联能力</p>
                <small>点击"添加能力"开始配置</small>
            </div>
        `;return}const t=a.map(i=>`
        <div class="ability-item tech-panel-item" data-ability-id="${i.id}">
            <div class="ability-header">
                <div class="ability-info">
                    <span class="ability-name tech-text-bold">${i.name||"未命名能力"}</span>
                    <span class="ability-tag tech-tag">${i.abilityTag||"Unknown"}</span>
                </div>
                <div class="ability-actions">
                    <button class="btn-tech-small edit-ability-btn" data-ability-id="${i.id}">EDIT</button>
                    <button class="btn-tech-small btn-danger remove-ability-btn" data-ability-id="${i.id}">DELETE</button>
                </div>
            </div>
            <div class="ability-details">
                <div class="ability-description tech-text-dim">${i.description||"暂无描述"}</div>
                <div class="ability-config">
                    <span class="config-item tech-tag">TYPE: ${i.type||"Triggered"}</span>
                    <span class="config-item tech-tag">PRIORITY: ${i.priority||0}</span>
                    <span class="config-item tech-tag ${i.isActive?"status-active":"status-inactive"}">
                        ${i.isActive?"ACTIVE":"INACTIVE"}
                    </span>
                </div>
            </div>
        </div>
    `).join("");e.innerHTML=t,this.bindAbilityItemEvents()};f.prototype.bindAbilityItemEvents=function(){const a=this.container.querySelector("#abilities-list");if(!a)return;a.querySelectorAll(".edit-ability-btn").forEach(r=>{r.addEventListener("click",s=>{s.stopPropagation();const n=r.dataset.abilityId;this.editAbility(n)})}),a.querySelectorAll(".remove-ability-btn").forEach(r=>{r.addEventListener("click",s=>{s.stopPropagation();const n=r.dataset.abilityId;this.removeAbility(n)})}),a.querySelectorAll(".ability-item").forEach(r=>{r.addEventListener("click",()=>{r.classList.toggle("expanded")})})};f.prototype.showAddAbilityModal=async function(){if(!this.currentCard){alert("请先选择一张卡牌");return}console.log("CardEditor: Showing add ability modal for card:",this.currentCard.id);try{document.body.insertAdjacentHTML("beforeend",`
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
    `);const e=document.getElementById("add-ability-editor-container"),t=new Q,i={cardId:this.currentCard.id,name:"",description:"",abilityTag:"",abilityType:1,priority:0,isActive:!0,trigger:{eventTypes:[],relationType:"Self",timing:"On",validZones:[]},conditions:[],targets:[],effects:[]};await t.render(e,i,async n=>{n===null?this.closeAddAbilityModal():(await this.createNewAbilityFromConfig(n),this.closeAddAbilityModal())});const r=document.getElementById("close-add-ability-modal");r&&r.addEventListener("click",()=>this.closeAddAbilityModal());const s=document.getElementById("add-ability-modal");s.addEventListener("click",n=>{n.target===s&&this.closeAddAbilityModal()})}catch(a){console.error("CardEditor: Failed to show add ability modal:",a),alert(`打开添加能力窗口失败: ${a.message}`)}};f.prototype.createNewAbilityFromConfig=async function(a){try{console.log("CardEditor: Creating new ability from config:",a);const e={cardId:this.currentCard.id,name:a.name,description:a.description,abilityTag:a.abilityTag,abilityType:a.abilityType||1,priority:a.priority||0,isActive:a.isActive,jsonConfig:a.jsonConfig},t=await fetch("/api/abilities",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify(e)});if(!t.ok)throw new Error(`HTTP ${t.status}: ${t.statusText}`);const i=await t.json();console.log("CardEditor: Created ability:",i),await this.renderAbilitiesList(),alert("能力添加成功！")}catch(e){console.error("CardEditor: Failed to create ability:",e),alert(`添加能力失败: ${e.message}`)}};f.prototype.bindAddAbilityModalEvents=function(){const a=document.getElementById("add-ability-modal");if(!a)return;const e=a.querySelector("#close-add-ability-modal"),t=a.querySelector("#cancel-add-ability"),i=a;[e,t].forEach(n=>{n&&n.addEventListener("click",()=>this.closeAddAbilityModal())}),i.addEventListener("click",n=>{n.target===i&&this.closeAddAbilityModal()}),a.querySelectorAll(".modal-tab-btn").forEach(n=>{n.addEventListener("click",()=>{const o=n.dataset.tab;this.switchAddAbilityTab(o)})});const s=a.querySelector("#confirm-add-ability");s&&s.addEventListener("click",async()=>{s.disabled=!0,s.textContent="PROCESSING...";try{await this.confirmAddAbility()}catch(n){console.error("Add ability failed:",n)}finally{s.disabled=!1,s.textContent="确认添加"}}),this.loadExistingAbilities()};f.prototype.closeAddAbilityModal=function(){const a=document.getElementById("add-ability-modal");a&&a.remove()};f.prototype.showLinkExistingAbilityModal=async function(){if(!this.currentCard||!this.currentCard.id){alert("请先选择并保存一张卡牌");return}try{document.body.insertAdjacentHTML("beforeend",`
            <div class="modal-overlay" id="link-ability-modal">
                <div class="modal-container ability-edit-container">
                    <div class="modal-header">
                        <h3>关联现有能力</h3>
                        <button class="modal-close-btn" id="close-link-ability-modal">×</button>
                    </div>
                    <div class="modal-content">
                        <div class="tech-text-dim" style="margin-bottom: 12px;">请选择一个能力并确认关联</div>
                        <div class="abilities-list custom-scrollbar" id="link-abilities-list">
                            <div class="loading-spinner"></div>
                            <p>正在加载能力列表...</p>
                        </div>
                        <div class="modal-actions" style="margin-top: 12px; display: flex; justify-content: flex-end; gap: 8px;">
                            <button class="btn-tech btn-secondary" id="cancel-link-ability">取消</button>
                            <button class="btn-tech btn-primary" id="confirm-link-ability" disabled>确认关联</button>
                        </div>
                    </div>
                </div>
            </div>
        `);const e=document.getElementById("link-ability-modal"),t=document.getElementById("close-link-ability-modal"),i=document.getElementById("cancel-link-ability"),r=document.getElementById("confirm-link-ability"),s=document.getElementById("link-abilities-list"),n=()=>this.closeLinkExistingAbilityModal();t&&t.addEventListener("click",n),i&&i.addEventListener("click",n),e&&e.addEventListener("click",c=>{c.target===e&&n()}),this.selectedExistingAbilityId=null;const o=await fetch("/api/abilities");if(!o.ok)throw new Error(`HTTP ${o.status}: ${o.statusText}`);const l=await o.json();if(!Array.isArray(l)||l.length===0){s.innerHTML='<div class="abilities-placeholder"><p>暂无可用能力</p></div>';return}s.innerHTML=l.map(c=>`
            <div class="ability-item tech-panel-item" data-ability-id="${c.id}">
                <div class="ability-header">
                    <div class="ability-info">
                        <span class="ability-name tech-text-bold">${c.name||"未命名能力"}</span>
                        <span class="ability-tag tech-tag">${c.abilityTag||"Unknown"}</span>
                    </div>
                    <div class="ability-config">
                        <span class="config-item tech-tag">CARD: ${c.cardId??"-"}</span>
                    </div>
                </div>
                <div class="ability-details">
                    <div class="ability-description tech-text-dim">${c.description||""}</div>
                </div>
            </div>
        `).join(""),s.querySelectorAll(".ability-item").forEach(c=>{c.addEventListener("click",()=>{s.querySelectorAll(".ability-item").forEach(p=>p.classList.remove("selected")),c.classList.add("selected"),this.selectedExistingAbilityId=c.dataset.abilityId,r&&(r.disabled=!1)})}),r&&r.addEventListener("click",async()=>{if(this.selectedExistingAbilityId){r.disabled=!0;try{const c=await fetch(`/api/cards/${this.currentCard.id}/abilities/${this.selectedExistingAbilityId}`,{method:"POST"});if(!c.ok)throw new Error(`HTTP ${c.status}: ${c.statusText}`);await this.renderAbilitiesList(),n(),alert("能力关联成功！")}catch(c){console.error("CardEditor: Failed to link ability:",c),alert(`关联能力失败: ${c.message}`)}finally{r.disabled=!1}}})}catch(a){console.error("CardEditor: Failed to show link ability modal:",a),alert(`打开关联窗口失败: ${a.message}`),this.closeLinkExistingAbilityModal()}};f.prototype.closeLinkExistingAbilityModal=function(){const a=document.getElementById("link-ability-modal");a&&a.remove()};f.prototype.switchAddAbilityTab=function(a){const e=document.getElementById("add-ability-modal");if(!e)return;e.querySelectorAll(".modal-tab-btn").forEach(r=>{r.classList.remove("active")}),e.querySelectorAll(".modal-tab-panel").forEach(r=>{r.classList.remove("active")});const t=e.querySelector(`[data-tab="${a}"]`),i=e.querySelector(`#${a}-ability-panel`);t&&i&&(t.classList.add("active"),i.classList.add("active"))};f.prototype.loadExistingAbilities=async function(){const a=document.getElementById("existing-abilities-list");if(a)try{console.log("CardEditor: Loading existing abilities...");const e=await fetch("/api/abilities");if(!e.ok)throw new Error(`HTTP ${e.status}: ${e.statusText}`);const t=await e.json();console.log("CardEditor: Loaded existing abilities:",t),this.renderExistingAbilitiesList(t)}catch(e){console.error("CardEditor: Failed to load existing abilities:",e),a.innerHTML=`
            <div class="error-container">
                <div class="error-icon">⚠️</div>
                <p>加载能力库失败</p>
                <small>${e.message}</small>
            </div>
        `}};f.prototype.renderExistingAbilitiesList=function(a){const e=document.getElementById("existing-abilities-list");if(!e)return;if(!Array.isArray(a)||a.length===0){e.innerHTML=`
            <div class="empty-state">
                <p>暂无可用能力</p>
                <small>请先创建一些能力</small>
            </div>
        `;return}const t=a.map(r=>`
        <div class="existing-ability-item" data-ability-id="${r.id}">
            <div class="ability-info">
                <div class="ability-name">${r.name||"未命名能力"}</div>
                <div class="ability-tag">${r.abilityTag||"Unknown"}</div>
                <div class="ability-description">${r.description||"暂无描述"}</div>
            </div>
            <button class="btn-secondary select-ability-btn" data-ability-id="${r.id}">选择</button>
        </div>
    `).join("");e.innerHTML=t,e.querySelectorAll(".select-ability-btn").forEach(r=>{r.addEventListener("click",()=>{const s=r.dataset.abilityId;this.selectExistingAbility(s)})})};f.prototype.selectExistingAbility=function(a){console.log("CardEditor: Selected existing ability:",a);const e=document.getElementById("existing-abilities-list");if(e){e.querySelectorAll(".existing-ability-item").forEach(i=>{i.classList.remove("selected")});const t=e.querySelector(`[data-ability-id="${a}"]`);t&&t.classList.add("selected")}this.selectedExistingAbilityId=a};f.prototype.confirmAddAbility=async function(){const a=document.getElementById("add-ability-modal");if(!a)return;const e=a.querySelector(".modal-tab-btn.active");if(!e)return;const t=e.dataset.tab;try{t==="create"?await this.createNewAbility():t==="existing"&&await this.linkExistingAbility(),this.closeAddAbilityModal(),await this.renderAbilitiesList()}catch(i){console.error("CardEditor: Failed to add ability:",i),alert(`添加能力失败: ${i.message}`)}};f.prototype.createNewAbility=async function(){const a=document.getElementById("add-ability-modal");if(!a)return;const e=a.querySelector("#ability-name").value.trim(),t=a.querySelector("#ability-description").value.trim(),i=a.querySelector("#ability-tag").value,r=a.querySelector("#ability-type").value,s=parseInt(a.querySelector("#ability-priority").value)||0;if(!e){alert("请输入能力名称");return}if(!i){alert("请选择能力标签");return}const n={cardId:this.currentCard.id,name:e,description:t,abilityTag:i,abilityType:r?parseInt(r):1,priority:s,isActive:!0,jsonConfig:JSON.stringify({name:e,description:t,abilityTag:i,type:r||"Triggered",trigger:{eventTypes:["CardPlayed"],timing:"On",validZones:[]},conditions:[],targets:{},effects:[]})};console.log("CardEditor: Creating new ability:",n);const o=await fetch("/api/abilities",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify(n)});if(!o.ok)throw new Error(`HTTP ${o.status}: ${o.statusText}`);const l=await o.json();console.log("CardEditor: Created ability:",l)};f.prototype.linkExistingAbility=async function(){if(!this.selectedExistingAbilityId){alert("请选择一个现有能力");return}console.log("CardEditor: Linking existing ability:",this.selectedExistingAbilityId);const a=await fetch(`/api/cards/${this.currentCard.id}/abilities/${this.selectedExistingAbilityId}`,{method:"POST",headers:{"Content-Type":"application/json"}});if(!a.ok)throw new Error(`HTTP ${a.status}: ${a.statusText}`);const e=await a.json();console.log("CardEditor: Linked ability:",e),this.selectedExistingAbilityId=null};f.prototype.editAbility=async function(a){console.log("CardEditor: Editing ability:",a);try{const e=await fetch(`/api/abilities/${a}`);if(!e.ok)throw new Error(`HTTP ${e.status}: ${e.statusText}`);const t=await e.json();console.log("CardEditor: Loaded ability for editing:",t),this.showAbilityEditModal(t)}catch(e){console.error("CardEditor: Failed to load ability for editing:",e),alert(`加载能力失败: ${e.message}`)}};f.prototype.removeAbility=async function(a){if(confirm("确定要移除这个能力吗？"))try{console.log("CardEditor: Removing ability:",a);const e=await fetch(`/api/cards/${this.currentCard.id}/abilities/${a}`,{method:"DELETE"});if(!e.ok)throw new Error(`HTTP ${e.status}: ${e.statusText}`);console.log("CardEditor: Ability removed successfully"),await this.renderAbilitiesList()}catch(e){console.error("CardEditor: Failed to remove ability:",e),alert(`移除能力失败: ${e.message}`)}};f.prototype.showAbilityEditModal=async function(a){console.log("CardEditor: Showing ability edit modal for:",a);try{const e=this.parseAbilityConfig(a),t=`
            <div class="modal-overlay" id="ability-edit-modal">
                <div class="modal-container ability-edit-container">
                    <div class="modal-header">
                        <h3>编辑能力: ${a.name}</h3>
                        <button class="modal-close-btn" id="close-ability-edit-modal">×</button>
                    </div>
                    <div class="modal-content" id="ability-editor-container">
                        <!-- 能力编辑器将在这里渲染 -->
                    </div>
                </div>
            </div>
        `;document.body.insertAdjacentHTML("beforeend",t);const i=document.getElementById("ability-editor-container");await new Q().render(i,e,async s=>{s===null?this.closeAbilityEditModal():(await this.saveAbilityChanges(a,s),this.closeAbilityEditModal())}),this.bindAbilityEditModalCloseEvents()}catch(e){console.error("CardEditor: Failed to show ability edit modal:",e),alert(`打开能力编辑器失败: ${e.message}`)}};f.prototype.parseAbilityConfig=function(a){let e={id:a.id,name:a.name||"",description:a.description||"",abilityTag:a.abilityTag||"",abilityType:a.abilityType||1,priority:a.priority||0,isActive:a.isActive!==!1,trigger:{eventTypes:[],relationType:"",timing:"On",validZones:[]},conditions:[],targets:[],effects:[]};if(a.jsonConfig)try{let t;typeof a.jsonConfig=="string"?t=JSON.parse(a.jsonConfig):t=a.jsonConfig,t.trigger&&(e.trigger={...e.trigger,...t.trigger}),t.conditions&&(e.conditions=t.conditions),t.targets&&(e.targets=t.targets),t.effects&&(e.effects=t.effects)}catch(t){console.warn("CardEditor: Failed to parse ability JSON config:",t)}return e};f.prototype.bindAbilityEditModalCloseEvents=function(){const a=document.getElementById("ability-edit-modal");if(!a)return;const e=a.querySelector("#close-ability-edit-modal");e&&e.addEventListener("click",()=>{this.closeAbilityEditModal()}),a.addEventListener("click",i=>{i.target===a&&this.closeAbilityEditModal()});const t=i=>{i.key==="Escape"&&(this.closeAbilityEditModal(),document.removeEventListener("keydown",t))};document.addEventListener("keydown",t)};f.prototype.closeAbilityEditModal=function(){const a=document.getElementById("ability-edit-modal");a&&a.remove()};f.prototype.saveAbilityChanges=async function(a,e){try{console.log("CardEditor: Saving ability changes:",e);const t=await fetch(`/api/abilities/${a.id}`,{method:"PUT",headers:{"Content-Type":"application/json"},body:JSON.stringify(e)});if(!t.ok)throw new Error(`HTTP ${t.status}: ${t.statusText}`);const i=await t.json();console.log("CardEditor: Ability updated successfully:",i),await this.renderAbilitiesList(),alert("能力保存成功！")}catch(t){throw console.error("CardEditor: Failed to save ability changes:",t),alert(`保存能力失败: ${t.message}`),t}};f.prototype.bindAbilityEditModalEvents=function(a){const e=document.getElementById("ability-edit-modal");if(!e)return;const t=e.querySelector("#close-ability-edit-modal"),i=e.querySelector("#cancel-ability-edit");[t,i].forEach(d=>{d&&d.addEventListener("click",()=>this.closeAbilityEditModal())}),e.querySelectorAll(".ability-edit-tab-btn").forEach(d=>{d.addEventListener("click",()=>{const u=d.dataset.tab;this.switchAbilityEditTab(u)})});const s=e.querySelector("#save-ability-edit");s&&s.addEventListener("click",()=>this.saveAbilityEdit(a));const n=e.querySelector("#add-condition-btn");n&&n.addEventListener("click",()=>this.addConditionEditor());const o=e.querySelector("#add-target-btn");o&&o.addEventListener("click",()=>this.addTargetEditor());const l=e.querySelector("#add-effect-btn");l&&l.addEventListener("click",()=>this.addEffectEditor());const c=e.querySelector("#add-event-type-btn");c&&c.addEventListener("click",()=>this.addEventTypeEditor());const p=e.querySelector("#add-valid-zone-btn");p&&p.addEventListener("click",()=>this.addValidZoneEditor())};f.prototype.initializeAbilityEditModal=async function(a){try{const e=await this.metadataManager.getAbilityTagOptions(),t=document.getElementById("edit-ability-tag");if(t&&e&&e.forEach(i=>{const r=document.createElement("option");r.value=i.value,r.textContent=i.label,(Array.isArray(a.abilityTag)&&a.abilityTag.includes(i.value)||a.abilityTag===i.value)&&(r.selected=!0),t.appendChild(r)}),a.jsonConfig)try{const i=typeof a.jsonConfig=="string"?JSON.parse(a.jsonConfig):a.jsonConfig;this.loadAbilityConfig(i)}catch(i){console.error("CardEditor: Failed to parse ability config:",i)}}catch(e){console.error("CardEditor: Failed to initialize ability edit modal:",e)}};f.prototype.switchAbilityEditTab=function(a){const e=document.getElementById("ability-edit-modal");if(!e)return;e.querySelectorAll(".ability-edit-tab-btn").forEach(r=>{r.classList.remove("active")}),e.querySelectorAll(".ability-edit-panel").forEach(r=>{r.classList.remove("active")});const t=e.querySelector(`[data-tab="${a}"]`),i=e.querySelector(`#${a}-edit-panel`);t&&i&&(t.classList.add("active"),i.classList.add("active"))};f.prototype.closeAbilityEditModal=function(){const a=document.getElementById("ability-edit-modal");a&&a.remove()};f.prototype.loadAbilityConfig=function(a){console.log("CardEditor: Loading ability config:",a);try{a.trigger&&this.displayTriggerInfo(a.trigger),a.conditions&&Array.isArray(a.conditions)&&this.displayConditionsInfo(a.conditions),a.targets&&Array.isArray(a.targets)&&this.displayTargetsInfo(a.targets),a.effects&&Array.isArray(a.effects)&&this.displayEffectsInfo(a.effects),console.log("CardEditor: Ability config loaded successfully")}catch(e){console.error("CardEditor: Error loading ability config:",e)}};f.prototype.addConditionEditor=function(){console.log("CardEditor: Adding condition editor");const a=document.querySelector("#conditions-list");if(!a)return;const e=a.querySelector(".conditions-placeholder");e&&e.remove();const t="condition_"+Math.random().toString(36).substr(2,9),i=`
        <div class="condition-item" data-condition-id="${t}">
            <div class="condition-item-header">
                <div class="condition-item-title">条件表达式</div>
                <div class="condition-item-actions">
                    <button class="btn-icon remove-condition-btn" data-condition-id="${t}" title="移除条件">
                        <span>🗑️</span>
                    </button>
                </div>
            </div>
            <div class="condition-item-content">
                <div class="form-group">
                    <label class="form-label">条件表达式</label>
                    <div class="condition-expression-editor" id="condition-expr-${t}">
                        <div class="loading-spinner"></div>
                        <p>正在初始化DSL编辑器...</p>
                    </div>
                </div>
                <div class="form-group">
                    <label class="checkbox-label">
                        <input type="checkbox" class="form-checkbox condition-enabled" checked>
                        <span>启用条件</span>
                    </label>
                </div>
            </div>
        </div>
    `;a.insertAdjacentHTML("beforeend",i);const r=a.querySelector(`[data-condition-id="${t}"].remove-condition-btn`);r&&r.addEventListener("click",()=>{const s=a.querySelector(`[data-condition-id="${t}"].condition-item`);if(s&&confirm("确定要移除这个条件吗？")&&(s.remove(),a.children.length===0)){a.innerHTML=`
                        <div class="conditions-placeholder">
                            <p>暂无条件</p>
                            <button class="btn-primary" id="add-condition-btn">添加条件</button>
                        </div>
                    `;const n=a.querySelector("#add-condition-btn");n&&n.addEventListener("click",()=>this.addConditionEditor())}}),this.initializeConditionDslEditor(t)};f.prototype.initializeConditionDslEditor=async function(a){const e=document.getElementById(`condition-expr-${a}`);if(e)try{console.log("CardEditor: Initializing condition DSL editor for:",a);const t=this.uiRenderer.createValueConfigEditor({name:"conditionExpression",displayName:"条件表达式",typeName:"ValueConfig"},"",i=>{e.dataset.dslExpression=i||"",console.log("CardEditor: Condition expression updated:",i)});e.innerHTML="",e.appendChild(t)}catch(t){console.error("CardEditor: Failed to initialize condition DSL editor:",t),e.innerHTML=`
            <div class="error-message">
                初始化DSL编辑器失败: ${t.message}
            </div>
        `}};f.prototype.addTargetEditor=function(){console.log("CardEditor: Adding target editor");const a=document.querySelector("#targets-list");if(!a)return;const e=a.querySelector(".targets-placeholder");e&&e.remove();const t="target_"+Math.random().toString(36).substr(2,9),i=`target${a.children.length+1}`,r=`
        <div class="target-item" data-target-id="${t}" data-target-key="${i}">
            <div class="target-item-header">
                <div class="target-item-title">目标: ${i}</div>
                <div class="target-item-actions">
                    <button class="btn-icon remove-target-btn" data-target-id="${t}" title="移除目标">
                        <span>🗑️</span>
                    </button>
                </div>
            </div>
            <div class="target-item-content">
                <div class="form-group">
                    <label class="form-label">目标池表达式</label>
                    <div class="target-pool-editor" id="target-pool-${t}">
                        <div class="loading-spinner"></div>
                        <p>正在初始化目标池编辑器...</p>
                    </div>
                </div>
                <div class="form-group">
                    <label class="form-label">失败处理方式</label>
                    <select class="form-select target-failure-handling">
                        <option value="AbortAbility" selected>中止能力</option>
                        <option value="SkipEffect">跳过效果</option>
                        <option value="UseDefault">使用默认值</option>
                    </select>
                </div>
                <div class="form-group">
                    <label class="checkbox-label">
                        <input type="checkbox" class="form-checkbox target-player-choice">
                        <span>需要玩家选择</span>
                    </label>
                </div>
                <div class="player-choice-config" style="display: none;">
                    <div class="form-grid form-grid-2">
                        <div class="form-group">
                            <label class="form-label">最小选择数量</label>
                            <input type="number" class="form-input target-min-count" value="1" min="0">
                        </div>
                        <div class="form-group">
                            <label class="form-label">最大选择数量</label>
                            <input type="number" class="form-input target-max-count" value="1" min="1">
                        </div>
                    </div>
                    <div class="form-group">
                        <label class="form-label">选择提示</label>
                        <input type="text" class="form-input target-prompt" placeholder="请选择目标...">
                    </div>
                </div>
            </div>
        </div>
    `;a.insertAdjacentHTML("beforeend",r);const s=a.querySelector(`[data-target-id="${t}"].remove-target-btn`);s&&s.addEventListener("click",()=>{const l=a.querySelector(`[data-target-id="${t}"].target-item`);if(l&&confirm("确定要移除这个目标吗？")&&(l.remove(),a.children.length===0)){a.innerHTML=`
                        <div class="targets-placeholder">
                            <p>暂无目标</p>
                            <button class="btn-primary" id="add-target-btn">添加目标</button>
                        </div>
                    `;const c=a.querySelector("#add-target-btn");c&&c.addEventListener("click",()=>this.addTargetEditor())}});const n=a.querySelector(`[data-target-id="${t}"] .target-player-choice`),o=a.querySelector(`[data-target-id="${t}"] .player-choice-config`);n&&o&&n.addEventListener("change",()=>{o.style.display=n.checked?"block":"none"}),this.initializeTargetPoolEditor(t)};f.prototype.initializeTargetPoolEditor=async function(a){const e=document.getElementById(`target-pool-${a}`);if(e)try{console.log("CardEditor: Initializing target pool editor for:",a);const t=this.uiRenderer.createValueConfigEditor({name:"sourcePoolExpression",displayName:"目标池表达式",typeName:"ValueConfig"},"battlefield.allUnits",i=>{e.dataset.dslExpression=i||"",console.log("CardEditor: Target pool expression updated:",i)});e.innerHTML="",e.appendChild(t)}catch(t){console.error("CardEditor: Failed to initialize target pool editor:",t),e.innerHTML=`
            <div class="error-message">
                初始化目标池编辑器失败: ${t.message}
            </div>
        `}};f.prototype.addEffectEditor=async function(){console.log("CardEditor: Adding effect editor");try{const a=await this.metadataManager.getEffectTypes();if(!a||a.length===0){alert("暂无可用的效果类型");return}this.showEffectTypeSelectionModal(a)}catch(a){console.error("CardEditor: Failed to load effect types:",a),alert(`加载效果类型失败: ${a.message}`)}};f.prototype.showEffectTypeSelectionModal=function(a){const e=`
        <div class="modal-overlay" id="effect-type-selection-modal">
            <div class="modal-container">
                <div class="modal-header">
                    <h3>选择效果类型</h3>
                    <button class="modal-close-btn" id="close-effect-type-modal">×</button>
                </div>
                <div class="modal-content">
                    <div class="effect-types-grid">
                        ${a.map(t=>`
                            <div class="effect-type-card" data-effect-type="${t.typeName}">
                                <div class="effect-type-header">
                                    <span class="effect-type-name">${t.displayName||t.typeName}</span>
                                </div>
                                <div class="effect-type-description">
                                    ${t.description||"暂无描述"}
                                </div>
                                <div class="effect-type-params">
                                    参数数量: ${t.parameters?t.parameters.length:0}
                                </div>
                            </div>
                        `).join("")}
                    </div>
                </div>
                <div class="modal-footer">
                    <button class="btn-secondary" id="cancel-effect-type-selection">取消</button>
                </div>
            </div>
        </div>
    `;document.body.insertAdjacentHTML("beforeend",e),this.bindEffectTypeSelectionEvents(a)};f.prototype.bindEffectTypeSelectionEvents=function(a){const e=document.getElementById("effect-type-selection-modal");if(!e)return;const t=e.querySelector("#close-effect-type-modal"),i=e.querySelector("#cancel-effect-type-selection");[t,i].forEach(s=>{s&&s.addEventListener("click",()=>{e.remove()})}),e.querySelectorAll(".effect-type-card").forEach(s=>{s.addEventListener("click",()=>{const n=s.dataset.effectType,o=a.find(l=>l.typeName===n);o&&(e.remove(),this.createEffectEditor(o))})})};f.prototype.createEffectEditor=function(a){console.log("CardEditor: Creating effect editor for:",a);const e=document.querySelector("#effects-list");if(!e)return;const t=e.querySelector(".effects-placeholder");t&&t.remove();const i="effect_"+Math.random().toString(36).substr(2,9),r=`
        <div class="effect-item" data-effect-id="${i}" data-effect-type="${a.typeName}">
            <div class="effect-item-header">
                <div class="effect-item-title">${a.displayName||a.typeName}</div>
                <div class="effect-item-actions">
                    <button class="btn-icon remove-effect-btn" data-effect-id="${i}" title="移除效果">
                        <span>🗑️</span>
                    </button>
                </div>
            </div>
            <div class="effect-item-content">
                <div class="effect-parameters-container" id="effect-params-${i}">
                    <div class="loading-spinner"></div>
                    <p>正在加载参数配置...</p>
                </div>
            </div>
        </div>
    `;e.insertAdjacentHTML("beforeend",r);const s=e.querySelector(`[data-effect-id="${i}"].remove-effect-btn`);s&&s.addEventListener("click",()=>{const n=e.querySelector(`[data-effect-id="${i}"].effect-item`);if(n&&confirm("确定要移除这个效果吗？")&&(n.remove(),e.children.length===0)){e.innerHTML=`
                        <div class="effects-placeholder">
                            <p>暂无效果</p>
                            <button class="btn-primary" id="add-effect-btn">添加效果</button>
                        </div>
                    `;const o=e.querySelector("#add-effect-btn");o&&o.addEventListener("click",()=>this.addEffectEditor())}}),this.renderEffectParameters(a,i)};f.prototype.renderEffectParameters=async function(a,e){const t=document.getElementById(`effect-params-${e}`);if(t)try{console.log("CardEditor: Rendering effect parameters for:",a);const i=this.uiRenderer.renderEffectParametersForm(a.typeName,{},r=>{console.log("CardEditor: Effect parameters updated:",r),t.dataset.effectParams=JSON.stringify(r||{})});t.innerHTML="",t.appendChild(i)}catch(i){console.error("CardEditor: Failed to render effect parameters:",i),t.innerHTML=`
            <div class="error-message">
                渲染参数失败: ${i.message}
            </div>
        `}};f.prototype.addEventTypeEditor=async function(){console.log("CardEditor: Adding event type editor");try{const a=await this.metadataManager.getEventTypes();if(!a||a.length===0){alert("暂无可用的事件类型");return}this.showEventTypeSelectionModal(a)}catch(a){console.error("CardEditor: Failed to load event types:",a),alert(`加载事件类型失败: ${a.message}`)}};f.prototype.showEventTypeSelectionModal=function(a){const e=`
        <div class="modal-overlay" id="event-type-selection-modal">
            <div class="modal-container">
                <div class="modal-header">
                    <h3>选择事件类型</h3>
                    <button class="modal-close-btn" id="close-event-type-modal">×</button>
                </div>
                <div class="modal-content">
                    <div class="event-types-grid">
                        ${a.map(t=>`
                            <div class="event-type-card" data-event-type="${t.value}">
                                <div class="event-type-header">
                                    <span class="event-type-name">${t.label}</span>
                                </div>
                                <div class="event-type-description">
                                    ${t.description||"暂无描述"}
                                </div>
                            </div>
                        `).join("")}
                    </div>
                </div>
                <div class="modal-footer">
                    <button class="btn-secondary" id="cancel-event-type-selection">取消</button>
                </div>
            </div>
        </div>
    `;document.body.insertAdjacentHTML("beforeend",e),this.bindEventTypeSelectionEvents(a)};f.prototype.bindEventTypeSelectionEvents=function(a){const e=document.getElementById("event-type-selection-modal");if(!e)return;const t=e.querySelector("#close-event-type-modal"),i=e.querySelector("#cancel-event-type-selection");[t,i].forEach(s=>{s&&s.addEventListener("click",()=>{e.remove()})}),e.querySelectorAll(".event-type-card").forEach(s=>{s.addEventListener("click",()=>{const n=s.dataset.eventType,o=a.find(l=>l.value===n);o&&(e.remove(),this.addEventTypeToTrigger(o))})})};f.prototype.addEventTypeToTrigger=function(a){const e=document.getElementById("event-types-container");if(!e)return;const t=e.querySelector(".event-types-placeholder");t&&t.remove();const i="event_"+Math.random().toString(36).substr(2,9),r=`
        <div class="event-type-item" data-event-id="${i}">
            <span class="event-type-label">${a.label}</span>
            <button class="btn-icon remove-event-type-btn" data-event-id="${i}" title="移除事件类型">
                <span>×</span>
            </button>
        </div>
    `;e.insertAdjacentHTML("beforeend",r);const s=e.querySelector(`[data-event-id="${i}"].remove-event-type-btn`);s&&s.addEventListener("click",()=>{const n=e.querySelector(`[data-event-id="${i}"].event-type-item`);if(n&&confirm("确定要移除这个事件类型吗？")&&(n.remove(),e.children.length===0)){e.innerHTML=`
                        <div class="event-types-placeholder">
                            <p>暂无监听事件</p>
                            <button class="btn-primary" id="add-event-type-btn">添加事件类型</button>
                        </div>
                    `;const o=e.querySelector("#add-event-type-btn");o&&o.addEventListener("click",()=>this.addEventTypeEditor())}})};f.prototype.addValidZoneEditor=async function(){console.log("CardEditor: Adding valid zone editor");try{const a=await this.metadataManager.getCardZones();if(!a||a.length===0){alert("暂无可用的卡牌区域");return}this.showValidZoneSelectionModal(a)}catch(a){console.error("CardEditor: Failed to load card zones:",a),alert(`加载卡牌区域失败: ${a.message}`)}};f.prototype.showValidZoneSelectionModal=function(a){const e=`
        <div class="modal-overlay" id="valid-zone-selection-modal">
            <div class="modal-container">
                <div class="modal-header">
                    <h3>选择有效区域</h3>
                    <button class="modal-close-btn" id="close-valid-zone-modal">×</button>
                </div>
                <div class="modal-content">
                    <div class="valid-zones-grid">
                        ${a.map(t=>`
                            <div class="valid-zone-card" data-zone="${t.value}">
                                <div class="valid-zone-header">
                                    <span class="valid-zone-name">${t.label}</span>
                                </div>
                                <div class="valid-zone-description">
                                    ${t.description||"暂无描述"}
                                </div>
                            </div>
                        `).join("")}
                    </div>
                </div>
                <div class="modal-footer">
                    <button class="btn-secondary" id="cancel-valid-zone-selection">取消</button>
                </div>
            </div>
        </div>
    `;document.body.insertAdjacentHTML("beforeend",e),this.bindValidZoneSelectionEvents(a)};f.prototype.bindValidZoneSelectionEvents=function(a){const e=document.getElementById("valid-zone-selection-modal");if(!e)return;const t=e.querySelector("#close-valid-zone-modal"),i=e.querySelector("#cancel-valid-zone-selection");[t,i].forEach(s=>{s&&s.addEventListener("click",()=>{e.remove()})}),e.querySelectorAll(".valid-zone-card").forEach(s=>{s.addEventListener("click",()=>{const n=s.dataset.zone,o=a.find(l=>l.value===n);o&&(e.remove(),this.addValidZoneToTrigger(o))})})};f.prototype.addValidZoneToTrigger=function(a){const e=document.getElementById("valid-zones-container");if(!e)return;const t=e.querySelector(".valid-zones-placeholder");t&&t.remove();const i="zone_"+Math.random().toString(36).substr(2,9),r=`
        <div class="valid-zone-item" data-zone-id="${i}">
            <span class="valid-zone-label">${a.label}</span>
            <button class="btn-icon remove-valid-zone-btn" data-zone-id="${i}" title="移除有效区域">
                <span>×</span>
            </button>
        </div>
    `;e.insertAdjacentHTML("beforeend",r);const s=e.querySelector(`[data-zone-id="${i}"].remove-valid-zone-btn`);s&&s.addEventListener("click",()=>{const n=e.querySelector(`[data-zone-id="${i}"].valid-zone-item`);if(n&&confirm("确定要移除这个有效区域吗？")&&(n.remove(),e.children.length===0)){e.innerHTML=`
                        <div class="valid-zones-placeholder">
                            <p>所有区域有效</p>
                            <button class="btn-secondary" id="add-valid-zone-btn">限制区域</button>
                        </div>
                    `;const o=e.querySelector("#add-valid-zone-btn");o&&o.addEventListener("click",()=>this.addValidZoneEditor())}})};f.prototype.saveAbilityEdit=async function(a){console.log("CardEditor: Saving ability edit:",a);try{const e=this.buildAbilityConfig(a);console.log("CardEditor: Built ability config:",e);const t=await fetch(`/api/abilities/${a.id}`,{method:"PUT",headers:{"Content-Type":"application/json"},body:JSON.stringify(e)});if(!t.ok)throw new Error(`HTTP ${t.status}: ${t.statusText}`);const i=await t.json();console.log("CardEditor: Ability saved successfully:",i),this.closeAbilityEditModal(),await this.renderAbilitiesList(),alert("能力保存成功！")}catch(e){console.error("CardEditor: Failed to save ability:",e),alert(`保存能力失败: ${e.message}`)}};f.prototype.buildAbilityConfig=function(a){const e=document.getElementById("ability-edit-modal");if(!e)throw new Error("Ability edit modal not found");const t=e.querySelector("#edit-ability-name").value.trim(),i=e.querySelector("#edit-ability-description").value.trim(),r=e.querySelector("#edit-ability-tag").value,s=parseInt(e.querySelector("#edit-ability-priority").value)||0,n=e.querySelector("#edit-ability-active").checked,o=this.buildTriggerConfig(e),l=this.buildConditionsConfig(e),c=this.buildTargetsConfig(e),p=this.buildEffectsConfig(e);return{id:a.id,cardId:this.currentCard.id,name:t,description:i,abilityTag:r,abilityType:a.abilityType||1,priority:s,isActive:n,jsonConfig:JSON.stringify({name:t,description:i,abilityTag:r,type:"Triggered",trigger:o,preConditions:[],conditions:l,targets:c,effects:p,isActive:n,version:"1.0"})}};f.prototype.buildTriggerConfig=function(a){const e=a.querySelector("#trigger-timing").value,t=a.querySelector("#trigger-relation-type").value,i=a.querySelector("#trigger-enabled").checked,r=a.querySelectorAll(".event-type-item"),s=Array.from(r).map(l=>{const c=l.querySelector(".event-type-label").textContent;return this.convertEventTypeLabelToEnum(c)}),n=a.querySelectorAll(".valid-zone-item"),o=Array.from(n).map(l=>{const c=l.querySelector(".valid-zone-label").textContent;return this.convertCardZoneLabelToEnum(c)});return{id:"trigger_"+Math.random().toString(36).substr(2,9),eventTypes:s,timing:e,validZones:o,relationType:t,isEnabled:i}};f.prototype.convertEventTypeLabelToEnum=function(a){try{const t=this.metadataManager.getEventTypes().find(i=>i.label===a);return t?t.value:a}catch(e){return console.error("CardEditor: Failed to convert event type label:",e),a}};f.prototype.convertCardZoneLabelToEnum=function(a){try{const t=this.metadataManager.getCardZones().find(i=>i.label===a);return t?t.value:a}catch(e){return console.error("CardEditor: Failed to convert card zone label:",e),a}};f.prototype.buildConditionsConfig=function(a){const e=a.querySelectorAll(".condition-item");return Array.from(e).map(t=>{const i=t.dataset.conditionId,r=t.querySelector(".condition-enabled").checked,s=t.querySelector(`#condition-expr-${i}`),n=this.extractDslEditorValue(s)||"true";return{id:i,expression:n,isEnabled:r}})};f.prototype.buildTargetsConfig=function(a){const e=a.querySelectorAll(".target-item"),t={};return Array.from(e).forEach((i,r)=>{const s=i.dataset.targetKey||`target${r+1}`,n=i.querySelector(".target-failure-handling").value,o=i.querySelector(".target-player-choice").checked,l=i.querySelector(`#target-pool-${i.dataset.targetId}`),c=this.extractDslEditorValue(l)||"battlefield.allUnits";let p=null;if(o){const d=parseInt(i.querySelector(".target-min-count").value)||1,u=parseInt(i.querySelector(".target-max-count").value)||1;p={isRequired:!0,prompt:i.querySelector(".target-prompt").value||"请选择目标...",canCancel:!1,autoSelect:!1,minCount:d,maxCount:u}}t[s]={query:{sourcePoolExpression:c,operations:[]},onFailure:this.convertFailureHandlingToEnum(n),playerChoiceOptions:p}}),t};f.prototype.convertFailureHandlingToEnum=function(a){return{AbortAbility:"AbortAbility",SkipEffect:"SkipEffect",UseDefault:"UseDefault"}[a]||"AbortAbility"};f.prototype.buildEffectsConfig=function(a){const e=a.querySelectorAll(".effect-item");return Array.from(e).map((t,i)=>{var o;const r=t.dataset.effectType||((o=t.querySelector(".effect-item-title"))==null?void 0:o.textContent)||"",s=t.querySelector(".effect-parameters-container"),n=this.extractEffectParameters(s);return{type:r,executionOrder:i,parameters:n,targetQuery:null,outputs:null}})};f.prototype.extractEffectParameters=function(a){if(!a)return{};if(a.dataset&&a.dataset.effectParams)try{const i=JSON.parse(a.dataset.effectParams);if(i&&typeof i=="object")return i}catch{}const e={};return a.querySelectorAll(".form-group").forEach(i=>{var u,m;const r=i.dataset.paramName||((m=(u=i.querySelector(".form-label"))==null?void 0:u.textContent)==null?void 0:m.trim());if(!r)return;let s=null;const n=i.querySelector('.form-input[type="text"]'),o=i.querySelector('.form-input[type="number"]'),l=i.querySelector(".form-checkbox"),c=i.querySelector(".form-select"),p=i.querySelector(".form-textarea"),d=i.querySelector(".dsl-editor");if(n)s={type:"Literal",value:n.value};else if(o)s={type:"Literal",value:parseFloat(o.value)||0};else if(l)s={type:"Literal",value:l.checked};else if(c)s={type:"Literal",value:c.value};else if(p)s={type:"Literal",value:p.value};else if(i.querySelector(".value-config-editor")){let h=null;if(i.dataset&&i.dataset.valueConfig)try{h=JSON.parse(i.dataset.valueConfig)}catch{h=null}if(!h){const y=i._valueConfig||i.querySelector(".value-config-editor")._valueConfig;y&&(h=y)}h&&(s=h)}else d&&(s={type:"Expression",expression:this.extractDslEditorValue(d)});s&&(e[r]=s)}),e};f.prototype.extractDslEditorValue=function(a){if(!a)return null;if(a.dataset&&typeof a.dataset.dslExpression<"u")return a.dataset.dslExpression;if(a._dslEditor&&typeof a._dslEditor.expression=="string")return a._dslEditor.expression;const e=a.querySelector(".dsl-preview-content code");return e?e.textContent:null};f.prototype.loadKeywordOptions=async function(a){try{const e=await this.metadataManager.getEnum("CardKeyword");a.innerHTML='<option value="">选择关键字</option>',Object.entries(e).forEach(([t,i])=>{if(t!=="None"){const r=document.createElement("option");r.value=t,r.textContent=i,a.appendChild(r)}})}catch(e){console.error("Failed to load keyword options:",e);const t={Storm:"疾走",Ward:"守护",Bane:"必杀",Rush:"突进",Ambush:"潜行",Drain:"吸血"};a.innerHTML='<option value="">选择关键字</option>',Object.entries(t).forEach(([i,r])=>{const s=document.createElement("option");s.value=i,s.textContent=r,a.appendChild(s)})}};f.prototype.displayTriggerInfo=function(a){const e=document.getElementById("trigger-edit-panel");if(!e)return;const t=e.querySelector("#trigger-event-type");t&&a.eventTypes&&a.eventTypes.length>0&&(t.innerHTML='<option value="">选择事件类型</option>',a.eventTypes.forEach(n=>{const o=document.createElement("option");o.value=n,o.textContent=n,o.selected=!0,t.appendChild(o)}));const i=e.querySelector("#trigger-relation-type");if(i&&a.relationType){i.innerHTML='<option value="">选择关系类型</option>';const n=document.createElement("option");n.value=a.relationType,n.textContent=a.relationType,n.selected=!0,i.appendChild(n)}const r=document.createElement("div");r.className="trigger-config-summary",r.innerHTML=`
        <div class="config-summary-item">
            <strong>已配置事件类型:</strong> ${a.eventTypes?a.eventTypes.join(", "):"未设置"}
        </div>
        <div class="config-summary-item">
            <strong>已配置关系类型:</strong> ${a.relationType||"未设置"}
        </div>
    `;const s=e.querySelector(".trigger-config-summary");s&&s.remove(),e.appendChild(r)};f.prototype.displayConditionsInfo=function(a){const e=document.getElementById("conditions-edit-panel");if(!e)return;const t=e.querySelector("#conditions-list");if(!t)return;const i=t.querySelector(".conditions-placeholder");if(i&&i.remove(),t.innerHTML="",a.length===0){t.innerHTML=`
            <div class="conditions-placeholder">
                <p>暂无条件</p>
                <button class="btn-primary" id="add-condition-btn">添加条件</button>
            </div>
        `;return}a.forEach((s,n)=>{const o=`condition_${n}`,l=document.createElement("div");l.className="condition-item",l.setAttribute("data-condition-id",o),l.innerHTML=`
            <div class="condition-item-header">
                <div class="condition-item-title">条件 ${n+1}</div>
                <div class="condition-item-actions">
                    <button class="btn-icon remove-condition-btn" data-condition-id="${o}" title="移除条件">
                        <span>🗑️</span>
                    </button>
                </div>
            </div>
            <div class="condition-item-content">
                <div class="form-group">
                    <label>条件类型:</label>
                    <input type="text" class="form-control" value="${s.type||""}" readonly>
                </div>
                <div class="form-group">
                    <label>条件配置:</label>
                    <textarea class="form-control" rows="4" readonly>${JSON.stringify(s,null,2)}</textarea>
                </div>
            </div>
        `,t.appendChild(l)});const r=document.createElement("div");r.className="add-condition-container",r.innerHTML='<button class="btn-primary" id="add-condition-btn">添加条件</button>',t.appendChild(r)};f.prototype.displayTargetsInfo=function(a){const e=document.getElementById("targets-edit-panel");if(!e)return;const t=e.querySelector("#targets-list");if(!t)return;const i=t.querySelector(".targets-placeholder");if(i&&i.remove(),t.innerHTML="",a.length===0){t.innerHTML=`
            <div class="targets-placeholder">
                <p>暂无目标</p>
                <button class="btn-primary" id="add-target-btn">添加目标</button>
            </div>
        `;return}a.forEach((s,n)=>{const o=`target_${n}`,l=`target${n+1}`,c=document.createElement("div");c.className="target-item",c.setAttribute("data-target-id",o),c.setAttribute("data-target-key",l),c.innerHTML=`
            <div class="target-item-header">
                <div class="target-item-title">目标 ${n+1}</div>
                <div class="target-item-actions">
                    <button class="btn-icon remove-target-btn" data-target-id="${o}" title="移除目标">
                        <span>🗑️</span>
                    </button>
                </div>
            </div>
            <div class="target-item-content">
                <div class="form-group">
                    <label>选择器类型:</label>
                    <select class="form-control target-selector-type">
                        <option value="">选择选择器类型</option>
                        <option value="Filtered" ${s.selectorType==="Filtered"?"selected":""}>Filtered</option>
                        <option value="All" ${s.selectorType==="All"?"selected":""}>All</option>
                        <option value="Random" ${s.selectorType==="Random"?"selected":""}>Random</option>
                    </select>
                </div>
                <div class="form-group">
                    <label>查询配置:</label>
                    <textarea class="form-control target-query-config" rows="4">${JSON.stringify(s.query||{},null,2)}</textarea>
                </div>
                <div class="form-group">
                    <label>完整目标配置:</label>
                    <textarea class="form-control" rows="3" readonly>${JSON.stringify(s,null,2)}</textarea>
                </div>
            </div>
        `,t.appendChild(c)});const r=document.createElement("div");r.className="add-target-container",r.innerHTML='<button class="btn-primary" id="add-target-btn">添加目标</button>',t.appendChild(r)};f.prototype.displayEffectsInfo=function(a){const e=document.getElementById("effects-edit-panel");if(!e)return;const t=e.querySelector("#effects-list");if(!t)return;const i=t.querySelector(".effects-placeholder");if(i&&i.remove(),t.innerHTML="",a.length===0){t.innerHTML=`
            <div class="effects-placeholder">
                <p>暂无效果</p>
                <button class="btn-primary" id="add-effect-btn">添加效果</button>
            </div>
        `;return}a.forEach((s,n)=>{const o=`effect_${n}`,l=document.createElement("div");l.className="effect-item",l.setAttribute("data-effect-id",o),l.innerHTML=`
            <div class="effect-item-header">
                <div class="effect-item-title">效果 ${n+1}</div>
                <div class="effect-item-actions">
                    <button class="btn-icon remove-effect-btn" data-effect-id="${o}" title="移除效果">
                        <span>🗑️</span>
                    </button>
                </div>
            </div>
            <div class="effect-item-content">
                <div class="form-group">
                    <label>效果类型:</label>
                    <select class="form-control effect-type-select">
                        <option value="">选择效果类型</option>
                        <option value="Draw" ${s.type==="Draw"?"selected":""}>Draw (抽牌)</option>
                        <option value="Damage" ${s.type==="Damage"?"selected":""}>Damage (伤害)</option>
                        <option value="Heal" ${s.type==="Heal"?"selected":""}>Heal (治疗)</option>
                        <option value="AddBuff" ${s.type==="AddBuff"?"selected":""}>AddBuff (添加增益)</option>
                        <option value="Summon" ${s.type==="Summon"?"selected":""}>Summon (召唤)</option>
                    </select>
                </div>
                <div class="form-group">
                    <label>效果参数:</label>
                    <textarea class="form-control effect-parameters" rows="4">${JSON.stringify(s.parameters||{},null,2)}</textarea>
                </div>
                <div class="form-group">
                    <label>完整效果配置:</label>
                    <textarea class="form-control" rows="3" readonly>${JSON.stringify(s,null,2)}</textarea>
                </div>
            </div>
        `,t.appendChild(l)});const r=document.createElement("div");r.className="add-effect-container",r.innerHTML='<button class="btn-primary" id="add-effect-btn">添加效果</button>',t.appendChild(r)};export{ve as cleanupCardEditor,ge as getCardEditor,ye as initializeCardEditor};
