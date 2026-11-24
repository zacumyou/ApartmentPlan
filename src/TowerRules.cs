using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
// Persisted independently of the slab/villa options. Missing keys keep approved defaults.
public sealed class TowerRules {
 public sealed class Rule {public string Id;public double Default,Min,Max,Step;public Rule(string id,double value,double min,double max,double step=1){Id=id;Default=value;Min=min;Max=max;Step=step;}}
 public static readonly Rule[] Definitions={
 new Rule("MaxBuildings",100,1,200),new Rule("FacilityWidth",20,8,64),new Rule("FacilityDepth",16,8,64),new Rule("Angle",45,0,180),new Rule("BuildingGap",20,4,60),new Rule("GapTolerance",2,0,4,.5),new Rule("BuildingBoundary",4,0,20,.5),new Rule("PackingStep",4,2,8),new Rule("BoundaryTurn",1,0,1),
 new Rule("EntranceInset",20,20,60),new Rule("RoadRadius",4.1,4,16,.1),new Rule("CurveGap",10,10,30),new Rule("FrontCurveGap",10,4,24),new Rule("ParallelGap",10,10,30),new Rule("BranchGap",16,16,40),new Rule("TurnaroundGap",4,4,12,.5),
 new Rule("WalkParallelGap",4,0,12,.5),new Rule("WalkRadius",2.1,2,8,.1),new Rule("WalkBoundary",4,0,20,.5),new Rule("BoundaryParallelRange",16,4,40),new Rule("RoadWalkGap",1,1,5,.5),new Rule("WalkApproachAngle",45,45,90,5),new Rule("ApproachExclusion",12,12,30),
 new Rule("CrossAreaPercent",200,100,500,10),new Rule("LoopBridgeGap",12,0,30),new Rule("CrossWalkGap",4,4,12,.5),new Rule("JunctionExclusion",8,4,16),new Rule("WalkCell",8,4,12),new Rule("TwoFaces",1,0,1),new Rule("Walks",1,0,1),new Rule("OutwardWalks",1,0,1)};
 static readonly Dictionary<string,Rule> ById=Definitions.ToDictionary(d=>d.Id,StringComparer.Ordinal);
 public Dictionary<string,double> Values=new Dictionary<string,double>();
 public double this[string id]=>Values.TryGetValue(id,out var v)?v:ById[id].Default;
 public bool Set(string id,double value){if(!ById.TryGetValue(id,out var d)||double.IsNaN(value)||double.IsInfinity(value))return false;Values[id]=Math.Max(d.Min,Math.Min(d.Max,value));return true;}
 public static TowerRules Parse(string json){var r=new TowerRules();try{var values=Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string,double>>(json??"{}");if(values!=null)foreach(var v in values)r.Set(v.Key,v.Value);}catch(Newtonsoft.Json.JsonException){}return r;}
 public string Encode()=>Newtonsoft.Json.JsonConvert.SerializeObject(Values);
 public object State()=>Definitions.Select(d=>new{id=d.Id,defaultValue=d.Default,value=this[d.Id],min=d.Min,max=d.Max,step=d.Step}).ToArray();
}
// For a point anywhere in a tile, the distance to an edge differs from
// its centre distance by at most half the diagonal. Edges farther than
// the nearest edge plus a full diagonal cannot be nearest in this tile.
internal sealed class TowerBoundary {
 const double Size=12;
 readonly IList<Point> poly;readonly Dictionary<(int,int),Tile> tiles=new Dictionary<(int,int),Tile>();
 sealed class Tile {public bool Interior;public Point Center;public double SafeRadiusSquared;public int[] Nearest,Crossing;}
 readonly double[] levels;readonly double[][] crossings;
 internal TowerBoundary(IList<Point> polygon){
  poly=polygon;
  if(poly.Count<12||!Enumerable.Range(0,poly.Count).All(i=>poly[i].X==poly[(i+1)%poly.Count].X||poly[i].Y==poly[(i+1)%poly.Count].Y))return;
  levels=poly.Select(q=>q.Y).Distinct().OrderBy(y=>y).ToArray();crossings=new double[levels.Length-1][];
  for(int j=0;j<crossings.Length;j++){
   var row=new List<double>();for(int i=0;i<poly.Count;i++){
    var a=poly[i];var b=poly[(i+1)%poly.Count];
    if(a.X==b.X&&Math.Min(a.Y,b.Y)<=levels[j]&&Math.Max(a.Y,b.Y)>=levels[j+1])row.Add(a.X);
   }
   row.Sort();crossings[j]=row.ToArray();
  }
 }
 Tile Get(int x,int y){
  var key=(x,y);if(tiles.TryGetValue(key,out var tile))return tile;
  var center=new Point((x+.5)*Size,(y+.5)*Size);var distances=new double[poly.Count];double nearest=double.MaxValue;
  var crossing=new List<int>();
  for(int i=0;i<poly.Count;i++){
   var a=poly[i];var b=poly[(i+1)%poly.Count];
   double d=(center-VillaGeometry.Nearest(center,a,b)).Length;distances[i]=d;nearest=Math.Min(nearest,d);
   if(Math.Max(a.X,b.X)>=x*Size-.001&&Math.Min(a.X,b.X)<=(x+1)*Size+.001&&
      Math.Max(a.Y,b.Y)>=y*Size-.001&&Math.Min(a.Y,b.Y)<=(y+1)*Size+.001)crossing.Add(i);
  }
  var close=new List<int>();for(int i=0;i<poly.Count;i++)if(distances[i]<=nearest+1.415*Size+.001)close.Add(i);
  tile=new Tile{Center=center,SafeRadiusSquared=Math.Pow(Math.Max(0,nearest-.001),2),Interior=Layout.Inside(center,poly),Nearest=close.ToArray(),Crossing=crossing.ToArray()};
  tiles[key]=tile;return tile;
 }
 static int Index(double q)=>(int)Math.Floor(q/Size);
 internal double Distance(Point q){
  if(poly.Count<12)return TowerGeometry.Distance(q,poly);
  var tile=Get(Index(q.X),Index(q.Y));double best=double.MaxValue;
  foreach(var i in tile.Nearest){var v=q-VillaGeometry.Nearest(q,poly[i],poly[(i+1)%poly.Count]);best=Math.Min(best,v.X*v.X+v.Y*v.Y);}
  return Math.Sqrt(best);
 }
 bool Inside(Point q){
  var tile=Get(Index(q.X),Index(q.Y));
  if(tile.Crossing.Length==0)return tile.Interior;
  if(levels!=null){
   int band=Array.BinarySearch(levels,q.Y);
   if(band<0){int upper=~band;
    if(upper>0&&upper<levels.Length&&q.Y-levels[upper-1]>.001&&levels[upper]-q.Y>.001){
     var row=crossings[upper-1];int k=Array.BinarySearch(row,q.X);
     if(k>=0)return true;
     int next=~k;
     if((next==0||q.X-row[next-1]>.001)&&(next==row.Length||row[next]-q.X>.001))return ((row.Length-next)&1)!=0;
    }
   }
  }
  return Layout.Inside(q,poly);
 }
 internal bool Contains(IList<Point> shape){
  if(poly.Count<12)return VillaGeometry.Inside(shape,poly);
  var tile=Get(Index(shape[0].X),Index(shape[0].Y));bool inDisk=true;
  foreach(var point in shape){var d=point-tile.Center;if(d.X*d.X+d.Y*d.Y>=tile.SafeRadiusSquared){inDisk=false;break;}}
  if(inDisk)return tile.Interior;
  for(int i=0;i<shape.Count;i++)if(!Inside(shape[i]))return false;
  for(int i=0;i<shape.Count;i++){
   var a=shape[i];var b=shape[(i+1)%shape.Count];if(!Inside((a+b)*.5))return false;
   var ab=b-a;int right=Index(Math.Max(a.X,b.X)),top=Index(Math.Max(a.Y,b.Y));
   for(int x=Index(Math.Min(a.X,b.X));x<=right;x++)for(int y=Index(Math.Min(a.Y,b.Y));y<=top;y++)
    foreach(int j in Get(x,y).Crossing){
     var c=poly[j];var d=poly[(j+1)%poly.Count];
     if(Layout.Cross(ab,c-a)*Layout.Cross(ab,d-a)<-1e-8&&Layout.Cross(d-c,a-c)*Layout.Cross(d-c,b-c)<-1e-8)return false;
    }
  }
  return true;
 }
}
public static class TowerGeometry {
 public static List<Point> SurfaceNodes(IList<Point> input){
  var points=new List<Point>();foreach(var q in input)if(points.Count==0||(q-points.Last()).Length>.01)points.Add(q);
  if(points.Count>1&&(points[0]-points.Last()).Length<=.01)points.RemoveAt(points.Count-1);
  bool changed=true;while(changed&&points.Count>3){changed=false;for(int i=0;i<points.Count;i++){
   var a=points[(i+points.Count-1)%points.Count];var b=points[(i+1)%points.Count];
   if((VillaGeometry.Nearest(points[i],a,b)-points[i]).Length>.001)continue;points.RemoveAt(i);changed=true;break;
  }}return points;
 }
 public static PlanBezier Line(Point a,Point b)=>new PlanBezier{A=a,B=a+(b-a)*(1d/3),C=a+(b-a)*(2d/3),D=b};
 // True circular fillet approximation, one cubic per bend. Never shrink radius silently.
 public static List<PlanBezier> Fillet(IList<Point> input,double radius,bool closed,double gap=0){
  var pts=input.ToList();if(closed&&(pts[0]-pts.Last()).Length<.001)pts.RemoveAt(pts.Count-1);int n=pts.Count;if(n<2)return null;
  var a=pts.ToArray();var b=pts.ToArray();var handles=new double[n];
  for(int i=closed?0:1;i<(closed?n:n-1);i++){
   var u=VillaGeometry.Unit(pts[i]-pts[(i+n-1)%n]);var v=VillaGeometry.Unit(pts[(i+1)%n]-pts[i]);double angle=Math.Acos(Math.Max(-1,Math.Min(1,SiteFrame.Dot(u,v))));if(angle<.001)continue;if(angle>Math.PI-.01)return null;
   double cut=radius*Math.Tan(angle/2);a[i]=pts[i]-u*cut;b[i]=pts[i]+v*cut;handles[i]=4d/3*Math.Tan(angle/4)*radius;
  }
  var curves=new List<PlanBezier>();for(int i=0;i<(closed?n:n-1);i++){int j=(i+1)%n;double length=(pts[j]-pts[i]).Length-(b[i]-pts[i]).Length-(a[j]-pts[j]).Length;if(length<-.001||handles[i]>0&&handles[j]>0&&length<gap-.001)return null;if(length>.001)curves.Add(Line(b[i],a[j]));if(handles[j]>0){var u=VillaGeometry.Unit(pts[j]-pts[i]);var v=VillaGeometry.Unit(pts[(j+1)%n]-pts[j]);curves.Add(new PlanBezier{A=a[j],B=a[j]+u*handles[j],C=b[j]-v*handles[j],D=b[j]});}}
  return curves;
 }
 public static PlanRoute Route(IEnumerable<PlanBezier> curves,string use,double width){var r=new PlanRoute{Use=use,Width=width,Curves=curves.ToList()};foreach(var c in r.Curves){var pts=VillaGeometry.Sample(c,2);r.Points.AddRange(r.Points.Count==0?pts:pts.Skip(1));}return r;}
 public static double Distance(Point p,IList<Point> poly){double d=double.MaxValue;for(int i=0;i<poly.Count;i++)d=Math.Min(d,(p-VillaGeometry.Nearest(p,poly[i],poly[(i+1)%poly.Count])).Length);return d;}
 public static double Distance(IList<Point> a,IList<Point> b){if(VillaGeometry.Overlap(a,b))return 0;return Math.Min(a.Min(p=>Distance(p,b)),b.Min(p=>Distance(p,a)));}
}
}
