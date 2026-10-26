using Game.Common;
using Game.Prefabs;
using Unity.Entities;
namespace ApartmentPlan {
public partial class ConstructionTool {
 // Snapshot on the existing explicit/post-build audit only; no vehicle mutation
 // or per-frame city query. Physical links alone cannot prove animated arrivals.
 void TraceParkingLane(Entity bay,Entity lane){
  var parking=EntityManager.GetComponentData<Game.Net.ParkingLane>(lane);
  string detail="bay="+bay+" lane="+lane+" flags="+parking.m_Flags;
  if(EntityManager.HasComponent<Game.Net.LaneConnection>(lane)){
   var link=EntityManager.GetComponentData<Game.Net.LaneConnection>(lane);
   detail+=" start="+DescribeParkingCarLane(link.m_StartLane)+" end="+DescribeParkingCarLane(link.m_EndLane);
  }else detail+=" connection=missing";
  if(EntityManager.HasComponent<PrefabRef>(lane)){
   var prefab=EntityManager.GetComponentData<PrefabRef>(lane).m_Prefab;
   if(EntityManager.HasComponent<NetLaneData>(prefab))detail+=" prefabFlags="+EntityManager.GetComponentData<NetLaneData>(prefab).m_Flags;
  }
  Trace.Event("construction.parking.lane",detail);
  if(!EntityManager.HasBuffer<Game.Net.LaneObject>(lane))return;
  var objects=EntityManager.GetBuffer<Game.Net.LaneObject>(lane,true);int count=0;
  foreach(var item in objects){var car=item.m_LaneObject;if(!Live(car)||!EntityManager.HasComponent<Game.Vehicles.PersonalCar>(car))continue;if(count++>=4)break;
   Trace.Event("construction.parking.vehicle","car="+car+" lane="+lane+" parked="+EntityManager.HasComponent<Game.Vehicles.ParkedCar>(car)+" moving="+EntityManager.HasComponent<Game.Vehicles.CarCurrentLane>(car)+" unspawned="+EntityManager.HasComponent<Game.Objects.Unspawned>(car)+" relocation="+EntityManager.HasComponent<Game.Vehicles.FixParkingLocation>(car)+" tripSource="+EntityManager.HasComponent<Game.Objects.TripSource>(car));
  }
 }
 string DescribeParkingCarLane(Entity lane){
  string text=DescribeConnection(lane);
  if(Live(lane)&&EntityManager.HasComponent<Game.Net.CarLane>(lane))text+=" carFlags="+EntityManager.GetComponentData<Game.Net.CarLane>(lane).m_Flags;
  return "["+text+"]";
 }
}
}
