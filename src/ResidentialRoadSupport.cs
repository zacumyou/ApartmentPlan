using System.Collections.Generic;
using Game.Net;
using Game.Prefabs;
using Unity.Entities;
using ServiceCoverage=Game.Net.ServiceCoverage;
namespace ApartmentPlan {
// RoadPrefab normally adds these only with m_ZoneBlock. Native FindHome
// rejects a building when its road lacks ServiceCoverage, even with utilities.
// Supply native road simulation data without creating zoning or changing lots.
public sealed class ResidentialRoadSupport:ComponentBase {
 public override void GetPrefabComponents(HashSet<ComponentType> components){}
 public override void GetArchetypeComponents(HashSet<ComponentType> components){
  if(!components.Contains(ComponentType.ReadWrite<Edge>()))return;
  components.Add(ComponentType.ReadWrite<ServiceCoverage>());
  components.Add(ComponentType.ReadWrite<ResourceAvailability>());
  components.Add(ComponentType.ReadWrite<Density>());
 }
 internal static bool RepairLoaded(EntityManager manager,Entity edge){
  bool changed=false;
  if(!manager.HasBuffer<ServiceCoverage>(edge)){manager.AddBuffer<ServiceCoverage>(edge);changed=true;}
  {
   var buffer=manager.GetBuffer<ServiceCoverage>(edge);int previous=buffer.Length;
   // Installed Game.Net.InitializeSystem reserves nine coverage entries.
   if(previous<9){buffer.ResizeUninitialized(9);for(int i=previous;i<buffer.Length;i++)buffer[i]=default;changed=true;}
  }
  if(!manager.HasBuffer<ResourceAvailability>(edge)){manager.AddBuffer<ResourceAvailability>(edge);changed=true;}
  {
   var buffer=manager.GetBuffer<ResourceAvailability>(edge);int previous=buffer.Length;
   if(previous<(int)AvailableResource.Count){buffer.ResizeUninitialized((int)AvailableResource.Count);for(int i=previous;i<buffer.Length;i++)buffer[i]=default;changed=true;}
  }
  if(!manager.HasComponent<Density>(edge)){manager.AddComponent<Density>(edge);changed=true;}
  return changed;
 }
}
}
