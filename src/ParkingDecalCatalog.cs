using System;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;
namespace ApartmentPlan {
public partial class PlanSystem {
 Entity parkingDecalEntity;
 internal bool ResolveParkingDecal(out Entity entity){
  var prefabs=World.GetOrCreateSystemManaged<PrefabSystem>();
  if(ValidParkingDecal(parkingDecalEntity,prefabs,out _)){entity=parkingDecalEntity;ReadParkingSize(entity);return true;}
  parkingDecalEntity=Entity.Null;PrefabBase chosen=null;string chosenId=null;int matches=0;
  // Imported/subscribed PrefabIDs contain an asset/platform hash. Never invent
  // a hashless StaticObjectPrefab ID, or load files that the game hasn't loaded.
  using(var query=EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PrefabData>(),ComponentType.ReadOnly<ObjectData>(),ComponentType.ReadOnly<ObjectGeometryData>()))
  using(var entities=query.ToEntityArray(Allocator.Temp))foreach(var candidate in entities){
   if(!ValidParkingDecal(candidate,prefabs,out var prefab))continue;
   matches++;var id=prefab.GetPrefabID().ToString();
   if(chosen!=null&&(chosen.isSubscribedMod&&!prefab.isSubscribedMod||chosen.isSubscribedMod==prefab.isSubscribedMod&&StringComparer.Ordinal.Compare(chosenId,id)<=0))continue;
   chosen=prefab;chosenId=id;parkingDecalEntity=candidate;
  }
  entity=parkingDecalEntity;
  if(entity==Entity.Null){Trace.Event("catalog.parkingDecal.missing","No loaded usable object named "+ParkingDecalId+"; disk presence and loaded prefab are distinct");return false;}
  ReadParkingSize(entity);
  Trace.Event("catalog.parkingDecal.resolved","id="+chosenId+" subscribed="+chosen.isSubscribedMod+" candidates="+matches+" width="+DecalWidth+" depth="+DecalDepth);
  return true;
 }
 bool ValidParkingDecal(Entity entity,PrefabSystem prefabs,out PrefabBase prefab){
  prefab=null;
  if(entity==Entity.Null||!EntityManager.Exists(entity)||!EntityManager.HasComponent<ObjectData>(entity)||!EntityManager.HasComponent<ObjectGeometryData>(entity)||!prefabs.TryGetPrefab(entity,out prefab)||!(prefab is StaticObjectPrefab)||prefab.name!=ParkingDecalId||prefabs.GetEntity(prefab)!=entity)return false;
  var size=EntityManager.GetComponentData<ObjectGeometryData>(entity).m_Size;
  return Unity.Mathematics.math.isfinite(size.x)&&size.x>0&&Unity.Mathematics.math.isfinite(size.z)&&size.z>0;
 }
 void ReadParkingSize(Entity entity){var size=EntityManager.GetComponentData<ObjectGeometryData>(entity).m_Size;DecalWidth=size.x;DecalDepth=size.z;}
}
}
