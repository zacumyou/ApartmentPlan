using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
public sealed class TreeSpecies {public string Id,Name,NameKey,Icon;public double Radius;public bool DefaultTheme;}
public sealed class PlannedTree {public Point Position;public string Species;public int Age;public double Rotation;}
// Sum green coverage across raster parcel seams. A hole, road, parking bay or
// facility cannot be crossed just because the centre happens to be green.
public sealed class GreenSpace {
 readonly Plan plan;readonly List<Parcel> green;
 readonly Dictionary<Tuple<int,int>,List<Parcel>> bins=new Dictionary<Tuple<int,int>,List<Parcel>>();
 public GreenSpace(Plan p){plan=p;green=p.Parcels.Where(r=>r.Kind==4).ToList();foreach(var r in green)
  for(int y=(int)Math.Floor(r.Y/16);y<=(int)Math.Floor((r.Y+r.D)/16);y++)for(int x=(int)Math.Floor(r.X/16);x<=(int)Math.Floor((r.X+r.W)/16);x++){
   var key=Tuple.Create(x,y);if(!bins.TryGetValue(key,out var list))bins[key]=list=new List<Parcel>();list.Add(r);
  }
 }
 public double Area=>green.Sum(r=>r.Area);
 public bool Fits(Point p,double radius){
  double covered=0,side=2*radius;
  var seen=new HashSet<Parcel>();
  for(int y=(int)Math.Floor((p.Y-radius)/16);y<=(int)Math.Floor((p.Y+radius)/16);y++)for(int x=(int)Math.Floor((p.X-radius)/16);x<=(int)Math.Floor((p.X+radius)/16);x++)
   if(bins.TryGetValue(Tuple.Create(x,y),out var list))foreach(var r in list)if(seen.Add(r))covered+=Math.Max(0,Math.Min(p.X+radius,r.X+r.W)-Math.Max(p.X-radius,r.X))*Math.Max(0,Math.Min(p.Y+radius,r.Y+r.D)-Math.Max(p.Y-radius,r.Y));
  return covered>=side*side-.00001;
 }
 public List<PlannedTree> Trees(IList<TreeSpecies> species,int density,int ages,int seed,int placementMode=0){
  var result=new List<PlannedTree>();if(species.Count==0||density<=0||green.Count==0||(ages&15)==0)return result;
  var random=new Random(seed);var allowed=Enumerable.Range(0,4).Where(i=>(ages&(1<<i))!=0).ToArray();
  double step=Math.Sqrt(1000.0/Math.Max(1,Math.Min(60,density)));
  double x0=green.Min(r=>r.X),y0=green.Min(r=>r.Y),x1=green.Max(r=>r.X+r.W),y1=green.Max(r=>r.Y+r.D);
  var positions=new List<Tuple<Point,double>>();
  // Follow the villa boundary before filling interior pockets. Full canopy bounds
  // still have to fit the green mask; roads, buildings and gaps remain excluded.
  if(plan.Villa&&placementMode!=2){
   for(int edge=0;edge<plan.Boundary.Count;edge++){
    var a=plan.Local(plan.Boundary[edge]);var b=plan.Local(plan.Boundary[(edge+1)%plan.Boundary.Count]);var d=b-a;double length=d.Length;if(length<.01)continue;
    var normal=new Point(-d.Y/length,d.X/length);
    for(double distance=step/2;distance<length;distance+=step){
     var along=a+d*(distance/length);bool placed=false;
     int first=random.Next(species.Count);
     for(int choice=0;choice<species.Count&&!placed;choice++){
      var type=species[(first+choice)%species.Count];double radius=Math.Max(1.2,Math.Min(50,type.Radius));
      for(double offset=radius+.35;offset<=12&&!placed;offset+=1){
       foreach(int side in new[]{1,-1}){
        var q=along+normal*(offset*side);
        if(!Fits(q,radius)||positions.Any(p=>(p.Item1-q).Length<(p.Item2+radius)*.75))continue;
        positions.Add(Tuple.Create(q,radius));result.Add(new PlannedTree{Position=plan.World(q),Species=type.Id,Age=allowed[random.Next(allowed.Length)],Rotation=random.NextDouble()*Math.PI*2});placed=true;break;
       }
      }
     }
     if(result.Count>=1500)return FilterTrees(result,placementMode);
    }
   }
  }
  for(double y=y0+step/2;y<y1;y+=step)for(double x=x0+step/2;x<x1;x+=step){
   var type=species[random.Next(species.Count)];double radius=Math.Max(1.2,Math.Min(50,type.Radius));
   var q=new Point(x+(random.NextDouble()-.5)*step*.45,y+(random.NextDouble()-.5)*step*.45);
   if(!Fits(q,radius)||positions.Any(p=>(p.Item1-q).Length<(p.Item2+radius)*.75))continue;
   positions.Add(Tuple.Create(q,radius));result.Add(new PlannedTree{Position=plan.World(q),Species=type.Id,Age=allowed[random.Next(allowed.Length)],Rotation=random.NextDouble()*Math.PI*2});
   if(result.Count>=1500)return FilterTrees(result,placementMode);
  }return FilterTrees(result,placementMode);
 }
 public double BoundaryDistance(Point world){
  double best=double.MaxValue;for(int i=0;i<plan.Boundary.Count;i++){var a=plan.Boundary[i];var d=plan.Boundary[(i+1)%plan.Boundary.Count]-a;double ll=SiteFrame.Dot(d,d);if(ll<.001)continue;double t=Math.Max(0,Math.Min(1,SiteFrame.Dot(world-a,d)/ll));best=Math.Min(best,(world-a-d*t).Length);}return best;
 }
 List<PlannedTree> FilterTrees(List<PlannedTree> trees,int mode)=>mode==1?trees.Where(t=>BoundaryDistance(t.Position)<=12).ToList():mode==2?trees.Where(t=>BoundaryDistance(t.Position)>12).ToList():trees;
 // World aligned mask pixels; a full brush footprint including pixel rounding
 // must stay inside green. Small slivers are deliberately left untouched.
 public bool GrassFits(Point world,double pixel)=>Fits(plan.Local(world),pixel*2.2);
 public IEnumerable<Point> Grass(double pixel,double minX,double minY)=>GrassCandidates(pixel,minX,minY).Where(q=>GrassFits(q,pixel));
 public IEnumerable<Point> GrassCandidates(double pixel,double minX,double minY){
  if(green.Count==0||pixel<=0)yield break;
  var corners=green.SelectMany(r=>r.Corners.Select(plan.World)).ToArray();
  int x0=(int)Math.Ceiling((corners.Min(q=>q.X)-minX)/pixel),x1=(int)Math.Floor((corners.Max(q=>q.X)-minX)/pixel);
  int y0=(int)Math.Ceiling((corners.Min(q=>q.Y)-minY)/pixel),y1=(int)Math.Floor((corners.Max(q=>q.Y)-minY)/pixel);
  for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++){
   var world=new Point(minX+x*pixel,minY+y*pixel);
   yield return world;
  }
 }
}
}

