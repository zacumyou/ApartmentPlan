using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
public static class ParkingFrontRules {
 public static bool Served(Parcel building,IEnumerable<Parcel> bays,double distance,int minimum,double coverage=.2){
  // A real entrance may split a row into pockets; count all nearby spaces.
  return bays.Where(p=>p.Kind==2&&Math.Sqrt(Math.Pow(Math.Max(0,Math.Max(p.X-building.X-building.W,building.X-p.X-p.W)),2)+Math.Pow(Math.Max(0,Math.Max(p.Y-building.Y-building.D,building.Y-p.Y-p.D)),2))<=distance).Sum(p=>p.ParkingSpaces)>=minimum;

 }
}
internal sealed partial class ResidentialLayout {
 bool[,] edgeGreen;
 int[,] packingBlocked,roadDistance;bool packingDirty=true;
 bool CandidateRoom(Parcel r,double x,double y){
  if(packingDirty){packingBlocked=new int[nx+1,ny+1];for(int j=0;j<ny;j++)for(int i=0;i<nx;i++)packingBlocked[i+1,j+1]=((map[i,j]!=Green&&map[i,j]!=1||reserved[i,j]||edgeGreen[i,j])?1:0)+packingBlocked[i,j+1]+packingBlocked[i+1,j]-packingBlocked[i,j];roadDistance=new int[nx,ny];var queue=new List<int>();
   for(int j=0;j<ny;j++)for(int i=0;i<nx;i++){roadDistance[i,j]=map[i,j]==1?0:int.MaxValue;if(map[i,j]==1)queue.Add(j*nx+i);}
   int[] dx={-1,1,0,0},dy={0,0,-1,1};for(int head=0;head<queue.Count;head++){int v=queue[head],i=v%nx,j=v/nx;for(int k=0;k<4;k++){int a=i+dx[k],b=j+dy[k];if(a<0||b<0||a>=nx||b>=ny||roadDistance[a,b]<=roadDistance[i,j]+1)continue;roadDistance[a,b]=roadDistance[i,j]+1;queue.Add(b*nx+a);}}
   packingDirty=false;}
  int left=X(x),bottom=Y(y),c=(int)Math.Ceiling((x+r.W-x0)/cell-1e-7),d=(int)Math.Ceiling((y+r.D-y0)/cell-1e-7);
  return left>=0&&bottom>=0&&c<=nx&&d<=ny&&packingBlocked[c,d]-packingBlocked[left,d]-packingBlocked[c,bottom]+packingBlocked[left,bottom]==0;
 }

