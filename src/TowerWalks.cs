using System;
using System.Linq;
using System.Collections.Generic;
namespace ApartmentPlan {
public static class TowerWalks {
 public static void Generate(Plan p,IList<Point> poly,List<Parcel> lots,List<Point[]> excluded,LayoutOptions o,TowerRules rules,Point u,Point v){
  if(rules["Walks"]==0)return;double w=o.Walkway,half=w/2,cell=rules["WalkCell"],radius=rules["WalkRadius"];
  Point UV(Point q)=>new Point(SiteFrame.Dot(q,u),SiteFrame.Dot(q,v));Point XY(Point q)=>u*q.X+v*q.Y;
  var roads=p.Routes.Where(q=>q.Use!="pedestrian").ToList();var ribbons=new List<Point[]>();foreach(var road in roads)for(int i=1;i<road.Points.Count;i++){var a=road.Points[i-1];var b=road.Points[i];ribbons.Add(VillaGeometry.Box((a+b)*.5,VillaGeometry.Unit(b-a),road.Width+2*rules["RoadWalkGap"],(b-a).Length+.01));}
  // Broad-phase lookup only: exact polygon tests and all clearances stay unchanged.
  var obstacles=new SpatialLookup<Point[]>(24);
  foreach(var shape in lots.Select(l=>l.PlacementOutline).Concat(excluded).Concat(ribbons))obstacles.Add(shape,shape.Min(q=>q.X),shape.Min(q=>q.Y),shape.Max(q=>q.X),shape.Max(q=>q.Y));
  bool Clear(IList<Point> shape,bool boundary=true){
   if(!VillaGeometry.Inside(shape,poly)||boundary&&shape.Any(q=>TowerGeometry.Distance(q,poly)<rules["WalkBoundary"]-.001))return false;
   double minX=double.MaxValue,minY=double.MaxValue,maxX=double.MinValue,maxY=double.MinValue;
   foreach(var q in shape){minX=Math.Min(minX,q.X);minY=Math.Min(minY,q.Y);maxX=Math.Max(maxX,q.X);maxY=Math.Max(maxY,q.Y);}
   foreach(var obstacle in obstacles.Within(minX,minY,maxX,maxY))if(VillaGeometry.Overlap(shape,obstacle))return false;
   return true;
  }
  bool RouteClear(PlanRoute route,bool boundary=true)=>route.Points.Skip(1).Select((b,i)=>{var a=route.Points[i];return Clear(VillaGeometry.Box((a+b)*.5,VillaGeometry.Unit(b-a),w,(b-a).Length),boundary);}).All(q=>q);
  var approaches=new HashSet<PlanBezier>();
  void Add(PlanRoute route){p.Routes.Add(route);}

  var bound=VillaGeometry.Bounds(poly.Select(UV).ToList());var cells=new List<Parcel>();
  for(double y=Math.Floor(bound.Y/cell)*cell;y<bound.Y+bound.D;y+=cell)for(double x=Math.Floor(bound.X/cell)*cell;x<bound.X+bound.W;x+=cell){o.Cancellation.ThrowIfCancellationRequested();var c=new Parcel{Kind=4,X=x,Y=y,W=cell,D=cell};if(Clear(c.Corners.Select(XY).ToArray()))cells.Add(c);}
  var loops=new List<PlanRoute>();var regions=new List<List<Point>>();
  foreach(var outline in SurfaceMerge.Create(cells)){
   if(outline.Count<3)continue;var vertices=outline.ToList();double winding=Layout.SignedArea(vertices)>0?1:-1;var inset=new List<Point>();
   for(int i=0;i<vertices.Count;i++){var a=VillaGeometry.Unit(vertices[i]-vertices[(i+vertices.Count-1)%vertices.Count]);var b=VillaGeometry.Unit(vertices[(i+1)%vertices.Count]-vertices[i]);var na=new Point(-a.Y,a.X)*winding;var nb=new Point(-b.Y,b.X)*winding;double den=1+SiteFrame.Dot(na,nb);if(den<.1){inset.Clear();break;}inset.Add(XY(vertices[i]+(na+nb)*((half+.2)/den)));}
   if(inset.Count<3)continue;
   // Simplify raster steps with collision-checked 45-degree chords. Concavities stay intact.
   bool changed=true;while(changed&&inset.Count>3){changed=false;for(int i=0;i<inset.Count;i++){int prev=(i+inset.Count-1)%inset.Count,next=(i+1)%inset.Count;var d=UV(inset[next]-inset[prev]);if(Math.Abs(Math.Abs(d.X)-Math.Abs(d.Y))>.05)continue;var trial=inset.ToList();trial.RemoveAt(i);var cs=TowerGeometry.Fillet(trial,radius,true);if(cs!=null&&RouteClear(TowerGeometry.Route(cs,"pedestrian",w))){inset=trial;changed=true;break;}}}
   // Within the configured boundary band, accept a boundary-parallel edge even
   // when its bearing differs from the building's 45/90 degree axes.
   for(int k=0;k<inset.Count;k++){int next=(k+1)%inset.Count;var a=inset[k];var b=inset[next];
    for(int edge=0;edge<poly.Count;edge++){var x=poly[edge];var y=poly[(edge+1)%poly.Count];var dir=VillaGeometry.Unit(y-x);var normal=new Point(-dir.Y,dir.X)*(Layout.SignedArea(poly)>0?1:-1);double da=SiteFrame.Dot(a-x,normal),db=SiteFrame.Dot(b-x,normal);
     if(Math.Min(da,db)<0||Math.Max(da,db)>rules["BoundaryParallelRange"]||Math.Abs(SiteFrame.Dot(VillaGeometry.Unit(b-a),dir))<.7)continue;
     double offset=Math.Max(rules["WalkBoundary"]+half+.1,Math.Min(da,db));var trial=inset.ToList();trial[k]=a+normal*(offset-da);trial[next]=b+normal*(offset-db);var candidate=TowerGeometry.Fillet(trial,radius,true);if(candidate!=null&&RouteClear(TowerGeometry.Route(candidate,"pedestrian",w))){inset=trial;break;}
    }
   }
   var curves=TowerGeometry.Fillet(inset,radius,true);if(curves==null)continue;var loop=TowerGeometry.Route(curves,"pedestrian",w);if(!RouteClear(loop))continue;Add(loop);loops.Add(loop);regions.Add(inset);
   // Optional facility consumes a real free parcel rather than forcing every plan to reserve one.
   if(o.TowerFacility&&!p.Parcels.Any(q=>q.Kind==5)){var c=inset.Aggregate(new Point(),(a,b)=>a+b)*(1d/inset.Count);var shape=VillaGeometry.Box(c,v,rules["FacilityWidth"],rules["FacilityDepth"]);if(Clear(shape)){var facility=VillaGeometry.Bounds(shape,5);facility.PlacementOutline=shape;p.Parcels.Add(facility);}}
  }
  bool Straight(PlanBezier c)=>Math.Abs(Layout.Cross(c.D-c.A,c.B-c.A))<.001;
  var hazards=roads.SelectMany(q=>q.Curves.Where(c=>!Straight(c)).SelectMany(c=>new[]{c.A,c.D})).ToList();
  foreach(var road in roads)foreach(var c in road.Curves.Where(Straight))foreach(var other in roads.Where(q=>q!=road))foreach(var e in other.Curves.Where(Straight)){var d=c.D-c.A;var f=e.D-e.A;double cross=Layout.Cross(d,f);if(Math.Abs(cross)<.001)continue;double t=Layout.Cross(e.A-c.A,f)/cross,s=Layout.Cross(e.A-c.A,d)/cross;if(t>=0&&t<=1&&s>=0&&s<=1)hazards.Add(c.A+d*t);}
  // Try another attachment before discarding an otherwise useful walking loop.
  bool WalkClear(PlanRoute route)=>!p.Routes.Where(r=>r.Use=="pedestrian").Any(r=>route.Curves.Any(a=>r.Curves.Any(b=>TowerWalkPolicy.ParallelConflict(a,w,b,r.Width,rules["WalkParallelGap"])||TowerWalkPolicy.CurveConflict(a,w,b,r.Width))));
  var extras=new List<PlanRoute>();
  foreach(var loop in loops){var choices=(from c in loop.Curves.Where(Straight) from road in roads from edge in road.Curves.Where(Straight) let a=(c.A+c.D)*.5 let b=VillaGeometry.Nearest(a,edge.A,edge.D) orderby (a-b).Length select new{a,b,road,edge}).ToList();
   foreach(var q in choices){if(hazards.Any(h=>(h-q.b).Length<rules["ApproachExclusion"]))continue;var dir=VillaGeometry.Unit(q.b-q.a);var roadDir=VillaGeometry.Unit(q.edge.D-q.edge.A);if(!TowerWalkPolicy.ApproachAngle(TowerGeometry.Line(q.a,q.b),q.edge,rules["WalkApproachAngle"]))continue;double sine=Math.Abs(Layout.Cross(dir,roadDir));var end=q.b-dir*((q.road.Width/2+rules["RoadWalkGap"]+half+.02)/sine);if(SiteFrame.Dot(end-q.a,dir)<=0)continue;var route=TowerGeometry.Route(new[]{TowerGeometry.Line(q.a,end)},"pedestrian",w);if(!RouteClear(route)||!WalkClear(route))continue;Add(route);foreach(var c in route.Curves)approaches.Add(c);extras.Add(route);break;}

  }
  for(int i=0;i<loops.Count;i++)for(int j=0;j<i;j++){
   var pair=(from ca in loops[i].Curves.Where(Straight) from cb in loops[j].Curves.Where(Straight) from a in new[]{ca.A,ca.D,(ca.A+ca.D)*.5} let b=VillaGeometry.Nearest(a,cb.A,cb.D) orderby (a-b).Length select new{a,b}).FirstOrDefault();if(pair==null)continue;if((pair.a-pair.b).Length-w>rules["LoopBridgeGap"])continue;var route=TowerGeometry.Route(new[]{TowerGeometry.Line(pair.a,pair.b)},"pedestrian",w);if(RouteClear(route)&&WalkClear(route)){Add(route);extras.Add(route);}
  }
  double threshold=lots.Where(l=>l.Kind==0).Select(l=>l.W*l.D).DefaultIfEmpty(1280).Average()*rules["CrossAreaPercent"]/100;
  for(int i=0;i<loops.Count;i++){
   if(Math.Abs(Layout.SignedArea(regions[i]))<=threshold)continue;var loop=loops[i];var sides=loop.Curves.Where(Straight).ToList();bool added=false;
   foreach(var a in sides){foreach(var b in sides.Where(b=>!ReferenceEquals(a,b))){var x=(a.A+a.D)*.5;var y=VillaGeometry.Nearest(x,b.A,b.D);var uv=UV(y-x);if(Math.Min(Math.Abs(uv.X),Math.Abs(uv.Y))>.05)continue;var candidate=TowerGeometry.Route(new[]{TowerGeometry.Line(x,y)},"pedestrian",w);if(!RouteClear(candidate)||candidate.Points.Any(q=>!Layout.Inside(q,regions[i])))continue;
     bool nearby=false;foreach(var q in candidate.Points){if((q-x).Length<rules["JunctionExclusion"]||(q-y).Length<rules["JunctionExclusion"])continue;foreach(var other in loops.Concat(extras)){if(other.Points.Skip(1).Select((z,k)=>(VillaGeometry.Nearest(q,other.Points[k],z)-q).Length).Min()<w+rules["CrossWalkGap"]-.01){nearby=true;break;}}if(nearby)break;}
     if(nearby||!WalkClear(candidate))continue;Add(candidate);extras.Add(candidate);added=true;break;}if(added)break;}
  }
  TowerWalkPolicy.Apply(p,approaches,rules);
  // Only surviving road-connected walks can sprout outward. One spur per straight span.
  if(rules["OutwardWalks"]>0){
   var spans=p.Routes.Where(q=>q.Use=="pedestrian").SelectMany(q=>q.Curves).Where(Straight).ToList();
   var used=new HashSet<PlanBezier>();
   for(int k=0;k<poly.Count;k++){var x=poly[k];var y=poly[(k+1)%poly.Count];var tangent=VillaGeometry.Unit(y-x);
    // Each original polygon edge gets its longest nearby surviving span.
    var c=spans.Where(z=>!used.Contains(z))
     .Where(z=>{var mid=(z.A+z.D)*.5;return (VillaGeometry.Nearest(mid,x,y)-mid).Length<=rules["BoundaryParallelRange"];})
     .OrderByDescending(z=>(z.D-z.A).Length).FirstOrDefault();
    if(c==null)continue;bool added=false;
    foreach(double fraction in new[]{.25,.75}){var a=c.A+(c.D-c.A)*fraction;
      var near=VillaGeometry.Nearest(a,x,y);if((near-a).Length>rules["BoundaryParallelRange"]||(near-a).Length<1+w)continue;
      var walkDirection=VillaGeometry.Unit(c.D-c.A);var normal=new Point(-walkDirection.Y,walkDirection.X);if(SiteFrame.Dot(normal,near-a)<0)normal=normal*-1;
      foreach(double skew in new[]{0d,1d,-1d}){var dir=VillaGeometry.Unit(normal+walkDirection*skew);double cross=Layout.Cross(dir,y-x);if(Math.Abs(cross)<1e-8)continue;
       double t=Layout.Cross(x-a,y-x)/cross,along=Layout.Cross(x-a,dir)/cross;if(t<=0||along<0||along>1)continue;
       // Maintain a full 1m gap from the boundary, including the angled path width.
       var boundaryNormal=new Point(-tangent.Y,tangent.X);double approach=Math.Abs(SiteFrame.Dot(dir,boundaryNormal));if(approach<.001)continue;
       var end=a+dir*(t-(1+half*Math.Abs(Layout.Cross(dir,boundaryNormal)))/approach);
       if(SiteFrame.Dot(end-a,dir)<=w)continue;
       var route=TowerGeometry.Route(new[]{TowerGeometry.Line(a,end)},"pedestrian",w);
       var shape=VillaGeometry.Box((a+end)*.5,dir,w,(end-a).Length);
       if(!RouteClear(route,false)||!WalkClear(route)||shape.Any(q=>TowerGeometry.Distance(q,poly)<.999))continue;
       Add(route);used.Add(c);added=true;break;
      }
     if(added)break;
    }
   }
   TowerWalkPolicy.Apply(p,approaches,rules);
  }
  foreach(var route in p.Routes.Where(q=>q.Use=="pedestrian")){var overlay=route.Curves.SelectMany(c=>VillaGeometry.Sample(c,4)).ToList();for(int i=1;i<overlay.Count;i++){var a=overlay[i-1];var b=overlay[i];if((b-a).Length<.001)continue;var shape=VillaGeometry.Box((a+b)*.5,VillaGeometry.Unit(b-a),route.Width,(b-a).Length);var parcel=VillaGeometry.Bounds(shape,9);parcel.PlacementOutline=shape;p.Parcels.Add(parcel);}}
 }
 // Landscaping uses its own fine mask, independent of walking-loop margins.
 public static void Parks(Plan p,IList<Point> poly,List<Point[]> excluded,LayoutOptions o){
  const double cell=2;var obstacles=new SpatialLookup<Point[]>(24);
  var shapes=p.Parcels.Where(q=>q.Kind==0||q.Kind==1||q.Kind==5||q.Kind==6||q.Kind==9).Select(q=>q.PlacementOutline??q.Corners).Concat(excluded).Concat(p.TowerEntryOutlines.Select(q=>q.ToArray())).ToList();
  foreach(var road in p.Routes)for(int i=1;i<road.Points.Count;i++){var a=road.Points[i-1];var b=road.Points[i];shapes.Add(VillaGeometry.Box((a+b)*.5,VillaGeometry.Unit(b-a),road.Width+.1,(b-a).Length+.1));}
  foreach(var shape in shapes)obstacles.Add(shape,shape.Min(q=>q.X),shape.Min(q=>q.Y),shape.Max(q=>q.X),shape.Max(q=>q.Y));
  var bounds=VillaGeometry.Bounds(poly);var cells=new List<Parcel>();
  for(double y=Math.Floor(bounds.Y/cell)*cell;y<bounds.Y+bounds.D;y+=cell)for(double x=Math.Floor(bounds.X/cell)*cell;x<bounds.X+bounds.W;x+=cell){
   o.Cancellation.ThrowIfCancellationRequested();var c=new Parcel{Kind=4,X=x,Y=y,W=cell,D=cell};var shape=c.Corners;
   if(!VillaGeometry.Inside(shape,poly)||obstacles.Within(x,y,x+cell,y+cell).Any(q=>VillaGeometry.Overlap(shape,q)))continue;cells.Add(c);
  }
  var merged=ParkingSurfaceGeometry.Union(cells).ToList();foreach(var green in merged)green.Kind=4;p.Parcels.AddRange(merged);foreach(var outline in SurfaceMerge.Create(merged))p.TowerGrassOutlines.Add(outline.ToList());
 }
}
}
