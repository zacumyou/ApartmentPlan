using System;
using System.Collections.Generic;
using System.Linq;

namespace ApartmentPlan {
internal sealed partial class ResidentialLayout {
 double CurveScale=>Math.Max(.25,Math.Min(1,o.MainCurveLengthRatio));
 readonly List<Point> entranceDirections=new List<Point>();
 bool TryMainTJunction(int source){
  var start=entranceTips[source];var direction=entranceDirections[source];
  var roads=plan.Routes.Where(r=>r.Use=="main").ToList();
  var candidates=new List<Tuple<Point,Point>>();double margin=Math.Max(12,o.Road*1.5);
  foreach(var route in roads)for(int i=1;i<route.Points.Count;i++){
   var a=route.Points[i-1];var delta=route.Points[i]-a;double length=delta.Length;
   if(length<margin*2)continue;
   var middle=a+delta*.5;
   if(route.Curves.Any(c=>(c.At(c.Parameter(middle))-middle).Length<.1))continue;
   var tangent=delta*(1/length);var normal=new Point(-tangent.Y,tangent.X);
   if(SiteFrame.Dot(a-start,normal)<0)normal=normal*-1;
   double projected=SiteFrame.Dot(start-a,tangent);
   var offsets=new List<double>{projected};
   double cross=Layout.Cross(direction,delta);
   if(Math.Abs(cross)>.001)offsets.Add(Layout.Cross(a-start,direction)/cross*length);
   foreach(double offset in new[]{-margin,margin,-2*margin,2*margin})offsets.Add(projected+offset);
   foreach(double offset in offsets){
    var end=a+tangent*Math.Max(margin,Math.Min(length-margin,offset));
    if((end-start).Length<o.Road)continue;
    if(roads.Any(r=>r.Curves.Any(c=>(c.At(c.Parameter(end))-end).Length<margin)))continue;
    // Keep spacing from existing branches as well as curve endpoints.
    if(roads.Any(r=>(r.Points.First()-end).Length<margin||(r.Points.Last()-end).Length<margin))continue;
    candidates.Add(Tuple.Create(end,normal));
   }
  }
  foreach(var candidate in candidates.OrderBy(c=>(c.Item1-start).Length)){
   var end=candidate.Item1;var approach=candidate.Item2;var skeletons=new List<List<Point>>();
   var d=end-start;
   if(Math.Abs(Layout.Cross(d,direction))<.01&&SiteFrame.Dot(d,direction)>0&&SiteFrame.Dot(direction,approach)>.999)skeletons.Add(new List<Point>{start,end});
   double cross=Layout.Cross(direction,approach);
   if(Math.Abs(cross)>.001){
    double along=Layout.Cross(d,approach)/cross,back=Layout.Cross(d,direction)/cross;
    if(along>margin&&back>margin)skeletons.Add(new List<Point>{start,start+direction*along,end});
   }else if(SiteFrame.Dot(direction,approach)>.999){
    double along=SiteFrame.Dot(d,direction);
    if(along>margin*2)foreach(double ratio in new[]{.5,.35,.65}){
     var first=start+direction*(along*ratio);var last=end-direction*(along*(1-ratio));
     skeletons.Add(new List<Point>{start,first,last,end});
    }
   }
   foreach(var skeleton in skeletons){
    double minimum=Math.Max(o.Road*1.5,8),desired=Math.Max(o.MainCurveRadius,minimum);
    foreach(double radius in new[]{desired,Math.Max(minimum,desired*.8),minimum}){
     var branch=RoundRoute(skeleton,radius);if(branch==null)continue;
     // The final approach must stay straight far enough before its T node.
     if(branch.Curves.Any(c=>(c.D-end).Length<margin))continue;
     if(!ClearMainBranch(branch,roads,end))continue;
     if(PaintCurvedMain(branch))return true;
    }
   }
  }
  return false;
 }
 bool ClearMainBranch(PlanRoute branch,List<PlanRoute> roads,Point end){
  for(int i=1;i<branch.Points.Count;i++)foreach(var road in roads)for(int j=1;j<road.Points.Count;j++){
   var a=branch.Points[i-1];var u=branch.Points[i]-a;var b=road.Points[j-1];var v=road.Points[j]-b;double cross=Layout.Cross(u,v);
   if(Math.Abs(cross)<1e-8){if(Math.Abs(Layout.Cross(b-a,u))<.001&&u.Length>.01){double x=SiteFrame.Dot(b-a,u)/SiteFrame.Dot(u,u),y=SiteFrame.Dot(b+v-a,u)/SiteFrame.Dot(u,u);if(Math.Min(1,Math.Max(x,y))-Math.Max(0,Math.Min(x,y))>.001)return false;}continue;}
   double t=Layout.Cross(b-a,v)/cross,k=Layout.Cross(b-a,u)/cross;
   if(t>=-1e-7&&t<=1+1e-7&&k>=-1e-7&&k<=1+1e-7&&(a+u*t-end).Length>.03)return false;
  }
  return true;
 }

