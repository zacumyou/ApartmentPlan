using System;
using ApartmentPlan;
foreach(var name in new[]{"snap.hit","binding.state","ui.command","layout.queued","overlay.draw"}){if(TracePolicy.Keep(name,false)||!TracePolicy.Keep(name,true))throw new Exception(name);}
foreach(var name in new[]{"session.start","session.end","save.complete","plan.construction.recorded","construction.AP.NativeBlocked","construction.failure.metrics","layout.networkUnavailable","ui.render.error","unity.Exception"})if(!TracePolicy.Keep(name,false))throw new Exception(name);
if(!TracePolicy.Keep("preferences.restore",false,true))throw new Exception("Exception lost");
Console.WriteLine("Logging policy passed: detailed events opt-in; failures and results retained.");
