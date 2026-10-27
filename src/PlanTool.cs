using System;
using System.Collections.Generic;
using System.Linq;
using Colossal.Mathematics;
using Game.Common;
using Game.Prefabs;
using Game.Rendering;
using Game.Tools;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
namespace ApartmentPlan {
public partial class PlanTool:ToolBaseSystem {
 public override string toolID=>"ApartmentPlan.Tool";
 public override PrefabBase GetPrefab()=>null;
 public override bool TrySetPrefab(PrefabBase prefab)=>false;
 int hoverEntrance=-1,hoverParcel=-1,hoverZone=-1,hoverZoneVertex=-1;
 List<Point> beforeInsert;int drag=-1;bool edgeDrag,dragMoved;int hover=-1;bool hoverEdge;string dragMode;Point dragOrigin;List<Point> original,originalEntrances;Point referenceAxis=new Point(1,0);int lastCount;
 internal string Hint="";
 bool pendingPress,clearPreview;Vector2 pressScreen;
 void CapturePress(Game.Input.ProxyAction action,InputActionPhase phase){if(phase==InputActionPhase.Started&&Mouse.current!=null){pressScreen=Mouse.current.position.ReadValue();pendingPress=true;}}
 bool GroundAtScreen(Vector2 screen,float elevation,out Point point){point=default;var camera=World.GetExistingSystemManaged<CameraUpdateSystem>()?.activeCamera;if(camera==null)return false;var ray=camera.ScreenPointToRay(screen);if(!new Plane(Vector3.up,new Vector3(0,elevation,0)).Raycast(ray,out float distance))return false;var p=ray.GetPoint(distance);point=new Point(p.x,p.z);return true;}
 protected override void OnStartRunning(){Trace.Event("tool.start");base.OnStartRunning();clearPreview=true;applyActionOverride=Mod.Options.GetAction("Click");secondaryApplyActionOverride=Mod.Options.GetAction("Back");pendingPress=false;applyAction.onInteraction+=CapturePress;applyAction.shouldBeEnabled=true;secondaryApplyAction.shouldBeEnabled=true;cacheTime=-10;}
 void CancelDrag(PlanSystem s){if(drag>=0&&s!=null&&original!=null&&s.Boundary.Count==original.Count){s.Boundary.Clear();s.Boundary.AddRange(beforeInsert??original);s.Entrances.Clear();s.Entrances.AddRange(originalEntrances);s.Entrance=s.Entrances.Count>0?(Point?)s.Entrances[0]:null;Trace.Event("boundary.drag.cancel");}drag=-1;original=null;originalEntrances=null;beforeInsert=null;dragMoved=false;}
 protected override void OnStopRunning(){CancelDrag(World.GetExistingSystemManaged<PlanSystem>());pendingPress=false;fillPreview.Clear();fillTime=-10;fillRoadTime=-10;applyAction.onInteraction-=CapturePress;Hint="";Trace.Event("tool.stop");applyAction.shouldBeEnabled=false;secondaryApplyAction.shouldBeEnabled=false;base.OnStopRunning();}
 public override void InitializeRaycast(){base.InitializeRaycast();m_ToolRaycastSystem.typeMask=TypeMask.Terrain;m_ToolRaycastSystem.collisionMask=CollisionMask.OnGround;}
 protected override JobHandle OnUpdate(JobHandle deps){applyMode=clearPreview?ApplyMode.Clear:ApplyMode.None;clearPreview=false;var s=World.GetExistingSystemManaged<PlanSystem>();if(s==null)return deps;if(drag>=0&&s.Boundary.Count!=original.Count){drag=-1;original=null;}bool blocked=s.CatalogLoading||s.LayoutPending||!s.Open||s.Focused||!Application.isFocused||Game.Input.InputManager.instance.hasInputFieldFocus||Game.Input.InputManager.instance.overlayActive;
  bool capturedPress=pendingPress;pendingPress=false;
  hover=hoverEntrance=hoverParcel=hoverZone=hoverZoneVertex=-1;
  if(blocked){fillPreview.Clear();fillTime=-10;if(drag>=0)CancelDrag(s);roadGhost.Clear();roadCursor=null;Hint="";Trace.Sample("input.blocked","open="+s.Open+" panel="+s.Focused);DrawOutline(s,null,false);return deps;}
  deps.Complete();if(s.Boundary.Count==0&&lastCount!=0)referenceAxis=new Point(1,0);lastCount=s.Boundary.Count;
  bool secondary=secondaryApplyAction.WasPressedThisFrame();if(secondary&&drag>=0){CancelDrag(s);secondary=false;}
  bool hitGround=GetRaycastResult(out Entity entity,out RaycastHit hit);if(!hitGround){fillPreview.Clear();fillTime=-10;Hint="";if(drag>=0&&!applyAction.IsPressed())CancelDrag(s);DrawOutline(s,null,false);return deps;}
  if(s.Mode=="fill"){if(secondary){fillPreview.Clear();s.Request("fill");return deps;}UpdateFill(s,hit.m_HitPosition,capturedPress||applyAction.WasPressedThisFrame());return deps;}
  fillPreview.Clear();fillTime=-10;fillRoadTime=-10;
  UpdateHover(s,new Point(hit.m_HitPosition.x,hit.m_HitPosition.z));
  if(secondary){if(!s.ToggleVillaConnection(new Point(hit.m_HitPosition.x,hit.m_HitPosition.z)))s.Request("undo");DrawOutline(s,null,false);return deps;}
  if(s.Mode=="road"){UpdateRoadDrawing(s,hit.m_HitPosition);return deps;}
  if(s.Mode=="zone"){
   CollectWorld(hit.m_HitPosition);var point=new Point(hit.m_HitPosition.x,hit.m_HitPosition.z);
   var zoneSnap=BoundarySnap.Solve(point,s.ZonePoints,nearby,s.Draft!=null?s.Draft.Right:referenceAxis,Mod.Options.SnapDistance,Mod.Options.CloseDistance,Mod.Options.SnapDirections,Mod.Options.SnapGuides,true);
   bool valid=zoneSnap.Close?SiteFrame.ContainsPolygon(s.Boundary,s.ZonePoints):Layout.Inside(zoneSnap.Position,s.Boundary)&&Layout.CanAppend(s.ZonePoints,zoneSnap.Position);
   Hint=zoneSnap.Close?"AP.CloseSnap":"AP.DrawZone";
   if(valid&&(capturedPress||applyAction.WasPressedThisFrame()))s.Click(WorldPoint(zoneSnap.Position,hit.m_HitPosition.y));DrawOutline(s,zoneSnap,valid);return deps;
  }
  if(s.Mode=="entrance"&&hoverEntrance>=0&&capturedPress){Hint="AP.ExistingEntrance";DrawOutline(s,null,false);return deps;}
  bool drawing=s.Mode=="boundary"||s.Mode=="edit"||s.Mode=="preview"||s.Mode=="entrance";if(!drawing){Hint="";DrawOutline(s,null,false);if(applyAction.WasPressedThisFrame())s.Click(hit.m_HitPosition);return deps;}
  CollectWorld(hit.m_HitPosition);var raw=new Point(hit.m_HitPosition.x,hit.m_HitPosition.z);var snap=BoundarySnap.Solve(raw,drag>=0?new List<Point>():s.Boundary,nearby,referenceAxis,Mod.Options.SnapDistance,Mod.Options.CloseDistance,Mod.Options.SnapDirections,Mod.Options.SnapGuides,s.Mode=="boundary"&&drag<0);
  // Read the press position at the input event, before a fast drag has moved
  // the cursor. The terrain raycast can otherwise report only its final point.
  if(drag<0&&capturedPress&&s.Mode!="boundary"&&s.Mode!="entrance"&&GroundAtScreen(pressScreen,s.Elevation,out var start)){
   int picked=BoundaryEditing.Hit(s.Boundary,start,true,out bool pickedEdge);
   if(picked>=0)BeginBoundaryDrag(s,picked,pickedEdge,start);
  }
  if(drag>=0){
   if(Mouse.current!=null&&GroundAtScreen(Mouse.current.position.ReadValue(),s.Elevation,out var current))raw=current;
   if(s.Mode!=dragMode){CancelDrag(s);DrawOutline(s,null,false);return deps;}
   if((raw-dragOrigin).Length>.75)dragMoved=true;
   if(dragMoved){
    var candidate=original[drag]+raw-dragOrigin;
    var moved=BoundarySnap.Solve(candidate,new List<Point>(),nearby,referenceAxis,Mod.Options.SnapDistance,Mod.Options.CloseDistance,Mod.Options.SnapDirections,false,false);
    BoundaryEditing.Move(original,s.Boundary,drag,edgeDrag,moved.Position-original[drag]);
    for(int i=0;i<s.Entrances.Count;i++)s.Entrances[i]=BoundaryEditing.Follow(original,s.Boundary,originalEntrances[i]);
    s.Entrance=s.Entrances.Count>0?(Point?)s.Entrances[0]:null;
   }
   if(!applyAction.IsPressed()){
    bool changed=dragMoved||beforeInsert!=null;bool clickEntrance=!changed&&dragMode=="entrance";
    bool valid=s.Mode=="boundary"?Layout.ValidOpen(s.Boundary):Layout.Valid(s.Boundary);
    if(valid&&s.Mode!="boundary")foreach(var zone in s.Zones)if(!SiteFrame.ContainsPolygon(s.Boundary,zone.Points)){valid=false;break;}
    if(!changed||!valid){CancelDrag(s);if(changed){s.Status="AP.InvalidBoundary";s.Revision++;}}
    else{Trace.Event("boundary.drag.commit","index="+drag+" edge="+edgeDrag+" mode="+dragMode);drag=-1;original=null;originalEntrances=null;beforeInsert=null;dragMoved=false;s.BoundaryEdited();s.Sound(PlanSound.Point);}
    if(clickEntrance)s.Click(WorldPoint(dragOrigin,hit.m_HitPosition.y));
   }
   Hint="AP.BoundaryDrag";DrawOutline(s,null,false);return deps;
  }
  hover=BoundaryEditing.Hit(s.Boundary,raw,s.Mode!="boundary",out hoverEdge);
  bool validCandidate=snap.Close?Layout.Valid(s.Boundary):Layout.CanAppend(s.Boundary,snap.Position);
  var last=s.Boundary.Count>0?s.Boundary[s.Boundary.Count-1]:snap.Position;double length=(snap.Position-last).Length;double angle=0;if(s.Boundary.Count>=2){var previous=last-s.Boundary[s.Boundary.Count-2];var next=snap.Position-last;angle=Math.Atan2(Math.Abs(Layout.Cross(previous,next)),BoundarySnap.Dot(previous,next))*180/Math.PI;}
  Hint=snap.Close?"AP.CloseSnap":string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0:0.0}m / {1:0.0} deg",length,angle,snap.Kind);if(s.Mode!="boundary")Hint="AP.BoundaryDrag";if(s.Mode=="entrance"&&!s.Villa){var gate=SiteFrame.Nearest(s.Boundary,raw,out _);Hint=(raw-gate).Length<=16&&EntranceSpacing.Near(gate,s.Entrances)?"AP.EntranceNearbyAdvisory":"AP.PickEntrance";}Trace.Sample("snap.kind","kind="+snap.Kind+" close="+snap.Close);
  if((capturedPress||applyAction.WasPressedThisFrame())&&drag<0){
   if(snap.Close){if(validCandidate)s.Request("finish");else Trace.Event("boundary.close.rejected","invalid polygon");}
   else {
    if(s.Mode=="entrance")s.Click(hit.m_HitPosition);
    else if(hover>=0)BeginBoundaryDrag(s,hover,hoverEdge,raw);
    else if(s.Mode=="boundary"&&validCandidate){if(s.Boundary.Count==0&&snap.Axis.Length>.5)referenceAxis=snap.Axis;s.Click(WorldPoint(snap.Position,hit.m_HitPosition.y));}
    else Trace.Event("boundary.point.rejected","mode="+s.Mode+" selfCross="+!validCandidate);
   }
  }
  s.Elevation=hit.m_HitPosition.y;DrawOutline(s,snap,validCandidate);return deps;
 }
 void BeginBoundaryDrag(PlanSystem s,int picked,bool edge,Point cursor){
  beforeInsert=null;
  if(edge&&(s.Mode=="edit"||s.Mode=="preview")){
   var previous=new List<Point>(s.Boundary);int inserted=BoundaryEditing.Insert(s.Boundary,cursor);
   if(inserted<0)return;beforeInsert=previous;picked=inserted;edge=false;
  }
  drag=picked;edgeDrag=edge;dragMoved=false;dragMode=s.Mode;original=new List<Point>(s.Boundary);originalEntrances=new List<Point>(s.Entrances);dragOrigin=cursor;
  Trace.Event("boundary.drag.begin","index="+drag+" inserted="+(beforeInsert!=null)+" mode="+dragMode);
 }
 void UpdateHover(PlanSystem s,Point p){
  for(int i=0;i<s.ZonePoints.Count;i++)if((s.ZonePoints[i]-p).Length<3){hoverZoneVertex=i;break;}
  for(int i=0;i<s.Entrances.Count;i++)if((s.Entrances[i]-p).Length<5){hoverEntrance=i;break;}
  for(int i=0;i<s.Zones.Count;i++)if(Layout.Inside(p,s.Zones[i].Points)){hoverZone=i;break;}
  if(s.Draft==null)return;var local=s.Draft.Local(p);double best=double.MaxValue;
  for(int i=0;i<s.Draft.Parcels.Count;i++){var r=s.Draft.Parcels[i];if(r.Kind==4||r.Kind==8||s.Mode=="replace"&&r.Kind!=0||s.Mode=="ramp"&&r.Kind!=6)continue;
   if(local.X>=r.X&&local.X<=r.X+r.W&&local.Y>=r.Y&&local.Y<=r.Y+r.D&&r.Area<best){best=r.Area;hoverParcel=i;}}
 }
 static float3 WorldPoint(Point p,float y)=>new float3((float)p.X,y,(float)p.Y);
 void DrawOutline(PlanSystem s,SnapResult snap,bool valid){if(!s.Open||s.Boundary.Count==0&&snap==null&&fillPreview.Count==0)return;var system=World.GetOrCreateSystemManaged<OverlayRenderSystem>();var b=system.GetBuffer(out var writer);writer.Complete();float y=s.Elevation;var blue=new Color(.31f,.76f,.97f,.85f);var green=new Color(.49f,.85f,.55f,.95f);var red=new Color(.94f,.32f,.32f,.9f);
  void Line(Point a,Point c,Color color,float width,bool dashed=false){var segment=new Line3.Segment(WorldPoint(a,y),WorldPoint(c,y));if((c-a).Length<.01)return;if(dashed)b.DrawDashedLine(color,color,0,OverlayRenderSystem.StyleFlags.Projected,segment,width,3,2);else b.DrawLine(color,color,0,OverlayRenderSystem.StyleFlags.Projected,segment,width,new float2(0));}
  if(s.Mode=="fill"&&fillPreview.Count>=3){for(int i=0;i<fillPreview.Count;i++)Line(fillPreview[i],fillPreview[(i+1)%fillPreview.Count],green,1.3f,true);}
  if(s.Villa)foreach(var gate in s.VillaConnections){var c=gate.Disabled?red:green;Line(gate.Boundary-new Point(3,0),gate.Boundary+new Point(3,0),c,1);Line(gate.Boundary-new Point(0,3),gate.Boundary+new Point(0,3),c,1);if(!gate.Disabled)Line(gate.Boundary,gate.Road,c,1);}

  void Ring(Point p,Color c,float size,bool active=false){b.DrawCircle(c,new Color(c.r,c.g,c.b,active?.7f:.1f),.24f,OverlayRenderSystem.StyleFlags.Projected,new float2(0,1),WorldPoint(p,y),size);}
  for(int i=0;i<s.Boundary.Count;i++){if(i+1<s.Boundary.Count||s.Mode!="boundary")Line(s.Boundary[i],s.Boundary[(i+1)%s.Boundary.Count],blue,.35f);Ring(s.Boundary[i],i==0&&snap?.Close==true?green:Color.white,i==drag||i==hover&&!hoverEdge?4.2f:2.4f,i==drag||i==hover&&!hoverEdge);}
  if(hover>=0&&hover<s.Boundary.Count&&hoverEdge&&(s.Mode=="preview"||s.Mode=="edit"||s.Mode=="entrance"))Line(s.Boundary[hover],s.Boundary[(hover+1)%s.Boundary.Count],Color.white,1f);
  if(s.Mode=="entrance"&&!s.Villa)foreach(var gate in s.Entrances){
   var fill=new Color(1f,.7f,.25f,.055f);var dotted=new Color(1f,.7f,.25f,.65f);
   b.DrawCircle(Color.clear,fill,0,OverlayRenderSystem.StyleFlags.Projected,new float2(0,1),WorldPoint(gate,y),(float)(EntranceSpacing.Radius*2));
   for(int n=0;n<128;n++){double a=n*Math.PI*2/128;var point=gate+new Point(Math.Cos(a),Math.Sin(a))*EntranceSpacing.Radius;b.DrawCircle(dotted,dotted,0,OverlayRenderSystem.StyleFlags.Projected,new float2(0,1),WorldPoint(point,y),1.2f);}
  }
  for(int i=0;i<s.Entrances.Count;i++)Ring(s.Entrances[i],green,i==hoverEntrance?8:6,i==hoverEntrance);
  if(s.Mode=="road"){
   foreach(var road in s.Roads){var points=road.Sample();bool active=roadCursor.HasValue&&points.Skip(1).Select((p,i)=>new SnapEdge(points[i],p,1)).Any(e=>(BoundarySnap.Nearest(roadCursor.Value,e)-roadCursor.Value).Length<4);
    for(int i=1;i<points.Count;i++)Line(points[i-1],points[i],active?Color.white:blue,active?1.4f:.5f);
    Ring(points[0],blue,active?5:3,active);Ring(points[points.Count-1],blue,active?5:3,active);
   }
   foreach(var points in s.RoadDraft.Samples){
    for(int i=1;i<points.Count;i++){Line(points[i-1],points[i],new Color(green.r,green.g,green.b,.28f),s.Network("main")?.Width??Mod.Options.RoadWidth);Line(points[i-1],points[i],green,.45f,true);}
    Ring(points[0],green,3);Ring(points[points.Count-1],green,3);
   }
   foreach(var point in s.RoadPoints)Ring(point,green,4,true);
   var tint=roadGhostValid?green:red;var fill=new Color(tint.r,tint.g,tint.b,.35f);
   for(int i=1;i<roadGhost.Count;i++){if(!s.RoadCurved||s.RoadPoints.Count>=2)Line(roadGhost[i-1],roadGhost[i],fill,s.Network("main")?.Width??Mod.Options.RoadWidth);Line(roadGhost[i-1],roadGhost[i],tint,.35f,true);}
  }
  for(int z=0;z<s.Zones.Count;z++){var zone=s.Zones[z];for(int i=0;i<zone.Points.Count;i++)Line(zone.Points[i],zone.Points[(i+1)%zone.Points.Count],z==hoverZone?Color.white:zone.Kind==5?new Color(.75f,.65f,.9f,.95f):green,z==hoverZone?1.2f:.5f,z!=hoverZone);}
  if(hoverParcel>=0&&s.Draft!=null&&hoverParcel<s.Draft.Parcels.Count){var corners=s.Draft.Parcels[hoverParcel].Corners;for(int i=0;i<4;i++)Line(s.Draft.World(corners[i]),s.Draft.World(corners[(i+1)%4]),Color.white,1.1f);}
  if(s.Mode=="zone"){
   for(int i=0;i<s.ZonePoints.Count;i++){Ring(s.ZonePoints[i],green,i==hoverZoneVertex?4.2f:2.4f,i==hoverZoneVertex);if(i>0)Line(s.ZonePoints[i-1],s.ZonePoints[i],green,.5f);}
   if(snap!=null&&s.ZonePoints.Count>0){Line(s.ZonePoints[s.ZonePoints.Count-1],snap.Position,valid?green:red,.5f);if(s.ZonePoints.Count>=2)Line(snap.Position,s.ZonePoints[0],green,.25f,true);}
  }
  if(s.ReplacementIndex>=0&&s.Draft!=null){var corners=s.Draft.Parcels[s.ReplacementIndex].Corners;for(int i=0;i<4;i++)Line(s.Draft.World(corners[i]),s.Draft.World(corners[(i+1)%4]),Color.white,.7f);}
  if(snap!=null){var color=!valid&&s.Mode=="boundary"?red:snap.Close?green:snap.Kind==1?new Color(1,.7f,.25f,.95f):snap.Kind==2?new Color(.8f,.6f,1,.95f):snap.Kind==3?green:snap.Kind==6?Color.white:blue;
   if(Mod.Options.ShowGuides)foreach(var line in snap.Guides){var d=line.B-line.A;d=d*(1/d.Length);if(line.Infinite)Line(snap.Position-d*40,snap.Position+d*40,color,.25f,true);else Line(line.A,line.B,color,.55f);}
   if(s.Mode=="boundary"&&drag<0&&s.Boundary.Count>0){Line(s.Boundary[s.Boundary.Count-1],snap.Position,color,.35f);if(s.Boundary.Count>=2)Line(snap.Position,s.Boundary[0],color,.25f,true);}
   Ring(snap.Position,color,snap.Kind==0?3.6f:4.8f);if(snap.Close)Ring(snap.Position,green,7f);
  }
  system.AddBufferWriter(default);
 }
}
}



