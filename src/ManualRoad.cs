using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
// World-space control points survive changes to the site's planning frame.
// Numeric evidence only; retains the existing ArgumentException/message contract.
internal sealed class ManualRoadFailure : ArgumentException {
 internal readonly Dictionary<string,int> Evidence;
 internal ManualRoadFailure(int reason,int ordinal=-1,int points=0,bool curved=false,double width=0):base("ManualRoadFit"){
  Evidence=new Dictionary<string,int>{{"reason",reason},{"ordinal",ordinal},{"connected",points},{"flags",curved?1:0},{"roadWidthCm",Centimetres(width)}};
 }
 internal static int Centimetres(double value)=>double.IsNaN(value)||double.IsInfinity(value)?0:(int)Math.Max(0,Math.Min(int.MaxValue,Math.Round(value*100)));
}
public sealed class ManualRoad {
 public bool Curved;public List<Point> Points=new List<Point>();
 public List<Point> Sample(){
  if(Points==null||Points.Count!=(Curved?3:2)||Points.Any(p=>double.IsNaN(p.X)||double.IsNaN(p.Y)||double.IsInfinity(p.X)||double.IsInfinity(p.Y)))throw new ManualRoadFailure(20,points:Points?.Count??0,curved:Curved);
  if((Points.Last()-Points[0]).Length<4||Curved&&((Points[1]-Points[0]).Length<4||(Points[2]-Points[1]).Length<4))throw new ManualRoadFailure(21,points:Points.Count,curved:Curved);
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
  if(roads==null)return;if(roads.Count>64)throw new ManualRoadFailure(22);
  for(int ordinal=0;ordinal<roads.Count;ordinal++){
   var road=roads[ordinal];
   if(road==null)throw new ManualRoadFailure(23,ordinal);
   try{
   var route=new PlanRoute{Use="main",Width=o.Road,Points=road.Sample().Select(plan.Local).ToList()};
   if(road.Curved){var a=plan.Local(road.Points[0]);var b=plan.Local(road.Points[1]);var c=plan.Local(road.Points[2]);route.Curves.Add(new PlanBezier{A=a,B=a+(b-a)*(2.0/3),C=c+(b-c)*(2.0/3),D=c});}
   int rejection=0;
   if(!PaintCurvedMain(route,reason=>rejection=reason))throw new ManualRoadFailure(rejection,ordinal,road.Points.Count,road.Curved,o.Road);
   }catch(ManualRoadFailure error){error.Evidence["ordinal"]=ordinal;error.Evidence["edges"]=roads.Count;error.Evidence["roadWidthCm"]=ManualRoadFailure.Centimetres(o.Road);throw;}
  }
 }
}
}
