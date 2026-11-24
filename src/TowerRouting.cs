using System;
using System.Linq;
using System.Collections.Generic;
namespace ApartmentPlan {
// Orthogonal visibility lattice in the building frame. Search carries the distance
// since the last bend so smoothing cannot invent sub-radius corners afterwards.
public sealed class TowerRouting {
 readonly TowerBoundary boundary;
 readonly Plan p;readonly IList<Point> poly;readonly List<(Point[] shape,Parcel bounds)> excluded;readonly Parcel rectangularSite;readonly Point u,v;readonly LayoutOptions o;readonly TowerRules r;
 public readonly List<Point> Anchors=new List<Point>();readonly List<PlanRoute> connected=new List<PlanRoute>();
 public readonly Dictionary<Parcel,int> FailedFrontages=new Dictionary<Parcel,int>();
 readonly List<(Point[] shape,Parcel bounds)> obstacles=new List<(Point[],Parcel)>();
 public TowerRouting(Plan p,IList<Point> poly,List<Parcel> lots,List<Point[]> excluded,Point u,Point v,LayoutOptions o,TowerRules r):this(p,poly,lots,excluded,u,v,o,r,new TowerBoundary(poly)){}
 internal TowerRouting(Plan p,IList<Point> poly,List<Parcel> lots,List<Point[]> excluded,Point u,Point v,LayoutOptions o,TowerRules r,TowerBoundary boundary){this.p=p;this.poly=poly;this.boundary=boundary;this.excluded=excluded.Select(q=>(q,VillaGeometry.Bounds(q))).ToList();
  if(poly.Count==4&&Enumerable.Range(0,4).All(i=>poly[i].X==poly[(i+1)%4].X||poly[i].Y==poly[(i+1)%4].Y))rectangularSite=VillaGeometry.Bounds(poly);
  this.u=u;this.v=v;this.o=o;this.r=r;foreach(var l in lots)obstacles.Add((l.PlacementOutline,VillaGeometry.Bounds(l.PlacementOutline)));}
 Point UV(Point q)=>new Point(SiteFrame.Dot(q,u),SiteFrame.Dot(q,v));Point XY(Point q)=>u*q.X+v*q.Y;
 long searchCalls,searchNodes,searchVisits,searchLimits;
 readonly Dictionary<string,int> rejectedReasons=new Dictionary<string,int>();
 bool Reject(string reason){rejectedReasons[reason]=rejectedReasons.TryGetValue(reason,out var n)?n+1:1;return false;}
 readonly Dictionary<(double,double,double,double),bool> staticClearance=new Dictionary<(double,double,double,double),bool>();
 bool Clear(Point a,Point b,bool branch=false){
  if(branch)return ComputeClearance(a,b,true);
  var key=(a.X,a.Y,b.X,b.Y);
  if(staticClearance.TryGetValue(key,out var clear))return clear;
  clear=ComputeClearance(a,b,false);
  if(staticClearance.Count<32768)staticClearance[key]=clear;
  return clear;
 }
 bool ComputeClearance(Point a,Point b,bool branch){if((b-a).Length<.001)return true;var shape=VillaGeometry.Box((a+b)*.5,VillaGeometry.Unit(b-a),o.ParkingAisle-.02,(b-a).Length);
  if(!branch&&!TurnClear(a,b))return Reject("AP.LayoutReason.TurnGap");
  double left=shape[0].X,right=left,bottom=shape[0].Y,top=bottom;
  for(int i=1;i<shape.Length;i++){left=Math.Min(left,shape[i].X);right=Math.Max(right,shape[i].X);bottom=Math.Min(bottom,shape[i].Y);top=Math.Max(top,shape[i].Y);}
  // For strictly interior ribbons in an axis-aligned rectangle, the
  // bounding box proves containment. Keep the polygon test at the edges.
  var site=rectangularSite;
  if(!(site!=null&&left>site.X+.001&&right<site.X+site.W-.001&&bottom>site.Y+.001&&top<site.Y+site.D-.001)&&!boundary.Contains(shape))return Reject("AP.LayoutReason.Boundary");
  foreach(var obstacle in obstacles){var q=obstacle.bounds;
   if(q.X>right||q.X+q.W<left||q.Y>top||q.Y+q.D<bottom)continue;
   if(VillaGeometry.Overlap(shape,obstacle.shape))return Reject("AP.LayoutReason.Building");
  }
  foreach(var zone in excluded){var q=zone.bounds;
   if(q.X>right||q.X+q.W<left||q.Y>top||q.Y+q.D<bottom)continue;
   if(VillaGeometry.Overlap(shape,zone.shape))return Reject("AP.LayoutReason.Excluded");
  }
  return true;
 }

