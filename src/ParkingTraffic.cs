using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
public sealed class ParkingConnection {
 public Parcel Bay,Aisle;public int EntrySide;public bool Valid;
 public string Failure="";public List<Point> Ports=new List<Point>();public List<Point> Route=new List<Point>();
}
// Planning topology and clearance only. This does not create native CarLane,
// ParkingLane or LaneConnection entities, or simulate a vehicle's swept body.
public static class ParkingTraffic {
 static double OverlapArea(Parcel a,Parcel b)=>Math.Max(0,Math.Min(a.X+a.W,b.X+b.W)-Math.Max(a.X,b.X))*Math.Max(0,Math.Min(a.Y+a.D,b.Y+b.D)-Math.Max(a.Y,b.Y));
 public static bool Covered(Parcel area,IEnumerable<Parcel> roads)=>roads.Sum(r=>OverlapArea(area,r))>=area.Area-.001;
 public static void Validate(Plan plan,LayoutOptions o){
  plan.ParkingConnections.Clear();plan.ParkingAccessFailures=0;
  var roads=plan.Parcels.Where(p=>p.Kind==1).ToList();if(roads.Count==0){foreach(var bay in plan.Parcels.Where(p=>p.Kind==2)){plan.ParkingConnections.Add(new ParkingConnection{Bay=bay,Failure="No vehicle network"});plan.ParkingAccessFailures++;}return;}
  var bounds=plan.Boundary.Select(plan.Local).ToList();double x0=Math.Floor(bounds.Min(p=>p.X))-2,y0=Math.Floor(bounds.Min(p=>p.Y))-2;
  double step=Math.Max(1,Math.Ceiling(Math.Max(bounds.Max(p=>p.X)-x0,bounds.Max(p=>p.Y)-y0)/600));
  int nx=(int)Math.Ceiling((bounds.Max(p=>p.X)-x0)/step)+3,ny=(int)Math.Ceiling((bounds.Max(p=>p.Y)-y0)/step)+3;
  // Rasterize the union of disjoint road parcels. Rounding each parcel inward
  // independently leaves artificial gaps at fractional-coordinate split seams.
  var coverage=new double[nx,ny];foreach(var r in roads){int a=Math.Max(0,(int)Math.Floor((r.X-x0)/step)),b=Math.Max(0,(int)Math.Floor((r.Y-y0)/step)),c=Math.Min(nx,(int)Math.Ceiling((r.X+r.W-x0)/step)),d=Math.Min(ny,(int)Math.Ceiling((r.Y+r.D-y0)/step));for(int y=b;y<d;y++)for(int x=a;x<c;x++)coverage[x,y]+=Math.Max(0,Math.Min(r.X+r.W,x0+(x+1)*step)-Math.Max(r.X,x0+x*step))*Math.Max(0,Math.Min(r.Y+r.D,y0+(y+1)*step)-Math.Max(r.Y,y0+y*step));}
  var road=new bool[nx,ny];for(int y=0;y<ny;y++)for(int x=0;x<nx;x++)road[x,y]=coverage[x,y]>=step*step-.00001;
  var sum=new int[nx+1,ny+1];for(int y=0;y<ny;y++)for(int x=0;x<nx;x++)sum[x+1,y+1]=(road[x,y]?0:1)+sum[x,y+1]+sum[x+1,y]-sum[x,y];
  int span=(int)Math.Ceiling(o.VehiclePassageWidth/step),lo=span/2,hi=span-lo;
  Func<int,int,bool> clear=(x,y)=>x>=lo&&y>=lo&&x+hi<=nx&&y+hi<=ny&&sum[x+hi,y+hi]-sum[x-lo,y+hi]-sum[x+hi,y-lo]+sum[x-lo,y-lo]==0;
  var parent=new int[nx*ny];for(int i=0;i<parent.Length;i++)parent[i]=-2;
  var queue=new List<int>();var entry=roads[0];double nearest=double.MaxValue;int seed=-1;
  for(int y=0;y<ny;y++)for(int x=0;x<nx;x++)if(clear(x,y)){
   double px=x0+(x+.5)*step,py=y0+(y+.5)*step;
   if(px<entry.X||px>entry.X+entry.W||py<entry.Y||py>entry.Y+entry.D)continue;
   double distance=Math.Abs(px-entry.X-entry.W/2)+Math.Abs(py-entry.Y-entry.D/2);if(distance<nearest){nearest=distance;seed=y*nx+x;}
  }
  if(seed>=0){queue.Add(seed);parent[seed]=-1;}
  int[] dx={1,0,-1,0},dy={0,1,0,-1};for(int head=0;head<queue.Count;head++){int n=queue[head],x=n%nx,y=n/nx;for(int k=0;k<4;k++){int a=x+dx[k],b=y+dy[k];if(!clear(a,b)||parent[b*nx+a]!=-2)continue;parent[b*nx+a]=n;queue.Add(b*nx+a);}}
  foreach(var bay in plan.Parcels.Where(p=>p.Kind==2)){
   var result=new ParkingConnection{Bay=bay,Failure="No full-width aisle with end clearance"};
   if(bay.ParkingSpaces<1||Math.Abs((bay.ParkingAlongX?bay.W:bay.D)-bay.ParkingSpaces*o.BayWidth)>.01||(bay.ParkingAlongX?bay.D:bay.W)<o.BayDepth-.001){result.Failure="Parking axis or stall dimensions inconsistent";plan.ParkingConnections.Add(result);plan.ParkingAccessFailures++;continue;}
   foreach(int side in new[]{-1,1}){
    bool h=bay.ParkingAlongX;double end=o.ParkingEndClearance;
    var aisle=h?new Parcel{X=bay.X-end,Y=side<0?bay.Y-o.ParkingAisle:bay.Y+bay.D,W=bay.W+2*end,D=o.ParkingAisle}:new Parcel{X=side<0?bay.X-o.ParkingAisle:bay.X+bay.W,Y=bay.Y-end,W=o.ParkingAisle,D=bay.D+2*end};
    if(!Covered(aisle,roads))continue;
    var ports=new List<Point>();bool connected=true;int middle=-1;
    for(int i=0;i<bay.ParkingSpaces;i++){
     var p=h?new Point(bay.X+(i+.5)*bay.W/bay.ParkingSpaces,aisle.Y+aisle.D/2):new Point(aisle.X+aisle.W/2,bay.Y+(i+.5)*bay.D/bay.ParkingSpaces);
     int x=(int)Math.Floor((p.X-x0)/step),y=(int)Math.Floor((p.Y-y0)/step);if(x<0||y<0||x>=nx||y>=ny||parent[y*nx+x]==-2){connected=false;break;}
     ports.Add(p);if(i==bay.ParkingSpaces/2)middle=y*nx+x;
    }
    if(!connected||ports.Count==0){result.Failure="Aisle disconnected or vehicle passage too narrow";continue;}
    result.Aisle=aisle;result.EntrySide=side;result.Ports=ports;result.Valid=true;result.Failure="";bay.ParkingEntrySide=side;
    var chain=new List<Point>();for(int at=middle;at>=0;at=parent[at])chain.Add(new Point(x0+(at%nx+.5)*step,y0+(at/nx+.5)*step));
    for(int i=0;i<chain.Count;i++)if(i==0||i==chain.Count-1||Math.Abs(Layout.Cross(chain[i]-chain[i-1],chain[i+1]-chain[i]))>.001)result.Route.Add(chain[i]);
    break;
   }
   plan.ParkingConnections.Add(result);if(!result.Valid)plan.ParkingAccessFailures++;
  }
 }
}
}
