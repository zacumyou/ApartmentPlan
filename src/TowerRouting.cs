using System;
using System.Linq;
using System.Collections.Generic;
namespace ApartmentPlan {
// Orthogonal visibility lattice in the building frame. Search carries the distance
// since the last bend so smoothing cannot invent sub-radius corners afterwards.
public sealed class TowerRouting {
 readonly Plan p;readonly IList<Point> poly;readonly List<Parcel> lots;readonly List<Point[]> excluded;readonly Point u,v;readonly LayoutOptions o;readonly TowerRules r;
 public readonly List<Point> Anchors=new List<Point>();readonly List<PlanRoute> connected=new List<PlanRoute>();
 public TowerRouting(Plan p,IList<Point> poly,List<Parcel> lots,List<Point[]> excluded,Point u,Point v,LayoutOptions o,TowerRules r){this.p=p;this.poly=poly;this.lots=lots;this.excluded=excluded;this.u=u;this.v=v;this.o=o;this.r=r;}
 Point UV(Point q)=>new Point(SiteFrame.Dot(q,u),SiteFrame.Dot(q,v));Point XY(Point q)=>u*q.X+v*q.Y;
 readonly Dictionary<string,int> rejectedReasons=new Dictionary<string,int>();
 bool Reject(string reason){rejectedReasons[reason]=rejectedReasons.TryGetValue(reason,out var n)?n+1:1;return false;}
 bool Clear(Point a,Point b,bool branch=false){if((b-a).Length<.001)return true;var shape=VillaGeometry.Box((a+b)*.5,VillaGeometry.Unit(b-a),o.ParkingAisle-.02,(b-a).Length);
  if(!branch&&!TurnClear(a,b))return Reject("AP.LayoutReason.TurnGap");
  if(!VillaGeometry.Inside(shape,poly))return Reject("AP.LayoutReason.Boundary");
  if(lots.Any(l=>VillaGeometry.Overlap(shape,l.PlacementOutline)))return Reject("AP.LayoutReason.Building");
  if(excluded.Any(q=>VillaGeometry.Overlap(shape,q)))return Reject("AP.LayoutReason.Excluded");return true;
 }

