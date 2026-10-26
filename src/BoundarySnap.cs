using System;
using System.Collections.Generic;
namespace ApartmentPlan {
// Constraint solver independent of the game. Finite edges never extend beyond their endpoints.
public struct SnapEdge {
 public Point A,B;public int Kind;public bool Infinite;
 public SnapEdge(Point a,Point b,int kind,bool infinite=false){A=a;B=b;Kind=kind;Infinite=infinite;}
}
public sealed class SnapResult {
 public Point Position,Axis;public int Kind;public bool Close;public readonly List<SnapEdge> Guides=new List<SnapEdge>();
}
public static class BoundarySnap {
 public static double Dot(Point a,Point b)=>a.X*b.X+a.Y*b.Y;
 public static Point Nearest(Point p,SnapEdge e){var d=e.B-e.A;double t=Dot(p-e.A,d)/Math.Max(.000001,Dot(d,d));return e.A+d*(e.Infinite?t:Math.Max(0,Math.Min(1,t)));}
 static bool OnEdge(Point p,SnapEdge e)=>e.Infinite||(Nearest(p,e)-p).Length<.01;
 static int Rank(int kind)=>kind==3?2:kind==1||kind==2?1:0;
 public static SnapResult Solve(Point raw,IList<Point> points,IList<SnapEdge> world,Point axis,double radius,double closeRadius,bool directions,bool guides,bool closing=true){
  var result=new SnapResult{Position=raw};if(closing&&points.Count>=3&&(raw-points[0]).Length<=closeRadius){result.Position=points[0];result.Close=true;result.Kind=7;return result;}
  var candidates=new List<SnapEdge>();foreach(var e in world)if((Nearest(raw,e)-raw).Length<=radius)candidates.Add(e);
  if(axis.Length<.5)axis=new Point(1,0);else axis=axis*(1/axis.Length);
  if(directions&&points.Count>0){var last=points[points.Count-1];var d=points.Count>1?last-points[points.Count-2]:axis;if(d.Length>.1){d=d*(1/d.Length);candidates.Add(new SnapEdge(last,last+d,4,true));candidates.Add(new SnapEdge(last,last+new Point(-d.Y,d.X),4,true));}}
  if(guides)foreach(var p in points){candidates.Add(new SnapEdge(p,p+axis,5,true));candidates.Add(new SnapEdge(p,p+new Point(-axis.Y,axis.X),5,true));}
  candidates.RemoveAll(e=>(e.B-e.A).Length<.01||(Nearest(raw,e)-raw).Length>radius);
  if(candidates.Count>128){candidates.Sort((a,b)=>(Nearest(raw,a)-raw).Length.CompareTo((Nearest(raw,b)-raw).Length));candidates.RemoveRange(128,candidates.Count-128);}
  int bestRank=-1;double bestQuality=double.NegativeInfinity;
  void Offer(Point p,SnapEdge a,SnapEdge? b){double distance=(p-raw).Length;if(distance>radius)return;int rank=Math.Max(Rank(a.Kind),b.HasValue?Rank(b.Value.Kind):0);double quality=(b.HasValue?2:1)*(1-distance/radius);if(rank<bestRank||rank==bestRank&&quality<=bestQuality)return;
   bestRank=rank;bestQuality=quality;result.Position=p;result.Kind=b.HasValue?6:a.Kind;var leading=b.HasValue&&Rank(b.Value.Kind)>Rank(a.Kind)?b.Value:a;var d=leading.B-leading.A;result.Axis=d*(1/d.Length);result.Guides.Clear();result.Guides.Add(a);if(b.HasValue)result.Guides.Add(b.Value);
  }
  for(int i=0;i<candidates.Count;i++){var a=candidates[i];Offer(Nearest(raw,a),a,null);for(int j=0;j<i;j++){var b=candidates[j];var u=a.B-a.A;var v=b.B-b.A;double den=Layout.Cross(u,v);if(Math.Abs(den)<.00001)continue;var p=a.A+u*(Layout.Cross(b.A-a.A,v)/den);if(OnEdge(p,a)&&OnEdge(p,b))Offer(p,a,b);}}
  return result;
 }
}
}
