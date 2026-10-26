using System;
using System.Collections.Generic;
using System.Linq;

namespace ApartmentPlan {
public sealed class ManualZone {
 public int Kind;
 public List<Point> Points=new List<Point>(); // world coordinates, independent of frame
}
public static class SiteFrame {
 public static Point Nearest(IList<Point> boundary,Point p,out Point inward){
  double best=double.MaxValue;Point result=default;inward=default;
  for(int i=0;i<boundary.Count;i++){
   var a=boundary[i];var d=boundary[(i+1)%boundary.Count]-a;double len=d.Length;
   double t=Math.Max(0,Math.Min(1,Dot(p-a,d)/(len*len)));var q=a+d*t;
   if((q-p).Length>=best)continue;best=(q-p).Length;result=q;
   inward=new Point(-d.Y/len,d.X/len)*(Layout.SignedArea(boundary)>0?1:-1);
  }
  return result;
 }
 internal static double Dot(Point a,Point b)=>a.X*b.X+a.Y*b.Y;
 public static Plan Create(IList<Point> boundary,Point entrance){
  var origin=Nearest(boundary,entrance,out var inward);
  // Vote for orthogonal edge families. A short diagonal cannot rotate all slabs.
  double best=-1,angle=0;
  for(int i=0;i<boundary.Count;i++){
   var edge=boundary[(i+1)%boundary.Count]-boundary[i];double a=Math.Atan2(edge.Y,edge.X),score=0;
   for(int j=0;j<boundary.Count;j++){
    var e=boundary[(j+1)%boundary.Count]-boundary[j];double alignment=Math.Cos(4*(Math.Atan2(e.Y,e.X)-a));
    if(alignment>Math.Cos(Math.PI/9))score+=e.Length*alignment;
   }
   // Canonical angle makes ties independent of boundary winding/start vertex.
   a=(a%(Math.PI/2)+Math.PI/2)%(Math.PI/2);
   if(score>best+1e-6||Math.Abs(score-best)<1e-6&&a<angle){best=score;angle=a;}
  }
  var axis=new Point(Math.Cos(angle),Math.Sin(angle));var perpendicular=new Point(-axis.Y,axis.X);
  var forward=Math.Abs(Dot(inward,axis))>Math.Abs(Dot(inward,perpendicular))?axis:perpendicular;
  if(Dot(inward,forward)<0)forward=forward*-1;
  return new Plan{Boundary=boundary.ToList(),Area=Math.Abs(Layout.SignedArea(boundary)),Origin=origin,Forward=forward,Right=new Point(forward.Y,-forward.X)};
 }
 public static bool ContainsPolygon(IList<Point> outer,IList<Point> inner){
  if(!Layout.Valid(inner)||inner.Any(p=>!Layout.Inside(p,outer)))return false;
  for(int i=0;i<inner.Count;i++){
   var a=inner[i];var b=inner[(i+1)%inner.Count];if(!Layout.Inside((a+b)*.5,outer))return false;
   for(int j=0;j<outer.Count;j++){
    var c=outer[j];var d=outer[(j+1)%outer.Count];
    if(Layout.Cross(b-a,c-a)*Layout.Cross(b-a,d-a)<-1e-8&&Layout.Cross(d-c,a-c)*Layout.Cross(d-c,b-c)<-1e-8)return false;
   }
  }
  return true;
 }
}
internal sealed partial class ResidentialLayout {
 readonly List<Parcel> entrances=new List<Parcel>();
 readonly List<Point> entranceTips=new List<Point>();
 bool[,] reserved;
 bool[,] entranceWalkBuffer;
 const double EntranceWalkRadius=8;
 void ProtectEntranceWalks(Point gate){
  // Reserve the full 8 m disk at the actual boundary entrance for vehicle
  // junctions. Mark cells touching it once; pedestrian routing then checks its
  // whole width through the normal clearance sum. Boundary exit tails are
  // intentionally drawn separately by OpenSpaceWalks and remain allowed.
  for(int y=Math.Max(0,Y(gate.Y-EntranceWalkRadius));y<Math.Min(ny,Y(gate.Y+EntranceWalkRadius)+1);y++)
   for(int x=Math.Max(0,X(gate.X-EntranceWalkRadius));x<Math.Min(nx,X(gate.X+EntranceWalkRadius)+1);x++){
    double px=Math.Max(x0+x*cell,Math.Min(x0+(x+1)*cell,gate.X));
    double py=Math.Max(y0+y*cell,Math.Min(y0+(y+1)*cell,gate.Y));
    if((new Point(px,py)-gate).Length<=EntranceWalkRadius)entranceWalkBuffer[x,y]=true;
   }
 }
 bool EntranceApproach(int x,int y){
  // The perimeter margin must leave a straight crossing at each user entrance.
  // Only routing may use this strip; housing/parking still obey edgeGreen.
  var p=new Point(x0+(x+.5)*cell,y0+(y+.5)*cell);
  for(int i=0;i<entrances.Count;i++){
   var e=entrances[i];var d=entranceDirections[i];var q=p-new Point(e.X+e.W/2,e.Y+e.D/2);
   double along=SiteFrame.Dot(q,d),across=Math.Abs(Layout.Cross(q,d));
   if(across<=o.Road/2+cell&&along>=-o.Entrance/2&&along<=o.Entrance/2+o.BoundaryGreenMargin+o.Road)return true;
  }
  return false;
 }
 Parcel EntranceParcel(Point world){
  var border=SiteFrame.Nearest(plan.Boundary,world,out var inward);var p=plan.Local(border);
  var normal=new Point(SiteFrame.Dot(inward,plan.Right),SiteFrame.Dot(inward,plan.Forward));
  bool alongX=Math.Abs(normal.X)>Math.Abs(normal.Y);double sign=(alongX?normal.X:normal.Y)>=0?1:-1;
  var direction=alongX?new Point(sign,0):new Point(0,sign);
  // A short clipped apron meets a sloping edge; all full-width interior roads
  // remain in the common frame. Near-corner entrances are rejected, not moved.
  for(double inset=0;inset<=o.Road;inset+=cell/2){
   double entranceLength=Math.Max(o.Entrance,o.Road+2*cell);
   var start=p+direction*inset;var end=start+direction*entranceLength;
   var r=new Parcel{Kind=1,X=alongX?Math.Min(start.X,end.X):start.X-o.Road/2,Y=alongX?start.Y-o.Road/2:Math.Min(start.Y,end.Y),W=alongX?entranceLength:o.Road,D=alongX?o.Road:entranceLength};
   if(!Layout.Contains(r,poly))continue;
   if(entrances.Any(e=>Layout.Overlap(e,r,o.Road)))throw new ArgumentException("EntranceSpacing");
   Bounds(r,out int a,out int b,out int c,out int d);
   for(int y=b;y<d;y++)for(int x=a;x<c;x++)if(reserved[x,y])throw new ArgumentException("ZoneFit");else if(map[x,y]==Fixed)throw new ArgumentException("RampFit");
   Paint(r,1);entrances.Add(r);ProtectEntranceWalks(p);entranceDirections.Add(direction);entranceTips.Add(end-direction*Math.Min(entranceLength/2,o.Road/2+cell));
   plan.ConstructionRoutes.Add(new PlanRoute{Use="main",Width=o.Road,Points=new List<Point>{p,entranceTips[entranceTips.Count-1]}});
   var apron=new Parcel{X=Math.Min(p.X,r.X),Y=Math.Min(p.Y,r.Y),W=Math.Max(p.X,r.X+r.W)-Math.Min(p.X,r.X),D=Math.Max(p.Y,r.Y+r.D)-Math.Min(p.Y,r.Y)};
   Bounds(apron,out a,out b,out c,out d);for(int y=b;y<d;y++)for(int x=a;x<c;x++){if(reserved[x,y])throw new ArgumentException("ZoneFit");if(map[x,y]==Fixed)throw new ArgumentException("RampFit");if(map[x,y]!=Outside)map[x,y]=1;}
   return r;
  }
  throw new ArgumentException("EntranceFit");
 }
 void ManualZones(IList<ManualZone> zones){
  if(zones==null)return;
  foreach(var zone in zones.OrderBy(z=>z.Kind==5?0:1)){
   if(zone.Kind!=3&&zone.Kind!=4&&zone.Kind!=5||!SiteFrame.ContainsPolygon(plan.Boundary,zone.Points))throw new ArgumentException("ZoneFit");
   var points=zone.Points.Select(plan.Local).ToList();int count=0;double xmin=points.Min(p=>p.X),xmax=points.Max(p=>p.X),ymin=points.Min(p=>p.Y),ymax=points.Max(p=>p.Y);
   // Conservatively reserve every intersecting grid cell. Boundary cells are
   // already clipped to the site, so neither packing nor routing enters a zone.
   for(int y=0;y<ny;y++)for(int x=0;x<nx;x++){
    if(map[x,y]==Outside)continue;var r=Rect(x,y,1,1);if(r.X>xmax||r.X+r.W<xmin||r.Y>ymax||r.Y+r.D<ymin)continue;
    bool hit=r.Corners.Any(p=>Layout.Inside(p,points))||points.Any(p=>p.X>=r.X&&p.X<=r.X+r.W&&p.Y>=r.Y&&p.Y<=r.Y+r.D);
    if(!hit){var corners=r.Corners;for(int i=0;i<points.Count&&!hit;i++)for(int j=0;j<4;j++){var a=points[i];var b=points[(i+1)%points.Count];var c=corners[j];var d=corners[(j+1)%4];if(Layout.Cross(b-a,c-a)*Layout.Cross(b-a,d-a)<0&&Layout.Cross(d-c,a-c)*Layout.Cross(d-c,b-c)<0){hit=true;break;}}}
    if(!hit)continue;if(zone.Kind==5){reserved[x,y]=true;map[x,y]=Reserve;count++;continue;}if(map[x,y]==Reserve)continue;if(reserved[x,y]||map[x,y]!=Green)throw new ArgumentException("ZoneFit");
    reserved[x,y]=true;map[x,y]=zone.Kind==5?Reserve:zone.Kind;count++;
   }
   if(count==0)throw new ArgumentException("ZoneFit");
  }
 }
 // Small binary heap keeps direction-aware routing independent of game APIs.
 sealed class RouteHeap {
  readonly List<(double,int)> values=new List<(double,int)>();
  public void Clear()=>values.Clear();
  public int Count=>values.Count;
  public void Push(double cost,int state){var value=(cost,state);int i=values.Count;values.Add(value);while(i>0){int parent=(i-1)/2;if(values[parent].Item1<=cost)break;values[i]=values[parent];i=parent;}values[i]=value;}
  public (double,int) Pop(){var result=values[0];var last=values[values.Count-1];values.RemoveAt(values.Count-1);if(values.Count==0)return result;int i=0;while(i*2+1<values.Count){int child=i*2+1;if(child+1<values.Count&&values[child+1].Item1<values[child].Item1)child++;if(values[child].Item1>=last.Item1)break;values[i]=values[child];i=child;}values[i]=last;return result;}
 }
 double[] routeDistances;int[] routePrevious;int[,] routeSums;readonly RouteHeap routeHeap=new RouteHeap();
 static readonly int[] routeDx={1,0,-1,0},routeDy={0,1,0,-1};
 List<Point> FindRoute(Point source,Func<int,int,bool> target,double width,bool pedestrian=false,double maxLength=double.MaxValue){
  int span=(int)Math.Ceiling(width/cell),lo=span/2,hi=span-lo;
  var sum=routeSums??(routeSums=new int[nx+1,ny+1]);
  for(int y=0;y<ny;y++)for(int x=0;x<nx;x++){
   int k=map[x,y];bool blocked=k==Outside||k==Fixed||k==Reserve||k==3||k==5||(pedestrian&&entranceWalkBuffer[x,y])||(!pedestrian&&((mainJunctionBuffer?[x,y]??false)||reserved[x,y]||edgeGreen[x,y]&&k!=1&&!EntranceApproach(x,y)));
   sum[x+1,y+1]=(blocked?1:0)+sum[x,y+1]+sum[x+1,y]-sum[x,y];
  }
  Func<int,int,bool> clear=(x,y)=>{int a=x-lo,b=y-lo,c=x+hi,d=y+hi;return a>=0&&b>=0&&c<=nx&&d<=ny&&sum[c,d]-sum[a,d]-sum[c,b]+sum[a,b]==0;};
  int sx=X(source.X),sy=Y(source.Y);if(!clear(sx,sy))return null;
  o.Cancellation.ThrowIfCancellationRequested();
  int count=nx*ny*4;var dist=routeDistances??(routeDistances=new double[count]);var prev=routePrevious??(routePrevious=new int[count]);for(int i=0;i<count;i++){dist[i]=double.MaxValue;prev[i]=-1;}
  var heap=routeHeap;heap.Clear();for(int dir=0;dir<4;dir++){int id=(sy*nx+sx)*4+dir;dist[id]=0;heap.Push(0,id);}
  var dx=routeDx;var dy=routeDy;int found=-1;
  int checks=0;while(heap.Count>0){if((++checks&255)==0)o.Cancellation.ThrowIfCancellationRequested();var item=heap.Pop();int id=item.Item2;if(item.Item1>dist[id]+1e-6)continue;int dir=id%4,n=id/4,x=n%nx,y=n/nx;
   if(target(x,y)){found=id;break;}
   if((new Point(x0+(x+.5)*cell,y0+(y+.5)*cell)-source).Length>maxLength)continue;
   for(int k=0;k<4;k++){
    if(k==(dir+2)%4||pedestrian&&map[x,y]==1&&k!=dir&&prev[id]>=0)continue;int xx=x+dx[k],yy=y+dy[k];if(!clear(xx,yy))continue;
    int next=(yy*nx+xx)*4+k;double cost=item.Item1+cell*(pedestrian&&map[xx,yy]==1?3:1)+(k==dir?0:Math.Max(width*2,8));
    if(cost>=dist[next])continue;dist[next]=cost;prev[next]=id;heap.Push(cost,next);
   }
  }
  if(found<0)return null;var result=new List<Point>();
  for(int id=found;id>=0;id=prev[id]){int n=id/4;result.Add(new Point(x0+(n%nx+((span%2)==0?0:.5))*cell,y0+(n/nx+((span%2)==0?0:.5))*cell));}
  result.Reverse();return result;
 }
 void DrawRoute(List<Point> points,double width,string use){
  int span=(int)Math.Ceiling(width/cell),lo=span/2,hi=span-lo;
  if(use=="access"||use=="aisleWide"||use=="main"&&o.MainRoadCornerMode!=2){
   double actualWidth=use=="aisleWide"?width:span*cell;
   var corners=SimplifyRoute(points);var shortened=DiagonalRouting.Shorten(corners,actualWidth,cell,(a,b)=>ClearDiagonal(a,b,actualWidth));
   if(shortened.Exists(p=>!corners.Exists(q=>(q-p).Length<.001))){
    var dense=new List<Point>();
    for(int n=1;n<shortened.Count;n++){
     var a=shortened[n-1];var b=shortened[n];var d=b-a;
     if(Math.Abs(d.X)>.001&&Math.Abs(d.Y)>.001){
      foreach(int id in DiagonalCells(a,b,actualWidth))map[id%nx,id/nx]=1;
     }else{
      int steps=Math.Max(1,(int)Math.Round(d.Length/cell));for(int k=0;k<=steps;k++)dense.Add(a+d*((double)k/steps));
     }
    }
    foreach(var p in dense){int x=X(p.X),y=Y(p.Y);for(int j=y-lo;j<y+hi;j++)for(int i=x-lo;i<x+hi;i++)if(!reserved[i,j]&&map[i,j]!=3&&map[i,j]!=5)map[i,j]=1;}
    plan.Routes.Add(new PlanRoute{Use=use,Width=actualWidth,Points=shortened});return;
   }
  }
  foreach(var p in points){int x=X(p.X),y=Y(p.Y);for(int j=y-lo;j<y+hi;j++)for(int i=x-lo;i<x+hi;i++)if(!reserved[i,j]&&map[i,j]!=3&&map[i,j]!=5)map[i,j]=use=="pedestrian"&&map[i,j]!=1?Walk:1;}
  var route=new PlanRoute{Use=use,Width=use=="aisleWide"?width:span*cell};
  for(int i=0;i<points.Count;i++)if(i==0||i==points.Count-1||Math.Abs(Layout.Cross(points[i]-points[i-1],points[i+1]-points[i]))>.01)route.Points.Add(points[i]);
  plan.Routes.Add(route);
 }
 void MainRoads(){
  if(entranceTips.Count>1){
   // Establish a through spine first. Additional gates may join that spine
   // at a true T instead of each making its own curved trip to gate zero.
   int trunk=1;
   if(o.AllowMainTJunctions&&entranceTips.Count>2)trunk=Enumerable.Range(1,entranceTips.Count-1)
    .OrderBy(n=>SiteFrame.Dot(entranceDirections[0],entranceDirections[n]))
    .ThenByDescending(n=>(entranceTips[n]-entranceTips[0]).Length).First();
   ConnectMainEntrances(trunk,0);
   for(int n=1;n<entranceTips.Count;n++){
    if(n==trunk)continue;
    if(o.AllowMainTJunctions){if(TryMainTJunction(n))continue;throw new ArgumentException("DisconnectedEntrance");}
    ConnectMainEntrances(n,0);
   }
  }else{
   // Prefer a straight spine toward the far part of the site. For concave sites,
   // use the nearest reachable interior candidate rather than an external centroid.
   var start=entranceTips[0];double best=0;List<Point> chosen=null;
   for(int n=8;n>=3;n--){var target=start+new Point(0,(poly.Max(p=>p.Y)-start.Y-o.Road)*n/10.0);int tx=X(target.X),ty=Y(target.Y);
    if(!Layout.Contains(new Parcel{X=target.X-o.Road/2,Y=target.Y-o.Road/2,W=o.Road,D=o.Road},poly))continue;
    var route=FindRoute(start,(x,y)=>x==tx&&y==ty,o.Road);if(route==null)continue;
    double score=(target-start).Length;if(score>best){chosen=route;best=score;}if(chosen!=null)break;
   }
   if(chosen!=null&&!TryMainSkeleton(chosen))throw new ArgumentException("CurveFit");
  }
 }
}
}
