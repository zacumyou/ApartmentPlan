using System;
using System.Collections.Generic;
using Game.Common;
using Game.Prefabs;
using Unity.Entities;
namespace ApartmentPlan {
public partial class PlanSystem {
 int occupancyPlan=-1;float nextOccupancy;IEnumerator<int> occupancyWork;
 internal int? ParkedCount,OccupiedHomes,LiveParkingCapacity,LiveHomeCapacity;
 void ClearOccupancy(){occupancyWork?.Dispose();occupancyWork=null;occupancyPlan=-1;nextOccupancy=0;ParkedCount=OccupiedHomes=LiveParkingCapacity=LiveHomeCapacity=null;}
 void TickOccupancy(){
  if(SelectedPlan!=occupancyPlan){ClearOccupancy();occupancyPlan=SelectedPlan;}
  var stored=SelectedStored;if(stored==null||!IsBuilt(stored))return;
  if(occupancyWork==null){if(UnityEngine.Time.unscaledTime<nextOccupancy)return;occupancyWork=ReadOccupancy(stored).GetEnumerator();}
  var watch=System.Diagnostics.Stopwatch.StartNew();
  for(int i=0;i<24&&watch.Elapsed.TotalMilliseconds<1.5;i++)if(!occupancyWork.MoveNext()){occupancyWork.Dispose();occupancyWork=null;nextOccupancy=UnityEngine.Time.unscaledTime+3;Revision++;break;}
 }
 bool OccupancyLive(Entity e)=>e!=Entity.Null&&EntityManager.Exists(e)&&!EntityManager.HasComponent<Deleted>(e)&&!EntityManager.HasComponent<Game.Tools.Temp>(e);
 IEnumerable<int> ReadOccupancy(StoredPlan stored){
  var cars=new HashSet<Entity>();var homes=new HashSet<Entity>();var lanes=new HashSet<Entity>();int capacity=0,parking=0;bool knownParking=true;
  // Only owned roots and their parking lanes. No whole-city vehicle query.
  foreach(var root in OwnedObjects(stored)){
   yield return 0;if(!OccupancyLive(root))continue;
   if(EntityManager.HasComponent<Game.Buildings.Building>(root)){
    if(EntityManager.HasComponent<PrefabRef>(root)){var prefab=EntityManager.GetComponentData<PrefabRef>(root).m_Prefab;if(EntityManager.HasComponent<BuildingPropertyData>(prefab))capacity+=EntityManager.GetComponentData<BuildingPropertyData>(prefab).m_ResidentialProperties;}
    if(EntityManager.HasBuffer<Game.Buildings.Renter>(root))foreach(var renter in EntityManager.GetBuffer<Game.Buildings.Renter>(root,true)){
     var e=renter.m_Renter;if(OccupancyLive(e)&&EntityManager.HasComponent<Game.Citizens.Household>(e)&&EntityManager.HasComponent<Game.Buildings.PropertyRenter>(e)&&EntityManager.GetComponentData<Game.Buildings.PropertyRenter>(e).m_Property==root)homes.Add(e);
    }
    continue;
   }
   // AP's standalone parking decals; exclude curb parking on owned roads.
   if(EntityManager.HasComponent<Game.Net.Edge>(root)||EntityManager.HasComponent<Game.Net.Node>(root)||!EntityManager.HasBuffer<Game.Net.SubLane>(root))continue;
   var children=new List<Entity>();foreach(var sub in EntityManager.GetBuffer<Game.Net.SubLane>(root,true))children.Add(sub.m_SubLane);
   foreach(var lane in children){yield return 0;
    if(!OccupancyLive(lane)||!lanes.Add(lane)||!EntityManager.HasComponent<Game.Net.ParkingLane>(lane)||EntityManager.HasComponent<Game.Net.SlaveLane>(lane)||!EntityManager.HasComponent<PrefabRef>(lane))continue;
    var prefab=EntityManager.GetComponentData<PrefabRef>(lane).m_Prefab;
    if(EntityManager.HasComponent<NetLaneData>(prefab)&&(EntityManager.GetComponentData<NetLaneData>(prefab).m_Flags&LaneFlags.Virtual)!=0)continue;
    if(EntityManager.HasComponent<Game.Net.Curve>(lane)&&EntityManager.HasComponent<ParkingLaneData>(prefab)){
     var data=EntityManager.GetComponentData<ParkingLaneData>(prefab);
     if(data.m_SlotInterval>0)parking+=Math.Max(0,Game.Net.NetUtils.GetParkingSlotCount(EntityManager.GetComponentData<Game.Net.Curve>(lane),EntityManager.GetComponentData<Game.Net.ParkingLane>(lane),data));else knownParking=false;
    }else knownParking=false;
    if(!EntityManager.HasBuffer<Game.Net.LaneObject>(lane))continue;
    foreach(var item in EntityManager.GetBuffer<Game.Net.LaneObject>(lane,true)){
     var car=item.m_LaneObject;if(OccupancyLive(car)&&EntityManager.HasComponent<Game.Vehicles.ParkedCar>(car)&&!EntityManager.HasComponent<Game.Objects.Unspawned>(car)&&EntityManager.GetComponentData<Game.Vehicles.ParkedCar>(car).m_Lane==lane)cars.Add(car);
    }
   }
  }
  ParkedCount=cars.Count;OccupiedHomes=homes.Count;LiveParkingCapacity=knownParking?(int?)parking:null;LiveHomeCapacity=capacity;
 }
}
}
