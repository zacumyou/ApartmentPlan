using System;
using ApartmentPlan;
foreach(var name in new[]{"snap.hit","binding.state","ui.command","layout.queued","overlay.draw"}){if(TracePolicy.Keep(name,false)||!TracePolicy.Keep(name,true))throw new Exception(name);}
foreach(var name in new[]{"session.start","session.end","save.complete","plan.construction.recorded","construction.AP.NativeBlocked","construction.failure.metrics","layout.networkUnavailable","ui.render.error","unity.Exception"})if(!TracePolicy.Keep(name,false))throw new Exception(name);
if(!TracePolicy.Keep("preferences.restore",false,true))throw new Exception("Exception lost");
Console.WriteLine("Logging policy passed: detailed events opt-in; failures and results retained.");

var failure=DiagnosticException.Parking("private path C:/secret asset-name",52,17,537);
var record=DiagnosticException.Record(failure,3);
if(record["reason"]!=52||record["ordinal"]!=17||record["connected"]!=537||record["flags"]!=3||record["kind"]!=2)throw new Exception("Parking failure evidence lost");
if(DiagnosticException.Record(new NullReferenceException("private"),3)["kind"]!=3||DiagnosticException.Record(new Exception(),1)["reason"]!=50)throw new Exception("Unexpected exception category lost");
failure.Data["apReason"]=999;
if(DiagnosticException.Record(failure,3)["reason"]!=50)throw new Exception("Unrecognized reason accepted");
Console.WriteLine("PASS exception evidence: numeric fields only, parking reasons/index/count and generic fallback; no exception text.");
