using System;
using System.Linq;
using System.Collections.Generic;
using ApartmentPlan;
static class ParkingTrafficTests {
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text);}
 static Plan Fixture()=>new Plan{Boundary=new List<Point>{new Point(0,0),new Point(80,0),new Point(80,70),new Point(0,70)},Right=new Point(1,0),Forward=new Point(0,1),Parcels=new List<Parcel>{new Parcel{Kind=1,X=0,Y=20,W=60,D=8},new Parcel{Kind=2,X=10,Y=15,W=20,D=5,ParkingSpaces=8,ParkingAlongX=true}}};
 public static void Run(){
  var o=new LayoutOptions();var p=Fixture();ParkingTraffic.Validate(p,o);
  Check(p.ParkingAccessFailures==0&&p.ParkingConnections.Single().Ports.Count==8&&p.ParkingConnections[0].EntrySide==1,"all stalls have aisle-side ports and connected vehicle approach");
  var b=new Parcel{Kind=0,X=10,Y=35,W=20,D=8,Frontage=new Parcel{X=10,Y=45,W=20,D=8}};
  Check(ParkingFrontRules.Served(b,p.Parcels,20,4),"parking behind the designated building front is accepted");
  p.Parcels[1].ParkingAlongX=false;ParkingTraffic.Validate(p,o);Check(p.ParkingAccessFailures==1,"wrong parking-decal axis is rejected");
  p=Fixture();p.Parcels[0].D=5;ParkingTraffic.Validate(p,o);Check(p.ParkingAccessFailures==1,"too narrow a maneuvering aisle is rejected");
  p=Fixture();p.Parcels[0].X=10;p.Parcels[0].W=20;ParkingTraffic.Validate(p,o);Check(p.ParkingAccessFailures==1,"missing parking-row end clearance is rejected");
  p=Fixture();p.Parcels.Insert(0,new Parcel{Kind=1,X=0,Y=40,W=20,D=8});ParkingTraffic.Validate(p,o);Check(p.ParkingAccessFailures==1,"isolated parking aisle cannot reach the site entrance");
  p=Fixture();p.Parcels.Insert(0,new Parcel{Kind=1,X=0,Y=40,W=20,D=8});p.Parcels.Add(new Parcel{Kind=1,X=10,Y=28,W=2,D=12});ParkingTraffic.Validate(p,o);Check(p.ParkingAccessFailures==1,"two-metre pinch cannot pass the configured three-metre corridor");
  p=Fixture();p.Parcels.RemoveAt(0);ParkingTraffic.Validate(p,o);Check(p.ParkingAccessFailures==1,"parking without any vehicle network is rejected");
  var site=new[]{new Point(0,0),new Point(160,0),new Point(160,275),new Point(0,325)};
  var entries=new[]{new Point(60,0),new Point(120,287.5)};
  var assets=new[]{new Asset{Id="L",Width=40,Depth=12,Height=45,Homes=80},new Asset{Id="S",Width=24,Depth=12,Height=30,Homes=40}};
  foreach(bool required in new[]{true,false}){
   var options=new LayoutOptions{ParkingOrientation=2,RequireBuildingRoad=required};var plan=Layout.Generate(site,entries,assets,options,null,null);
   Check(plan.ParkingConnections.Count>0&&plan.ParkingAccessFailures==0,"all perpendicular-preference rows have valid access, frontage required="+required);
   Check(plan.Courts.Any(c=>c.Parking.Any(row=>row.ParkingAlongX!=(c.Buildings[0].W>=c.Buildings[0].D))),"parking orientation is independent from primary building orientation");
   Check(!required||plan.Parcels.Where(v=>v.Kind==0).All(v=>v.Frontage!=null),"required building frontage retained after denser court packing");
   Check(plan.Parcels.Any(v=>v.Kind==1&&v.InvisibleRoad)&&plan.Parcels.Any(v=>v.Kind==1&&!v.InvisibleRoad),"main road and invisible vehicle paths have separate display roles");
  }
  // Denser courts can fill this site without any residual bars. Exercise the
  // roadless opt-out on a fixture that explicitly prevents court road links.
  var roadless=Layout.Generate(site,entries,assets,new LayoutOptions{RequireBuildingRoad=false,RequireFrontParking=false,MaxAccessLength=0,AllowRingCourts=false,AllowOpenCourts=false,AllowParallelPairs=false},null,null);
  Check(roadless.Parcels.Any(v=>v.Kind==0&&v.Frontage==null),"roadless option still permits residual buildings when courts cannot connect");
 }
}
