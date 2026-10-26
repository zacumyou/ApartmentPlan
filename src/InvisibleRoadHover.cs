using System;
using Game;
using Game.Common;
using Game.Prefabs;
using Game.Rendering;
using Game.Tools;
using Colossal.Mathematics;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
namespace ApartmentPlan {
// Render the native bulldozer hit, never a separate approximate deletion target.
// AP roads keep their display Marker; enabling all markers would expose unrelated subnets.
public partial class InvisibleRoadHover:GameSystemBase {
 protected override void OnUpdate(){
  if(!(World.GetExistingSystemManaged<ToolSystem>()?.activeTool is BulldozeToolSystem))return;
  var input=Game.Input.InputManager.instance;if(input.mouseOverUI||input.overlayActive)return;
  var ray=World.GetExistingSystemManaged<ToolRaycastSystem>();if(ray==null||!ray.GetRaycastResult(out var hit))return;
  var entity=hit.m_Owner;if(EntityManager.HasComponent<Game.Net.Node>(entity)&&EntityManager.HasComponent<Game.Net.Edge>(hit.m_Hit.m_HitEntity))entity=hit.m_Hit.m_HitEntity;if(entity==Entity.Null||!EntityManager.Exists(entity)||!EntityManager.HasComponent<PrefabRef>(entity))return;
  var prefabs=World.GetOrCreateSystemManaged<PrefabSystem>();
  if(!prefabs.TryGetPrefab<PrefabBase>(EntityManager.GetComponentData<PrefabRef>(entity).m_Prefab,out var prefab)||!prefab.name.StartsWith("AP Invisible ",StringComparison.Ordinal))return;
  var overlay=World.GetOrCreateSystemManaged<OverlayRenderSystem>();var buffer=overlay.GetBuffer(out var writer);writer.Complete();
  void Draw(Entity edge){
   if(!EntityManager.Exists(edge)||!EntityManager.HasComponent<Game.Net.Curve>(edge)||EntityManager.HasComponent<Deleted>(edge))return;
   var curve=EntityManager.GetComponentData<Game.Net.Curve>(edge).m_Bezier;
   float width=4;var pr=EntityManager.GetComponentData<PrefabRef>(edge).m_Prefab;if(EntityManager.HasComponent<NetGeometryData>(pr))width=EntityManager.GetComponentData<NetGeometryData>(pr).m_DefaultWidth;
   var color=new Color(1,.38f,.12f,.8f);var previous=curve.a;
   int steps=math.clamp((int)(MathUtils.Length(curve)/2)+1,8,128);
   for(int i=1;i<=steps;i++){var next=MathUtils.Position(curve,(float)i/steps);buffer.DrawLine(color,color,0,OverlayRenderSystem.StyleFlags.Projected,new Line3.Segment(previous,next),width,new float2(0));previous=next;}
  }
  if(EntityManager.HasComponent<Game.Net.Edge>(entity))Draw(entity);
  else if(EntityManager.HasBuffer<Game.Net.ConnectedEdge>(entity))foreach(var edge in EntityManager.GetBuffer<Game.Net.ConnectedEdge>(entity,true))Draw(edge.m_Edge);
  overlay.AddBufferWriter(default);
 }
}
}
