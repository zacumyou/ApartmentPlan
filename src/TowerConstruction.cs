using System;
using System.Linq;
using System.Collections.Generic;
namespace ApartmentPlan {
// Split original cubics at branch attachments without tessellating road bends.
public static class TowerConstruction {
 public static bool Straight(PlanBezier c)=>Math.Abs(Layout.Cross(c.D-c.A,c.B-c.A))<.0001&&Math.Abs(Layout.Cross(c.D-c.A,c.C-c.A))<.0001;
 public static List<ConstructionCourse> Create(Plan p){
  var source=p.Routes.Concat(p.ConstructionRoutes).SelectMany(r=>r.Curves.Select(c=>new ConstructionCourse{Use=r.Use,Width=r.Width,Curved=!Straight(c),Curve=new PlanBezier{A=p.World(c.A),B=p.World(c.B),C=p.World(c.C),D=p.World(c.D)}})).ToList();
  JoinOppositeEnds(source);
  // Canonicalize before intersection cuts and duplicate detection, not after them.
  // Keep pedestrian nodes separate from vehicle nodes.
  var canonical=new Dictionary<bool,List<Point>>{{true,new List<Point>()},{false,new List<Point>()}};
  Point Canon(Point q,bool walk){var list=canonical[walk];foreach(var n in list)if((n-q).Length<.001)return n;list.Add(q);return q;}
  foreach(var item in source){var c=item.Curve;var a=Canon(c.A,item.Use=="pedestrian");var d=Canon(c.D,item.Use=="pedestrian");c.B=c.B+(a-c.A);c.C=c.C+(d-c.D);c.A=a;c.D=d;if(!item.Curved)item.Curve=TowerGeometry.Line(a,d);}
  var result=new List<ConstructionCourse>();var keys=new HashSet<string>();
  string Key(Point q)=>Math.Round(q.X,4)+":"+Math.Round(q.Y,4);
  foreach(var c in source.OrderBy(c=>c.Use=="main"?0:c.Use=="turnaround"?1:2)){
   var cuts=new List<double>{0,1};var d=c.Curve.D-c.Curve.A;double dd=SiteFrame.Dot(d,d);if(dd<.000001)continue;
   if(c.Curved){
    foreach(var q in canonical[c.Use=="pedestrian"]){
     if(!c.Curve.NearBounds(q,.001)||(q-c.Curve.A).Length<.001||(q-c.Curve.D).Length<.001)continue;
     double t=c.Curve.Parameter(q);
     if((c.Curve.At(t)-q).Length<.001)cuts.Add(t);
    }
   }else foreach(var other in source){if(ReferenceEquals(c,other)||(c.Use=="pedestrian")!=(other.Use=="pedestrian"))continue;
    foreach(var q in new[]{other.Curve.A,other.Curve.D}){double t=SiteFrame.Dot(q-c.Curve.A,d)/dd;if(t>1e-6&&t<1-1e-6&&(c.Curve.A+d*t-q).Length<.001)cuts.Add(t);}
    if(other.Curved)continue;var e=other.Curve.D-other.Curve.A;double cross=Layout.Cross(d,e);if(Math.Abs(cross)<.000001)continue;double t2=Layout.Cross(other.Curve.A-c.Curve.A,e)/cross,u=Layout.Cross(other.Curve.A-c.Curve.A,d)/cross;if(t2>0&&t2<1&&u>=0&&u<=1)cuts.Add(t2);
   }
   var ordered=new List<double>();foreach(var t in cuts.OrderBy(t=>t))if(ordered.Count==0||(t-ordered.Last())*Math.Sqrt(dd)>.001)ordered.Add(t);for(int i=1;i<ordered.Count;i++){var curve=c.Curve.Slice(ordered[i-1],ordered[i]);if((curve.D-curve.A).Length<.000001)continue;var a=Key(curve.A);var b=Key(curve.D);var key=(c.Use=="pedestrian"?"walk/":"road/")+(String.CompareOrdinal(a,b)<0?a+"/"+b:b+"/"+a)+"/"+Key(curve.At(.5));if(keys.Add(key))result.Add(new ConstructionCourse{Curve=curve,Width=c.Width,Use=c.Use,Curved=c.Curved});}
  }
  // The visibility lattice rounds coordinates; canonicalize those sub-mm
  // differences before native node creation and connectivity validation.
  foreach(var course in result){var c=course.Curve;var a=Canon(c.A,course.Use=="pedestrian");var d=Canon(c.D,course.Use=="pedestrian");c.B=c.B+(a-c.A);c.C=c.C+(d-c.D);c.A=a;c.D=d;}
  keys.Clear();result=result.Where(course=>{var c=course.Curve;var a=Key(c.A);var b=Key(c.D);var key=(course.Use=="pedestrian"?"walk/":"road/")+(String.CompareOrdinal(a,b)<0?a+"/"+b:b+"/"+a)+"/"+Key(c.At(.5));return keys.Add(key);}).ToList();
  MergeCloseJunctions(result);
  return result;
 }
 // Pair only free aisle ends aimed at opposite sides of the same main span.
 static void JoinOppositeEnds(List<ConstructionCourse> source){
  var roads=source.Where(r=>r.Use!="pedestrian").ToList();
  var ends=roads.SelectMany(r=>new[]{r.Curve.A,r.Curve.D}).GroupBy(p=>p).ToDictionary(g=>g.Key,g=>g.Count());
  foreach(var main in roads.Where(r=>r.Use=="main"&&!r.Curved)){
   var axis=main.Curve.D-main.Curve.A;var length=axis.Length;if(length<8)continue;
   var unit=axis*(1/length);
   var hits=new List<(ConstructionCourse road,bool start,Point end,double along,double side)>();
   foreach(var road in roads.Where(r=>r.Use=="aisle"&&!r.Curved)){
    foreach(bool start in new[]{true,false}){
     var end=start?road.Curve.A:road.Curve.D;
     if(!ends.TryGetValue(end,out int count)||count!=1)continue;
     var away=(start?road.Curve.D:road.Curve.A)-end;
     if(away.Length<8||Math.Abs(SiteFrame.Dot(unit,away))>.3*away.Length)continue;
     var offset=end-main.Curve.A;
     double along=SiteFrame.Dot(unit,offset),side=Layout.Cross(unit,offset);
     if(along<4||along>length-4||Math.Abs(side)>Math.Min(main.Width*.5,4)||side*Layout.Cross(unit,away)<0)continue;
     hits.Add((road,start,end,along,Layout.Cross(unit,away)));
    }
   }
   foreach(var h in hits){
    var pair=hits.Where(k=>k.side*h.side<0&&Math.Abs(k.along-h.along)<=4.00001).ToArray();
    if(pair.Length!=1||hits.Count(k=>k.side*pair[0].side<0&&Math.Abs(k.along-pair[0].along)<=4.00001)!=1)continue;
    var target=main.Curve.A+unit*((h.along+pair[0].along)*.5);
    if((target-h.end).Length<.00001)continue;
    h.road.Curve=h.start?TowerGeometry.Line(target,h.road.Curve.D):TowerGeometry.Line(h.road.Curve.A,target);
   }
  }
 }
 // Move a complete perpendicular row, including every intermediate junction.
 // Work in the site's frame so the same constraints apply after rotation.
 static void AlignSlabCrossing(Plan plan,List<ConstructionCourse> roads,
  Dictionary<Point,List<ConstructionCourse>> incident,ConstructionCourse link,Point a,Point b,List<Point> directions){
  const double tolerance=.0001;
  Point Key(Point p)=>new Point(Math.Round(p.X,5),Math.Round(p.Y,5));
  var axis=VillaGeometry.Unit(b-a);
  bool Along(Point v)=>Math.Abs(Layout.Cross(v,axis))<tolerance;
  bool Across(Point v)=>Math.Abs(SiteFrame.Dot(v,axis))<tolerance;
  if(axis.Length<.99||!Along(plan.Right)&&!Along(plan.Forward)||
     directions.Count(Along)!=2||directions.Count(Across)!=2)return;
  var obstacles=plan.Parcels.Where(p=>p.Kind==0||p.Kind==2).Select(p=>(p.PlacementOutline??p.Corners).Select(plan.World).ToArray()).ToArray();
  Dictionary<ConstructionCourse,PlanBezier> best=null;
  int bestCount=int.MaxValue;
  foreach(var target in new[]{a,b,(a+b)*.5}){
   var moves=new Dictionary<Point,Point>();
   var affected=new HashSet<ConstructionCourse>();
   bool Trace(Point root){
    var shift=target-root;
    if(shift.Length<tolerance)return true;
    var queue=new List<Point>{root};
    for(int index=0;index<queue.Count;index++){
     var p=queue[index];
     if(moves.ContainsKey(p))continue;
     if(!incident.TryGetValue(p,out var edges)||edges.Count==0)return false;
     if(edges.Count==1&&plan.Boundary.Count>1&&plan.Boundary.Select((q,i)=>(p-VillaGeometry.Nearest(p,q,plan.Boundary[(i+1)%plan.Boundary.Count])).Length).Min()<shift.Length+.1)return false;
     moves.Add(p,p+shift);
     foreach(var edge in edges){
      if(edge==link)continue;
      var c=edge.Curve;
      if(!Straight(c))return false;
      var other=Key(Key(c.A).Equals(p)?c.D:c.A);
      var v=VillaGeometry.Unit(other-p);
      if(Across(v))queue.Add(other);
      else if(!Along(v)||SiteFrame.Dot(other-p-shift,v)<.1)return false;
      affected.Add(edge);
     }
    }
    return true;
   }
   if(!Trace(a)||!Trace(b)||moves.Count>=bestCount)continue;
   var edits=new Dictionary<ConstructionCourse,PlanBezier>();
   Point Moved(Point p)=>moves.TryGetValue(Key(p),out var q)?q:p;
   foreach(var edge in affected){
    var c=edge.Curve;var u=Moved(c.A);var v=Moved(c.D);
    // Check only the newly occupied ribbon, including the full road width.
    var dir=VillaGeometry.Unit(c.D-c.A);var normal=new Point(-dir.Y,dir.X)*(edge.Width/2);
    var patches=new List<Point[]>();
    if(Across(dir)){
     var side=SiteFrame.Dot(u-c.A,normal)>0?normal:normal*(-1);
     patches.Add(new[]{c.A+side,c.D+side,v+side,u+side});
    }else{
     if(SiteFrame.Dot(u-c.A,dir)<-tolerance)patches.Add(new[]{u+normal,c.A+normal,c.A-normal,u-normal});
     if(SiteFrame.Dot(v-c.D,dir)>tolerance)patches.Add(new[]{c.D+normal,v+normal,v-normal,c.D-normal});
    }
    if(patches.Any(strip=>obstacles.Any(o=>VillaGeometry.Overlap(strip,o))))break;
    edits.Add(edge,TowerGeometry.Line(u,v));
   }
   if(edits.Count!=affected.Count)continue;
   // Preserve access to the original ribbon for every affected entrance and stall.
   bool KeepsAccess(Point p,double reserve){
    foreach(var edge in edits.Keys){
     var c=edge.Curve;double limit=edge.Width/2-reserve;
     if((VillaGeometry.Nearest(p,c.A,c.D)-p).Length>limit+tolerance)continue;
     if(!roads.Any(r=>{
      if(r.Use=="pedestrian"||r==link)return false;
      var line=edits.TryGetValue(r,out var next)?next:r.Curve;
      if(!line.NearBounds(p,r.Width/2))return false;
      var nearest=Straight(line)?VillaGeometry.Nearest(p,line.A,line.D):line.At(line.Parameter(p));
      return (nearest-p).Length<=r.Width/2-reserve+tolerance;
     }))return false;
    }
    return true;
   }
   if(plan.Parcels.Any(p=>p.Kind==0&&p.Access.HasValue&&!KeepsAccess(plan.World(p.Access.Value),0))||
      plan.ParkingConnections.Any(c=>c.Ports.Any(p=>!KeepsAccess(plan.World(p),1.5))))continue;
   // An existing path close enough for clipping to change anchors the row.
   if(roads.Any(path=>path.Use=="pedestrian"&&edits.Any(edit=>{
    var c=edit.Key.Curve;double pad=(edit.Key.Width+path.Width)/2+(b-a).Length+WalkRoadClearance.Gap;
    var w=path.Curve;
    return Math.Min(c.A.X,c.D.X)-pad<=Math.Max(Math.Max(w.A.X,w.B.X),Math.Max(w.C.X,w.D.X))&&
           Math.Max(c.A.X,c.D.X)+pad>=Math.Min(Math.Min(w.A.X,w.B.X),Math.Min(w.C.X,w.D.X))&&
           Math.Min(c.A.Y,c.D.Y)-pad<=Math.Max(Math.Max(w.A.Y,w.B.Y),Math.Max(w.C.Y,w.D.Y))&&
           Math.Max(c.A.Y,c.D.Y)+pad>=Math.Min(Math.Min(w.A.Y,w.B.Y),Math.Min(w.C.Y,w.D.Y));
   })))continue;
   best=edits;bestCount=moves.Count;
  }
  if(best==null)return;
  foreach(var edge in best.Keys.Concat(new[]{link})){
   foreach(var node in new[]{Key(edge.Curve.A),Key(edge.Curve.D)})incident[node].Remove(edge);
  }
  roads.Remove(link);
  foreach(var edit in best){
   edit.Key.Curve=edit.Value;
   foreach(var node in new[]{Key(edit.Value.A),Key(edit.Value.D)}){
    if(!incident.TryGetValue(node,out var at))incident[node]=at=new List<ConstructionCourse>();
    at.Add(edit.Key);
   }
  }
 }
 internal static void MergeCloseJunctions(List<ConstructionCourse> roads,bool tower=true,Plan slab=null){
  var candidates=roads.Where(r=>r.Use!="pedestrian"&&Straight(r.Curve)&&
   (r.Curve.D-r.Curve.A).Length<=Math.Min(4,r.Width*.5)+1e-6).ToArray();
  if(candidates.Length==0)return;
  Point Node(Point p)=>new Point(Math.Round(p.X,5),Math.Round(p.Y,5));
  var incident=new Dictionary<Point,List<ConstructionCourse>>();
  foreach(var r in roads.Where(r=>r.Use!="pedestrian")){
   foreach(var p in new[]{Node(r.Curve.A),Node(r.Curve.D)}){
    if(!incident.TryGetValue(p,out var list))incident[p]=list=new List<ConstructionCourse>();
    list.Add(r);
   }
  }
  foreach(var link in candidates){
   var a=Node(link.Curve.A);var b=Node(link.Curve.D);
   if(!incident.TryGetValue(a,out var left)||!incident.TryGetValue(b,out var right))continue;
   double length=(b-a).Length;
   // Absorb a tiny transition into the next span, keeping the junction fixed.
   if(tower&&length<=Math.Min(.5,link.Width/8)&&left.Count>=2&&right.Count>=2&&(left.Count==2||right.Count==2)){
    bool removeStart=left.Count==2;
    var from=removeStart?a:b;var to=removeStart?b:a;
    var keep=removeStart?right:left;
    var next=(removeStart?left:right).FirstOrDefault(r=>r!=link);
    if(next==null||next.Use!=link.Use||next.Width!=link.Width||keep.Contains(next))continue;
    var curve=next.Curve;
    if((curve.D-curve.A).Length<8*length)continue;
    if(Node(curve.A).Equals(from)){curve.B=curve.B+(to-from);curve.A=to;}
    else{curve.C=curve.C+(to-from);curve.D=to;}
    if(!next.Curved)next.Curve=TowerGeometry.Line(curve.A,curve.D);
    keep.Remove(link);keep.Add(next);incident.Remove(from);roads.Remove(link);
    continue;
   }
   if(left.Count!=3||right.Count!=3)continue;
   var arms=left.Concat(right).Where(r=>r!=link).ToArray();
   if(arms.Length!=4||arms.Distinct().Count()!=4||arms.Any(r=>!Straight(r.Curve)))continue;
   var directions=new List<Point>();
   foreach(var r in arms){
    bool start=Node(r.Curve.A).Equals(a)||Node(r.Curve.A).Equals(b);
    var delta=start?r.Curve.D-r.Curve.A:r.Curve.A-r.Curve.D;
    if(delta.Length<=2*length||length>r.Width*.5+1e-6)break;
    var direction=delta*(1/delta.Length);
    // Two arms leaving on the same bearing need separate junctions.
    if(directions.Any(v=>SiteFrame.Dot(v,direction)>.7))break;
    directions.Add(direction);
   }
   if(directions.Count!=4)continue;
   if(slab!=null){
    AlignSlabCrossing(slab,roads,incident,link,a,b,directions);
    continue;
   }
   var joint=(a+b)*.5;
   foreach(var r in arms){
    bool start=Node(r.Curve.A).Equals(a)||Node(r.Curve.A).Equals(b);
    r.Curve=start?TowerGeometry.Line(joint,r.Curve.D):TowerGeometry.Line(r.Curve.A,joint);
   }
   roads.Remove(link);incident.Remove(a);incident.Remove(b);
   incident[Node(joint)]=arms.ToList();
  }
 }
}
}
