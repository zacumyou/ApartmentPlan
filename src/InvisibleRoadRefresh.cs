using System;
using System.Collections.Generic;
using Game;
using Game.Common;
using Game.Prefabs;
using Colossal.Serialization.Entities;
using Unity.Collections;
using Unity.Entities;
namespace ApartmentPlan {
// Existing AP networks must regenerate native junction/lane data after loading
// their updated prefab. No road deletion, replacement, utility graph edits.
public partial class InvisibleRoadRefresh:GameSystemBase {
 bool pending;int ticks;
 protected override void OnGameLoadingComplete(Purpose purpose,GameMode mode){base.OnGameLoadingComplete(purpose,mode);pending=mode==GameMode.Game;ticks=0;}
 protected override void OnUpdate(){
  if(!pending||++ticks%60!=0)return;
  var ready=World.GetOrCreateSystemManaged<InvisibleRoadPrefabs>();var prefabs=World.GetOrCreateSystemManaged<PrefabSystem>();var targets=new HashSet<Entity>();var roads=new List<Entity>();bool waiting=false;
  using(var query=EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Game.Net.Edge>(),ComponentType.ReadOnly<PrefabRef>(),ComponentType.Exclude<Deleted>(),ComponentType.Exclude<Game.Tools.Temp>()))
  using(var items=query.ToEntityArray(Allocator.Temp))foreach(var e in items){
   if(!prefabs.TryGetPrefab<PrefabBase>(EntityManager.GetComponentData<PrefabRef>(e).m_Prefab,out var prefab)||!prefab.name.StartsWith("AP Invisible ",StringComparison.Ordinal))continue;
   if(!ready.IsReady(prefab.name)){waiting=true;continue;}
   roads.Add(e);targets.Add(e);var edge=EntityManager.GetComponentData<Game.Net.Edge>(e);targets.Add(edge.m_Start);targets.Add(edge.m_End);
  }
  if(waiting&&ticks<600)return;pending=false;
  int repaired=0;foreach(var e in roads)if(ResidentialRoadSupport.RepairLoaded(EntityManager,e))repaired++;
  int markers=0;
  // Restore ONLY the instance rendering marker. The geometry/composition marker
  // remains off so ordinary road connections, collisions and tools keep working.
  foreach(var e in targets){
   if(!EntityManager.Exists(e)||!EntityManager.HasComponent<PrefabRef>(e)||!prefabs.TryGetPrefab<PrefabBase>(EntityManager.GetComponentData<PrefabRef>(e).m_Prefab,out var p)||!p.name.StartsWith("AP Invisible ",StringComparison.Ordinal))continue;
   if(!EntityManager.HasComponent<Game.Net.Marker>(e)){EntityManager.AddComponent<Game.Net.Marker>(e);markers++;}
  }
  using(var query=EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetCompositionData>(),ComponentType.ReadOnly<PrefabRef>()))
  using(var items=query.ToEntityArray(Allocator.Temp))foreach(var e in items){
   if(!prefabs.TryGetPrefab<PrefabBase>(EntityManager.GetComponentData<PrefabRef>(e).m_Prefab,out var p)||!p.name.StartsWith("AP Invisible ",StringComparison.Ordinal))continue;
   var data=EntityManager.GetComponentData<NetCompositionData>(e);
   if((data.m_State&Game.Prefabs.CompositionState.Marker)==0)continue;
   data.m_State&=~(Game.Prefabs.CompositionState.Marker|Game.Prefabs.CompositionState.NoSubCollisions);EntityManager.SetComponentData(e,data);markers++;
  }
  foreach(var e in targets)if(EntityManager.Exists(e)&&!EntityManager.HasComponent<Deleted>(e)&&!EntityManager.HasComponent<Updated>(e))EntityManager.AddComponent<Updated>(e);
  Trace.Event("invisibleRoad.loaded.refresh","native update targets="+targets.Count+" residentialRoadsRepaired="+repaired+" displayMarkersRepaired="+markers+" unavailablePrefabs="+waiting+"; display-only markers; normal road geometry and simulation retained");
 }
}
}
