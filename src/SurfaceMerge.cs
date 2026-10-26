using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
// Join non-overlapping rectangular surface cells by removing shared edges.
// Keep holes as separate simple polygons: native Area definitions have one ring.
public static class SurfaceMerge {
 public const int NodeBudget=128; // Our complexity budget, not a claimed game limit.
 const double Scale=1000000;
 static long Key(double value)=>(long)Math.Round(value*Scale);
 public static List<List<Point>> Create(IEnumerable<Parcel> source,int nodeBudget=NodeBudget){
  var rects=ParkingSurfaceGeometry.Union(source).OrderBy(r=>r.X).ThenBy(r=>r.Y).ToArray();
  var horizontal=new Dictionary<long,SortedSet<long>>();var vertical=new Dictionary<long,SortedSet<long>>();
  void Line(Dictionary<long,SortedSet<long>> lines,long axis,long a,long b){if(!lines.TryGetValue(axis,out var values))lines[axis]=values=new SortedSet<long>();values.Add(a);values.Add(b);}
  foreach(var r in rects){long x=Key(r.X),y=Key(r.Y),u=Key(r.X+r.W),v=Key(r.Y+r.D);Line(horizontal,y,x,u);Line(horizontal,v,x,u);Line(vertical,x,y,v);Line(vertical,u,y,v);}
  var ids=new Dictionary<Tuple<long,long>,int>();var points=new List<Point>();
  int Vertex(long x,long y){var key=Tuple.Create(x,y);if(!ids.TryGetValue(key,out int id)){id=points.Count;ids[key]=id;points.Add(new Point(x/Scale,y/Scale));}return id;}
  var boundaries=new HashSet<Tuple<int,int>>[rects.Length];var owners=new Dictionary<Tuple<int,int>,int>();
  var adjacent=new List<Tuple<int,int,double>>();var parent=Enumerable.Range(0,rects.Length).ToArray();
  int Root(int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
  for(int i=0;i<rects.Length;i++){
   var edges=boundaries[i]=new HashSet<Tuple<int,int>>();
   void Side(bool h,long axis,long a,long b){
    var cuts=(h?horizontal:vertical)[axis].GetViewBetween(Math.Min(a,b),Math.Max(a,b)).ToArray();if(a>b)Array.Reverse(cuts);
    for(int k=1;k<cuts.Length;k++){
     int from=h?Vertex(cuts[k-1],axis):Vertex(axis,cuts[k-1]),to=h?Vertex(cuts[k],axis):Vertex(axis,cuts[k]);
     var edge=Tuple.Create(from,to);edges.Add(edge);owners[edge]=i;
     if(owners.TryGetValue(Tuple.Create(to,from),out int other))adjacent.Add(Tuple.Create(other,i,Math.Abs(cuts[k]-cuts[k-1])/Scale));
    }
   }
   var r=rects[i];long x=Key(r.X),y=Key(r.Y),u=Key(r.X+r.W),v=Key(r.Y+r.D);
   Side(true,y,x,u);Side(false,u,y,v);Side(true,v,u,x);Side(false,x,v,y);
  }
  // Prefer long shared boundaries. Retry deferred joins after another merge
  // removes intervening edges; corner-only contacts are never joined.
  var candidates=adjacent.OrderByDescending(e=>e.Item3).ToArray();bool changed;
  do{changed=false;var tried=new HashSet<Tuple<int,int>>();
   foreach(var edge in candidates){int a=Root(edge.Item1),b=Root(edge.Item2);if(a==b)continue;if(a>b){int swap=a;a=b;b=swap;}
    if(!tried.Add(Tuple.Create(a,b)))continue;
    var combined=new HashSet<Tuple<int,int>>(boundaries[a]);
    foreach(var e in boundaries[b])if(!combined.Remove(Tuple.Create(e.Item2,e.Item1)))combined.Add(e);
    var ring=Ring(combined,points);if(ring==null||ring.Count>nodeBudget)continue;
    boundaries[a]=combined;boundaries[b]=null;parent[b]=a;changed=true;
   }
  }while(changed);
  var result=new List<List<Point>>();
  for(int i=0;i<rects.Length;i++)if(parent[i]==i){var ring=Ring(boundaries[i],points);if(ring==null)throw new InvalidOperationException("Surface boundary is not a simple ring");result.Add(ring);}
  return result;
 }
 static List<Point> Ring(HashSet<Tuple<int,int>> edges,List<Point> points){
  var next=new Dictionary<int,int>();var incoming=new HashSet<int>();
  foreach(var e in edges){if(next.ContainsKey(e.Item1)||!incoming.Add(e.Item2))return null;next.Add(e.Item1,e.Item2);}
  if(next.Count<3)return null;
  int start=next.Keys.Min(),at=start;var vertices=new List<int>();
  do{vertices.Add(at);if(!next.TryGetValue(at,out at))return null;if(vertices.Count>edges.Count)return null;}while(at!=start);
  if(vertices.Count!=edges.Count)return null; // Multiple rings would fill a hole.
  var ring=new List<Point>();
  for(int i=0;i<vertices.Count;i++){
   var a=points[vertices[(i+vertices.Count-1)%vertices.Count]];var b=points[vertices[i]];var c=points[vertices[(i+1)%vertices.Count]];
   if((a.X==b.X&&b.X==c.X)||(a.Y==b.Y&&b.Y==c.Y))continue;
   ring.Add(b);
  }
  return ring.Count>=3?ring:null;
 }
}
}
