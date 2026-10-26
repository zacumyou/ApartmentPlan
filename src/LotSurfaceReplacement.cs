using System;
using System.Collections.Generic;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;
namespace ApartmentPlan {
// Per instance, serialized opt-in. Source asset prefabs remain untouched.
public struct PlanTileLot:IComponentData,IEmptySerializable {}
public partial class LotSurfaceReplacement:GameSystemBase {
 EntityQuery roots;int tick;
 protected override void OnCreate(){base.OnCreate();roots=GetEntityQuery(ComponentType.ReadOnly<PlanTileLot>(),ComponentType.Exclude<Deleted>(),ComponentType.Exclude<Game.Tools.Temp>());}
 protected override void OnUpdate(){if(++tick%60!=0)return;using(var items=roots.ToEntityArray(Allocator.Temp))foreach(var root in items)Remove(EntityManager,root);}
 internal static int Remove(EntityManager manager,Entity root){
  int count=0;var pending=new List<Entity>();var seen=new HashSet<Entity>();pending.Add(root);
  while(pending.Count>0){var owner=pending[pending.Count-1];pending.RemoveAt(pending.Count-1);if(!manager.Exists(owner)||!seen.Add(owner))continue;
   if(manager.HasBuffer<Game.Objects.SubObject>(owner))foreach(var child in manager.GetBuffer<Game.Objects.SubObject>(owner,true))if(manager.Exists(child.m_SubObject)&&manager.HasComponent<Owner>(child.m_SubObject)&&manager.GetComponentData<Owner>(child.m_SubObject).m_Owner==owner)pending.Add(child.m_SubObject);
   if(!manager.HasBuffer<Game.Areas.SubArea>(owner))continue;
   // Native area/reference systems process Deleted and repair owner buffers.
   using(var areas=manager.GetBuffer<Game.Areas.SubArea>(owner,true).ToNativeArray(Allocator.Temp))foreach(var child in areas){
    var e=child.m_Area;if(!manager.Exists(e)||manager.HasComponent<Created>(e)||manager.HasComponent<Deleted>(e)||!manager.HasComponent<Owner>(e)||manager.GetComponentData<Owner>(e).m_Owner!=owner)continue;
    if(manager.HasComponent<Game.Tools.Temp>(e)&&manager.GetComponentData<Game.Tools.Temp>(e).m_Original!=Entity.Null)continue;
    // Functional areas are not decorative paving: preserve their navigation lanes and descendants.
    if(manager.HasComponent<Game.Areas.Clip>(e)||manager.HasComponent<Game.Areas.Navigation>(e)||manager.HasComponent<Game.Net.SubLane>(e))continue;
    if(manager.HasComponent<PrefabRef>(e)&&manager.HasComponent<NavigationAreaData>(manager.GetComponentData<PrefabRef>(e).m_Prefab))continue;
    if(manager.HasComponent<PrefabRef>(e)&&manager.HasComponent<SurfaceData>(manager.GetComponentData<PrefabRef>(e).m_Prefab)){manager.AddComponent<Deleted>(e);count++;}else pending.Add(e);
   }
  }return count;
 }
}
public partial class ConstructionTool {
 bool TrimLotSurfaces(){
  var plan=World.GetExistingSystemManaged<PlanSystem>()?.Draft;if(!(plan?.Villa==true?Mod.Options.VillaTileLots:Mod.Options.TileBuildingLots))return false;int removed=0;
  using(var entities=temps.ToEntityArray(Allocator.Temp))foreach(var e in entities){
   if(!EntityManager.HasComponent<Game.Buildings.Building>(e)||!EntityManager.HasComponent<PrefabRef>(e)||!EntityManager.HasComponent<Game.Objects.Transform>(e)||EntityManager.GetComponentData<Game.Tools.Temp>(e).m_Original!=Entity.Null)continue;
   var position=EntityManager.GetComponentData<Game.Objects.Transform>(e).m_Position;var prefab=EntityManager.GetComponentData<PrefabRef>(e).m_Prefab;
   if(!expected.Exists(v=>v.Item1==prefab&&Unity.Mathematics.math.distancesq(v.Item2,position)<1))continue;
   removed+=LotSurfaceReplacement.Remove(EntityManager,e);
  }
  if(removed>0)Trace.Event("construction.lotSurfaces.removed","owned preview surfaces="+removed);return removed>0;
 }
}
}
