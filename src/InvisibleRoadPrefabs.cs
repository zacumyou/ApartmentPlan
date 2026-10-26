using System;
using System.Collections.Generic;
using System.Linq;
using Game;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using CarLane=Game.Prefabs.CarLane;
using UtilityLane=Game.Prefabs.UtilityLane;
using ElectricityConnection=Game.Prefabs.ElectricityConnection;
using WaterPipeConnection=Game.Prefabs.WaterPipeConnection;

namespace ApartmentPlan {
// Observe the separately imported asset pack. Stable names remain the save contract.
public sealed partial class InvisibleRoadPrefabs:GameSystemBase {
 internal const string Prefix="AP ";
 const UtilityTypes Supply=UtilityTypes.LowVoltageLine|UtilityTypes.WaterPipe|UtilityTypes.SewagePipe;
 internal sealed class Entry {internal NetGeometryPrefab Source;internal RoadPrefab Road;internal Entity SourceEntity,Entity;internal bool Ready,Checked;internal int RetryTicks;internal string Audit="pending";}
 readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>(StringComparer.Ordinal);
 EntityQuery networks;PrefabSystem prefabs;int previousCount=-1;
 internal int Revision {get;private set;}
 protected override void OnCreate(){base.OnCreate();prefabs=World.GetOrCreateSystemManaged<PrefabSystem>();networks=GetEntityQuery(ComponentType.ReadOnly<NetData>(),ComponentType.ReadOnly<NetGeometryData>());}
 internal static readonly string[] RequiredNames={
  "AP Invisible Car Path - 1xOneway 1xBay",
  "AP Invisible Car Path - 1xOneway",
  "AP Invisible Car Path - 1xTwoway",
  "AP Invisible Car Path - 2xTwoway",
  "AP Invisible Road Path - 1xOneway",
  "AP Invisible Road Path - 1xTwoway",
  "AP Invisible Road Path - 2xTwoway 2xPerpendicular",
  "AP Invisible Road Path - 2xTwoway",
 };
 internal bool TryGetRoad(string name,out Entity entity){entity=Entity.Null;if(!entries.TryGetValue(name,out var entry)||!entry.Ready)return false;entity=entry.Entity;return true;}
 internal string UnavailableStatus=>entries.Count==0?"AP.RoadPackMissing":"AP.RoadAssetsInvalid";
 internal bool Available=>RequiredNames.All(IsReady);
 internal bool IsReady(string name)=>entries.TryGetValue(name,out var entry)&&entry.Ready;
 internal string Diagnostic()=>Newtonsoft.Json.JsonConvert.SerializeObject(new{revision=Revision,missing=RequiredNames.Where(n=>!IsReady(n)),roads=entries.Select(p=>new{name=p.Key,ready=p.Value.Ready,checkedOnce=p.Value.Checked,audit=p.Value.Audit})});
 internal string Replacement(string source)=>entries.TryGetValue(Prefix+source,out var entry)&&entry.Ready?entry.Road.name:source;
 protected override void OnUpdate(){
  int count=networks.CalculateEntityCount();if(count==previousCount)return;
  foreach(var name in RequiredNames.Concat(new[]{"AP Invisible Harbor Path - 1xOneway"})){
   if(entries.ContainsKey(name))continue;
   // The game loads the independent ImportedData/Paradox asset pack. Never
   // synthesize a substitute or clone native road/lane/section assets here.
   PrefabBase loaded=null;
   // Imported assets have a CID/platform hash, unlike the former runtime IDs.
   using(var candidates=networks.ToEntityArray(Allocator.Temp))foreach(var candidate in candidates)if(prefabs.TryGetPrefab<PrefabBase>(candidate,out var found)&&found is RoadPrefab&&found.asset!=null&&found.name==name){loaded=found;break;}
   if(!(loaded is RoadPrefab road))continue;
   if(!prefabs.TryGetPrefab(new PrefabID("PathwayPrefab",name.Substring(Prefix.Length)),out PrefabBase original)||!(original is NetGeometryPrefab source))continue;
   var entity=prefabs.GetEntity(road);var sourceEntity=prefabs.GetEntity(source);
   if(!EntityManager.HasComponent<NetGeometryData>(entity)||!EntityManager.HasComponent<NetGeometryData>(sourceEntity))continue;
   entries.Add(name,new Entry{Source=source,SourceEntity=sourceEntity,Road=road,Entity=entity});Revision++;
   Trace.Event("invisibleRoad.asset.loaded","name="+name+" cid="+road.asset.id);
  }
  previousCount=count;
 }
 // Native RoadPrefab sets ClipTerrain. An invisible road has no rendered road
 // deck to cover that hole: remove clipping on our copies only, after init.
 internal void CompleteInitialization(){
  foreach(var entry in entries.Values){if(!EntityManager.Exists(entry.Entity))continue;
   if(!EntityManager.HasComponent<NetGeometryData>(entry.Entity))continue;
   var geometry=EntityManager.GetComponentData<NetGeometryData>(entry.Entity);
   // Native RoadPrefab requires a zone-block reference for residential service
   // buffers. Keep those buffers but suppress automatic zoning for AP plans.
   if(EntityManager.HasComponent<RoadData>(entry.Entity)){
    var data=EntityManager.GetComponentData<RoadData>(entry.Entity);
    if((data.m_Flags&Game.Prefabs.RoadFlags.EnableZoning)!=0){data.m_Flags&=~Game.Prefabs.RoadFlags.EnableZoning;EntityManager.SetComponentData(entry.Entity,data);}
   }
   const Game.Net.GeometryFlags hidden=Game.Net.GeometryFlags.ClipTerrain|Game.Net.GeometryFlags.Marker|Game.Net.GeometryFlags.SnapCellSize;
   if(entry.Checked&&entry.Ready&&(geometry.m_Flags&hidden)==0)continue;
   // Native initialization can span multiple prefab updates. A failed early
   // audit must not permanently remove a later-complete road from the catalog.
   if(entry.Checked&&!entry.Ready&&++entry.RetryTicks<120)continue;entry.RetryTicks=0;
   if((geometry.m_Flags&hidden)!=0){geometry.m_Flags&=~hidden;EntityManager.SetComponentData(entry.Entity,geometry);}
   float sourceWidth=EntityManager.GetComponentData<NetGeometryData>(entry.SourceEntity).m_DefaultWidth;
   if(sourceWidth<=0||geometry.m_DefaultWidth<=0)continue;
   bool access=false,coverage=false,availability=false;var net=EntityManager.GetComponentData<NetData>(entry.Entity);
   if(!net.m_EdgeArchetype.Valid)continue;
   using(var types=net.m_EdgeArchetype.GetComponentTypes(Allocator.Temp))foreach(var type in types){access|=type==ComponentType.ReadWrite<Game.Buildings.ConnectedBuilding>();coverage|=type==ComponentType.ReadWrite<Game.Net.ServiceCoverage>();availability|=type==ComponentType.ReadWrite<ResourceAvailability>();}
   bool utilities=EntityManager.HasComponent<ElectricityConnectionData>(entry.Entity)&&EntityManager.HasComponent<WaterPipeConnectionData>(entry.Entity);
   if(utilities){var power=EntityManager.GetComponentData<ElectricityConnectionData>(entry.Entity);var water=EntityManager.GetComponentData<WaterPipeConnectionData>(entry.Entity);utilities=power.m_Capacity>0&&water.m_FreshCapacity>0&&water.m_SewageCapacity>0;}
   UtilityTypes lanes=CompositionUtilities(entry.Entity,out bool cars,out string traffic);
   bool roadMerge=(geometry.m_MergeLayers&Layer.Road)!=0;
   bool ready=access&&coverage&&availability&&utilities&&cars&&roadMerge&&(lanes&Supply)==Supply&&math.abs(sourceWidth-geometry.m_DefaultWidth)<.05f;
   bool replaceable=World.GetOrCreateSystemManaged<NetInitializeSystem>().CanReplace(net,true)&&!EntityManager.HasBuffer<FixedNetElement>(entry.Entity)&&(!EntityManager.HasBuffer<AuxiliaryNet>(entry.Entity)||EntityManager.GetBuffer<AuxiliaryNet>(entry.Entity,true).Length==0);
   string audit="sourceWidth="+sourceWidth+" width="+geometry.m_DefaultWidth+" access="+access+" homeSearchCoverage="+coverage+" resourceAvailability="+availability+" utilityData="+utilities+" lanes="+lanes+" roadMerge="+roadMerge+" replaceable="+replaceable+" marker="+((geometry.m_Flags&Game.Net.GeometryFlags.Marker)!=0)+" "+traffic;
   bool changed=!entry.Checked||entry.Ready!=ready||entry.Audit!=audit;if(changed)Revision++;
   entry.Checked=true;entry.Ready=ready;entry.Audit=audit;
   if(changed)Trace.Event("invisibleRoad.initialized","name="+entry.Road.name+" ready="+entry.Ready+" "+audit+" terrainClipping=false; prefab checks are not live traffic proof");
  }
 }
 UtilityTypes CompositionUtilities(Entity entity,out bool cars,out string traffic){
  cars=false;traffic="composition unavailable";
  // RoadPrefab has no DefaultNetLane buffer. Reproduce the native ground-road
  // composition, including underground/overhead sections and requirement flags.
  // Merely finding a utility in prefab dependencies would accept inactive pieces.
  if(!EntityManager.HasBuffer<NetGeometrySection>(entity))return UtilityTypes.None;
  using(var pieces=new NativeList<NetCompositionPiece>(32,Allocator.Temp))
  using(var lanes=new NativeList<NetCompositionLane>(32,Allocator.Temp)){
   var sections=EntityManager.GetBuffer<NetGeometrySection>(entity,true);
   NetCompositionHelpers.GetCompositionPieces(pieces,sections.AsNativeArray(),default(CompositionFlags),GetBufferLookup<NetSubSection>(true),GetBufferLookup<NetSectionPiece>(true));
   var composition=default(NetCompositionData);
   NetCompositionHelpers.CalculateCompositionData(ref composition,pieces.AsArray(),GetComponentLookup<NetPieceData>(true),GetComponentLookup<NetVertexMatchData>(true));
   NetCompositionHelpers.AddCompositionLanes(Entity.Null,ref composition,pieces,lanes,default(DynamicBuffer<NetCompositionCarriageway>),GetComponentLookup<NetLaneData>(true),GetBufferLookup<NetPieceLane>(true));
   var utilities=GetComponentLookup<UtilityLaneData>(true);UtilityTypes result=UtilityTypes.None;
   foreach(var lane in lanes)if(utilities.HasComponent(lane.m_Lane))result|=utilities[lane.m_Lane].m_UtilityTypes;
   int carCount=0,walkCount=0,usable=0;bool forward=false,backward=false;
   foreach(var lane in lanes){
    if((lane.m_Flags&LaneFlags.Pedestrian)!=0)walkCount++;
    if(!EntityManager.HasComponent<CarLaneData>(lane.m_Lane)||(EntityManager.GetComponentData<CarLaneData>(lane.m_Lane).m_RoadTypes&RoadTypes.Car)==0)continue;
    carCount++;var data=EntityManager.GetComponentData<NetLaneData>(lane.m_Lane);
    bool valid=EntityManager.HasComponent<PathfindCarData>(data.m_PathfindPrefab)&&(lane.m_Flags&(LaneFlags.DisconnectedStart|LaneFlags.DisconnectedEnd|LaneFlags.Virtual|LaneFlags.PublicOnly))==0;
    if(!valid)continue;usable++;forward|=(lane.m_Flags&LaneFlags.Invert)==0||(lane.m_Flags&LaneFlags.Twoway)!=0;backward|=(lane.m_Flags&LaneFlags.Invert)!=0||(lane.m_Flags&LaneFlags.Twoway)!=0;
   }
   cars=usable>0&&(!prefabs.GetPrefab<PrefabBase>(entity).name.EndsWith("Twoway",StringComparison.Ordinal)||forward&&backward);
   traffic="cars="+usable+"/"+carCount+" walk="+walkCount+" forward="+forward+" backward="+backward+" state="+composition.m_State;
   return result;
  }
 }
 internal static IEnumerable<NetLanePrefab> NetworkLanes(PrefabBase root){
  var pending=new List<PrefabBase>{root};var seen=new HashSet<PrefabBase>();
  while(pending.Count>0){var prefab=pending[pending.Count-1];pending.RemoveAt(pending.Count-1);if(prefab==null||!seen.Add(prefab))continue;if(prefab is NetLanePrefab lane){yield return lane;continue;}
   var children=new List<PrefabBase>();prefab.GetDependencies(children);foreach(var component in prefab.components)if(component!=null&&component.active)component.GetDependencies(children);
   foreach(var child in children)if(child is NetGeometryPrefab||child is NetSectionPrefab||child is NetPiecePrefab||child is NetLanePrefab)pending.Add(child);
  }
 }
}
// Separate post-initialization phase; never run native initialization manually.
public sealed partial class InvisibleRoadPrefabAudit:GameSystemBase {
 protected override void OnUpdate()=>World.GetOrCreateSystemManaged<InvisibleRoadPrefabs>().CompleteInitialization();
}
}

