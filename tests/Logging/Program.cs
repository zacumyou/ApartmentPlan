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

if(!OverlayDetailPolicy.Visible(4,100,1000,false,3)||OverlayDetailPolicy.Visible(4,2000,1000,false,3))throw new Exception("Perspective detail LOD");
if(!OverlayDetailPolicy.Visible(4,2000,1,true,3)||OverlayDetailPolicy.Visible(2,1,1,true,3))throw new Exception("Orthographic detail LOD");
if(!OverlayDetailPolicy.Visible(4,-10,1000,false,3)||!OverlayDetailPolicy.Visible(4,100,0,false,3))throw new Exception("Conservative camera fallback");
Console.WriteLine("PASS overlay detail policy: perspective/orthographic distance and conservative fallback.");

var lookup=DiagnosticException.Record(DiagnosticException.ParkingLookup(262,271,3,5,12.34,8,8),3);
if(lookup["edges"]!=3||lookup["ownerKind"]!=5||lookup["coverageCm"]!=1234||lookup["marginCm"]!=800)throw new Exception("Parking rejection evidence lost");
var emptyLookup=DiagnosticException.Record(DiagnosticException.ParkingLookup(0,1,0,0,float.MaxValue,0,0),3);
if(emptyLookup["coverageCm"]!=-1||emptyLookup["edges"]!=0)throw new Exception("Unknown distance must not overflow or appear as zero");
Console.WriteLine("PASS parking lookup evidence: candidate count, rejection mask, bounded distances and unknown sentinel.");

if(OverlayDetailPolicy.Visible(.12,100,500,false,.75)||!OverlayDetailPolicy.Visible(.12,50,500,false,.75))throw new Exception("Subpixel parking line cutoff");
if(OverlayDetailPolicy.TreeStride(309)!=4||OverlayDetailPolicy.TreeStride(60)!=1)throw new Exception("Tree detail budget");
Console.WriteLine("PASS detail reduction: 309 far-tree markers -> 78, close trees unthinned; subpixel parking line omitted.");
