using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
internal sealed partial class ResidentialLayout {
 void ReclaimWideParking(){
  if(!o.ContinuousFrontage||o.Parking<=0||o.ParkingIsland>0)return;
  double width=o.ParkingAisle;
  var routes=plan.ConstructionRoutes.Concat(plan.Routes).ToArray();
  foreach(var route in routes.Where(r=>r.Use=="aisleWide"&&r.Points.Count==2&&r.Width>width+.1)){
   o.Cancellation.ThrowIfCancellationRequested();
   var a=route.Points[0];var b=route.Points[1];
   if(plan.Routes.Contains(route))continue; // Main-road approaches retain their wider reservation.
   bool h=Math.Abs(a.Y-b.Y)<.01;
   if(!h&&Math.Abs(a.X-b.X)>.01)continue;
   double low=h?Math.Min(a.X,b.X):Math.Min(a.Y,b.Y),high=h?Math.Max(a.X,b.X):Math.Max(a.Y,b.Y),across=h?a.Y:a.X;
   double end=Math.Max(route.Width,o.ParkingEndClearance);
   foreach(var main in routes.Where(r=>r.Use=="main"&&r.Points.Any(p=>(p-a).Length<.05||(p-b).Length<.05)))end=Math.Max(end,main.Width/2+o.MainParkingSetback+cell);
   double run=Math.Floor((high-low-2*end)/o.BayWidth)*o.BayWidth;
   if(run<o.MinFrontParkingSpaces*o.BayWidth)continue;
   Parcel Strip(double u,double v,double w,double d,int kind)=>new Parcel{Kind=kind,X=h?u:v,Y=h?v:u,W=h?w:d,D=h?d:w,ParkingAlongX=h};
   var old=Strip(low,across-route.Width/2,high-low,route.Width,1);
   // Do not narrow a building's frontage or a shared/intersecting corridor.
   if(exact.Any(p=>p.Kind==0&&Layout.Overlap(p,old,2)))continue;
   var candidates=new List<Parcel>();
   foreach(int sign in new[]{-1,1}){
    var row=Strip(low+end,sign<0?across-width/2-o.BayDepth:across+width/2,run,o.BayDepth,2);
    if(!Layout.Contains(row,poly)||!ClearOfMain(row)||exact.Any(p=>Layout.Overlap(p,row)))continue;
    bool clear=true;Bounds(row,out int x1,out int y1,out int x2,out int y2);
    for(int y=y1;y<y2&&clear;y++)for(int x=x1;x<x2;x++){
     var c=Rect(x,y,1,1);var center=new Point(c.X+cell/2,c.Y+cell/2);
     bool own=center.X>=old.X&&center.X<=old.X+old.W&&center.Y>=old.Y&&center.Y<=old.Y+old.D;
     if(reserved[x,y]||edgeGreen[x,y]||map[x,y]!=Green&&map[x,y]!=CourtGreen&&map[x,y]!=Paved&&!(own&&map[x,y]==1)){clear=false;break;}
    }
    // Exact distance to every other road protects junction approaches/branches.
    foreach(var other in routes.Where(r=>!ReferenceEquals(r,route)))for(int i=1;i<other.Points.Count&&clear;i++){
     var u=other.Points[i-1];var v=other.Points[i];var delta=v-u;double ll=SiteFrame.Dot(delta,delta);if(ll<.01)continue;
     for(double t=0;t<=1.0001;t+=Math.Min(1,.5/Math.Sqrt(ll))){var p=u+delta*t;double dx=Math.Max(row.X-p.X,Math.Max(0,p.X-row.X-row.W)),dy=Math.Max(row.Y-p.Y,Math.Max(0,p.Y-row.Y-row.D));if(Math.Sqrt(dx*dx+dy*dy)<other.Width/2+.1){clear=false;break;}}
    }
    if(clear)candidates.Add(row);
   }
   if(candidates.Count==0)continue;
   // Keep the full previous apron paved; only the native lane and new bays
   // change. Intersection centre lines and all existing endpoints are retained.
   Bounds(old,out int l,out int d0,out int r0,out int t0);
   for(int y=d0;y<t0;y++)for(int x=l;x<r0;x++)if(map[x,y]==1){
    var p=new Point(x0+(x+.5)*cell,y0+(y+.5)*cell);bool shared=false;
    foreach(var other in routes.Where(r=>!ReferenceEquals(r,route)))for(int i=1;i<other.Points.Count&&!shared;i++){
     var u=other.Points[i-1];var delta=other.Points[i]-u;double ll=SiteFrame.Dot(delta,delta);if(ll<.01)continue;
     double t=Math.Max(0,Math.Min(1,SiteFrame.Dot(p-u,delta)/ll));shared=(p-u-delta*t).Length<other.Width/2+cell*.71;
    }
    if(!shared)map[x,y]=Paved;
   }
   Paint(Strip(low,across-width/2,high-low,width,1),1);
   route.Use="aisle";route.Width=width;
   foreach(var row in candidates){row.ParkingSpaces=(int)Math.Round(run/o.BayWidth);Paint(row,Fixed);exact.Add(row);spaces+=row.ParkingSpaces;}
  }
 }
 void PreferWideAccess(){
  if(o.WideParkingAisle<=o.ParkingAisle)return;
  foreach(var route in plan.Routes.Concat(plan.ConstructionRoutes).Where(r=>r.Use=="access"||r.Use=="aisle").ToArray()){
   o.Cancellation.ThrowIfCancellationRequested();
   if(route.Points.Count<2||!Enumerable.Range(1,route.Points.Count-1).All(i=>ClearVehicleRibbon(route.Points[i-1],route.Points[i],o.WideParkingAisle)))continue;
   for(int i=1;i<route.Points.Count;i++)PaintVehicleRibbon(route.Points[i-1],route.Points[i],o.WideParkingAisle);
   route.Use="aisleWide";route.Width=o.WideParkingAisle;
  }
 }
}
}
