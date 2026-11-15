using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
// Worker-owned, bounded numeric evidence. Never stores names, coordinates or replay text.
internal sealed class PlanningObservation {
 internal static readonly string[] Stages={"validation","entranceCourts","roads","candidates","packing","routing","frontJoins","overlayRibbons","walks","pruning","curves","parks","summary"};
 internal static readonly string[] Metrics={"version","planMode","outcome","stage","totalMs","boundaryPoints","entrances","assets","zones","manualRoads","roadCm","aisleCm","assetMinWidthCm","assetMaxWidthCm","assetMinDepthCm","assetMaxDepthCm","villaRows","villaMesh","villaCentralRoad","villaFollowBoundary","villaGapCm","villaBoundaryCm","villaMainSpacingCm","villaMaxBuildings","villaConnections","existingRoads","parkingWidthCm","parkingDepthCm","attempted","boundaryRejected","courtRejected","frontRejected","roadRejected","candidates","packed","connections","rejectedConnections","gridPieces","routeAttempts","unreachable","searchCalls","searchNodes","searchVisits","searchLimits","rejectTurnGap","rejectBoundary","rejectBuilding","rejectExcluded","rejectGateBranch","rejectParallel","rejectSearch","buildings","routes","curves","parcels","accessFailures","parkingFailures","villaThrough","villaEmptyBlocks","villaExistingOnly"};
 internal static readonly HashSet<string> Allowed=new HashSet<string>(Metrics.Concat(Stages.Select(s=>s+"Ms")).Concat(TowerRules.Definitions.Select(r=>"tower"+r.Id+"X100")));
 readonly Dictionary<string,int> values=new Dictionary<string,int>();
 readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();long phase;string stage;
 internal PlanningObservation(LayoutOptions o,int boundary,int entrances,IList<Asset> assets,int zones,int roads){
  Set("version",1);Set("planMode",o.Tower?2:o.Villa?1:0);Set("boundaryPoints",boundary);Set("entrances",entrances);Set("assets",assets.Count);Set("zones",zones);Set("manualRoads",roads);Set("roadCm",o.Road*100);Set("aisleCm",o.ParkingAisle*100);
  if(assets.Count>0){Set("assetMinWidthCm",assets.Min(a=>a.Width)*100);Set("assetMaxWidthCm",assets.Max(a=>a.Width)*100);Set("assetMinDepthCm",assets.Min(a=>a.Depth)*100);Set("assetMaxDepthCm",assets.Max(a=>a.Depth)*100);}
  if(o.Villa){Set("villaRows",o.VillaRows);Set("villaMesh",o.VillaMesh?1:0);Set("villaCentralRoad",o.VillaCentralRoad?1:0);Set("villaFollowBoundary",o.VillaFollowBoundary?1:0);Set("villaGapCm",o.VillaGap*100);Set("villaBoundaryCm",o.VillaBoundary*100);Set("villaMainSpacingCm",o.VillaMainSpacing*100);Set("villaMaxBuildings",o.VillaMaxBuildings);Set("villaConnections",o.VillaConnections.Count);Set("existingRoads",o.VillaExistingRoads?.Count??0);}
  if(o.Tower){foreach(var r in TowerRules.Definitions)Set("tower"+r.Id+"X100",(o.TowerRules??new TowerRules())[r.Id]*100);Set("parkingWidthCm",(o.TowerParking?.Width??0)*100);Set("parkingDepthCm",(o.TowerParking?.Depth??0)*100);}
  Stage("validation");
 }
 internal void Set(string key,double value){if(Allowed.Contains(key)&&!double.IsNaN(value)&&!double.IsInfinity(value))values[key]=(int)Math.Max(int.MinValue,Math.Min(int.MaxValue,Math.Round(value)));}
 internal void Add(string key){Set(key,(values.TryGetValue(key,out int v)?(double)v:0)+1);}
 internal void Stage(string next){CloseStage();stage=next;Set("stage",Array.IndexOf(Stages,next)+1);phase=clock.ElapsedMilliseconds;}
 void CloseStage(){if(stage!=null){string key=stage+"Ms";Set(key,(values.TryGetValue(key,out int v)?v:0)+clock.ElapsedMilliseconds-phase);}}
 internal void Result(Plan p){Set("buildings",p.Parcels.Count(q=>q.Kind==0));Set("routes",p.Routes.Count+p.ConstructionRoutes.Count);Set("curves",p.Routes.Sum(r=>r.Curves?.Count??0));Set("parcels",p.Parcels.Count);Set("accessFailures",p.RequiredAccessFailures);Set("parkingFailures",p.ParkingAccessFailures);if(p.Villa){Set("villaThrough",p.VillaThrough?1:0);Set("villaEmptyBlocks",p.VillaEmptyBlocks);Set("villaExistingOnly",p.VillaExistingRoadsOnly?1:0);}}
 internal Dictionary<string,int> Finish(int outcome){CloseStage();stage=null;Set("outcome",outcome);Set("totalMs",clock.ElapsedMilliseconds);return new Dictionary<string,int>(values);}
}
}
