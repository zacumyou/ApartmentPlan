using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
public static class PedestrianRoadAccess {
 public static bool Supports(PlanRoute road,LayoutOptions o)=>road.Use=="main"?o.MainRoadPedestrian:(road.Use=="aisle"||road.Use=="aisleWide"||road.Use=="access")&&o.AisleRoadPedestrian;
 public static double Width(PlanRoute road,LayoutOptions o)=>road.Use=="main"?o.Road:road.Use=="aisleWide"?o.WideParkingAisle:o.AccessRoad>0?o.AccessRoad:o.ParkingAisle;
 public static bool Covers(PlanRoute road,Point p,LayoutOptions o){
  for(int i=1;i<road.Points.Count;i++){
   var a=road.Points[i-1];var d=road.Points[i]-a;double length=d.Length;if(length<.001)continue;
   double t=SiteFrame.Dot(p-a,d)/(length*length);
   // No implied sidewalk beyond the end of the native road.
   if(t>=0&&t<=1&&(p-a-d*t).Length<=Width(road,o)/2+.001)return true;
  }
  return false;
 }
}
internal sealed partial class ResidentialLayout {
 const int Walk=9;
 // Joint residential courts: two parallel frontage rows and an optional side bar.
 // All dimensions are selected asset lot dimensions, never stretched footprints.
 Block Court(double x,double y,Asset a,int lanes,bool side,bool vertical,out CourtGroup group){
  double l=Snap(Math.Max(a.Width,a.Depth)),d=Snap(Math.Min(a.Width,a.Depth)),w=Snap(o.Walkway),rear=Snap(Math.Max(2,o.Gap/2));
  double bay=o.Parking>0?Snap(o.BayDepth):0,aisle=Snap(Math.Max(o.Road,o.ParkingAisle)),turn=Snap(Math.Max(o.TurnClearance,aisle));
  double island=Snap(Math.Max(w,o.ParkingIsland)),courtLength=Math.Max(l,side?l-2*w:0);
  double courtDepth=lanes*(2*bay+aisle)+(lanes-1)*island;
  if(side)courtDepth=Math.Max(courtDepth,l);
  double cx=rear+(side?d+w:0)+w,cy=rear+d+w;
  double width=cx+courtLength+turn+w+rear,height=cy+courtDepth+w+d+rear;
  Func<double,double,double,double,int,Parcel> part=(u,v,b,h,k)=>new Parcel{X=x+(vertical?v:u),Y=y+(vertical?u:v),W=vertical?h:b,D=vertical?b:h,Kind=k,ParkingAlongX=!vertical};
  var b=new Block{Bounds=part(0,0,width,height,4),MaxConnection=100};
  group=new CourtGroup{Id=plan.Courts.Count,Pattern=(side?"U court / ":"parallel court / ")+lanes+" aisles",Bounds=b.Bounds};
  b.Pieces.Add(part(0,0,width,height,4));
  // A continuous frontage walk surrounds the court; only its gate crosses traffic.
  b.Pieces.Add(part(cx-w,cy-w,courtLength+turn+2*w,w,Walk));
  b.Pieces.Add(part(cx-w,cy+courtDepth,courtLength+turn+2*w,w,Walk));
  b.Pieces.Add(part(cx-w,cy,w,courtDepth,Walk));
  b.Pieces.Add(part(cx+courtLength+turn,cy,w,courtDepth,Walk));
  b.Pieces.Add(part(cx+courtLength,cy,turn,courtDepth,1));
  for(int n=0;n<lanes;n++){
   double v=cy+n*(2*bay+aisle+island);
   b.Pieces.Add(part(cx,v+bay,courtLength+turn,aisle,1));
   if(bay==0)continue;
   // Preserve a cross-court walking strip and clear both ends of the parking row.
   double usable=courtLength-2*w,half=Math.Floor((usable-island)/2/o.BayWidth)*o.BayWidth;
   foreach(double by in new[]{v,v+bay+aisle})for(int j=0;j<2;j++){
    if(half<4*o.BayWidth)continue;
    var row=part(cx+w+j*(half+island),by,half,bay,2);row.ParkingSpaces=(int)Math.Round(half/o.BayWidth);row.ClusterId=group.Id;
    b.Pieces.Add(row);group.Parking.Add(row);group.Capacity+=row.ParkingSpaces;
   }
  }
  Action<double,double,bool> building=(u,v,rot)=>{
   var r=part(u,v,rot?d:l,rot?l:d,0);r.AssetId=a.Id;r.ClusterId=plan.Courts.Count;r.Homes=a.Homes;
   r.FloorArea=Math.Min(a.Width*a.Depth,a.Footprint>0?a.Footprint:a.Width*a.Depth*o.FootprintRatio)*Math.Max(1,Math.Floor(a.Height/o.FloorHeight));
   Point door=rot?new Point(u+d+w/2,v+l/2):new Point(u+l/2,v<cy?v+d+w/2:v-w/2);
   r.Access=new Point(x+(vertical?door.Y:door.X),y+(vertical?door.X:door.Y));
   b.Pieces.Add(r);b.Homes+=r.Homes;b.FloorArea+=r.FloorArea;
  };
  building(cx,cy-w-d,false);building(cx,cy+courtDepth+w,false);
  if(side)building(rear,cy+(courtDepth-l)/2,true);
  group.Buildings=b.Pieces.Where(r=>r.Kind==0).ToList();group.AccessPoints=group.Buildings.Select(r=>r.Access.Value).ToList();
  var gate=part(cx+courtLength,cy+bay,turn,aisle,1);b.Gate=new Point(gate.X+gate.W/2,gate.Y+gate.D/2);group.Gate=b.Gate;
  return b;
 }
 void SharedPlan(){
  if(assets.Count==0)return;
  // A common row datum survives court width and asset changes; only boundary
  // candidates may use the orthogonal direction. Connected courts grow from entry.
  double datum=y0+cell;int candidates=0;
  double rowPitch=Snap(2*assets.Max(a=>Math.Min(a.Width,a.Depth))+2*o.Walkway+2*Math.Max(2,o.Gap/2)+Math.Max(assets.Max(a=>Math.Max(a.Width,a.Depth)),Math.Min(3,Math.Max(1,o.MaxParkingAisles))*(2*o.BayDepth+Math.Max(o.Road,o.ParkingAisle))+(Math.Min(3,Math.Max(1,o.MaxParkingAisles))-1)*Math.Max(o.Walkway,o.ParkingIsland)));
  for(int pass=0;pass<(o.EdgeInfill?3:2)&&!Full;pass++){
   int stride=Math.Max(2,(int)Math.Ceiling(4/cell));
   for(int y=1;y<ny-1&&!Full;y+=(pass<2&&o.AlignBlocks?(int)Math.Ceiling(rowPitch/cell):stride))for(int x=1;x<nx-1&&!Full;x+=stride){
    if(map[x,y]!=Green)continue;
    for(int ai=0;ai<assets.Count;ai++){
     var a=assets[(assetIndex+ai)%assets.Count];int max=o.WideSlabParking?Math.Min(3,Math.Max(1,o.MaxParkingAisles)):1;
     bool placed=false;
     for(int lanes=max;lanes>=1&&!placed;lanes--){
      bool side=pass==0;bool vertical=pass==2;
      var b=Court(x0+x*cell,datum+(y-1)*cell,a,lanes,side,vertical,out var group);candidates++;
      if(!Fits(b)||!Place(b))continue;
      plan.Courts.Add(group);assetIndex=(assetIndex+ai+1)%assets.Count;placed=true;
     }
     if(placed)break;
    }
   }
  }
  ConnectWalks();
  plan.Diagnostics="courts="+plan.Courts.Count+" candidates="+candidates+" parking="+spaces+" pedestrianRoutes="+plan.Routes.Count+" unreachable="+plan.UnreachableAccess.Count;
 }
 void ConnectWalks(){
  var connected=new bool[nx,ny];var roots=new List<Point>();
  var walkRoads=plan.ConstructionRoutes.Concat(plan.Routes).Where(r=>PedestrianRoadAccess.Supports(r,o)).ToList();
  // Road sidewalks are destinations too. Seed only the actual native road ribbon;
  // a car-only invisible aisle must never satisfy pedestrian access by proximity.
  foreach(var road in walkRoads)for(int i=1;i<road.Points.Count;i++){
   var a=road.Points[i-1];var b=road.Points[i];double half=PedestrianRoadAccess.Width(road,o)/2;
   int left=Math.Max(0,X(Math.Min(a.X,b.X)-half)),right=Math.Min(nx-1,X(Math.Max(a.X,b.X)+half));
   int bottom=Math.Max(0,Y(Math.Min(a.Y,b.Y)-half)),top=Math.Min(ny-1,Y(Math.Max(a.Y,b.Y)+half));
   for(int y=bottom;y<=top;y++)for(int x=left;x<=right;x++){
    if(connected[x,y]||map[x,y]!=1)continue;var point=new Point(x0+(x+.5)*cell,y0+(y+.5)*cell);
    if(!PedestrianRoadAccess.Covers(road,point,o))continue;
    var delta=b-a;double t=Math.Max(0,Math.Min(1,SiteFrame.Dot(point-a,delta)/SiteFrame.Dot(delta,delta)));
    if((point-a-delta*t).Length>cell*.8)continue;connected[x,y]=true;if(roots.Count==0)roots.Add(point);
   }
  }
  // Seed only real walkable roads. An arbitrary point beside an entrance
  // is not a connected path and must not terminate building access routes.
  // An absent seed is a routing failure, not proof of pedestrian access.
  var sortOrigin=roots.Count>0?roots[0]:entranceTips[0];
  var targets=new List<Point[]>();
  foreach(var b in exact.Where(r=>r.Kind==0)){
   double pad=o.Walkway/2+cell;
   var asset=assets.Find(a=>a.Id==b.AssetId);
   // Do not invent a frontage entrance for every lot. Access is a layout hint,
   // not a native pedestrian requirement. Route only actual prefab access points;
   // those already covered by a walkable road are skipped below.
   // A frontage walk can be exactly one native path wide. The old extra-cell
   // offset moved its start into the adjacent parking row. Try centers on the
   // same lot edge down to half-width; FindRoute still checks full clearance.
   if(asset!=null)foreach(var point in asset.PedestrianAccess)targets.Add(new[]{pad,o.Walkway/2,o.Walkway/2+cell/2}.Select(offset=>BuildingPlacement.AccessTarget(b,asset,point,offset)).ToArray());
  }
  // One usable entrance per connected facility area, considering ALL edges.
  // Sampling only the west edge can put school access inside a neighboring slab.
  var seen=new bool[nx,ny];int[] dx={1,-1,0,0},dy={0,0,1,-1};
  for(int y=1;y<ny-1;y++)for(int x=1;x<nx-1;x++){
   if(seen[x,y]||map[x,y]!=3&&map[x,y]!=5)continue;
   int kind=map[x,y];var pending=new List<int>();var choices=new List<Point>();pending.Add(y*nx+x);seen[x,y]=true;
   for(int head=0;head<pending.Count;head++){int n=pending[head],cx=n%nx,cy=n/nx;
    for(int k=0;k<4;k++){int xx=cx+dx[k],yy=cy+dy[k];if(xx<0||yy<0||xx>=nx||yy>=ny)continue;
     if(map[xx,yy]==kind){if(!seen[xx,yy]){seen[xx,yy]=true;pending.Add(yy*nx+xx);}continue;}
     if((cx+cy)%4!=0)continue;int pad=(int)Math.Ceiling(o.Walkway/2/cell)+1;xx=cx+dx[k]*pad;yy=cy+dy[k]*pad;
     if(xx<=0||yy<=0||xx>=nx-1||yy>=ny-1)continue;int value=map[xx,yy];
     if(value==Green||value==Paved||value==CourtGreen||value==Walk)choices.Add(new Point(x0+(xx+.5)*cell,y0+(yy+.5)*cell));
    }
   }
   if(choices.Count>0)targets.Add(choices.OrderBy(p=>(p-sortOrigin).Length).Take(64).ToArray());else targets.Add(new[]{new Point(x0+(x+.5)*cell,y0+(y+.5)*cell)});
  }
  int required=targets.Count,targetIndex=0;
  // Only actual building/facility access targets need paths; no arbitrary perimeter stubs.
  foreach(var choices in targets){
   // An access target already on a road's sidewalk needs no extra path footprint.
   if(choices.Any(p=>{int x=X(p.X),y=Y(p.Y);return x>=0&&y>=0&&x<nx&&y<ny&&map[x,y]==1&&walkRoads.Any(r=>PedestrianRoadAccess.Covers(r,p,o));})){targetIndex++;continue;}
   List<Point> route=null;Point access=choices[0];int attempts=0,rejections=roots.Count==0?4:0,visited=0;
   foreach(var target in choices){route=FindRoute(target,(x,y)=>connected[x,y],o.Walkway,true);attempts++;rejections|=lastRouteFailure;visited=(int)Math.Min(int.MaxValue,(long)visited+lastRouteVisited);if(route!=null){access=target;break;}}
   if(route==null){if(targetIndex<required){plan.RequiredAccessFailures++;plan.AccessEvidenceTotal++;if(plan.AccessEvidence.Count<24)plan.AccessEvidence.Add(new Dictionary<string,int>{{"reason",86},{"ordinal",targetIndex},{"flags",rejections},{"edges",attempts},{"connected",visited}});}targetIndex++;plan.UnreachableAccess.Add(access);continue;}targetIndex++;
   if(route.Count>1)DrawRoute(route,o.Walkway,"pedestrian");foreach(var p in route)connected[X(p.X),Y(p.Y)]=true;
  }
 }
}
}
