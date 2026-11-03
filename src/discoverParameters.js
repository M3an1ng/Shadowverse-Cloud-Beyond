import { unwrapStaticValueConfig } from './components/staticValueConfig.js';
export function isActiveDiscoverParameter(type, parameters, name) {
  if (String(type).toLowerCase() !== 'discover') return true;
  const key=Object.keys(parameters || {}).find(k=>k.toLowerCase()==='mode');
  const raw=parameters?.[key]; const unwrapped=unwrapStaticValueConfig(raw);
  const copy=['copy','1'].includes(String(unwrapped.ok ? unwrapped.value : raw ?? 'Definition').toLowerCase());
  return !(copy ? name.toLowerCase()==='carddefinitionids' : name.toLowerCase()==='copysourcetargets');
}
