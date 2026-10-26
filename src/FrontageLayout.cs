using System;
using System.Collections.Generic;
using System.Linq;

namespace ApartmentPlan {
public static class FrontageRules {
 public static bool Spaced(Parcel a,Parcel b,double parallelGap,double endGap){
  bool ax=a.W>=a.D,bx=b.W>=b.D;
  double ox=Math.Min(a.X+a.W,b.X+b.W)-Math.Max(a.X,b.X);
  double oy=Math.Min(a.Y+a.D,b.Y+b.D)-Math.Max(a.Y,b.Y);
  if(ax==bx&&(ax?ox:oy)>.001){
   double distance=ax?Math.Max(b.Y-a.Y-a.D,a.Y-b.Y-b.D):Math.Max(b.X-a.X-a.W,a.X-b.X-b.W);
   return distance>=parallelGap-.001;
  }
  return !Layout.Overlap(a,b,endGap);
 }
}
internal sealed partial class ResidentialLayout {
 int[,] mainSum;bool[,] mainJunctionBuffer;
 void ProtectMainRoad(){
  // Capture BEFORE frontage roads. Intersections never reclassify a main road
  // as a parking aisle. Entrances and diagonal aprons are included.
  mainJunctionBuffer=new bool[nx,ny];
  var protection=MainJunctionClearance.Zones(plan);
  foreach(var z in protection){var center=plan.Local(z.Center);double radius=z.Radius;
   for(int y=Math.Max(0,Y(center.Y-radius));y<Math.Min(ny,Y(center.Y+radius)+1);y++)
    for(int x=Math.Max(0,X(center.X-radius));x<Math.Min(nx,X(center.X+radius)+1);x++){
     var q=new Point(Math.Max(x0+x*cell,Math.Min(x0+(x+1)*cell,center.X)),Math.Max(y0+y*cell,Math.Min(y0+(y+1)*cell,center.Y)));
     if((q-center).Length<=radius)mainJunctionBuffer[x,y]=true;
    }
  }
  mainSum=new int[nx+1,ny+1];
  for(int y=0;y<ny;y++)for(int x=0;x<nx;x++)mainSum[x+1,y+1]=(map[x,y]==1?1:0)+mainSum[x,y+1]+mainSum[x+1,y]-mainSum[x,y];
  for(int y=0;y<ny;y++)for(int x=0;x<nx;){if(map[x,y]!=1){x++;continue;}int begin=x++;while(x<nx&&map[x,y]==1)x++;plan.MainRoadArea.Add(Rect(begin,y,x-begin,1,1));}
 }
 bool ClearOfMain(Parcel r){
  double gap=o.MainParkingSetback;
  var expanded=new Parcel{X=r.X-gap,Y=r.Y-gap,W=r.W+2*gap,D=r.D+2*gap};
  Bounds(expanded,out int a,out int b,out int c,out int d);
  return mainSum[c,d]-mainSum[a,d]-mainSum[c,b]+mainSum[a,b]==0;
 }
 Block FrontageBlock(double x,double row,Asset asset,double depth,double gap,bool positive,bool rotated){
  double length=o.ContinuousFrontage?asset.Width:Math.Max(asset.Width,asset.Depth),lotDepth=o.ContinuousFrontage?asset.Depth:Math.Min(asset.Width,asset.Depth),road=Snap(o.ContinuousFrontage?o.ParkingAisle+2*o.ParkingPavingMargin:o.Road);
  if(o.ContinuousFrontage){depth=lotDepth;gap=road;}
  double by=positive?row+depth-lotDepth:row;
  double ry=positive?row+depth+(gap-road)/2:row-(gap+road)/2;
  double low=Math.Min(by,ry),high=Math.Max(by+lotDepth,ry+road);
  Func<double,double,double,double,int,Parcel> part=(u,v,w,h,k)=>new Parcel{Kind=k,X=rotated?v:u,Y=rotated?u:v,W=rotated?h:w,D=rotated?w:h};
  var building=part(x,by,length,lotDepth,0);building.AssetId=asset.Id;building.Homes=asset.Homes;
  building.FloorArea=Math.Min(asset.Width*asset.Depth,asset.Footprint>0?asset.Footprint:asset.Width*asset.Depth*o.FootprintRatio)*Math.Max(1,Math.Floor(asset.Height/o.FloorHeight));
  var street=part(x,ry,length,road,1);building.Frontage=street;
  // Explicit planning front, not inferred from the nearest unrelated road.
  building.Access=rotated?new Point(positive?by+lotDepth:by,x+length/2):new Point(x+length/2,positive?by+lotDepth:by);
  var block=new Block{LandscapeRemainder=true,MaxConnection=o.MaxAccessLength,Bounds=part(x,low,length,high-low,8),Gate=new Point(street.X+street.W/2,street.Y+street.D/2),Homes=building.Homes,FloorArea=building.FloorArea};
  block.Pieces.Add(building);block.Pieces.Add(street);if(o.ContinuousFrontage){block.StreetAxes[street]=new PlanRoute{Use="aisle",Width=road,Points=new List<Point>{new Point(rotated?street.X+street.W/2:street.X,rotated?street.Y:street.Y+street.D/2),new Point(rotated?street.X+street.W/2:street.X+street.W,rotated?street.Y+street.D:street.Y+street.D/2)}};}return block;
 }
 bool PlaceFrontage(Block block,bool edgeOnly=false){
  var building=block.Pieces[0];
  if(exact.Any(r=>r.Kind==0&&!FrontageRules.Spaced(building,r,o.ParallelGap,o.Gap)))return false;
  if(edgeOnly&&Layout.Contains(new Parcel{X=building.X-12,Y=building.Y-12,W=building.W+24,D=building.D+24},poly))return false;
  return Place(block);
 }
 // A court is three independent lots, not a U-shaped prefab. Its two bars
 // face a shared open space; the perpendicular end bar leaves the other end
 // open. Reflect the whole arrangement, including its fronts and access.
 List<Block> CourtGroup(double x,double y,Asset top,Asset bottom,Asset cap,bool mirror,bool rotate,int variant){
  double d=Math.Min(cap.Width,cap.Depth),length=Math.Max(cap.Width,cap.Depth);
  double gap=Snap(Math.Max(o.ParallelGap,o.Road));
  double depth=Math.Max(Math.Min(top.Width,top.Depth),Math.Min(bottom.Width,bottom.Depth));
  double end=Snap(Math.Max(o.Gap,(gap-o.Road)/2));
  double start=d+end,step=Snap(variant%2==0?4:6);
  double lower=depth+2*end+length;
  // Longitudinal offsets leave road-to-road overlap at the cap and create
  // different end lines without reducing required parallel separation.
  var result=new List<Block>{
   FrontageBlock(start,0,top,depth,gap,true,false),
   FrontageBlock(start+step,lower,bottom,depth,gap,false,false),
   FrontageBlock(depth+end,0,cap,d,gap,true,true)
  };
  double width=result.Max(b=>b.Bounds.X+b.Bounds.W);
  Func<Point,Point> point=p=>{double u=mirror?width-p.X:p.X;return rotate?new Point(x+p.Y,y+u):new Point(x+u,y+p.Y);};
  Action<Parcel> rect=r=>{double u=mirror?width-r.X-r.W:r.X,v=r.Y,w=r.W,h=r.D;r.X=x+(rotate?v:u);r.Y=y+(rotate?u:v);r.W=rotate?h:w;r.D=rotate?w:h;};
  foreach(var block in result){rect(block.Bounds);block.Gate=point(block.Gate);var building=block.Pieces[0];building.Access=point(building.Access.Value);foreach(var piece in block.Pieces)rect(piece);}
  return result;
 }
 bool PlaceCourt(List<Block> blocks){
  if(modules+blocks.Count>100||Full)return false;
  var buildings=blocks.Select(b=>b.Pieces[0]).ToList();
  for(int i=0;i<buildings.Count;i++){
   if(exact.Any(r=>r.Kind==0&&!FrontageRules.Spaced(buildings[i],r,o.ParallelGap,o.Gap)))return false;
   for(int j=0;j<i;j++)if(!FrontageRules.Spaced(buildings[i],buildings[j],o.ParallelGap,o.Gap))return false;
  }
  if(blocks.Any(b=>!Fits(b)))return false;
  // Stage all footprints before routing; a connection cannot run through a
  // member that will be placed later. Rollback includes routing outside them.
  var before=(int[,])map.Clone();int routes=plan.Routes.Count;
  bool ok=true;
  foreach(var block in blocks){
   if(!Fits(block)){ok=false;break;}
   Paint(block.Bounds,block.LandscapeRemainder?CourtGreen:Paved);foreach(var r in block.Pieces)Paint(r,r.Kind==0?Fixed:r.Kind);
  }
  if(ok)foreach(var block in blocks){
   var connected=new bool[nx,ny];var queue=new List<int>();
   for(int yy=0;yy<ny;yy++)for(int xx=0;xx<nx;xx++)if(before[xx,yy]==1){connected[xx,yy]=true;queue.Add(yy*nx+xx);}
   int[] dx={-1,1,0,0},dy={0,0,-1,1};
   for(int head=0;head<queue.Count;head++){int v=queue[head],xx=v%nx,yy=v/nx;for(int k=0;k<4;k++){int a=xx+dx[k],b=yy+dy[k];if(a<0||b<0||a>=nx||b>=ny||connected[a,b]||map[a,b]!=1)continue;connected[a,b]=true;queue.Add(b*nx+a);}}
   var path=FindRoute(block.Gate,(a,b)=>connected[a,b],o.Road);
   if(path==null){ok=false;break;}DrawRoute(path,o.Road,"access");
  }
  if(!ok){Array.Copy(before,map,map.Length);if(plan.Routes.Count>routes)plan.Routes.RemoveRange(routes,plan.Routes.Count-routes);return false;}
  foreach(var block in blocks){exact.Add(block.Pieces[0]);floors+=block.FloorArea;plan.Homes+=block.Homes;modules++;}
  return true;
 }
 void CourtyardHousing(){
  if(!o.MixedDirections||o.BuildingPattern!=0)return;
  int group=0;
  var caps=assets.OrderBy(a=>Math.Max(a.Width,a.Depth)).ToList();
  for(int iy=1;iy<ny-1&&!Full;iy+=4)for(int ix=1;ix<nx-1&&!Full;ix+=4){
   if(map[ix,iy]!=Green)continue;bool placed=false;
   for(int trial=0;trial<4&&!placed;trial++)foreach(var cap in caps){
    var top=assets[group%assets.Count];var bottom=assets[(group+1)%assets.Count];
    bool mirror=(group+trial)%2==1,rotate=trial>=2;
    var blocks=CourtGroup(x0+ix*cell,y0+iy*cell,top,bottom,cap,mirror,rotate,group);
    if(!PlaceCourt(blocks))continue;
    double left=blocks.Min(b=>b.Bounds.X),low=blocks.Min(b=>b.Bounds.Y);
    var court=new CourtGroup{Id=group,Pattern=(mirror?"open-left":"open-right")+(rotate?"-rotated":""),Gate=blocks[0].Gate,Bounds=new Parcel{X=left,Y=low,W=blocks.Max(b=>b.Bounds.X+b.Bounds.W)-left,D=blocks.Max(b=>b.Bounds.Y+b.Bounds.D)-low}};
    foreach(var b in blocks){b.Pieces[0].ClusterId=group;court.Buildings.Add(b.Pieces[0]);court.AccessPoints.Add(b.Pieces[0].Access.Value);}
    plan.Courts.Add(court);
    group++;placed=true;break;
   }
  }
 }
 void FrontageHousing(bool includeCourts=true){
  if(assets.Count==0)return;
  if(includeCourts)CourtyardHousing();
  double gap=Snap(Math.Max(o.ParallelGap,o.Road));
  // Fill residual pockets from local candidates. A modest orientation balance
  // selects trial order only: constraints decide fit, not a horizontal/vertical
  // zoning band. Independent end lines follow each court and available street.
  for(int pass=0;pass<(o.EdgeInfill?2:1);pass++){
   int stride=pass==0?4:2;
   for(int iy=1;iy<ny-1&&!Full;iy+=stride)for(int ix=1;ix<nx-1&&!Full;ix+=stride){
    if(map[ix,iy]!=Green)continue;
    int total=exact.Count(r=>r.Kind==0),vertical=exact.Count(r=>r.Kind==0&&r.D>r.W);
    bool preferVertical=(o.MixedDirections||o.BuildingPattern==3)&&vertical<total*.34;
    bool placed=false;
    foreach(bool rotated in new[]{preferVertical,!preferVertical}){
     if(rotated&&!o.MixedDirections&&o.BuildingPattern==0)continue;
     for(int side=0;side<2&&!placed;side++)for(int i=0;i<assets.Count;i++){
      int index=(assetIndex+i)%assets.Count;var a=assets[index];
      double x=x0+ix*cell,y=y0+iy*cell;
      var block=FrontageBlock(rotated?y:x,rotated?x:y,a,Math.Min(a.Width,a.Depth),gap,(total+side)%2==0,rotated);
      if(block.Pieces.Any(p=>p.Kind==0&&!BarOrientationAllowed(p)))continue;
      if(!PlaceFrontage(block))continue;
      assetIndex=(index+1)%assets.Count;placed=true;break;
     }
     if(placed)break;
    }
   }
  }
 }

