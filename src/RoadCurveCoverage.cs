using System;
using System.Collections.Generic;
using Colossal.Mathematics;
using Unity.Mathematics;
namespace ApartmentPlan {
internal static class RoadCurveCoverage {
 internal static bool Covers(Bezier4x3 source,List<Bezier4x3> survivors,bool groundProjection){
  var intervals=new List<Tuple<double,double>>();
  foreach(var curve in survivors){
   float first=1,last=0;bool found=false;
   int samples=math.clamp((int)math.ceil(MathUtils.Length(curve)/.25f),16,8192);
   for(int i=0;i<=samples;i++){
    var q=MathUtils.Position(curve,(float)i/samples);float t;
    bool on=(groundProjection?MathUtils.Distance(source.xz,q.xz,out t):MathUtils.Distance(source,q,out t))<=.25f;
    if(on){if(!found){first=last=t;found=true;}else{first=math.min(first,t);last=math.max(last,t);}}
    if((!on||i==samples)&&found){intervals.Add(Tuple.Create((double)first,(double)last));found=false;}
   }
  }
  return NetworkReplacementPolicy.CoversIntervals(intervals,.5/Math.Max(1,MathUtils.Length(source)));
 }
}
}
