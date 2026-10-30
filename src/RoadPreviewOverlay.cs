using Colossal.Mathematics;
using Game.Rendering;
using Unity.Mathematics;
using UnityEngine;
namespace ApartmentPlan {
public partial class PlanTool {
 // Submit after the filled plan so parks/buildings cannot wash out the road ghost.
 internal int DrawRoadPreview(OverlayRenderSystem.Buffer buffer,PlanSystem s){
  if(s.Mode!="road")return 0;int count=0;float width=s.Network("main")?.Width??Mod.Options.RoadWidth;
  void Segment(Point a,Point b,bool valid,bool fill){
   if((b-a).Length<.01)return;
   var line=new Line3.Segment(WorldPoint(a,s.Elevation),WorldPoint(b,s.Elevation));
   var color=valid?new Color(.02f,.65f,1f,.95f):new Color(1f,.12f,.18f,.95f);
   if(fill){buffer.DrawLine(new Color(.02f,.06f,.1f,1),color,.65f,OverlayRenderSystem.StyleFlags.Projected,line,width,new float2(0));count++;}
   buffer.DrawDashedLine(Color.white,Color.white,0,OverlayRenderSystem.StyleFlags.Projected,line,.55f,3,2);count++;
  }
  foreach(var points in s.RoadDraft.Samples)for(int i=1;i<points.Count;i++)Segment(points[i-1],points[i],true,true);
  for(int i=1;i<roadGhost.Count;i++)Segment(roadGhost[i-1],roadGhost[i],roadGhostValid,!s.RoadCurved||s.RoadPoints.Count>=2);
  return count;
 }
}
}
