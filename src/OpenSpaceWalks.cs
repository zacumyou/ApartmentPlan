using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
internal sealed partial class ResidentialLayout {
 void RoundWalkCorners(){
  const double radius=2;
  var graph=ConstructionGraph.Create(plan);
  foreach(var route in plan.Routes.Concat(plan.ConstructionRoutes).Where(r=>r.Use=="pedestrian"&&r.Curves.Count==0)){
   var corners=SimplifyRoute(route.Points);if(corners.Count<3||route.Width/2+.05>=radius)continue;
   var points=new List<Point>{corners[0]};double previousTrim=0;
   for(int i=1;i<corners.Count-1;i++){
    o.Cancellation.ThrowIfCancellationRequested();var p=corners[i];var incoming=p-corners[i-1];var outgoing=corners[i+1]-p;
    var u=incoming*(1/incoming.Length);var v=outgoing*(1/outgoing.Length);
    double angle=Math.Acos(Math.Max(-1,Math.Min(1,SiteFrame.Dot(u,v))));
    double trim=radius*Math.Tan(angle/2),lead=Math.Max(2,route.Width);
    var world=plan.World(p);var incident=graph.Where(e=>(e.A-world).Length<.03||(e.B-world).Length<.03).ToArray();
    // Keep junctions fixed and do not trade a corner for tiny straight stubs.
    if(angle<.1||angle>Math.PI/2+.001||incoming.Length-previousTrim<trim+lead||outgoing.Length<trim+lead||incident.Length!=2||incident.Any(e=>e.Use!="pedestrian")){
     points.Add(p);previousTrim=0;continue;
    }
    double handle=4.0/3*radius*Math.Tan(angle/4);
    var curve=new PlanBezier{A=p-u*trim,D=p+v*trim};curve.B=curve.A+u*handle;curve.C=curve.D-v*handle;
    var cells=new HashSet<int>();bool clear=true;
    // Sixteen samples plus a conservative interpolation pad reserve the whole
    // curved ribbon. No overlap with lots, bays, roads, masks or entrance disks.
    double pad=route.Width/2+.12;
    for(int n=0;n<=16&&clear;n++){
     var q=curve.At(n/16.0);
     for(int y=Y(q.Y-pad);y<=Y(q.Y+pad)&&clear;y++)for(int x=X(q.X-pad);x<=X(q.X+pad);x++){
      double px=Math.Max(x0+x*cell,Math.Min(x0+(x+1)*cell,q.X)),py=Math.Max(y0+y*cell,Math.Min(y0+(y+1)*cell,q.Y));
      if((q-new Point(px,py)).Length>pad)continue;
      if(x<0||y<0||x>=nx||y>=ny||entranceWalkBuffer[x,y]||reserved[x,y]||map[x,y]==Outside||map[x,y]==Fixed||map[x,y]==Reserve||map[x,y]==1||map[x,y]==3||map[x,y]==5){clear=false;break;}
      cells.Add(y*nx+x);
     }
     if(!clear)break;
     var w=plan.World(q);
     foreach(var e in graph){if(incident.Contains(e))continue;var d=e.B-e.A;double t=Math.Max(0,Math.Min(1,SiteFrame.Dot(w-e.A,d)/SiteFrame.Dot(d,d)));
      if((w-e.A-d*t).Length<pad+e.Width/2){clear=false;break;}
     }
    }
    if(!clear){points.Add(p);previousTrim=0;continue;}
    foreach(int id in cells)map[id%nx,id/nx]=Walk;
    points.Add(curve.A);for(int n=1;n<=16;n++)points.Add(curve.At(n/16.0));
    route.Curves.Add(curve);route.CornerRadii.Add(radius);previousTrim=trim;
   }
   points.Add(corners[corners.Count-1]);route.Points=points;
  }
 }
 // Optional useful links, never counted as mandatory building access.
 void OpenSpaceWalks(){
  if(o.ExternalWalkTargets==null||o.ExternalWalkTargets.Count==0||!o.MainRoadPedestrian)return;
  var mains=plan.Routes.Concat(plan.ConstructionRoutes).Where(r=>r.Use=="main").ToList();
  var target=new bool[nx,ny];foreach(var road in mains)for(int y=0;y<ny;y++)for(int x=0;x<nx;x++)
   if(map[x,y]==1&&PedestrianRoadAccess.Covers(road,new Point(x0+(x+.5)*cell,y0+(y+.5)*cell),o))target[x,y]=road.Points.Zip(road.Points.Skip(1),(a,b)=>{var q=new Point(x0+(x+.5)*cell,y0+(y+.5)*cell);var d=b-a;double t=Math.Max(0,Math.Min(1,SiteFrame.Dot(q-a,d)/SiteFrame.Dot(d,d)));return (q-a-d*t).Length;}).Any(d=>d<=cell*.8);
  var accepted=new List<Point>();int attempts=0;
  foreach(var candidate in o.ExternalWalkTargets){
   o.Cancellation.ThrowIfCancellationRequested();if(accepted.Count>=4||attempts>=24)break;
   var start=plan.Local(candidate.Inside);var end=plan.Local(candidate.Road);var boundary=plan.Local(candidate.Boundary);
   if(accepted.Any(p=>(p-boundary).Length<Math.Max(30,o.PerimeterPathSpacing)))continue;
   int sx=X(start.X),sy=Y(start.Y);if(sx<0||sy<0||sx>=nx||sy>=ny||map[sx,sy]!=Green)continue;
   // Require open space near the edge rather than inserting narrow decorative stubs.
   var room=new Parcel{X=start.X-o.Walkway,Y=start.Y-o.Walkway,W=2*o.Walkway,D=2*o.Walkway};
   Bounds(room,out int a,out int b,out int c,out int d);bool open=true;for(int y=b;y<d;y++)for(int x=a;x<c;x++)if(map[x,y]!=Green)open=false;if(!open)continue;
   attempts++;var path=FindRoute(start,(x,y)=>target[x,y],o.Walkway,true,160);if(path==null||path.Count<2)continue;
   double length=0;int turns=0;for(int k=1;k<path.Count;k++){length+=(path[k]-path[k-1]).Length;if(k>1&&Math.Abs(Layout.Cross(path[k]-path[k-1],path[k-1]-path[k-2]))>.001)turns++;}
   if(length<12||length>180||turns>3)continue;
   // Check the final boundary crossing against every occupied/excluded cell.
   // Outside cells are allowed only here, to reach the known native road centre.
   var tail=new List<Point>{path[0],boundary,end};bool clear=true;
   for(int k=1;k<tail.Count&&clear;k++){
    var delta=tail[k]-tail[k-1];int steps=Math.Max(1,(int)Math.Ceiling(delta.Length/(cell/2)));
    for(int n=0;n<=steps&&clear;n++){var q=tail[k-1]+delta*((double)n/steps);double half=o.Walkway/2;
     var box=new Parcel{X=q.X-half,Y=q.Y-half,W=o.Walkway,D=o.Walkway};
     if(exact.Any(p=>Layout.Overlap(box,p))){clear=false;break;}
     Bounds(box,out a,out b,out c,out d);for(int y=b;y<d;y++)for(int x=a;x<c;x++)if(reserved[x,y]||map[x,y]==Fixed||map[x,y]==Reserve||map[x,y]==3||map[x,y]==5)clear=false;
    }
   }
   if(!clear)continue;
   DrawRoute(path,o.Walkway,"pedestrian");
   for(int k=1;k<tail.Count;k++){var delta=tail[k]-tail[k-1];int steps=Math.Max(1,(int)Math.Ceiling(delta.Length/(cell/2)));for(int n=0;n<=steps;n++){var q=tail[k-1]+delta*((double)n/steps);double half=o.Walkway/2;var box=new Parcel{X=q.X-half,Y=q.Y-half,W=o.Walkway,D=o.Walkway};Bounds(box,out a,out b,out c,out d);for(int y=b;y<d;y++)for(int x=a;x<c;x++)if(map[x,y]==Green||map[x,y]==Paved)map[x,y]=Walk;}}
   plan.ConstructionRoutes.Add(new PlanRoute{Use="pedestrian",Width=o.Walkway,Points=tail});accepted.Add(boundary);
  }
 }
}
}
