using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
// Resolve conflicts in creation order, then prune whole components that have no
// surviving, explicitly validated road approach. Mere proximity is not access.
public static class TowerWalkPolicy {
 public static bool ApproachAngle(PlanBezier walk,PlanBezier road,double degrees){
  var a=walk.D-walk.A;var b=road.D-road.A;if(a.Length<.001||b.Length<.001)return false;
  return Math.Abs(SiteFrame.Dot(VillaGeometry.Unit(a),VillaGeometry.Unit(b)))<=Math.Cos(degrees*Math.PI/180)+1e-8;
 }
 sealed class Span {public PlanBezier Curve;public double Width;}
 public static bool ParallelConflict(PlanBezier a,double aw,PlanBezier b,double bw,double gap){
  if(!TowerConstruction.Straight(a)||!TowerConstruction.Straight(b))return false;
  var d=a.D-a.A;double length=d.Length;if(length<.001)return false;var u=d*(1/length);var v=VillaGeometry.Unit(b.D-b.A);
  if(Math.Abs(Layout.Cross(u,v))>1e-6)return false;
  double begin=Math.Max(0,Math.Min(SiteFrame.Dot(b.A-a.A,u),SiteFrame.Dot(b.D-a.A,u))),end=Math.Min(length,Math.Max(SiteFrame.Dot(b.A-a.A,u),SiteFrame.Dot(b.D-a.A,u)));
  return end-begin>.001&&Math.Abs(Layout.Cross(u,b.A-a.A))-(aw+bw)/2<=gap+.00001;
 }
 static bool OnStraight(Point p,PlanBezier c)=>TowerConstruction.Straight(c)&&(VillaGeometry.Nearest(p,c.A,c.D)-p).Length<.001;
 static bool Connected(PlanBezier a,PlanBezier b){
  if(new[]{a.A,a.D}.Any(x=>new[]{b.A,b.D}.Any(y=>(x-y).Length<.001)))return true;
  if(new[]{a.A,a.D}.Any(x=>OnStraight(x,b))||new[]{b.A,b.D}.Any(x=>OnStraight(x,a)))return true;
  if(!TowerConstruction.Straight(a)||!TowerConstruction.Straight(b))return false;
  var u=a.D-a.A;var v=b.D-b.A;double cross=Layout.Cross(u,v);if(Math.Abs(cross)<1e-8)return false;
  double t=Layout.Cross(b.A-a.A,v)/cross,s=Layout.Cross(b.A-a.A,u)/cross;return t>=0&&t<=1&&s>=0&&s<=1;
 }
 // Curved spans were not covered by the straight-only parallel rule.
 public static bool CurveConflict(PlanBezier a,double aw,PlanBezier b,double bw){
  if(TowerConstruction.Straight(a)&&TowerConstruction.Straight(b))return false;
  double clearance=(aw+bw)/2+.25;
  // The cubic is inside its control hull; reject distant pairs before sampling.
  var ah=new[]{a.A,a.B,a.C,a.D};var bh=new[]{b.A,b.B,b.C,b.D};
  if(ah.Max(q=>q.X)+clearance<bh.Min(q=>q.X)||bh.Max(q=>q.X)+clearance<ah.Min(q=>q.X)||ah.Max(q=>q.Y)+clearance<bh.Min(q=>q.Y)||bh.Max(q=>q.Y)+clearance<ah.Min(q=>q.Y))return false;
  var ap=VillaGeometry.Sample(a,.5);var bp=VillaGeometry.Sample(b,.5);
  var joints=new[]{a.A,a.D}.Where(x=>new[]{b.A,b.D}.Any(y=>(x-y).Length<.001)).ToList();
  foreach(var x in ap){if(joints.Any(j=>(x-j).Length<clearance*2))continue;
   for(int i=1;i<bp.Count;i++){var nearest=VillaGeometry.Nearest(x,bp[i-1],bp[i]);if(joints.Any(j=>(nearest-j).Length<clearance*2))continue;if((nearest-x).Length<clearance)return true;}
  }return false;
 }
 public static void Apply(Plan p,ISet<PlanBezier> approaches,TowerRules rules){
  var kept=new List<Span>();int parallel=0;
  foreach(var route in p.Routes.Where(r=>r.Use=="pedestrian"))foreach(var curve in route.Curves){
   if(kept.Any(old=>(ParallelConflict(curve,route.Width,old.Curve,old.Width,rules["WalkParallelGap"])||CurveConflict(curve,route.Width,old.Curve,old.Width)))){parallel++;continue;}
   kept.Add(new Span{Curve=curve,Width=route.Width});
  }
  var reached=new HashSet<int>();var queue=new List<int>();for(int i=0;i<kept.Count;i++)if(approaches.Contains(kept[i].Curve)){reached.Add(i);queue.Add(i);}
  for(int cursor=0;cursor<queue.Count;cursor++){int i=queue[cursor];for(int j=0;j<kept.Count;j++)if(!reached.Contains(j)&&Connected(kept[i].Curve,kept[j].Curve)){reached.Add(j);queue.Add(j);}}
  p.Routes.RemoveAll(r=>r.Use=="pedestrian");
  // Individual original cubics preserve gaps left by a removed span; concatenating
  // the remaining Points would silently draw a replacement bridge across it.
  for(int i=0;i<kept.Count;i++)if(reached.Contains(i))p.Routes.Add(TowerGeometry.Route(new[]{kept[i].Curve},"pedestrian",kept[i].Width));
  p.Diagnostics+=" walkParallelRemoved="+parallel+" walkIsolatedRemoved="+(kept.Count-reached.Count)+" walkKept="+reached.Count;
 }
}
}
