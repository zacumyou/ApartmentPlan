using System;
using System.Collections.Generic;
using System.Linq;
using Game.Rendering;
using Unity.Mathematics;
using UnityEngine;
namespace ApartmentPlan {
public partial class PlanTool {
 List<Point> roadGhost=new List<Point>();Point? roadCursor;bool roadGhostValid;
 void UpdateRoadDrawing(PlanSystem s,float3 position){
  CollectWorld(position);var edges=new List<SnapEdge>(nearby);
  // Main routes are stable through repacking; generated parking aisles are not.
  if(Mod.Options.SnapRoads&&s.Draft!=null)foreach(var r in s.Draft.Routes.Concat(s.Draft.ConstructionRoutes).Where(r=>r.Use=="main"))
   for(int i=1;i<r.Points.Count;i++)edges.Add(new SnapEdge(s.Draft.World(r.Points[i-1]),s.Draft.World(r.Points[i]),1));
  if(Mod.Options.SnapRoads)foreach(var points in s.RoadDraft.Samples)for(int i=1;i<points.Count;i++)edges.Add(new SnapEdge(points[i-1],points[i],1));
  var raw=new Point(position.x,position.z);
  var snap=BoundarySnap.Solve(raw,s.RoadPoints,edges,s.Draft?.Right??referenceAxis,Mod.Options.SnapDistance,Mod.Options.CloseDistance,Mod.Options.SnapDirections,Mod.Options.SnapGuides,false);
  // The display polyline is approximate. Put a snapped native connection back
  // on the real Bezier so CoursePos can find/split that same existing edge.
  if(Mod.Options.SnapRoads&&snap.Guides.Any(e=>e.Kind==1)){
   float best=(float)Mod.Options.SnapDistance;
   foreach(var curve in nativeRoadCurves){
    var target=new float3((float)snap.Position.X,position.y,(float)snap.Position.Y);
    float distance=Colossal.Mathematics.MathUtils.Distance(curve.xz,target.xz,out float t);
    var exact=Colossal.Mathematics.MathUtils.Position(curve,t);
    if(distance>=best||math.distance(exact.xz,position.xz)>Mod.Options.SnapDistance)continue;
    best=distance;snap.Position=new Point(exact.x,exact.z);
   }
  }
  roadCursor=snap.Position;roadGhost.Clear();roadGhostValid=Layout.Inside(snap.Position,s.Boundary);
  var controls=s.RoadPoints.Concat(new[]{snap.Position}).ToList();
  if(controls.Count==(s.RoadCurved?3:2)){
   try{roadGhost=new ManualRoad{Curved=s.RoadCurved,Points=controls}.Sample();roadGhostValid&=RoadRibbonInside(s,roadGhost);}
   catch(ArgumentException){roadGhostValid=false;}
  }else roadGhost=controls;
  Hint=roadGhostValid?s.RoadHint:"AP.ManualRoadFit";
  if(applyAction.WasPressedThisFrame()&&roadGhostValid)s.Click(WorldPoint(snap.Position,position.y));
  s.Elevation=position.y;DrawOutline(s,snap,roadGhostValid);
 }
 bool RoadRibbonInside(PlanSystem s,List<Point> points){
  double half=(s.Network("main")?.Width??Mod.Options.RoadWidth)/2;
  for(int i=1;i<points.Count;i++){
   var a=points[i-1];var d=points[i]-a;var normal=new Point(-d.Y,d.X)*(half/d.Length);int steps=Math.Max(1,(int)Math.Ceiling(d.Length/2));
   for(int j=0;j<=steps;j++){
    var p=a+d*((double)j/steps);
    foreach(var q in new[]{p-normal,p,p+normal})if(!Layout.Inside(q,s.Boundary)||s.Zones.Any(z=>Layout.Inside(q,z.Points)))return false;
   }
  }return true;
 }
}
}
