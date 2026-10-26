using System;
using System.Collections.Generic;
using System.Linq;

namespace ApartmentPlan {
// Templates describe the user's reference: parallel residential bars with a
// parking court BETWEEN them. The grid only packs/routes/coalesces these units.
// It is not used to paint a generic perimeter-road/master-grid layout.
internal sealed partial class ResidentialLayout {
 const int Outside=-1,Green=4,Paved=8,Fixed=10,Reserve=11,CourtGreen=12;
 readonly Plan plan;readonly LayoutOptions o;readonly List<Point> poly;
 readonly int nx,ny;readonly double cell,x0,y0;readonly int[,] map;
 readonly List<Parcel> exact=new List<Parcel>();
 readonly List<Asset> assets;readonly Parcel entry;
 double floors;int spaces,assetIndex,modules,supplementalRoutes;
 bool BarOrientationAllowed(Parcel p)=>o.BuildingPattern==1?p.W>=p.D-.01:o.BuildingPattern==2?p.D>=p.W-.01:true;
 sealed class Block {
  public bool SupplementalParking;public Parcel Bounds;public List<Parcel> Pieces=new List<Parcel>();
  public List<Parcel> Paving=new List<Parcel>();
  public Dictionary<Parcel,PlanRoute> StreetAxes=new Dictionary<Parcel,PlanRoute>();
  public double MaxConnection=double.MaxValue;public Point Gate;public int Homes;public double FloorArea;public bool LandscapeRemainder;
 }
 ResidentialLayout(Plan p,LayoutOptions options,IList<Asset> selected,IList<ManualZone> zones){
  plan=p;o=options;poly=p.Boundary.Select(p.Local).ToList();assets=selected.Where(a=>a.Width>0&&a.Depth>0&&a.Height>=0).ToList();
  double w=poly.Max(v=>v.X)-poly.Min(v=>v.X),h=poly.Max(v=>v.Y)-poly.Min(v=>v.Y);
  if(p.Area>1000000)throw new PlanLimitException("AP.LimitSiteArea",p.Area,1000000);
  if(w>2048||h>2048)throw new PlanLimitException("AP.LimitSiteExtent",Math.Max(w,h),2048);
  cell=Math.Max(1,Math.Ceiling(Math.Max(w,h)/400));
  x0=Math.Floor(poly.Min(v=>v.X)/cell)*cell-cell;y0=Math.Floor(poly.Min(v=>v.Y)/cell)*cell-cell;
  nx=(int)Math.Ceiling(w/cell)+4;ny=(int)Math.Ceiling(h/cell)+4;map=new int[nx,ny];
  for(int y=0;y<ny;y++)for(int x=0;x<nx;x++)map[x,y]=Layout.Contains(Rect(x,y,1,1),poly)?Green:Outside;
  reserved=new bool[nx,ny];entranceWalkBuffer=new bool[nx,ny];ProtectEdge();ManualZones(zones);entry=EntranceParcel(p.Origin);

 }
 double Snap(double value)=>Math.Ceiling(value/cell)*cell;
 Parcel Rect(int x,int y,int w,int h,int kind=4)=>new Parcel{X=x0+x*cell,Y=y0+y*cell,W=w*cell,D=h*cell,Kind=kind};
 int X(double x)=>(int)Math.Floor((x-x0)/cell);
 int Y(double y)=>(int)Math.Floor((y-y0)/cell);
 void Bounds(Parcel r,out int a,out int b,out int c,out int d){a=Math.Max(0,X(r.X));b=Math.Max(0,Y(r.Y));c=Math.Min(nx,(int)Math.Ceiling((r.X+r.W-x0)/cell-1e-7));d=Math.Min(ny,(int)Math.Ceiling((r.Y+r.D-y0)/cell-1e-7));}
 bool Free(Parcel r){
  if(r.X<x0||r.Y<y0||r.X+r.W>x0+nx*cell||r.Y+r.D>y0+ny*cell)return false;
  Bounds(r,out int a,out int b,out int c,out int d);
  for(int y=b;y<d;y++)for(int x=a;x<c;x++)if(map[x,y]!=Green||reserved[x,y]||edgeGreen[x,y])return false;
  return true;
 }
 void Paint(Parcel r,int kind){Bounds(r,out int a,out int b,out int c,out int d);for(int y=b;y<d;y++)for(int x=a;x<c;x++)if(map[x,y]!=Outside)map[x,y]=kind;}
 Block Template(double x,double y,Asset asset,bool single,bool rotated,bool aligned=false,bool compact=false){
  // Orient the actual selected lot's long side along the common building row.
  // This rotates its footprint; it never stretches a square lot into a slab.
  double length=Math.Max(asset.Width,asset.Depth),depth=Math.Min(asset.Width,asset.Depth);
  double road=Snap(o.Road),walk=Snap(Math.Max(o.Walkway,Math.Min(6,asset.Height*.08)));
  double rear=Snap(Math.Max(2,o.Gap/2)),bay=o.Parking>0?Snap(o.BayDepth):0,aisle=Snap(o.ParkingAisle);
  if(aligned)walk=Snap(Math.Max(o.Walkway,Math.Min(6,assets.Max(a=>a.Height)*.08)));
  double body=Snap(aligned&&!compact?assets.Max(a=>Math.Max(a.Width,a.Depth)):length),d=Snap(aligned?assets.Max(a=>Math.Min(a.Width,a.Depth)):depth),width=body+2*(road+walk);
  // Lot proportions are a planning heuristic, not a prefab type claim.
  // Estimate surface demand separately; never invent underground capacity.
  double usable=Math.Max(0,body-2*o.Walkway),island=Snap(o.ParkingIsland);
  int perRow=usable>=30&&island>0?(int)Math.Floor(Math.Max(0,usable-island)/2/o.BayWidth)*2:(int)Math.Floor(usable/o.BayWidth);
  Func<Asset,int> requested=a=>{
   bool slab=Math.Max(a.Width,a.Depth)/Math.Min(a.Width,a.Depth)>=2&&Math.Max(a.Width,a.Depth)>=32;
   if(!o.WideSlabParking||!slab||bay==0)return 1;
   double demand=Math.Max(0,a.Homes)*(single?1:2)*o.ParkingPerHome*o.SurfaceParkingShare;
   return Math.Min(Math.Max(1,o.MaxParkingAisles),Math.Max(single?1:2,(int)Math.Ceiling(demand/Math.Max(1,perRow*2))));
  };
  int aisleCount=aligned?assets.Max(requested):requested(asset);
  double courtDepth=aisleCount*(2*bay+aisle)+(aisleCount-1)*island;
  double courtY=rear+d+walk,height=courtY+courtDepth+(single?rear:walk+d+rear);
  var block=new Block{Bounds=new Parcel{X=x,Y=y,W=rotated?height:width,D=rotated?width:height}};
  Func<double,double,double,double,int,Parcel> part=(u,v,w,h,k)=>new Parcel{Kind=k,X=x+(rotated?v:u),Y=y+(rotated?u:v),W=rotated?h:w,D=rotated?w:h,ParkingAlongX=!rotated};
  double aisleY=courtY+bay;
  block.Pieces.Add(part(0,0,road,height,1));block.Pieces.Add(part(width-road,0,road,height,1));
  for(int lane=0;lane<aisleCount;lane++){
   double bottom=courtY+lane*(2*bay+aisle+island);
   block.Pieces.Add(part(road,bottom+bay,width-2*road,aisle,1));
   if(lane>0&&island>0)block.Pieces.Add(part(road,bottom-island,width-2*road,island,4));
   if(bay<=0)continue;
   foreach(double v in new[]{bottom,bottom+bay+aisle}){
    int segments=usable>=30&&island>0?2:1;
    double span=Math.Floor((usable-(segments-1)*island)/segments/o.BayWidth)*o.BayWidth;
    if(span<o.BayWidth*4)continue;
    for(int segment=0;segment<segments;segment++){
     double u=road+walk+o.Walkway+segment*(span+island);
     var bays=part(u,v,span,bay,2);bays.ParkingSpaces=(int)Math.Round(span/o.BayWidth);block.Pieces.Add(bays);
     if(segment>0&&island>0)block.Pieces.Add(part(u-island,v,island,bay,4));
    }
   }
  }
  int rows=single?1:2;
  for(int row=0;row<rows;row++){
   double v=row==0?rear+d-depth:courtY+courtDepth+walk;
   var building=part(road+walk,v,length,depth,0);building.AssetId=asset.Id;building.Homes=asset.Homes;
   building.FloorArea=Math.Min(asset.Width*asset.Depth,asset.Footprint>0?asset.Footprint:asset.Width*asset.Depth*o.FootprintRatio)*Math.Max(1,Math.Floor(asset.Height/o.FloorHeight));
   block.FloorArea+=building.FloorArea;block.Homes+=building.Homes;block.Pieces.Add(building);
   block.Pieces.Add(part(road,row==0?0:height-rear,width-2*road,rear,4));
  }
  block.Gate=rotated?new Point(x+aisleY+aisle/2,y+road/2):new Point(x+road/2,y+aisleY+aisle/2);
  return block;
 }
 bool RoadCell(int x,int y)=>x>=0&&y>=0&&x<nx&&y<ny&&map[x,y]==1;
 bool Route(Block block){
  Bounds(block.Bounds,out int a,out int b,out int c,out int d);
  if(block.SupplementalParking){
   if(supplementalRoutes++>=256)return false;
   Func<int,int,bool> join=(x,y)=>map[x,y]==1&&(x<a||y<b||x>=c||y>=d);
   var access=FindRoute(block.Gate,join,o.SupplementalAccessWidth,false,block.MaxConnection);
   if(access==null||access.Count*cell>block.MaxConnection)return false;
   DrawRoute(access,o.SupplementalAccessWidth,"aisleWide");return true;
  }
  double width=o.AccessRoad>0?o.AccessRoad:o.ContinuousFrontage?o.ParkingAisle:o.Road;
  Func<int,int,bool> target=(x,y)=>map[x,y]==1&&(x<a||y<b||x>=c||y>=d);
  // Reserve the wider approach before subsequent courts/parking occupy it.
  if(o.WideParkingAisle>width){
   var wide=FindRoute(block.Gate,target,o.WideParkingAisle,false,block.MaxConnection);
   if(wide!=null&&wide.Count*cell<=block.MaxConnection){DrawRoute(wide,o.WideParkingAisle,"aisleWide");return true;}
  }
  var path=FindRoute(block.Gate,target,width,false,block.MaxConnection);
  if(path==null||path.Count*cell>block.MaxConnection)return false;
  DrawRoute(path,width,"access");return true;
 }

