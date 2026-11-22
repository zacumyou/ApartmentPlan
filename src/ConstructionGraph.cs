using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
// World-space graph. Split crossings/collinear overlaps before native generation,
// so T-junctions use exactly equal endpoint positions rather than visual contact.
public sealed class ConstructionEdge {public Point A,B;public string Use;public double Width;}
public static class ConstructionGraph {
 internal static bool ParallelRibbons(Point a,Point b,double width,Point c,Point d,double otherWidth){
  var span=b-a;double length=span.Length;
  if(length<.1||(d-c).Length<.1)return false;
  var unit=span*(1/length);
  if(Math.Abs(Layout.Cross(unit,d-c))>.05)return false;
  double offset=Math.Abs(Layout.Cross(unit,c-a));
  if(offset<.02||offset>=(width+otherWidth)/2-.05)return false;
  double u=SiteFrame.Dot(c-a,unit),v=SiteFrame.Dot(d-a,unit);
  return Math.Min(length,Math.Max(u,v))-Math.Max(0,Math.Min(u,v))>.5;
 }
 public static List<ConstructionEdge> Create(Plan plan,bool preserveCurves=true){
  var raw=new List<ConstructionEdge>();
  foreach(var route in plan.ConstructionRoutes.Concat(plan.Routes)){
   var points=preserveCurves&&route.Curves?.Count>0?route.Points:Simplify(route.Points,.35);
   for(int i=1;i<points.Count;i++)if((points[i]-points[i-1]).Length>.1)raw.Add(new ConstructionEdge{A=plan.World(points[i-1]),B=plan.World(points[i]),Use=route.Use=="access"?"aisle":route.Use,Width=route.Width});
  }
  // Raster routes stop when their full footprint reaches another road's ribbon.
  // Extend to its centreline so a visual touch becomes a real junction.
  var extensions=new List<ConstructionEdge>();
  foreach(var e in raw)foreach(var p in new[]{e.A,e.B}){
   if(plan.Tower||plan.VillaExistingRoadsOnly)continue; // Tower endpoints and pedestrian gaps are explicit geometry.
   ConstructionEdge target=null;Point join=default;double best=double.MaxValue;
   var outward=(p-e.A).Length<.001?e.A-e.B:e.B-e.A;
   foreach(var other in raw){if(ReferenceEquals(e,other)||e.Use!="pedestrian"&&other.Use=="pedestrian")continue;
    var d=other.B-other.A;double t=Math.Max(0,Math.Min(1,SiteFrame.Dot(p-other.A,d)/SiteFrame.Dot(d,d)));var q=other.A+d*t;double distance=(q-p).Length;
    // Near contact still needs a split and shared node. The short-edge contraction below welds it.
    if(distance<.03){target=other;join=q;best=distance;break;}
    if(plan.Villa&&e.Use!="pedestrian"){if(!ForwardJoin(e,p,other,other.Width/2+2,out q))continue;distance=(q-p).Length;}
    if(SiteFrame.Dot(q-p,outward)<-.001||distance>other.Width/2+2||distance>=best)continue;
    // Do not turn neighbouring parallel aisles into diagonal roads.
    if(e.Use!="pedestrian"&&Math.Abs(Layout.Cross(e.B-e.A,d))/((e.B-e.A).Length*d.Length)<.15&&t>.001&&t<.999)continue;
    if(e.Use=="pedestrian"&&!ClearPath(plan,p,q,e.Width))continue;
    target=other;join=q;best=distance;
   }
   if(target!=null&&best>.000001&&e.Use!="pedestrian"&&raw.Concat(extensions).Any(r=>r.Use!="pedestrian"&&ParallelRibbons(p,join,e.Width,r.A,r.B,r.Width)))continue;
   if(target!=null&&best>.000001)extensions.Add(new ConstructionEdge{A=p,B=join,Use=e.Use,Width=e.Width});
  }
  raw.AddRange(extensions);
  var cuts=raw.Select(e=>new List<double>{0,1}).ToArray();
  for(int i=0;i<raw.Count;i++)for(int j=0;j<i;j++){
   var a=raw[i];var b=raw[j];if((a.Use=="pedestrian")!=(b.Use=="pedestrian"))continue;var u=a.B-a.A;var v=b.B-b.A;double cross=Layout.Cross(u,v);
   if(Math.Abs(cross)>1e-8){double t=Layout.Cross(b.A-a.A,v)/cross,s=Layout.Cross(b.A-a.A,u)/cross;if(t>=-1e-7&&t<=1+1e-7&&s>=-1e-7&&s<=1+1e-7){cuts[i].Add(Math.Max(0,Math.Min(1,t)));cuts[j].Add(Math.Max(0,Math.Min(1,s)));}}
   else if(Math.Abs(Layout.Cross(b.A-a.A,u))/u.Length<.02){foreach(var p in new[]{b.A,b.B})Cut(a,p,cuts[i]);foreach(var p in new[]{a.A,a.B})Cut(b,p,cuts[j]);}
  }
  var result=new List<ConstructionEdge>();var keys=new HashSet<string>();
  // Main road wins duplicate spans at entrances and shared court connections.
  foreach(int i in Enumerable.Range(0,raw.Count).OrderBy(i=>raw[i].Use=="main"?0:raw[i].Use=="aisleWide"?1:raw[i].Use=="aisle"?2:3)){
   var e=raw[i];var t=cuts[i].Distinct().OrderBy(x=>x).ToList();
   for(int k=1;k<t.Count;k++){var a=e.A+(e.B-e.A)*t[k-1];var b=e.A+(e.B-e.A)*t[k];if((b-a).Length<.000001)continue;string ka=Key(a),kb=Key(b),key=string.CompareOrdinal(ka,kb)<0?ka+"/"+kb:kb+"/"+ka;if(keys.Add(key))result.Add(new ConstructionEdge{A=a,B=b,Use=e.Use,Width=e.Width});}
  }
  // Nearby cuts can create sub-10cm junction spans. Dropping that edge alone
  // disconnects the road graph. Contract its two nodes across every incident
  // edge instead, retaining the first (main-road-priority) endpoint.
  while(true){
   var shortEdge=result.FirstOrDefault(e=>(e.B-e.A).Length<.1);if(shortEdge==null)break;
   var a=shortEdge.A;string b=Key(shortEdge.B);
   foreach(var e in result){if(Key(e.A)==b)e.A=a;if(Key(e.B)==b)e.B=a;}
   result.Remove(shortEdge);result.RemoveAll(e=>(e.B-e.A).Length<.000001);
  }
  keys.Clear();result.RemoveAll(e=>{string a=Key(e.A),b=Key(e.B);return !keys.Add(string.CompareOrdinal(a,b)<0?a+"/"+b:b+"/"+a);});
  MergeStraight(result);
  // Raster targets can leave very short path corners/junctions. Collapse only
  // pedestrian nodes, and recheck the full corridor against lots and the site.
  // These short chords represent one authored Bezier, not tiny native paths.
  // ConstructionCurves restores that curve; welding its samples destroys it.
  var curvedWalkNodes=new HashSet<string>(plan.Routes.Concat(plan.ConstructionRoutes).Where(r=>r.Use=="pedestrian"&&r.Curves?.Count>0).SelectMany(r=>r.Points).Select(p=>Key(plan.World(p))));
  bool welded=true;while(welded){welded=false;foreach(var edge in result.ToArray()){
   if(edge.Use!="pedestrian"||(edge.B-edge.A).Length>=edge.Width-.01)continue;
   if(curvedWalkNodes.Contains(Key(edge.A))||curvedWalkNodes.Contains(Key(edge.B)))continue;
   var incident=Incident(result,edge.A).Concat(Incident(result,edge.B)).Distinct().ToList();
   if(incident.Count<=1||incident.Any(e=>e.Use!="pedestrian"))continue;
   var to=(edge.A+edge.B)*.5;
   if(!incident.Where(e=>!ReferenceEquals(e,edge)).All(e=>ClearPath(plan,to,(e.A-edge.A).Length<.03||(e.A-edge.B).Length<.03?e.B:e.A,e.Width)))continue;
   var a=edge.A;var b=edge.B;
   foreach(var other in incident){if((other.A-a).Length<.03||(other.A-b).Length<.03)other.A=to;if((other.B-a).Length<.03||(other.B-b).Length<.03)other.B=to;}
   result.Remove(edge);welded=true;break;
  }}
  MergeStraight(result);
  if(!plan.Tower&&!plan.VillaExistingRoadsOnly)JoinRibbonComponents(result,plan.Villa);
  if(!plan.Tower&&!plan.Villa)RemoveCoveredJunctionStubs(plan,result);
  return result;
 }
 // Extend the source tangent to the target centreline, never project sideways.
 // A wide ribbon alone is not evidence that parallel streets should be joined.
 static bool ForwardJoin(ConstructionEdge source,Point endpoint,ConstructionEdge target,double maximum,out Point join){
  join=default;var outward=(endpoint-source.A).Length<.001?source.A-source.B:source.B-source.A;
  var direction=VillaGeometry.Unit(outward);var span=target.B-target.A;double cross=Layout.Cross(direction,span);
  if(Math.Abs(cross)<.000001){
   if(Math.Abs(Layout.Cross(direction,target.A-endpoint))>.02)return false;
   join=VillaGeometry.Nearest(endpoint,target.A,target.B);
  }else{
   double travel=Layout.Cross(target.A-endpoint,span)/cross;
   double t=Layout.Cross(target.A-endpoint,direction)/cross;
   if(travel<0||t<0||t>1)return false;join=target.A+span*t;
  }
  double distance=(join-endpoint).Length;
  return distance>.000001&&distance<=maximum&&SiteFrame.Dot(join-endpoint,direction)>0;
 }
 // Join separate vehicle components only where an endpoint already lies in
 // another road ribbon. This does not bridge unrelated roads across empty land.
 static void JoinRibbonComponents(List<ConstructionEdge> edges,bool preserveHeading){
  while(true){
   var roads=edges.Where(e=>e.Use!="pedestrian").ToList();
   var incident=new Dictionary<string,List<int>>();
   for(int i=0;i<roads.Count;i++)foreach(var p in new[]{roads[i].A,roads[i].B}){var key=Key(p);if(!incident.TryGetValue(key,out var list))incident[key]=list=new List<int>();list.Add(i);}
   var component=Enumerable.Repeat(-1,roads.Count).ToArray();int count=0;
   for(int i=0;i<roads.Count;i++){if(component[i]>=0)continue;var queue=new List<int>{i};component[i]=count;
    for(int at=0;at<queue.Count;at++){int j=queue[at];foreach(var p in new[]{roads[j].A,roads[j].B})foreach(int k in incident[Key(p)])if(component[k]<0){component[k]=count;queue.Add(k);}}count++;
   }
   if(count<2)return;
   ConstructionEdge source=null,target=null;Point from=default,to=default;double best=double.MaxValue;
   for(int i=0;i<roads.Count;i++)foreach(var p in new[]{roads[i].A,roads[i].B})for(int j=0;j<roads.Count;j++){
    if(component[i]==component[j]||preserveHeading&&incident[Key(p)].Count!=1)continue;var e=roads[j];var d=e.B-e.A;double length2=SiteFrame.Dot(d,d);if(length2<1e-8)continue;
    Point q;if(preserveHeading){if(!ForwardJoin(roads[i],p,e,e.Width/2+.03,out q))continue;}else{double t=SiteFrame.Dot(p-e.A,d)/length2;if(t<0||t>1)continue;q=e.A+d*t;}double gap=(q-p).Length;
    if(gap<=.000001||gap>e.Width/2+.03||gap>=best)continue;
    if(roads.Any(r=>ParallelRibbons(p,q,roads[i].Width,r.A,r.B,r.Width)))continue;
    source=roads[i];target=e;from=p;to=q;best=gap;
   }
   if(target==null)return;
   // Use exact shared endpoints on both halves, not independent projections.
   if((to-target.A).Length<.1)to=target.A;
   else if((to-target.B).Length<.1)to=target.B;
   else{edges.Add(new ConstructionEdge{A=to,B=target.B,Use=target.Use,Width=target.Width});target.B=to;}
   edges.Add(new ConstructionEdge{A=from,B=to,Use=source.Use,Width=source.Width});
  }
 }
 // Remove only a leaf wholly covered by a straight through-road of the same
 // role/width. Never weld two junctions or move an existing branch endpoint.
 internal static void RemoveCoveredJunctionStubs(Plan plan,List<ConstructionEdge> edges){
  bool removed=false;
  var curved=plan.Routes.Concat(plan.ConstructionRoutes).Where(r=>r.Curves?.Count>0).ToArray();
  foreach(var stub in edges.ToArray()){
   if(stub.Use!="aisle"&&stub.Use!="aisleWide")continue;
   double length=(stub.B-stub.A).Length;if(length<.1||length>stub.Width/2-.01)continue;
   var ia=Incident(edges,stub.A);var ib=Incident(edges,stub.B);
   Point root,leaf;List<ConstructionEdge> arms;
   if(ia.Count==1&&ib.Count>=3){leaf=stub.A;root=stub.B;arms=ib;}
   else if(ib.Count==1&&ia.Count>=3){leaf=stub.B;root=stub.A;arms=ia;}
   else continue;
   if(curved.Any(r=>r.Curves.Any(c=>c.NearBounds(plan.Local(leaf),stub.Width)&&
      (c.At(c.Parameter(plan.Local(leaf)))-plan.Local(leaf)).Length<stub.Width)))continue;
   if(edges.Any(e=>e.Use=="pedestrian"&&((e.A-leaf).Length<.03||(e.B-leaf).Length<.03)))continue;
   var through=arms.Where(e=>!ReferenceEquals(e,stub)&&e.Use==stub.Use&&Math.Abs(e.Width-stub.Width)<.001).ToArray();
   Point End(ConstructionEdge e)=>(e.A-root).Length<.03?e.B:e.A;
   Point a=default,b=default;bool found=false;
   for(int i=0;i<through.Length&&!found;i++)for(int j=0;j<i;j++){
    var u=End(through[i])-root;var v=End(through[j])-root;
    if(SiteFrame.Dot(u,v)>=0||Math.Abs(Layout.Cross(u,v))>u.Length*v.Length*.0001)continue;
    a=End(through[i]);b=End(through[j]);found=true;break;
   }
   if(!found)continue;
   var axis=VillaGeometry.Unit(b-a);var direction=VillaGeometry.Unit(leaf-root);
   if(Math.Abs(SiteFrame.Dot(axis,direction))>.01)continue;
   var side=new Point(-direction.Y,direction.X)*(stub.Width/2);
   bool Covered(Point q){double t=SiteFrame.Dot(q-a,axis);return t>=0&&t<=(b-a).Length&&Math.Abs(Layout.Cross(axis,q-a))<=stub.Width/2-.001;}
   if(!new[]{root+side,root-side,leaf+side,leaf-side}.All(Covered))continue;
   // Protect explicit access and nearby building/parking/facility footprints.
   bool protectedLot=plan.Parcels.Where(p=>p.Kind==0||p.Kind==4||p.Kind==5||p.Kind==6||p.Kind==7).Any(p=>{
    if(p.Access.HasValue&&(VillaGeometry.Nearest(plan.World(p.Access.Value),root,leaf)-plan.World(p.Access.Value)).Length<=stub.Width/2+1)return true;
    var corners=p.PlacementOutline??new[]{new Point(p.X,p.Y),new Point(p.X+p.W,p.Y),new Point(p.X+p.W,p.Y+p.D),new Point(p.X,p.Y+p.D)};
    var world=corners.Select(plan.World).ToArray();double pad=stub.Width/2+1;
    return world.Min(q=>q.X)<=Math.Max(root.X,leaf.X)+pad&&world.Max(q=>q.X)>=Math.Min(root.X,leaf.X)-pad&&world.Min(q=>q.Y)<=Math.Max(root.Y,leaf.Y)+pad&&world.Max(q=>q.Y)>=Math.Min(root.Y,leaf.Y)-pad;
   });
   if(protectedLot)continue;
   edges.Remove(stub);removed=true;
  }
  if(removed)MergeStraight(edges);
 }
 static bool ClearPath(Plan plan,Point a,Point b,double width){
  a=plan.Local(a);b=plan.Local(b);double pad=width/2;
  var bounds=new Parcel{X=Math.Min(a.X,b.X)-pad,Y=Math.Min(a.Y,b.Y)-pad,W=Math.Abs(a.X-b.X)+width,D=Math.Abs(a.Y-b.Y)+width};
  return (plan.Boundary.Count==0||Layout.Contains(bounds,plan.Boundary.Select(plan.Local).ToList()))&&!plan.Parcels.Any(p=>(p.Kind==0||p.Kind==2||p.Kind==3||p.Kind==5||p.Kind==6)&&Layout.Overlap(bounds,p));
 }
 static List<ConstructionEdge> Incident(List<ConstructionEdge> edges,Point p)=>edges.Where(e=>(e.A-p).Length<.03||(e.B-p).Length<.03).ToList();
 static void MergeStraight(List<ConstructionEdge> edges){
  bool changed=true;while(changed){changed=false;
   foreach(var e in edges.ToArray()){
    foreach(var p in new[]{e.A,e.B}){
     var incident=Incident(edges,p);if(incident.Count!=2)continue;var other=ReferenceEquals(incident[0],e)?incident[1]:incident[0];
     // A role uses one native prefab. Court paving margins can make the
     // planning ribbon wider than its access ribbon, without a native change.
     if(e.Use!=other.Use)continue;
     var a=(e.A-p).Length<.03?e.B:e.A;var b=(other.A-p).Length<.03?other.B:other.A;var u=a-p;var v=b-p;
     if(SiteFrame.Dot(u,v)>=0||Math.Abs(Layout.Cross(u,v))>u.Length*v.Length*.0001)continue;
     e.A=a;e.B=b;e.Width=Math.Max(e.Width,other.Width);edges.Remove(other);changed=true;break;
    }
    if(changed)break;
   }
  }
 }
 static string Key(Point p)=>Math.Round(p.X*100)+","+Math.Round(p.Y*100);
 static void Cut(ConstructionEdge e,Point p,List<double> values){var d=e.B-e.A;double t=SiteFrame.Dot(p-e.A,d)/SiteFrame.Dot(d,d);if(t>0&&t<1)values.Add(t);}
 static List<Point> Simplify(IList<Point> p,double tolerance){
  if(p.Count<3)return p.ToList();var d=p[p.Count-1]-p[0];double best=0;int index=0;
  for(int i=1;i<p.Count-1;i++){double t=d.Length<.001?0:Math.Max(0,Math.Min(1,SiteFrame.Dot(p[i]-p[0],d)/SiteFrame.Dot(d,d)));double distance=(p[i]-p[0]-d*t).Length;if(distance>best){best=distance;index=i;}}
  if(best<=tolerance)return new List<Point>{p[0],p[p.Count-1]};var left=Simplify(p.Take(index+1).ToList(),tolerance);left.RemoveAt(left.Count-1);left.AddRange(Simplify(p.Skip(index).ToList(),tolerance));return left;
 }
}
}
