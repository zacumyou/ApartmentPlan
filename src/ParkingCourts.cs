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
 int[,] packingBlocked,packingRoads,roadDistance;bool packingDirty=true;
 bool CandidateRoom(Parcel r,double x,double y){
  using(var timingScope=LayoutFunctionTiming.Start(o.FunctionTiming,"CandidateRoom")){
  if(packingDirty){packingBlocked=new int[nx+1,ny+1];for(int j=0;j<ny;j++)for(int i=0;i<nx;i++)packingBlocked[i+1,j+1]=((map[i,j]!=Green&&map[i,j]!=1||reserved[i,j]||edgeGreen[i,j])?1:0)+packingBlocked[i,j+1]+packingBlocked[i+1,j]-packingBlocked[i,j];packingRoads=new int[nx+1,ny+1];
   for(int j=0;j<ny;j++)for(int i=0;i<nx;i++)packingRoads[i+1,j+1]=(map[i,j]==1?1:0)+packingRoads[i,j+1]+packingRoads[i+1,j]-packingRoads[i,j];
   roadDistance=new int[nx,ny];var queue=new List<int>();
   // Lower bound on connector length before the candidate occupies any
   // cells. Buildings and junction reservations cannot disappear on commit.
   int[,] obstacles=new int[nx+1,ny+1];var clear=new bool[nx,ny];
   for(int j=0;j<ny;j++)for(int i=0;i<nx;i++){
    int k=map[i,j];bool blocked=k==Outside||k==Fixed||k==Reserve||k==3||k==5||reserved[i,j]||(mainJunctionBuffer?[i,j]??false);
    obstacles[i+1,j+1]=(blocked?1:0)+obstacles[i,j+1]+obstacles[i+1,j]-obstacles[i,j];
   }
   double access=o.AccessRoad>0?o.AccessRoad:o.ContinuousFrontage?o.ParkingAisle:o.Road;
   int span=(int)Math.Ceiling(access/cell),lo=span/2,hi=span-lo;
   for(int j=0;j<ny;j++)for(int i=0;i<nx;i++){
    int a=i-lo,b=j-lo,endX=i+hi,endY=j+hi;
    clear[i,j]=a>=0&&b>=0&&endX<=nx&&endY<=ny&&obstacles[endX,endY]-obstacles[a,endY]-obstacles[endX,b]+obstacles[a,b]==0;
    bool target=clear[i,j]&&map[i,j]==1;roadDistance[i,j]=target?0:int.MaxValue;if(target)queue.Add(j*nx+i);
   }
   int[] dx={-1,1,0,0},dy={0,0,-1,1};for(int head=0;head<queue.Count;head++){int v=queue[head],i=v%nx,j=v/nx;for(int k=0;k<4;k++){int a=i+dx[k],b=j+dy[k];if(a<0||b<0||a>=nx||b>=ny||!clear[a,b]||roadDistance[a,b]<=roadDistance[i,j]+1)continue;roadDistance[a,b]=roadDistance[i,j]+1;queue.Add(b*nx+a);}}
   packingDirty=false;}
  int left=X(x),bottom=Y(y),c=(int)Math.Ceiling((x+r.W-x0)/cell-1e-7),d=(int)Math.Ceiling((y+r.D-y0)/cell-1e-7);
  return left>=0&&bottom>=0&&c<=nx&&d<=ny&&packingBlocked[c,d]-packingBlocked[left,d]-packingBlocked[c,bottom]+packingBlocked[left,bottom]==0;
 
  }
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
  using(var timingScope=LayoutFunctionTiming.Start(o.FunctionTiming,"ParkingCourt")){
  if(o.ContinuousFrontage||!o.GenerateSurfaceParking)return ConnectedFrontageCourt(x,y,bar,cap,kind,lanes,mirror,rotate,crossParking,wideEntrance);
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
 }
 bool CommitParkingCourt(Block b,string kind){
  using(var timingScope=LayoutFunctionTiming.Start(o.FunctionTiming,"CommitParkingCourt")){
  if(b!=null&&o.RequireBuildingRoad&&!EnsureCourtFrontage(b))return false;
  if(b==null||roadDistance!=null&&roadDistance[X(b.Gate.X),Y(b.Gate.Y)]*cell>o.MaxAccessLength||!Fits(b))return false;
  if(o.PedestrianNetwork)OpenCourtAccess(b);
  var bars=b.Pieces.Where(p=>p.Kind==0).ToList();var bays=b.Pieces.Where(p=>p.Kind==2).ToList();
  if(bars.Any(p=>!BarOrientationAllowed(p)))return false;
  if(exact.Count(p=>p.Kind==0)+bars.Count>o.MaxBuildings||bays.Any(p=>!ClearOfMain(p)))return false;
  for(int i=0;i<bars.Count;i++){
   if(exact.Any(p=>p.Kind==0&&!FrontageRules.Spaced(bars[i],p,o.ParallelGap,o.Gap))||bars.Take(i).Any(p=>!FrontageRules.Spaced(bars[i],p,o.ParallelGap,o.Gap)))return false;
  }
  if(b.Pieces.Where(p=>p.Kind==0||p.Kind==2).Any(p=>b.Pieces.Any(q=>!ReferenceEquals(p,q)&&Layout.Overlap(p,q))))return false;
  if(!Place(b))return false;packingDirty=true;
  var c=new CourtGroup{Id=plan.Courts.Count,Pattern=kind,Bounds=b.Bounds,Gate=b.Gate,Buildings=bars,Parking=bays,Capacity=bays.Sum(p=>p.ParkingSpaces)};
  foreach(var p in bars){p.ClusterId=c.Id;c.AccessPoints.Add(p.Access.Value);}plan.Courts.Add(c);return true;
 
  }
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
   foreach(var a in AssetChoice.Shuffle(assets,assetRandom))foreach(bool rotate in new[]{false,true}){
    if(placed)break;if(rotate&&!o.MixedDirections&&o.BuildingPattern==0)continue;
    double l=Math.Max(a.Width,a.Depth),d=Math.Min(a.Width,a.Depth);var p=new Parcel{Kind=0,X=x0+x*cell,Y=y0+y*cell,W=rotate?d:l,D=rotate?l:d,AssetId=a.Id,Homes=a.Homes};
    if(!BarOrientationAllowed(p)||!Free(p)||!ClearOfMain(p)||exact.Any(q=>q.Kind==0&&!FrontageRules.Spaced(p,q,o.ParallelGap,o.Gap)))continue;
    p.FloorArea=Math.Min(a.Width*a.Depth,a.Footprint>0?a.Footprint:a.Width*a.Depth*o.FootprintRatio)*Math.Max(1,Math.Floor(a.Height/o.FloorHeight));
    if(o.RespectTargetFar&&floors+p.FloorArea>plan.Area*o.TargetFar/100+.001)continue;
    Paint(p,Fixed);exact.Add(p);floors+=p.FloorArea;plan.Homes+=p.Homes;modules++;placed=true;
   }
  }
 }
 // Reachable road cells relative to a court's anchor, before site obstacles.
 // A connector cannot run parallel inside an existing street ribbon, even
 // for one grid step. Site obstacles can only narrow this template envelope.
 List<(int Y,int Left,int Right)> CourtReach(Block block){
  double budget=block.MaxConnection/cell;
  if(!(budget>=1&&budget<=32))return null;
  int steps=(int)Math.Floor(budget)-1;
  int gx=(int)Math.Floor(block.Gate.X/cell),gy=(int)Math.Floor(block.Gate.Y/cell);
  int left=gx-steps,bottom=gy-steps,n=2*steps+1;
  var targets=new bool[n,n];
  var streets=block.Pieces.Where(p=>p.Kind==1).Select(p=>StreetAxis(block,p)).ToArray();
  double narrow=o.AccessRoad>0?o.AccessRoad:o.ContinuousFrontage?o.ParkingAisle:o.Road;
  void Flood(double width){
   int span=(int)Math.Ceiling(width/cell);
   double offset=span%2==0?0:.5;
   var seen=new bool[n,n];var queue=new List<(int X,int Y,int Depth)>{(steps,steps,0)};seen[steps,steps]=true;
   for(int head=0;head<queue.Count;head++){
    var cur=queue[head];int x=cur.X,y=cur.Y;
    int ax=left+x,ay=bottom+y;
    if(ax<(int)Math.Floor(block.Bounds.X/cell)||ay<(int)Math.Floor(block.Bounds.Y/cell)||
       ax>=(int)Math.Ceiling((block.Bounds.X+block.Bounds.W)/cell-1e-7)||ay>=(int)Math.Ceiling((block.Bounds.Y+block.Bounds.D)/cell-1e-7))targets[x,y]=true;
    if(cur.Depth>=steps)continue;
    var from=new Point((ax+offset)*cell,(ay+offset)*cell);
    for(int dir=0;dir<4;dir++){
     int xx=x+routeDx[dir],yy=y+routeDy[dir];if(seen[xx,yy])continue;
     var to=from+new Point(routeDx[dir]*cell,routeDy[dir]*cell);
     bool conflict=false;
     foreach(var street in streets){
      for(int j=1;j<street.Points.Count;j++)if(ConstructionGraph.ParallelRibbons(from,to,width,street.Points[j-1],street.Points[j],street.Width)){conflict=true;break;}
      if(conflict)break;
     }
     if(conflict)continue;
     seen[xx,yy]=true;queue.Add((xx,yy,cur.Depth+1));
    }
   }
  }
  Flood(narrow);if(o.WideParkingAisle>narrow)Flood(o.WideParkingAisle);
  var rows=new List<(int,int,int)>();
  for(int y=0;y<n;y++)for(int x=0;x<n;){if(!targets[x,y]){x++;continue;}int begin=x++;while(x<n&&targets[x,y])x++;rows.Add((bottom+y,left+begin,left+x));}
  return rows;
 }
 bool ReachHasRoad(List<(int Y,int Left,int Right)> rows,int x,int y){
  if(rows==null)return true;
  foreach(var row in rows){
   int j=y+row.Y;if(j<0||j>=ny)continue;
   int a=Math.Max(0,x+row.Left),b=Math.Min(nx,x+row.Right);
   if(a<b&&packingRoads[b,j+1]-packingRoads[b,j]>packingRoads[a,j+1]-packingRoads[a,j])return true;
  }
  return false;
 }
 void ParkingCourtHousing(){
  if(assets.Count==0)return;
  var shapes=new Dictionary<(string,Asset,Asset,int,int,bool,bool,bool),(Parcel Bounds,Point Gate,Parcel[] Clearance)>();
  var reachByBounds=new Dictionary<Parcel,List<(int Y,int Left,int Right)>>();
  var candidateBuilding=new Parcel();
  var existingBars=exact.Where(q=>q.Kind==0).ToList();
  int verticalBars=existingBars.Count(q=>q.D>q.W);
  bool HousingFull()=>existingBars.Count>=o.MaxBuildings||o.RespectTargetFar&&floors>=plan.Area*o.TargetFar/100;
  var spacingAtAnchor=new Dictionary<(double,double,double,double),bool>();
  bool SpacedAtAnchor(Parcel candidate){
   var key=(candidate.X,candidate.Y,candidate.W,candidate.D);
   if(spacingAtAnchor.TryGetValue(key,out var spaced))return spaced;
   spaced=true;
   foreach(var bar in existingBars)if(!FrontageRules.Spaced(candidate,bar,o.ParallelGap,o.Gap)){spaced=false;break;}
   spacingAtAnchor[key]=spaced;return spaced;
  }
  // Fits requires road cells to be covered by a street. A centre strictly
  // inside a building or parking bay cannot also be covered by a valid street.
  bool CoversRoadCentre(Parcel r){
   int a=Math.Max(0,(int)Math.Ceiling((r.X-x0)/cell-.5+1e-7)),b=Math.Max(0,(int)Math.Ceiling((r.Y-y0)/cell-.5+1e-7));
   int c=Math.Min(nx,(int)Math.Ceiling((r.X+r.W-x0)/cell-.5-1e-7)),d=Math.Min(ny,(int)Math.Ceiling((r.Y+r.D-y0)/cell-.5-1e-7));
   return c>a&&d>b&&packingRoads[c,d]-packingRoads[a,d]-packingRoads[c,b]+packingRoads[a,b]>0;
  }
  // A connector must reach an existing road outside its own painted court.
  bool HasExternalRoad(Parcel shape,double x,double y,Point gate){
   if(o.MaxAccessLength/cell>=Math.Max(nx,ny))return true;
   int radius=(int)Math.Ceiling(o.MaxAccessLength/cell)+1;
   int gx=X(x+gate.X),gy=Y(y+gate.Y);
   int left=Math.Max(0,gx-radius),bottom=Math.Max(0,gy-radius),right=Math.Min(nx,gx+radius+1),top=Math.Min(ny,gy+radius+1);
   int Count(int a,int b,int c,int d)=>a>=c||b>=d?0:packingRoads[c,d]-packingRoads[a,d]-packingRoads[c,b]+packingRoads[a,b];
   int inside=Count(Math.Max(left,X(x+shape.X)),Math.Max(bottom,Y(y+shape.Y)),
    Math.Min(right,(int)Math.Ceiling((x+shape.X+shape.W-x0)/cell-1e-7)),Math.Min(top,(int)Math.Ceiling((y+shape.Y+shape.D-y0)/cell-1e-7)));
   return Count(left,bottom,right,top)>inside;
  }
  var orderedAssets=assets.ToList();
  bool mixedSizes=reserveSizes&&assets.Select(a=>(a.Width,a.Depth)).Distinct().Skip(1).Any();
  var used=new Dictionary<string,int>();
  void RankAssets(){orderedAssets=assets.OrderBy(a=>used.TryGetValue(a.Id,out var n)?n:0).ThenByDescending(a=>a.Width*a.Depth).ThenBy(a=>a.Id,StringComparer.Ordinal).ToList();}
  if(mixedSizes)RankAssets();
  var crossOptions=!o.GenerateSurfaceParking?new[]{false}:o.ParkingOrientation==2?new[]{true,false}:new[]{false,true};
  var entranceOptions=o.ContinuousFrontage&&o.WideParkingAisle>o.ParkingAisle?new[]{true,false}:new[]{false};
  foreach(var phase in mixedSizes&&o.EdgeInfill?new[]{"seed","ring","u","pair","single"}:new[]{"ring","u","pair","single"}){
   bool seed=phase=="seed";string kind=seed?"single":phase;
   if(o.BuildingPattern!=0&&(kind=="ring"||kind=="u"))continue;
   if(kind=="ring"&&(!o.AllowRingCourts||!o.MixedDirections)||kind=="u"&&(!o.AllowOpenCourts||!o.MixedDirections)||kind=="pair"&&!o.AllowParallelPairs||kind=="single"&&!o.EdgeInfill)continue;
   // A single frontage court always has one longitudinal parking row.
   var parkingDirections=kind=="single"&&o.ContinuousFrontage?new[]{false}:crossOptions;
   var housingTimer=o.Timing!=null?System.Diagnostics.Stopwatch.StartNew():null;
   int stride=Math.Max(1,(int)Math.Ceiling(o.SearchStep/cell));
   for(int iy=1;iy<ny-1&&!HousingFull();iy+=stride)for(int ix=1;ix<nx-1&&!HousingFull();ix+=stride){
    o.Cancellation.ThrowIfCancellationRequested();
    if(seed&&used.Count==orderedAssets.Count)break;
    if(kind=="ring"&&plan.Courts.Count(c=>c.Pattern=="ring")>=o.MaxRingCourts)break;
    if(map[ix,iy]!=Green||edgeGreen[ix,iy])continue;bool placed=false;spacingAtAnchor.Clear();
    bool preferVertical=(o.MixedDirections||o.BuildingPattern==3)&&verticalBars<existingBars.Count*o.VerticalPreference;
    bool seedMissing=mixedSizes&&used.Count<orderedAssets.Count;
    foreach(var a in seedMissing?(IEnumerable<Asset>)orderedAssets:AssetChoice.Shuffle(assets,assetRandom)){
    if(seed&&used.ContainsKey(a.Id))continue;
    var cap=seedMissing&&(kind=="ring"||kind=="u")?orderedAssets.FirstOrDefault(other=>other.Id!=a.Id&&!used.ContainsKey(other.Id))??a:assets[assetRandom.Next(assets.Count)];
    foreach(bool rotation in new[]{preferVertical,!preferVertical}){
     if(rotation&&!o.MixedDirections&&o.BuildingPattern==0)continue;
      if(o.BuildingPattern==1&&(rotation?a.Depth:a.Width)<(rotation?a.Width:a.Depth)-.01||o.BuildingPattern==2&&(rotation?a.Width:a.Depth)<(rotation?a.Depth:a.Width)-.01)continue;
      if(placed)break;
      int lanes=o.GenerateSurfaceParking&&o.WideSlabParking&&kind!="single"?Math.Min(o.MaxParkingAisles,Math.Max(1,(int)Math.Ceiling(a.Homes*2*o.ParkingPerHome*o.SurfaceParkingShare/(Math.Max(a.Width,a.Depth)/o.BayWidth*2)))):1;
      for(int lane=lanes;lane>=1&&!placed;lane--)for(int reflection=0;reflection<2&&!placed;reflection++)foreach(bool cross in parkingDirections){
       if(placed)break;if(o.ParkingOrientation==1&&cross)continue;
       foreach(bool wideEntrance in entranceOptions){
       if(placed)break;
       var key=(kind,a,cap,lane,reflection,rotation,cross,wideEntrance);
       if(!shapes.TryGetValue(key,out var shape)){
        var template=ParkingCourt(0,0,a,cap,kind,lane,reflection==1,rotation,cross,wideEntrance);
        if(template!=null)shape=(template.Bounds,template.Gate,template.Pieces.Where(p=>p.Kind==0).ToArray());
        shapes[key]=shape;
       }
       double x=x0+ix*cell,y=y0+iy*cell;
       if(shape.Bounds==null||!CandidateRoom(shape.Bounds,x,y))continue;
       if(roadDistance[X(x+shape.Gate.X),Y(y+shape.Gate.Y)]*cell>o.MaxAccessLength||!HasExternalRoad(shape.Bounds,x,y,shape.Gate))continue;
       bool clear=true;
       foreach(var piece in shape.Clearance){
        candidateBuilding.X=x+piece.X;candidateBuilding.Y=y+piece.Y;
        candidateBuilding.W=piece.W;candidateBuilding.D=piece.D;
        if(CoversRoadCentre(candidateBuilding)||!ClearOfMain(candidateBuilding)||!SpacedAtAnchor(candidateBuilding)){clear=false;break;}
       }
       if(!clear)continue;
       // Cache only templates that survived the inexpensive site tests.
       // Beyond the cache budget the ordinary exact routing path still runs.
       if(!reachByBounds.TryGetValue(shape.Bounds,out var reach)&&reachByBounds.Count<1024){
        reach=CourtReach(ParkingCourt(0,0,a,cap,kind,lane,reflection==1,rotation,cross,wideEntrance));
        reachByBounds.Add(shape.Bounds,reach);
       }
       if(!ReachHasRoad(reach,ix,iy))continue;
       var block=ParkingCourt(x,y,a,cap,kind,lane,reflection==1,rotation,cross,wideEntrance);
       if(CommitParkingCourt(block,kind)){
        placed=true;var added=plan.Courts[plan.Courts.Count-1].Buildings;existingBars.AddRange(added);verticalBars+=added.Count(q=>q.D>q.W);
        if(mixedSizes){
         foreach(var lot in plan.Courts[plan.Courts.Count-1].Buildings)used[lot.AssetId]=(used.TryGetValue(lot.AssetId,out var n)?n:0)+1;
         RankAssets();
        }
       }
       }
      }
      if(placed)break;
     }
     if(placed)break;
    }
   }
   if(housingTimer!=null)o.Timing("housing."+phase,housingTimer.ElapsedMilliseconds);
  }
  var infillTimer=o.Timing!=null?System.Diagnostics.Stopwatch.StartNew():null;
  // Disabling the rule explicitly allows unserved residual bars.
  if(!o.ContinuousFrontage&&!o.RequireBuildingRoad&&o.EdgeInfill)UnfrontedInfill();
  else if(o.EdgeInfill)FrontageHousing(false);
  if(infillTimer!=null)o.Timing("housing.infill",infillTimer.ElapsedMilliseconds);
 }
}
}

