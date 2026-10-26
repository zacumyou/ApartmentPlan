using System;
using System.Linq;
using System.Collections.Generic;
namespace ApartmentPlan {
public static class TowerLayout {
 sealed class Court {public Point Gate,Center,Forward,Side,Anchor;public double W,D;public Point[] Envelope,Stem;}
 public static Plan Generate(IList<Point> boundary,IList<Point> entries,IList<Asset> assets,LayoutOptions o,IList<ManualZone> zones){
  if(!Layout.Valid(boundary))throw new ArgumentException("InvalidBoundary");if(entries.Count==0)throw new ArgumentException("EntranceFit");if(o.TowerParking==null)throw new ArgumentException("AP.Tower.SelectParking");
  var timer=System.Diagnostics.Stopwatch.StartNew();
  var p=SiteFrame.Create(boundary,entries[0]);p.Tower=true;p.TowerAlgorithmVersion=1;p.TowerSurface=o.TowerSurface;p.TowerEntranceSurface=o.TowerEntranceSurface;
  var rules=o.TowerRules??new TowerRules();p.TowerWalkGap=rules["RoadWalkGap"];var poly=boundary.Select(p.Local).ToList();var zonesLocal=(zones??new List<ManualZone>()).Select(z=>z.Points.Select(p.Local).ToArray()).ToList();
  if(p.Area>1000000)throw new PlanLimitException("AP.LimitSiteArea",p.Area,1000000);
  var bounds=VillaGeometry.Bounds(poly);if(Math.Max(bounds.W,bounds.D)>2048)throw new PlanLimitException("AP.LimitSiteExtent",Math.Max(bounds.W,bounds.D),2048);
  bool Fits(IList<Point> shape)=>VillaGeometry.Inside(shape,poly)&&!zonesLocal.Any(z=>VillaGeometry.Overlap(shape,z));
  var courts=new List<Court>();var lots=new List<Parcel>();double width=o.ParkingAisle,radius=rules["RoadRadius"],apron=2*radius;
  PlanRoute Add(IEnumerable<PlanBezier> curves,string use,double w){var r=TowerGeometry.Route(curves,use,w);p.Routes.Add(r);return r;}
  foreach(var entry in entries){
   var gateWorld=SiteFrame.Nearest(boundary,entry,out var inward);var f=new Point(SiteFrame.Dot(inward,p.Right),SiteFrame.Dot(inward,p.Forward));var side=new Point(f.Y,-f.X);var gate=p.Local(gateWorld);var asset=o.TowerParking;
   // Match the incoming road width; leave enough diagonal length for both fillets.
   // Keep the original parking center whenever the taper fits its existing court.
   double halfWidth=asset.Width/2+radius,neck=Math.Max(Math.Min(halfWidth-Math.Sqrt(2)*radius*Math.Tan(Math.PI/8)-.5,o.Road/2-o.TowerTurnWidth/2),radius*.5);
   double bottom=-asset.Depth/2-(asset.Width/2-neck)-o.TowerTurnWidth/Math.Sqrt(2)-1;
   double shoulder=bottom+halfWidth-neck,outerBottom=bottom-radius,outerTop=asset.Depth/2+apron;
   var c=new Court{Gate=gate,Forward=f,Side=side,W=asset.Width,D=asset.Depth};c.Center=gate+f*(rules["EntranceInset"]+Math.Max(asset.Depth/2+apron,-outerBottom));c.Envelope=VillaGeometry.Box(c.Center+f*((outerBottom+outerTop)/2),f,asset.Width+2*apron,outerTop-outerBottom);c.Stem=VillaGeometry.Box(gate+f*(rules["EntranceInset"]/2),f,o.Road,rules["EntranceInset"]);c.Anchor=c.Center-side*(asset.Width/2+radius);
   if(!Fits(c.Envelope)||courts.Any(q=>VillaGeometry.Overlap(q.Envelope,c.Envelope)))throw new ArgumentException("AP.Tower.EntranceFit");courts.Add(c);
   Point At(double x,double y)=>c.Center+side*x+f*y;
   var lot=new Parcel{Kind=6,AssetId=asset.Id,X=c.Center.X-asset.Width/2,Y=c.Center.Y-asset.Depth/2,W=asset.Width,D=asset.Depth,PlacementForward=f*-1,PlacementOutline=VillaGeometry.Box(c.Center,f,asset.Width,asset.Depth)};lots.Add(lot);p.Parcels.Add(lot);
   // Counter-clockwise one-way loop; taper only the entrance end at 45 degrees.
   Point[] RingPoints(double offset){double h=halfWidth+offset,n=neck+(Math.Sqrt(2)-1)*offset,b=bottom-offset,t=asset.Depth/2+radius+offset,s=shoulder-(Math.Sqrt(2)-1)*offset;
    return new[]{At(-n,b),At(n,b),At(h,s),At(h,t),At(-h,t),At(-h,s)};
   }
   var ring=TowerGeometry.Fillet(RingPoints(0),radius,true);if(ring==null)throw new ArgumentException("AP.Tower.EntranceFit");
   Add(ring,"turnaround",o.TowerTurnWidth);var mouth=At(0,bottom);Add(new[]{TowerGeometry.Line(gate,mouth)},"main",o.Road);
   // Two continuous U-shaped surfaces; no raster fragments and no unsupported hole.
   if(o.TowerEntrancePaving){
    var outer=TowerGeometry.Route(TowerGeometry.Fillet(RingPoints(radius),apron,true),"",0).Points;
    for(int sign=-1;sign<=1;sign+=2){var half=outer.Where(q=>sign*SiteFrame.Dot(q-c.Center,side)>=-.001).ToList();
     var bottomPoint=At(0,outerBottom);var top=At(0,outerTop);
     // Order the outer half by angle, then return along the parking rectangle.
     half=half.OrderBy(q=>Math.Atan2(SiteFrame.Dot(q-c.Center,f),sign*SiteFrame.Dot(q-c.Center,side))).ToList();
     var outline=new List<Point>{bottomPoint};outline.AddRange(half);outline.Add(top);outline.Add(At(0,asset.Depth/2));outline.Add(At(sign*asset.Width/2,asset.Depth/2));outline.Add(At(sign*asset.Width/2,-asset.Depth/2));outline.Add(At(0,-asset.Depth/2));p.TowerEntryOutlines.Add(TowerGeometry.SurfaceNodes(outline));
    }
   }
  }
  var selected=assets.Where(a=>a.Id!=o.TowerParking.Id&&a.Width>0&&a.Depth>0).ToList();if(selected.Count==0)return p;
  double angle=rules["Angle"]*Math.PI/180;var forward=new Point(Math.Sin(angle),-Math.Cos(angle));var right=new Point(-forward.Y,forward.X);double gap=Math.Max(0,rules["BuildingGap"]-rules["GapTolerance"]),step=rules["PackingStep"];
  bool EscapeFits(Point center,Asset asset,Point[] lot,Point facing,bool boundaryOnly=false){
   var forward=facing;var right=new Point(-facing.Y,facing.X);
   var mid=center+forward*(asset.Depth/2+width/2);double half=Math.Max(1,asset.Width/2-4);
   // Reject boundary-facing dead pockets before packing, not by deleting an
   // apartment after road routing fails. Other apartments remain higher priority.
   if(TowerGeometry.Distance(mid,poly)>2*(radius+rules["FrontCurveGap"]+width))return true;
   foreach(var arm in new[]{Tuple.Create(mid,forward),Tuple.Create(mid-right*half,right*-1),Tuple.Create(mid+right*half,right)})foreach(int sign in new[]{-1,1}){
    var turn=arm.Item1+arm.Item2*(radius+rules["FrontCurveGap"]+width);var side=new Point(-arm.Item2.Y,arm.Item2.X)*sign;
    var cs=TowerGeometry.Fillet(new[]{arm.Item1,turn,turn+side*(radius+width)},radius,false);if(cs==null)continue;
    var route=TowerGeometry.Route(cs,"aisle",width);bool clear=true;
    for(int i=1;i<route.Points.Count;i++){var a=route.Points[i-1];var b=route.Points[i];var ribbon=VillaGeometry.Box((a+b)*.5,VillaGeometry.Unit(b-a),width-.02,(b-a).Length);
     if(!(boundaryOnly?VillaGeometry.Inside(ribbon,poly):Fits(ribbon))||VillaGeometry.Overlap(ribbon,lot)||!boundaryOnly&&courts.Any(c=>VillaGeometry.Overlap(ribbon,c.Envelope)||VillaGeometry.Overlap(ribbon,c.Stem))){clear=false;break;}}
    if(clear)return true;
   }return false;
  }
  var candidates=new List<Parcel>();
  for(double y=bounds.Y;y<bounds.Y+bounds.D;y+=step)for(double x=bounds.X;x<bounds.X+bounds.W;x+=step){o.Cancellation.ThrowIfCancellationRequested();foreach(var a in selected){
    var center=new Point(x,y);var original=VillaGeometry.Box(center,forward,a.Width,a.Depth);
    if(!Fits(original)||original.Any(q=>TowerGeometry.Distance(q,poly)<rules["BuildingBoundary"]-.001))continue;
    var originalFront=VillaGeometry.Box(center+forward*(a.Depth/2+width/2),right,width,Math.Max(2,a.Width-8));
    bool boundaryBlocked=!VillaGeometry.Inside(originalFront,poly)||!EscapeFits(center,a,original,forward,true);
    var facings=boundaryBlocked&&rules["BoundaryTurn"]>0?new[]{right,right*-1}:new[]{forward};
    // Keep the shared bearing unless the original front has no boundary escape.
    // Then try both quarter-turns at exactly the same center, preferring more room.
    foreach(var facing in facings.OrderByDescending(f=>TowerGeometry.Distance(center+f*(a.Depth/2+width/2),poly))){
     var lateral=new Point(-facing.Y,facing.X);var shape=VillaGeometry.Box(center,facing,a.Width,a.Depth);
     if(!Fits(shape)||shape.Any(q=>TowerGeometry.Distance(q,poly)<rules["BuildingBoundary"]-.001)||courts.Any(c=>TowerGeometry.Distance(shape,c.Envelope)<gap||VillaGeometry.Overlap(shape,c.Stem)))continue;
     var access=center+facing*(a.Depth/2+width/2);var front=VillaGeometry.Box(access,lateral,width,Math.Max(2,a.Width-8));
     if(!Fits(front)||courts.Any(c=>VillaGeometry.Overlap(front,c.Envelope)||VillaGeometry.Overlap(front,c.Stem))||!EscapeFits(center,a,shape,facing))continue;
     candidates.Add(new Parcel{Kind=0,AssetId=a.Id,X=x-a.Width/2,Y=y-a.Depth/2,W=a.Width,D=a.Depth,Homes=a.Homes,FloorArea=(a.Footprint>0?a.Footprint:a.Width*a.Depth*o.FootprintRatio)*Math.Max(1,a.Height/o.FloorHeight),PlacementForward=facing,PlacementOutline=shape,Access=center+facing*(a.Depth/2),AccessEstimated=false});
    }
   }}
  List<Parcel> best=new List<Parcel>();
  // Multiple deterministic sweeps permit staggered lines without moving a single tower into an unsafe gap.
  for(int sweep=0;sweep<16;sweep++){double a=sweep*Math.PI/8;var axis=new Point(Math.Cos(a),Math.Sin(a));var chosen=new List<Parcel>();foreach(var candidate in candidates.OrderBy(q=>SiteFrame.Dot(new Point(q.X+q.W/2,q.Y+q.D/2),axis)).ThenBy(q=>q.AssetId)){
    if(chosen.Count>=rules["MaxBuildings"])break;if(chosen.Any(q=>TowerGeometry.Distance(q.PlacementOutline,candidate.PlacementOutline)<gap-.001))continue;chosen.Add(candidate);
   }if(chosen.Count>best.Count)best=chosen;}
  long packingMs=timer.ElapsedMilliseconds;
  lots.AddRange(best);p.Parcels.AddRange(best);
  var router=new TowerRouting(p,poly,lots,courts.Select(c=>c.Stem).Concat(zonesLocal).ToList(),right,forward,o,rules);
  foreach(var court in courts)router.Anchors.Add(court.Anchor);
  router.Build(best);TowerFrontJoins.Apply(p,poly,lots,courts.Select(c=>c.Stem).Concat(zonesLocal).ToList(),o,rules,assets);if(best.Count==0)throw new ArgumentException("AP.Tower.ConnectionFit");
  // Build preview ribbons from the same analytic routes used for native construction.
  foreach(var route in p.Routes){var overlay=route.Curves.SelectMany(c=>VillaGeometry.Sample(c,4)).ToList();for(int i=1;i<overlay.Count;i++){var a=overlay[i-1];var b=overlay[i];if((b-a).Length<.001)continue;var shape=VillaGeometry.Box((a+b)*.5,VillaGeometry.Unit(b-a),route.Width,(b-a).Length);var parcel=VillaGeometry.Bounds(shape,route.Use=="pedestrian"?9:1);parcel.InvisibleRoad=route.Use=="turnaround";parcel.PlacementOutline=shape;p.Parcels.Add(parcel);}}
  long roadsMs=timer.ElapsedMilliseconds-packingMs;
  TowerWalks.Generate(p,poly,lots,zonesLocal,o,rules,right,forward);
  TowerWalks.Parks(p,poly,zonesLocal,o);
  p.Homes=best.Sum(q=>q.Homes);p.Far=best.Sum(q=>q.FloorArea)/p.Area*100;p.RequestedParcels=p.Parcels.Count;p.ParcelLimit=o.MaxParcels;if(p.Parcels.Count>o.MaxParcels)throw new PlanLimitException("AP.LimitParcels",p.Parcels.Count,o.MaxParcels);
  p.Diagnostics+=" timingMs packing="+packingMs+" roads="+roadsMs+" walks="+(timer.ElapsedMilliseconds-packingMs-roadsMs)+" total="+timer.ElapsedMilliseconds;
  p.Diagnostics+=" rotated="+best.Count(q=>SiteFrame.Dot(q.PlacementForward.Value,forward)<.99)+" tower building-first buildings="+best.Count+" entries="+entries.Count+" entry-surfaces="+p.TowerEntryOutlines.Count;return p;
 }
}
}
