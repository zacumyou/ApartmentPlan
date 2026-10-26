using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
namespace ApartmentPlan {
public partial class PlanSystem {
 bool catalogRetryLayout,catalogFailed,catalogBuildingsChanged;IEnumerator<bool> catalogWork;int catalogProcessed,catalogSlices;double catalogCpuMs,catalogMaxSliceMs;
 readonly HashSet<string> subscribedAssets=new HashSet<string>(StringComparer.Ordinal);
 readonly HashSet<string> accessProfiles=new HashSet<string>(StringComparer.Ordinal);
 internal bool CatalogLoading=>catalogWork!=null;
 void CancelCatalog(){catalogWork?.Dispose();catalogWork=null;catalogProcessed=0;catalogRetryLayout=false;catalogFailed=false;}
 void Catalog(){
  if(!Ready||Mod.Options==null||CatalogLoading)return;
  if(!catalogReady||catalogFailed){StartCatalog(!catalogReady);return;}
  if(Networks.Count==0||networkRevision!=World.GetOrCreateSystemManaged<InvisibleRoadPrefabs>().Revision)StartCatalog(false);
 }
 void StartCatalog(bool buildings){
  if(CatalogLoading)return;catalogFailed=false;catalogBuildingsChanged=buildings;
  catalogRetryLayout=Status=="AP.NetworkUnavailable"&&Entrance.HasValue;
  catalogProcessed=catalogSlices=0;catalogCpuMs=catalogMaxSliceMs=0;
  catalogWork=PrepareCatalog(buildings).GetEnumerator();Revision++;
  Trace.Event("catalog.queued","buildings="+buildings);
 }
 void TickCatalog(){
  if(catalogWork==null)return;
  var timer=Stopwatch.StartNew();bool finished=false;
  try{
   // All Unity/ECS reads stay on the game thread. Yield between prefabs and
   // network dependencies; never retain a NativeArray/DynamicBuffer across frames.
   for(int steps=0;steps<256;steps++){
    if(!catalogWork.MoveNext()){finished=true;break;}
    if(timer.Elapsed.TotalMilliseconds>=2)break;
   }
   catalogSlices++;catalogCpuMs+=timer.Elapsed.TotalMilliseconds;catalogMaxSliceMs=Math.Max(catalogMaxSliceMs,timer.Elapsed.TotalMilliseconds);
   if(finished){
    catalogWork.Dispose();catalogWork=null;catalogReady=true;if(catalogBuildingsChanged)CatalogRevision++;Revision++;
    if(Status=="AP.CatalogLoading")Status=Draft==null?"AP.Draw":"AP.Ready";
    Trace.Event("catalog.end","count="+Assets.Count+" slices="+catalogSlices+" cpuMs="+catalogCpuMs.ToString("F1")+" maxSliceMs="+catalogMaxSliceMs.ToString("F1"));
    if(catalogRetryLayout){catalogRetryLayout=false;Rebuild();}
   }else if(catalogSlices%8==0)Revision++;
  }catch(Exception e){CancelCatalog();catalogFailed=true;Status="AP.CatalogFailed";Revision++;Trace.Failure("catalog.failed",e);}
 }
 IEnumerable<bool> PrepareCatalog(bool buildings){
  if(buildings)foreach(var step in ScanBuildings())yield return step;
  foreach(var step in ScanNetworks())yield return step;
  foreach(var step in ScanParkingDecal())yield return step;
  ScanTowerSurfaces();ApplyNetworkPreferences();
 }
 IEnumerable<bool> ScanBuildings(){
  var prefabs=World.GetOrCreateSystemManaged<PrefabSystem>();var uiSystem=World.GetOrCreateSystemManaged<Game.UI.InGame.PrefabUISystem>();
  subscribedAssets.Clear();
  var assets=new List<Asset>();var entitiesById=new Dictionary<string,Entity>(StringComparer.Ordinal);
  Entity[] snapshot;
  using(var query=EntityManager.CreateEntityQuery(ComponentType.ReadOnly<BuildingData>(),ComponentType.ReadOnly<ObjectGeometryData>()))
  using(var entities=query.ToEntityArray(Allocator.Temp))snapshot=entities.ToArray();
  yield return true;
  foreach(var e in snapshot){
   catalogProcessed++;yield return true;
   if(!EntityManager.Exists(e)||!prefabs.TryGetPrefab<PrefabBase>(e,out var prefab)||prefab==null||entitiesById.ContainsKey(prefab.name)||prefabs.GetEntity(prefab)!=e)continue;
   var b=EntityManager.GetComponentData<BuildingData>(e);var g=EntityManager.GetComponentData<ObjectGeometryData>(e);
   double w=b.m_LotSize.x*8,d=b.m_LotSize.y*8,h=g.m_Size.y;if(w<=0||d<=0||w>500||d>500||!math.isfinite((float)h)||h<=0)continue;
   var ui=prefab.GetComponent<UIObject>();int homes=EntityManager.HasComponent<BuildingPropertyData>(e)?EntityManager.GetComponentData<BuildingPropertyData>(e).m_ResidentialProperties:0;
   var asset=new Asset{Id=prefab.name,Name=prefab.name,Icon=!string.IsNullOrEmpty(ui?.m_Icon)?ui.m_Icon:prefab.thumbnailUrl??"",GeometryWidth=2*Math.Max(Math.Abs(g.m_Bounds.min.x),Math.Abs(g.m_Bounds.max.x)),GeometryDepth=2*Math.Max(Math.Abs(g.m_Bounds.min.z),Math.Abs(g.m_Bounds.max.z)),Width=w,Depth=d,Height=h,Homes=homes,Footprint=0};
   if(prefab.isSubscribedMod)subscribedAssets.Add(asset.Id);
   uiSystem.GetTitleAndDescription(e,out asset.NameKey,out _);assets.Add(asset);entitiesById.Add(asset.Id,e);
  }
  assets.Sort((a,b)=>StringComparer.OrdinalIgnoreCase.Compare(a.Name,b.Name));
  Assets.Clear();Assets.AddRange(assets);BuildingPrefabs.Clear();foreach(var pair in entitiesById)BuildingPrefabs.Add(pair.Key,pair.Value);
  RestoreAssetPreferences();if(Villa&&string.IsNullOrEmpty(Mod.Options.VillaSelectedAssetsJson))foreach(var a in Assets.Where(VillaLayout.Candidate))Selected.Add(a.Id);EntranceLength=Mod.Options.EntranceLength;
  yield return true;
 }
 void EnsureAccessProfile(Asset asset){
  if(accessProfiles.Contains(asset.Id)||!BuildingPrefabs.TryGetValue(asset.Id,out var e)||!EntityManager.Exists(e))return;
  asset.PedestrianAccess.Clear();ReadPedestrianAccess(e,float3.zero,quaternion.identity,asset.PedestrianAccess,0);accessProfiles.Add(asset.Id);
 }
 IEnumerable<bool> ScanParkingDecal(){
  var prefabs=World.GetOrCreateSystemManaged<PrefabSystem>();
  if(ValidParkingDecal(parkingDecalEntity,prefabs,out _)){ReadParkingSize(parkingDecalEntity);yield break;}
  Entity[] snapshot;
  using(var query=EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PrefabData>(),ComponentType.ReadOnly<ObjectData>(),ComponentType.ReadOnly<ObjectGeometryData>()))
  using(var entities=query.ToEntityArray(Allocator.Temp))snapshot=entities.ToArray();
  yield return true;
  Entity chosenEntity=Entity.Null;PrefabBase chosen=null;string chosenId=null;int matches=0;
  foreach(var e in snapshot){
   yield return true;if(!ValidParkingDecal(e,prefabs,out var prefab))continue;
   matches++;var id=prefab.GetPrefabID().ToString();
   if(chosen!=null&&(chosen.isSubscribedMod&&!prefab.isSubscribedMod||chosen.isSubscribedMod==prefab.isSubscribedMod&&StringComparer.Ordinal.Compare(chosenId,id)<=0))continue;
   chosen=prefab;chosenId=id;chosenEntity=e;
  }
  parkingDecalEntity=chosenEntity;if(chosenEntity!=Entity.Null)ReadParkingSize(chosenEntity);
  Trace.Event("catalog.parkingDecal.scan","id="+chosenId+" candidates="+matches);
 }
 // UI needs only catalog card/search fields, never recursive access geometry.
 internal string CatalogJson()=>Newtonsoft.Json.JsonConvert.SerializeObject(Assets.Select(a=>new{a.Id,Subscribed=subscribedAssets.Contains(a.Id),a.Name,a.NameKey,a.Icon,a.Width,a.Depth,a.GeometryWidth,a.GeometryDepth,a.Height,a.Homes,a.Footprint}));
}
}
