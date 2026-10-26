using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
internal sealed partial class ResidentialLayout {
 // Housing has already had its infill pass. Consume only enclosed paved courts,
 // independently of the minimum parking quota, never a park or perimeter strip.
 void FillPavedParkingCourts(){
  if(!o.ContinuousFrontage||o.Parking<=0||o.ParkingIsland>0)return;
  PaveEnclosedParking();
  var original=exact.Where(r=>r.Kind==2).ToArray();if(original.Length==0)return;
  bool preferred=original.Where(r=>!r.ParkingAlongX).Sum(r=>r.ParkingSpaces)>=original.Where(r=>r.ParkingAlongX).Sum(r=>r.ParkingSpaces);
  foreach(double wanted in new[]{48d,36d,24d,15d})foreach(bool horizontal in new[]{preferred,!preferred})
   for(int y=1;y<ny-1;y++){o.Cancellation.ThrowIfCancellationRequested();for(int x=1;x<nx-1;x++){
    if(map[x,y]!=Paved)continue;
    double run=Math.Floor(wanted/o.BayWidth)*o.BayWidth;
    if(run<o.MinFrontParkingSpaces*o.BayWidth)continue;
    TryBackToBack(horizontal,horizontal?x0+x*cell:y0+y*cell,run,horizontal?y0+y*cell:x0+x*cell);
   }}
 }
 bool TryBackToBack(bool horizontal,double along,double run,double across){
  double bay=Snap(o.BayDepth),aisle=Snap(o.ParkingAisle),end=Snap(o.ParkingEndClearance);
  Parcel Part(double u,double v,double w,double h,int kind)=>new Parcel{Kind=kind,X=horizontal?u:v,Y=horizontal?v:u,W=horizontal?w:h,D=horizontal?h:w,ParkingAlongX=horizontal};
  var rows=new[]{Part(along,across,run,bay,2),Part(along,across+bay,run,bay,2)};
  if(rows.Any(r=>!KindRectangle(r,Paved)||!ParkingSpace(r)))return false;
  bool Space(Parcel r){
   if(!ParkingStreetSpace(r))return false;
   Bounds(r,out int a,out int b,out int c,out int d);
   for(int y=b;y<d;y++)for(int x=a;x<c;x++)if(mainJunctionBuffer?[x,y]??false)return false;
   return true;
  }
  var streets=new List<Parcel>();var routes=new List<PlanRoute>();
  foreach(double v in new[]{across-aisle,across+2*bay}){
   double low=Math.Floor((along-end)/cell)*cell,high=Snap(along+run+end);
   if(!Space(Part(low,v,high-low,aisle,1)))return false;
   double Join(bool first){
    double initial=first?low:high;
    for(double distance=0;distance<=o.MaxAccessLength;distance+=cell){
     double at=initial+(first?-distance:distance);
     var probe=Part(first?at-aisle:at,v,aisle,aisle,1);
     var corridor=Part(first?at-aisle:low,v,first?high-at+aisle:at+aisle-low,aisle,1);
     if(!Space(corridor))break;
     if(KindRectangle(probe,1))return first?at-aisle/2:at+aisle/2;
    }return double.NaN;
   }
   // Through aisles avoid adding isolated dead ends inside the parking court.
   double a=Join(true),b=Join(false);if(double.IsNaN(a)||double.IsNaN(b))return false;
   var street=Part(a-aisle/2,v,b-a+aisle,aisle,1);if(!Space(street))return false;
   streets.Add(street);routes.Add(new PlanRoute{Use="aisle",Width=aisle,Points=horizontal?new List<Point>{new Point(a,v+aisle/2),new Point(b,v+aisle/2)}:new List<Point>{new Point(v+aisle/2,a),new Point(v+aisle/2,b)}});
  }
  foreach(var r in streets)Paint(r,1);
  plan.ConstructionRoutes.AddRange(routes);
  foreach(var row in rows){row.ParkingSpaces=(int)Math.Round(run/o.BayWidth);Paint(row,Fixed);exact.Add(row);spaces+=row.ParkingSpaces;}
  return true;
 }
}
}
