using System.Collections.Generic;
using Game;
using Game.Buildings;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
namespace ApartmentPlan {
// The durable authority is the existing, entity-remapped PlanOwnedEntity buffer.
// This index is rebuilt on load; no prefab changes or new save schema are needed.
public partial class CompoundGrowableProtection:GameSystemBase {
 readonly HashSet<Entity> owned=new HashSet<Entity>();
 EntityQuery condemned,configuration;
 protected override void OnCreate(){
  base.OnCreate();
  condemned=GetEntityQuery(ComponentType.ReadOnly<Condemned>(),ComponentType.ReadOnly<Building>(),ComponentType.ReadOnly<PrefabRef>(),ComponentType.Exclude<Temp>(),ComponentType.Exclude<Deleted>(),ComponentType.Exclude<Destroyed>());
  configuration=GetEntityQuery(ComponentType.ReadOnly<BuildingConfigurationData>());
  RequireForUpdate(condemned);
 }
 internal void ResetOwnership(){owned.Clear();}
 internal void Include(Entity entity){if(EntityManager.Exists(entity)&&EntityManager.HasComponent<Building>(entity))owned.Add(entity);}
 public override int GetUpdateInterval(SystemUpdatePhase phase)=>64;
 protected override void OnUpdate(){
  if(Mod.Options==null||owned.Count==0)return;
  // Runs before the native demolition consumer, not after a Deleted command.
  using(var entities=condemned.ToEntityArray(Allocator.Temp)){
   var icons=World.GetOrCreateSystemManaged<Game.Notifications.IconCommandSystem>().CreateCommandBuffer();
   bool hasConfig=configuration.CalculateEntityCount()==1;
   var notification=hasConfig?configuration.GetSingleton<BuildingConfigurationData>().m_CondemnedNotification:Entity.Null;
   foreach(var entity in entities){
    var prefab=EntityManager.GetComponentData<PrefabRef>(entity).m_Prefab;
    if(!GrowableProtectionPolicy.Protect(owned.Contains(entity),EntityManager.HasComponent<SpawnableBuildingData>(prefab),EntityManager.HasComponent<SignatureBuildingData>(prefab)))continue;
    EntityManager.RemoveComponent<Condemned>(entity);
    if(hasConfig)icons.Remove(entity,notification);
    Trace.Sample("construction.growable.zoneProtected",entity.ToString());
   }
  }
 }
}
public partial class PlanSystem {
 void RestoreGrowableProtection(){
  var protection=World.GetOrCreateSystemManaged<CompoundGrowableProtection>();protection.ResetOwnership();
  foreach(var plan in Saved){
   if(!WasConstructed(plan.Entity)||!EntityManager.HasBuffer<PlanOwnedEntity>(plan.Entity))continue;
   foreach(var item in EntityManager.GetBuffer<PlanOwnedEntity>(plan.Entity,true))protection.Include(item.Entity);
  }
 }
}
}
