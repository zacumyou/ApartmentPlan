using System;
using System.Collections.Generic;
using System.Linq;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;
namespace ApartmentPlan {
public sealed class NetworkChoice {
 public string Id,Icon,Name;public bool RoadBuilder,Builtin;public float Width;public bool Path,Car,Walk,BuildingAccess,TwoWay;
 public bool Internal=>PlanSystem.ApartmentAisle(this);
 [Newtonsoft.Json.JsonIgnore] public Entity Entity;
}
public partial class PlanSystem {
 internal const string ParkingDecalId="CSKRParkingDecalClosed1x";
 internal double ParkingPitch=>DecalWidth-Math.Max(0,Math.Min(.3,Mod.Options.ParkingLineOverlap));
 internal double DecalWidth=3,DecalDepth=5.97292232;
 internal readonly List<NetworkChoice> Networks=new List<NetworkChoice>();
 int networkRevision=-1;
 // The planner can also be opened from a saved compound's demolition flow,
 // without ever visiting the menu that originally populated these catalogs.
 void EnsurePlanningCatalog(){
  if(!Ready||Mod.Options==null)return;
  if(!catalogReady||Networks.Count==0||networkRevision!=World.GetOrCreateSystemManaged<InvisibleRoadPrefabs>().Revision)Catalog();
 }
 void CatalogNetworks(){if(catalogWork==null)StartCatalog(false);}
 IEnumerable<bool> ScanNetworks(){
  var choices=new List<NetworkChoice>();var ids=new HashSet<string>(StringComparer.Ordinal);var revision=World.GetOrCreateSystemManaged<InvisibleRoadPrefabs>().Revision;var prefabs=World.GetOrCreateSystemManaged<PrefabSystem>();var copies=World.GetOrCreateSystemManaged<InvisibleRoadPrefabs>();
  Entity[] snapshot;using(var query=EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetData>(),ComponentType.ReadOnly<NetGeometryData>()))
  using(var entities=query.ToEntityArray(Allocator.Temp))snapshot=entities.ToArray();
  foreach(var entity in snapshot){
   yield return true;if(!EntityManager.Exists(entity))continue;
   bool road=EntityManager.HasComponent<RoadData>(entity),path=EntityManager.HasComponent<PathwayData>(entity);
   if(!road&&!path||!prefabs.TryGetPrefab<PrefabBase>(entity,out var prefab)||prefab==null)continue;
   if(prefab.name.StartsWith(InvisibleRoadPrefabs.Prefix+"Invisible ",StringComparison.Ordinal)&&!copies.IsReady(prefab.name))continue;
   bool rb=prefab.GetType().FullName=="RoadBuilder.Domain.Prefabs.RoadBuilderPrefab";
   if(rb&&prefab.GetType().GetProperty("Deleted")?.GetValue(prefab) is bool deleted&&deleted)continue;
   string displayName=null;
   if(rb){var config=prefab.GetType().GetProperty("Config")?.GetValue(prefab);displayName=config?.GetType().GetProperty("Name")?.GetValue(config) as string;}
   var width=EntityManager.GetComponentData<NetGeometryData>(entity).m_DefaultWidth;
   if(!Unity.Mathematics.math.isfinite(width)||width<=0||width>100)continue;
   if(prefabs.GetEntity(prefab)!=entity||!ids.Add(prefab.name))continue;
   bool car=false,walk=false;var pending=new List<PrefabBase>();var visited=new HashSet<PrefabBase>();pending.Add(prefab);
   while(pending.Count>0){yield return true;var p=pending[pending.Count-1];pending.RemoveAt(pending.Count-1);if(p==null||!visited.Add(p))continue;
    if(p is NetLanePrefab){var lane=p.GetComponent<CarLane>();car|=lane!=null&&(lane.m_RoadType&Game.Net.RoadTypes.Car)!=0;walk|=p.GetComponent<PedestrianLane>()!=null;continue;}
    // PrefabBase.GetDependencies does not traverse its attached components.
    // Net-piece lane references live in those components.
    var children=new List<PrefabBase>();p.GetDependencies(children);
    foreach(var component in p.components)if(component!=null&&component.active)component.GetDependencies(children);
    foreach(var child in children)if(child is NetGeometryPrefab||child is NetSectionPrefab||child is NetPiecePrefab||child is NetLanePrefab)pending.Add(child);
   }
   if(!car&&!walk)continue;
   bool twoWay=false;if(road)GroundRoadCapabilities(entity,out car,out walk,out twoWay);
   bool buildingAccess=false;var net=EntityManager.GetComponentData<NetData>(entity);
   if(net.m_EdgeArchetype.Valid)using(var types=net.m_EdgeArchetype.GetComponentTypes(Allocator.Temp))foreach(var type in types)buildingAccess|=type==ComponentType.ReadWrite<Game.Buildings.ConnectedBuilding>();
   choices.Add(new NetworkChoice{BuildingAccess=buildingAccess,TwoWay=twoWay,Id=prefab.name,Name=displayName,RoadBuilder=rb,Builtin=!rb&&prefab.isBuiltin,Icon=prefab.GetComponent<UIObject>()?.m_Icon??"",Width=width,Path=!road,Car=car,Walk=walk,Entity=entity});
  }
  Networks.Clear();Networks.AddRange(choices);networkRevision=revision;
  yield return true;
 }
 // Use active ground composition, not optional lanes found anywhere in prefab dependencies.
 void GroundRoadCapabilities(Entity entity,out bool car,out bool walk,out bool twoWay){
  car=walk=twoWay=false;if(!EntityManager.HasBuffer<NetGeometrySection>(entity))return;
  using(var pieces=new NativeList<NetCompositionPiece>(32,Allocator.Temp))
  using(var lanes=new NativeList<NetCompositionLane>(32,Allocator.Temp)){
   NetCompositionHelpers.GetCompositionPieces(pieces,EntityManager.GetBuffer<NetGeometrySection>(entity,true).AsNativeArray(),default(CompositionFlags),GetBufferLookup<NetSubSection>(true),GetBufferLookup<NetSectionPiece>(true));
   var composition=default(NetCompositionData);
   NetCompositionHelpers.CalculateCompositionData(ref composition,pieces.AsArray(),GetComponentLookup<NetPieceData>(true),GetComponentLookup<NetVertexMatchData>(true));
   NetCompositionHelpers.AddCompositionLanes(Entity.Null,ref composition,pieces,lanes,default(DynamicBuffer<NetCompositionCarriageway>),GetComponentLookup<NetLaneData>(true),GetBufferLookup<NetPieceLane>(true));
   bool forward=false,backward=false;
   foreach(var lane in lanes){
    if((lane.m_Flags&(Game.Prefabs.LaneFlags.DisconnectedStart|Game.Prefabs.LaneFlags.DisconnectedEnd|Game.Prefabs.LaneFlags.Virtual|Game.Prefabs.LaneFlags.PublicOnly))!=0)continue;
    walk|=(lane.m_Flags&Game.Prefabs.LaneFlags.Pedestrian)!=0;
    if(!EntityManager.HasComponent<CarLaneData>(lane.m_Lane)||(EntityManager.GetComponentData<CarLaneData>(lane.m_Lane).m_RoadTypes&Game.Net.RoadTypes.Car)==0)continue;
    var data=EntityManager.GetComponentData<NetLaneData>(lane.m_Lane);
    if(!EntityManager.HasComponent<PathfindCarData>(data.m_PathfindPrefab))continue;
    car=true;forward|=(lane.m_Flags&Game.Prefabs.LaneFlags.Invert)==0||(lane.m_Flags&Game.Prefabs.LaneFlags.Twoway)!=0;backward|=(lane.m_Flags&Game.Prefabs.LaneFlags.Invert)!=0||(lane.m_Flags&Game.Prefabs.LaneFlags.Twoway)!=0;
   }
   twoWay=forward&&backward;
  }
 }
 void ApplyNetworkPreferences(){
  var copies=World.GetOrCreateSystemManaged<InvisibleRoadPrefabs>();
  var previousSettings=Tuple.Create(Mod.Options.MainNetwork,Mod.Options.AisleNetwork,Mod.Options.WalkNetwork,Mod.Options.RequireBuildingRoad,Mod.Options.NetworkDefaultsVersion,Mod.Options.InternalRoadDefaultsVersion);
  Networks.Sort((a,b)=>StringComparer.OrdinalIgnoreCase.Compare(a.Id,b.Id));
  // 0.1.11 prototype used misspelled prefab names and selected alphabetic fallbacks.
  if(Mod.Options.NetworkDefaultsVersion==0){if(Mod.Options.MainNetwork=="Alley - Double Sided Parking")Mod.Options.MainNetwork="";if(Mod.Options.WalkNetwork=="Arc Bike Bridge")Mod.Options.WalkNetwork="";Mod.Options.NetworkDefaultsVersion=1;}
  if(!Networks.Any(n=>n.Id==Mod.Options.MainNetwork&&n.Car&&!n.Path)&&!(Mod.Options.MainNetwork??"").StartsWith("AP Invisible ",StringComparison.Ordinal))Mod.Options.MainNetwork=Networks.FirstOrDefault(n=>n.Id=="Small Road")?.Id??"";
  if(Mod.Options.InternalRoadDefaultsVersion<1){Mod.Options.AisleNetwork="Alley";Mod.Options.InternalRoadDefaultsVersion=1;}
  Mod.Options.AisleNetwork=copies.Replacement(Mod.Options.AisleNetwork??"");
  Mod.Options.MainNetwork=copies.Replacement(Mod.Options.MainNetwork??"");
  if(copies.Available&&!Networks.Any(n=>n.Id==Mod.Options.AisleNetwork&&ApartmentAisle(n))){
   var previous=Mod.Options.AisleNetwork;
   Mod.Options.AisleNetwork=Networks.FirstOrDefault(n=>n.Id=="AP Invisible Road Path - 1xTwoway"&&ApartmentAisle(n))?.Id??Networks.FirstOrDefault(n=>n.Id=="Alley"&&ApartmentAisle(n))?.Id??Networks.FirstOrDefault(ApartmentAisle)?.Id??"";
   Trace.Event("catalog.internalRoad.fallback","requested="+previous+" selected="+Mod.Options.AisleNetwork+"; selected native width used by layout");
   if(previous!=Mod.Options.AisleNetwork&&Draft!=null&&!NativeCommitted){CancelLayout();LayoutOutdated=true;Status="AP.NetworkRecalculate";}
  }
  Mod.Options.RequireBuildingRoad=true;
  if(!Networks.Any(n=>n.Id==Mod.Options.WalkNetwork&&n.Walk&&n.Path&&!n.Car))Mod.Options.WalkNetwork=Networks.FirstOrDefault(n=>n.Id=="Pavement Path")?.Id??"";
  if(!previousSettings.Equals(Tuple.Create(Mod.Options.MainNetwork,Mod.Options.AisleNetwork,Mod.Options.WalkNetwork,Mod.Options.RequireBuildingRoad,Mod.Options.NetworkDefaultsVersion,Mod.Options.InternalRoadDefaultsVersion)))Mod.Options.ApplyAndSave();Trace.Event("catalog.networks",Newtonsoft.Json.JsonConvert.SerializeObject(Networks));
  foreach(var n in Networks.Where(n=>n.Id.StartsWith("AP Invisible ",StringComparison.Ordinal)))Trace.Event("catalog.invisibleRoad",Newtonsoft.Json.JsonConvert.SerializeObject(n));
 }
 internal static bool ApartmentAisle(NetworkChoice n)=>n!=null&&n.Car&&n.Walk&&!n.Path&&n.BuildingAccess&&n.TwoWay&&n.Width>=3&&n.Width<=32;
 internal NetworkChoice Network(string role,bool forPlanning=false)=>role=="parkingTwoWay"?Networks.Find(n=>n.Id=="AP Invisible Road Path - 1xTwoway"):role=="parkingOneWay"?Networks.Find(n=>n.Id=="AP Invisible Road Path - 1xOneway"):Tower?Networks.Find(n=>n.Id==(!forPlanning&&Draft?.Tower==true&&Draft.TowerAlgorithmVersion==0&&role!="pedestrian"?"AP Invisible Road Path - 1xTwoway":role=="main"?"Medium Road":role=="turnaround"?"AP Invisible Road Path - 1xOneway":role=="pedestrian"?Mod.Options.WalkNetwork:"Alley")):role=="aisleWide"?Networks.Find(n=>n.Id=="AP Invisible Road Path - 2xTwoway"&&ApartmentAisle(n)):Networks.Find(n=>n.Id==(role=="main"?Mod.Options.MainNetwork:role=="pedestrian"?Mod.Options.WalkNetwork:Villa?"Alley":Mod.Options.AisleNetwork)&&(role!="main"||n.Car&&n.Walk&&!n.Path&&n.BuildingAccess));
 LayoutOptions ConstructionOptions(){var o=Mod.Options.Layout(EntranceLength);o.Tower=Tower;o.TowerRules=TowerRules.Parse(Mod.Options.TowerRulesJson);o.TowerTurnWidth=Network("turnaround",true)?.Width??4;o.TowerParking=Assets.Find(a=>a.Id==Mod.Options.TowerParkingAsset);o.TowerFacility=Mod.Options.TowerFacility;o.TowerEntrancePaving=Mod.Options.TowerEntrancePaving;o.TowerTurnStyle=Mod.Options.TowerTurnStyle;o.TowerSurface=Mod.Options.TowerSurface;o.TowerEntranceSurface=Mod.Options.TowerEntranceSurface;o.Villa=Villa;o.VillaCentralRoad=Mod.Options.VillaCentralRoad;o.VillaFollowBoundary=Mod.Options.VillaFollowBoundary;o.VillaMesh=true;o.VillaGap=Mod.Options.VillaGap;o.VillaReduction=Mod.Options.VillaReduction;o.VillaMainSpacing=Mod.Options.VillaMainSpacing;o.VillaRows=Mod.Options.VillaRows;o.VillaMaxBuildings=Mod.Options.VillaMaxBuildings;o.VillaBoundary=Mod.Options.VillaBoundary;o.VillaSetback=0;o.VillaConnections=VillaConnections;o.ContinuousFrontage=true;o.SupplementalTwoWayWidth=Network("parkingTwoWay",true)?.Width??4;o.SupplementalAccessWidth=Network("aisleWide",true)?.Width??8;if(Network("aisle",true)?.Id.StartsWith("AP Invisible Road Path - ",StringComparison.Ordinal)==true)o.WideParkingAisle=Network("aisleWide",true)?.Width??0;o.RequireBuildingRoad=true;var main=Network("main",true);var aisle=Network("aisle",true);var walk=Network("pedestrian",true);o.BayWidth=ParkingPitch;o.BayDepth=Math.Max(o.BayDepth,DecalDepth);if(main!=null){o.Road=main.Width;o.MainRoadPedestrian=main.Walk;}if(aisle!=null){o.ParkingAisle=aisle.Width;o.AccessRoad=aisle.Width;o.AisleRoadPedestrian=aisle.Walk;}if(walk!=null){o.Walkway=walk.Width;if(o.PedestrianNetwork)o.ExternalWalkTargets=CollectExternalWalkTargets(walk.Width);}return o;}
 void SelectNetwork(string value){var parts=value.Split(new[]{'|'},2);if(parts.Length!=2)return;var preferred=World.GetOrCreateSystemManaged<InvisibleRoadPrefabs>().Replacement(parts[1]);var n=Networks.Find(x=>x.Id==preferred);if(n==null)return;
  switch(parts[0]){case "main":if(n.Path||!n.Car||!n.Walk||!n.BuildingAccess)return;Mod.Options.MainNetwork=n.Id;break;case "aisle":if(!ApartmentAisle(n))return;Mod.Options.AisleNetwork=n.Id;break;case "pedestrian":if(!n.Path||!n.Walk||n.Car)return;Mod.Options.WalkNetwork=n.Id;break;default:return;}
  Dictionary<string,List<string>> recent;
  try{recent=Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string,List<string>>>(Mod.Options.RecentNetworksJson??"{}")??new Dictionary<string,List<string>>();}catch{recent=new Dictionary<string,List<string>>();}
  if(!recent.TryGetValue(parts[0],out var history)||history==null)history=new List<string>();
  history.RemoveAll(id=>id==n.Id||string.IsNullOrEmpty(id));history.Insert(0,n.Id);recent[parts[0]]=history.Take(24).ToList();
  Mod.Options.RecentNetworksJson=Newtonsoft.Json.JsonConvert.SerializeObject(recent);
  Mod.Options.ApplyAndSave();Rebuild();
 }
}
}
