using System;
using System.Linq;
using System.Collections.Generic;
namespace ApartmentPlan {
// Replace a terminal hard corner with one tangent cubic; never round a T junction.
public static class TowerFrontJoins {
 static PlanBezier Reverse(PlanBezier c)=>new PlanBezier{A=c.D,B=c.C,C=c.B,D=c.A};
 static PlanBezier Move(PlanBezier c,Point d)=>new PlanBezier{A=c.A+d,B=c.B+d,C=c.C+d,D=c.D+d};
 static bool Near(Point a,Point b)=>(a-b).Length<.01;
 // A third road may pass THROUGH the junction without an endpoint there.
 public static bool HasJunction(Point corner,IEnumerable<PlanBezier> others)=>others.Any(c=>c.NearBounds(corner,.02)&&(c.At(c.Parameter(corner))-corner).Length<.02);
 public static int Apply(Plan p,IList<Point> poly,List<Parcel> lots,List<Point[]> excluded,LayoutOptions o,TowerRules rules,IList<Asset> assets){
  int count=0;double radius=rules["RoadRadius"],gap=rules["CurveGap"];
  var frontRoutes=p.Routes.Where(q=>q.Use=="aisle"&&q.Curves.Count==1&&TowerConstruction.Straight(q.Curves[0])).Where(q=>lots.Any(l=>l.Kind==0&&l.Access.HasValue&&(VillaGeometry.Nearest(l.Access.Value,q.Curves[0].A,q.Curves[0].D)-l.Access.Value).Length<=o.ParkingAisle/2+.01)).ToList();
  foreach(var front in frontRoutes){foreach(bool frontReverse in new[]{false,true}){
   bool done=false;var f=frontReverse?Reverse(front.Curves[0]):front.Curves[0];
   foreach(var route in p.Routes.Where(q=>q.Use=="aisle"&&q!=front).ToList()){
    foreach(bool reverse in new[]{false,true}){
     var cs=(reverse?route.Curves.AsEnumerable().Reverse().Select(Reverse):route.Curves).ToList();var last=cs.Last();
     if(!TowerConstruction.Straight(last)||(last.D-f.A).Length>4.001)continue;
     var incoming=VillaGeometry.Unit(last.D-last.A);var outgoing=VillaGeometry.Unit(f.D-f.A);double dot=SiteFrame.Dot(incoming,outgoing);
     if(Math.Abs(dot)>.001&&Math.Abs(dot-Math.Sqrt(.5))>.001)continue;
     double cross=Layout.Cross(incoming,outgoing);if(Math.Abs(cross)<.001)continue;
     var corner=last.A+incoming*(Layout.Cross(f.A-last.A,outgoing)/cross);
     if((corner-f.A).Length>4.001||SiteFrame.Dot(corner-last.A,incoming)<=0)continue;
     // Preserve all third-party junctions. Only the two roads' private tails may move.
     var other=p.Routes.Where(q=>q!=front&&q!=route).SelectMany(q=>q.Curves).ToList();
     if(HasJunction(corner,other.Concat(cs.Take(cs.Count-1))))continue;
     double cut=radius*Math.Tan(Math.Acos(dot)/2);var frontStart=corner+outgoing*cut;
     if(SiteFrame.Dot(f.D-frontStart,outgoing)<(f.D-f.A).Length*.5)continue;
     if(other.Any(c=>new[]{c.A,c.D}.Any(x=>(VillaGeometry.Nearest(x,f.A,frontStart)-x).Length<.01)))continue;
     // If rounding consumes the final bend gap, move the preceding parallel leg
     // and its two elbows together. Radius and the requested straight gap remain fixed.
     double need=gap+cut-(corner-last.A).Length;
     int movedFrom=cs.Count-1;
     if(cs.Count>1&&!TowerConstruction.Straight(cs[cs.Count-2])&&need>0){
      if(cs.Count<5||TowerConstruction.Straight(cs[cs.Count-4])||!TowerConstruction.Straight(cs[cs.Count-5]))continue;
      int k=cs.Count-5;var lead=cs[k];var shift=incoming*(-need-.02);
      if(Math.Abs(Layout.Cross(VillaGeometry.Unit(lead.D-lead.A),incoming))>.001)continue;
      var newEnd=lead.D+shift;if(SiteFrame.Dot(newEnd-lead.A,VillaGeometry.Unit(lead.D-lead.A))<gap)continue;
      cs[k]=TowerGeometry.Line(lead.A,newEnd);for(int j=k+1;j<cs.Count-1;j++)cs[j]=Move(cs[j],shift);
      last=TowerGeometry.Line(last.A+shift,last.D);cs[cs.Count-1]=last;movedFrom=k;
     }
     var fillet=TowerGeometry.Fillet(new[]{last.A,corner,f.D},radius,false);if(fillet==null||fillet.Count!=3)continue;
     var changed=route.Curves.Skip(reverse?0:movedFrom).Take(reverse?route.Curves.Count-movedFrom:route.Curves.Count).ToList();
     if(other.Any(c=>new[]{c.A,c.D}.Any(x=>changed.Any(z=>(z.At(z.Parameter(x))-x).Length<.01))))continue;
     cs.RemoveAt(cs.Count-1);cs.AddRange(fillet.Take(2));
     var newFront=TowerGeometry.Route(new[]{frontReverse?Reverse(fillet[2]):fillet[2]},"aisle",front.Width);
     var newRoute=TowerGeometry.Route(reverse?cs.AsEnumerable().Reverse().Select(Reverse):cs,"aisle",route.Width);
     var newCurves=cs.Skip(movedFrom).ToList();
     bool valid=true;
     foreach(var curve in newCurves){var points=VillaGeometry.Sample(curve,.5);
      for(int i=1;i<points.Count;i++){var a=points[i-1];var b=points[i];var box=VillaGeometry.Box((a+b)*.5,VillaGeometry.Unit(b-a),route.Width-.02,(b-a).Length);
       if(!VillaGeometry.Inside(box,poly)||lots.Any(l=>VillaGeometry.Overlap(box,l.PlacementOutline))||excluded.Any(z=>VillaGeometry.Overlap(box,z))){valid=false;break;}
       foreach(var ring in p.Routes.Where(q=>q.Use=="turnaround"))for(int j=1;j<ring.Points.Count;j++)if(TowerRouting.SegmentDistance(a,b,ring.Points[j-1],ring.Points[j])<(route.Width+ring.Width)/2+rules["TurnaroundGap"]-.001)valid=false;
      }
      if(!TowerConstruction.Straight(curve)){
       foreach(var fr in frontRoutes.Where(q=>q!=front))if(!TowerRouting.FrontCurveClear(curve,fr.Curves[0],rules["FrontCurveGap"]))valid=false;
       foreach(var old in other.Concat(cs.Take(movedFrom)).Where(c=>!TowerConstruction.Straight(c))){var samples=VillaGeometry.Sample(old,.5);if(points.Any(x=>samples.Skip(1).Select((b,i)=>(VillaGeometry.Nearest(x,samples[i],b)-x).Length).Min()<gap-.01))valid=false;}
      }
     }
     if(!valid)continue;
     var oldRoute=route.Curves;var oldFront=front.Curves;var oldPoints=route.Points;var oldFrontPoints=front.Points;
     route.Curves=newRoute.Curves;route.Points=newRoute.Points;front.Curves=newFront.Curves;front.Points=newFront.Points;
     if(FrontageNetwork.Missing(p,assets,o.ParkingAisle,1)>0){route.Curves=oldRoute;route.Points=oldPoints;front.Curves=oldFront;front.Points=oldFrontPoints;continue;}
     count++;done=true;break;
    }if(done)break;
   }
  }}
  p.Diagnostics+=" frontageFillets="+count;return count;
 }
}
}
