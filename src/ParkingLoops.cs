using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
internal sealed partial class ResidentialLayout {
 sealed class LoopAxis {public bool Horizontal;public double Across,Low,High,Width;}
 // Close nearby parking streets, not the site perimeter. Both ends must join
 // actual centre lines, including any short extension beyond their old ends.
 void CloseParkingLoops(){
  double width=Snap(o.AccessRoad>0?o.AccessRoad:o.ParkingAisle);
  var axes=new List<LoopAxis>();var rows=exact.Where(r=>r.Kind==2).ToArray();
  foreach(var r in plan.ConstructionRoutes.Where(r=>r.Use=="aisle"||r.Use=="aisleWide"||r.Use=="access"))
   for(int i=1;i<r.Points.Count;i++){
    var a=r.Points[i-1];var b=r.Points[i];bool h=Math.Abs(a.Y-b.Y)<.01;
    if(!h&&Math.Abs(a.X-b.X)>.01)continue;
    axes.Add(new LoopAxis{Horizontal=h,Across=h?a.Y:a.X,Low=h?Math.Min(a.X,b.X):Math.Min(a.Y,b.Y),High=h?Math.Max(a.X,b.X):Math.Max(a.Y,b.Y),Width=r.Width});
   }
  double maxSpan=Math.Min(o.MaxAccessLength,2*Snap(o.BayDepth)+3*width+2*o.ParkingEndClearance);
  bool Space(Parcel r){
   if(!Layout.Contains(r,poly)||!ClearOfMain(r)||exact.Any(p=>Layout.Overlap(p,r)))return false;
   Bounds(r,out int a,out int b,out int c,out int d);
   for(int y=b;y<d;y++)for(int x=a;x<c;x++)if(reserved[x,y]||edgeGreen[x,y]||
    map[x,y]!=1&&map[x,y]!=Green&&map[x,y]!=Paved&&map[x,y]!=9&&(map[x,y]!=CourtGreen||o.ParkingIsland>0))return false;
   return true;
  }
  foreach(var a in axes)foreach(var b in axes){
   o.Cancellation.ThrowIfCancellationRequested();
   bool h=a.Horizontal;double span=b.Across-a.Across,low=Math.Max(a.Low,b.Low),high=Math.Min(a.High,b.High);
   if(h!=b.Horizontal||span<width+cell||span>maxSpan||high-low<2*width)continue;
   // A parking row must actually lie between these streets, parallel to them.
   if(!rows.Any(r=>r.ParkingAlongX==h&&(h?r.Y:r.X)>=a.Across&&
    (h?r.Y+r.D:r.X+r.W)<=b.Across&&(h?r.X:r.Y)<high&&(h?r.X+r.W:r.Y+r.D)>low))continue;
   Parcel RectBetween(double u0,double u1,double v0,double v1)=>new Parcel{Kind=1,InvisibleRoad=true,X=h?u0:v0,Y=h?v0:u0,W=h?u1-u0:v1-v0,D=h?v1-v0:u1-u0};
   Point P(double u,double v)=>h?new Point(u,v):new Point(v,u);
   foreach(bool first in new[]{true,false}){
    double edge=first?low:high;int limit=(int)Math.Ceiling((width+o.ParkingEndClearance+cell)/cell);
    for(int step=0;step<=limit;step++){
     double at=edge+(first?-step:step)*cell;
     var cap=RectBetween(at-width/2,at+width/2,a.Across-width/2,b.Across+width/2);
     if(!Space(cap))continue;
     var extensions=new List<Tuple<Parcel,Point,Point>>();bool fits=true;
     foreach(var axis in new[]{a,b}){
      double end=Math.Max(axis.Low,Math.Min(axis.High,at));if(Math.Abs(end-at)<.01)continue;
      var r=RectBetween(Math.Min(at,end)-width/2,Math.Max(at,end)+width/2,axis.Across-width/2,axis.Across+width/2);
      if(!Space(r)){fits=false;break;}extensions.Add(Tuple.Create(r,P(end,axis.Across),P(at,axis.Across)));
     }
     if(!fits)continue;
     // Already closed: do not add duplicate native courses in the same place.
     if(KindRectangle(cap,1))break;
     foreach(var e in extensions){Paint(e.Item1,1);plan.ConstructionRoutes.Add(new PlanRoute{Use="aisle",Width=width,Points=new List<Point>{e.Item2,e.Item3}});}
     Paint(cap,1);plan.ConstructionRoutes.Add(new PlanRoute{Use="aisle",Width=width,Points=new List<Point>{P(at,a.Across),P(at,b.Across)}});
     break;
    }
   }
  }
 }
}
}