 bool KindRectangle(Parcel r,int kind){
  if(!Layout.Contains(r,poly))return false;Bounds(r,out int a,out int b,out int c,out int d);
  for(int y=b;y<d;y++)for(int x=a;x<c;x++)if(map[x,y]!=kind)return false;return true;
 }
 bool ParkingSpace(Parcel r){
  if(r.X<x0||r.Y<y0||r.X+r.W>x0+nx*cell||r.Y+r.D>y0+ny*cell)return false;
  Bounds(r,out int a,out int b0,out int c,out int d);
  for(int y=b0;y<d;y++)for(int x=a;x<c;x++)if(reserved[x,y]||edgeGreen[x,y]||map[x,y]!=Green&&map[x,y]!=Paved&&(map[x,y]!=CourtGreen||o.ParkingIsland>0))return false;
  if(!Layout.Contains(r,poly)||!ClearOfMain(r))return false;
  // Infill must not reintroduce a full-width walk along every building either.
  foreach(var lot in exact.Where(p=>p.Kind==0)){
   if(Layout.Overlap(lot,r))return false;
   if(!o.PedestrianNetwork)continue;var asset=assets.Find(a=>a.Id==lot.AssetId);if(asset==null)continue;
   foreach(var access in asset.PedestrianAccess){
    double half=Snap(o.Walkway)/2+cell;var target=BuildingPlacement.AccessTarget(lot,asset,access,half);
    if(Layout.Overlap(r,new Parcel{X=target.X-half,Y=target.Y-half,W=2*half,D=2*half}))return false;
   }
  }
  return true;
 }
 bool ParkingStreetSpace(Parcel r){
  if(r.X<x0||r.Y<y0||r.X+r.W>x0+nx*cell||r.Y+r.D>y0+ny*cell)return false;
  Bounds(r,out int a,out int b,out int c,out int d);
  for(int y=b;y<d;y++)for(int x=a;x<c;x++)if(reserved[x,y]||edgeGreen[x,y]||
   map[x,y]!=1&&map[x,y]!=Green&&map[x,y]!=Paved&&(map[x,y]!=CourtGreen||o.ParkingIsland>0))return false;
  return Layout.Contains(r,poly)&&ClearOfMain(r)&&!exact.Any(p=>Layout.Overlap(p,r));
 }
 bool TryParkingPair(bool horizontal,double along,double run,double across,double aisle){
  double bay=Snap(o.BayDepth),end=o.ParkingEndClearance;
  Parcel Part(double u,double v,double w,double h,int kind=8)=>new Parcel{Kind=kind,X=horizontal?u:v,Y=horizontal?v:u,W=horizontal?w:h,D=horizontal?h:w,ParkingAlongX=horizontal};
  var rows=new[]{Part(along,across,run,bay,2),Part(along,across+bay+aisle,run,bay,2)};
  if(rows.Any(r=>!ParkingSpace(r)))return false;
  // Raster-aligned road limits encompass the exact, potentially fractional,
  // neighbouring stall endpoints. Only the road can overlap an existing road.
  double low=Math.Floor((along-end)/cell)*cell,high=Snap(along+run+end),apronLow=low,apronHigh=high;
  if(!ParkingStreetSpace(Part(low,across+bay,high-low,aisle)))return false;
  bool Connect(ref double limit,bool first){
   double initial=limit;
   // The end-clearance extension can already terminate INSIDE a transverse
   // street; requiring another full-width strip beyond it skips that junction.
   var mouth=Part(first?limit:limit-cell,across+bay,cell,aisle);
   if(KindRectangle(mouth,1)&&ParkingStreetSpace(mouth))return true;
   for(double distance=0;distance<=o.MaxAccessLength;distance+=cell){
    double at=initial+(first?-distance:distance);
    var probe=Part(first?at-aisle:at,across+bay,aisle,aisle);
    var corridor=Part(first?at:initial,across+bay,distance+cell,aisle);
    if(!ParkingStreetSpace(corridor))break;
    if(!KindRectangle(probe,1))continue;
    // Keep the connection itself subject to reservations and main setback.
    if(!ParkingStreetSpace(probe))break;
    limit=at;return true;
   }
   return false;
  }
  bool first=Connect(ref low,true),last=Connect(ref high,false);
  if(!first&&!last)return false;
  var street=Part(low,across+bay,high-low,aisle,1);Paint(street,1);
  foreach(var row in rows){
   row.ParkingSpaces=(int)Math.Round(run/o.BayWidth);
   // Only paint free row-end ground. Existing transverse streets and native
   // pedestrian openings must remain available at the mouth of the aisle.
   var apron=Part(apronLow,horizontal?row.Y:row.X,apronHigh-apronLow,bay);
   Bounds(apron,out int a,out int b,out int c,out int d);
   for(int y=b;y<d;y++)for(int x=a;x<c;x++)if(!reserved[x,y]&&!edgeGreen[x,y]&&(map[x,y]==Green||map[x,y]==CourtGreen&&o.ParkingIsland<=0))map[x,y]=Paved;
   Paint(row,Fixed);exact.Add(row);spaces+=row.ParkingSpaces;
  }
  plan.ConstructionRoutes.Add(new PlanRoute{Use="aisle",Width=aisle,Points=new List<Point>{
   new Point(horizontal?low:across+bay+aisle/2,horizontal?across+bay+aisle/2:low),
   new Point(horizontal?high:across+bay+aisle/2,horizontal?across+bay+aisle/2:high)}});
  return true;
 }
 // Match neighbouring rows before trying shorter free-space modules. Both
 // ends seek an existing street, including beyond the end of the parking row.
 void AddParkingAisles(){
  if(!o.ContinuousFrontage||o.Parking<=0)return;
  var original=exact.Where(p=>p.Kind==2).ToArray();
  double bay=Snap(o.BayDepth),end=Snap(o.ParkingEndClearance);
  double target=Math.Ceiling(plan.Homes*o.ParkingPerHome*o.SurfaceParkingShare);
  var widths=new[]{Snap(o.ParkingAisle+2*o.ParkingPavingMargin),Snap(o.ParkingAisle)}.Distinct().ToArray();
  foreach(var row in original.OrderByDescending(p=>p.ParkingSpaces))foreach(double aisle in widths){
   bool horizontal=row.ParkingAlongX;double start=horizontal?row.X:row.Y,run=horizontal?row.W:row.D;
   double across=horizontal?row.Y:row.X,width=2*bay+aisle;
   if(row.ParkingSpaces<o.MinFrontParkingSpaces)continue;
   // Near a row first, then up to one module farther into its courtyard.
   for(double offset=0;offset<=width;offset+=cell)foreach(bool positive in new[]{true,false}){
    if(spaces>=target)return;
    double v=positive?Snap(across+bay)+offset:Math.Floor((across-width)/cell)*cell-offset;
    TryParkingPair(horizontal,start,run,v,aisle);
   }
  }
  foreach(double aisle in widths)
   foreach(double wanted in new[]{40.0,32.0,24.0,18.0})for(int rotation=0;rotation<2;rotation++)
    for(int y=1;y<ny-1;y++)for(int x=1;x<nx-1;x++){
     if(x==1)o.Cancellation.ThrowIfCancellationRequested();
     if(spaces>=target)return;
     if(map[x,y]!=Green&&map[x,y]!=CourtGreen&&map[x,y]!=Paved)continue;
     double length=Snap(wanted),width=2*bay+aisle;
     Parcel Part(double u,double v,double w,double h,int kind=8)=>new Parcel{Kind=kind,X=x0+x*cell+(rotation==0?u:v),Y=y0+y*cell+(rotation==0?v:u),W=rotation==0?w:h,D=rotation==0?h:w,ParkingAlongX=rotation==0};
     var area=Part(0,0,length,width);
     if(!original.Any(p=>Layout.Overlap(p,area,bay+aisle))||!ParkingSpace(area))continue;
     double run=Math.Floor((length-2*end)/o.BayWidth)*o.BayWidth;
     int count=(int)Math.Round(run/o.BayWidth);if(count<o.MinFrontParkingSpaces)continue;
     TryParkingPair(rotation==0,(rotation==0?area.X:area.Y)+end,run,rotation==0?area.Y:area.X,aisle);
    }
 }
 // Replace aligned gaps between stall fragments with a connected internal aisle.
 // Only free ground is consumed; lots, bays, masks and main-road setbacks stay protected.
 void JoinParkingGaps(){
  double width=Snap(o.AccessRoad>0?o.AccessRoad:o.ParkingAisle);
  var rows=exact.Where(r=>r.Kind==2).ToArray();var used=new HashSet<string>();
  foreach(var a in rows)foreach(var b in rows){
   bool h=a.ParkingAlongX;if(h!=b.ParkingAlongX)continue;
   double across=h?a.Y:a.X,depth=h?a.D:a.W;
   if(Math.Abs(across-(h?b.Y:b.X))>.01||Math.Abs(depth-(h?b.D:b.W))>.01)continue;
   double from=h?a.X+a.W:a.Y+a.D,to=h?b.X:b.Y,gap=to-from;
   if(gap<width-.01||gap>width+2*o.BayWidth)continue;
   double along=Math.Floor(((from+to-width)/2)/cell)*cell;if(along<from-.01)along+=cell;if(along+width>to+.01)continue;
   string key=h+":"+along+":"+across;if(!used.Add(key))continue;
   Parcel Strip(double low,double high)=>new Parcel{Kind=1,X=h?along:low,Y=h?low:along,W=h?width:high-low,D=h?high-low:width,InvisibleRoad=true};
   if(!ParkingStreetSpace(Strip(across,across+depth)))continue;
   double lo=double.NaN,hi=double.NaN;
   for(double d=0;d<=o.MaxAccessLength;d+=cell){double edge=Math.Floor(across/cell)*cell-d;var test=Strip(edge,across+depth);if(!ParkingStreetSpace(test))break;if(KindRectangle(Strip(edge,edge+width),1)){lo=edge+width/2;break;}}
   for(double d=0;d<=o.MaxAccessLength;d+=cell){double edge=Snap(across+depth)+d;var test=Strip(across,edge);if(!ParkingStreetSpace(test))break;if(KindRectangle(Strip(edge-width,edge),1)){hi=edge-width/2;break;}}
   if(double.IsNaN(lo)&&double.IsNaN(hi))continue;
   if(double.IsNaN(lo))lo=across+width/2;if(double.IsNaN(hi))hi=across+depth-width/2;if(hi<=lo)continue;
   var street=Strip(lo-width/2,hi+width/2);if(!ParkingStreetSpace(street))continue;Paint(street,1);
   var points=h?new List<Point>{new Point(along+width/2,lo),new Point(along+width/2,hi)}:new List<Point>{new Point(lo,along+width/2),new Point(hi,along+width/2)};
   plan.ConstructionRoutes.Add(new PlanRoute{Use="aisle",Width=width,Points=points});
  }
 }
 void SharedFrontageParking(){
  if(o.Parking<=0)return;
  double target=plan.Homes>0?Math.Ceiling(plan.Homes*o.ParkingPerHome*o.SurfaceParkingShare):plan.Area*o.Parking/o.SpaceArea;
  // Fill already served space after all housing is fixed. No dwelling is lost
  // to a parking quota. Shortfall remains visible; no underground capacity invented.
  foreach(double wanted in new[]{40.0,25.0,15.0,10.0})for(int rotation=0;rotation<2;rotation++)
   for(int y=1;y<ny-1;y++)for(int x=1;x<nx-1;x++){
    if(spaces>=target)return;if(map[x,y]!=Green&&map[x,y]!=Paved&&map[x,y]!=CourtGreen)continue;
    double length=Math.Floor(wanted/o.BayWidth)*o.BayWidth;
    var bay=new Parcel{Kind=2,X=x0+x*cell,Y=y0+y*cell,W=rotation==0?length:o.BayDepth,D=rotation==0?o.BayDepth:length,ParkingAlongX=rotation==0,ParkingSpaces=(int)Math.Round(length/o.BayWidth)};
    if(!ParkingSpace(bay))continue;
    double aisle=o.ParkingAisle;
    var fronts=rotation==0?new[]{new Parcel{X=bay.X,Y=bay.Y-aisle,W=bay.W,D=aisle},new Parcel{X=bay.X,Y=bay.Y+bay.D,W=bay.W,D=aisle}}:new[]{new Parcel{X=bay.X-aisle,Y=bay.Y,W=aisle,D=bay.D},new Parcel{X=bay.X+bay.W,Y=bay.Y,W=aisle,D=bay.D}};
    if(!fronts.Any(r=>{if(rotation==0){r.X-=o.ParkingEndClearance;r.W+=2*o.ParkingEndClearance;}else{r.Y-=o.ParkingEndClearance;r.D+=2*o.ParkingEndClearance;}return KindRectangle(r,1);}))continue;
    Paint(bay,Fixed);exact.Add(bay);spaces+=bay.ParkingSpaces;
    if(rotation==0)x+=(int)Math.Ceiling((length+Math.Max(2,o.ParkingIsland))/cell)-1;
   }
 }
}
}

