const fs=require('fs'),assert=require('assert');
const read=p=>fs.readFileSync(p,'utf8');
assert(read('src/Settings.cs').includes('DebugLogging{get;set;}=false'));
assert(read('src/Mod.cs').includes('Options.DebugLogging=false;'));
assert(read('src/Trace.cs').includes('TracePolicy.Keep(action,Detailed,failure)'));
assert(read('src/PlanEditing.cs').includes('pair.Key!="DebugLogging"'));
console.log('Release logging defaults verified.');

assert(read('src/LayoutCalculation.cs').includes('#if AP_LOCAL_TIMING'));
assert(read('src/LayoutCalculation.cs').includes('AssemblyMetadata("ApartmentPlan.LocalLayoutTiming", "false")'));
assert(read('build.ps1').includes('[switch]$LocalDiagnostics'));
assert(read('build.ps1').includes('-p:LocalLayoutTiming='));
assert(read('build.ps1').includes('$localTimingEnabled=-not $PublicRelease'));
assert(read('build.ps1').includes('if($PublicRelease -and $LocalDiagnostics)'));
console.log('Local timing defaults ON; explicit public release excludes timing.');
