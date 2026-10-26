using System;
using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Game.Simulation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
namespace ApartmentPlan {
public partial class ConstructionTool {
 // ObjectToolBaseSystem submits these separately from ObjectDefinition.
 // Snapshot prefab buffers before creating entities: structural changes invalidate buffers.
 void SubmitBuildingChildren(Entity prefab,float3 position,quaternion rotation){
  var owner=new OwnerDefinition{m_Prefab=prefab,m_Position=position,m_Rotation=rotation};
  var transform=new Game.Objects.Transform{m_Position=position,m_Rotation=rotation};
  var nets=new List<Game.Prefabs.SubNet>();
  var nodes=new Dictionary<int,float4>();
  void Add(int index,float3 point){if(index<0)return;nodes.TryGetValue(index,out var sum);nodes[index]=sum+new float4(point,1);}
  if(EntityManager.HasBuffer<Game.Prefabs.SubNet>(prefab)){
   var buffer=EntityManager.GetBuffer<Game.Prefabs.SubNet>(prefab,true);
   var geometryLookup=GetComponentLookup<NetGeometryData>(true);
   bool left=World.GetOrCreateSystemManaged<Game.City.CityConfigurationSystem>().leftHandTraffic;
   for(int i=0;i<buffer.Length;i++){var original=buffer[i];Add(original.m_NodeIndex.x,original.m_Curve.a);Add(original.m_NodeIndex.y,original.m_Curve.d);nets.Add(Game.Net.NetUtils.GetSubNet(buffer,i,left,ref geometryLookup));}
  }
  var terrain=World.GetOrCreateSystemManaged<TerrainSystem>().GetHeightData();
  foreach(var net in nets){
   var geometry=EntityManager.HasComponent<NetGeometryData>(net.m_Prefab)?EntityManager.GetComponentData<NetGeometryData>(net.m_Prefab):default;
   var local=net.m_Curve;var curve=Game.Objects.ObjectUtils.LocalToWorld(position,rotation,local);
   bool startFixed=net.m_ParentMesh.x>=0,endFixed=net.m_ParentMesh.y>=0;
   bool onWater=(geometry.m_Flags&Game.Net.GeometryFlags.OnWater)!=0;
   if(onWater){
    var water=World.GetOrCreateSystemManaged<WaterSystem>().GetSurfaceData(out var waterDeps);waterDeps.Complete();
    var flat=local;flat.y=default(Bezier4x1);
    curve=Game.Net.NetUtils.AdjustPosition(new Game.Net.Curve{m_Bezier=Game.Objects.ObjectUtils.LocalToWorld(position,rotation,flat)},false,false,false,ref terrain,ref water).m_Bezier;
   }
   else if(!(startFixed&&endFixed)&&(geometry.m_Flags&Game.Net.GeometryFlags.FlattenTerrain)==0){
    curve=Game.Net.NetUtils.AdjustPosition(new Game.Net.Curve{m_Bezier=curve},startFixed,startFixed||endFixed,endFixed,ref terrain).m_Bezier;
    curve.a.y+=local.a.y;curve.b.y+=local.b.y;curve.c.y+=local.c.y;curve.d.y+=local.d.y;
   }
   float3 Endpoint(int index,float3 fallback){if(index<0)return fallback;var n=nodes[index];var point=Game.Objects.ObjectUtils.LocalToWorld(transform,n.xyz/math.max(1,n.w));if(onWater){var water=World.GetOrCreateSystemManaged<WaterSystem>().GetSurfaceData(out var deps);deps.Complete();point.y=WaterUtils.SampleHeight(ref water,ref terrain,point);}return point;}
   var course=new NetCourse{m_Curve=curve,m_Length=MathUtils.Length(curve),m_FixedIndex=-1,
    m_StartPosition=new CoursePos{m_Position=Endpoint(net.m_NodeIndex.x,curve.a),m_Rotation=Game.Net.NetUtils.GetNodeRotation(MathUtils.StartTangent(curve),rotation),m_CourseDelta=0,m_Elevation=local.a.y,m_ParentMesh=net.m_ParentMesh.x,m_Flags=CoursePosFlags.IsFirst},
    m_EndPosition=new CoursePos{m_Position=Endpoint(net.m_NodeIndex.y,curve.d),m_Rotation=Game.Net.NetUtils.GetNodeRotation(MathUtils.EndTangent(curve),rotation),m_CourseDelta=1,m_Elevation=local.d.y,m_ParentMesh=net.m_ParentMesh.y,m_Flags=CoursePosFlags.IsLast}};
   if(course.m_StartPosition.m_Position.Equals(course.m_EndPosition.m_Position)){course.m_StartPosition.m_Flags|=CoursePosFlags.IsLast;course.m_EndPosition.m_Flags|=CoursePosFlags.IsFirst;}
   var e=Definition(net.m_Prefab);EntityManager.AddComponentData(e,owner);EntityManager.AddComponentData(e,course);
   if(net.m_Upgrades!=default(CompositionFlags))EntityManager.AddComponentData(e,new Game.Net.Upgraded{m_Flags=net.m_Upgrades});
  }
  if(!EntityManager.HasBuffer<Game.Prefabs.SubArea>(prefab))return;
  var areas=new List<Tuple<Entity,Game.Areas.Node[],int>>();
  var areaBuffer=EntityManager.GetBuffer<Game.Prefabs.SubArea>(prefab,true);var nodeBuffer=EntityManager.GetBuffer<SubAreaNode>(prefab,true);
  var random=new Unity.Mathematics.Random((uint)(12345+definitions.Count*7919));
  using(var selected=new NativeParallelHashMap<Entity,int>(10,Allocator.Temp))
  foreach(var source in areaBuffer){
   var area=source;int seed=random.NextInt();
   if(EntityManager.HasBuffer<PlaceholderObjectElement>(area.m_Prefab)&&!Game.Areas.AreaUtils.SelectAreaPrefab(EntityManager.GetBuffer<PlaceholderObjectElement>(area.m_Prefab,true),GetComponentLookup<SpawnableObjectData>(true),selected,ref random,out area.m_Prefab,out seed))continue;
   bool tile=Plans.Draft.Villa?Mod.Options.VillaTileLots:Mod.Options.TileBuildingLots;
   if(Plans.Draft.Tower&&Plans.BuildingPrefabs.TryGetValue(Mod.Options.TowerParkingAsset,out var parking)&&parking==prefab)tile=false;
   if(tile&&EntityManager.HasComponent<SurfaceData>(area.m_Prefab)&&!EntityManager.HasComponent<NavigationAreaData>(area.m_Prefab)&&World.GetOrCreateSystemManaged<PrefabSystem>().TryGetPrefab<PrefabBase>(area.m_Prefab,out var areaPrefab)&&!areaPrefab.Has<ClipArea>())continue;
   if(!EntityManager.HasComponent<AreaGeometryData>(area.m_Prefab))throw new InvalidOperationException("Building area has no geometry: "+area.m_Prefab);
   var range=area.m_NodeRange;if(range.x<0||range.y>nodeBuffer.Length||range.y-range.x<3)throw new InvalidOperationException("Invalid building area node range");
   var points=new Game.Areas.Node[range.y-range.x+1];int index=ObjectToolBaseSystem.GetFirstNodeIndex(nodeBuffer,range);
   for(int i=0;i<points.Length;i++){var n=nodeBuffer[index];points[i]=new Game.Areas.Node(Game.Objects.ObjectUtils.LocalToWorld(transform,n.m_Position),n.m_ParentMesh>=0?n.m_Position.y:float.MinValue);if(++index==range.y)index=range.x;}
   areas.Add(Tuple.Create(area.m_Prefab,points,seed));
  }
  foreach(var area in areas){var type=EntityManager.GetComponentData<AreaGeometryData>(area.Item1).m_Type;var e=Definition(area.Item1);var creation=EntityManager.GetComponentData<CreationDefinition>(e);creation.m_RandomSeed=area.Item3;if(type!=Game.Areas.AreaType.Lot)creation.m_Flags|=CreationFlags.Hidden;EntityManager.SetComponentData(e,creation);EntityManager.AddComponentData(e,owner);var buffer=EntityManager.AddBuffer<Game.Areas.Node>(e);foreach(var point in area.Item2)buffer.Add(point);}
 }
}
}
