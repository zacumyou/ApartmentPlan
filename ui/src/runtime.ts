// Gameface capabilities differ from desktop browsers.
export function uiNow():number{
 return typeof performance!=='undefined'&&typeof performance.now==='function'?performance.now():Date.now();
}
