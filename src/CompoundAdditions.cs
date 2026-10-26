using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
// Conservative world-space overlap: never move or replace an existing lot.
public static class CompoundAdditions {
 static Parcel Bounds(Plan plan,Parcel p){var points=(p.PlacementOutline??p.Corners).Select(plan.World).ToArray();double x=points.Min(q=>q.X),y=points.Min(q=>q.Y);return new Parcel{X=x,Y=y,W=points.Max(q=>q.X)-x,D=points.Max(q=>q.Y)-y};}
 public static bool FreeLot(Plan plan,Parcel lot,IEnumerable<Plan> applied){var bounds=Bounds(plan,lot);return !applied.Any(old=>old.Parcels.Any(p=>(p.Kind==0||p.Kind==2)&&Layout.Overlap(bounds,Bounds(old,p),.1)));}
 public static bool SameCourse(ConstructionCourse a,ConstructionCourse b){if(a.Use!=b.Use||Math.Abs(a.Width-b.Width)>.05)return false;return Match(a.Curve,b.Curve,false)||Match(a.Curve,b.Curve,true);}
 static bool Match(PlanBezier a,PlanBezier b,bool reverse){for(int i=0;i<=8;i++){double t=i/8d;if((a.At(t)-b.At(reverse?1-t:t)).Length>.1)return false;}return true;}
 public static void Filter(Plan candidate,IList<Plan> applied){
  var oldCourses=applied.SelectMany(ConstructionCurves.Create).ToList();
  candidate.AdditionCourses=ConstructionCurves.Create(candidate).Where(c=>!oldCourses.Any(old=>SameCourse(c,old))).ToList();
  candidate.Parcels.RemoveAll(p=>(p.Kind==0||p.Kind==2)&&!FreeLot(candidate,p,applied));
  candidate.Homes=candidate.Parcels.Where(p=>p.Kind==0).Sum(p=>p.Homes);
  candidate.Far=candidate.Area>0?candidate.Parcels.Where(p=>p.Kind==0).Sum(p=>p.FloorArea)/candidate.Area*100:0;
 }
}
}
