using System;
using System.Linq;
using System.Collections.Generic;
namespace ApartmentPlan {
// Analytic tower network: only straight spans are noded. A bend is emitted as
// its original cubic, never converted to many short native edges.
public static class TowerConstruction {
 public static bool Straight(PlanBezier c)=>Math.Abs(Layout.Cross(c.D-c.A,c.B-c.A))<.0001&&Math.Abs(Layout.Cross(c.D-c.A,c.C-c.A))<.0001;
 public static List<ConstructionCourse> Create(Plan p){
  var source=p.Routes.Concat(p.ConstructionRoutes).SelectMany(r=>r.Curves.Select(c=>new ConstructionCourse{Use=r.Use,Width=r.Width,Curved=!Straight(c),Curve=new PlanBezier{A=p.World(c.A),B=p.World(c.B),C=p.World(c.C),D=p.World(c.D)}})).ToList();
  // Canonicalize before intersection cuts and duplicate detection, not after them.
  // Keep pedestrian nodes separate from vehicle nodes.
  var canonical=new Dictionary<bool,List<Point>>{{true,new List<Point>()},{false,new List<Point>()}};
  Point Canon(Point q,bool walk){var list=canonical[walk];foreach(var n in list)if((n-q).Length<.001)return n;list.Add(q);return q;}
  foreach(var item in source){var c=item.Curve;var a=Canon(c.A,item.Use=="pedestrian");var d=Canon(c.D,item.Use=="pedestrian");c.B=c.B+(a-c.A);c.C=c.C+(d-c.D);c.A=a;c.D=d;if(!item.Curved)item.Curve=TowerGeometry.Line(a,d);}
  var result=new List<ConstructionCourse>();var keys=new HashSet<string>();
  string Key(Point q)=>Math.Round(q.X,4)+":"+Math.Round(q.Y,4);
  foreach(var c in source.OrderBy(c=>c.Use=="main"?0:c.Use=="turnaround"?1:2)){
   var cuts=new List<double>{0,1};var d=c.Curve.D-c.Curve.A;double dd=SiteFrame.Dot(d,d);if(dd<.000001)continue;
   if(!c.Curved)foreach(var other in source){if(ReferenceEquals(c,other)||(c.Use=="pedestrian")!=(other.Use=="pedestrian"))continue;
    foreach(var q in new[]{other.Curve.A,other.Curve.D}){double t=SiteFrame.Dot(q-c.Curve.A,d)/dd;if(t>1e-6&&t<1-1e-6&&(c.Curve.A+d*t-q).Length<.001)cuts.Add(t);}
    if(other.Curved)continue;var e=other.Curve.D-other.Curve.A;double cross=Layout.Cross(d,e);if(Math.Abs(cross)<.000001)continue;double t2=Layout.Cross(other.Curve.A-c.Curve.A,e)/cross,u=Layout.Cross(other.Curve.A-c.Curve.A,d)/cross;if(t2>0&&t2<1&&u>=0&&u<=1)cuts.Add(t2);
   }
   var ordered=new List<double>();foreach(var t in cuts.OrderBy(t=>t))if(ordered.Count==0||(t-ordered.Last())*Math.Sqrt(dd)>.001)ordered.Add(t);for(int i=1;i<ordered.Count;i++){var curve=c.Curve.Slice(ordered[i-1],ordered[i]);if((curve.D-curve.A).Length<.01)continue;var a=Key(curve.A);var b=Key(curve.D);var key=(c.Use=="pedestrian"?"walk/":"road/")+(String.CompareOrdinal(a,b)<0?a+"/"+b:b+"/"+a)+"/"+Key(curve.At(.5));if(keys.Add(key))result.Add(new ConstructionCourse{Curve=curve,Width=c.Width,Use=c.Use,Curved=c.Curved});}
  }
  // The visibility lattice rounds coordinates; canonicalize those sub-mm
  // differences before native node creation and connectivity validation.
  foreach(var course in result){var c=course.Curve;var a=Canon(c.A,course.Use=="pedestrian");var d=Canon(c.D,course.Use=="pedestrian");c.B=c.B+(a-c.A);c.C=c.C+(d-c.D);c.A=a;c.D=d;}
  keys.Clear();return result.Where(course=>{var c=course.Curve;var a=Key(c.A);var b=Key(c.D);var key=(course.Use=="pedestrian"?"walk/":"road/")+(String.CompareOrdinal(a,b)<0?a+"/"+b:b+"/"+a)+"/"+Key(c.At(.5));return keys.Add(key);}).ToList();
 }
}
}
