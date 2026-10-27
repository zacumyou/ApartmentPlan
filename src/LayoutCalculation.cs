using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
namespace ApartmentPlan {
internal sealed class LayoutInput {
 public List<Point> Boundary,Entrances;public List<Asset> Assets;
 public LayoutOptions Options;public List<Parcel> Ramps;public List<ManualZone> Zones;public List<ManualRoad> Roads;
}
public partial class PlanSystem {
 readonly LatestLayoutWork<Plan> layoutWorker=new LatestLayoutWork<Plan>();
 readonly LayoutDebounce layoutDebounce=new LayoutDebounce();
 internal bool LayoutPending,LayoutOutdated;Action layoutRejected;Plan layoutPrevious;
 void CancelLayout(){layoutDebounce.Cancel();layoutWorker.Cancel();LayoutPending=false;layoutRejected=null;}
 void Rebuild(Action rejected=null){
  if(!Mod.Options.AutoCalculate){
   // Deferred edits stay in the input model. A later failure must not roll back
   // just one edit while silently accepting the others against an older plan.
   CancelLayout();LayoutOutdated=true;Status="AP.CalculationNeeded";Revision++;return;
  }
  // Transactional edits retain their immediate rollback callback. Ordinary
  // changes are sampled only after the user stops editing for one second.
  if(rejected!=null||(!Entrance.HasValue&&!Villa)||!Layout.Valid(Boundary)){CalculateLayout(rejected);return;}
  CancelLayout();LayoutOutdated=true;layoutDebounce.Schedule(UnityEngine.Time.realtimeSinceStartupAsDouble);Status="AP.LayoutWaiting";Revision++;
 }
 void ToggleAutoCalculate(){
  if(NativeCommitted||World.GetExistingSystemManaged<Game.Tools.ToolSystem>()?.activeTool is ConstructionTool)return;
  Mod.Options.AutoCalculate=!Mod.Options.AutoCalculate;Mod.Options.ApplyAndSave();
  if(!Mod.Options.AutoCalculate){
   if(LayoutPending||layoutDebounce.Pending){CancelLayout();LayoutOutdated=true;Status="AP.CalculationNeeded";}
  }else if(!NativeCommitted&&Layout.Valid(Boundary)&&(Entrance.HasValue||Villa))CalculateLayout();
  Revision++;
 }
 void CalculateLayout(Action rejected=null){
  EnsurePlanningCatalog();
  if(!catalogReady||CatalogLoading){Status="AP.CatalogLoading";Revision++;return;}
  EntranceLength=Mod.Options.EntranceLength;ReplacementIndex=-1;CancelLayout();LayoutOutdated=true;
  if((!Entrance.HasValue&&!Villa)||!Layout.Valid(Boundary)){Draft=null;Preview.Clear();Revision++;return;}
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
   // Replay snapshots are opt-in diagnostic files.
   if(Trace.Detailed)try{Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"layout-last-input.json"),json);}catch{}
   var watch=System.Diagnostics.Stopwatch.StartNew();
   var result=Layout.Generate(input.Boundary,input.Entrances,input.Assets,input.Options,input.Ramps,input.Zones,input.Roads);
   token.ThrowIfCancellationRequested();
   if(Trace.Detailed)try{File.WriteAllText(Path.Combine(folder,"layout-last-result.json"),JsonConvert.SerializeObject(new{utc=DateTime.UtcNow,ms=watch.ElapsedMilliseconds,result.LayoutTimings,buildings=result.Parcels.Count(r=>r.Kind==0),parking=result.Parcels.Sum(r=>r.ParkingSpaces),result.Diagnostics,result.ParkingAccessFailures,result.RequiredAccessFailures}));}catch{}
   return result;
  });
 }
 void PollLayout(){
  if(layoutDebounce.Pending){if(commands.Count==0&&layoutDebounce.Take(UnityEngine.Time.realtimeSinceStartupAsDouble)){try{CalculateLayout();}catch(Exception e){CancelLayout();LayoutOutdated=true;Status="AP.Error";Trace.Failure("layout.prepare.failed",e);Revision++;}}return;}

  if(!layoutWorker.Poll(out var result,out var error))return;
  LayoutPending=false;var rejected=layoutRejected;layoutRejected=null;
  if(error!=null){
   failedLayoutStatus=error.Message;
   if(error.Data["layoutReasons"] is System.Collections.Generic.Dictionary<string,int> reasons)layoutReasons=reasons;
   else layoutReasons=new System.Collections.Generic.Dictionary<string,int>();
   Trace.Event("layout.rejection.reasons",JsonConvert.SerializeObject(layoutReasons));
   Trace.Failure("layout.rejected",error);rejected?.Invoke();LayoutOutdated=rejected==null;Draft=layoutPrevious;Preview.Clear();if(Draft!=null)RenderDraft();
   if(error is PlanLimitException limit)SetLimit(limit.Status,limit.Current,limit.Maximum);
   else Status=error is ArgumentException?(error.Message.StartsWith("AP.Tower.",StringComparison.Ordinal)?error.Message:error.Message=="ManualRoadFit"?"AP.ManualRoadFit":error.Message=="EntranceFit"||error.Message=="EntranceSpacing"?"AP.EntranceFit":error.Message=="CurveFit"?"AP.CurveFit":error.Message=="DisconnectedEntrance"?"AP.DisconnectedEntrance":error.Message=="ZoneFit"?"AP.ZoneFit":error.Message=="RampFit"?"AP.RampFit":"AP.InvalidBoundary"):"AP.Error";
  }else{
   failedLayoutInput=null;failedLayoutStatus=null;layoutReasons.Clear();LayoutOutdated=false;Draft=result;RenderDraft();Status=Selected.Count==0?"AP.SelectAssets":Draft.ParkingAccessFailures>0?"AP.ParkingAccessIncomplete":Draft.RequiredAccessFailures>0?(Villa?"AP.Villa.NoConnection":"AP.AccessIncomplete"):!Draft.Parcels.Any(r=>r.Kind==0)?"AP.NoBuildingFit":Draft.VillaPedestrianWarnings>0?"AP.Villa.PedestrianWarning":Draft.VillaRejectedConnections.Count>0?"AP.Villa.RejectedApproach":Draft.Limited?"AP.LimitParcelsTrimmed":Draft.Shortfall.Skip(2).Any(x=>x>1)?"AP.Shortfall":"AP.Ready";
   if(Status=="AP.LimitParcelsTrimmed")SetLimit(Status,Draft.RequestedParcels,Draft.ParcelLimit);
   if(CompoundEditing)SaveCompoundPending(false);
   Trace.Event("layout.end","timings="+JsonConvert.SerializeObject(Draft.LayoutTimings)+" status="+Status+" buildings="+Draft.Parcels.Count(r=>r.Kind==0));
  }
  layoutPrevious=null;Revision++;
 }
}
}
