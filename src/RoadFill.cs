using System;
using System.Collections.Generic;
namespace ApartmentPlan {
// Bounded visibility fill: real curb edges constrain rays; missing sectors close
// with straight chords, never with a synthetic circle around the pointer.
public static class RoadFill {
 static bool Crosses(Point a,Point b,Point c,Point d)=>Layout.Cross(b-a,c-a)*Layout.Cross(b-a,d-a)<0&&Layout.Cross(d-c,a-c)*Layout.Cross(d-c,b-c)<0;
 static readonly Point[] Directions=MakeDirections();
 static Point[] MakeDirections(){var result=new Point[96];for(int i=0;i<result.Length;i++){double a=i*Math.PI*2/result.Length;result[i]=new Point(Math.Cos(a),Math.Sin(a));}return result;}
 public struct Edge { public Point A,B;public double Width;public Edge(Point a,Point b,double width){A=a;B=b;Width=width;} }
 public static List<Point> Solve(Point seed,IList<Edge> roads,double radius){
  var empty=new List<Point>();if(roads.Count==0||roads.Count>4096)return empty;
  var edges=new List<Edge>();
  foreach(var e in roads){var d=e.B-e.A;if(d.Length<.01)continue;
   if((BoundarySnap.Nearest(seed,new SnapEdge(e.A,e.B,1))-seed).Length<=e.Width/2+1)return empty;
   var n=new Point(-d.Y,d.X)*(1/d.Length);if(BoundarySnap.Dot(seed-e.A,n)<0)n=n*(-1);
   edges.Add(new Edge(e.A+n*(e.Width/2+1),e.B+n*(e.Width/2+1),0));
  }
  const int count=96;var points=new Point[count];var hit=new bool[count];int hits=0;
  for(int i=0;i<count;i++){var ray=Directions[i];double best=radius;
   foreach(var e in edges){var d=e.B-e.A;double cross=Layout.Cross(ray,d);if(Math.Abs(cross)<1e-8)continue;double t=Layout.Cross(e.A-seed,d)/cross,u=Layout.Cross(e.A-seed,ray)/cross;
    if(t>0&&t<best&&u>=0&&u<=1){best=t;hit[i]=true;}}
   if(hit[i]){points[i]=seed+ray*best;hits++;}
  }
  if(hits<count/2)return empty;
  int gap=0;for(int i=0;i<count*2;i++){gap=hit[i%count]?0:gap+1;if(gap>=count/2)return empty;}
  var polygon=new List<Point>();for(int i=0;i<count;i++)if(hit[i])polygon.Add(points[i]);
  // Remove only nearly collinear samples. Preserve curved curb and concave changes.
  bool changed=true;while(changed&&polygon.Count>3){changed=false;for(int i=0;i<polygon.Count;i++){var a=polygon[(i+polygon.Count-1)%polygon.Count];var b=polygon[i];var c=polygon[(i+1)%polygon.Count];
   if((b-a).Length<1.05||(BoundarySnap.Nearest(b,new SnapEdge(a,c,1))-b).Length<.35){polygon.RemoveAt(i);changed=true;break;}}}
  if(!Layout.Valid(polygon)||!Layout.Inside(seed,polygon))return empty;
  // Never silently enclose a road hidden by a nearer road or cross a road at a gap.
  foreach(var road in roads){if(Layout.Inside((road.A+road.B)*.5,polygon))return empty;
   for(int i=0;i<polygon.Count;i++)if(Crosses(polygon[i],polygon[(i+1)%polygon.Count],road.A,road.B))return empty;}
  return polygon;
 }
}
}