 // A single explicit branch crosses each court clearance belt. All searched
 // roads start outside that belt; proximity alone never grants an exception.
 public static double SegmentDistance(Point a,Point b,Point c,Point d){
  var u=b-a;var v=d-c;double cross=Layout.Cross(u,v);
  if(Math.Abs(cross)>1e-9){double t=Layout.Cross(c-a,v)/cross,k=Layout.Cross(c-a,u)/cross;if(t>=0&&t<=1&&k>=0&&k<=1)return 0;}
  return Math.Min(Math.Min((a-VillaGeometry.Nearest(a,c,d)).Length,(b-VillaGeometry.Nearest(b,c,d)).Length),Math.Min((c-VillaGeometry.Nearest(c,a,b)).Length,(d-VillaGeometry.Nearest(d,a,b)).Length));
 }
 readonly List<Tuple<Point,Point,double>> courtEdges=new List<Tuple<Point,Point,double>>();
 readonly List<(double left,double right,double bottom,double top,int start,int end)> courtBounds=new List<(double,double,double,double,int,int)>();
 bool TurnClear(Point a,Point b){
  double left=Math.Min(a.X,b.X),right=Math.Max(a.X,b.X),bottom=Math.Min(a.Y,b.Y),top=Math.Max(a.Y,b.Y);
  foreach(var box in courtBounds){
   if(right<box.left||left>box.right||top<box.bottom||bottom>box.top)continue;
   for(int i=box.start;i<box.end;i++){var e=courtEdges[i];if(!(
  Math.Max(a.X,b.X)<Math.Min(e.Item1.X,e.Item2.X)-e.Item3 || Math.Min(a.X,b.X)>Math.Max(e.Item1.X,e.Item2.X)+e.Item3 ||
  Math.Max(a.Y,b.Y)<Math.Min(e.Item1.Y,e.Item2.Y)-e.Item3 || Math.Min(a.Y,b.Y)>Math.Max(e.Item1.Y,e.Item2.Y)+e.Item3 ||
  SegmentDistance(a,b,e.Item1,e.Item2)>=e.Item3-.001))return false;}
  }return true;
 }
 bool PrepareBranches(int attempt){

  var rings=p.Routes.Where(q=>q.Use=="turnaround").ToList();
  if(courtEdges.Count==0)foreach(var ring in rings){int start=courtEdges.Count;foreach(var curve in ring.Curves){var pts=VillaGeometry.Sample(curve,.5);for(int i=1;i<pts.Count;i++)courtEdges.Add(Tuple.Create(pts[i-1],pts[i],(ring.Width+o.ParkingAisle)/2+r["TurnaroundGap"]+.02));}
   var edges=courtEdges.GetRange(start,courtEdges.Count-start);
   courtBounds.Add((edges.Min(e=>Math.Min(e.Item1.X,e.Item2.X)-e.Item3),edges.Max(e=>Math.Max(e.Item1.X,e.Item2.X)+e.Item3),edges.Min(e=>Math.Min(e.Item1.Y,e.Item2.Y)-e.Item3),edges.Max(e=>Math.Max(e.Item1.Y,e.Item2.Y)+e.Item3),start,courtEdges.Count));
  }
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
  for(int i=1;i<points.Count;i++)if(SegmentDistance(points[i-1],points[i],front.A,front.D)<gap-.001)return false;
  return true;
 }
 static bool BoundsApart(PlanBezier a,PlanBezier b,double gap){
   return Math.Max(Math.Max(a.A.X,a.B.X),Math.Max(a.C.X,a.D.X))+gap<Math.Min(Math.Min(b.A.X,b.B.X),Math.Min(b.C.X,b.D.X))||
    Math.Max(Math.Max(b.A.X,b.B.X),Math.Max(b.C.X,b.D.X))+gap<Math.Min(Math.Min(a.A.X,a.B.X),Math.Min(a.C.X,a.D.X))||
    Math.Max(Math.Max(a.A.Y,a.B.Y),Math.Max(a.C.Y,a.D.Y))+gap<Math.Min(Math.Min(b.A.Y,b.B.Y),Math.Min(b.C.Y,b.D.Y))||
    Math.Max(Math.Max(b.A.Y,b.B.Y),Math.Max(b.C.Y,b.D.Y))+gap<Math.Min(Math.Min(a.A.Y,a.B.Y),Math.Min(a.C.Y,a.D.Y));
 }
 bool FrontClear(PlanBezier curve){
  List<Point> samples=null;double gap=r["FrontCurveGap"];
  foreach(var front in fronts){
   if(BoundsApart(curve,front,gap))continue;
   if(samples==null)samples=VillaGeometry.Sample(curve,.5);
   if(!FrontCurveClear(curve,front,gap,samples))return false;
  }
  return true;
 }
 bool StaticRouteClear(PlanRoute route){
  var lines=route.Curves.Where(IsLine).ToArray();for(int i=0;i<lines.Length;i++)for(int j=0;j<i;j++)if(ParallelPair(lines[i].A,lines[i].D,lines[j],r["ParallelGap"]))return false;
  if(route.Curves.Any(c=>!IsLine(c)&&!FrontClear(c)))return false;
  if(!route.Points.Skip(1).Select((q,i)=>Clear(route.Points[i],q)).All(q=>q))return false;
  return true;
 }
 bool Clear(PlanRoute route)=>StaticRouteClear(route)&&CurveGapsClear(route,p.Routes.Where(q=>q.Use!="pedestrian").SelectMany(q=>q.Curves).Where(c=>!IsLine(c)));
 readonly Dictionary<PlanBezier,List<Point>> bendSamples=new Dictionary<PlanBezier,List<Point>>();
 List<Point> BendSamples(PlanBezier bend){
  if(bendSamples.TryGetValue(bend,out var points))return points;
  points=VillaGeometry.Sample(bend,1);if(bendSamples.Count<4096)bendSamples[bend]=points;return points;
 }
 bool CurveGapsClear(PlanRoute route,IEnumerable<PlanBezier> existing){
  foreach(var curve in route.Curves.Where(c=>!IsLine(c)))foreach(var old in existing){
   if(BoundsApart(curve,old,r["CurveGap"]))continue;
   if((curve.A-old.A).Length<.01&&(curve.D-old.D).Length<.01)continue;
   var a=BendSamples(curve);var b=BendSamples(old);double gap=r["CurveGap"]-.01;
   foreach(var q in a)for(int i=1;i<b.Count;i++){
    var x=b[i-1];var y=b[i];
    if(q.X<Math.Min(x.X,y.X)-gap||q.X>Math.Max(x.X,y.X)+gap||q.Y<Math.Min(x.Y,y.Y)-gap||q.Y>Math.Max(x.Y,y.Y)+gap)continue;
    if((VillaGeometry.Nearest(q,x,y)-q).Length<gap)return false;
   }
  }return true;
 }
 static bool ParallelPair(Point a,Point b,PlanBezier curve,double gap){var delta=b-a;double len=delta.Length;var d=curve.D-curve.A;if(len<.01||d.Length<.01||!IsLine(curve))return false;var dir=delta*(1/len);if(Math.Abs(SiteFrame.Dot(dir,VillaGeometry.Unit(d)))<Math.Cos(Math.PI/18))return false;double lateral=Math.Abs(Layout.Cross(dir,curve.A-a)),overlap=Math.Min(len,Math.Max(SiteFrame.Dot(curve.A-a,dir),SiteFrame.Dot(curve.D-a,dir)))-Math.Max(0,Math.Min(SiteFrame.Dot(curve.A-a,dir),SiteFrame.Dot(curve.D-a,dir)));return lateral>.1&&lateral<gap-.001&&overlap>.1;}
 bool Parallel(Point a,Point b){
  double gap=r["ParallelGap"];
  foreach(var f in fronts)if(ParallelPair(a,b,f,gap))return true;
  foreach(var route in connected)foreach(var c in route.Curves)if(ParallelPair(a,b,c,gap))return true;
  return false;
 }
 PlanRoute Add(List<PlanBezier> cs){var route=TowerGeometry.Route(cs,"aisle",o.ParkingAisle);connected.Add(route);p.Routes.Add(route);return route;}
 public void Build(List<Parcel> buildings){
  foreach(var lot in buildings){var v=lot.PlacementForward??this.v;var u=new Point(-v.Y,v.X);var c=new Point(lot.X+lot.W/2,lot.Y+lot.D/2);var mid=c+v*(lot.D/2+o.ParkingAisle/2);fronts.Add(TowerGeometry.Line(mid-u*Math.Max(1,lot.W/2-4),mid+u*Math.Max(1,lot.W/2-4)));}
  int saved=p.Routes.Count;var rawAnchors=Anchors.ToArray();var triedOrders=new HashSet<string>();
  for(int attempt=0;attempt<8;attempt++){
   o.Cancellation.ThrowIfCancellationRequested();connected.Clear();p.Routes.RemoveRange(saved,p.Routes.Count-saved);
   Anchors.Clear();Anchors.AddRange(rawAnchors);if(!PrepareBranches(attempt)){o.Observation?.Set("routeAttempts",attempt+1);o.Observation?.Set("unreachable",buildings.Count);o.Observation?.Set("searchCalls",searchCalls);o.Observation?.Set("searchNodes",searchNodes);o.Observation?.Set("searchVisits",searchVisits);o.Observation?.Set("searchLimits",searchLimits);foreach(var reason in rejectedReasons)o.Observation?.Set("reject"+reason.Key.Substring(reason.Key.LastIndexOf('.')+1),reason.Value);
   o.Diagnostic?.Invoke("routing attempt="+(attempt+1)+" branch=false reasons="+string.Join(",",rejectedReasons.Select(q=>q.Key+":"+q.Value)));continue;}
   var pending=buildings.OrderBy(l=>attempt<4?Anchors.Min(a=>(a-l.Access.Value).Length):SiteFrame.Dot(new Point(l.X,l.Y),new Point(Math.Cos((attempt-2)*Math.PI/3),Math.Sin((attempt-2)*Math.PI/3)))).ToList();
   string orderKey=(attempt<4&&Anchors.Count>1?"links:":"noLinks:")+string.Join(";",Anchors.Select(a=>a.X.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+a.Y.ToString("R",System.Globalization.CultureInfo.InvariantCulture)))+":"+string.Join(",",pending.Select(l=>buildings.IndexOf(l)));
   if(!triedOrders.Add(orderKey))continue;
   // Linking gates is optional. Later retries reserve space for buildings first.
   int gateLinks=0;
   if(attempt<4)foreach(var anchor in Anchors.Skip(1)){var link=Find(anchor,Anchors.Take(1).ToList());if(link!=null){Add(link);gateLinks++;}}
   var unreachable=new List<Parcel>();
  // Reserve fronts before connecting so one connector cannot steal another building's frontage.
  foreach(var lot in pending){o.Cancellation.ThrowIfCancellationRequested();var v=lot.PlacementForward??this.v;var u=new Point(-v.Y,v.X);var c=new Point(lot.X+lot.W/2,lot.Y+lot.D/2);var mid=c+v*(lot.D/2+o.ParkingAisle/2);var a=mid-u*Math.Max(1,lot.W/2-4);var b=mid+u*Math.Max(1,lot.W/2-4);
   var targets=Anchors.Concat(connected.SelectMany(q=>q.Curves.Where(IsLine).SelectMany(z=>new[]{z.A,z.D,(z.A+z.D)*.5}))).Where(q=>Anchors.All(a=>(a-q).Length<.001||(a-q).Length>=r["BranchGap"]+o.ParkingAisle/2)).ToList();List<PlanBezier> path=null;
   if(!Clear(a,b)){unreachable.Add(lot);break;}if(Parallel(a,b)){Reject("AP.LayoutReason.Parallel");unreachable.Add(lot);break;}
   var reservedFront=Add(new List<PlanBezier>{TowerGeometry.Line(a,b)});
   foreach(var start in new[]{mid,a,b}){path=Find(start,targets);if(path!=null)break;}
   if(path==null){Reject("AP.LayoutReason.Search");connected.Remove(reservedFront);p.Routes.Remove(reservedFront);unreachable.Add(lot);break;}
   if(path.Count>0)Add(path);
  }
   o.Observation?.Set("routeAttempts",attempt+1);o.Observation?.Set("unreachable",unreachable.Count);o.Observation?.Set("searchCalls",searchCalls);o.Observation?.Set("searchNodes",searchNodes);o.Observation?.Set("searchVisits",searchVisits);o.Observation?.Set("searchLimits",searchLimits);foreach(var reason in rejectedReasons)o.Observation?.Set("reject"+reason.Key.Substring(reason.Key.LastIndexOf('.')+1),reason.Value);
   o.Diagnostic?.Invoke("routing attempt="+(attempt+1)+" buildings="+buildings.Count+" unreachable="+unreachable.Count+" routes="+p.Routes.Count+" searchCalls="+searchCalls+" gridNodes="+searchNodes+" visited="+searchVisits+" searchLimits="+searchLimits+" reasons="+string.Join(",",rejectedReasons.Select(q=>q.Key+":"+q.Value)));
   if(unreachable.Count>0){var failed=unreachable[0];FailedFrontages[failed]=FailedFrontages.TryGetValue(failed,out var failures)?failures+1:1;continue;}

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
 // Binary heap with the same insertion-order tie break as the route search.
 sealed class SearchQueue {
  readonly List<(double Score,int Serial,State Value)> heap=new List<(double,int,State)>();
  int serial;
  static bool Before((double Score,int Serial,State Value) a,(double Score,int Serial,State Value) b)=>a.Score<b.Score||a.Score==b.Score&&a.Serial<b.Serial;
  public int Count=>heap.Count;
  public void Add(double score,State value){
   var item=(score,serial++,value);int i=heap.Count;heap.Add(item);
   while(i>0){int parent=(i-1)/2;if(!Before(item,heap[parent]))break;heap[i]=heap[parent];i=parent;}
   heap[i]=item;
  }
  public State Pop(){
   var result=heap[0].Value;int end=heap.Count-1;var last=heap[end];heap.RemoveAt(end);
   if(end==0)return result;
   int i=0;
   while(2*i+1<end){int child=2*i+1;if(child+1<end&&Before(heap[child+1],heap[child]))child++;if(!Before(heap[child],last))break;heap[i]=heap[child];i=child;}
   heap[i]=last;return result;
  }
 }
 // Static turn clearance survives searches on this same packing. Curve gaps
 // to connected streets are still checked against the current route set.
 readonly Dictionary<(double,double,double,double,double,double),PlanRoute> turnTemplates=new Dictionary<(double,double,double,double,double,double),PlanRoute>();
 List<PlanBezier> Find(Point start,List<Point> targets){
  searchCalls++;
  using(var timing=LayoutFunctionTiming.Start(o.FunctionTiming,"TowerRouting.Find")){
  if(targets.Count==0)return null;if(targets.Any(t=>(t-start).Length<.01))return new List<PlanBezier>();
  var all=poly.Select(UV).ToList();double minX=all.Min(q=>q.X),minY=all.Min(q=>q.Y);var special=targets.Concat(new[]{start}).Concat(fronts.SelectMany(f=>new[]{f.A,f.D})).Select(UV).ToList();double spacing=8;
  var xs=special.Select(q=>q.X).Concat(Enumerable.Range(0,Math.Max(1,(int)Math.Ceiling((all.Max(q=>q.X)-minX)/spacing))).Select(i=>minX+i*spacing)).Select(x=>Math.Round(x,4)).Distinct().OrderBy(x=>x).ToArray();
  var ys=special.Select(q=>q.Y).Concat(Enumerable.Range(0,Math.Max(1,(int)Math.Ceiling((all.Max(q=>q.Y)-minY)/spacing))).Select(i=>minY+i*spacing)).Select(x=>Math.Round(x,4)).Distinct().OrderBy(x=>x).ToArray();
  // Frontages and connected routes are fixed throughout this search.
  var lines=fronts.Concat(connected.SelectMany(route=>route.Curves)).Where(c=>IsLine(c)&&(c.D-c.A).Length>=.01)
   .Select(c=>(a:c.A,b:c.D,dir:VillaGeometry.Unit(c.D-c.A))).ToArray();
  var existingBends=p.Routes.Where(q=>q.Use!="pedestrian").SelectMany(q=>q.Curves).Where(c=>!IsLine(c)).ToArray();
  double parallelGap=r["ParallelGap"],alignment=Math.Cos(Math.PI/18);
  bool NearParallel(Point a,Point b){
   var delta=b-a;double length=delta.Length;if(length<.01)return false;
   var direction=delta*(1/length);
   foreach(var line in lines){
    if(Math.Abs(SiteFrame.Dot(direction,line.dir))<alignment)continue;
    var from=line.a-a;double lateral=Math.Abs(Layout.Cross(direction,from));
    if(lateral<=.1||lateral>=parallelGap-.001)continue;
    double first=SiteFrame.Dot(from,direction),last=SiteFrame.Dot(line.b-a,direction);
    if(Math.Min(length,Math.Max(first,last))-Math.Max(0,Math.Min(first,last))>.1)return true;
   }
   return false;
  }
  int nx=xs.Length,ny=ys.Length;searchNodes+=(long)nx*ny;Point Node(int i)=>XY(new Point(xs[i/ny],ys[i%ny]));
  int Index(Point q){var uv=UV(q);return Array.IndexOf(xs,Math.Round(uv.X,4))*ny+Array.IndexOf(ys,Math.Round(uv.Y,4));}
  int source=Index(start);var goals=new HashSet<int>(targets.Select(Index));var queue=new SearchQueue();var cost=new Dictionary<(int,int,double),double>();var turnCache=new Dictionary<(int,int,int),bool>();var heuristics=new double?[nx*ny];var edgeCache=new Dictionary<long,bool>();double minrun=2*r["RoadRadius"]+r["CurveGap"];
  double Heuristic(int id){
   if(heuristics[id].HasValue)return heuristics[id].Value;
   var point=Node(id);double best=double.MaxValue;
   foreach(var goal in targets){var delta=point-goal;best=Math.Min(best,delta.X*delta.X+delta.Y*delta.Y);}
   double result=Math.Sqrt(best);heuristics[id]=result;return result;
  }
  void Push(State s){var key=(s.Node,s.Dir,Math.Round(Math.Min(minrun,s.Run),1));if(cost.TryGetValue(key,out var old)&&old<=s.Cost)return;cost[key]=s.Cost;queue.Add(s.Cost+Heuristic(s.Node),s);}
  Push(new State{Node=source,Dir=-1,Run=minrun});int iterations=0;
  while(queue.Count>0&&iterations++<150000){searchVisits++;if(iterations%500==0)o.Cancellation.ThrowIfCancellationRequested();var s=queue.Pop();
   if(goals.Contains(s.Node)){
    var pts=new List<Point>();for(var a=s;a!=null;a=a.Prev)pts.Add(Node(a.Node));pts.Reverse();for(int i=pts.Count-2;i>0;i--)if(Math.Abs(Layout.Cross(VillaGeometry.Unit(pts[i]-pts[i-1]),VillaGeometry.Unit(pts[i+1]-pts[i])))<.001)pts.RemoveAt(i);
    var cs=TowerGeometry.Fillet(pts,r["RoadRadius"],false,r["CurveGap"]);if(cs!=null&&Clear(TowerGeometry.Route(cs,"aisle",o.ParkingAisle)))return cs;continue;
   }
   int ix=s.Node/ny,iy=s.Node%ny;var aNode=Node(s.Node);
   for(int d=0;d<4;d++){int j=d==0?(ix+1<nx?s.Node+ny:-1):d==1?(iy+1<ny?s.Node+1:-1):d==2?(ix>0?s.Node-ny:-1):(iy>0?s.Node-1:-1);if(j<0||s.Dir>=0&&(d+2)%4==s.Dir)continue;bool turn=s.Dir>=0&&s.Dir!=d;if(turn&&s.Run<minrun-.001)continue;var a=aNode;var b=Node(j);long key=((long)Math.Min(s.Node,j)<<32)|(uint)Math.Max(s.Node,j);
    if(!edgeCache.TryGetValue(key,out bool clear)){clear=Clear(a,b)&&!NearParallel(a,b);edgeCache[key]=clear;}if(!clear)continue;
    if(turn){var turnKey=(s.Node,s.Dir,d);if(!turnCache.TryGetValue(turnKey,out bool turnClear)){var incoming=VillaGeometry.Unit(a-Node(s.Prev.Node));var outgoing=VillaGeometry.Unit(b-a);var turnShape=(a.X,a.Y,incoming.X,incoming.Y,outgoing.X,outgoing.Y);
     if(!turnTemplates.TryGetValue(turnShape,out var route)){
      var cs=TowerGeometry.Fillet(new[]{a-incoming*(r["RoadRadius"]+1),a,a+outgoing*(r["RoadRadius"]+1)},r["RoadRadius"],false);
      if(cs!=null){route=TowerGeometry.Route(cs,"aisle",o.ParkingAisle);if(!StaticRouteClear(route))route=null;}
      if(turnTemplates.Count<8192)turnTemplates[turnShape]=route;
     }
     turnClear=route!=null&&CurveGapsClear(route,existingBends);turnCache[turnKey]=turnClear;}if(!turnClear)continue;}
    double length=(b-a).Length;Push(new State{Node=j,Dir=d,Run=turn?length:Math.Min(minrun,s.Run+length),Cost=s.Cost+length+(turn?4:0),Prev=s});
   }
  }if(queue.Count>0)searchLimits++;return null;
  }
 }
}
}
