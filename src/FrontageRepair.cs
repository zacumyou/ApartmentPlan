using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
internal sealed partial class ResidentialLayout {
 // SAT against the entire flat-ended road ribbon, not its centre or a
 // conservative round cap that wrongly rejects a lot beside a junction.
 bool RibbonHits(Point a,Point b,double width,Parcel r){
  var d=b-a;double length=d.Length;if(length<.001)return false;d=d*(1/length);var n=new Point(-d.Y,d.X);
  var q=new Point(r.X+r.W/2,r.Y+r.D/2)-(a+b)*.5;double half=width/2-.01;
  return Math.Abs(q.X)<Math.Abs(d.X)*length/2+Math.Abs(n.X)*half+r.W/2-.001&&
   Math.Abs(q.Y)<Math.Abs(d.Y)*length/2+Math.Abs(n.Y)*half+r.D/2-.001&&
   Math.Abs(SiteFrame.Dot(q,d))<length/2+Math.Abs(d.X)*r.W/2+Math.Abs(d.Y)*r.D/2-.001&&
   Math.Abs(SiteFrame.Dot(q,n))<half+Math.Abs(n.X)*r.W/2+Math.Abs(n.Y)*r.D/2-.001;
 }
 bool ClearVehicleRibbon(Point a,Point b,double width){
  var d=b-a;if(d.Length<.001)return false;var n=new Point(-d.Y,d.X)*(width/2/d.Length);
  var corners=new[]{a+n,b+n,b-n,a-n};if(corners.Any(p=>!Layout.Inside(p,poly)))return false;
  for(int i=0;i<4;i++)for(int j=0;j<poly.Count;j++){var u=corners[i];var v=corners[(i+1)%4];var p=poly[j];var q=poly[(j+1)%poly.Count];if(Layout.Cross(v-u,p-u)*Layout.Cross(v-u,q-u)<-1e-8&&Layout.Cross(q-p,u-p)*Layout.Cross(q-p,v-p)<-1e-8)return false;}
  if(exact.Any(r=>RibbonHits(a,b,width,r)))return false;
  for(int y=Math.Max(0,Y(Math.Min(a.Y,b.Y)-width));y<Math.Min(ny,Y(Math.Max(a.Y,b.Y)+width)+1);y++)
   for(int x=Math.Max(0,X(Math.Min(a.X,b.X)-width));x<Math.Min(nx,X(Math.Max(a.X,b.X)+width)+1);x++)
    if(((mainJunctionBuffer?[x,y]??false)||reserved[x,y]||map[x,y]==Outside||map[x,y]==Reserve||map[x,y]==3||map[x,y]==5||edgeGreen[x,y]&&map[x,y]!=1)&&RibbonHits(a,b,width,Rect(x,y,1,1)))return false;
  return true;
 }
 void PaintVehicleRibbon(Point a,Point b,double width){
  foreach(int id in DiagonalCells(a,b,width))if(id>=0){int x=id%nx,y=id/nx;if(RibbonHits(a,b,width,Rect(x,y,1,1))&&map[x,y]!=Outside&&map[x,y]!=Fixed&&!reserved[x,y])map[x,y]=1;}
 }
 static HashSet<ConstructionEdge> EntranceComponent(Plan p,List<ConstructionEdge> edges){
  var connected=new HashSet<ConstructionEdge>();if(edges.Count==0)return connected;
  string Key(Point v)=>Math.Round(v.X*100)+","+Math.Round(v.Y*100);
  var incident=new Dictionary<string,List<ConstructionEdge>>();foreach(var e in edges)foreach(var v in new[]{e.A,e.B}){string k=Key(v);if(!incident.TryGetValue(k,out var list))incident[k]=list=new List<ConstructionEdge>();list.Add(e);}
  var root=edges.OrderBy(e=>Math.Min((e.A-p.Origin).Length,(e.B-p.Origin).Length)).First();var queue=new List<ConstructionEdge>{root};connected.Add(root);
  for(int i=0;i<queue.Count;i++)foreach(var v in new[]{queue[i].A,queue[i].B})foreach(var e in incident[Key(v)])if(connected.Add(e))queue.Add(e);
  return connected;
 }
 void RepairFrontageConnections(){
  if(!o.ContinuousFrontage)return;
  for(int pass=0;pass<32;pass++){
   o.Cancellation.ThrowIfCancellationRequested();var graph=ConstructionGraph.Create(plan).Where(e=>e.Use!="pedestrian").ToList();var connected=EntranceComponent(plan,graph);
   var isolated=graph.Where(e=>!connected.Contains(e)).ToArray();if(isolated.Length==0)return;
   // Correct the raster access endpoint itself when it almost meets a real
   // junction. This avoids inserting a 1–2 m road stub beside the junction.
   bool snapped=false;
   foreach(var route in plan.Routes.Where(r=>r.Use=="access"||r.Use=="aisle"||r.Use=="aisleWide")){
    if(route.Curves?.Count>0||route.Points.Count<2)continue;
    foreach(int end in new[]{0,route.Points.Count-1}){
     var old=route.Points[end];var world=plan.World(old);
     if(!isolated.Any(e=>(e.A-world).Length<.03||(e.B-world).Length<.03))continue;
     int neighbor=end==0?1:end-1;var from=route.Points[neighbor];
     foreach(var e in connected.OrderBy(e=>Math.Min((e.A-world).Length,(e.B-world).Length))){
      var d=e.B-e.A;double t=Math.Max(0,Math.Min(1,SiteFrame.Dot(world-e.A,d)/SiteFrame.Dot(d,d)));var q=plan.Local(e.A+d*t);
      if((q-old).Length>Math.Max(8,route.Width)||!ClearVehicleRibbon(from,q,route.Width))continue;
      route.Points[end]=q;var next=ConstructionGraph.Create(plan).Where(v=>v.Use!="pedestrian").ToList();
      if(next.Count-EntranceComponent(plan,next).Count<isolated.Length){PaintVehicleRibbon(from,q,route.Width);snapped=true;break;}
      route.Points[end]=old;
     }
     if(snapped)break;
    }
    if(snapped)break;
   }
   if(snapped)continue;
   var candidates=new List<Tuple<Point,Point,double>>();
   void Candidate(Point a,ConstructionEdge e){var d=e.B-e.A;double t=Math.Max(0,Math.Min(1,SiteFrame.Dot(a-e.A,d)/SiteFrame.Dot(d,d)));var b=e.A+d*t;double length=(b-a).Length;
    if(length>=o.ParkingAisle+.25&&length<=o.MaxAccessLength)candidates.Add(Tuple.Create(plan.Local(a),plan.Local(b),length));
   }
   foreach(var e in isolated)foreach(var other in connected){Candidate(e.A,other);Candidate(e.B,other);Candidate(other.A,e);Candidate(other.B,e);}
   bool repaired=false;
   foreach(var c in candidates.OrderBy(c=>c.Item3)){
    double width=o.WideParkingAisle>o.ParkingAisle&&c.Item3>=o.WideParkingAisle+.25&&ClearVehicleRibbon(c.Item1,c.Item2,o.WideParkingAisle)?o.WideParkingAisle:o.ParkingAisle;
    if(c.Item3<width+.25||!ClearVehicleRibbon(c.Item1,c.Item2,width))continue;
    var route=new PlanRoute{Use=width>o.ParkingAisle?"aisleWide":"aisle",Width=width,Points=new List<Point>{c.Item1,c.Item2}};plan.Routes.Add(route);
    var next=ConstructionGraph.Create(plan).Where(e=>e.Use!="pedestrian").ToList();
    if(next.Count-EntranceComponent(plan,next).Count>=isolated.Length){plan.Routes.Remove(route);continue;}
    PaintVehicleRibbon(c.Item1,c.Item2,width);repaired=true;break;
   }
   if(!repaired)return; // Keep the normal preflight rejection; never hide it.
  }
 }
}
}
