using System;
using System.Collections.Generic;
namespace ApartmentPlan {
// Conservative broad phase; exact overlap/space/access checks remain mandatory.
internal static class ParkingCandidateMask {
 internal static bool[,] Build(IEnumerable<Parcel> rows,int nx,int ny,double x0,double y0,double cell,double extent,double gap){
  var delta=new int[nx+1,ny+1];
  foreach(var row in rows){
   int a=Math.Max(0,Math.Min(nx,(int)Math.Floor((row.X-extent-gap-x0)/cell)-1));
   int b=Math.Max(0,Math.Min(ny,(int)Math.Floor((row.Y-extent-gap-y0)/cell)-1));
   int c=Math.Max(0,Math.Min(nx,(int)Math.Ceiling((row.X+row.W+gap-x0)/cell)+1));
   int d=Math.Max(0,Math.Min(ny,(int)Math.Ceiling((row.Y+row.D+gap-y0)/cell)+1));
   if(a>=c||b>=d)continue;delta[a,b]++;delta[c,b]--;delta[a,d]--;delta[c,d]++;
  }
  var mask=new bool[nx,ny];
  for(int y=0;y<ny;y++)for(int x=0;x<nx;x++){
   if(x>0)delta[x,y]+=delta[x-1,y];if(y>0)delta[x,y]+=delta[x,y-1];if(x>0&&y>0)delta[x,y]-=delta[x-1,y-1];mask[x,y]=delta[x,y]>0;
  }
  return mask;
 }
}
}
