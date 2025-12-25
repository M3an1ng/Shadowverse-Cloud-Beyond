import{g as Se,s as me}from"./chunk-5HRBRIJM-DI4FTzoC.js";import{_ as s,o as _e,p as be,s as Te,g as ke,b as Ee,a as xe,l as T,c as $,t as Ce,u as $e,k as M,x as De,a9 as ve}from"./AugmentMessage-FtcicXdY.js";var Dt=function(){var t=s(function(r,p,d,f){for(d=d||{},f=r.length;f--;d[r[f]]=p);return d},"o"),e=[1,2],l=[1,3],a=[1,4],n=[2,4],c=[1,9],y=[1,11],S=[1,16],o=[1,17],m=[1,18],v=[1,19],O=[1,32],P=[1,20],Y=[1,21],L=[1,22],u=[1,23],D=[1,24],I=[1,26],G=[1,27],j=[1,28],R=[1,29],N=[1,30],tt=[1,31],et=[1,34],st=[1,35],it=[1,36],nt=[1,37],J=[1,33],g=[1,4,5,16,17,19,21,22,24,25,26,27,28,29,33,35,37,38,42,45,48,49,50,51,54],rt=[1,4,5,14,15,16,17,19,21,22,24,25,26,27,28,29,33,35,37,38,42,45,48,49,50,51,54],Bt=[4,5,16,17,19,21,22,24,25,26,27,28,29,33,35,37,38,42,45,48,49,50,51,54],kt={trace:s(function(){},"trace"),yy:{},symbols_:{error:2,start:3,SPACE:4,NL:5,SD:6,document:7,line:8,statement:9,classDefStatement:10,styleStatement:11,cssClassStatement:12,idStatement:13,DESCR:14,"-->":15,HIDE_EMPTY:16,scale:17,WIDTH:18,COMPOSIT_STATE:19,STRUCT_START:20,STRUCT_STOP:21,STATE_DESCR:22,AS:23,ID:24,FORK:25,JOIN:26,CHOICE:27,CONCURRENT:28,note:29,notePosition:30,NOTE_TEXT:31,direction:32,acc_title:33,acc_title_value:34,acc_descr:35,acc_descr_value:36,acc_descr_multiline_value:37,classDef:38,CLASSDEF_ID:39,CLASSDEF_STYLEOPTS:40,DEFAULT:41,style:42,STYLE_IDS:43,STYLEDEF_STYLEOPTS:44,class:45,CLASSENTITY_IDS:46,STYLECLASS:47,direction_tb:48,direction_bt:49,direction_rl:50,direction_lr:51,eol:52,";":53,EDGE_STATE:54,STYLE_SEPARATOR:55,left_of:56,right_of:57,$accept:0,$end:1},terminals_:{2:"error",4:"SPACE",5:"NL",6:"SD",14:"DESCR",15:"-->",16:"HIDE_EMPTY",17:"scale",18:"WIDTH",19:"COMPOSIT_STATE",20:"STRUCT_START",21:"STRUCT_STOP",22:"STATE_DESCR",23:"AS",24:"ID",25:"FORK",26:"JOIN",27:"CHOICE",28:"CONCURRENT",29:"note",31:"NOTE_TEXT",33:"acc_title",34:"acc_title_value",35:"acc_descr",36:"acc_descr_value",37:"acc_descr_multiline_value",38:"classDef",39:"CLASSDEF_ID",40:"CLASSDEF_STYLEOPTS",41:"DEFAULT",42:"style",43:"STYLE_IDS",44:"STYLEDEF_STYLEOPTS",45:"class",46:"CLASSENTITY_IDS",47:"STYLECLASS",48:"direction_tb",49:"direction_bt",50:"direction_rl",51:"direction_lr",53:";",54:"EDGE_STATE",55:"STYLE_SEPARATOR",56:"left_of",57:"right_of"},productions_:[0,[3,2],[3,2],[3,2],[7,0],[7,2],[8,2],[8,1],[8,1],[9,1],[9,1],[9,1],[9,1],[9,2],[9,3],[9,4],[9,1],[9,2],[9,1],[9,4],[9,3],[9,6],[9,1],[9,1],[9,1],[9,1],[9,4],[9,4],[9,1],[9,2],[9,2],[9,1],[10,3],[10,3],[11,3],[12,3],[32,1],[32,1],[32,1],[32,1],[52,1],[52,1],[13,1],[13,1],[13,3],[13,3],[30,1],[30,1]],performAction:s(function(r,p,d,f,_,i,K){var h=i.length-1;switch(_){case 3:return f.setRootDoc(i[h]),i[h];case 4:this.$=[];break;case 5:i[h]!="nl"&&(i[h-1].push(i[h]),this.$=i[h-1]);break;case 6:case 7:case 12:this.$=i[h];break;case 8:this.$="nl";break;case 13:const b=i[h-1];b.description=f.trimColon(i[h]),this.$=b;break;case 14:this.$={stmt:"relation",state1:i[h-2],state2:i[h]};break;case 15:const A=f.trimColon(i[h]);this.$={stmt:"relation",state1:i[h-3],state2:i[h-1],description:A};break;case 19:this.$={stmt:"state",id:i[h-3],type:"default",description:"",doc:i[h-1]};break;case 20:var z=i[h],H=i[h-2].trim();if(i[h].match(":")){var ot=i[h].split(":");z=ot[0],H=[H,ot[1]]}this.$={stmt:"state",id:z,type:"default",description:H};break;case 21:this.$={stmt:"state",id:i[h-3],type:"default",description:i[h-5],doc:i[h-1]};break;case 22:this.$={stmt:"state",id:i[h],type:"fork"};break;case 23:this.$={stmt:"state",id:i[h],type:"join"};break;case 24:this.$={stmt:"state",id:i[h],type:"choice"};break;case 25:this.$={stmt:"state",id:f.getDividerId(),type:"divider"};break;case 26:this.$={stmt:"state",id:i[h-1].trim(),note:{position:i[h-2].trim(),text:i[h].trim()}};break;case 29:this.$=i[h].trim(),f.setAccTitle(this.$);break;case 30:case 31:this.$=i[h].trim(),f.setAccDescription(this.$);break;case 32:case 33:this.$={stmt:"classDef",id:i[h-1].trim(),classes:i[h].trim()};break;case 34:this.$={stmt:"style",id:i[h-1].trim(),styleClass:i[h].trim()};break;case 35:this.$={stmt:"applyClass",id:i[h-1].trim(),styleClass:i[h].trim()};break;case 36:f.setDirection("TB"),this.$={stmt:"dir",value:"TB"};break;case 37:f.setDirection("BT"),this.$={stmt:"dir",value:"BT"};break;case 38:f.setDirection("RL"),this.$={stmt:"dir",value:"RL"};break;case 39:f.setDirection("LR"),this.$={stmt:"dir",value:"LR"};break;case 42:case 43:this.$={stmt:"state",id:i[h].trim(),type:"default",description:""};break;case 44:case 45:this.$={stmt:"state",id:i[h-2].trim(),classes:[i[h].trim()],type:"default",description:""}}},"anonymous"),table:[{3:1,4:e,5:l,6:a},{1:[3]},{3:5,4:e,5:l,6:a},{3:6,4:e,5:l,6:a},t([1,4,5,16,17,19,22,24,25,26,27,28,29,33,35,37,38,42,45,48,49,50,51,54],n,{7:7}),{1:[2,1]},{1:[2,2]},{1:[2,3],4:c,5:y,8:8,9:10,10:12,11:13,12:14,13:15,16:S,17:o,19:m,22:v,24:O,25:P,26:Y,27:L,28:u,29:D,32:25,33:I,35:G,37:j,38:R,42:N,45:tt,48:et,49:st,50:it,51:nt,54:J},t(g,[2,5]),{9:38,10:12,11:13,12:14,13:15,16:S,17:o,19:m,22:v,24:O,25:P,26:Y,27:L,28:u,29:D,32:25,33:I,35:G,37:j,38:R,42:N,45:tt,48:et,49:st,50:it,51:nt,54:J},t(g,[2,7]),t(g,[2,8]),t(g,[2,9]),t(g,[2,10]),t(g,[2,11]),t(g,[2,12],{14:[1,39],15:[1,40]}),t(g,[2,16]),{18:[1,41]},t(g,[2,18],{20:[1,42]}),{23:[1,43]},t(g,[2,22]),t(g,[2,23]),t(g,[2,24]),t(g,[2,25]),{30:44,31:[1,45],56:[1,46],57:[1,47]},t(g,[2,28]),{34:[1,48]},{36:[1,49]},t(g,[2,31]),{39:[1,50],41:[1,51]},{43:[1,52]},{46:[1,53]},t(rt,[2,42],{55:[1,54]}),t(rt,[2,43],{55:[1,55]}),t(g,[2,36]),t(g,[2,37]),t(g,[2,38]),t(g,[2,39]),t(g,[2,6]),t(g,[2,13]),{13:56,24:O,54:J},t(g,[2,17]),t(Bt,n,{7:57}),{24:[1,58]},{24:[1,59]},{23:[1,60]},{24:[2,46]},{24:[2,47]},t(g,[2,29]),t(g,[2,30]),{40:[1,61]},{40:[1,62]},{44:[1,63]},{47:[1,64]},{24:[1,65]},{24:[1,66]},t(g,[2,14],{14:[1,67]}),{4:c,5:y,8:8,9:10,10:12,11:13,12:14,13:15,16:S,17:o,19:m,21:[1,68],22:v,24:O,25:P,26:Y,27:L,28:u,29:D,32:25,33:I,35:G,37:j,38:R,42:N,45:tt,48:et,49:st,50:it,51:nt,54:J},t(g,[2,20],{20:[1,69]}),{31:[1,70]},{24:[1,71]},t(g,[2,32]),t(g,[2,33]),t(g,[2,34]),t(g,[2,35]),t(rt,[2,44]),t(rt,[2,45]),t(g,[2,15]),t(g,[2,19]),t(Bt,n,{7:72}),t(g,[2,26]),t(g,[2,27]),{4:c,5:y,8:8,9:10,10:12,11:13,12:14,13:15,16:S,17:o,19:m,21:[1,73],22:v,24:O,25:P,26:Y,27:L,28:u,29:D,32:25,33:I,35:G,37:j,38:R,42:N,45:tt,48:et,49:st,50:it,51:nt,54:J},t(g,[2,21])],defaultActions:{5:[2,1],6:[2,2],46:[2,46],47:[2,47]},parseError:s(function(r,p){if(!p.recoverable){var d=new Error(r);throw d.hash=p,d}this.trace(r)},"parseError"),parse:s(function(r){var p=this,d=[0],f=[],_=[null],i=[],K=this.table,h="",z=0,H=0,ot=i.slice.call(arguments,1),b=Object.create(this.lexer),A={yy:{}};for(var Et in this.yy)Object.prototype.hasOwnProperty.call(this.yy,Et)&&(A.yy[Et]=this.yy[Et]);b.setInput(r,A.yy),A.yy.lexer=b,A.yy.parser=this,b.yylloc===void 0&&(b.yylloc={});var xt=b.yylloc;i.push(xt);var fe=b.options&&b.options.ranges;function Ft(){var C;return typeof(C=f.pop()||b.lex()||1)!="number"&&(C instanceof Array&&(C=(f=C).pop()),C=p.symbols_[C]||C),C}typeof A.yy.parseError=="function"?this.parseError=A.yy.parseError:this.parseError=Object.getPrototypeOf(this).parseError,s(function(C){d.length=d.length-2*C,_.length=_.length-C,i.length=i.length-C},"popStack"),s(Ft,"lex");for(var E,U,x,Pt,lt,w,Yt,ct,V={};;){if(U=d[d.length-1],this.defaultActions[U]?x=this.defaultActions[U]:(E==null&&(E=Ft()),x=K[U]&&K[U][E]),x===void 0||!x.length||!x[0]){var Gt="";for(lt in ct=[],K[U])this.terminals_[lt]&&lt>2&&ct.push("'"+this.terminals_[lt]+"'");Gt=b.showPosition?"Parse error on line "+(z+1)+`:
`+b.showPosition()+`
Expecting `+ct.join(", ")+", got '"+(this.terminals_[E]||E)+"'":"Parse error on line "+(z+1)+": Unexpected "+(E==1?"end of input":"'"+(this.terminals_[E]||E)+"'"),this.parseError(Gt,{text:b.match,token:this.terminals_[E]||E,line:b.yylineno,loc:xt,expected:ct})}if(x[0]instanceof Array&&x.length>1)throw new Error("Parse Error: multiple actions possible at state: "+U+", token: "+E);switch(x[0]){case 1:d.push(E),_.push(b.yytext),i.push(b.yylloc),d.push(x[1]),E=null,H=b.yyleng,h=b.yytext,z=b.yylineno,xt=b.yylloc;break;case 2:if(w=this.productions_[x[1]][1],V.$=_[_.length-w],V._$={first_line:i[i.length-(w||1)].first_line,last_line:i[i.length-1].last_line,first_column:i[i.length-(w||1)].first_column,last_column:i[i.length-1].last_column},fe&&(V._$.range=[i[i.length-(w||1)].range[0],i[i.length-1].range[1]]),(Pt=this.performAction.apply(V,[h,H,z,A.yy,x[1],_,i].concat(ot)))!==void 0)return Pt;w&&(d=d.slice(0,-1*w*2),_=_.slice(0,-1*w),i=i.slice(0,-1*w)),d.push(this.productions_[x[1]][0]),_.push(V.$),i.push(V._$),Yt=K[d[d.length-2]][d[d.length-1]],d.push(Yt);break;case 3:return!0}}return!0},"parse")},ge=function(){return{EOF:1,parseError:s(function(r,p){if(!this.yy.parser)throw new Error(r);this.yy.parser.parseError(r,p)},"parseError"),setInput:s(function(r,p){return this.yy=p||this.yy||{},this._input=r,this._more=this._backtrack=this.done=!1,this.yylineno=this.yyleng=0,this.yytext=this.matched=this.match="",this.conditionStack=["INITIAL"],this.yylloc={first_line:1,first_column:0,last_line:1,last_column:0},this.options.ranges&&(this.yylloc.range=[0,0]),this.offset=0,this},"setInput"),input:s(function(){var r=this._input[0];return this.yytext+=r,this.yyleng++,this.offset++,this.match+=r,this.matched+=r,r.match(/(?:\r\n?|\n).*/g)?(this.yylineno++,this.yylloc.last_line++):this.yylloc.last_column++,this.options.ranges&&this.yylloc.range[1]++,this._input=this._input.slice(1),r},"input"),unput:s(function(r){var p=r.length,d=r.split(/(?:\r\n?|\n)/g);this._input=r+this._input,this.yytext=this.yytext.substr(0,this.yytext.length-p),this.offset-=p;var f=this.match.split(/(?:\r\n?|\n)/g);this.match=this.match.substr(0,this.match.length-1),this.matched=this.matched.substr(0,this.matched.length-1),d.length-1&&(this.yylineno-=d.length-1);var _=this.yylloc.range;return this.yylloc={first_line:this.yylloc.first_line,last_line:this.yylineno+1,first_column:this.yylloc.first_column,last_column:d?(d.length===f.length?this.yylloc.first_column:0)+f[f.length-d.length].length-d[0].length:this.yylloc.first_column-p},this.options.ranges&&(this.yylloc.range=[_[0],_[0]+this.yyleng-p]),this.yyleng=this.yytext.length,this},"unput"),more:s(function(){return this._more=!0,this},"more"),reject:s(function(){return this.options.backtrack_lexer?(this._backtrack=!0,this):this.parseError("Lexical error on line "+(this.yylineno+1)+`. You can only invoke reject() in the lexer when the lexer is of the backtracking persuasion (options.backtrack_lexer = true).
`+this.showPosition(),{text:"",token:null,line:this.yylineno})},"reject"),less:s(function(r){this.unput(this.match.slice(r))},"less"),pastInput:s(function(){var r=this.matched.substr(0,this.matched.length-this.match.length);return(r.length>20?"...":"")+r.substr(-20).replace(/\n/g,"")},"pastInput"),upcomingInput:s(function(){var r=this.match;return r.length<20&&(r+=this._input.substr(0,20-r.length)),(r.substr(0,20)+(r.length>20?"...":"")).replace(/\n/g,"")},"upcomingInput"),showPosition:s(function(){var r=this.pastInput(),p=new Array(r.length+1).join("-");return r+this.upcomingInput()+`
`+p+"^"},"showPosition"),test_match:s(function(r,p){var d,f,_;if(this.options.backtrack_lexer&&(_={yylineno:this.yylineno,yylloc:{first_line:this.yylloc.first_line,last_line:this.last_line,first_column:this.yylloc.first_column,last_column:this.yylloc.last_column},yytext:this.yytext,match:this.match,matches:this.matches,matched:this.matched,yyleng:this.yyleng,offset:this.offset,_more:this._more,_input:this._input,yy:this.yy,conditionStack:this.conditionStack.slice(0),done:this.done},this.options.ranges&&(_.yylloc.range=this.yylloc.range.slice(0))),(f=r[0].match(/(?:\r\n?|\n).*/g))&&(this.yylineno+=f.length),this.yylloc={first_line:this.yylloc.last_line,last_line:this.yylineno+1,first_column:this.yylloc.last_column,last_column:f?f[f.length-1].length-f[f.length-1].match(/\r?\n?/)[0].length:this.yylloc.last_column+r[0].length},this.yytext+=r[0],this.match+=r[0],this.matches=r,this.yyleng=this.yytext.length,this.options.ranges&&(this.yylloc.range=[this.offset,this.offset+=this.yyleng]),this._more=!1,this._backtrack=!1,this._input=this._input.slice(r[0].length),this.matched+=r[0],d=this.performAction.call(this,this.yy,this,p,this.conditionStack[this.conditionStack.length-1]),this.done&&this._input&&(this.done=!1),d)return d;if(this._backtrack){for(var i in _)this[i]=_[i];return!1}return!1},"test_match"),next:s(function(){if(this.done)return this.EOF;var r,p,d,f;this._input||(this.done=!0),this._more||(this.yytext="",this.match="");for(var _=this._currentRules(),i=0;i<_.length;i++)if((d=this._input.match(this.rules[_[i]]))&&(!p||d[0].length>p[0].length)){if(p=d,f=i,this.options.backtrack_lexer){if((r=this.test_match(d,_[i]))!==!1)return r;if(this._backtrack){p=!1;continue}return!1}if(!this.options.flex)break}return p?(r=this.test_match(p,_[f]))!==!1&&r:this._input===""?this.EOF:this.parseError("Lexical error on line "+(this.yylineno+1)+`. Unrecognized text.
`+this.showPosition(),{text:"",token:null,line:this.yylineno})},"next"),lex:s(function(){var r=this.next();return r||this.lex()},"lex"),begin:s(function(r){this.conditionStack.push(r)},"begin"),popState:s(function(){return this.conditionStack.length-1>0?this.conditionStack.pop():this.conditionStack[0]},"popState"),_currentRules:s(function(){return this.conditionStack.length&&this.conditionStack[this.conditionStack.length-1]?this.conditions[this.conditionStack[this.conditionStack.length-1]].rules:this.conditions.INITIAL.rules},"_currentRules"),topState:s(function(r){return(r=this.conditionStack.length-1-Math.abs(r||0))>=0?this.conditionStack[r]:"INITIAL"},"topState"),pushState:s(function(r){this.begin(r)},"pushState"),stateStackSize:s(function(){return this.conditionStack.length},"stateStackSize"),options:{"case-insensitive":!0},performAction:s(function(r,p,d,f){switch(d){case 0:return 41;case 1:case 42:return 48;case 2:case 43:return 49;case 3:case 44:return 50;case 4:case 45:return 51;case 5:case 6:case 8:case 9:case 10:case 11:case 54:case 56:case 62:break;case 7:case 77:return 5;case 12:case 32:return this.pushState("SCALE"),17;case 13:case 33:return 18;case 14:case 20:case 34:case 49:case 52:this.popState();break;case 15:return this.begin("acc_title"),33;case 16:return this.popState(),"acc_title_value";case 17:return this.begin("acc_descr"),35;case 18:return this.popState(),"acc_descr_value";case 19:this.begin("acc_descr_multiline");break;case 21:return"acc_descr_multiline_value";case 22:return this.pushState("CLASSDEF"),38;case 23:return this.popState(),this.pushState("CLASSDEFID"),"DEFAULT_CLASSDEF_ID";case 24:return this.popState(),this.pushState("CLASSDEFID"),39;case 25:return this.popState(),40;case 26:return this.pushState("CLASS"),45;case 27:return this.popState(),this.pushState("CLASS_STYLE"),46;case 28:return this.popState(),47;case 29:return this.pushState("STYLE"),42;case 30:return this.popState(),this.pushState("STYLEDEF_STYLES"),43;case 31:return this.popState(),44;case 35:this.pushState("STATE");break;case 36:case 39:return this.popState(),p.yytext=p.yytext.slice(0,-8).trim(),25;case 37:case 40:return this.popState(),p.yytext=p.yytext.slice(0,-8).trim(),26;case 38:case 41:return this.popState(),p.yytext=p.yytext.slice(0,-10).trim(),27;case 46:this.pushState("STATE_STRING");break;case 47:return this.pushState("STATE_ID"),"AS";case 48:case 64:return this.popState(),"ID";case 50:return"STATE_DESCR";case 51:return 19;case 53:return this.popState(),this.pushState("struct"),20;case 55:return this.popState(),21;case 57:return this.begin("NOTE"),29;case 58:return this.popState(),this.pushState("NOTE_ID"),56;case 59:return this.popState(),this.pushState("NOTE_ID"),57;case 60:this.popState(),this.pushState("FLOATING_NOTE");break;case 61:return this.popState(),this.pushState("FLOATING_NOTE_ID"),"AS";case 63:return"NOTE_TEXT";case 65:return this.popState(),this.pushState("NOTE_TEXT"),24;case 66:return this.popState(),p.yytext=p.yytext.substr(2).trim(),31;case 67:return this.popState(),p.yytext=p.yytext.slice(0,-8).trim(),31;case 68:case 69:return 6;case 70:return 16;case 71:return 54;case 72:return 24;case 73:return p.yytext=p.yytext.trim(),14;case 74:return 15;case 75:return 28;case 76:return 55;case 78:return"INVALID"}},"anonymous"),rules:[/^(?:default\b)/i,/^(?:.*direction\s+TB[^\n]*)/i,/^(?:.*direction\s+BT[^\n]*)/i,/^(?:.*direction\s+RL[^\n]*)/i,/^(?:.*direction\s+LR[^\n]*)/i,/^(?:%%(?!\{)[^\n]*)/i,/^(?:[^\}]%%[^\n]*)/i,/^(?:[\n]+)/i,/^(?:[\s]+)/i,/^(?:((?!\n)\s)+)/i,/^(?:#[^\n]*)/i,/^(?:%[^\n]*)/i,/^(?:scale\s+)/i,/^(?:\d+)/i,/^(?:\s+width\b)/i,/^(?:accTitle\s*:\s*)/i,/^(?:(?!\n||)*[^\n]*)/i,/^(?:accDescr\s*:\s*)/i,/^(?:(?!\n||)*[^\n]*)/i,/^(?:accDescr\s*\{\s*)/i,/^(?:[\}])/i,/^(?:[^\}]*)/i,/^(?:classDef\s+)/i,/^(?:DEFAULT\s+)/i,/^(?:\w+\s+)/i,/^(?:[^\n]*)/i,/^(?:class\s+)/i,/^(?:(\w+)+((,\s*\w+)*))/i,/^(?:[^\n]*)/i,/^(?:style\s+)/i,/^(?:[\w,]+\s+)/i,/^(?:[^\n]*)/i,/^(?:scale\s+)/i,/^(?:\d+)/i,/^(?:\s+width\b)/i,/^(?:state\s+)/i,/^(?:.*<<fork>>)/i,/^(?:.*<<join>>)/i,/^(?:.*<<choice>>)/i,/^(?:.*\[\[fork\]\])/i,/^(?:.*\[\[join\]\])/i,/^(?:.*\[\[choice\]\])/i,/^(?:.*direction\s+TB[^\n]*)/i,/^(?:.*direction\s+BT[^\n]*)/i,/^(?:.*direction\s+RL[^\n]*)/i,/^(?:.*direction\s+LR[^\n]*)/i,/^(?:["])/i,/^(?:\s*as\s+)/i,/^(?:[^\n\{]*)/i,/^(?:["])/i,/^(?:[^"]*)/i,/^(?:[^\n\s\{]+)/i,/^(?:\n)/i,/^(?:\{)/i,/^(?:%%(?!\{)[^\n]*)/i,/^(?:\})/i,/^(?:[\n])/i,/^(?:note\s+)/i,/^(?:left of\b)/i,/^(?:right of\b)/i,/^(?:")/i,/^(?:\s*as\s*)/i,/^(?:["])/i,/^(?:[^"]*)/i,/^(?:[^\n]*)/i,/^(?:\s*[^:\n\s\-]+)/i,/^(?:\s*:[^:\n;]+)/i,/^(?:[\s\S]*?end note\b)/i,/^(?:stateDiagram\s+)/i,/^(?:stateDiagram-v2\s+)/i,/^(?:hide empty description\b)/i,/^(?:\[\*\])/i,/^(?:[^:\n\s\-\{]+)/i,/^(?:\s*:[^:\n;]+)/i,/^(?:-->)/i,/^(?:--)/i,/^(?::::)/i,/^(?:$)/i,/^(?:.)/i],conditions:{LINE:{rules:[9,10],inclusive:!1},struct:{rules:[9,10,22,26,29,35,42,43,44,45,54,55,56,57,71,72,73,74,75],inclusive:!1},FLOATING_NOTE_ID:{rules:[64],inclusive:!1},FLOATING_NOTE:{rules:[61,62,63],inclusive:!1},NOTE_TEXT:{rules:[66,67],inclusive:!1},NOTE_ID:{rules:[65],inclusive:!1},NOTE:{rules:[58,59,60],inclusive:!1},STYLEDEF_STYLEOPTS:{rules:[],inclusive:!1},STYLEDEF_STYLES:{rules:[31],inclusive:!1},STYLE_IDS:{rules:[],inclusive:!1},STYLE:{rules:[30],inclusive:!1},CLASS_STYLE:{rules:[28],inclusive:!1},CLASS:{rules:[27],inclusive:!1},CLASSDEFID:{rules:[25],inclusive:!1},CLASSDEF:{rules:[23,24],inclusive:!1},acc_descr_multiline:{rules:[20,21],inclusive:!1},acc_descr:{rules:[18],inclusive:!1},acc_title:{rules:[16],inclusive:!1},SCALE:{rules:[13,14,33,34],inclusive:!1},ALIAS:{rules:[],inclusive:!1},STATE_ID:{rules:[48],inclusive:!1},STATE_STRING:{rules:[49,50],inclusive:!1},FORK_STATE:{rules:[],inclusive:!1},STATE:{rules:[9,10,36,37,38,39,40,41,46,47,51,52,53],inclusive:!1},ID:{rules:[9,10],inclusive:!1},INITIAL:{rules:[0,1,2,3,4,5,6,7,8,10,11,12,15,17,19,22,26,29,32,35,53,57,68,69,70,71,72,73,74,76,77,78],inclusive:!0}}}}();function at(){this.yy={}}return kt.lexer=ge,s(at,"Parser"),at.prototype=kt,kt.Parser=at,new at}();Dt.parser=Dt;var ts=Dt,gt="state",Lt="relation",Q="default",Vt="divider",Wt="fill:none",Jt="fill: #333",Kt="text",qt="normal",Ct="rect",$t="rectWithTitle",jt="divider",zt="roundedWithTitle",Z="statediagram",Le=`${Z}-state`,Qt="transition",Ie=`${Qt} note-edge`,Ae=`${Z}-note`,we=`${Z}-cluster`,Oe=`${Z}-cluster-alt`,Zt="parent",te="note",It="----",Re=`${It}${te}`,Ut=`${It}${Zt}`,ee=s((t,e="TB")=>{if(!t.doc)return e;let l=e;for(const a of t.doc)a.stmt==="dir"&&(l=a.value);return l},"getDir"),es={getClasses:s(function(t,e){return e.db.extract(e.db.getRootDocV2()),e.db.getClasses()},"getClasses"),draw:s(async function(t,e,l,a){T.info("REF0:"),T.info("Drawing state diagram (v2)",e);const{securityLevel:n,state:c,layout:y}=$();a.db.extract(a.db.getRootDocV2());const S=a.db.getData(),o=Se(e,n);S.type=a.type,S.layoutAlgorithm=y,S.nodeSpacing=(c==null?void 0:c.nodeSpacing)||50,S.rankSpacing=(c==null?void 0:c.rankSpacing)||50,S.markers=["barb"],S.diagramId=e,await Ce(S,o),$e.insertTitle(o,"statediagramTitleText",(c==null?void 0:c.titleTopMargin)??25,a.db.getDiagramTitle()),me(o,8,Z,(c==null?void 0:c.useMaxWidth)??!0)},"draw"),getDir:ee},dt=new Map,B=0;function ut(t="",e=0,l="",a=It){return`state-${t}${l!==null&&l.length>0?`${a}${l}`:""}-${e}`}s(ut,"stateDomId");var Ne=s((t,e,l,a,n,c,y,S)=>{T.trace("items",e),e.forEach(o=>{switch(o.stmt){case gt:case Q:pt(t,o,l,a,n,c,y,S);break;case Lt:{pt(t,o.state1,l,a,n,c,y,S),pt(t,o.state2,l,a,n,c,y,S);const m={id:"edge"+B,start:o.state1.id,end:o.state2.id,arrowhead:"normal",arrowTypeEnd:"arrow_barb",style:Wt,labelStyle:"",label:M.sanitizeText(o.description,$()),arrowheadStyle:Jt,labelpos:"c",labelType:Kt,thickness:qt,classes:Qt,look:y};n.push(m),B++}}})},"setupDoc"),Mt=s((t,e="TB")=>{let l=e;if(t.doc)for(const a of t.doc)a.stmt==="dir"&&(l=a.value);return l},"getDir");function q(t,e,l){if(!e.id||e.id==="</join></fork>"||e.id==="</choice>")return;e.cssClasses&&(Array.isArray(e.cssCompiledStyles)||(e.cssCompiledStyles=[]),e.cssClasses.split(" ").forEach(n=>{if(l.get(n)){const c=l.get(n);e.cssCompiledStyles=[...e.cssCompiledStyles,...c.styles]}}));const a=t.find(n=>n.id===e.id);a?Object.assign(a,e):t.push(e)}function se(t){var e;return((e=t==null?void 0:t.classes)==null?void 0:e.join(" "))??""}function ie(t){return(t==null?void 0:t.styles)??[]}s(q,"insertOrUpdateNode"),s(se,"getClassesFromDbInfo"),s(ie,"getStylesFromDbInfo");var pt=s((t,e,l,a,n,c,y,S)=>{var P,Y;const o=e.id,m=l.get(o),v=se(m),O=ie(m);if(T.info("dataFetcher parsedItem",e,m,O),o!=="root"){let L=Ct;e.start===!0?L="stateStart":e.start===!1&&(L="stateEnd"),e.type!==Q&&(L=e.type),dt.get(o)||dt.set(o,{id:o,shape:L,description:M.sanitizeText(o,$()),cssClasses:`${v} ${Le}`,cssStyles:O});const u=dt.get(o);e.description&&(Array.isArray(u.description)?(u.shape=$t,u.description.push(e.description)):((P=u.description)==null?void 0:P.length)>0?(u.shape=$t,u.description===o?u.description=[e.description]:u.description=[u.description,e.description]):(u.shape=Ct,u.description=e.description),u.description=M.sanitizeTextOrArray(u.description,$())),((Y=u.description)==null?void 0:Y.length)===1&&u.shape===$t&&(u.type==="group"?u.shape=zt:u.shape=Ct),!u.type&&e.doc&&(T.info("Setting cluster for XCX",o,Mt(e)),u.type="group",u.isGroup=!0,u.dir=Mt(e),u.shape=e.type===Vt?jt:zt,u.cssClasses=`${u.cssClasses} ${we} ${c?Oe:""}`);const D={labelStyle:"",shape:u.shape,label:u.description,cssClasses:u.cssClasses,cssCompiledStyles:[],cssStyles:u.cssStyles,id:o,dir:u.dir,domId:ut(o,B),type:u.type,isGroup:u.type==="group",padding:8,rx:10,ry:10,look:y};if(D.shape===jt&&(D.label=""),t&&t.id!=="root"&&(T.trace("Setting node ",o," to be child of its parent ",t.id),D.parentId=t.id),D.centerLabel=!0,e.note){const I={labelStyle:"",shape:"note",label:e.note.text,cssClasses:Ae,cssStyles:[],cssCompilesStyles:[],id:o+Re+"-"+B,domId:ut(o,B,te),type:u.type,isGroup:u.type==="group",padding:$().flowchart.padding,look:y,position:e.note.position},G=o+Ut,j={labelStyle:"",shape:"noteGroup",label:e.note.text,cssClasses:u.cssClasses,cssStyles:[],id:o+Ut,domId:ut(o,B,Zt),type:"group",isGroup:!0,padding:16,look:y,position:e.note.position};B++,j.id=G,I.parentId=G,q(a,j,S),q(a,I,S),q(a,D,S);let R=o,N=I.id;e.note.position==="left of"&&(R=I.id,N=o),n.push({id:R+"-"+N,start:R,end:N,arrowhead:"none",arrowTypeEnd:"",style:Wt,labelStyle:"",classes:Ie,arrowheadStyle:Jt,labelpos:"c",labelType:Kt,thickness:qt,look:y})}else q(a,D,S)}e.doc&&(T.trace("Adding nodes children "),Ne(e,e.doc,l,a,n,!c,y,S))},"dataFetcher"),Be=s(()=>{dt.clear(),B=0},"reset"),At="[*]",ne="start",re=At,Xt="color",Ht="fill";function wt(){return new Map}s(wt,"newClassesList");var ft=[],Ot=[],ae="LR",St=[],W=wt(),oe=s(()=>({relations:[],states:new Map,documents:{}}),"newDoc"),mt={root:oe()},k=mt.root,_t=0,Fe=0,ht=s(t=>JSON.parse(JSON.stringify(t)),"clone"),Pe=s(t=>{T.info("Setting root doc",t),St=t},"setRootDoc"),Ye=s(()=>St,"getRootDoc"),yt=s((t,e,l)=>{if(e.stmt===Lt)yt(t,e.state1,!0),yt(t,e.state2,!1);else if(e.stmt===gt&&(e.id==="[*]"?(e.id=l?t.id+"_start":t.id+"_end",e.start=l):e.id=e.id.trim()),e.doc){const a=[];let n,c=[];for(n=0;n<e.doc.length;n++)if(e.doc[n].type===Vt){const y=ht(e.doc[n]);y.doc=ht(c),a.push(y),c=[]}else c.push(e.doc[n]);if(a.length>0&&c.length>0){const y={stmt:gt,id:ve(),type:"divider",doc:ht(c)};a.push(ht(y)),e.doc=a}e.doc.forEach(y=>yt(e,y,!0))}},"docTranslator"),Rt=s(()=>(yt({id:"root"},{id:"root",doc:St},!0),{id:"root",doc:St}),"getRootDocV2"),Ge=s(t=>{let e;e=t.doc?t.doc:t,T.info(e),le(!0),T.info("Extract initial document:",e),e.forEach(n=>{switch(T.warn("Statement",n.stmt),n.stmt){case gt:F(n.id.trim(),n.type,n.doc,n.description,n.note,n.classes,n.styles,n.textStyles);break;case Lt:pe(n.state1,n.state2,n.description);break;case"classDef":ye(n.id.trim(),n.classes);break;case"style":{const c=n.id.trim().split(","),y=n.styleClass.split(",");c.forEach(S=>{let o=X(S);if(o===void 0){const m=S.trim();F(m),o=X(m)}o.styles=y.map(m=>{var v;return(v=m.replace(/;/g,""))==null?void 0:v.trim()})})}break;case"applyClass":Nt(n.id.trim(),n.styleClass)}});const l=ce(),a=$().look;Be(),pt(void 0,Rt(),l,ft,Ot,!0,a,W),ft.forEach(n=>{if(Array.isArray(n.label)){if(n.description=n.label.slice(1),n.isGroup&&n.description.length>0)throw new Error("Group nodes can only have label. Remove the additional description for node ["+n.id+"]");n.label=n.label[0]}})},"extract"),F=s(function(t,e=Q,l=null,a=null,n=null,c=null,y=null,S=null){const o=t==null?void 0:t.trim();if(k.states.has(o)?(k.states.get(o).doc||(k.states.get(o).doc=l),k.states.get(o).type||(k.states.get(o).type=e)):(T.info("Adding state ",o,a),k.states.set(o,{id:o,descriptions:[],type:e,doc:l,note:n,classes:[],styles:[],textStyles:[]})),a&&(T.info("Setting state description",o,a),typeof a=="string"&&vt(o,a.trim()),typeof a=="object"&&a.forEach(m=>vt(o,m.trim()))),n){const m=k.states.get(o);m.note=n,m.note.text=M.sanitizeText(m.note.text,$())}c&&(T.info("Setting state classes",o,c),(typeof c=="string"?[c]:c).forEach(m=>Nt(o,m.trim()))),y&&(T.info("Setting state styles",o,y),(typeof y=="string"?[y]:y).forEach(m=>He(o,m.trim()))),S&&(T.info("Setting state styles",o,y),(typeof S=="string"?[S]:S).forEach(m=>Ve(o,m.trim())))},"addState"),le=s(function(t){ft=[],Ot=[],mt={root:oe()},k=mt.root,_t=0,W=wt(),t||De()},"clear"),X=s(function(t){return k.states.get(t)},"getState"),ce=s(function(){return k.states},"getStates"),je=s(function(){T.info("Documents = ",mt)},"logDocuments"),ze=s(function(){return k.relations},"getRelations");function bt(t=""){let e=t;return t===At&&(_t++,e=`${ne}${_t}`),e}function Tt(t="",e=Q){return t===At?ne:e}function he(t=""){let e=t;return t===re&&(e=`end${++_t}`),e}function de(t="",e=Q){return t===re?"end":e}function ue(t,e,l){let a=bt(t.id.trim()),n=Tt(t.id.trim(),t.type),c=bt(e.id.trim()),y=Tt(e.id.trim(),e.type);F(a,n,t.doc,t.description,t.note,t.classes,t.styles,t.textStyles),F(c,y,e.doc,e.description,e.note,e.classes,e.styles,e.textStyles),k.relations.push({id1:a,id2:c,relationTitle:M.sanitizeText(l,$())})}s(bt,"startIdIfNeeded"),s(Tt,"startTypeIfNeeded"),s(he,"endIdIfNeeded"),s(de,"endTypeIfNeeded"),s(ue,"addRelationObjs");var pe=s(function(t,e,l){if(typeof t=="object")ue(t,e,l);else{const a=bt(t.trim()),n=Tt(t),c=he(e.trim()),y=de(e);F(a,n),F(c,y),k.relations.push({id1:a,id2:c,title:M.sanitizeText(l,$())})}},"addRelation"),vt=s(function(t,e){const l=k.states.get(t),a=e.startsWith(":")?e.replace(":","").trim():e;l.descriptions.push(M.sanitizeText(a,$()))},"addDescription"),Ue=s(function(t){return t.substring(0,1)===":"?t.substr(2).trim():t.trim()},"cleanupLabel"),Me=s(()=>"divider-id-"+ ++Fe,"getDividerId"),ye=s(function(t,e=""){W.has(t)||W.set(t,{id:t,styles:[],textStyles:[]});const l=W.get(t);e!=null&&e.split(",").forEach(a=>{const n=a.replace(/([^;]*);/,"$1").trim();if(RegExp(Xt).exec(a)){const c=n.replace(Ht,"bgFill").replace(Xt,Ht);l.textStyles.push(c)}l.styles.push(n)})},"addStyleClass"),Xe=s(function(){return W},"getClasses"),Nt=s(function(t,e){t.split(",").forEach(function(l){let a=X(l);if(a===void 0){const n=l.trim();F(n),a=X(n)}a.classes.push(e)})},"setCssClass"),He=s(function(t,e){const l=X(t);l!==void 0&&l.styles.push(e)},"setStyle"),Ve=s(function(t,e){const l=X(t);l!==void 0&&l.textStyles.push(e)},"setTextStyle"),We=s(()=>ae,"getDirection"),Je=s(t=>{ae=t},"setDirection"),Ke=s(t=>t&&t[0]===":"?t.substr(1).trim():t.trim(),"trimColon"),qe=s(()=>{const t=$();return{nodes:ft,edges:Ot,other:{},config:t,direction:ee(Rt())}},"getData"),ss={getConfig:s(()=>$().state,"getConfig"),getData:qe,addState:F,clear:le,getState:X,getStates:ce,getRelations:ze,getClasses:Xe,getDirection:We,addRelation:pe,getDividerId:Me,setDirection:Je,cleanupLabel:Ue,lineType:{LINE:0,DOTTED_LINE:1},relationType:{AGGREGATION:0,EXTENSION:1,COMPOSITION:2,DEPENDENCY:3},logDocuments:je,getRootDoc:Ye,setRootDoc:Pe,getRootDocV2:Rt,extract:Ge,trimColon:Ke,getAccTitle:xe,setAccTitle:Ee,getAccDescription:ke,setAccDescription:Te,addStyleClass:ye,setCssClass:Nt,addDescription:vt,setDiagramTitle:be,getDiagramTitle:_e},is=s(t=>`
defs #statediagram-barbEnd {
    fill: ${t.transitionColor};
    stroke: ${t.transitionColor};
  }
g.stateGroup text {
  fill: ${t.nodeBorder};
  stroke: none;
  font-size: 10px;
}
g.stateGroup text {
  fill: ${t.textColor};
  stroke: none;
  font-size: 10px;

}
g.stateGroup .state-title {
  font-weight: bolder;
  fill: ${t.stateLabelColor};
}

g.stateGroup rect {
  fill: ${t.mainBkg};
  stroke: ${t.nodeBorder};
}

g.stateGroup line {
  stroke: ${t.lineColor};
  stroke-width: 1;
}

.transition {
  stroke: ${t.transitionColor};
  stroke-width: 1;
  fill: none;
}

.stateGroup .composit {
  fill: ${t.background};
  border-bottom: 1px
}

.stateGroup .alt-composit {
  fill: #e0e0e0;
  border-bottom: 1px
}

.state-note {
  stroke: ${t.noteBorderColor};
  fill: ${t.noteBkgColor};

  text {
    fill: ${t.noteTextColor};
    stroke: none;
    font-size: 10px;
  }
}

.stateLabel .box {
  stroke: none;
  stroke-width: 0;
  fill: ${t.mainBkg};
  opacity: 0.5;
}

.edgeLabel .label rect {
  fill: ${t.labelBackgroundColor};
  opacity: 0.5;
}
.edgeLabel {
  background-color: ${t.edgeLabelBackground};
  p {
    background-color: ${t.edgeLabelBackground};
  }
  rect {
    opacity: 0.5;
    background-color: ${t.edgeLabelBackground};
    fill: ${t.edgeLabelBackground};
  }
  text-align: center;
}
.edgeLabel .label text {
  fill: ${t.transitionLabelColor||t.tertiaryTextColor};
}
.label div .edgeLabel {
  color: ${t.transitionLabelColor||t.tertiaryTextColor};
}

.stateLabel text {
  fill: ${t.stateLabelColor};
  font-size: 10px;
  font-weight: bold;
}

.node circle.state-start {
  fill: ${t.specialStateColor};
  stroke: ${t.specialStateColor};
}

.node .fork-join {
  fill: ${t.specialStateColor};
  stroke: ${t.specialStateColor};
}

.node circle.state-end {
  fill: ${t.innerEndBackground};
  stroke: ${t.background};
  stroke-width: 1.5
}
.end-state-inner {
  fill: ${t.compositeBackground||t.background};
  // stroke: ${t.background};
  stroke-width: 1.5
}

.node rect {
  fill: ${t.stateBkg||t.mainBkg};
  stroke: ${t.stateBorder||t.nodeBorder};
  stroke-width: 1px;
}
.node polygon {
  fill: ${t.mainBkg};
  stroke: ${t.stateBorder||t.nodeBorder};;
  stroke-width: 1px;
}
#statediagram-barbEnd {
  fill: ${t.lineColor};
}

.statediagram-cluster rect {
  fill: ${t.compositeTitleBackground};
  stroke: ${t.stateBorder||t.nodeBorder};
  stroke-width: 1px;
}

.cluster-label, .nodeLabel {
  color: ${t.stateLabelColor};
  // line-height: 1;
}

.statediagram-cluster rect.outer {
  rx: 5px;
  ry: 5px;
}
.statediagram-state .divider {
  stroke: ${t.stateBorder||t.nodeBorder};
}

.statediagram-state .title-state {
  rx: 5px;
  ry: 5px;
}
.statediagram-cluster.statediagram-cluster .inner {
  fill: ${t.compositeBackground||t.background};
}
.statediagram-cluster.statediagram-cluster-alt .inner {
  fill: ${t.altBackground?t.altBackground:"#efefef"};
}

.statediagram-cluster .inner {
  rx:0;
  ry:0;
}

.statediagram-state rect.basic {
  rx: 5px;
  ry: 5px;
}
.statediagram-state rect.divider {
  stroke-dasharray: 10,10;
  fill: ${t.altBackground?t.altBackground:"#efefef"};
}

.note-edge {
  stroke-dasharray: 5;
}

.statediagram-note rect {
  fill: ${t.noteBkgColor};
  stroke: ${t.noteBorderColor};
  stroke-width: 1px;
  rx: 0;
  ry: 0;
}
.statediagram-note rect {
  fill: ${t.noteBkgColor};
  stroke: ${t.noteBorderColor};
  stroke-width: 1px;
  rx: 0;
  ry: 0;
}

.statediagram-note text {
  fill: ${t.noteTextColor};
}

.statediagram-note .nodeLabel {
  color: ${t.noteTextColor};
}
.statediagram .edgeLabel {
  color: red; // ${t.noteTextColor};
}

#dependencyStart, #dependencyEnd {
  fill: ${t.lineColor};
  stroke: ${t.lineColor};
  stroke-width: 1;
}

.statediagramTitleText {
  text-anchor: middle;
  font-size: 18px;
  fill: ${t.textColor};
}
`,"getStyles");export{ss as a,ts as b,es as c,is as s};
