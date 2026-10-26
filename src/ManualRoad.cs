using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
// World-space control points survive changes to the site's planning frame.
public sealed class ManualRoad {
 public bool Curved;public List<Point> Points=new List<Point>();
 public List<Point> Sample(){
  if(Points==null||Points.Count!=(Curved?3:2)||Points.Any(p=>double.IsNaN(p.X)||double.IsNaN(p.Y)||double.IsInfinity(p.X)||double.IsInfinity(p.Y)))throw new ArgumentException("ManualRoadFit");
  if((Points.Last()-Points[0]).Length<4||Curved&&((Points[1]-Points[0]).Length<4||(Points[2]-Points[1]).Length<4))throw new ArgumentException("ManualRoadFit");
  if(!Curved)return Points.ToList();
  // Sampling is only for planning/clearance; native construction retains cubic
  // control points. Bound chord error for junction projection below 2 cm.
  int steps=Math.Min(512,Math.Max(8,(int)Math.Ceiling(Math.Sqrt((Points[0]-Points[1]*2+Points[2]).Length/.08))));
  var result=new List<Point>();for(int i=0;i<=steps;i++){double t=(double)i/steps;result.Add(Points[0]*((1-t)*(1-t))+Points[1]*(2*(1-t)*t)+Points[2]*(t*t));}
  return result;
 }
}
internal sealed partial class ResidentialLayout {
 void AddManualRoads(IList<ManualRoad> roads){
  if(roads==null)return;if(roads.Count>64)throw new ArgumentException("ManualRoadFit");
  foreach(var road in roads){
   if(road==null)throw new ArgumentException("ManualRoadFit");
   var route=new PlanRoute{Use="main",Width=o.Road,Points=road.Sample().Select(plan.Local).ToList()};
   if(road.Curved){var a=plan.Local(road.Points[0]);var b=plan.Local(road.Points[1]);var c=plan.Local(road.Points[2]);route.Curves.Add(new PlanBezier{A=a,B=a+(b-a)*(2.0/3),C=c+(b-c)*(2.0/3),D=c});}
   if(!PaintCurvedMain(route))throw new ArgumentException("ManualRoadFit");
  }
 }
}
}
