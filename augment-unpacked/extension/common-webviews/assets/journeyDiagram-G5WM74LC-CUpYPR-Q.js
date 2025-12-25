import{a as pt,g as st,f as ut,d as yt}from"./chunk-ASOPGD6M-DUT4ngJu.js";import{_ as s,g as dt,s as ft,a as mt,b as gt,o as xt,p as kt,c as I,d as J,e as bt,x as _t}from"./AugmentMessage-FtcicXdY.js";import{d as K}from"./arc-5iJ2Fwws.js";import"./SpinnerAugment-BY2Lraps.js";import"./CalloutAugment-BPYQDfw6.js";import"./CardAugment-BaFOe6RO.js";import"./IconButtonAugment-B8y0FMb_.js";import"./index-78K9HN9C.js";import"./async-messaging-BnOo7nYC.js";import"./message-broker-BauNv3yh.js";import"./types-CGlLNakm.js";import"./focusTrapStack-CAuiPHBF.js";import"./isObjectLike-DuRpH5zX.js";import"./BaseTextInput-C64uUToe.js";import"./index-CoHT-xzg.js";import"./diff-operations-TFiyZoZ6.js";import"./svelte-component-DfqKRK9G.js";import"./Filespan-7QsRDmJG.js";import"./toggleHighContrast-Cb9MCs64.js";import"./preload-helper-Dv6uf1Os.js";import"./index-BPm23rLE.js";import"./keypress-DD1aQVr0.js";import"./folder-opened-B-IBFaHN.js";import"./await-CoczQRp_.js";import"./OpenFileButton-DL6LWlGL.js";import"./chat-model-context-C9JFkoqk.js";import"./index-C4gKbsWy.js";import"./remote-agents-client-XI3B217g.js";import"./ra-diff-ops-model-3WeVBTrk.js";import"./TextAreaAugment-BkN7aH6v.js";import"./ButtonAugment-BoJU5mQc.js";import"./CollapseButtonAugment-CllkyJKm.js";import"./trash-can-Col-hlUX.js";import"./MaterialIcon-CKuUXxrb.js";import"./feedback-rating-BX5Icwas.js";import"./copy-DdR1jezc.js";import"./ellipsis-BVNflcFA.js";import"./LanguageIcon-BvE8QmB9.js";import"./chevron-down-CVLGkBkY.js";import"./index-ALhsmmIa.js";import"./augment-logo-DFXa-EF4.js";import"./pen-to-square-BKF2K8ly.js";var Y=function(){var e=s(function(n,u,r,p){for(r=r||{},p=n.length;p--;r[n[p]]=u);return r},"o"),t=[6,8,10,11,12,14,16,17,18],a=[1,9],l=[1,10],i=[1,11],o=[1,12],h=[1,13],d=[1,14],y={trace:s(function(){},"trace"),yy:{},symbols_:{error:2,start:3,journey:4,document:5,EOF:6,line:7,SPACE:8,statement:9,NEWLINE:10,title:11,acc_title:12,acc_title_value:13,acc_descr:14,acc_descr_value:15,acc_descr_multiline_value:16,section:17,taskName:18,taskData:19,$accept:0,$end:1},terminals_:{2:"error",4:"journey",6:"EOF",8:"SPACE",10:"NEWLINE",11:"title",12:"acc_title",13:"acc_title_value",14:"acc_descr",15:"acc_descr_value",16:"acc_descr_multiline_value",17:"section",18:"taskName",19:"taskData"},productions_:[0,[3,3],[5,0],[5,2],[7,2],[7,1],[7,1],[7,1],[9,1],[9,2],[9,2],[9,1],[9,1],[9,2]],performAction:s(function(n,u,r,p,f,c,$){var x=c.length-1;switch(f){case 1:return c[x-1];case 2:case 6:case 7:this.$=[];break;case 3:c[x-1].push(c[x]),this.$=c[x-1];break;case 4:case 5:this.$=c[x];break;case 8:p.setDiagramTitle(c[x].substr(6)),this.$=c[x].substr(6);break;case 9:this.$=c[x].trim(),p.setAccTitle(this.$);break;case 10:case 11:this.$=c[x].trim(),p.setAccDescription(this.$);break;case 12:p.addSection(c[x].substr(8)),this.$=c[x].substr(8);break;case 13:p.addTask(c[x-1],c[x]),this.$="task"}},"anonymous"),table:[{3:1,4:[1,2]},{1:[3]},e(t,[2,2],{5:3}),{6:[1,4],7:5,8:[1,6],9:7,10:[1,8],11:a,12:l,14:i,16:o,17:h,18:d},e(t,[2,7],{1:[2,1]}),e(t,[2,3]),{9:15,11:a,12:l,14:i,16:o,17:h,18:d},e(t,[2,5]),e(t,[2,6]),e(t,[2,8]),{13:[1,16]},{15:[1,17]},e(t,[2,11]),e(t,[2,12]),{19:[1,18]},e(t,[2,4]),e(t,[2,9]),e(t,[2,10]),e(t,[2,13])],defaultActions:{},parseError:s(function(n,u){if(!u.recoverable){var r=new Error(n);throw r.hash=u,r}this.trace(n)},"parseError"),parse:s(function(n){var u=this,r=[0],p=[],f=[null],c=[],$=this.table,x="",O=0,X=0,lt=c.slice.call(arguments,1),k=Object.create(this.lexer),S={yy:{}};for(var N in this.yy)Object.prototype.hasOwnProperty.call(this.yy,N)&&(S.yy[N]=this.yy[N]);k.setInput(n,S.yy),S.yy.lexer=k,S.yy.parser=this,k.yylloc===void 0&&(k.yylloc={});var R=k.yylloc;c.push(R);var ht=k.options&&k.options.ranges;function G(){var v;return typeof(v=p.pop()||k.lex()||1)!="number"&&(v instanceof Array&&(v=(p=v).pop()),v=u.symbols_[v]||v),v}typeof S.yy.parseError=="function"?this.parseError=S.yy.parseError:this.parseError=Object.getPrototypeOf(this).parseError,s(function(v){r.length=r.length-2*v,f.length=f.length-v,c.length=c.length-v},"popStack"),s(G,"lex");for(var b,E,_,U,F,M,H,B,P={};;){if(E=r[r.length-1],this.defaultActions[E]?_=this.defaultActions[E]:(b==null&&(b=G()),_=$[E]&&$[E][b]),_===void 0||!_.length||!_[0]){var Z="";for(F in B=[],$[E])this.terminals_[F]&&F>2&&B.push("'"+this.terminals_[F]+"'");Z=k.showPosition?"Parse error on line "+(O+1)+`:
`+k.showPosition()+`
Expecting `+B.join(", ")+", got '"+(this.terminals_[b]||b)+"'":"Parse error on line "+(O+1)+": Unexpected "+(b==1?"end of input":"'"+(this.terminals_[b]||b)+"'"),this.parseError(Z,{text:k.match,token:this.terminals_[b]||b,line:k.yylineno,loc:R,expected:B})}if(_[0]instanceof Array&&_.length>1)throw new Error("Parse Error: multiple actions possible at state: "+E+", token: "+b);switch(_[0]){case 1:r.push(b),f.push(k.yytext),c.push(k.yylloc),r.push(_[1]),b=null,X=k.yyleng,x=k.yytext,O=k.yylineno,R=k.yylloc;break;case 2:if(M=this.productions_[_[1]][1],P.$=f[f.length-M],P._$={first_line:c[c.length-(M||1)].first_line,last_line:c[c.length-1].last_line,first_column:c[c.length-(M||1)].first_column,last_column:c[c.length-1].last_column},ht&&(P._$.range=[c[c.length-(M||1)].range[0],c[c.length-1].range[1]]),(U=this.performAction.apply(P,[x,X,O,S.yy,_[1],f,c].concat(lt)))!==void 0)return U;M&&(r=r.slice(0,-1*M*2),f=f.slice(0,-1*M),c=c.slice(0,-1*M)),r.push(this.productions_[_[1]][0]),f.push(P.$),c.push(P._$),H=$[r[r.length-2]][r[r.length-1]],r.push(H);break;case 3:return!0}}return!0},"parse")},g=function(){return{EOF:1,parseError:s(function(n,u){if(!this.yy.parser)throw new Error(n);this.yy.parser.parseError(n,u)},"parseError"),setInput:s(function(n,u){return this.yy=u||this.yy||{},this._input=n,this._more=this._backtrack=this.done=!1,this.yylineno=this.yyleng=0,this.yytext=this.matched=this.match="",this.conditionStack=["INITIAL"],this.yylloc={first_line:1,first_column:0,last_line:1,last_column:0},this.options.ranges&&(this.yylloc.range=[0,0]),this.offset=0,this},"setInput"),input:s(function(){var n=this._input[0];return this.yytext+=n,this.yyleng++,this.offset++,this.match+=n,this.matched+=n,n.match(/(?:\r\n?|\n).*/g)?(this.yylineno++,this.yylloc.last_line++):this.yylloc.last_column++,this.options.ranges&&this.yylloc.range[1]++,this._input=this._input.slice(1),n},"input"),unput:s(function(n){var u=n.length,r=n.split(/(?:\r\n?|\n)/g);this._input=n+this._input,this.yytext=this.yytext.substr(0,this.yytext.length-u),this.offset-=u;var p=this.match.split(/(?:\r\n?|\n)/g);this.match=this.match.substr(0,this.match.length-1),this.matched=this.matched.substr(0,this.matched.length-1),r.length-1&&(this.yylineno-=r.length-1);var f=this.yylloc.range;return this.yylloc={first_line:this.yylloc.first_line,last_line:this.yylineno+1,first_column:this.yylloc.first_column,last_column:r?(r.length===p.length?this.yylloc.first_column:0)+p[p.length-r.length].length-r[0].length:this.yylloc.first_column-u},this.options.ranges&&(this.yylloc.range=[f[0],f[0]+this.yyleng-u]),this.yyleng=this.yytext.length,this},"unput"),more:s(function(){return this._more=!0,this},"more"),reject:s(function(){return this.options.backtrack_lexer?(this._backtrack=!0,this):this.parseError("Lexical error on line "+(this.yylineno+1)+`. You can only invoke reject() in the lexer when the lexer is of the backtracking persuasion (options.backtrack_lexer = true).
`+this.showPosition(),{text:"",token:null,line:this.yylineno})},"reject"),less:s(function(n){this.unput(this.match.slice(n))},"less"),pastInput:s(function(){var n=this.matched.substr(0,this.matched.length-this.match.length);return(n.length>20?"...":"")+n.substr(-20).replace(/\n/g,"")},"pastInput"),upcomingInput:s(function(){var n=this.match;return n.length<20&&(n+=this._input.substr(0,20-n.length)),(n.substr(0,20)+(n.length>20?"...":"")).replace(/\n/g,"")},"upcomingInput"),showPosition:s(function(){var n=this.pastInput(),u=new Array(n.length+1).join("-");return n+this.upcomingInput()+`
`+u+"^"},"showPosition"),test_match:s(function(n,u){var r,p,f;if(this.options.backtrack_lexer&&(f={yylineno:this.yylineno,yylloc:{first_line:this.yylloc.first_line,last_line:this.last_line,first_column:this.yylloc.first_column,last_column:this.yylloc.last_column},yytext:this.yytext,match:this.match,matches:this.matches,matched:this.matched,yyleng:this.yyleng,offset:this.offset,_more:this._more,_input:this._input,yy:this.yy,conditionStack:this.conditionStack.slice(0),done:this.done},this.options.ranges&&(f.yylloc.range=this.yylloc.range.slice(0))),(p=n[0].match(/(?:\r\n?|\n).*/g))&&(this.yylineno+=p.length),this.yylloc={first_line:this.yylloc.last_line,last_line:this.yylineno+1,first_column:this.yylloc.last_column,last_column:p?p[p.length-1].length-p[p.length-1].match(/\r?\n?/)[0].length:this.yylloc.last_column+n[0].length},this.yytext+=n[0],this.match+=n[0],this.matches=n,this.yyleng=this.yytext.length,this.options.ranges&&(this.yylloc.range=[this.offset,this.offset+=this.yyleng]),this._more=!1,this._backtrack=!1,this._input=this._input.slice(n[0].length),this.matched+=n[0],r=this.performAction.call(this,this.yy,this,u,this.conditionStack[this.conditionStack.length-1]),this.done&&this._input&&(this.done=!1),r)return r;if(this._backtrack){for(var c in f)this[c]=f[c];return!1}return!1},"test_match"),next:s(function(){if(this.done)return this.EOF;var n,u,r,p;this._input||(this.done=!0),this._more||(this.yytext="",this.match="");for(var f=this._currentRules(),c=0;c<f.length;c++)if((r=this._input.match(this.rules[f[c]]))&&(!u||r[0].length>u[0].length)){if(u=r,p=c,this.options.backtrack_lexer){if((n=this.test_match(r,f[c]))!==!1)return n;if(this._backtrack){u=!1;continue}return!1}if(!this.options.flex)break}return u?(n=this.test_match(u,f[p]))!==!1&&n:this._input===""?this.EOF:this.parseError("Lexical error on line "+(this.yylineno+1)+`. Unrecognized text.
`+this.showPosition(),{text:"",token:null,line:this.yylineno})},"next"),lex:s(function(){var n=this.next();return n||this.lex()},"lex"),begin:s(function(n){this.conditionStack.push(n)},"begin"),popState:s(function(){return this.conditionStack.length-1>0?this.conditionStack.pop():this.conditionStack[0]},"popState"),_currentRules:s(function(){return this.conditionStack.length&&this.conditionStack[this.conditionStack.length-1]?this.conditions[this.conditionStack[this.conditionStack.length-1]].rules:this.conditions.INITIAL.rules},"_currentRules"),topState:s(function(n){return(n=this.conditionStack.length-1-Math.abs(n||0))>=0?this.conditionStack[n]:"INITIAL"},"topState"),pushState:s(function(n){this.begin(n)},"pushState"),stateStackSize:s(function(){return this.conditionStack.length},"stateStackSize"),options:{"case-insensitive":!0},performAction:s(function(n,u,r,p){switch(r){case 0:case 1:case 3:case 4:break;case 2:return 10;case 5:return 4;case 6:return 11;case 7:return this.begin("acc_title"),12;case 8:return this.popState(),"acc_title_value";case 9:return this.begin("acc_descr"),14;case 10:return this.popState(),"acc_descr_value";case 11:this.begin("acc_descr_multiline");break;case 12:this.popState();break;case 13:return"acc_descr_multiline_value";case 14:return 17;case 15:return 18;case 16:return 19;case 17:return":";case 18:return 6;case 19:return"INVALID"}},"anonymous"),rules:[/^(?:%(?!\{)[^\n]*)/i,/^(?:[^\}]%%[^\n]*)/i,/^(?:[\n]+)/i,/^(?:\s+)/i,/^(?:#[^\n]*)/i,/^(?:journey\b)/i,/^(?:title\s[^#\n;]+)/i,/^(?:accTitle\s*:\s*)/i,/^(?:(?!\n||)*[^\n]*)/i,/^(?:accDescr\s*:\s*)/i,/^(?:(?!\n||)*[^\n]*)/i,/^(?:accDescr\s*\{\s*)/i,/^(?:[\}])/i,/^(?:[^\}]*)/i,/^(?:section\s[^#:\n;]+)/i,/^(?:[^#:\n;]+)/i,/^(?::[^#\n;]+)/i,/^(?::)/i,/^(?:$)/i,/^(?:.)/i],conditions:{acc_descr_multiline:{rules:[12,13],inclusive:!1},acc_descr:{rules:[10],inclusive:!1},acc_title:{rules:[8],inclusive:!1},INITIAL:{rules:[0,1,2,3,4,5,6,7,9,11,14,15,16,17,18,19],inclusive:!0}}}}();function m(){this.yy={}}return y.lexer=g,s(m,"Parser"),m.prototype=y,y.Parser=m,new m}();Y.parser=Y;var vt=Y,C="",q=[],j=[],V=[],wt=s(function(){q.length=0,j.length=0,C="",V.length=0,_t()},"clear"),$t=s(function(e){C=e,q.push(e)},"addSection"),Mt=s(function(){return q},"getSections"),Tt=s(function(){let e=Q(),t=0;for(;!e&&t<100;)e=Q(),t++;return j.push(...V),j},"getTasks"),St=s(function(){const e=[];return j.forEach(t=>{t.people&&e.push(...t.people)}),[...new Set(e)].sort()},"updateActors"),Et=s(function(e,t){const a=t.substr(1).split(":");let l=0,i=[];a.length===1?(l=Number(a[0]),i=[]):(l=Number(a[0]),i=a[1].split(","));const o=i.map(d=>d.trim()),h={section:C,type:C,people:o,task:e,score:l};V.push(h)},"addTask"),At=s(function(e){const t={section:C,type:C,description:e,task:e,classes:[]};j.push(t)},"addTaskOrg"),Q=s(function(){const e=s(function(a){return V[a].processed},"compileTask");let t=!0;for(const[a,l]of V.entries())e(a),t=t&&l.processed;return t},"compileTasks"),It=s(function(){return St()},"getActors"),tt={getConfig:s(()=>I().journey,"getConfig"),clear:wt,setDiagramTitle:kt,getDiagramTitle:xt,setAccTitle:gt,getAccTitle:mt,setAccDescription:ft,getAccDescription:dt,addSection:$t,getSections:Mt,getTasks:Tt,addTask:Et,addTaskOrg:At,getActors:It},Pt=s(e=>`.label {
    font-family: 'trebuchet ms', verdana, arial, sans-serif;
    font-family: var(--mermaid-font-family);
    color: ${e.textColor};
  }
  .mouth {
    stroke: #666;
  }

  line {
    stroke: ${e.textColor}
  }

  .legend {
    fill: ${e.textColor};
  }

  .label text {
    fill: #333;
  }
  .label {
    color: ${e.textColor}
  }

  .face {
    ${e.faceColor?`fill: ${e.faceColor}`:"fill: #FFF8DC"};
    stroke: #999;
  }

  .node rect,
  .node circle,
  .node ellipse,
  .node polygon,
  .node path {
    fill: ${e.mainBkg};
    stroke: ${e.nodeBorder};
    stroke-width: 1px;
  }

  .node .label {
    text-align: center;
  }
  .node.clickable {
    cursor: pointer;
  }

  .arrowheadPath {
    fill: ${e.arrowheadColor};
  }

  .edgePath .path {
    stroke: ${e.lineColor};
    stroke-width: 1.5px;
  }

  .flowchart-link {
    stroke: ${e.lineColor};
    fill: none;
  }

  .edgeLabel {
    background-color: ${e.edgeLabelBackground};
    rect {
      opacity: 0.5;
    }
    text-align: center;
  }

  .cluster rect {
  }

  .cluster text {
    fill: ${e.titleColor};
  }

  div.mermaidTooltip {
    position: absolute;
    text-align: center;
    max-width: 200px;
    padding: 2px;
    font-family: 'trebuchet ms', verdana, arial, sans-serif;
    font-family: var(--mermaid-font-family);
    font-size: 12px;
    background: ${e.tertiaryColor};
    border: 1px solid ${e.border2};
    border-radius: 2px;
    pointer-events: none;
    z-index: 100;
  }

  .task-type-0, .section-type-0  {
    ${e.fillType0?`fill: ${e.fillType0}`:""};
  }
  .task-type-1, .section-type-1  {
    ${e.fillType0?`fill: ${e.fillType1}`:""};
  }
  .task-type-2, .section-type-2  {
    ${e.fillType0?`fill: ${e.fillType2}`:""};
  }
  .task-type-3, .section-type-3  {
    ${e.fillType0?`fill: ${e.fillType3}`:""};
  }
  .task-type-4, .section-type-4  {
    ${e.fillType0?`fill: ${e.fillType4}`:""};
  }
  .task-type-5, .section-type-5  {
    ${e.fillType0?`fill: ${e.fillType5}`:""};
  }
  .task-type-6, .section-type-6  {
    ${e.fillType0?`fill: ${e.fillType6}`:""};
  }
  .task-type-7, .section-type-7  {
    ${e.fillType0?`fill: ${e.fillType7}`:""};
  }

  .actor-0 {
    ${e.actor0?`fill: ${e.actor0}`:""};
  }
  .actor-1 {
    ${e.actor1?`fill: ${e.actor1}`:""};
  }
  .actor-2 {
    ${e.actor2?`fill: ${e.actor2}`:""};
  }
  .actor-3 {
    ${e.actor3?`fill: ${e.actor3}`:""};
  }
  .actor-4 {
    ${e.actor4?`fill: ${e.actor4}`:""};
  }
  .actor-5 {
    ${e.actor5?`fill: ${e.actor5}`:""};
  }
`,"getStyles"),W=s(function(e,t){return yt(e,t)},"drawRect"),Ct=s(function(e,t){const l=e.append("circle").attr("cx",t.cx).attr("cy",t.cy).attr("class","face").attr("r",15).attr("stroke-width",2).attr("overflow","visible"),i=e.append("g");function o(y){const g=K().startAngle(Math.PI/2).endAngle(Math.PI/2*3).innerRadius(7.5).outerRadius(6.8181818181818175);y.append("path").attr("class","mouth").attr("d",g).attr("transform","translate("+t.cx+","+(t.cy+2)+")")}function h(y){const g=K().startAngle(3*Math.PI/2).endAngle(Math.PI/2*5).innerRadius(7.5).outerRadius(6.8181818181818175);y.append("path").attr("class","mouth").attr("d",g).attr("transform","translate("+t.cx+","+(t.cy+7)+")")}function d(y){y.append("line").attr("class","mouth").attr("stroke",2).attr("x1",t.cx-5).attr("y1",t.cy+7).attr("x2",t.cx+5).attr("y2",t.cy+7).attr("class","mouth").attr("stroke-width","1px").attr("stroke","#666")}return i.append("circle").attr("cx",t.cx-5).attr("cy",t.cy-5).attr("r",1.5).attr("stroke-width",2).attr("fill","#666").attr("stroke","#666"),i.append("circle").attr("cx",t.cx+5).attr("cy",t.cy-5).attr("r",1.5).attr("stroke-width",2).attr("fill","#666").attr("stroke","#666"),s(o,"smile"),s(h,"sad"),s(d,"ambivalent"),t.score>3?o(i):t.score<3?h(i):d(i),l},"drawFace"),rt=s(function(e,t){const a=e.append("circle");return a.attr("cx",t.cx),a.attr("cy",t.cy),a.attr("class","actor-"+t.pos),a.attr("fill",t.fill),a.attr("stroke",t.stroke),a.attr("r",t.r),a.class!==void 0&&a.attr("class",a.class),t.title!==void 0&&a.append("title").text(t.title),a},"drawCircle"),at=s(function(e,t){return ut(e,t)},"drawText"),jt=s(function(e,t){function a(i,o,h,d,y){return i+","+o+" "+(i+h)+","+o+" "+(i+h)+","+(o+d-y)+" "+(i+h-1.2*y)+","+(o+d)+" "+i+","+(o+d)}s(a,"genPoints");const l=e.append("polygon");l.attr("points",a(t.x,t.y,50,20,7)),l.attr("class","labelBox"),t.y=t.y+t.labelMargin,t.x=t.x+.5*t.labelMargin,at(e,t)},"drawLabel"),Vt=s(function(e,t,a){const l=e.append("g"),i=st();i.x=t.x,i.y=t.y,i.fill=t.fill,i.width=a.width*t.taskCount+a.diagramMarginX*(t.taskCount-1),i.height=a.height,i.class="journey-section section-type-"+t.num,i.rx=3,i.ry=3,W(l,i),ot(a)(t.text,l,i.x,i.y,i.width,i.height,{class:"journey-section section-type-"+t.num},a,t.colour)},"drawSection"),et=-1,Lt=s(function(e,t,a){const l=t.x+a.width/2,i=e.append("g");et++,i.append("line").attr("id","task"+et).attr("x1",l).attr("y1",t.y).attr("x2",l).attr("y2",450).attr("class","task-line").attr("stroke-width","1px").attr("stroke-dasharray","4 2").attr("stroke","#666"),Ct(i,{cx:l,cy:300+30*(5-t.score),score:t.score});const o=st();o.x=t.x,o.y=t.y,o.fill=t.fill,o.width=a.width,o.height=a.height,o.class="task task-type-"+t.num,o.rx=3,o.ry=3,W(i,o);let h=t.x+14;t.people.forEach(d=>{const y=t.actors[d].color,g={cx:h,cy:t.y,r:7,fill:y,stroke:"#000",title:d,pos:t.actors[d].position};rt(i,g),h+=10}),ot(a)(t.task,i,o.x,o.y,o.width,o.height,{class:"task"},a,t.colour)},"drawTask"),Ot=s(function(e,t){pt(e,t)},"drawBackgroundRect"),ot=function(){function e(i,o,h,d,y,g,m,n){l(o.append("text").attr("x",h+y/2).attr("y",d+g/2+5).style("font-color",n).style("text-anchor","middle").text(i),m)}function t(i,o,h,d,y,g,m,n,u){const{taskFontSize:r,taskFontFamily:p}=n,f=i.split(/<br\s*\/?>/gi);for(let c=0;c<f.length;c++){const $=c*r-r*(f.length-1)/2,x=o.append("text").attr("x",h+y/2).attr("y",d).attr("fill",u).style("text-anchor","middle").style("font-size",r).style("font-family",p);x.append("tspan").attr("x",h+y/2).attr("dy",$).text(f[c]),x.attr("y",d+g/2).attr("dominant-baseline","central").attr("alignment-baseline","central"),l(x,m)}}function a(i,o,h,d,y,g,m,n){const u=o.append("switch"),r=u.append("foreignObject").attr("x",h).attr("y",d).attr("width",y).attr("height",g).attr("position","fixed").append("xhtml:div").style("display","table").style("height","100%").style("width","100%");r.append("div").attr("class","label").style("display","table-cell").style("text-align","center").style("vertical-align","middle").text(i),t(i,u,h,d,y,g,m,n),l(r,m)}function l(i,o){for(const h in o)h in o&&i.attr(h,o[h])}return s(e,"byText"),s(t,"byTspan"),s(a,"byFo"),s(l,"_setTextAttrs"),function(i){return i.textPlacement==="fo"?a:i.textPlacement==="old"?e:t}}(),L={drawRect:W,drawCircle:rt,drawSection:Vt,drawText:at,drawLabel:jt,drawTask:Lt,drawBackgroundRect:Ot,initGraphics:s(function(e){e.append("defs").append("marker").attr("id","arrowhead").attr("refX",5).attr("refY",2).attr("markerWidth",6).attr("markerHeight",4).attr("orient","auto").append("path").attr("d","M 0,0 V 4 L6,2 Z")},"initGraphics")},Ft=s(function(e){Object.keys(e).forEach(function(t){D[t]=e[t]})},"setConf"),T={};function ct(e){const t=I().journey;let a=60;Object.keys(T).forEach(l=>{const i=T[l].color,o={cx:20,cy:a,r:7,fill:i,stroke:"#000",pos:T[l].position};L.drawCircle(e,o);const h={x:40,y:a+7,fill:"#666",text:l,textMargin:5|t.boxTextMargin};L.drawText(e,h),a+=20})}s(ct,"drawActorLegend");var D=I().journey,A=D.leftMargin,Bt=s(function(e,t,a,l){const i=I().journey,o=I().securityLevel;let h;o==="sandbox"&&(h=J("#i"+t));const d=J(o==="sandbox"?h.nodes()[0].contentDocument.body:"body");w.init();const y=d.select("#"+t);L.initGraphics(y);const g=l.db.getTasks(),m=l.db.getDiagramTitle(),n=l.db.getActors();for(const $ in T)delete T[$];let u=0;n.forEach($=>{T[$]={color:i.actorColours[u%i.actorColours.length],position:u},u++}),ct(y),w.insert(0,0,A,50*Object.keys(T).length),Dt(y,g,0);const r=w.getBounds();m&&y.append("text").text(m).attr("x",A).attr("font-size","4ex").attr("font-weight","bold").attr("y",25);const p=r.stopy-r.starty+2*i.diagramMarginY,f=A+r.stopx+2*i.diagramMarginX;bt(y,p,f,i.useMaxWidth),y.append("line").attr("x1",A).attr("y1",4*i.height).attr("x2",f-A-4).attr("y2",4*i.height).attr("stroke-width",4).attr("stroke","black").attr("marker-end","url(#arrowhead)");const c=m?70:0;y.attr("viewBox",`${r.startx} -25 ${f} ${p+c}`),y.attr("preserveAspectRatio","xMinYMin meet"),y.attr("height",p+c+25)},"draw"),w={data:{startx:void 0,stopx:void 0,starty:void 0,stopy:void 0},verticalPos:0,sequenceItems:[],init:s(function(){this.sequenceItems=[],this.data={startx:void 0,stopx:void 0,starty:void 0,stopy:void 0},this.verticalPos=0},"init"),updateVal:s(function(e,t,a,l){e[t]===void 0?e[t]=a:e[t]=l(a,e[t])},"updateVal"),updateBounds:s(function(e,t,a,l){const i=I().journey,o=this;let h=0;function d(y){return s(function(g){h++;const m=o.sequenceItems.length-h+1;o.updateVal(g,"starty",t-m*i.boxMargin,Math.min),o.updateVal(g,"stopy",l+m*i.boxMargin,Math.max),o.updateVal(w.data,"startx",e-m*i.boxMargin,Math.min),o.updateVal(w.data,"stopx",a+m*i.boxMargin,Math.max),y!=="activation"&&(o.updateVal(g,"startx",e-m*i.boxMargin,Math.min),o.updateVal(g,"stopx",a+m*i.boxMargin,Math.max),o.updateVal(w.data,"starty",t-m*i.boxMargin,Math.min),o.updateVal(w.data,"stopy",l+m*i.boxMargin,Math.max))},"updateItemBounds")}s(d,"updateFn"),this.sequenceItems.forEach(d())},"updateBounds"),insert:s(function(e,t,a,l){const i=Math.min(e,a),o=Math.max(e,a),h=Math.min(t,l),d=Math.max(t,l);this.updateVal(w.data,"startx",i,Math.min),this.updateVal(w.data,"starty",h,Math.min),this.updateVal(w.data,"stopx",o,Math.max),this.updateVal(w.data,"stopy",d,Math.max),this.updateBounds(i,h,o,d)},"insert"),bumpVerticalPos:s(function(e){this.verticalPos=this.verticalPos+e,this.data.stopy=this.verticalPos},"bumpVerticalPos"),getVerticalPos:s(function(){return this.verticalPos},"getVerticalPos"),getBounds:s(function(){return this.data},"getBounds")},z=D.sectionFills,nt=D.sectionColours,Dt=s(function(e,t,a){const l=I().journey;let i="";const o=a+(2*l.height+l.diagramMarginY);let h=0,d="#CCC",y="black",g=0;for(const[m,n]of t.entries()){if(i!==n.section){d=z[h%z.length],g=h%z.length,y=nt[h%nt.length];let r=0;const p=n.section;for(let c=m;c<t.length&&t[c].section==p;c++)r+=1;const f={x:m*l.taskMargin+m*l.width+A,y:50,text:n.section,fill:d,num:g,colour:y,taskCount:r};L.drawSection(e,f,l),i=n.section,h++}const u=n.people.reduce((r,p)=>(T[p]&&(r[p]=T[p]),r),{});n.x=m*l.taskMargin+m*l.width+A,n.y=o,n.width=l.diagramMarginX,n.height=l.diagramMarginY,n.colour=y,n.fill=d,n.num=g,n.actors=u,L.drawTask(e,n,l),w.insert(n.x,n.y,n.x+n.width+l.taskMargin,450)}},"drawTasks"),it={setConf:Ft,draw:Bt},Ee={parser:vt,db:tt,renderer:it,styles:Pt,init:s(e=>{it.setConf(e.journey),tt.clear()},"init")};export{Ee as diagram};
