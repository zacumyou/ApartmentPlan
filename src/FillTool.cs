using System;
using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
namespace ApartmentPlan {
public partial class PlanSystem {
 internal int FillRadius=250;
 string fillPreviousMode="boundary";
 internal bool CanFill=>(Mode=="boundary"||Mode=="fill")&&!NativeCommitted&&EditingPlan<0&&Boundary.Count==0&&!LayoutPending;
 internal void AcceptFill(List<Point> points,float elevation){if(Mode!="fill"||!CanFill||!Layout.Valid(points))return;Boundary.AddRange(points);Elevation=elevation;Mode="boundary";Command("finish","");Revision++;}
 void ToggleFill(){if(Mode=="fill"){Mode=fillPreviousMode;Status="AP.Draw";}else if(CanFill){fillPreviousMode=Mode;Mode="fill";Status="AP.FillHint";Activate();}Revision++;}
}
public partial class PlanTool {
 readonly List<RoadFill.Edge> fillRoads=new List<RoadFill.Edge>();
 Point fillRoadPosition;float fillRoadTime=-10,fillRoadElevation;int fillRoadRadius;
 List<Point> fillPreview=new List<Point>();Point fillPosition;float fillTime=-10;int fillRadius;
 void UpdateFill(PlanSystem s,float3 position,bool pressed){
  var raw=new Point(position.x,position.z);
  if(fillRadius!=s.FillRadius||UnityEngine.Time.unscaledTime-fillTime>.05f&&((raw-fillPosition).Length>1||UnityEngine.Time.unscaledTime-fillTime>2)||pressed){
   fillPosition=raw;fillRadius=s.FillRadius;fillTime=UnityEngine.Time.unscaledTime;
   // Reuse a padded local road snapshot while the pointer remains in its core.
   // ECS search/job synchronization and curve sampling are not hover-frame work.
   bool refresh=fillRoadRadius!=fillRadius||fillRoadTime<0||(raw-fillRoadPosition).Length>48||Math.Abs(position.y-fillRoadElevation)>2||UnityEngine.Time.unscaledTime-fillRoadTime>2||pressed&&UnityEngine.Time.unscaledTime-fillRoadTime>.25f;
   if(refresh){
   fillRoadPosition=raw;fillRoadElevation=position.y;fillRoadRadius=fillRadius;fillRoadTime=UnityEngine.Time.unscaledTime;fillRoads.Clear();
   var edges=fillRoads;var box=new Bounds2(position.xz-fillRadius-64,position.xz+fillRadius+64);
   using(var entities=new NativeList<Entity>(Allocator.Temp)){
    var tree=World.GetOrCreateSystemManaged<Game.Net.SearchSystem>().GetNetSearchTree(true,out var job);job.Complete();var query=new NearbyEntity{Box=box,Items=entities};tree.Iterate(ref query);var seen=new HashSet<Entity>();
    foreach(var e in entities){if(!seen.Add(e)||!Live(e)||!EntityManager.HasComponent<Road>(e)||EntityManager.HasComponent<Owner>(e)||!EntityManager.HasComponent<Game.Net.Curve>(e)||!EntityManager.HasComponent<PrefabRef>(e))continue;
     var prefab=EntityManager.GetComponentData<PrefabRef>(e).m_Prefab;if(!EntityManager.HasComponent<NetGeometryData>(prefab))continue;
     double width=EntityManager.GetComponentData<NetGeometryData>(prefab).m_DefaultWidth;
     var curve=EntityManager.GetComponentData<Game.Net.Curve>(e).m_Bezier;
     if(Math.Abs((curve.a.y+curve.d.y)*.5-position.y)>4)continue;
     int steps=math.clamp((int)Math.Ceiling((math.distance(curve.a,curve.b)+math.distance(curve.b,curve.c)+math.distance(curve.c,curve.d))/8),2,128);
     var previous=curve.a;for(int i=1;i<=steps;i++){var p=MathUtils.Position(curve,i/(float)steps);edges.Add(new RoadFill.Edge(new Point(previous.x,previous.z),new Point(p.x,p.z),width));previous=p;}
     if(edges.Count>4096)break;
    }
   }
   }
   fillPreview=RoadFill.Solve(raw,fillRoads,fillRadius);
  }
  s.Elevation=position.y;Hint=fillPreview.Count>=3?"AP.FillReady":"AP.FillEmpty";
  if(pressed&&fillPreview.Count>=3){s.AcceptFill(fillPreview,position.y);fillPreview.Clear();fillTime=-10;}
  DrawOutline(s,null,false);
 }
}
}