 static List<Point> SimplifyRoute(IList<Point> points){
  var result=new List<Point>();foreach(var p in points){
   if(result.Count>0&&(p-result[result.Count-1]).Length<.01)continue;
   if(result.Count>1){var a=result[result.Count-1]-result[result.Count-2];var b=p-result[result.Count-1];if(Math.Abs(Layout.Cross(a,b))<.001&&SiteFrame.Dot(a,b)>0)result.RemoveAt(result.Count-1);}
   result.Add(p);
  }return result;
 }
 PlanRoute RoundRoute(IList<Point> input,double wanted){
  var corners=SimplifyRoute(input);if(corners.Count<2)return null;
  var radii=new double[corners.Count];var angles=new double[corners.Count];
  for(int i=1;i<corners.Count-1;i++){
   var a=corners[i]-corners[i-1];var b=corners[i+1]-corners[i];
   double angle=Math.Acos(Math.Max(-1,Math.Min(1,SiteFrame.Dot(a,b)/(a.Length*b.Length))));
   if(angle<.001||angle>Math.PI/2+.001)return null;
   angles[i]=angle;radii[i]=wanted*Math.Tan(angle/2); // tangent distance, not radius
  }
  // A lone corner may use almost the full approach. Adjacent corners share
  // their connecting segment rather than every corner losing 55 percent.
  for(int i=1;i<corners.Count;i++){double sum=radii[i-1]+radii[i],available=(corners[i]-corners[i-1]).Length*.95;if(sum>available){double scale=available/sum;radii[i-1]*=scale;radii[i]*=scale;}}
  var route=new PlanRoute{Use="main",Width=o.Road};route.Points.Add(corners[0]);
  for(int i=1;i<corners.Count-1;i++){
   var incoming=corners[i]-corners[i-1];var outgoing=corners[i+1]-corners[i];double before=incoming.Length,after=outgoing.Length;
   var u=incoming*(1/before);var v=outgoing*(1/after);
   double tangent=radii[i]*CurveScale,radius=tangent/Math.Tan(angles[i]/2);
   if(radius<Math.Max(o.Road*.5+1,Math.Max(o.Road*1.5,8)*CurveScale)-.001)return null;
   var start=corners[i]-u*tangent;var end=corners[i]+v*tangent;
   double handle=4.0/3*radius*Math.Tan(angles[i]/4);
   var curve=new PlanBezier{A=start,B=start+u*handle,C=end-v*handle,D=end};
   route.Curves.Add(curve);route.Points.Add(start);
   int steps=Math.Max(6,(int)Math.Ceiling(angles[i]*radius/cell));
   for(int n=1;n<=steps;n++)route.Points.Add(curve.At((double)n/steps));
   route.CornerRadii.Add(radius);
  }
  route.Points.Add(corners[corners.Count-1]);return route;
 }
 bool PaintCurvedMain(PlanRoute route){
  if(route==null)return false;
  var cells=new HashSet<int>();double half=route.Width/2;
  // Conservative cell intersection: the real circular ribbon lies entirely
  // inside these reserved road cells. Test the SAME ribbon used for parking exclusion.
  for(int n=1;n<route.Points.Count;n++){
   var a=route.Points[n-1];var b=route.Points[n];var delta=b-a;double length2=SiteFrame.Dot(delta,delta);if(length2<1e-8)continue;
   int left=X(Math.Min(a.X,b.X)-half-cell),right=X(Math.Max(a.X,b.X)+half+cell),bottom=Y(Math.Min(a.Y,b.Y)-half-cell),top=Y(Math.Max(a.Y,b.Y)+half+cell);
   for(int y=bottom;y<=top;y++)for(int x=left;x<=right;x++){
    var center=new Point(x0+(x+.5)*cell,y0+(y+.5)*cell);double t=Math.Max(0,Math.Min(1,SiteFrame.Dot(center-a,delta)/length2));
    if((center-a-delta*t).Length>half+cell*.707107)continue;
    if(x<0||y<0||x>=nx||y>=ny||reserved[x,y]||edgeGreen[x,y]&&map[x,y]!=1&&!EntranceApproach(x,y)||map[x,y]==Outside||map[x,y]==Fixed||map[x,y]==3||map[x,y]==5)return false;
    cells.Add(y*nx+x);
   }
  }
  foreach(int n in cells)map[n%nx,n/nx]=1;
  plan.Routes.Add(route);return true;
 }
 bool TryMainSkeleton(IList<Point> skeleton){
  double minimum=Math.Max(o.Road*1.5,8),desired=Math.Max(o.MainCurveRadius,minimum);
  foreach(double radius in new[]{desired,Math.Max(minimum,desired*.8),minimum})if(PaintCurvedMain(RoundRoute(skeleton,radius)))return true;
  return false;
 }
 bool TryShallowOffset(Point a,Point b,Point direction){
  // A small lateral offset cannot fit two orthogonal fillets, but can fit a
  // long shallow S curve. Linear longitudinal progress prevents loops.
  double length=SiteFrame.Dot(b-a,direction);if(length<=0)return false;
  var lateral=b-a-direction*length;
  double minimum=Math.Max(o.Road*.5+1,Math.Max(o.MainCurveRadius,Math.Max(o.Road*1.5,8))*CurveScale);
  double transition=length*CurveScale,lead=(length-transition)/2;
  // |curvature| <= 6*offset/length^2 for this smoothstep curve.
  double radius=lateral.Length<.001?double.MaxValue:transition*transition/(6*lateral.Length);
  if(radius<minimum)return false;
  var route=new PlanRoute{Use="main",Width=o.Road};route.CornerRadii.Add(radius);
  route.Curves.Add(new PlanBezier{A=a+direction*lead,B=a+direction*(lead+transition/3),C=a+direction*(lead+transition*2/3)+lateral,D=a+direction*(lead+transition)+lateral});
  if(lead>.001)route.Points.Add(a);
  int steps=Math.Max(24,(int)Math.Ceiling((transition+1.5*lateral.Length)/cell));
  for(int i=0;i<=steps;i++){double t=(double)i/steps;route.Points.Add(a+direction*(lead+transition*t)+lateral*(t*t*(3-2*t)));}
  if(lead>.001)route.Points.Add(b);
  return PaintCurvedMain(route);
 }
 void ConnectMainEntrances(int source,int target){
  var a=entranceTips[source];var b=entranceTips[target];var u=entranceDirections[source];var v=entranceDirections[target];
  if(o.MainRoadCornerMode==2){ConnectAngularMain(a,b);return;}
  if(Math.Abs(Layout.Cross(b-a,u))<.01&&SiteFrame.Dot(u,v)<-.9){if(TryMainSkeleton(new[]{a,b}))return;}
  // Offset opposite entrances get a central S bend, with tangent approach at
  // both gates. Do not put the entire lateral shift at a gate as a sharp dogleg.
  if(SiteFrame.Dot(u,v)<-.9){
   // Replace the transverse 90 + 90 bend with a 45-degree connector where
   // both entrance leads and the complete curved road ribbon fit.
   double length=SiteFrame.Dot(b-a,u);var side=b-a-u*length;double offset=side.Length;
   if(offset>=o.Road&&length-offset>o.Road*2){
    foreach(double fraction in new[]{.5,.35,.65,.2,.8}){
     var c=a+u*((length-offset)*fraction);var d=c+u*offset+side;
     if(TryMainSkeleton(new[]{a,c,d,b}))return;
    }
   }
   foreach(double fraction in new[]{.5,.35,.65,.2,.8}){
    var middle=a+(b-a)*fraction;
    var c=Math.Abs(u.X)>.5?new Point(middle.X,a.Y):new Point(a.X,middle.Y);
    var d=Math.Abs(u.X)>.5?new Point(middle.X,b.Y):new Point(b.X,middle.Y);
    if(SiteFrame.Dot(c-a,u)>0&&SiteFrame.Dot(d-b,v)>0&&TryMainSkeleton(new[]{a,c,d,b}))return;
   }
   if(TryShallowOffset(a,b,u))return;
  }else if(Math.Abs(SiteFrame.Dot(u,v))<.1){
   var corner=Math.Abs(u.X)>.5?new Point(b.X,a.Y):new Point(a.X,b.Y);
   if(SiteFrame.Dot(corner-a,u)>0&&SiteFrame.Dot(corner-b,v)>0&&TryMainSkeleton(new[]{a,corner,b}))return;
  }
  double lead=Math.Max(o.MainCurveRadius*2*CurveScale,o.Road*3*CurveScale);var start=a+u*lead;var end=b+v*lead;
  int tx=X(end.X),ty=Y(end.Y);var path=FindRoute(start,(x,y)=>x==tx&&y==ty,o.Road);
  if(path!=null){path.Insert(0,a);path.Add(b);if(TryMainSkeleton(path))return;}
  // Distinguish no vehicular corridor from a corridor too tight for the fillets.
  if(o.MainRoadCornerMode==0){ConnectAngularMain(a,b);return;}
  tx=X(b.X);ty=Y(b.Y);if(FindRoute(a,(x,y)=>x==tx&&y==ty,o.Road)==null)throw new ArgumentException("DisconnectedEntrance");
  throw new ArgumentException("CurveFit");
 }
 void ConnectAngularMain(Point a,Point b){
  int tx=X(b.X),ty=Y(b.Y);
  // The width-aware router still excludes masks, the green border and narrow
  // necks. Only the corner-radius restriction is relaxed.
  var path=FindRoute(a,(x,y)=>x==tx&&y==ty,o.Road);
  if(path==null)throw new ArgumentException("DisconnectedEntrance");
  DrawRoute(path,o.Road,"main");
 }
}
}
