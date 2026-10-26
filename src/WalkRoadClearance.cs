using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
// Native path ends stop outside vehicle ribbons; vehicle connectivity is intact.
public static class WalkRoadClearance {
 public const double Gap=1;
 public static List<ConstructionCourse> Apply(List<ConstructionCourse> source,double gap=Gap){
  var roads=source.Where(e=>e.Use!="pedestrian").ToArray();
  var result=new List<ConstructionCourse>();
  foreach(var e in source){
   if(e.Use!="pedestrian"){result.Add(e);continue;}
   bool Clear(double t){var p=e.Curve.At(t);var tangent=e.Curve.Tangent(t);double length=tangent.Length;
    foreach(var road in roads){var c=road.Curve;double pad=road.Width/2+gap+e.Width/2;
     if(p.X<Math.Min(Math.Min(c.A.X,c.B.X),Math.Min(c.C.X,c.D.X))-pad||p.X>Math.Max(Math.Max(c.A.X,c.B.X),Math.Max(c.C.X,c.D.X))+pad||p.Y<Math.Min(Math.Min(c.A.Y,c.B.Y),Math.Min(c.C.Y,c.D.Y))-pad||p.Y>Math.Max(Math.Max(c.A.Y,c.B.Y),Math.Max(c.C.Y,c.D.Y))+pad)continue;var chord=c.D-c.A;double u=road.Curved?c.Parameter(p):Math.Max(0,Math.Min(1,SiteFrame.Dot(p-c.A,chord)/Math.Max(.000001,SiteFrame.Dot(chord,chord))));var q=c.At(u);var delta=p-q;
     // Include the lateral edge of an oblique path's flat end.
     double side=length>.001&&delta.Length>.001?Math.Abs(Layout.Cross(tangent,delta))/(length*delta.Length)*e.Width/2:0;
     if(delta.Length<road.Width/2+gap+side-1e-7)return false;
    }return true;
   }
   double approximate=0;for(int i=1;i<=16;i++)approximate+=(e.Curve.At(i/16.0)-e.Curve.At((i-1)/16.0)).Length;
   int steps=Math.Max(8,(int)Math.Ceiling(approximate/.5));double begin=0;bool previous=Clear(0);
   void Emit(double end){if(end-begin<1e-7)return;var c=e.Curve.Slice(begin,end);if((c.D-c.A).Length>.1)result.Add(new ConstructionCourse{Use=e.Use,Width=e.Width,Curved=e.Curved,Curve=c});}
   for(int i=1;i<=steps;i++){double t=(double)i/steps;bool current=Clear(t);
    if(current!=previous){double lo=(double)(i-1)/steps,hi=t;for(int n=0;n<28;n++){double mid=(lo+hi)/2;if(Clear(mid)==previous)lo=mid;else hi=mid;}double cut=(lo+hi)/2;if(previous)Emit(cut);else begin=cut;}
    previous=current;
   }
   if(previous)Emit(1);
  }return result;
 }
}
}
