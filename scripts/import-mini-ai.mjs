import {readFileSync,writeFileSync,mkdirSync,renameSync} from 'node:fs';
import {resolve,dirname} from 'node:path';
import {createHash} from 'node:crypto';
const source=resolve(process.argv[2] || '../Shadowverse_StarCloud/work/ai-training');
const output=resolve(process.argv[3] || 'data/ai-model/model.json');
const read=p=>JSON.parse(readFileSync(p,'utf8'));
const ordered=v=>Array.isArray(v)?v.map(ordered):v&&typeof v==='object'?Object.fromEntries(Object.keys(v).sort().map(k=>[k,ordered(v[k])])):v;
const hash=v=>createHash('sha256').update(JSON.stringify(ordered(v))).digest('hex');
const live=read(resolve(source,'live-policy.json'));
function unpack(value,kind,snapshot){
 const key=kind==='policies'?'policyHash':'teacherHash';
 if(!value?.[key])return value;
 if(!/^[a-f0-9]{64}$/.test(value[key]))throw Error('Invalid model reference');
 const data=read(resolve(source,snapshot,kind,value[key]+'.json'));
 if(hash(data)!==value[key])throw Error('Model checksum mismatch');
 return data;
}
const models={};
function store(model){const key=hash(model);models[key]=model;return key;}
function weights(raw,snapshot){
 const w=unpack(raw,'policies',snapshot), result={};
 for(const key of ['board','face','survival','cards','resource','tempo','utility','neuralValueScale','learningVersion','valueModel','actionPolicy'])if(w[key]!==undefined)result[key]=['valueModel','actionPolicy'].includes(key)?store(w[key]):w[key];
 if(w.valueTeacher)result.valueTeacher=store(unpack(w.valueTeacher,'teachers',snapshot));
 return result;
}
const root=unpack(live.weights,'policies',live.snapshotId), specialists={};
for(const [key,head] of Object.entries(root.deckPolicies || {}))specialists[key]={...weights(root,live.snapshotId),...(head.residual?{valueModel:store(head.residual)}:{}),...(head.teacher?{valueTeacher:store(unpack(head.teacher,'teachers',live.snapshotId))}:{})};
for(const [key,route] of Object.entries(root.deckRoutes || {}))specialists[key]=weights(route,live.specialistReleases?.[key]?.snapshotId || live.snapshotId);
const model={format:'svnebula-native-ai-1',version:live.version,importedAt:new Date().toISOString(),sourcePublishedAt:live.publishedAt,weights:weights(root,live.snapshotId),specialists,models};
const used=new Set([model.weights,...Object.values(specialists)].flatMap(w=>[w.valueModel,w.valueTeacher,w.actionPolicy]).filter(Boolean));for(const key of Object.keys(models))if(!used.has(key))delete models[key];
mkdirSync(dirname(output),{recursive:true});writeFileSync(output+'.tmp',JSON.stringify(model));renameSync(output+'.tmp',output);
console.log(JSON.stringify({output,bytes:readFileSync(output).length,version:model.version,specialists:Object.keys(specialists).length,sha256:createHash('sha256').update(readFileSync(output)).digest('hex')}));
