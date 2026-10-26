using System.Collections.Generic;
using Game.Net;
using Game.Prefabs;
using Unity.Entities;
namespace ApartmentPlan {
// PreCullingSystem reads the INSTANCE Marker to restrict rendering to Debug
// (RenderingSystem.markersVisible). Do NOT add MarkerNetData: that also changes
// prefab connection layers, composition collision flags and native tool raycasts.
public sealed class InvisibleRoadDisplay:ComponentBase {
 public override void GetPrefabComponents(HashSet<ComponentType> components){}
 public override void GetArchetypeComponents(HashSet<ComponentType> components){
  if(components.Contains(ComponentType.ReadWrite<Edge>())||components.Contains(ComponentType.ReadWrite<Node>()))
   components.Add(ComponentType.ReadWrite<Marker>());
 }
}
}
