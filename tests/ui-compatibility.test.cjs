const fs=require('fs'),path=require('path'),assert=require('assert'),vm=require('vm');
const ts=require('../ui/node_modules/typescript');
const root=path.join(__dirname,'..');
const languages=['en-US','ko-KR'].map(n=>JSON.parse(fs.readFileSync(path.join(root,'lang',n+'.json'),'utf8')));
const keys=new Set();
const prefixes=new Set(['AP.','AP.Step.','AP.Snap.','AP.UI.Tab.','AP.Network.','AP.CornerMode.','AP.BuildingPattern.','AP.Zone.','AP.Kind.','AP.TreePlacement','AP.TreeAge','AP.Tower.Rule.','AP.Tower.','AP.Option.','AP.Desc.']);
for(const folder of ['ui/src','src'])for(const name of fs.readdirSync(path.join(root,folder))){
 if(!/\.(tsx?|cs)$/.test(name))continue;
 const source=fs.readFileSync(path.join(root,folder,name),'utf8');
 for(const match of source.matchAll(/["'](AP\.[A-Za-z0-9_.]*)["']/g)){
  const key=match[1];if(!prefixes.has(key))keys.add(key);
 }
 if(folder==='ui/src')for(const match of source.matchAll(/action\('([^']+)'/g))keys.add('AP.'+match[1]);
}
for(const [prefix,values] of [['AP.TreePlacement',[0,1,2]],['AP.TreeAge',[0,1,2,3]],['AP.Network.',['main','aisle','pedestrian']],['AP.UI.Tab.',['assets','rules','summary','saved']]])for(const value of values)keys.add(prefix+value);
for(const [i,lang] of languages.entries())for(const key of keys)assert(typeof lang[key]==='string'&&lang[key].trim()&&lang[key]!==key,`${i===0?'en-US':'ko-KR'} missing ${key}`);
const runtime=ts.transpileModule(fs.readFileSync(path.join(root,'ui/src/runtime.ts'),'utf8'),{compilerOptions:{module:ts.ModuleKind.CommonJS}}).outputText;
for(const perf of [undefined,{}, {now:()=>123}]){
 const context={exports:{},performance:perf,Date:{now:()=>456}};vm.runInNewContext(runtime,context);
 assert.equal(context.exports.uiNow(),perf&&perf.now?123:456);
}
const asset=ts.transpileModule(fs.readFileSync(path.join(root,'ui/src/asset-name.ts'),'utf8'),{compilerOptions:{module:ts.ModuleKind.CommonJS}}).outputText;
const context={exports:{},Intl:undefined};vm.runInNewContext(asset,context);
assert(context.exports.assetMatches({Id:'Road10',Name:'아파트 A'},'아파트'));
assert(context.exports.assetMatches({Id:'Road10',Name:'아파트 A'},'ROAD'));
assert.equal(context.exports.assetName({Id:'x',Name:'Fallback'},()=>null),'Fallback');
console.log(`PASS ${keys.size} UI/backend translation keys in both languages; clock fallbacks and asset search without Intl`);
