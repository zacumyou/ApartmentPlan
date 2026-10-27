using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Prefabs;
using Game.Rendering;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
namespace ApartmentPlan {
public partial class ConstructionTool {
 // Geometry snapshots only: never retain a Temp entity for rendering or scan the city per frame.
 internal struct WarningShape {internal float3 A,B;internal float Width;internal bool Candidate,Circle;}
 internal readonly List<WarningShape> WarningShapes=new List<WarningShape>();
 readonly HashSet<Entity> warningEntities=new HashSet<Entity>();
 string warningNotice="";
 void ClearWarnings(){WarningShapes.Clear();warningEntities.Clear();warningNotice="";}
 void WarningLine(float3 a,float3 b,float width,bool candidate=false,bool circle=false){
  if(WarningShapes.Count>=2048||!math.all(math.isfinite(a))||!math.all(math.isfinite(b))||!math.isfinite(width))return;
  WarningShapes.Add(new WarningShape{A=a,B=b,Width=math.clamp(width,.5f,128),Candidate=candidate,Circle=circle});
 }
 void WarningObject(Entity prefab,float3 position,quaternion rotation,bool candidate){
  float2 size=new float2(4);
  if(EntityManager.HasComponent<BuildingData>(prefab))size=(float2)EntityManager.GetComponentData<BuildingData>(prefab).m_LotSize*8;
  else if(EntityManager.HasComponent<ObjectGeometryData>(prefab)){var s=EntityManager.GetComponentData<ObjectGeometryData>(prefab).m_Size;size=new float2(s.x,s.z);}
  var forward=math.mul(rotation,new float3(0,0,math.max(.5f,size.y)*.5f));
  WarningLine(position-forward,position+forward,math.max(.5f,size.x),candidate);
 }
 void WarningEntity(Entity e,bool candidate=false){
  if(e==Entity.Null||!EntityManager.Exists(e)||WarningShapes.Count>=2048||!warningEntities.Add(e))return;
  Entity prefab=EntityManager.HasComponent<PrefabRef>(e)?EntityManager.GetComponentData<PrefabRef>(e).m_Prefab:Entity.Null;
  if(EntityManager.HasComponent<Game.Net.Curve>(e)){
   var curve=EntityManager.GetComponentData<Game.Net.Curve>(e).m_Bezier;float width=4;
   if(EntityManager.HasComponent<NetGeometryData>(prefab))width=EntityManager.GetComponentData<NetGeometryData>(prefab).m_DefaultWidth;
   if(EntityManager.HasComponent<Game.Net.Composition>(e)){var c=EntityManager.GetComponentData<Game.Net.Composition>(e).m_Edge;if(EntityManager.HasComponent<NetCompositionData>(c))width=EntityManager.GetComponentData<NetCompositionData>(c).m_Width;}
   int steps=math.clamp((int)(MathUtils.Length(curve)/3)+1,8,128);var previous=curve.a;
   for(int i=1;i<=steps;i++){var next=MathUtils.Position(curve,(float)i/steps);WarningLine(previous,next,width,candidate);previous=next;}
  }else if(EntityManager.HasComponent<Game.Net.Node>(e)){var position=EntityManager.GetComponentData<Game.Net.Node>(e).m_Position;WarningLine(position,position,8,candidate,true);}
  else if(EntityManager.HasComponent<Game.Objects.Transform>(e)){var t=EntityManager.GetComponentData<Game.Objects.Transform>(e);WarningObject(prefab,t.m_Position,t.m_Rotation,candidate);}
 }
 internal bool ShowWarnings=>Plans!=null&&Plans.Open&&Plans.Status=="AP.NativeBlocked"&&WarningShapes.Count>0&&!Demolishing&&World.GetExistingSystemManaged<Game.Tools.ToolSystem>()?.activeTool==this;
 internal void DrawWarnings(OverlayRenderSystem.Buffer buffer,float3 camera,bool hasCamera,float distance){
  foreach(var shape in WarningShapes){if(hasCamera&&math.distance((shape.A+shape.B)*.5f,camera)>distance+math.distance(shape.A,shape.B)+shape.Width)continue;
   var border=shape.Candidate?new Color(1,.55f,.08f,.95f):new Color(1,.12f,.16f,.95f);var fill=border;fill.a=.32f;
   if(shape.Circle)buffer.DrawCircle(border,fill,.4f,OverlayRenderSystem.StyleFlags.Projected,new float2(0,1),shape.A,shape.Width);
   else buffer.DrawLine(border,fill,.35f,OverlayRenderSystem.StyleFlags.Projected,new Line3.Segment(shape.A,shape.B),shape.Width,new float2(0));
  }
 }
}
}
