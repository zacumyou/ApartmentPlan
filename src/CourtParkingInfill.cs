using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
internal sealed partial class ResidentialLayout {
 // Optional bounded pass: only unreserved green space, with native-connected aisles.
 void FillOpenSpaceParking(){
  if(!o.ParkingInOpenSpace)return;
  double area=exact.Where(p=>p.Kind==2).Sum(p=>p.Area);int attempts=0,added=0;
  bool Needed()=>spaces+0.001<plan.Homes*o.ParkingPerHome||area+0.001<plan.Area*o.Parking;
  foreach(double wanted in new[]{40d,25d,15d})for(int rotation=0;rotation<2;rotation++)
   for(int y=1;y<ny-1;y+=3){o.Cancellation.ThrowIfCancellationRequested();for(int x=1;x<nx-1;x+=3){
    if(!Needed()||added>=48||attempts>=12000)return;
    if(map[x,y]!=Green||reserved[x,y]||edgeGreen[x,y])continue;
    attempts++;double length=Math.Floor(wanted/o.BayWidth)*o.BayWidth;
    if(length<o.BayWidth*4)continue;
    var block=SupplementalParkingBlock(x0+x*cell,y0+y*cell,length,rotation==1);
    if(Place(block)){area+=block.Pieces.Where(p=>p.Kind==2).Sum(p=>p.Area);added++;}
   }}
 }
 Block SupplementalParkingBlock(double x,double y,double run,bool rotated){
  double lane=Snap(o.SupplementalTwoWayWidth),wide=Snap(o.SupplementalAccessWidth),bay=Snap(o.BayDepth),end=Snap(Math.Max(wide,o.ParkingEndClearance)),rear=Snap(Math.Max(2,(wide-lane)/2));
  double width=run+2*end,center=rear+bay+lane/2,height=center+wide/2;
  Point P(double u,double v)=>new Point(x+(rotated?v:u),y+(rotated?u:v));
  Parcel R(double u,double v,double w,double h,int kind)=>new Parcel{Kind=kind,X=x+(rotated?v:u),Y=y+(rotated?u:v),W=rotated?h:w,D=rotated?w:h,ParkingAlongX=!rotated};
  var block=new Block{SupplementalParking=true,Bounds=R(0,0,width,height,8),MaxConnection=32,Gate=P(wide/2,center)};
  var street=R(0,center-lane/2,width,lane,1);block.Pieces.Add(street);
  block.StreetAxes[street]=new PlanRoute{Use="parkingTwoWay",Width=o.SupplementalTwoWayWidth,Points=new List<Point>{P(wide/2,center),P(width-lane/2,center)}};
  var apron=R(0,center-wide/2,wide,wide,1);block.Pieces.Add(apron);
  block.StreetAxes[apron]=new PlanRoute{Use="aisleWide",Width=o.SupplementalAccessWidth,Points=new List<Point>{P(wide/2,center)}};
  var row=R(end,rear,run,bay,2);row.ParkingSpaces=(int)Math.Round(run/o.BayWidth);row.ParkingAisleWidth=o.SupplementalTwoWayWidth;block.Pieces.Add(row);return block;
 }
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
