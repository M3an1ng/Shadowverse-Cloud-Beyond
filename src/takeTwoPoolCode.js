const classNames = ['Forestcraft','Swordcraft','Runecraft','Dragoncraft','Abysscraft','Havencraft','Portalcraft'];
const invalid = () => { throw new Error('卡池分享代码无效或版本不受支持'); };
export function validateSharedPool(pool) {
  if (!pool || typeof pool !== 'object' || Array.isArray(pool)) return invalid();
  const ids = value => {
    if (!Array.isArray(value) || value.length > 10000 || value.some(id => !Number.isSafeInteger(id) || id <= 0)) return invalid();
    return [...new Set(value)];
  };
  if (!Array.isArray(pool.packs) || pool.packs.length > 500 || pool.packs.some(p=>typeof p !== 'string' || !p.trim() || p.length>100)) return invalid();
  const includedIds=ids(pool.includedIds), excludedIds=ids(pool.excludedIds);
  const excluded = new Set(excludedIds);
  if (includedIds.some(id=>excluded.has(id))) return invalid();
  const redraws={}, neutralByClass={};
  for (const [key,value] of Object.entries(pool.redraws ?? {})) {
    if (!classNames.includes(key) || !Number.isInteger(value) || value<0 || value>20) return invalid();
    redraws[key]=value;
  }
  for (const [key,value] of Object.entries(pool.neutralByClass ?? {})) {
    if (!classNames.includes(key)) return invalid();
    neutralByClass[key]=ids(value);
  }
  return {packs:[...new Set(pool.packs)],includedIds,excludedIds,redraws,neutralByClass};
}
export function exportPoolCode(pool) {
  const bytes=new TextEncoder().encode(JSON.stringify({version:1,pool:validateSharedPool(pool)}));
  let binary=''; for (const byte of bytes) binary+=String.fromCharCode(byte);
  return 'SVPOOL1:'+btoa(binary);
}
export function importPoolCode(code) {
  try {
    const text=String(code).trim();
    if (text.length>2000000 || !text.startsWith('SVPOOL1:')) return invalid();
    const bytes=Uint8Array.from(atob(text.slice(8).replace(/\s/g,'')),c=>c.charCodeAt(0));
    const data=JSON.parse(new TextDecoder('utf-8',{fatal:true}).decode(bytes));
    if (data.version!==1) return invalid();
    return validateSharedPool(data.pool);
  } catch { return invalid(); }
}
