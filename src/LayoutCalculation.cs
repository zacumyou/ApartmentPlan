using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
#if AP_LOCAL_TIMING
[assembly: System.Reflection.AssemblyMetadata("ApartmentPlan.LocalLayoutTiming", "true")]
#else
[assembly: System.Reflection.AssemblyMetadata("ApartmentPlan.LocalLayoutTiming", "false")]
#endif
namespace ApartmentPlan {
internal sealed class LayoutInput {
 public List<Point> Boundary,Entrances;public List<Asset> Assets;
 public LayoutOptions Options;public List<Parcel> Ramps;public List<ManualZone> Zones;public List<ManualRoad> Roads;
}
public partial class PlanSystem {
 readonly LatestLayoutWork<Plan> layoutWorker=new LatestLayoutWork<Plan>();
 readonly LayoutDebounce layoutDebounce=new LayoutDebounce();
 Dictionary<string,int> lastPlanning;
 internal bool LayoutPending,LayoutOutdated;Action layoutRejected;Plan layoutPrevious;
 void CancelLayout(){lastPlanning=null;layoutDebounce.Cancel();layoutWorker.Cancel();LayoutPending=false;layoutRejected=null;}
 void ScheduleLayout()=>layoutDebounce.Schedule(UnityEngine.Time.realtimeSinceStartupAsDouble);
 void ToggleAutoCalculate(){
  if(NativeCommitted||World.GetExistingSystemManaged<Game.Tools.ToolSystem>()?.activeTool is ConstructionTool)return;
  ChangeAutoCalculation();
 }
 void CalculateLayout(Action rejected=null){
  EnsurePlanningCatalog();
  if(!catalogReady||CatalogLoading){Status="AP.CatalogLoading";Revision++;return;}
  EntranceLength=Mod.Options.EntranceLength;ReplacementIndex=-1;CancelLayout();LayoutOutdated=true;
  if(Entrances.Count==0||!Layout.Valid(Boundary)){Draft=null;Preview.Clear();LayoutOutdated=false;Status=Mode=="entrance"?"AP.PickEntrance":"AP.Draw";Revision++;return;}
  if(!World.GetOrCreateSystemManaged<InvisibleRoadPrefabs>().Available){Status="AP.RoadPackMissing";Revision++;return;}
  var existingVillaRoads=Villa?CollectVillaExistingRoads():new List<PlanRoute>();
  // Missing initialization must never silently turn into mismatched widths
  // and thousands of rejected housing candidates.
  string networkFolder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)+"Low","Colossal Order","Cities Skylines II","Logs","ApartmentPlan");
  if(Trace.Detailed){var networkReport=JsonConvert.SerializeObject(new{utc=DateTime.UtcNow,main=Mod.Options.MainNetwork,mainReady=Network("main")!=null,aisle=Mod.Options.AisleNetwork,aisleReady=Network("aisle")!=null,audit=World.GetOrCreateSystemManaged<InvisibleRoadPrefabs>().Diagnostic()});
  if(Trace.Detailed)try{Directory.CreateDirectory(networkFolder);File.WriteAllText(Path.Combine(networkFolder,"layout-network-state.json"),networkReport);}catch(Exception e){Trace.Failure("layout.diagnostic.write",e);}}
  if(existingVillaRoads.Count==0&&((!Villa||Mod.Options.VillaCentralRoad)&&Network("main")==null||Network("aisle")==null||!ApartmentAisle(Network("aisle")))){Status="AP.NetworkUnavailable";Trace.Event("layout.networkUnavailable","main="+Mod.Options.MainNetwork+" found="+(Network("main")!=null)+" aisle="+Mod.Options.AisleNetwork+" found="+(Network("aisle")!=null));Revision++;return;}
  if(Tower&&(!Assets.Any(a=>a.Id==Mod.Options.TowerParkingAsset))){Status="AP.Tower.SelectParking";Revision++;return;}
  if(Villa)Selected.RemoveWhere(id=>Assets.Any(a=>a.Id==id&&VillaLayout.Excluded(a)));
  foreach(var asset in Assets.Where(a=>Selected.Contains(a.Id)))EnsureAccessProfile(asset);
  if(Villa){if(VillaConnections.Any(c=>c.Tangent.Length<.1&&(c.Arms==null||c.Arms.Count==0)))DetectVillaConnections();SyncVillaEntrances();}
  var input=new LayoutInput{Boundary=Boundary,Entrances=Entrances,Assets=Assets.Where(a=>Selected.Contains(a.Id)).ToList(),Options=ConstructionOptions(),Ramps=Ramps,Zones=Zones,Roads=Roads};
  if(Villa)input.Options.VillaExistingRoads=existingVillaRoads;
  layoutReasons.Clear();failedLayoutStatus=null;failedLayoutInput=null;
  var json=JsonConvert.SerializeObject(input);input=JsonConvert.DeserializeObject<LayoutInput>(json);failedLayoutInput=json;
  string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)+"Low","Colossal Order","Cities Skylines II","Logs","ApartmentPlan");
  layoutPrevious=Draft;layoutRejected=rejected;LayoutPending=true;Status="AP.Calculating";Revision++;
  Trace.Event("layout.queued","points="+input.Boundary.Count+" assets="+input.Assets.Count);
  layoutWorker.Submit(token=>{
   input.Options.Cancellation=token;
#if AP_LOCAL_TIMING
   input.Options.FunctionTiming=new LayoutFunctionTiming();
   var timingId=Guid.NewGuid().ToString("N").Substring(0,8);
   input.Options.Timing=(stage,ms)=>Trace.Event("layout.localTiming", "run="+timingId+" stage="+stage+" ms="+ms,true);
   input.Options.Diagnostic=line=>Trace.Event("layout.localTower","run="+timingId+" "+line,true);
   input.Options.Timing("start",0);
   // Local-only fingerprint compares complete inputs without logging coordinates or asset names.
   using(var digest=System.Security.Cryptography.SHA256.Create()){
    var fingerprint=BitConverter.ToString(digest.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json))).Replace("-","");
    Trace.Event("layout.localInput","run="+timingId+" hash="+fingerprint+" mode="+(input.Options.Tower?"tower":input.Options.Villa?"villa":"slab")+" points="+input.Boundary.Count+" assets="+input.Assets.Count+" entrances="+input.Entrances.Count+" roads="+(input.Roads?.Count??0)+" zones="+(input.Zones?.Count??0),true);
   }
