using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
public static class VillaGeometry {
 public static Point Unit(Point p)=>p.Length>.00001?p*(1/p.Length):new Point();
 public static Point Nearest(Point p,Point a,Point b){var d=b-a;double ll=SiteFrame.Dot(d,d);return ll<1e-8?a:a+d*Math.Max(0,Math.Min(1,SiteFrame.Dot(p-a,d)/ll));}
 public static Point[] Box(Point c,Point forward,double w,double d){var side=new Point(forward.Y,-forward.X);return new[]{c-side*(w/2)-forward*(d/2),c+side*(w/2)-forward*(d/2),c+side*(w/2)+forward*(d/2),c-side*(w/2)+forward*(d/2)};}
 public static bool Overlap(IList<Point> a,IList<Point> b){foreach(var poly in new[]{a,b})for(int i=0;i<poly.Count;i++){var edge=poly[(i+1)%poly.Count]-poly[i];var n=new Point(-edge.Y,edge.X);double amin=a.Min(p=>SiteFrame.Dot(p,n)),amax=a.Max(p=>SiteFrame.Dot(p,n)),bmin=b.Min(p=>SiteFrame.Dot(p,n)),bmax=b.Max(p=>SiteFrame.Dot(p,n));if(amax<=bmin+.00001||bmax<=amin+.00001)return false;}return true;}
 public static bool Inside(IList<Point> shape,IList<Point> poly){
  if(shape.Any(p=>!Layout.Inside(p,poly)))return false;
  for(int i=0;i<shape.Count;i++){var a=shape[i];var b=shape[(i+1)%shape.Count];if(!Layout.Inside((a+b)*.5,poly))return false;
   for(int j=0;j<poly.Count;j++){var c=poly[j];var d=poly[(j+1)%poly.Count];if(Layout.Cross(b-a,c-a)*Layout.Cross(b-a,d-a)<-1e-8&&Layout.Cross(d-c,a-c)*Layout.Cross(d-c,b-c)<-1e-8)return false;}}
  return true;
 }
 public static Parcel Bounds(IList<Point> points,int kind=0)=>new Parcel{Kind=kind,X=points.Min(p=>p.X),Y=points.Min(p=>p.Y),W=points.Max(p=>p.X)-points.Min(p=>p.X),D=points.Max(p=>p.Y)-points.Min(p=>p.Y)};
 public static List<PlanBezier> Contour(IList<Point> poly,double offset){
  var vertices=new List<Point>();double winding=Layout.SignedArea(poly)>0?1:-1;
  for(int i=0;i<poly.Count;i++){var a=Unit(poly[i]-poly[(i+poly.Count-1)%poly.Count]);var b=Unit(poly[(i+1)%poly.Count]-poly[i]);var na=new Point(-a.Y,a.X)*winding;var nb=new Point(-b.Y,b.X)*winding;double den=1+SiteFrame.Dot(na,nb);if(den<.15)return new List<PlanBezier>();vertices.Add(poly[i]+(na+nb)*(offset/den));}
  var before=new Point[vertices.Count];var after=new Point[vertices.Count];
  for(int i=0;i<vertices.Count;i++){var prev=vertices[(i+vertices.Count-1)%vertices.Count]-vertices[i];var next=vertices[(i+1)%vertices.Count]-vertices[i];double trim=Math.Min(16,Math.Min(prev.Length,next.Length)*.3);before[i]=vertices[i]+Unit(prev)*trim;after[i]=vertices[i]+Unit(next)*trim;}
  var curves=new List<PlanBezier>();for(int i=0;i<vertices.Count;i++){var v=vertices[i];curves.Add(new PlanBezier{A=before[i],B=before[i]+(v-before[i])*.6666667,C=after[i]+(v-after[i])*.6666667,D=after[i]});int j=(i+1)%vertices.Count;curves.Add(new PlanBezier{A=after[i],B=after[i]+(before[j]-after[i])*(1d/3),C=after[i]+(before[j]-after[i])*(2d/3),D=before[j]});}return curves;
 }
 public static List<Point> Sample(PlanBezier curve,double step=2){int count=Math.Max(2,(int)Math.Ceiling(((curve.B-curve.A).Length+(curve.C-curve.B).Length+(curve.D-curve.C).Length)/step));return Enumerable.Range(0,count+1).Select(i=>curve.At(i/(double)count)).ToList();}
 public static List<List<Point>> BoundarySides(IList<Point> poly){
  int n=poly.Count;var corners=Enumerable.Range(0,n).OrderByDescending(i=>1-SiteFrame.Dot(Unit(poly[i]-poly[(i+n-1)%n]),Unit(poly[(i+1)%n]-poly[i]))).Take(4).OrderBy(i=>i).ToArray();var sides=new List<List<Point>>();
  for(int k=0;k<corners.Length;k++){int end=corners[(k+1)%corners.Length];var side=new List<Point>{poly[corners[k]]};for(int i=(corners[k]+1)%n;i!=end;i=(i+1)%n)side.Add(poly[i]);side.Add(poly[end]);sides.Add(side);}return sides;
 }
 public static Point ChainMidpoint(IList<Point> side){double length=Enumerable.Range(1,side.Count-1).Sum(i=>(side[i]-side[i-1]).Length),run=0;for(int i=1;i<side.Count;i++){double d=(side[i]-side[i-1]).Length;if(run+d>=length/2)return side[i-1]+(side[i]-side[i-1])*((length/2-run)/d);run+=d;}return side[0];}
 public static double MidpointPenalty(IList<Point> boundary,Point point){var sides=BoundarySides(boundary);var side=sides.OrderBy(s=>Enumerable.Range(1,s.Count-1).Min(i=>(Nearest(point,s[i-1],s[i])-point).Length)).First();double length=Enumerable.Range(1,side.Count-1).Sum(i=>(side[i]-side[i-1]).Length);return(point-ChainMidpoint(side)).Length/Math.Max(1,length);}
 // Four boundary chains define a curvilinear grid independently of the arterials.
 public static List<PlanBezier> BoundaryGrid(IList<Point> poly,double inset,double spacing,double frontage=0,double houseDepth=0,double roadWidth=0,double setback=0){
  int n=poly.Count;var corners=Enumerable.Range(0,n).OrderByDescending(i=>1-SiteFrame.Dot(Unit(poly[i]-poly[(i+n-1)%n]),Unit(poly[(i+1)%n]-poly[i]))).Take(4).OrderBy(i=>i).ToArray();
  if(corners.Length<4)return new List<PlanBezier>();double winding=Layout.SignedArea(poly)>0?1:-1;var inner=new List<Point>();
  for(int i=0;i<n;i++){var a=Unit(poly[i]-poly[(i+n-1)%n]);var b=Unit(poly[(i+1)%n]-poly[i]);var na=new Point(-a.Y,a.X)*winding;var nb=new Point(-b.Y,b.X)*winding;double den=1+SiteFrame.Dot(na,nb);if(den<.15)return new List<PlanBezier>();inner.Add(poly[i]+(na+nb)*(inset/den));}
  List<Point> Chain(int from,int to){var result=new List<Point>{inner[from]};for(int i=(from+1)%n;i!=to;i=(i+1)%n)result.Add(inner[i]);result.Add(inner[to]);return result;}
  var bottom=Chain(corners[0],corners[1]);var right=Chain(corners[1],corners[2]);var top=Chain(corners[2],corners[3]);top.Reverse();var left=Chain(corners[3],corners[0]);left.Reverse();
  double Length(List<Point> a)=>a.Skip(1).Select((q,i)=>(q-a[i]).Length).Sum();
  Point Along(List<Point> a,double t){double target=Math.Max(0,Math.Min(1,t))*Length(a),run=0;for(int i=1;i<a.Count;i++){double d=(a[i]-a[i-1]).Length;if(target<=run+d||i==a.Count-1){double u=(target-run)/Math.Max(.001,d);var p0=a[Math.Max(0,i-2)];var p1=a[i-1];var p2=a[i];var p3=a[Math.Min(a.Count-1,i+1)];var m1=Unit(p2-p0)*d;var m2=Unit(p3-p1)*d;return p1*(2*u*u*u-3*u*u+1)+m1*(u*u*u-2*u*u+u)+p2*(-2*u*u*u+3*u*u)+m2*(u*u*u-u*u);}run+=d;}return a.Last();}
  Point Map(double u,double v){var blend=bottom[0]*((1-u)*(1-v))+bottom.Last()*(u*(1-v))+top[0]*((1-u)*v)+top.Last()*(u*v);return Along(bottom,u)*(1-v)+Along(top,u)*v+Along(left,v)*(1-u)+Along(right,v)*u-blend;}
  int cols=Math.Max(1,(int)Math.Floor((Length(bottom)+Length(top))*.5/spacing)),rows=Math.Max(1,(int)Math.Floor((Length(left)+Length(right))*.5/spacing));
  // Soft preference for blocks with at least a pair of buildable lots. Keep
  // the original division available; never reject a site for odd capacities.
  if(frontage>0&&houseDepth>0){
   double Score(int cx,int cy){double score=0;
    for(int i=0;i<cx;i++)for(int j=0;j<cy;j++){
     var a=Map(i/(double)cx,j/(double)cy);var b=Map((i+1d)/cx,j/(double)cy);var c=Map((i+1d)/cx,(j+1d)/cy);var d=Map(i/(double)cx,(j+1d)/cy);
     double w=Math.Max(0,Math.Min((b-a).Length,(c-d).Length)-roadWidth-2*setback),h=Math.Max(0,Math.Min((d-a).Length,(c-b).Length)-roadWidth-2*setback);
     double Fit(double x,double y){int along=(int)Math.Floor((x+.01)/frontage),rows=Math.Min(2,(int)Math.Floor((y+.01)/houseDepth));int capacity=along*rows;int paired=(along/2*2)*rows;if(rows==2)paired=capacity;
      double unused=x*y-capacity*frontage*houseDepth;return paired*2+(capacity-paired)*.25-unused/(frontage*houseDepth)*.2-(capacity<2?2:0);}
     // A long single-row strip must not outscore a distributed street grid.
     double narrow=Math.Max(0,houseDepth*2-Math.Min(w,h))/houseDepth;
     double longBlock=Math.Max(0,Math.Max(w,h)-Math.Max(80,frontage*6))/frontage;
     score+=Math.Max(Fit(w,h),Fit(h,w))-narrow*12-longBlock*12;
    }return score;
   }
   int baseCols=cols,baseRows=rows;double best=Score(cols,rows);
   for(int cx=Math.Max(1,baseCols-2);cx<=baseCols+2;cx++)for(int cy=Math.Max(1,baseRows-2);cy<=baseRows+2;cy++){
    double score=Score(cx,cy)-.5*(Math.Abs(cx-baseCols)+Math.Abs(cy-baseRows));if(score>best+.001){best=score;cols=cx;rows=cy;}
   }
  }
  // Elongated sites need a ladder, not small cells that are later pruned
  // into an uneven collection of short cross streets.
  double across=(Length(bottom)+Length(top))*.5,along=(Length(left)+Length(right))*.5;
  if(frontage>0&&Math.Max(across,along)>Math.Min(across,along)*2.2){
   double crossPitch=Math.Max(80,frontage*6);
   if(across>along){cols=Math.Max(1,(int)Math.Round(across/crossPitch));rows=Math.Max(1,(int)Math.Floor(along/(houseDepth*2+roadWidth)));}
   else{rows=Math.Max(1,(int)Math.Round(along/crossPitch));cols=Math.Max(1,(int)Math.Floor(across/(houseDepth*2+roadWidth)));}
  }
  var result=new List<PlanBezier>();
  void Curve(Func<double,Point> eval){const int parts=12;for(int i=0;i<parts;i++){double a=i/(double)parts,b=(i+1)/(double)parts,h=.0001;var p=eval(a);var q=eval(b);var da=(eval(Math.Min(1,a+h))-eval(Math.Max(0,a-h)))*(1/(Math.Min(1,a+h)-Math.Max(0,a-h)));var db=(eval(Math.Min(1,b+h))-eval(Math.Max(0,b-h)))*(1/(Math.Min(1,b+h)-Math.Max(0,b-h)));result.Add(new PlanBezier{A=p,B=p+da*((b-a)/3),C=q-db*((b-a)/3),D=q});}}
  for(int i=0;i<=cols;i++){double u=i/(double)cols;Curve(t=>Map(u,t));}for(int j=0;j<=rows;j++){double v=j/(double)rows;Curve(t=>Map(t,v));}return result;
 }
 // At a junction only a continuation of an existing arm is eligible; occupied
 // directions and diagonal fifth arms are never manufactured. Mid-edge joins are perpendicular.
 public static List<Point> Approaches(VillaConnection gate){
  var arms=(gate.Arms??new List<Point>()).Where(a=>a.Length>.1).Select(Unit).ToList();
  if(gate.Junction){return arms.Select(a=>a*-1).Where(a=>!arms.Any(b=>SiteFrame.Dot(a,b)>.94)).ToList();}
  if(gate.Tangent.Length<.1)return new List<Point>();var t=Unit(gate.Tangent);return new List<Point>{new Point(-t.Y,t.X),new Point(t.Y,-t.X)};
 }
 public static bool RayEntry(Point origin,Point direction,IList<Point> boundary,out Point entry){double best=double.MaxValue;entry=default;
  for(int i=0;i<boundary.Count;i++){var a=boundary[i];var d=boundary[(i+1)%boundary.Count]-a;double cross=Layout.Cross(direction,d);if(Math.Abs(cross)<1e-8)continue;double t=Layout.Cross(a-origin,d)/cross,u=Layout.Cross(a-origin,direction)/cross;if(t>0&&t<best&&u>=0&&u<=1){var q=origin+direction*t;if(Layout.Inside(q+direction*.5,boundary)){best=t;entry=q;}}}return best<double.MaxValue;
 }
}
}
