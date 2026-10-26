using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
public sealed class VillaConnection {public Point Boundary,Road,Tangent;public List<Point> Arms=new List<Point>();public int Side;public bool Junction,Disabled,Manual;}
public static class VillaLayout {
 public static bool Excluded(Asset a)=>(a.Id+" "+a.Name).IndexOf("Rural",StringComparison.OrdinalIgnoreCase)>=0;
 public static bool Candidate(Asset a)=>!Excluded(a)&&a.Homes>0&&Math.Abs(a.Width-16)<.1&&Math.Abs(a.Depth-16)<.1&&System.Text.RegularExpressions.Regex.IsMatch(a.Id+" "+a.Name,@"CS[\s_-]*(KR|Korean)",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
 // Geometry is deliberately ignored. The stable coordinate hash avoids reshuffling on rebuild.
 public static double Span(double lot,double geometry,LayoutOptions o)=>lot-(o.VillaMesh?2.5:0)+o.VillaGap;
 public static double Spacing(double lot,Point at,int axis,LayoutOptions o){
  uint h;unchecked{h=(uint)(int)Math.Round(at.X*100)*73856093u^(uint)(int)Math.Round(at.Y*100)*19349663u^(uint)(axis+1)*83492791u;h^=h>>16;h*=2246822519u;h^=h>>13;}
  return lot-(o.VillaMesh?2+h/(double)uint.MaxValue:0)+o.VillaGap;
 }

 public static Plan Generate(IList<Point> boundary,IList<Point> entries,IList<Asset> assets,LayoutOptions o,IList<ManualZone> zones){
  if(!Layout.Valid(boundary))throw new ArgumentException("InvalidBoundary");
  // Original lot frontage must touch the street; legacy setback is not an access margin.
  const double frontageSetback=0;
  var gates=o.VillaConnections.Where(c=>!c.Disabled).ToList();
  var origin=gates.Count>0?gates[0].Boundary:entries.Count>0?entries[0]:boundary[0];
  var p=SiteFrame.Create(boundary,origin);p.Villa=true;
  var poly=boundary.Select(p.Local).ToList();double x0=poly.Min(q=>q.X),x1=poly.Max(q=>q.X),y0=poly.Min(q=>q.Y),y1=poly.Max(q=>q.Y);
  if(p.Area>1000000)throw new PlanLimitException("AP.LimitSiteArea",p.Area,1000000);
  if(Math.Max(x1-x0,y1-y0)>2048)throw new PlanLimitException("AP.LimitSiteExtent",Math.Max(x1-x0,y1-y0),2048);
  var selected=assets.Where(a=>!Excluded(a)&&a.Width>0&&a.Depth>0).ToList();if(selected.Count==0)return p;
  var reserved=(zones??new List<ManualZone>()).Select(z=>z.Points.Select(p.Local).ToList()).ToList();
  bool Clear(Parcel r)=>Layout.Contains(r,poly)&&!reserved.Any(z=>r.Corners.Any(q=>Layout.Inside(q,z))||z.Any(q=>q.X>=r.X&&q.X<=r.X+r.W&&q.Y>=r.Y&&q.Y<=r.Y+r.D)||!NoCross(r,z));
  double depth=selected.Max(a=>Span(a.Depth,a.GeometryDepth,o)),aisle=o.ParkingAisle,margin=o.VillaBoundary;
  double lotDepth=selected.Max(a=>a.Depth);double pitch=(o.VillaRows==2?lotDepth+depth:depth)+aisle+2*frontageSetback;
  double rowStart=y0+margin+(o.VillaRows==2?(lotDepth+depth)/2:0)+aisle/2+frontageSetback;
  double rowEnd=rowStart+Math.Max(0,Math.Floor((y1-margin-aisle/2-rowStart-.01)/pitch))*pitch;
  var routes=p.Routes;
  var existing=(o.VillaExistingRoads??new List<PlanRoute>()).Where(r=>r.Points.Count>1).ToList();
  p.VillaExistingRoadLength=existing.Sum(r=>r.Points.Skip(1).Select((q,i)=>Layout.Inside((q+r.Points[i])*.5,boundary)&&TowerGeometry.Distance((q+r.Points[i])*.5,boundary)>r.Width/2+2?(q-r.Points[i]).Length:0).Sum());
  p.VillaRoadDensity=p.VillaExistingRoadLength/p.Area*10000; // metres per hectare
  p.VillaExistingRoadsOnly=p.VillaExistingRoadLength>1;
  Parcel Ribbon(Point a,Point b,double width)=>new Parcel{Kind=1,X=Math.Min(a.X,b.X)-(Math.Abs(a.X-b.X)<.01?width/2:0),Y=Math.Min(a.Y,b.Y)-(Math.Abs(a.Y-b.Y)<.01?width/2:0),W=Math.Max(width,Math.Abs(a.X-b.X)),D=Math.Max(width,Math.Abs(a.Y-b.Y))};
  void Add(Point a,Point b,double width,string use){if((b-a).Length<2)return;routes.Add(new PlanRoute{Use=use,Width=width,Points=new List<Point>{a,b}});}
  // Clip full road width, including reserved zones, rather than just its centreline.
  void Scan(bool vertical,double at,double width,string use){
   double lo=vertical?rowStart:x0+margin,hi=vertical?rowEnd:x1-margin,start=double.NaN;
   for(double t=lo;t<=hi+1;t+=1){o.Cancellation.ThrowIfCancellationRequested();Point a=vertical?new Point(at,t):new Point(t,at),b=vertical?new Point(at,t+1):new Point(t+1,at);bool ok=t+1<=hi&&Clear(Ribbon(a,b,width));
    if(ok&&double.IsNaN(start))start=t;
    if(!ok&&!double.IsNaN(start)){if(t-start>=width*2)Add(vertical?new Point(at,start):new Point(start,at),vertical?new Point(at,t):new Point(t,at),width,use);start=double.NaN;}
   }
  }
  var contour=new List<PlanBezier>();var roots=new List<Point>();int mains=0,gridPieces=0;
  bool ClearShape(IList<Point> shape)=>VillaGeometry.Inside(shape,poly)&&!reserved.Any(z=>shape.Any(q=>Layout.Inside(q,z))||z.Any(q=>Layout.Inside(q,shape))||!EdgesClear(shape,z));
  if(p.VillaExistingRoadsOnly){
   foreach(var r in existing){
    routes.Add(new PlanRoute{Use="aisle",Width=r.Width,Points=r.Points.Select(p.Local).ToList()});
    foreach(var c in r.Curves??new List<PlanBezier>())contour.Add(new PlanBezier{A=p.Local(c.A),B=p.Local(c.B),C=p.Local(c.C),D=p.Local(c.D)});
    p.VillaRoots.Add(r.Points[0]);roots.Add(p.Local(r.Points[0]));
   }
  }else{
  double usable=x1-x0-2*margin;mains=Math.Max(1,(int)Math.Ceiling(usable/o.VillaMainSpacing));
  for(int i=0;i<mains;i++)Scan(true,x0+margin+usable*(i+.5)/mains,aisle,"aisle");
  for(double y=rowStart;y<y1-margin-aisle/2;y+=pitch)Scan(false,y,aisle,"aisle");
  // The perimeter road is authored as Beziers; native generation restores them
  // after graph crossings split the sampled centreline.
  bool Corridor(Point a,Point b,double width){var tangent=VillaGeometry.Unit(b-a);if(tangent.Length<.1)return true;return ClearShape(VillaGeometry.Box((a+b)*.5,new Point(-tangent.Y,tangent.X),(b-a).Length,width));}
  routes.Clear();
  if(o.VillaFollowBoundary){foreach(var c in VillaGeometry.BoundaryGrid(poly,lotDepth+aisle/2+margin+frontageSetback,2*lotDepth+aisle+2*frontageSetback,selected.Max(a=>Span(a.Width,a.GeometryWidth,o)),Math.Max(depth,(lotDepth+depth)/2),aisle,frontageSetback)){
   var pts=VillaGeometry.Sample(c);if(pts.Skip(1).Where((q,i)=>!Corridor(pts[i],q,aisle)).Any())continue;
   contour.Add(c);routes.Add(new PlanRoute{Use="aisle",Width=aisle,Points=pts});
  }}else{for(int i=0;i<mains;i++)Scan(true,x0+margin+usable*(i+.5)/mains,aisle,"aisle");for(double y=rowStart;y<y1-margin-aisle/2;y+=pitch)Scan(false,y,aisle,"aisle");}
  gridPieces=routes.Count;
  var rootDirections=new List<Point>();var arterialGates=new HashSet<VillaConnection>();
  var approaches=new List<Tuple<VillaConnection,Point,Point,Point>>();
  foreach(var gate in gates)foreach(var direction in VillaGeometry.Approaches(gate)){
   if(!VillaGeometry.RayEntry(gate.Road,direction,boundary,out var entry)||(entry-gate.Road).Length>64)continue;
   var local=p.Local(entry);var inward=new Point(SiteFrame.Dot(direction,p.Right),SiteFrame.Dot(direction,p.Forward));
   var inside=local+inward*Math.Max(32,o.Road*2.5);
   if(Corridor(local+inward*(o.Road/2+.1),inside,o.Road))approaches.Add(Tuple.Create(gate,direction,inside,inward));
  }
  PlanBezier Line(Point a,Point b)=>new PlanBezier{A=a,B=a+(b-a)*(1d/3),C=a+(b-a)*(2d/3),D=b};
  // Continue the entrance tangents first; round their intersection once.
  List<PlanBezier> Spine(Point a,Point b,Point inwardA,Point inwardB){
   double cross=Layout.Cross(inwardA,inwardB);
   if(Math.Abs(cross)>.05){
    double ta=Layout.Cross(b-a,inwardB)/cross,tb=Layout.Cross(b-a,inwardA)/cross;
    if(ta>24&&tb>24){
     var corner=a+inwardA*ta;double cut=Math.Min(Math.Min(ta,tb)*.55,Math.Max(64,o.Road*5));
     var start=corner-inwardA*cut;var end=corner-inwardB*cut;
     double angle=Math.Acos(Math.Max(-1,Math.Min(1,SiteFrame.Dot(inwardA,inwardB*-1))));double handle=cut*(4d/3)*Math.Tan(angle/4)/Math.Max(.0001,Math.Tan(angle/2));
     var bend=new PlanBezier{A=start,B=start+inwardA*handle,C=end+inwardB*handle,D=end};
     var trial=new List<PlanBezier>{Line(a,start),bend,Line(end,b)};bool clear=true;
     foreach(var c in trial){var pts=VillaGeometry.Sample(c);for(int i=1;i<pts.Count;i++)if(!Corridor(pts[i-1],pts[i],o.Road)){clear=false;break;}}
     if(clear)return trial;
    }
   }

   var axis=VillaGeometry.Unit(b-a);double trim=Math.Min(16,(b-a).Length*.1);var aa=a-inwardA*trim;var ab=a+axis*trim;var ba=b-axis*trim;var bb=b-inwardB*trim;
   return new List<PlanBezier>{new PlanBezier{A=aa,B=aa+inwardA*(trim*2/3),C=ab-axis*(trim*2/3),D=ab},Line(ab,ba),new PlanBezier{A=ba,B=ba+axis*(trim*2/3),C=bb+inwardB*(trim*2/3),D=bb}};
  }
  var mainCurves=new List<PlanBezier>();var usedPairs=new List<Tuple<Point,Point>>();
  for(int pass=0;o.VillaCentralRoad&&pass<2;pass++){
   Tuple<VillaConnection,Point,Point,Point> first=null,last=null;List<PlanBezier> best=null;double bestScore=double.MinValue;
   foreach(var a in approaches.Where(a=>!arterialGates.Contains(a.Item1)))foreach(var b in approaches.Where(b=>!arterialGates.Contains(b.Item1))){
    var delta=b.Item3-a.Item3;double span=delta.Length;if(a.Item1==b.Item1||SiteFrame.Dot(a.Item2,b.Item2)>.5||span<64)continue;
    if(pass==1&&Math.Abs(SiteFrame.Dot(VillaGeometry.Unit(delta),VillaGeometry.Unit(usedPairs[0].Item2-usedPairs[0].Item1)))>.65)continue;
    var pieces=Spine(a.Item3,b.Item3,a.Item4,b.Item4);var pts=pieces.SelectMany(c=>VillaGeometry.Sample(c)).ToList();bool valid=true;
    for(int i=1;i<pts.Count;i++)if(!Corridor(pts[i-1],pts[i],o.Road)){valid=false;break;}if(!valid)continue;
    if(pass==1){var other=mainCurves.SelectMany(c=>VillaGeometry.Sample(c)).ToList();bool crossing=false;for(int i=1;i<pts.Count;i++)for(int j=1;j<other.Count;j++)if(Layout.Cross(pts[i]-pts[i-1],other[j-1]-pts[i-1])*Layout.Cross(pts[i]-pts[i-1],other[j]-pts[i-1])<0&&Layout.Cross(other[j]-other[j-1],pts[i-1]-other[j-1])*Layout.Cross(other[j]-other[j-1],pts[i]-other[j-1])<0)crossing=true;if(!crossing)continue;}
    var longAxis=x1-x0>y1-y0?new Point(1,0):new Point(0,1);double centrality=VillaGeometry.MidpointPenalty(boundary,a.Item1.Boundary)+VillaGeometry.MidpointPenalty(boundary,b.Item1.Boundary);
    double score=span*(pass==0?.25+Math.Abs(SiteFrame.Dot(VillaGeometry.Unit(delta),longAxis)):1)-centrality*Math.Max(x1-x0,y1-y0)*4;
    if(score<=bestScore)continue;bestScore=score;best=pieces;first=a;last=b;
   }
   if(best==null)break;mainCurves.AddRange(best);usedPairs.Add(Tuple.Create(first.Item3,last.Item3));
   foreach(var c in best){contour.Add(c);routes.Add(new PlanRoute{Use="main",Width=o.Road,Points=VillaGeometry.Sample(c)});}
   Add(p.Local(first.Item1.Road),best[0].A,o.Road,"main");Add(best.Last().D,p.Local(last.Item1.Road),o.Road,"main");
   foreach(var a in new[]{first,last}){arterialGates.Add(a.Item1);p.VillaRoots.Add(a.Item1.Road);rootDirections.Add(a.Item2);roots.Add(p.Local(a.Item1.Road));}
  }
  // Reserve a buildable housing row beside every arterial before placing homes.
  // Crossing streets remain connected; narrow parallel strips are merged away.
  var arterialSegments=routes.Where(r=>r.Use=="main").SelectMany(r=>r.Points.Skip(1).Select((b,i)=>Tuple.Create(r.Points[i],b))).ToArray();var cleaned=new List<PlanRoute>();
  foreach(var r in routes){if(r.Use=="main"){cleaned.Add(r);continue;}var chunk=new List<Point>();
   for(int i=0;i<r.Points.Count;i++){var q=r.Points[i];var tangent=VillaGeometry.Unit(r.Points[Math.Min(i+1,r.Points.Count-1)]-r.Points[Math.Max(0,i-1)]);
    bool narrow=arterialSegments.Any(e=>Math.Abs(SiteFrame.Dot(tangent,VillaGeometry.Unit(e.Item2-e.Item1)))>.8&&(VillaGeometry.Nearest(q,e.Item1,e.Item2)-q).Length<(o.Road+aisle)/2+lotDepth+2*frontageSetback);
    if(narrow){if(chunk.Count>1&&(chunk.Last()-chunk[0]).Length>=8)cleaned.Add(new PlanRoute{Use=r.Use,Width=r.Width,Points=chunk});chunk=new List<Point>();}else chunk.Add(q);
   }if(chunk.Count>1&&(chunk.Last()-chunk[0]).Length>=8)cleaned.Add(new PlanRoute{Use=r.Use,Width=r.Width,Points=chunk});
  }routes.Clear();routes.AddRange(cleaned);
  // On long narrow sites use continuous cross streets between the perimeter
  // alleys. Tiny Bezier pieces must not become independently pruned branches.
  var sides=VillaGeometry.BoundarySides(poly);var longSide=sides.OrderByDescending(side=>side.Skip(1).Select((v,i)=>(v-side[i]).Length).Sum()).First();
  var longDirection=VillaGeometry.Unit(longSide.Last()-longSide[0]);var crossDirection=new Point(-longDirection.Y,longDirection.X);
  double longMin=poly.Min(v=>SiteFrame.Dot(v,longDirection)),longMax=poly.Max(v=>SiteFrame.Dot(v,longDirection)),crossMin=poly.Min(v=>SiteFrame.Dot(v,crossDirection)),crossMax=poly.Max(v=>SiteFrame.Dot(v,crossDirection));
  if(o.VillaFollowBoundary&&(longMax-longMin)>(crossMax-crossMin)*2.2){
   bool Transverse(PlanRoute r)=>r.Points.Count>1&&Math.Abs(SiteFrame.Dot(VillaGeometry.Unit(r.Points.Last()-r.Points[0]),longDirection))<.65;
   routes.RemoveAll(r=>r.Use!="main"&&Transverse(r));
   contour.RemoveAll(c=>!mainCurves.Contains(c)&&Math.Abs(SiteFrame.Dot(VillaGeometry.Unit(c.D-c.A),longDirection))<.65);
   var longitudinal=routes.Where(r=>r.Use!="main").SelectMany(r=>r.Points.Skip(1).Select((b,i)=>Tuple.Create(r.Points[i],b))).ToArray();
   double minimumPitch=2*lotDepth+aisle,targetPitch=Math.Max(80,selected.Max(a=>Span(a.Width,a.GeometryWidth,o))*6);
   int divisions=Math.Max(1,(int)Math.Round((longMax-longMin-2*(lotDepth+aisle/2+margin))/targetPitch));
   for(int k=1;k<divisions;k++){
    double station=longMin+(longMax-longMin)*k/divisions;var hits=new List<Point>();
    foreach(var e in longitudinal){double a=SiteFrame.Dot(e.Item1,longDirection),b=SiteFrame.Dot(e.Item2,longDirection);if(Math.Abs(b-a)<.001||station<Math.Min(a,b)||station>Math.Max(a,b))continue;hits.Add(e.Item1+(e.Item2-e.Item1)*((station-a)/(b-a)));}
    if(hits.Count<2)continue;var firstHit=hits.OrderBy(v=>SiteFrame.Dot(v,crossDirection)).First();var lastHit=hits.OrderByDescending(v=>SiteFrame.Dot(v,crossDirection)).First();
    if((lastHit-firstHit).Length<minimumPitch||!Corridor(firstHit,lastHit,aisle))continue;
    Add(firstHit,lastHit,aisle,"aisle");
   }
  }
  var internalSegments=routes.SelectMany(r=>r.Points.Skip(1).Select((b,i)=>new{A=r.Points[i],B=b,Width=r.Width})).ToArray();
  foreach(var gate in gates){
   if(arterialGates.Contains(gate))continue;
   // Extra entrances near a corner add a short redundant bend. Arterial
   // through connections remain eligible; only secondary approaches are filtered.
   if(roots.Count>0&&!gate.Manual&&(VillaGeometry.BoundarySides(boundary).Any(side=>(side[0]-gate.Boundary).Length<lotDepth*2+aisle)||VillaGeometry.MidpointPenalty(boundary,gate.Boundary)>.35)){p.VillaRejectedConnections.Add(gate.Road);continue;}
   o.Cancellation.ThrowIfCancellationRequested();bool joined=false;
   foreach(var worldDirection in VillaGeometry.Approaches(gate)){
    if(!VillaGeometry.RayEntry(gate.Road,worldDirection,boundary,out var worldEntry)||(worldEntry-gate.Road).Length>64)continue;
    var q=p.Local(worldEntry);var road=p.Local(gate.Road);var inward=new Point(SiteFrame.Dot(worldDirection,p.Right),SiteFrame.Dot(worldDirection,p.Forward));
    var inside=q+inward*Math.Max(16,aisle*2);
    if(!Corridor(q+inward*(aisle/2+.1),inside,aisle))continue;
    var choices=internalSegments.Select(r=>new{Segment=r,Point=VillaGeometry.Nearest(inside,r.A,r.B)}).OrderBy(c=>(c.Point-inside).Length).ToList();
    foreach(var c in choices){
     // Finish perpendicular to the receiving street. Preserve the straight
     // external approach for at least two road widths before any turn.
     var t=VillaGeometry.Unit(c.Segment.B-c.Segment.A);var n=new Point(-t.Y,t.X);
     var elbow=c.Point+n*SiteFrame.Dot(inside-c.Point,n);
     if((c.Point-elbow).Length<8)elbow=c.Point+n*(SiteFrame.Dot(inside-c.Point,n)<0?-12:12);
     var path=new List<Point>{road,inside,elbow,c.Point};
     if((inside-elbow).Length<2)path.RemoveAt(2);
     bool ok=true;for(int k=2;k<path.Count;k++)if(!Corridor(path[k-1],path[k],aisle)){ok=false;break;}
     if(!ok)continue;
     for(int k=1;k<path.Count;k++)Add(path[k-1],path[k],aisle,"aisle");roots.Add(c.Point);p.VillaRoots.Add(gate.Road);rootDirections.Add(worldDirection);joined=true;break;
    }
    if(joined)break;
   }
   if(!joined)p.VillaRejectedConnections.Add(gate.Road);
  }
  // Only keep components reachable from an approved external connection.
  foreach(var r in routes.Where(r=>r.Points.Count>2)){var c=contour.FirstOrDefault(c=>r.Points.All(q=>c.NearBounds(q)&&(c.At(c.Parameter(q))-q).Length<.08));if(c!=null)r.Curves=new List<PlanBezier>{c};}
  var graph=ConstructionGraph.Create(p);var reached=new HashSet<ConstructionEdge>();
  foreach(var e in graph)if(roots.Any(q=>Distance(p.World(q),e.A,e.B)<.1))reached.Add(e);
  bool changed=true;while(changed){changed=false;foreach(var e in graph)if(!reached.Contains(e)&&reached.Any(r=>(r.A-e.A).Length<.03||(r.A-e.B).Length<.03||(r.B-e.A).Length<.03||(r.B-e.B).Length<.03)){reached.Add(e);changed=true;}}
  p.VillaThrough=VillaRoadRules.Through(reached.ToList(),p.VillaRoots,rootDirections,o.Road);
  routes.Clear();foreach(var e in reached)routes.Add(new PlanRoute{Use=e.Use,Width=e.Width,Points=new List<Point>{p.Local(e.A),p.Local(e.B)}});
  }
  var occupied=new List<Point[]>();
  var roadSegments=routes.SelectMany(r=>r.Points.Skip(1).Select((b,i)=>new{A=r.Points[i],B=b,Width=r.Width,Use=r.Use})).ToArray();
  foreach(var r in roadSegments){var tangent=VillaGeometry.Unit(r.B-r.A);var box=VillaGeometry.Box((r.A+r.B)*.5,new Point(-tangent.Y,tangent.X),(r.B-r.A).Length,r.Width);var parcel=VillaGeometry.Bounds(box,1);parcel.PlacementOutline=box;p.Parcels.Add(parcel);occupied.Add(box);}
  // Retain only curve pieces whose centreline survived reachability filtering.
  contour=contour.Where(c=>roadSegments.Any(e=>c.NearBounds((e.A+e.B)*.5)&&(c.At(c.Parameter((e.A+e.B)*.5))-(e.A+e.B)*.5).Length<.1)).ToList();
  foreach(var curve in contour){
   var spans=roadSegments.Where(e=>curve.NearBounds((e.A+e.B)*.5)&&(curve.At(curve.Parameter((e.A+e.B)*.5))-(e.A+e.B)*.5).Length<.08&&(curve.At(curve.Parameter(e.A))-e.A).Length<.08&&(curve.At(curve.Parameter(e.B))-e.B).Length<.08)
    .Select(e=>new{Start=Math.Min(curve.Parameter(e.A),curve.Parameter(e.B)),End=Math.Max(curve.Parameter(e.A),curve.Parameter(e.B)),e.Use,e.Width}).OrderBy(e=>e.Start).ToArray();
   for(int i=0;i<spans.Length;){var s0=spans[i];double end=s0.End;int j=i+1;while(j<spans.Length&&spans[j].Use==s0.Use&&spans[j].Start-end<.005){end=spans[j].End;j++;}
    if(end-s0.Start>.0001)routes.Add(new PlanRoute{Use=s0.Use,Width=s0.Width,Curves=new List<PlanBezier>{curve.Slice(s0.Start,end)}});i=j;
   }
  }
  int index=0;var candidates=new List<Parcel>();var mainCandidates=new HashSet<Parcel>();
  void Place(Point roadPoint,Point tangent,double nativeWidth,int sign,string use){
   o.Cancellation.ThrowIfCancellationRequested();
   var a=selected[index++%selected.Count];double width=Spacing(a.Width,roadPoint,0,o),d=Spacing(a.Depth,roadPoint,1,o);
   var outward=new Point(-tangent.Y,tangent.X)*sign;var forward=outward*-1;
   var center=roadPoint+outward*(nativeWidth/2+frontageSetback+a.Depth/2);
   var envelope=VillaGeometry.Box(center,forward,width,d);
   if(!ClearShape(envelope))return;
   // Extra spacing separates houses, without extending the road collision footprint.
   var roadFootprint=VillaGeometry.Box(center,forward,Math.Min(width,a.Width),Math.Min(d,a.Depth));
   if(occupied.Any(b=>VillaGeometry.Overlap(b,roadFootprint)))return;
   var fullLot=VillaGeometry.Box(center,forward,a.Width,a.Depth);
   if(!ClearShape(fullLot))return;
   // The supporting street must be internal along the full frontage, not an external approach.
   if(!Layout.Inside(roadPoint,poly)||Enumerable.Range(0,poly.Count).Min(i=>(VillaGeometry.Nearest(roadPoint,poly[i],poly[(i+1)%poly.Count])-roadPoint).Length)<nativeWidth/2+2)return;
   // Do not place frontage across a junction or a sharply curving approach.
   var side=new Point(forward.Y,-forward.X);var front=center+forward*(a.Depth/2);
   foreach(double u in new[]{-a.Width*.45,0,a.Width*.45}){
    var target=front+side*u;bool supported=roadSegments.Any(e=>{
     var dir=VillaGeometry.Unit(e.B-e.A);if(Math.Abs(SiteFrame.Dot(dir,side))<.94)return false;
     var at=VillaGeometry.Nearest(target,e.A,e.B);double gap=(at-target).Length-e.Width/2;
     return SiteFrame.Dot(at-target,forward)>0&&gap>=-.5&&gap<=Math.Min(7.9,frontageSetback+2);
    });if(!supported)return;
   }
   var lot=new Parcel{Kind=0,AssetId=a.Id,X=center.X-a.Width/2,Y=center.Y-a.Depth/2,W=a.Width,D=a.Depth,Homes=a.Homes,PlacementForward=forward,CompactBounds=VillaGeometry.Bounds(envelope),PlacementOutline=envelope};
   lot.FloorArea=Math.Min(a.Width*a.Depth,a.GeometryWidth>0&&a.GeometryDepth>0?a.GeometryWidth*a.GeometryDepth:a.Width*a.Depth*.7)*Math.Max(1,Math.Floor(a.Height/3));
   if(!candidates.Any(c=>(new Point(c.X-lot.X,c.Y-lot.Y)).Length<.4&&SiteFrame.Dot(c.PlacementForward.Value,forward)>.999)){candidates.Add(lot);if(use=="main")mainCandidates.Add(lot);}
  }
  double spacing=selected.Max(a=>Span(a.Width,a.GeometryWidth,o));
  // Curved/perimeter frontage gets priority over the central orthogonal infill.
  foreach(var curve in contour){var points=VillaGeometry.Sample(curve,.5);double travel=0,next=spacing/2;
   for(int i=1;i<points.Count;i++){double length=(points[i]-points[i-1]).Length;while(next<=travel+length){double u=(next-travel)/Math.Max(.0001,length);var at=points[i-1]+(points[i]-points[i-1])*u;var tangent=VillaGeometry.Unit(curve.Tangent((i-1+u)/(points.Count-1)));
     var support=roadSegments.OrderBy(e=>(VillaGeometry.Nearest(at,e.A,e.B)-at).Length).First();foreach(int sign in new[]{-1,1})Place(at,tangent,support.Width,sign,support.Use);next+=spacing;}travel+=length;}
  }
  foreach(var r in roadSegments.Where(r=>(r.B-r.A).Length>=spacing+12)){
   var tangent=VillaGeometry.Unit(r.B-r.A);if(contour.Any(c=>c.NearBounds((r.A+r.B)*.5)&&(c.At(c.Parameter((r.A+r.B)*.5))-(r.A+r.B)*.5).Length<.1))continue;
   for(double at=8+spacing/2;at<(r.B-r.A).Length-8;at+=spacing)foreach(int sign in o.VillaRows==1?new[]{1}:new[]{-1,1})Place(r.A+tangent*at,tangent,r.Width,sign,r.Use);
  }
  // Fill leftover frontage with shifted candidates; fixed row phase must not
  // strand a whole house-width near intersections or the end of a curve.
  foreach(var curve in contour){var pts=VillaGeometry.Sample(curve,1);for(int i=1;i<pts.Count-1;i++){
   var at=pts[i];var tangent=VillaGeometry.Unit(curve.Tangent(i/(double)(pts.Count-1)));
   var nearest=roadSegments.OrderBy(e=>(VillaGeometry.Nearest(at,e.A,e.B)-at).Length).First();
   if((VillaGeometry.Nearest(at,nearest.A,nearest.B)-at).Length>.1)continue;
   foreach(int sign in new[]{-1,1})Place(at,tangent,nearest.Width,sign,nearest.Use);
  }}
  // Graph cuts are not frontage ends: inspect short pieces and their endpoints too.
  // Place checks frontage across all adjacent segments and still rejects junction overlap.
  foreach(var r in roadSegments){
   var tangent=VillaGeometry.Unit(r.B-r.A);double length=(r.B-r.A).Length;if(length<.01)continue;
   for(double at=0;at<=length;at+=Math.Min(1,length))foreach(int sign in o.VillaRows==1?new[]{1}:new[]{-1,1})Place(r.A+tangent*at,tangent,r.Width,sign,r.Use);
  }
  // Compare packing orders rather than locking the first candidate forever.
  // Then replace one obstructing house with two compatible candidates when possible.
  bool Conflict(Parcel a,Parcel b)=>Layout.Overlap(a.CompactBounds,b.CompactBounds)&&VillaGeometry.Overlap(a.PlacementOutline,b.PlacementOutline);
  var packed=new List<Parcel>();
  foreach(var order in new IEnumerable<Parcel>[] {candidates,candidates.AsEnumerable().Reverse(),candidates.OrderBy(c=>c.X),candidates.OrderBy(c=>c.Y),candidates.OrderByDescending(c=>c.X),candidates.OrderByDescending(c=>c.Y)}){
   var trial=new List<Parcel>();foreach(var c in order.OrderByDescending(c=>mainCandidates.Contains(c))){if(trial.Count>=o.VillaMaxBuildings)break;if(!trial.Any(q=>Conflict(c,q)))trial.Add(c);}if(trial.Count(mainCandidates.Contains)>packed.Count(mainCandidates.Contains)||trial.Count(mainCandidates.Contains)==packed.Count(mainCandidates.Contains)&&trial.Count>packed.Count)packed=trial;
  }
  for(int pass=0;pass<4&&packed.Count<o.VillaMaxBuildings;pass++){
   bool improved=false;foreach(var old in packed.ToArray()){
    o.Cancellation.ThrowIfCancellationRequested();var choices=candidates.Where(c=>!ReferenceEquals(c,old)&&Conflict(c,old)&&!packed.Any(q=>!ReferenceEquals(q,old)&&Conflict(c,q))).ToArray();
    Parcel a=null,b=null;for(int i=0;i<choices.Length&&a==null;i++)for(int j=i+1;j<choices.Length;j++)if(!Conflict(choices[i],choices[j])&&(!mainCandidates.Contains(old)||mainCandidates.Contains(choices[i])||mainCandidates.Contains(choices[j]))){a=choices[i];b=choices[j];break;}
    if(a==null)continue;packed.Remove(old);packed.Add(a);packed.Add(b);improved=true;if(packed.Count>=o.VillaMaxBuildings)break;
   }if(!improved)break;
  }
  p.Parcels.AddRange(packed);
  // Merge empty road-enclosed wedges into neighbouring housing blocks. Never
  // remove the through road or a street required by an already placed house.
  for(int pass=0;!p.VillaExistingRoadsOnly&&pass<32;pass++){
   var checkGraph=ConstructionGraph.Create(p);var empty=VillaRoadRules.EmptyBlocks(p,checkGraph);if(empty.Count==0)break;
   bool removed=false;
   foreach(var candidate in routes.Where(r=>r.Use!="main"&&r.Points.Count==2&&(r.Points[1]-r.Points[0]).Length>=.1).OrderByDescending(r=>(r.Points[1]-r.Points[0]).Length).ToArray()){
    var a=p.World(candidate.Points[0]);var b=p.World(candidate.Points[1]);
    if(!empty.Any(face=>Enumerable.Range(0,face.Count).Any(i=>(VillaGeometry.Nearest((a+b)*.5,face[i],face[(i+1)%face.Count])-(a+b)*.5).Length<.1)))continue;
    var originalRoutes=routes.ToList();var originalLots=p.Parcels.ToList();var chain=new HashSet<PlanRoute>{candidate};var pending=new List<Point>(candidate.Points);
    for(int cursor=0;cursor<pending.Count;cursor++){var q=pending[cursor];var incident=routes.Where(r=>r.Points.Count==2&&r.Points.Any(v=>(v-q).Length<.03)).ToArray();if(incident.Length!=2)continue;foreach(var r in incident)if(r.Use!="main"&&chain.Add(r))foreach(var v in r.Points)pending.Add(v);}
    routes.RemoveAll(r=>chain.Contains(r));
    var next=ConstructionGraph.Create(p);var unsupported=new List<Parcel>();FrontageNetwork.Missing(p,selected,aisle,frontageSetback,null,unsupported);p.Parcels.RemoveAll(r=>unsupported.Contains(r));
    if(VillaRoadRules.EmptyBlocks(p,next).Count<empty.Count&&FrontageNetwork.Missing(p,selected,aisle,frontageSetback)==0){removed=true;break;}
    routes.Clear();routes.AddRange(originalRoutes);p.Parcels.Clear();p.Parcels.AddRange(originalLots);
   }
   if(!removed)break;
  }
  for(int pass=0;!p.VillaExistingRoadsOnly&&pass<64;pass++){
   var network=ConstructionGraph.Create(p);bool removed=false;
   foreach(var r in routes.Where(r=>r.Use!="main"&&r.Points.Count==2).ToArray()){
    var a=p.World(r.Points[0]);var b=p.World(r.Points[1]);
    bool Leaf(Point q)=>!p.VillaRoots.Any(root=>(root-q).Length<.1)&&network.Count(e=>(e.A-q).Length<.03||(e.B-q).Length<.03)==1;
    if(!Leaf(a)&&!Leaf(b))continue;
    // Curve metadata is kept only for supported sampled spans.
    if(contour.Any(c=>(c.At(c.Parameter((r.Points[0]+r.Points[1])*.5))-(r.Points[0]+r.Points[1])*.5).Length<.1))continue;
    int at=routes.IndexOf(r);routes.RemoveAt(at);
    if(FrontageNetwork.Missing(p,selected,aisle,frontageSetback)==0){removed=true;break;}routes.Insert(at,r);
   }if(!removed)break;
  }
  // Reconstruct only surviving Bezier intervals after pruning. Metadata alone
  // must never bridge a removed road span in the native construction output.
  routes.RemoveAll(r=>r.Points.Count==0&&r.Curves?.Count>0);
  var finalEdges=ConstructionGraph.Create(p);
  foreach(var c in contour){var intervals=finalEdges.Where(e=>c.NearBounds(p.Local((e.A+e.B)*.5))&&(c.At(c.Parameter(p.Local(e.A)))-p.Local(e.A)).Length<.08&&(c.At(c.Parameter(p.Local(e.B)))-p.Local(e.B)).Length<.08&&(c.At(c.Parameter(p.Local((e.A+e.B)*.5)))-p.Local((e.A+e.B)*.5)).Length<.08).Select(e=>new{Start=Math.Min(c.Parameter(p.Local(e.A)),c.Parameter(p.Local(e.B))),End=Math.Max(c.Parameter(p.Local(e.A)),c.Parameter(p.Local(e.B))),e.Width,e.Use}).OrderBy(e=>e.Start).ToArray();
   for(int i=0;i<intervals.Length;){var a=intervals[i];double end=a.End;int j=i+1;while(j<intervals.Length&&intervals[j].Use==a.Use&&intervals[j].Start-end<.0001){end=intervals[j].End;j++;}if(end-a.Start>.0001)routes.Add(new PlanRoute{Use=a.Use,Width=a.Width,Curves=new List<PlanBezier>{c.Slice(a.Start,end)}});i=j;}
  }
  p.VillaEmptyBlocks=p.VillaExistingRoadsOnly?0:VillaRoadRules.EmptyBlocks(p,ConstructionGraph.Create(p)).Count;
  p.Parcels.RemoveAll(r=>r.Kind==1);occupied.Clear();occupied.AddRange(p.Parcels.Where(r=>r.Kind==0).Select(r=>r.PlacementOutline));
  foreach(var edge in ConstructionGraph.Create(p)){var a=p.Local(edge.A);var b=p.Local(edge.B);var t=VillaGeometry.Unit(b-a);var box=VillaGeometry.Box((a+b)*.5,new Point(-t.Y,t.X),(b-a).Length,edge.Width);var parcel=VillaGeometry.Bounds(box,1);parcel.PlacementOutline=box;p.Parcels.Add(parcel);occupied.Add(box);}
  // Coalesce unused cells into strips; existing tree/Grass Sprites integration consumes green parcels.
  const double cell=4;
  for(double y=y0;!p.VillaExistingRoadsOnly&&y+cell<=y1;y+=cell){o.Cancellation.ThrowIfCancellationRequested();double start=double.NaN;for(double x=x0;x<=x1;x+=cell){var tile=new Parcel{Kind=4,X=x,Y=y,W=cell,D=cell};bool free=x+cell<=x1&&Clear(tile)&&!occupied.Any(b=>VillaGeometry.Overlap(b,tile.Corners));if(free&&double.IsNaN(start))start=x;if(!free&&!double.IsNaN(start)){p.Parcels.Add(new Parcel{Kind=4,X=start,Y=y,W=x-start,D=cell});start=double.NaN;}}}
  p.GrassPockets.AddRange(p.Parcels.Where(r=>r.Kind==4));
  VillaVariety.Assign(p.Parcels.Where(r=>r.Kind==0).ToList(),selected);
  p.Homes=p.Parcels.Sum(r=>r.Homes);p.Far=p.Parcels.Sum(r=>r.FloorArea)/p.Area*100;p.RequestedParcels=p.Parcels.Count;p.ParcelLimit=o.MaxParcels;
  if(p.Parcels.Count>o.MaxParcels)throw new PlanLimitException("AP.LimitParcels",p.Parcels.Count,o.MaxParcels);
  p.VillaPedestrianWarnings=0;
  p.Diagnostics="villa connections="+roots.Count+"/"+gates.Count+" local axes="+mains+" gridPieces="+gridPieces;
  p.RequiredAccessFailures=roots.Count==0?1:FrontageNetwork.Missing(p,selected,aisle,frontageSetback);
  if(p.VillaExistingRoadsOnly){p.Diagnostics+=" existingRoadLength="+p.VillaExistingRoadLength.ToString("F1")+" densityMPerHa="+p.VillaRoadDensity.ToString("F1");p.Parcels.RemoveAll(r=>r.Kind!=0);p.GrassPockets.Clear();}
  return p;
 }
 static bool EdgesClear(IList<Point> a,IList<Point> b){for(int i=0;i<a.Count;i++)for(int j=0;j<b.Count;j++){var p=a[i];var q=a[(i+1)%a.Count];var r=b[j];var s=b[(j+1)%b.Count];if(Layout.Cross(q-p,r-p)*Layout.Cross(q-p,s-p)<-1e-8&&Layout.Cross(s-r,p-r)*Layout.Cross(s-r,q-r)<-1e-8)return false;}return true;}
 static bool NoCross(Parcel r,List<Point> z){foreach(var a in r.Corners)foreach(var b in r.Corners)for(int i=0;i<z.Count;i++){var c=z[i];var d=z[(i+1)%z.Count];if(Layout.Cross(b-a,c-a)*Layout.Cross(b-a,d-a)<-1e-8&&Layout.Cross(d-c,a-c)*Layout.Cross(d-c,b-c)<-1e-8)return false;}return true;}
 static double Distance(Point p,Point a,Point b){var d=b-a;return(p-a-d*Math.Max(0,Math.Min(1,SiteFrame.Dot(p-a,d)/SiteFrame.Dot(d,d)))).Length;}
}
}
