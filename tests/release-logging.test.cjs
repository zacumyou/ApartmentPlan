const fs=require('fs'),assert=require('assert');
const read=p=>fs.readFileSync(p,'utf8');
assert(read('src/Settings.cs').includes('DebugLogging{get;set;}=false'));
assert(read('src/Mod.cs').includes('Options.DebugLogging=false;'));
assert(read('src/Trace.cs').includes('TracePolicy.Keep(action,Detailed,failure)'));
assert(read('src/PlanEditing.cs').includes('pair.Key!="DebugLogging"'));
console.log('Release logging defaults verified.');
