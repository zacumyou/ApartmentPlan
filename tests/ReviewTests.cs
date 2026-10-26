using System;
using System.Linq;
using ApartmentPlan;
static class ReviewTests {
 static void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);}
 public static void Run(){
  var site=new[]{new Point(0,0),new Point(160,0),new Point(160,275),new Point(0,325)};
  var entries=new[]{new Point(60,0),new Point(120,287.5)};
  var assets=new[]{new Asset{Id="L",Width=40,Depth=12,Height=45,Homes=80},new Asset{Id="S",Width=24,Depth=12,Height=30,Homes=40}};
  var cases=new[]{new LayoutOptions(),new LayoutOptions{RespectTargetFar=true,TargetFar=50},new LayoutOptions{MaxBuildings=10},new LayoutOptions{ParkingOrientation=2,ParkingAisle=5,ParkingPavingMargin=0,VehiclePassageWidth=4},new LayoutOptions{BayWidth=2.3,BayDepth=4.5,ParkingEndClearance=8},new LayoutOptions{RequireBuildingRoad=false,RequireFrontParking=false,RespectTargetFar=true,TargetFar=50},new LayoutOptions{Parking=0,RequireFrontParking=false},new LayoutOptions{BoundaryGreenMargin=12,ParallelGap=32,MaxAccessLength=16},new LayoutOptions{BoundaryGreenMargin=20},new LayoutOptions{Road=4,ParkingOrientation=2}};
  for(int i=0;i<cases.Length;i++){
   var o=cases[i];var p=Layout.Generate(site,entries,assets,o,null,null);var bars=p.Parcels.Where(r=>r.Kind==0).ToList();var local=site.Select(p.Local).ToList();
   Check(p.Parcels.All(r=>Layout.Contains(r,local)),"review case "+i+" stays inside boundary");
   for(int a=0;a<p.Parcels.Count;a++)for(int b=0;b<a;b++)CheckSilent(!Layout.Overlap(p.Parcels[a],p.Parcels[b]),"overlap case "+i);
   for(int a=0;a<bars.Count;a++)for(int b=0;b<a;b++)CheckSilent(FrontageRules.Spaced(bars[a],bars[b],o.ParallelGap,o.Gap),"spacing case "+i);
   Check(p.ParkingAccessFailures==0,"review case "+i+" connected parking");
   Check(bars.Count<=o.MaxBuildings,"review case "+i+" building cap");
   Check(!o.RespectTargetFar||p.Far<=o.TargetFar+.001,"review case "+i+" strict FAR cap actual="+p.Far);
   Check(!o.RequireFrontParking||bars.All(b=>ParkingFrontRules.Served(b,p.Parcels,o.FrontParkingDistance,o.MinFrontParkingSpaces)),"review case "+i+" nearby parking rule");
   Check(!o.RequireBuildingRoad||bars.All(b=>b.Frontage!=null),"review case "+i+" frontage rule");
  }
  foreach(double offset in new[]{1.0,10.0}){
   var rectangle=new[]{new Point(0,0),new Point(160,0),new Point(160,300),new Point(0,300)};
   var p=Layout.Generate(rectangle,new[]{new Point(70,0),new Point(70+offset,300)},assets,new LayoutOptions(),null,null);
   Check(p.Routes.Any(r=>r.Use=="main")&&p.ParkingAccessFailures==0,"shallow offset entrance "+offset+" m is connected");
   Check(p.Routes.Where(r=>r.Use=="main").SelectMany(r=>r.CornerRadii).All(r=>r>=16),"shallow offset respects curve radius");
  }
  foreach(bool vertical in new[]{false,true})foreach(bool positive in new[]{false,true}){
   var old=new Parcel{X=10,Y=10,W=vertical?12:40,D=vertical?40:12};
   old.Frontage=vertical?new Parcel{X=positive?25:0,Y=10,W=8,D=40}:new Parcel{X=10,Y=positive?25:0,W=40,D=8};
   var next=new Parcel{X=old.X+2,Y=old.Y+2,W=old.W-4,D=old.D-4};ReplacementRules.KeepFront(old,next);
   Check(vertical?(positive?next.X+next.W==old.X+old.W:next.X==old.X):(positive?next.Y+next.D==old.Y+old.D:next.Y==old.Y),"replacement preserves frontage edge");
   var vacant=ReplacementRules.Vacated(old,next).ToList();
   Check(Math.Abs(vacant.Sum(r=>r.Area)+next.Area-old.Area)<.001&&vacant.All(r=>!Layout.Overlap(r,next)),"replacement vacated strips conserve area without overlap");
  }
 }
 static void CheckSilent(bool v,string message){if(!v)throw new Exception(message);}
}