#endif
   // Replay snapshots are opt-in diagnostic files.
   if(Trace.Detailed)try{Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"layout-last-input.json"),json);}catch{}
   var watch=System.Diagnostics.Stopwatch.StartNew();
   Plan result;
   try{result=Layout.Generate(input.Boundary,input.Entrances,input.Assets,input.Options,input.Ramps,input.Zones,input.Roads);token.ThrowIfCancellationRequested();}
   catch(Exception error){input.Options.Timing?.Invoke(error is OperationCanceledException?"cancelled":"failed",watch.ElapsedMilliseconds);throw;}
   finally{
    watch.Stop(); // Exclude aggregate log output from calculation wall time.
#if AP_LOCAL_TIMING
    input.Options.FunctionTiming.Flush(line=>Trace.Event("layout.localFunction","run="+timingId+" "+line,true));
#endif
   }
   input.Options.Timing?.Invoke("total",watch.ElapsedMilliseconds);
   if(Trace.Detailed)try{File.WriteAllText(Path.Combine(folder,"layout-last-result.json"),JsonConvert.SerializeObject(new{utc=DateTime.UtcNow,ms=watch.ElapsedMilliseconds,result.LayoutTimings,buildings=result.Parcels.Count(r=>r.Kind==0),parking=result.Parcels.Sum(r=>r.ParkingSpaces),result.Diagnostics,result.ParkingAccessFailures,result.RequiredAccessFailures}));}catch{}
   return result;
  });
 }
 void PollLayout(){
  if(layoutDebounce.Pending){if(commands.Count==0&&layoutDebounce.Take(UnityEngine.Time.realtimeSinceStartupAsDouble)){try{CalculateLayout();}catch(Exception e){CancelLayout();LayoutOutdated=true;Status="AP.Error";Trace.Failure("layout.prepare.failed",e);Revision++;}}return;}

  if(!layoutWorker.Poll(out var result,out var error))return;
  lastPlanning=error?.Data["planning"] as Dictionary<string,int>??result?.Planning;
  LayoutPending=false;var rejected=layoutRejected;layoutRejected=null;
  #if AP_LOCAL_TIMING
  if(lastPlanning!=null)Trace.Event("layout.localPlanning",JsonConvert.SerializeObject(lastPlanning),true);
#endif
  if(error!=null){
   failedLayoutStatus=error.Message;
   if(error.Message=="EntranceFit"||error.Message=="EntranceSpacing"){
    EntranceEvidence.Clear();
    if(error.Data["entranceEvidence"] is Dictionary<string,int> entrance)EntranceEvidence.Add(new Dictionary<string,int>(entrance));
    Trace.Event("layout.failure.entrance",JsonConvert.SerializeObject(EntranceEvidence));
   }
   if(error.Message=="CurveFit"){
    CurveEvidence.Clear();
    if(error.Data["curveEvidence"] is Dictionary<string,int> curve)CurveEvidence.Add(new Dictionary<string,int>(curve));
    Trace.Event("layout.failure.curve",JsonConvert.SerializeObject(CurveEvidence));
   }
   if(error.Data["layoutReasons"] is System.Collections.Generic.Dictionary<string,int> reasons)layoutReasons=reasons;
   else layoutReasons=new System.Collections.Generic.Dictionary<string,int>();
   Trace.Event("layout.rejection.reasons",JsonConvert.SerializeObject(layoutReasons));
   if(error is ArgumentException roadError&&error.Message=="ManualRoadFit")RecordManualRoadFailure(roadError);
   Trace.Failure("layout.rejected",error);rejected?.Invoke();LayoutOutdated=rejected==null;Draft=layoutPrevious;Preview.Clear();if(Draft!=null)RenderDraft();
   if(error is PlanLimitException limit)SetLimit(limit.Status,limit.Current,limit.Maximum);
   else Status=error is ArgumentException?(error.Message.StartsWith("AP.Tower.",StringComparison.Ordinal)?error.Message:error.Message=="ManualRoadFit"?"AP.ManualRoadFit":error.Message=="EntranceFit"||error.Message=="EntranceSpacing"?"AP.EntranceFit":error.Message=="CurveFit"?"AP.CurveFit":error.Message=="DisconnectedEntrance"?"AP.DisconnectedEntrance":error.Message=="ZoneFit"?"AP.ZoneFit":error.Message=="RampFit"?"AP.RampFit":"AP.InvalidBoundary"):"AP.Error";
  }else{
   failedLayoutInput=null;failedLayoutStatus=null;layoutReasons.Clear();LayoutOutdated=false;Draft=result;RenderDraft();Status=PlanDiagnostics.Status(Draft,Selected.Count>0,Villa);
   if(Status=="AP.LimitParcelsTrimmed")SetLimit(Status,Draft.RequestedParcels,Draft.ParcelLimit);
   if(CompoundEditing)SaveCompoundPending(false);
   Trace.Event("layout.end","timings="+JsonConvert.SerializeObject(Draft.LayoutTimings)+" status="+Status+" buildings="+Draft.Parcels.Count(r=>r.Kind==0));
  }
  layoutPrevious=null;Revision++;
 }
}
}