 void ProtectEdge(){
  edgeGreen=new bool[nx,ny];
  for(int y=0;y<ny;y++)for(int x=0;x<nx;x++){
   var corners=Rect(x,y,1,1).Corners;double distance=double.MaxValue;
   foreach(var p in corners)for(int i=0;i<poly.Count;i++){
    var a=poly[i];var d=poly[(i+1)%poly.Count]-a;double t=Math.Max(0,Math.Min(1,SiteFrame.Dot(p-a,d)/SiteFrame.Dot(d,d)));
    distance=Math.Min(distance,(p-a-d*t).Length);
   }
   edgeGreen[x,y]=distance<o.BoundaryGreenMargin-.001;
  }
 }
 Block ParkingCourt(double x,double y,Asset bar,Asset cap,string kind,int lanes,bool mirror,bool rotate,bool crossParking=false,bool wideEntrance=false){
  if(o.ContinuousFrontage)return ConnectedFrontageCourt(x,y,bar,cap,kind,lanes,mirror,rotate,crossParking,wideEntrance);
  double length=Math.Max(bar.Width,bar.Depth),depth=Math.Min(bar.Width,bar.Depth);
  double cd=Math.Min(cap.Width,cap.Depth),cl=Math.Max(cap.Width,cap.Depth);
  // A selected path's width is not a compulsory strip along every building.
  // Actual access points get local openings in the parking rows below.
  double aisle=Snap(o.ParkingAisle+2*o.ParkingPavingMargin),bay=Snap(o.BayDepth),island=Snap(o.ParkingIsland);
  double stagger=o.AlignBlocks?Snap(o.BuildingStagger):0;
  bool side=kind=="u"||kind=="ring",ring=kind=="ring",single=kind=="single";
  double court=lanes*(2*bay+aisle)+(lanes-1)*island;
  if(crossParking)court=Math.Max(court,2*bay+2*aisle+2*o.ParkingEndClearance+o.MinFrontParkingSpaces*o.BayWidth);
  court=Math.Max(court,o.ParallelGap);if(side)court=Math.Max(court,cl);
  double start=side?cd+Math.Max(o.Gap,aisle):0;
  double right=start+length+stagger;
  double width=ring?right+o.Gap+cd:right,height=single?depth+bay+aisle:2*depth+court;
  var b=new Block{MaxConnection=o.MaxAccessLength,LandscapeRemainder=true};
  Func<double,double,double,double,int,Parcel> part=(u,v,w,h,k)=>{var p=new Parcel{X=u,Y=v,W=w,D=h,Kind=k,ParkingAlongX=true};b.Pieces.Add(p);return p;};
  Action<double,double,Asset,bool,Parcel> building=(u,v,a,vertical,front)=>{
   double l=Math.Max(a.Width,a.Depth),d=Math.Min(a.Width,a.Depth);var p=part(u,v,vertical?d:l,vertical?l:d,0);
   p.AssetId=a.Id;p.Homes=a.Homes;p.FloorArea=Math.Min(a.Width*a.Depth,a.Footprint>0?a.Footprint:a.Width*a.Depth*o.FootprintRatio)*Math.Max(1,Math.Floor(a.Height/o.FloorHeight));p.Frontage=front;
   p.Access=vertical?new Point(front.X>u?u+d:u,v+l/2):new Point(u+l/2,front.Y>v?v+d:v);
   b.Homes+=p.Homes;b.FloorArea+=p.FloorArea;
  };
  double leftRoad=side?cd:0,rightRoad=ring?right+o.Gap-aisle:right;
  double parkingStart=side?leftRoad+aisle:0;
  double parkingEnd=ring?rightRoad:right;
  parkingStart+=o.ParkingEndClearance;parkingEnd-=o.ParkingEndClearance;
  double span=Math.Floor((parkingEnd-parkingStart)/o.BayWidth)*o.BayWidth;
  if(span<o.MinFrontParkingSpaces*o.BayWidth)return null;
  var streets=new List<Parcel>();
  for(int lane=0;lane<(single?1:lanes);lane++){
   double bottom=depth+lane*(2*bay+aisle+island);
   if(!single&&lane==lanes-1)bottom=depth+court-2*bay-aisle;
   var road=part(side?leftRoad:0,bottom+bay,(ring?rightRoad+aisle:right)-(side?leftRoad:0),aisle,1);streets.Add(road);
   var first=part(parkingStart,bottom,span,bay,2);first.ParkingSpaces=(int)Math.Floor(span/o.BayWidth+.001);
   if(!single){var second=part(parkingStart,bottom+bay+aisle,span,bay,2);second.ParkingSpaces=first.ParkingSpaces;}
  }
  // Full-length fronts share the court's aisles; selected lots are never resized.
  var firstFront=new Parcel{Kind=1,X=start,Y=streets[0].Y,W=length,D=aisle};
  building(start,0,bar,false,firstFront);
  if(!single){var lastFront=new Parcel{Kind=1,X=start+stagger,Y=streets.Last().Y,W=length,D=aisle};building(start+stagger,depth+court,bar,false,lastFront);}
  if(side){
   double cy=depth+(court-cl)/2;
   var front=part(leftRoad,depth,aisle,court,1);building(0,cy,cap,true,new Parcel{Kind=1,X=front.X,Y=cy,W=aisle,D=cl});
   if(ring){part(rightRoad,depth,aisle,court,1);building(right+o.Gap,cy,cap,true,new Parcel{Kind=1,X=rightRoad,Y=cy,W=aisle,D=cl});
    // Exit through the corner gap without crossing any parking bays.
    double gateY=Math.Max(0,depth-aisle);part(0,gateY,leftRoad+aisle,aisle,1);part(leftRoad,gateY,aisle,depth+court-gateY,1);
    b.Gate=new Point(aisle/2,gateY+aisle/2);
   }else b.Gate=new Point(right-aisle/2,streets[0].Y+aisle/2);
  }else{
   if(lanes>1&&!single){ // Leave a side link clear of every bay row.
    double extra=aisle;foreach(var p in b.Pieces.Where(p=>p.Kind==1))p.W+=extra;
    part(right,streets[0].Y,extra,streets.Last().Y-streets[0].Y+aisle,1);width+=extra;
   }
   b.Gate=new Point(width-aisle/2,streets[0].Y+aisle/2);
  }
  if(crossParking){
   if(single||streets.Count<2)return null;
   double top=streets[0].Y+aisle+o.ParkingEndClearance,bottom=streets.Last().Y-o.ParkingEndClearance;
   double run=Math.Floor((bottom-top)/o.BayWidth)*o.BayWidth;
   double left=side?leftRoad+aisle:0,rightEnd=ring?rightRoad:right;
   if(run<o.MinFrontParkingSpaces*o.BayWidth||rightEnd-left<2*bay+aisle)return null;
   b.Pieces.RemoveAll(p=>p.Kind==2||streets.Skip(1).Take(streets.Count-2).Contains(p));
   int added=0;for(double u=left;u+2*bay+aisle<=rightEnd+.001;u+=2*bay+aisle+island){
    part(u+bay,streets[0].Y,aisle,streets.Last().Y+aisle-streets[0].Y,1);
    foreach(double bx in new[]{u,u+bay+aisle}){var p=part(bx,top,bay,run,2);p.ParkingAlongX=false;p.ParkingSpaces=(int)Math.Round(run/o.BayWidth);added++;}
   }
   if(added==0)return null;
  }
  b.Bounds=new Parcel{X=0,Y=0,W=width,D=height};
  Func<Point,Point> point=p=>{double u=mirror?width-p.X:p.X;return new Point(x+(rotate?p.Y:u),y+(rotate?u:p.Y));};
  Action<Parcel> transform=p=>{double u=mirror?width-p.X-p.W:p.X,v=p.Y,w=p.W,h=p.D;p.X=x+(rotate?v:u);p.Y=y+(rotate?u:v);p.W=rotate?h:w;p.D=rotate?w:h;if(rotate)p.ParkingAlongX=!p.ParkingAlongX;};
  b.Gate=point(b.Gate);foreach(var p in b.Pieces.Where(p=>p.Kind==0)){p.Access=point(p.Access.Value);transform(p.Frontage);}foreach(var p in b.Pieces)transform(p);transform(b.Bounds);return b;
 }
 bool CommitParkingCourt(Block b,string kind){
  if(b!=null&&o.RequireBuildingRoad&&!EnsureCourtFrontage(b))return false;
  if(b==null||roadDistance!=null&&roadDistance[X(b.Gate.X),Y(b.Gate.Y)]*cell>o.MaxAccessLength||!Fits(b))return false;
  if(o.PedestrianNetwork)OpenCourtAccess(b);
  var bars=b.Pieces.Where(p=>p.Kind==0).ToList();var bays=b.Pieces.Where(p=>p.Kind==2).ToList();
  if(bars.Any(p=>!BarOrientationAllowed(p)))return false;
  if(exact.Count(p=>p.Kind==0)+bars.Count>o.MaxBuildings||bays.Any(p=>!ClearOfMain(p)))return false;
  for(int i=0;i<bars.Count;i++){
   if(exact.Any(p=>p.Kind==0&&!FrontageRules.Spaced(bars[i],p,o.ParallelGap,o.Gap))||bars.Take(i).Any(p=>!FrontageRules.Spaced(bars[i],p,o.ParallelGap,o.Gap)))return false;
   if(o.RequireFrontParking&&!ParkingFrontRules.Served(bars[i],bays,o.FrontParkingDistance,o.MinFrontParkingSpaces,o.FrontParkingCoverage))return false;
  }
  if(b.Pieces.Where(p=>p.Kind==0||p.Kind==2).Any(p=>b.Pieces.Any(q=>!ReferenceEquals(p,q)&&Layout.Overlap(p,q))))return false;
  if(!Place(b))return false;packingDirty=true;
  var c=new CourtGroup{Id=plan.Courts.Count,Pattern=kind,Bounds=b.Bounds,Gate=b.Gate,Buildings=bars,Parking=bays,Capacity=bays.Sum(p=>p.ParkingSpaces)};
  foreach(var p in bars){p.ClusterId=c.Id;c.AccessPoints.Add(p.Access.Value);}plan.Courts.Add(c);return true;
 }
 void OpenCourtAccess(Block block){
  // Reserve only a crossing at a real prefab entrance. A continuous frontage
  // path is neither a prerequisite for parking nor a substitute for native access.
  foreach(var lot in block.Pieces.Where(p=>p.Kind==0).ToArray()){
   var asset=assets.Find(a=>a.Id==lot.AssetId);if(asset==null)continue;
   foreach(var access in asset.PedestrianAccess){
    var edge=BuildingPlacement.AccessTarget(lot,asset,access,0);
    var normal=BuildingPlacement.AccessTarget(lot,asset,access,1)-edge;
    bool horizontal=Math.Abs(normal.X)>.5;double sign=horizontal?normal.X:normal.Y;
    var street=block.Pieces.Where(p=>p.Kind==1&&
     (horizontal?edge.Y>=p.Y&&edge.Y<=p.Y+p.D:edge.X>=p.X&&edge.X<=p.X+p.W)&&
     (horizontal?p.X+p.W/2-edge.X:p.Y+p.D/2-edge.Y)*sign>0)
     .OrderBy(p=>(horizontal?p.X+p.W/2-edge.X:p.Y+p.D/2-edge.Y)*sign).FirstOrDefault();
    if(street==null)continue;
    double distance=(horizontal?street.X+street.W/2-edge.X:street.Y+street.D/2-edge.Y)*sign;
    double half=Snap(o.Walkway)/2+cell;var end=edge+normal*distance;
    var gap=new Parcel{X=horizontal?Math.Min(edge.X,end.X):edge.X-half,Y=horizontal?edge.Y-half:Math.Min(edge.Y,end.Y),W=horizontal?distance:2*half,D=horizontal?2*half:distance};
    if(block.Pieces.Any(p=>p.Kind==0&&Layout.Overlap(p,gap)))continue;
    SplitParkingRows(block,gap);
   }
  }
 }
 static void SplitParkingRows(Block block,Parcel corridor){
  foreach(var row in block.Pieces.Where(p=>p.Kind==2&&Layout.Overlap(p,corridor)).ToArray()){
   bool along=row.ParkingAlongX;double origin=along?row.X:row.Y,length=along?row.W:row.D,pitch=length/row.ParkingSpaces;
   int a=Math.Max(0,(int)Math.Floor(((along?corridor.X:corridor.Y)-origin)/pitch));
   int z=Math.Min(row.ParkingSpaces,(int)Math.Ceiling(((along?corridor.X+corridor.W:corridor.Y+corridor.D)-origin)/pitch));
   block.Pieces.Remove(row);
   foreach(var range in new[]{new[]{0,a},new[]{z,row.ParkingSpaces}}){int count=range[1]-range[0];if(count<=0)continue;
    block.Pieces.Add(new Parcel{Kind=2,X=row.X+(along?range[0]*pitch:0),Y=row.Y+(along?0:range[0]*pitch),W=along?count*pitch:row.W,D=along?row.D:count*pitch,ParkingAlongX=along,ParkingSpaces=count});
   }
  }
 }
 bool EnsureCourtFrontage(Block block){
  double width=o.AccessRoad>0?o.AccessRoad:o.ParkingAisle;
  foreach(var lot in block.Pieces.Where(p=>p.Kind==0).ToArray()){
   var asset=assets.Find(a=>a.Id==lot.AssetId);if(asset==null||lot.Frontage==null)continue;
   var f=BuildingPlacement.Forward(lot,asset);var side=new Point(f.Y,-f.X);
   var front=new Point(lot.X+lot.W/2,lot.Y+lot.D/2)+f*(asset.Depth/2);
   var road=lot.Frontage;var center=new Point(road.X+road.W/2,road.Y+road.D/2);
   double depth=SiteFrame.Dot(center-front,f);
   // Match the installed native 8.4m road-edge search. Deep real decals plus
   // a full-width footpath can exceed it; reserve a short access spur instead
   // of relaxing the native road requirement or hiding the warning.
   if(depth-width/2<8.0)continue;
   double offset=Math.Min(width/2,asset.Width/4);
   var start=front+side*offset+f*depth;var end=front+side*offset+f*(width/2+3);
   if(SiteFrame.Dot(start-end,f)<=.1)return false;
   var corridor=new Parcel{Kind=1,X=Math.Min(start.X,end.X)-width/2,Y=Math.Min(start.Y,end.Y)-width/2,W=Math.Abs(start.X-end.X)+width,D=Math.Abs(start.Y-end.Y)+width};
   if(!Layout.Contains(corridor,block.Bounds.Corners)||block.Pieces.Any(p=>p.Kind==0&&Layout.Overlap(p,corridor)))return false;
   SplitParkingRows(block,corridor);
   block.Pieces.Add(corridor);block.StreetAxes[corridor]=new PlanRoute{Use="aisle",Width=width,Points=new List<Point>{start,end}};
  }
  return true;
 }
 void UnfrontedInfill(){
  int stride=Math.Max(1,(int)Math.Ceiling(o.SearchStep/cell));
  for(int y=1;y<ny-1&&!Full;y+=stride)for(int x=1;x<nx-1&&!Full;x+=stride){
   if(map[x,y]!=Green)continue;bool placed=false;
   foreach(var a in assets.OrderByDescending(a=>a.Width*a.Depth))foreach(bool rotate in new[]{false,true}){
    if(placed)break;if(rotate&&!o.MixedDirections&&o.BuildingPattern==0)continue;
    double l=Math.Max(a.Width,a.Depth),d=Math.Min(a.Width,a.Depth);var p=new Parcel{Kind=0,X=x0+x*cell,Y=y0+y*cell,W=rotate?d:l,D=rotate?l:d,AssetId=a.Id,Homes=a.Homes};
    if(!BarOrientationAllowed(p)||!Free(p)||!ClearOfMain(p)||exact.Any(q=>q.Kind==0&&!FrontageRules.Spaced(p,q,o.ParallelGap,o.Gap))||o.RequireFrontParking&&!ParkingFrontRules.Served(p,exact,o.FrontParkingDistance,o.MinFrontParkingSpaces))continue;
    p.FloorArea=Math.Min(a.Width*a.Depth,a.Footprint>0?a.Footprint:a.Width*a.Depth*o.FootprintRatio)*Math.Max(1,Math.Floor(a.Height/o.FloorHeight));
    if(o.RespectTargetFar&&floors+p.FloorArea>plan.Area*o.TargetFar/100+.001)continue;
    Paint(p,Fixed);exact.Add(p);floors+=p.FloorArea;plan.Homes+=p.Homes;modules++;placed=true;
   }
  }
 }
 void ParkingCourtHousing(){
  if(assets.Count==0)return;
  var shapes=new Dictionary<(string,Asset,Asset,int,int,bool,bool,bool),Parcel>();
  var orderedAssets=assets.OrderByDescending(a=>Math.Max(a.Width,a.Depth)).ToArray();
  var cap=assets.OrderBy(a=>Math.Max(a.Width,a.Depth)).First();
  var crossOptions=o.ParkingOrientation==2?new[]{true,false}:new[]{false,true};
  var entranceOptions=o.ContinuousFrontage&&o.WideParkingAisle>o.ParkingAisle?new[]{true,false}:new[]{false};
  foreach(var kind in new[]{"ring","u","pair","single"}){
   if(o.BuildingPattern!=0&&(kind=="ring"||kind=="u"))continue;
   if(kind=="ring"&&(!o.AllowRingCourts||!o.MixedDirections)||kind=="u"&&(!o.AllowOpenCourts||!o.MixedDirections)||kind=="pair"&&!o.AllowParallelPairs||kind=="single"&&!o.EdgeInfill)continue;
   int stride=Math.Max(1,(int)Math.Ceiling(o.SearchStep/cell));
   for(int iy=1;iy<ny-1&&!Full;iy+=stride)for(int ix=1;ix<nx-1&&!Full;ix+=stride){
    o.Cancellation.ThrowIfCancellationRequested();
    if(kind=="ring"&&plan.Courts.Count(c=>c.Pattern=="ring")>=o.MaxRingCourts)break;
    if(map[ix,iy]!=Green||edgeGreen[ix,iy])continue;bool placed=false;
    bool preferVertical=(o.MixedDirections||o.BuildingPattern==3)&&exact.Count(p=>p.Kind==0&&p.D>p.W)<exact.Count(p=>p.Kind==0)*o.VerticalPreference;
    foreach(bool rotation in new[]{preferVertical,!preferVertical}){
     if(rotation&&!o.MixedDirections&&o.BuildingPattern==0)continue;
     foreach(var a in orderedAssets){
      if(o.BuildingPattern==1&&(rotation?a.Depth:a.Width)<(rotation?a.Width:a.Depth)-.01||o.BuildingPattern==2&&(rotation?a.Width:a.Depth)<(rotation?a.Depth:a.Width)-.01)continue;
      if(placed)break;
      int lanes=o.WideSlabParking&&kind!="single"?Math.Min(o.MaxParkingAisles,Math.Max(1,(int)Math.Ceiling(a.Homes*2*o.ParkingPerHome*o.SurfaceParkingShare/(Math.Max(a.Width,a.Depth)/o.BayWidth*2)))):1;
      for(int lane=lanes;lane>=1&&!placed;lane--)for(int reflection=0;reflection<2&&!placed;reflection++)foreach(bool cross in crossOptions){
       if(placed)break;if(o.ParkingOrientation==1&&cross)continue;
       foreach(bool wideEntrance in entranceOptions){
       if(placed)break;
       var key=(kind,a,cap,lane,reflection,rotation,cross,wideEntrance);
       if(!shapes.TryGetValue(key,out var shape)){shape=ParkingCourt(0,0,a,cap,kind,lane,reflection==1,rotation,cross,wideEntrance)?.Bounds;shapes[key]=shape;}
       if(shape==null||!CandidateRoom(shape,x0+ix*cell,y0+iy*cell))continue;
       var block=ParkingCourt(x0+ix*cell,y0+iy*cell,a,cap,kind,lane,reflection==1,rotation,cross,wideEntrance);
       if(CommitParkingCourt(block,kind))placed=true;
       }
      }
      if(placed)break;
     }
     if(placed)break;
    }
   }
  }
  // Disabling the rule explicitly allows unserved residual bars.
  if(!o.ContinuousFrontage&&!o.RequireBuildingRoad&&o.EdgeInfill)UnfrontedInfill();
  else if(!o.RequireFrontParking&&o.EdgeInfill)FrontageHousing(false);
 }
}
}

