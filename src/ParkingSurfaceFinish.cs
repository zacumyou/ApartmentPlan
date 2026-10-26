using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
internal sealed partial class ResidentialLayout {
 void FindGrassPockets(){
  // A local strip must have developed ground on opposite sides and a third
  // closed direction. The large open park and manual green reservations survive.
  var selected=new bool[nx,ny];
  bool Developed(int x,int y)=>map[x,y]==1||map[x,y]==Fixed||map[x,y]==Paved||map[x,y]==Walk;
  int Ray(int x,int y,int dx,int dy){for(int d=1;d*cell<=80;d++){int u=x+dx*d,v=y+dy*d;if(u<0||v<0||u>=nx||v>=ny||map[u,v]==Outside||reserved[u,v]||edgeGreen[u,v])return 99;if(Developed(u,v))return d;}return 99;}
  for(int y=1;y<ny-1;y++){o.Cancellation.ThrowIfCancellationRequested();for(int x=1;x<nx-1;x++){
   if(reserved[x,y]||edgeGreen[x,y]||(map[x,y]!=Green&&map[x,y]!=CourtGreen))continue;
   int l=Ray(x,y,-1,0),r=Ray(x,y,1,0),b=Ray(x,y,0,-1),t=Ray(x,y,0,1);
   selected[x,y]=((l+r-1)*cell<=10&&(b<99||t<99))||((b+t-1)*cell<=10&&(l<99||r<99));
  }}
  // Keep only small connected pockets. A narrow connection cannot turn an
  // entire perimeter park into a grass-surface blanket.
  var seen=new bool[nx,ny];int[] dx={1,-1,0,0},dy={0,0,1,-1};
  var patches=new List<Parcel>();
  for(int y=0;y<ny;y++)for(int x=0;x<nx;x++){
   if(!selected[x,y]||seen[x,y])continue;
   var cells=new List<int>{y*nx+x};seen[x,y]=true;
   for(int i=0;i<cells.Count;i++){int u=cells[i]%nx,v=cells[i]/nx;for(int k=0;k<4;k++){int a=u+dx[k],b=v+dy[k];if(a<0||b<0||a>=nx||b>=ny||seen[a,b]||!selected[a,b])continue;seen[a,b]=true;cells.Add(b*nx+a);}}
   if(cells.Count*cell*cell>600||cells.Count*cell*cell<2)continue;
   foreach(int id in cells)patches.Add(Rect(id%nx,id/nx,1,1));
  }
  plan.GrassPockets.AddRange(ParkingSurfaceGeometry.Union(patches));
 }
}
public static class ParkingSurfaceGeometry {
 // Exact rectangular union removes raster seams and overlapping coplanar
 // patches. Yield each x slab so native submission can pace the CPU work.
 public static IEnumerable<Parcel> Union(IEnumerable<Parcel> source){
  var rects=source.Where(r=>r.W>.001&&r.D>.001).ToList();
  var xs=rects.SelectMany(r=>new[]{r.X,r.X+r.W}).Distinct().OrderBy(x=>x).ToArray();
  var active=new Dictionary<string,Parcel>();
  for(int i=1;i<xs.Length;i++){
   double low=xs[i-1],high=xs[i];if(high-low<.001)continue;
   var ranges=rects.Where(r=>r.X<high-.0001&&r.X+r.W>low+.0001).OrderBy(r=>r.Y).ToList();
   var next=new Dictionary<string,Parcel>();
   for(int j=0;j<ranges.Count;){double a=ranges[j].Y,b=a+ranges[j].D;j++;
    while(j<ranges.Count&&ranges[j].Y<=b+.0001){b=Math.Max(b,ranges[j].Y+ranges[j].D);j++;}
    string key=a.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+":"+b.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
    if(active.TryGetValue(key,out var p)&&Math.Abs(p.X+p.W-low)<.001){p.W=high-p.X;active.Remove(key);}else p=new Parcel{Kind=8,X=low,Y=a,W=high-low,D=b-a};
    next[key]=p;
   }
   foreach(var p in active.Values)yield return p;active=next;
  }
  foreach(var p in active.Values)yield return p;
 }
 public static IEnumerable<Parcel> Paving(Plan plan,bool invisible){
  var covers=plan.Parcels.Where(r=>r.Kind==8||r.Kind==2||invisible&&r.Kind==1&&r.InvisibleRoad).Concat(plan.PavementPads??new List<Parcel>());
  // Existing saved plans may lack pads. Always preserve lots/masks/paths.
  var blockers=plan.Parcels.Where(r=>r.Kind==0||r.Kind==3||r.Kind==5||r.Kind==6||r.Kind==9).ToArray();
  foreach(var cover in Union(covers)){
   var pieces=new List<Parcel>{cover};
   foreach(var b in blockers){var next=new List<Parcel>();foreach(var p in pieces){
    if(!Layout.Overlap(p,b)){next.Add(p);continue;}
    var cut=new Parcel{X=Math.Max(p.X,b.X),Y=Math.Max(p.Y,b.Y)};cut.W=Math.Min(p.X+p.W,b.X+b.W)-cut.X;cut.D=Math.Min(p.Y+p.D,b.Y+b.D)-cut.Y;
    next.AddRange(ReplacementRules.Vacated(p,cut));
   }pieces=next;}
   foreach(var p in pieces)yield return p;
  }
 }
 // Native main/aisle graph endpoints are exact shared junctions. A trapezoid
 // extends from the invisible approach to the main road centre (half its width).
 public static IEnumerable<List<Point>> JunctionAprons(IList<ConstructionCourse> graph){
  var used=new HashSet<string>();
  foreach(var side in graph.Where(e=>e.Use=="aisle"||e.Use=="aisleWide"))foreach(bool start in new[]{true,false}){
   var p=start?side.Curve.A:side.Curve.D;var tangent=start?side.Curve.B-p:side.Curve.C-p;if(tangent.Length<.01)continue;var d=tangent*(1/tangent.Length);
   var main=graph.Where(e=>e.Use=="main"&&((e.Curve.A-p).Length<.05||(e.Curve.D-p).Length<.05)).OrderByDescending(e=>e.Width).FirstOrDefault();if(main==null)continue;
   var m=(main.Curve.A-p).Length<.05?main.Curve.B-p:main.Curve.C-p;if(m.Length<.01||Math.Abs(Layout.Cross(d,m))/m.Length<.5)continue;
   string key=(Math.Round(p.X*100)+","+Math.Round(p.Y*100))+":"+Math.Round(d.X,2)+":"+Math.Round(d.Y,2);if(!used.Add(key))continue;
   double reach=main.Width/2/Math.Max(.5,Math.Abs(Layout.Cross(d,m))/m.Length),half=side.Width/2;
   var n=new Point(-d.Y,d.X);var outer=p+d*(reach+1);
   yield return new List<Point>{p-n*(half+main.Width/2),p+n*(half+main.Width/2),outer+n*half,outer-n*half};
  }
 }
}
}