 bool Fits(Block block){
  if(!Layout.Contains(block.Bounds,poly))return false;
  if(block.Pieces.Any(r=>(r.Kind==0||r.Kind==2)&&!ClearOfMain(r)))return false;
  Bounds(block.Bounds,out int a,out int b,out int c,out int d);
  for(int y=b;y<d;y++)for(int x=a;x<c;x++){
   if(reserved[x,y]||edgeGreen[x,y])return false;
   if(mainJunctionBuffer?[x,y]??false){var tile=Rect(x,y,1,1);foreach(var piece in block.Pieces)if(piece.Kind==1&&Layout.Overlap(piece,tile))return false;}
   if(map[x,y]==Green)continue;
   if(map[x,y]!=1)return false;
   double px=x0+(x+.5)*cell,py=y0+(y+.5)*cell;
   bool covered=false;foreach(var piece in block.Pieces)if(piece.Kind==1&&px>=piece.X&&px<piece.X+piece.W&&py>=piece.Y&&py<piece.Y+piece.D){covered=true;break;}if(!covered)return false;
  }
  return true;
 }
 bool Place(Block block){
  if(!Fits(block)||exact.Count(r=>r.Kind==0)+block.Pieces.Count(r=>r.Kind==0)>o.MaxBuildings||o.RespectTargetFar&&floors+block.FloorArea>plan.Area*o.TargetFar/100+.001)return false;
  Bounds(block.Bounds,out int a,out int b,out int c,out int d);var backup=new int[c-a,d-b];
  for(int y=b;y<d;y++)for(int x=a;x<c;x++)backup[x-a,y-b]=map[x,y];
  // Reserve court spacing for packing, but do not present unused space as pavement.
  Paint(block.Bounds,block.LandscapeRemainder?CourtGreen:Paved);
  foreach(var r in block.Paving)Paint(r,Paved);
  foreach(var r in block.Pieces)Paint(r,r.Kind==0||r.Kind==2?Fixed:r.Kind);
  if(!Route(block)){for(int y=b;y<d;y++)for(int x=a;x<c;x++)map[x,y]=backup[x-a,y-b];return false;}
  plan.PavementPads.AddRange(block.Paving.Select(r=>new Parcel{Kind=8,X=r.X,Y=r.Y,W=r.W,D=r.D}));
  // Retain the original street axes before display raster coalescing loses them.
  foreach(var street in block.Pieces.Where(p=>p.Kind==1)){
   if(block.StreetAxes.TryGetValue(street,out var explicitAxis)){plan.ConstructionRoutes.Add(explicitAxis);continue;}
   bool horizontal=street.W>=street.D;
   var axis=new PlanRoute{Use="aisle",Width=horizontal?street.D:street.W};
   axis.Points.Add(new Point(horizontal?street.X:street.X+street.W/2,horizontal?street.Y+street.D/2:street.Y));
   axis.Points.Add(new Point(horizontal?street.X+street.W:street.X+street.W/2,horizontal?street.Y+street.D/2:street.Y+street.D));
   plan.ConstructionRoutes.Add(axis);
  }
  foreach(var r in block.Pieces)if(r.Kind==0||r.Kind==2){exact.Add(r);spaces+=r.ParkingSpaces;}
  floors+=block.FloorArea;plan.Homes+=block.Homes;modules++;return true;
 }
 bool Full=>exact.Count(r=>r.Kind==0)>=o.MaxBuildings||o.RespectTargetFar&&floors>=plan.Area*o.TargetFar/100;
 bool TryBlock(double x,double y,bool single,bool rotated,bool aligned,bool edgeOnly=false,bool compact=false){
  if(Full)return false;
  for(int n=0;n<assets.Count;n++){
   var asset=assets[(assetIndex+n)%assets.Count];var block=Template(x,y,asset,single,rotated,aligned,compact);
   if(!Fits(block))continue;
   if(edgeOnly){var expanded=new Parcel{X=block.Bounds.X-8,Y=block.Bounds.Y-8,W=block.Bounds.W+16,D=block.Bounds.D+16};if(Layout.Contains(expanded,poly))continue;}
   if(!Place(block))continue;assetIndex=(assetIndex+n+1)%assets.Count;return true;
  }
  return false;
 }
 void Pack(){
  if(assets.Count==0)return;
  if(!o.AlignBlocks){
   for(int pass=0;pass<4&&!Full;pass++)for(int y=1;y<ny-1&&!Full;y+=2)for(int x=1;x<nx-1&&!Full;x+=2)
    if(map[x,y]==Green)TryBlock(x0+x*cell,y0+y*cell,pass>=2,pass%2==1,false);
   return;
  }
  // One site-wide grid, based on selected lot dimensions, aligns facing
  // building fronts and parking aisles across adjacent blocks. Search a few
  // global offsets; never shift each interior block independently to fit.
  var standard=Template(0,0,assets[0],false,false,true);
  double pitchX=standard.Bounds.W-Snap(o.Road),pitchY=standard.Bounds.D;
  double baseX=x0+cell,baseY=y0+cell,best=-1,originX=baseX,originY=baseY;
  for(int sy=0;sy<4;sy++)for(int sx=0;sx<4;sx++){
   double ox=baseX+Snap(pitchX*sx/4),oy=baseY+Snap(pitchY*sy/4),score=0;
   for(double y=oy-pitchY;y<y0+ny*cell;y+=pitchY)for(double x=ox-pitchX;x<x0+nx*cell;x+=pitchX)
    if(Free(new Parcel{X=x,Y=y,W=standard.Bounds.W,D=pitchY}))score++;
   if(score>best){best=score;originX=ox;originY=oy;}
  }
  var rows=new List<double>();for(double y=originY-pitchY;y<y0+ny*cell;y+=pitchY)rows.Add(y);
  foreach(double y in rows)for(double x=originX-pitchX;x<x0+nx*cell&&!Full;x+=pitchX)TryBlock(x,y,false,false,true);
  // Tapered edges may use shorter lots, but keep the same row datum first.
  foreach(double y in rows)for(int x=1;x<nx-1&&!Full;x+=2)TryBlock(x0+x*cell,y,false,false,true,false,true);
  foreach(double delta in new[]{-2*cell,2*cell,-4*cell,4*cell})foreach(double row in rows)
   for(int x=1;x<nx-1&&!Full;x+=2)TryBlock(x0+x*cell,row+delta,false,false,true,false,true);
  double rear=Snap(Math.Max(2,o.Gap/2));
  double secondFront=standard.Pieces.Where(r=>r.Kind==0).Max(r=>r.Y)-rear;
  foreach(double row in rows)foreach(double y in new[]{row,row+secondFront})for(int x=1;x<nx-1&&!Full;x+=2)
   TryBlock(x0+x*cell,y,true,false,false);
  // Rotation and an independent baseline are an edge treatment only.
  for(int pass=0;pass<2&&!Full;pass++)for(int y=1;y<ny-1&&!Full;y+=2)for(int x=1;x<nx-1&&!Full;x+=2)
   if(map[x,y]==Green)TryBlock(x0+x*cell,y0+y*cell,pass==1,true,false,true);
 }
 Block EdgeBlock(double x,double y,Asset asset,double length,bool rotated,double roadWidth=0){
  double walk=Snap(o.Walkway),road=Snap(roadWidth>0?roadWidth:o.Road),bay=Snap(o.BayDepth),rear=Snap(Math.Max(2,o.Gap/2));
  bool building=asset!=null;
  double depth=building?Math.Min(asset.Width,asset.Depth):bay;
  double width=building?length+2*walk:length+2*road,height=Snap(depth)+walk+road+rear;
  var block=new Block{Bounds=new Parcel{X=x,Y=y,W=rotated?height:width,D=rotated?width:height},MaxConnection=32};
  Func<double,double,double,double,int,Parcel> part=(u,v,w,h,k)=>new Parcel{Kind=k,X=x+(rotated?v:u),Y=y+(rotated?u:v),W=rotated?h:w,D=rotated?w:h,ParkingAlongX=!rotated};
  double aisleY=rear+Snap(depth)+walk;
  block.Pieces.Add(part(0,aisleY,width,road,1));
  if(building){
   var r=part(walk,rear,length,depth,0);r.AssetId=asset.Id;r.Homes=asset.Homes;r.FloorArea=Math.Min(asset.Width*asset.Depth,asset.Footprint>0?asset.Footprint:asset.Width*asset.Depth*o.FootprintRatio)*Math.Max(1,Math.Floor(asset.Height/o.FloorHeight));
   block.Homes=r.Homes;block.FloorArea=r.FloorArea;block.Pieces.Add(r);
   block.Pieces.Add(part(0,0,width,rear,4));
  }else{
   // Bay fronts meet the aisle directly; the end pads remain turning space.
   var r=part(road,aisleY-bay,length,bay,2);r.ParkingSpaces=(int)Math.Floor(length/o.BayWidth+.001);block.Pieces.Add(r);
  }
  block.Gate=rotated?new Point(x+aisleY+road/2,y+road/2):new Point(x+road/2,y+aisleY+road/2);
  return block;
 }
 bool EdgeSpace(Block block){
  if(!Fits(block))return false;
  var r=block.Bounds;return !Layout.Contains(new Parcel{X=r.X-12,Y=r.Y-12,W=r.W+24,D=r.D+24},poly);
 }
 void Infill(){
  if(!o.EdgeInfill||assets.Count==0)return;
  // Reuse existing building front/side datums. Detached infill shares the
  // neighborhood's courts instead of requiring another oversized full block.
  for(int iteration=0;iteration<2&&!Full;iteration++){
   var existing=exact.Where(r=>r.Kind==0&&r.W>=r.D).ToList();
   foreach(var a in assets.OrderBy(a=>a.Width*a.Depth)){
    double length=Math.Max(a.Width,a.Depth),depth=Math.Min(a.Width,a.Depth),walk=Snap(o.Walkway),rear=Snap(Math.Max(2,o.Gap/2));
    var xs=existing.SelectMany(r=>new[]{r.X-walk,r.X+r.W-length-walk}).Distinct().ToList();
    var ys=existing.SelectMany(r=>new[]{r.Y-rear,r.Y+r.D+Math.Max(o.Gap,8)-rear}).Distinct().OrderByDescending(y=>y).ToList();
    foreach(double y in ys)foreach(double x in xs){if(Full)return;var block=EdgeBlock(x,y,a,length,false);if(EdgeSpace(block))Place(block);}
   }
  }
  if(o.Parking<=0)return;
  // Short edge courts follow the common orthogonal axes and must route to
  // the existing network within 32m. Never count landscape-only fill as use.
  int added=0;
  foreach(double wanted in new[]{40.0,25.0,15.0})for(int rotation=0;rotation<2;rotation++)
   for(int y=ny-2;y>0;y-=2)for(int x=1;x<nx-1;x+=2){
    if(added>=24||modules>=100)return;
    if(map[x,y]!=Green)continue;
    double length=Math.Floor(wanted/o.BayWidth)*o.BayWidth;
    var block=EdgeBlock(x0+x*cell,y0+y*cell,null,length,rotation==1);
    if(EdgeSpace(block)&&Place(block))added++;
   }
 }
 Parcel FindOpen(double w,double h,Point target,bool requireRoad){
  Parcel best=null;double cost=double.MaxValue;
  for(int y=1;y<ny-1;y+=2)for(int x=1;x<nx-1;x+=2){var r=new Parcel{X=x0+x*cell,Y=y0+y*cell,W=Snap(w),D=Snap(h)};if(!Free(r))continue;
   if(requireRoad){var near=new Parcel{X=r.X-6,Y=r.Y-6,W=r.W+12,D=r.D+12};Bounds(near,out int a,out int b,out int c,out int d);bool road=false;for(int yy=b;yy<d&&!road;yy++)for(int xx=a;xx<c;xx++)if(map[xx,yy]==1){road=true;break;}if(!road)continue;}
   double dist=(new Point(r.X+r.W/2,r.Y+r.D/2)-target).Length;if(dist<cost){cost=dist;best=r;}
  }
  return best;
 }
 void ReserveAmenities(){
  // A compact playground is reserved first so maximum packing cannot erase it.
  if(o.Play>0){var center=new Point(poly.Average(v=>v.X),poly.Average(v=>v.Y));var r=FindOpen(20,16,center,false);if(r!=null)Paint(r,3);}
  if(o.Facility>0){var r=FindOpen(18,12,new Point(o.Road+12,o.Entrance+8),true);if(r!=null)Paint(r,5);}
 }
 void Finish(){
  PaveEnclosedParking();
  // The only residual fill is landscape. It is accounted separately from
  // developed blocks; a high fill ratio is not evidence of useful packing.
  // Preserve the part of each road cell beyond the exact entrance apron.
  // Marking a fractional apron as Fixed used to erase the whole end cell,
  // leaving a half-metre break between the entrance and every parking aisle.
  Bounds(entry,out int ea,out int eb,out int ec,out int ed);
  for(int y=eb;y<ed;y++)for(int x=ea;x<ec;x++)if(map[x,y]==1){
   var tile=Rect(x,y,1,1,1);double left=Math.Max(tile.X,entry.X),bottom=Math.Max(tile.Y,entry.Y),right=Math.Min(tile.X+tile.W,entry.X+entry.W),top=Math.Min(tile.Y+tile.D,entry.Y+entry.D);
   if(right<=left||top<=bottom)continue;
   foreach(var rem in ReplacementRules.Vacated(tile,new Parcel{X=left,Y=bottom,W=right-left,D=top-bottom})){rem.Kind=1;rem.InvisibleRoad=mainSum[x+1,y+1]-mainSum[x,y+1]-mainSum[x+1,y]+mainSum[x,y]==0;exact.Add(rem);}
  }
  Paint(entry,Fixed);plan.Parcels.Insert(0,entry);
  var active=new Dictionary<string,Parcel>();
  for(int y=0;y<ny;y++){
   var next=new Dictionary<string,Parcel>();
   for(int x=0;x<nx;){int kind=map[x,y];if(kind==Outside||kind==Fixed||kind==Reserve){x++;continue;}bool invisible=kind==1&&mainSum[x+1,y+1]-mainSum[x,y+1]-mainSum[x+1,y]+mainSum[x,y]==0;int begin=x++;while(x<nx&&map[x,y]==kind&&(kind!=1||(mainSum[x+1,y+1]-mainSum[x,y+1]-mainSum[x+1,y]+mainSum[x,y]==0)==invisible)&&(x-begin+1)*cell<990)x++;
    string key=kind+":"+invisible+":"+begin+":"+x;
    if(active.TryGetValue(key,out var r))r.D+=cell;else{r=Rect(begin,y,x-begin,1,kind==CourtGreen?Green:kind);r.InvisibleRoad=invisible;plan.Parcels.Add(r);}next[key]=r;
   }active=next;
  }
  plan.Parcels.AddRange(exact);
  FindGrassPockets();
  plan.RequestedParcels=plan.Parcels.Count;plan.ParcelLimit=o.MaxParcels;
  if(plan.Parcels.Count>o.MaxParcels){plan.Limited=true;int excess=plan.Parcels.Count-o.MaxParcels;foreach(var r in plan.Parcels.Where(r=>r.Kind==4).OrderBy(r=>r.Area).Take(excess).ToList())plan.Parcels.Remove(r);}
  if(plan.Parcels.Count>o.MaxParcels)throw new PlanLimitException("AP.LimitParcels",plan.RequestedParcels,o.MaxParcels);
  plan.Far=floors/plan.Area*100;double[] targets={0,0,o.Parking,0,o.Park,0};
  for(int k=2;k<=5;k++)plan.Shortfall[k]=Math.Max(0,plan.Area*targets[k]-plan.Parcels.Where(r=>r.Kind==k).Sum(r=>r.Area));
  plan.Shortfall[0]=Math.Max(0,plan.Homes*o.ParkingPerHome-spaces);
 }
 // Flood non-road cells. Only components cut off from the site boundary by
 // roads and containing parking become pavement; manual reservations survive.
 void PaveEnclosedParking(){
  var solid=new bool[nx,ny];foreach(var lot in exact.Where(p=>p.Kind==0||p.Kind==5||p.Kind==6)){Bounds(lot,out int a,out int b,out int c,out int d);for(int y=b;y<d;y++)for(int x=a;x<c;x++)solid[x,y]=true;}
  var parking=new bool[nx,ny];foreach(var bay in exact.Where(p=>p.Kind==2)){Bounds(bay,out int a,out int b,out int c,out int d);for(int y=b;y<d;y++)for(int x=a;x<c;x++)parking[x,y]=true;}
  var seen=new bool[nx,ny];int[] dx={1,0,-1,0},dy={0,1,0,-1};
  for(int y=0;y<ny;y++)for(int x=0;x<nx;x++){
   if(seen[x,y]||solid[x,y]||reserved[x,y]||map[x,y]==Walk||map[x,y]==1||map[x,y]==Outside)continue;
   var cells=new List<int>{y*nx+x};seen[x,y]=true;bool open=false,hasParking=false;
   for(int at=0;at<cells.Count;at++){int u=cells[at]%nx,v=cells[at]/nx;hasParking|=parking[u,v];
    for(int k=0;k<4;k++){int xx=u+dx[k],yy=v+dy[k];if(xx<0||yy<0||xx>=nx||yy>=ny||map[xx,yy]==Outside){open=true;continue;}if(solid[xx,yy]||reserved[xx,yy]||map[xx,yy]==Walk||map[xx,yy]==1||seen[xx,yy])continue;seen[xx,yy]=true;cells.Add(yy*nx+xx);}
   }
   if(open||!hasParking)continue;
   foreach(int n in cells){int u=n%nx,v=n/nx;if(!reserved[u,v]&&!edgeGreen[u,v]&&(map[u,v]==Green||map[u,v]==CourtGreen))map[u,v]=Paved;}
  }
 }
 internal static Plan Generate(IList<Point> boundary,Point entrance,IList<Asset> assets,LayoutOptions o,IList<Parcel> ramps)=>Generate(boundary,new[]{entrance},assets,o,ramps,null);
 internal static Plan Generate(IList<Point> boundary,IList<Point> entrances,IList<Asset> assets,LayoutOptions o,IList<Parcel> ramps,IList<ManualZone> zones,IList<ManualRoad> roads=null){
  if(!Layout.Valid(boundary)||entrances==null||entrances.Count==0||entrances.Count>16)throw new ArgumentException("Boundary");
  var p=Layout.Frame(boundary,entrances[0]);var timer=System.Diagnostics.Stopwatch.StartNew();var solver=new ResidentialLayout(p,o,assets,zones);
  void Stage(string name){o.Cancellation.ThrowIfCancellationRequested();p.LayoutTimings[name]=timer.ElapsedMilliseconds;timer.Restart();}
  Stage("grid");
  if(ramps!=null)foreach(var r in ramps){if(!Layout.Contains(r,solver.poly)||!solver.Free(r))throw new ArgumentException("RampFit");solver.Paint(r,Fixed);solver.exact.Add(r);}
  for(int i=1;i<entrances.Count;i++)solver.EntranceParcel(entrances[i]);
  solver.MainRoads();solver.AddManualRoads(roads);solver.ProtectMainRoad();Stage("mainRoad");solver.ParkingCourtHousing();Stage("housing");solver.AddParkingAisles();solver.SharedFrontageParking();solver.JoinParkingGaps();solver.CloseParkingLoops();solver.RepairFrontageConnections();solver.PreferWideAccess();solver.ReclaimWideParking();solver.FillPavedParkingCourts();solver.FillOpenSpaceParking();Stage("parking");if(o.PedestrianNetwork){solver.OpenSpaceWalks();solver.ConnectWalks();solver.RoundWalkCorners();}Stage("walks");solver.Finish();ParkingTraffic.Validate(p,o);Stage("finish");
  p.Diagnostics="entrances="+entrances.Count+" routes="+p.Routes.Count+" unreachable="+p.UnreachableAccess.Count+" parkingAccessFailures="+p.ParkingAccessFailures;return p;
 }
}
}