 // A single explicit branch crosses each court clearance belt. All searched
 // roads start outside that belt; proximity alone never grants an exception.
 public static double SegmentDistance(Point a,Point b,Point c,Point d){
  var u=b-a;var v=d-c;double cross=Layout.Cross(u,v);
  if(Math.Abs(cross)>1e-9){double t=Layout.Cross(c-a,v)/cross,k=Layout.Cross(c-a,u)/cross;if(t>=0&&t<=1&&k>=0&&k<=1)return 0;}
  return Math.Min(Math.Min((a-VillaGeometry.Nearest(a,c,d)).Length,(b-VillaGeometry.Nearest(b,c,d)).Length),Math.Min((c-VillaGeometry.Nearest(c,a,b)).Length,(d-VillaGeometry.Nearest(d,a,b)).Length));
 }
 readonly List<Tuple<Point,Point,double>> courtEdges=new List<Tuple<Point,Point,double>>();
 bool TurnClear(Point a,Point b)=>courtEdges.All(e=>
  Math.Max(a.X,b.X)<Math.Min(e.Item1.X,e.Item2.X)-e.Item3 || Math.Min(a.X,b.X)>Math.Max(e.Item1.X,e.Item2.X)+e.Item3 ||
  Math.Max(a.Y,b.Y)<Math.Min(e.Item1.Y,e.Item2.Y)-e.Item3 || Math.Min(a.Y,b.Y)>Math.Max(e.Item1.Y,e.Item2.Y)+e.Item3 ||
  SegmentDistance(a,b,e.Item1,e.Item2)>=e.Item3-.001);
 bool PrepareBranches(int attempt){
  courtEdges.Clear();
  var rings=p.Routes.Where(q=>q.Use=="turnaround").ToList();
  foreach(var ring in rings)foreach(var curve in ring.Curves){var pts=VillaGeometry.Sample(curve,.5);for(int i=1;i<pts.Count;i++)courtEdges.Add(Tuple.Create(pts[i-1],pts[i],(ring.Width+o.ParkingAisle)/2+r["TurnaroundGap"]+.02));}
  for(int i=0;i<Anchors.Count;i++){
   var anchor=Anchors[i];var ring=rings.OrderBy(q=>q.Points.Min(x=>(x-anchor).Length)).First();
   var center=new Point(ring.Points.Average(q=>q.X),ring.Points.Average(q=>q.Y));var outward=VillaGeometry.Unit(anchor-center);
   bool found=false;
   var directions=new[]{u,u*-1,v,v*-1}.Where(q=>SiteFrame.Dot(q,outward)>=.2).OrderByDescending(q=>SiteFrame.Dot(q,outward)).ToList();
   if(((attempt>>i)&1)!=0)directions.Reverse();
   foreach(var dir in directions){
    if(SiteFrame.Dot(dir,outward)<.2)continue;
    for(double length=12;length<=32;length+=2){var end=anchor+dir*length;
     if(!TurnClear(end,end))continue;
     if(!Clear(anchor,end,true))break;
     p.Routes.Add(TowerGeometry.Route(new[]{TowerGeometry.Line(anchor,end)},"aisle",o.ParkingAisle));Anchors[i]=end;found=true;break;
    }if(found)break;
   }
   if(!found)return Reject("AP.LayoutReason.GateBranch");
  }return true;
 }
 readonly List<PlanBezier> fronts=new List<PlanBezier>();
 public static bool FrontCurveClear(PlanBezier curve,PlanBezier front,double gap){
  return FrontCurveClear(curve,front,gap,VillaGeometry.Sample(curve,.5));
 }
 static bool FrontCurveClear(PlanBezier curve,PlanBezier front,double gap,List<Point> points){
  var dir=VillaGeometry.Unit(front.D-front.A);double length=(front.D-front.A).Length;
  // A tangent continuation beyond the frontage is continuous road, not a new
  // branch tucked into a bend. All of the bend must remain beyond that end.
  foreach(var end in new[]{curve.A,curve.D}){
   var tangent=VillaGeometry.Unit((end-curve.A).Length<.001?curve.B-curve.A:curve.D-curve.C);
   if(Math.Abs(Layout.Cross(dir,tangent))>.0001||Math.Abs(Layout.Cross(dir,end-front.A))>.001)continue;
   var projections=points.Select(q=>SiteFrame.Dot(q-front.A,dir)).ToArray();
   if(projections.Max()<=.001||projections.Min()>=length-.001)return true;
  }
  return points.Skip(1).Select((b,i)=>SegmentDistance(points[i],b,front.A,front.D)).Min()>=gap-.001;
 }
 bool FrontClear(PlanBezier curve){var points=VillaGeometry.Sample(curve,.5);return fronts.All(f=>FrontCurveClear(curve,f,r["FrontCurveGap"],points));}
 bool Clear(PlanRoute route){
  var lines=route.Curves.Where(IsLine).ToArray();for(int i=0;i<lines.Length;i++)for(int j=0;j<i;j++)if(ParallelPair(lines[i].A,lines[i].D,lines[j],r["ParallelGap"]))return false;
  if(route.Curves.Any(c=>!IsLine(c)&&!FrontClear(c)))return false;
  if(!route.Points.Skip(1).Select((q,i)=>Clear(route.Points[i],q)).All(q=>q))return false;
  foreach(var curve in route.Curves.Where(c=>!IsLine(c)))foreach(var old in p.Routes.Where(q=>q.Use!="pedestrian").SelectMany(q=>q.Curves).Where(c=>!IsLine(c))){
   if((curve.A-old.A).Length<.01&&(curve.D-old.D).Length<.01)continue;
   var a=VillaGeometry.Sample(curve,1);var b=VillaGeometry.Sample(old,1);if(a.Any(q=>b.Skip(1).Select((z,i)=>(VillaGeometry.Nearest(q,b[i],z)-q).Length).Min()<r["CurveGap"]-.01))return false;
  }return true;
 }
 static bool ParallelPair(Point a,Point b,PlanBezier curve,double gap){var delta=b-a;double len=delta.Length;var d=curve.D-curve.A;if(len<.01||d.Length<.01||!IsLine(curve))return false;var dir=delta*(1/len);if(Math.Abs(SiteFrame.Dot(dir,VillaGeometry.Unit(d)))<Math.Cos(Math.PI/18))return false;double lateral=Math.Abs(Layout.Cross(dir,curve.A-a)),overlap=Math.Min(len,Math.Max(SiteFrame.Dot(curve.A-a,dir),SiteFrame.Dot(curve.D-a,dir)))-Math.Max(0,Math.Min(SiteFrame.Dot(curve.A-a,dir),SiteFrame.Dot(curve.D-a,dir)));return lateral>.1&&lateral<gap-.001&&overlap>.1;}
 bool Parallel(Point a,Point b)=>fronts.Concat(connected.SelectMany(route=>route.Curves)).Any(c=>ParallelPair(a,b,c,r["ParallelGap"]));
 PlanRoute Add(List<PlanBezier> cs){var route=TowerGeometry.Route(cs,"aisle",o.ParkingAisle);connected.Add(route);p.Routes.Add(route);return route;}
 public void Build(List<Parcel> buildings){
  foreach(var lot in buildings){var v=lot.PlacementForward??this.v;var u=new Point(-v.Y,v.X);var c=new Point(lot.X+lot.W/2,lot.Y+lot.D/2);var mid=c+v*(lot.D/2+o.ParkingAisle/2);fronts.Add(TowerGeometry.Line(mid-u*Math.Max(1,lot.W/2-4),mid+u*Math.Max(1,lot.W/2-4)));}
  int saved=p.Routes.Count;var rawAnchors=Anchors.ToArray();
  for(int attempt=0;attempt<8;attempt++){
   o.Cancellation.ThrowIfCancellationRequested();connected.Clear();p.Routes.RemoveRange(saved,p.Routes.Count-saved);
   Anchors.Clear();Anchors.AddRange(rawAnchors);if(!PrepareBranches(attempt))continue;
   // Linking gates is optional. Later retries reserve space for buildings first.
   int gateLinks=0;
   if(attempt<4)foreach(var anchor in Anchors.Skip(1)){var link=Find(anchor,Anchors.Take(1).ToList());if(link!=null){Add(link);gateLinks++;}}
   var pending=buildings.OrderBy(l=>attempt<4?Anchors.Min(a=>(a-l.Access.Value).Length):SiteFrame.Dot(new Point(l.X,l.Y),new Point(Math.Cos((attempt-2)*Math.PI/3),Math.Sin((attempt-2)*Math.PI/3)))).ToList();var unreachable=new List<Parcel>();
  // Reserve fronts before connecting so one connector cannot steal another building's frontage.
  foreach(var lot in pending){o.Cancellation.ThrowIfCancellationRequested();var v=lot.PlacementForward??this.v;var u=new Point(-v.Y,v.X);var c=new Point(lot.X+lot.W/2,lot.Y+lot.D/2);var mid=c+v*(lot.D/2+o.ParkingAisle/2);var a=mid-u*Math.Max(1,lot.W/2-4);var b=mid+u*Math.Max(1,lot.W/2-4);
   var targets=Anchors.Concat(connected.SelectMany(q=>q.Curves.Where(IsLine).SelectMany(z=>new[]{z.A,z.D,(z.A+z.D)*.5}))).Where(q=>Anchors.All(a=>(a-q).Length<.001||(a-q).Length>=r["BranchGap"]+o.ParkingAisle/2)).ToList();List<PlanBezier> path=null;
   if(!Clear(a,b)){unreachable.Add(lot);continue;}if(Parallel(a,b)){Reject("AP.LayoutReason.Parallel");unreachable.Add(lot);continue;}
   var reservedFront=Add(new List<PlanBezier>{TowerGeometry.Line(a,b)});
   foreach(var start in new[]{mid,a,b}){path=Find(start,targets);if(path!=null)break;}
   if(path==null){Reject("AP.LayoutReason.Search");connected.Remove(reservedFront);p.Routes.Remove(reservedFront);unreachable.Add(lot);continue;}
   if(path.Count>0)Add(path);
  }
   if(unreachable.Count>0)continue;

   // Optional second faces cannot consume the space needed by another frontage.
   foreach(var lot in buildings){var v=lot.PlacementForward??this.v;var u=new Point(-v.Y,v.X);var c=new Point(lot.X+lot.W/2,lot.Y+lot.D/2);var mid=c+v*(lot.D/2+o.ParkingAisle/2);var b=mid+u*Math.Max(1,lot.W/2-4);
   if(r["TwoFaces"]>0){
    // A continuous second face uses the same R as all other road bends.
    double h=o.ParkingAisle/2;var points=new[]{c-u*(lot.W/2+h)-v*(lot.D/2-4),c-u*(lot.W/2+h)+v*(lot.D/2+h),b};var cs=TowerGeometry.Fillet(points,r["RoadRadius"],false,r["CurveGap"]);
    if(cs!=null){var route=TowerGeometry.Route(cs,"aisle",o.ParkingAisle);if(Clear(route)&&!route.Curves.Any(z=>IsLine(z)&&Parallel(z.A,z.D)))Add(cs);}
   }
   }
   p.Diagnostics="road routing attempt="+(attempt+1)+" optionalGateLinks="+gateLinks+" preserved buildings="+buildings.Count;return;
  }
  p.Routes.RemoveRange(saved,p.Routes.Count-saved);connected.Clear();
  var failure=new ArgumentException("AP.Tower.RoutingRetryFit");failure.Data["layoutReasons"]=rejectedReasons;throw failure;
 }
 static bool IsLine(PlanBezier c)=>Math.Abs(Layout.Cross(c.D-c.A,c.B-c.A))<.001&&Math.Abs(Layout.Cross(c.D-c.A,c.C-c.A))<.001;
 sealed class State {public int Node,Dir;public double Run,Cost;public State Prev;}
 List<PlanBezier> Find(Point start,List<Point> targets){
  if(targets.Count==0)return null;if(targets.Any(t=>(t-start).Length<.01))return new List<PlanBezier>();
  var all=poly.Select(UV).ToList();var special=targets.Concat(new[]{start}).Concat(fronts.SelectMany(f=>new[]{f.A,f.D})).Select(UV).ToList();double spacing=8;
  var xs=special.Select(q=>q.X).Concat(Enumerable.Range(0,Math.Max(1,(int)Math.Ceiling((all.Max(q=>q.X)-all.Min(q=>q.X))/spacing))).Select(i=>all.Min(q=>q.X)+i*spacing)).Select(x=>Math.Round(x,4)).Distinct().OrderBy(x=>x).ToArray();
  var ys=special.Select(q=>q.Y).Concat(Enumerable.Range(0,Math.Max(1,(int)Math.Ceiling((all.Max(q=>q.Y)-all.Min(q=>q.Y))/spacing))).Select(i=>all.Min(q=>q.Y)+i*spacing)).Select(x=>Math.Round(x,4)).Distinct().OrderBy(x=>x).ToArray();
  int nx=xs.Length,ny=ys.Length;var nodes=new Point[nx*ny];for(int x=0;x<nx;x++)for(int y=0;y<ny;y++)nodes[x*ny+y]=XY(new Point(xs[x],ys[y]));
  int Index(Point q){var uv=UV(q);return Array.IndexOf(xs,Math.Round(uv.X,4))*ny+Array.IndexOf(ys,Math.Round(uv.Y,4));}
  int source=Index(start);var goals=new HashSet<int>(targets.Select(Index));var queue=new SortedSet<Tuple<double,int,State>>(Comparer<Tuple<double,int,State>>.Create((a,b)=>{int c=a.Item1.CompareTo(b.Item1);return c!=0?c:a.Item2.CompareTo(b.Item2);}));int serial=0;var cost=new Dictionary<(int,int,double),double>();var turnCache=new Dictionary<(int,int,int),bool>();var heuristics=new double?[nodes.Length];var edgeCache=new Dictionary<long,bool>();double minrun=2*r["RoadRadius"]+r["CurveGap"];
  void Push(State s){var key=(s.Node,s.Dir,Math.Round(Math.Min(minrun,s.Run),1));if(cost.TryGetValue(key,out var old)&&old<=s.Cost)return;cost[key]=s.Cost;double h=heuristics[s.Node]??(heuristics[s.Node]=targets.Min(t=>(nodes[s.Node]-t).Length)).Value;queue.Add(Tuple.Create(s.Cost+h,serial++,s));}
  Push(new State{Node=source,Dir=-1,Run=minrun});int iterations=0;
  while(queue.Count>0&&iterations++<150000){if(iterations%500==0)o.Cancellation.ThrowIfCancellationRequested();var item=queue.Min;queue.Remove(item);var s=item.Item3;
   if(goals.Contains(s.Node)){
    var pts=new List<Point>();for(var a=s;a!=null;a=a.Prev)pts.Add(nodes[a.Node]);pts.Reverse();for(int i=pts.Count-2;i>0;i--)if(Math.Abs(Layout.Cross(VillaGeometry.Unit(pts[i]-pts[i-1]),VillaGeometry.Unit(pts[i+1]-pts[i])))<.001)pts.RemoveAt(i);
    var cs=TowerGeometry.Fillet(pts,r["RoadRadius"],false,r["CurveGap"]);if(cs!=null&&Clear(TowerGeometry.Route(cs,"aisle",o.ParkingAisle)))return cs;continue;
   }
   int ix=s.Node/ny,iy=s.Node%ny;var next=new[]{ix+1<nx?s.Node+ny:-1,iy+1<ny?s.Node+1:-1,ix>0?s.Node-ny:-1,iy>0?s.Node-1:-1};
   for(int d=0;d<4;d++){int j=next[d];if(j<0||s.Dir>=0&&(d+2)%4==s.Dir)continue;bool turn=s.Dir>=0&&s.Dir!=d;if(turn&&s.Run<minrun-.001)continue;var a=nodes[s.Node];var b=nodes[j];long key=((long)Math.Min(s.Node,j)<<32)|(uint)Math.Max(s.Node,j);
    if(!edgeCache.TryGetValue(key,out bool clear)){clear=Clear(a,b)&&!Parallel(a,b);edgeCache[key]=clear;}if(!clear)continue;
    if(turn){var turnKey=(s.Node,s.Dir,d);if(!turnCache.TryGetValue(turnKey,out bool turnClear)){var incoming=VillaGeometry.Unit(a-nodes[s.Prev.Node]);var outgoing=VillaGeometry.Unit(b-a);var cs=TowerGeometry.Fillet(new[]{a-incoming*(r["RoadRadius"]+1),a,a+outgoing*(r["RoadRadius"]+1)},r["RoadRadius"],false);turnClear=cs!=null&&Clear(TowerGeometry.Route(cs,"aisle",o.ParkingAisle));turnCache[turnKey]=turnClear;}if(!turnClear)continue;}
    double length=(b-a).Length;Push(new State{Node=j,Dir=d,Run=turn?length:Math.Min(minrun,s.Run+length),Cost=s.Cost+length+(turn?4:0),Prev=s});
   }
  }return null;
 }
}
}
