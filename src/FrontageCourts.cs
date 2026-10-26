using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
internal sealed partial class ResidentialLayout {
 // Actual prefab +Z frontage gets a continuous vehicle street. Parking is on
 // the far side of that street, never between the lot and its access road.
 Block ConnectedFrontageCourt(double x,double y,Asset bar,Asset cap,string kind,int lanes,bool mirror,bool rotate,bool cross,bool wideEntrance=false){
  double length=bar.Width,depth=bar.Depth,cd=cap.Depth,cl=cap.Width;
  double aisle=Snap((wideEntrance?Math.Max(o.ParkingAisle,o.WideParkingAisle):o.ParkingAisle)+2*o.ParkingPavingMargin),bay=Snap(o.BayDepth),island=Snap(o.ParkingIsland);
  bool side=kind=="u"||kind=="ring",ring=kind=="ring",single=kind=="single";
  double stagger=o.AlignBlocks?Snap(o.BuildingStagger):0;
  double entranceWidth=wideEntrance?Snap(Math.Max(o.ParkingAisle,o.WideParkingAisle)+2*o.ParkingPavingMargin):aisle;
  double baseCourt=(lanes+1)*aisle+lanes*(2*bay+island)+(entranceWidth-aisle);
  double court=Math.Max(baseCourt,Math.Max(o.ParallelGap,side?cl:0));
  if(cross)court=Math.Max(court,2*aisle+2*o.ParkingEndClearance+o.MinFrontParkingSpaces*o.BayWidth);
  court=Snap(court); // parking pitch must not move lot edges between raster cells
  // Keep rear-to-rear rows together. Space needed by parallel lots belongs
  // to drivable aisles, bounded by the available native network widths.
  var streetWidths=Enumerable.Repeat(aisle,lanes+1).ToArray();
  streetWidths[0]=entranceWidth;
  if(!single&&!cross){
   double extra=court-baseCourt,maximum=Snap(Math.Max(o.ParkingAisle,o.WideParkingAisle)+2*o.ParkingPavingMargin);
   if(extra>streetWidths.Sum(w=>maximum-w)+.001)return null;
   var order=Enumerable.Range(0,lanes+1).OrderBy(i=>Math.Abs(i-lanes/2.0)).ToArray();
   foreach(int i in order){double upgrade=maximum-streetWidths[i];if(upgrade>0&&extra>=upgrade-.001){streetWidths[i]+=upgrade;extra-=upgrade;}}
   while(extra>.001){bool added=false;foreach(int i in order){if(extra<=.001)break;double next=streetWidths[i]+cell;
    double native=o.WideParkingAisle>o.ParkingAisle&&next>=o.WideParkingAisle?o.WideParkingAisle:o.ParkingAisle;
    if(next>maximum+.001||next-native>2*(o.ParkingPavingMargin+cell)+.001)continue;streetWidths[i]=next;extra-=cell;added=true;
   }if(!added)return null;}

  }
  double firstWidth=streetWidths[0],lastWidth=streetWidths[lanes];
  double start=side?cd+Math.Max(o.Gap,aisle):0,right=start+length+stagger;
  double rightRoad=ring?right+o.Gap-aisle:right-aisle,leftRoad=side?cd:0;
  double width=ring?right+o.Gap+cd:right,height=single?depth+firstWidth+bay:2*depth+court;
  if(length<aisle+2*o.ParkingEndClearance||cd<=0)return null;
  var b=new Block{MaxConnection=o.MaxAccessLength,LandscapeRemainder=true};
  Parcel Part(double u,double v,double w,double h,int k){var p=new Parcel{X=u,Y=v,W=w,D=h,Kind=k,ParkingAlongX=true};b.Pieces.Add(p);return p;}
  Parcel Street(double u,double v,double w,double h,bool horizontal){
   var p=Part(u,v,w,h,1);double span=horizontal?h:w;bool wide=o.WideParkingAisle>o.ParkingAisle&&span>=o.WideParkingAisle-.001;b.StreetAxes[p]=new PlanRoute{Use=wide?"aisleWide":"aisle",Width=wide?o.WideParkingAisle:o.ParkingAisle,Points=new List<Point>{new Point(horizontal?u:u+w/2,horizontal?v+h/2:v),new Point(horizontal?u+w:u+w/2,horizontal?v+h/2:v+h)}};return p;
  }
  void Building(double u,double v,Asset a,bool vertical,Parcel street){
   var p=Part(u,v,vertical?a.Depth:a.Width,vertical?a.Width:a.Depth,0);p.AssetId=a.Id;p.Homes=a.Homes;
   p.FloorArea=Math.Min(a.Width*a.Depth,a.Footprint>0?a.Footprint:a.Width*a.Depth*o.FootprintRatio)*Math.Max(1,Math.Floor(a.Height/o.FloorHeight));
   p.Frontage=new Parcel{Kind=1,X=street.X,Y=street.Y,W=street.W,D=street.D};
   p.Access=vertical?new Point(street.X>u?u+p.W:u,v+p.D/2):new Point(u+p.W/2,street.Y>v?v+p.D:v);
   b.Homes+=p.Homes;b.FloorArea+=p.FloorArea;
  }
  double roadEnd=ring?rightRoad+aisle:right;
  var bottom=Street(leftRoad,depth,roadEnd-leftRoad,firstWidth,true);
  Building(start,0,bar,false,bottom);
  Parcel top=null;if(!single){top=Street(leftRoad,depth+court-lastWidth,roadEnd-leftRoad,lastWidth,true);Building(start+stagger,depth+court,bar,false,top);}
  if(side){var r=Street(leftRoad,depth,aisle,court,false);Building(0,depth+(court-cl)/2,cap,true,r);}
  if(!single){var r=Street(rightRoad,depth,aisle,court,false);if(ring)Building(right+o.Gap,depth+(court-cl)/2,cap,true,r);}
  // A transverse street already supplies the end clearance of an adjoining
  // row. Do not subtract that clearance a second time inside the parking bay.
  double parkingStart=Math.Max(side?leftRoad+aisle:0,leftRoad+o.ParkingEndClearance);
  double parkingEnd=Math.Min(single?right:rightRoad,roadEnd-o.ParkingEndClearance);
  double span=Math.Floor((parkingEnd-parkingStart)/o.BayWidth)*o.BayWidth;
  if(span<o.MinFrontParkingSpaces*o.BayWidth)return null;
  void Bays(double u,double v,double w,double h,bool along){var p=Part(u,v,w,h,2);p.ParkingAlongX=along;p.ParkingSpaces=(int)Math.Round((along?w:h)/o.BayWidth);}
  if(single)Bays(parkingStart,depth+firstWidth,span,bay,true);
  else if(cross){
   double run=Math.Floor((court-firstWidth-lastWidth-2*o.ParkingEndClearance)/o.BayWidth)*o.BayWidth;int count=0;
   // Recover a stall row only when a genuine narrow 1x aisle makes it fit.
   // The wide entrance and every building's frontage remain unchanged.
   double narrow=Snap(o.ParkingAisle),inner=aisle,available=parkingEnd-parkingStart;
   int Rows(double w){int pairs=(int)Math.Floor((available+island)/(2*bay+w+island));return pairs*2+(available-pairs*(2*bay+w+island)>=bay?1:0);}
   if(narrow<inner&&Rows(narrow)>Rows(inner))inner=narrow;
   double u=parkingStart;
   for(;u+2*bay+inner<=parkingEnd+.001;u+=2*bay+inner+island){
    Street(u+bay,depth+firstWidth/2,inner,court-(firstWidth+lastWidth)/2,false);
    Bays(u,depth+firstWidth+o.ParkingEndClearance,bay,run,false);Bays(u+bay+inner,depth+firstWidth+o.ParkingEndClearance,bay,run,false);count++;
   }
   if(count==0)return null;
   // A leftover single row can face the existing closing street. No extra
   // dead-end or decorative lane is needed, and its front remains drivable.
   if(rightRoad-bay>=u-.001)Bays(rightRoad-bay,depth+firstWidth+o.ParkingEndClearance,bay,run,false);
  }else{
   double at=depth+firstWidth;
   for(int i=0;i<lanes;i++){
    Bays(parkingStart,at,span,bay,true);at+=bay+island;Bays(parkingStart,at,span,bay,true);at+=bay;
    if(i<lanes-1)Street(leftRoad,at,roadEnd-leftRoad,streetWidths[i+1],true);at+=streetWidths[i+1];
   }
  }
  b.Gate=new Point(right-aisle/2,depth+firstWidth/2);b.Bounds=new Parcel{W=width,D=height};
  // End clearance and fractional pitch remainder belong to the parking apron,
  // including courts open at one end. Keep intentional landscape islands.
  foreach(var row in b.Pieces.Where(p=>p.Kind==2))b.Paving.Add(row.ParkingAlongX?
   new Parcel{X=side?leftRoad+aisle:0,Y=row.Y,W=(single?right:rightRoad)-(side?leftRoad+aisle:0),D=row.D}:
   new Parcel{X=row.X,Y=depth+firstWidth,W=row.W,D=court-firstWidth-lastWidth});
  if(!single&&o.ParkingIsland<=0)b.Paving.Add(new Parcel{Kind=8,X=leftRoad,Y=depth,W=roadEnd-leftRoad,D=court});
  Point TransformPoint(Point p){double u=mirror?width-p.X:p.X;return new Point(x+(rotate?p.Y:u),y+(rotate?u:p.Y));}
  void Transform(Parcel p){double u=mirror?width-p.X-p.W:p.X,v=p.Y,w=p.W,h=p.D;p.X=x+(rotate?v:u);p.Y=y+(rotate?u:v);p.W=rotate?h:w;p.D=rotate?w:h;if(rotate)p.ParkingAlongX=!p.ParkingAlongX;}
  b.Gate=TransformPoint(b.Gate);foreach(var r in b.StreetAxes.Values)r.Points=r.Points.Select(TransformPoint).ToList();
  foreach(var p in b.Pieces.Where(p=>p.Kind==0)){p.Access=TransformPoint(p.Access.Value);Transform(p.Frontage);}foreach(var p in b.Pieces)Transform(p);foreach(var p in b.Paving)Transform(p);Transform(b.Bounds);return b;
 }
}
}
