using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
public sealed class PlanBezier {
 public Point A,B,C,D;
 public bool NearBounds(Point p,double pad=.1)=>p.X>=Math.Min(Math.Min(A.X,B.X),Math.Min(C.X,D.X))-pad&&p.X<=Math.Max(Math.Max(A.X,B.X),Math.Max(C.X,D.X))+pad&&p.Y>=Math.Min(Math.Min(A.Y,B.Y),Math.Min(C.Y,D.Y))-pad&&p.Y<=Math.Max(Math.Max(A.Y,B.Y),Math.Max(C.Y,D.Y))+pad;
 public Point At(double t){double u=1-t;return A*(u*u*u)+B*(3*u*u*t)+C*(3*u*t*t)+D*(t*t*t);}
 public Point Tangent(double t){double u=1-t;return (B-A)*(3*u*u)+(C-B)*(6*u*t)+(D-C)*(3*t*t);}
 public PlanBezier Slice(double a,double b){var p=At(a);var q=At(b);return new PlanBezier{A=p,B=p+Tangent(a)*((b-a)/3),C=q-Tangent(b)*((b-a)/3),D=q};}
 public double Parameter(Point p){double best=double.MaxValue,t=0;for(int i=0;i<=64;i++){double v=(At(i/64.0)-p).Length;if(v<best){best=v;t=i/64.0;}}double lo=Math.Max(0,t-1.0/64),hi=Math.Min(1,t+1.0/64);for(int i=0;i<28;i++){double a=(2*lo+hi)/3,b=(lo+2*hi)/3;if((At(a)-p).Length<(At(b)-p).Length)hi=b;else lo=a;}return (lo+hi)/2;}
}
public sealed class ConstructionCourse {public PlanBezier Curve;public string Use;public double Width;public bool Curved;}
public static class ConstructionCurves {
 public static List<ConstructionCourse> Create(Plan plan){
  if(plan.AdditionCourses!=null)return plan.AdditionCourses.ToList();
  if(plan.VillaExistingRoadsOnly)return new List<ConstructionCourse>();
  if(plan.TowerAlgorithmVersion>=1)return TowerConstruction.Create(plan);
  var edges=ConstructionGraph.Create(plan);var junctionGraph=edges.ToArray();var nodes=edges.SelectMany(e=>new[]{e.A,e.B}).ToArray();var result=new List<ConstructionCourse>();
  foreach(var route in plan.Routes.Concat(plan.ConstructionRoutes))foreach(var local in route.Curves??new List<PlanBezier>()){
   var curve=new PlanBezier{A=plan.World(local.A),B=plan.World(local.B),C=plan.World(local.C),D=plan.World(local.D)};
   var matches=edges.Where(e=>e.Use==route.Use&&On(curve,e.A)&&On(curve,e.B)&&On(curve,(e.A+e.B)*.5)).ToList();
   if(matches.Count==0)continue;
   // Keep real branch nodes. Sampling vertices of a curve are not junctions.
   var cuts=new List<double>{0,1};
   // Keep junction evidence after earlier curves consume their graph edges.
   foreach(var edge in junctionGraph.Except(matches).Where(e=>(e.Use=="pedestrian")== (route.Use=="pedestrian")))foreach(var p in new[]{edge.A,edge.B})if(On(curve,p)){
    double t=curve.Parameter(p);if(t>.0001&&t<.9999)cuts.Add(t);
   }
   var ordered=cuts.OrderBy(x=>x).Aggregate(new List<double>(),(list,t)=>{if(list.Count==0||t-list.Last()>.0001)list.Add(t);return list;});
   for(int i=1;i<ordered.Count;i++)result.Add(new ConstructionCourse{Curve=curve.Slice(ordered[i-1],ordered[i]),Use=route.Use,Width=route.Width,Curved=true});
   foreach(var edge in matches)edges.Remove(edge);
  }
  // Keep the graph's shared positions, not independently projected curve positions.
  Point Snap(Point p){var near=nodes.OrderBy(q=>(q-p).Length).FirstOrDefault();return (near-p).Length<.1?near:p;}
  foreach(var course in result){var c=course.Curve;var a=Snap(c.A);var b=Snap(c.D);c.B=c.B+(a-c.A);c.C=c.C+(b-c.D);c.A=a;c.D=b;}
  result.AddRange(edges.Select(e=>new ConstructionCourse{Use=e.Use,Width=e.Width,Curve=new PlanBezier{A=e.A,B=e.A+(e.B-e.A)*(1.0/3),C=e.A+(e.B-e.A)*(2.0/3),D=e.B}}));if(plan.Villa)SimplifyVillaNodes(result);return WalkRoadClearance.Apply(result,plan.Tower?plan.TowerWalkGap:WalkRoadClearance.Gap);
 }
 // Merge only degree-two continuation nodes. Junctions and road types stay fixed.
 public static void SimplifyVillaNodes(List<ConstructionCourse> courses){
  bool changed=true;while(changed){changed=false;
   foreach(var a in courses.ToArray()){
    if(a.Use=="pedestrian")continue;var at=a.Curve.D;
    var incident=courses.Where(c=>(c.Curve.A-at).Length<.001||(c.Curve.D-at).Length<.001).ToList();
    if(incident.Count!=2)continue;var b=incident.First(c=>!ReferenceEquals(c,a));
    if(a.Use!=b.Use||Math.Abs(a.Width-b.Width)>.001)continue;
    var x=a.Curve;var y=b.Curve;
    if((y.D-at).Length<.001)y=new PlanBezier{A=y.D,B=y.C,C=y.B,D=y.A};
    if((x.D-x.A).Length>Math.Max(8,a.Width)&&(y.D-y.A).Length>Math.Max(8,b.Width))continue;
    var incoming=x.D-x.C;var outgoing=y.B-y.A;
    if(incoming.Length<1e-6||outgoing.Length<1e-6||SiteFrame.Dot(VillaGeometry.Unit(incoming),VillaGeometry.Unit(outgoing))<.99999)continue;
    double ratio=outgoing.Length/incoming.Length;
    var merged=new PlanBezier{A=x.A,B=x.A+(x.B-x.A)*(1+ratio),C=y.D+(y.C-y.D)*(1+1/ratio),D=y.D};
    double split=1/(1+ratio);bool fits=true;
    for(int i=0;i<=16;i++){double t=i/16d;if((merged.At(t*split)-x.At(t)).Length>.02||(merged.At(split+t*(1-split))-y.At(t)).Length>.02){fits=false;break;}}
    if(!fits)continue;
    a.Curve=merged;a.Curved=!TowerConstruction.Straight(merged);courses.Remove(b);changed=true;break;
   }
  }
 }
 static bool On(PlanBezier curve,Point p)=>curve.NearBounds(p)&&(curve.At(curve.Parameter(p))-p).Length<.08;
}
}
