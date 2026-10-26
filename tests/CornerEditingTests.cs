using System;
using System.Linq;
using ApartmentPlan;
static class CornerEditingTests {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
 public static void Run(){
  var box=new[]{new Point(0,0),new Point(32,0),new Point(32,60),new Point(0,60)};
  var entries=new[]{new Point(0,15),new Point(32,45)};
  var assets=new[]{new Asset{Id="A",Width=16,Depth=8,Height=20,Homes=10}};
  foreach(int mode in new[]{0,2}){
   var p=Layout.Generate(box,entries,assets,new LayoutOptions{MainRoadCornerMode=mode},null,null);
   Check(p.Routes.Any(r=>r.Use=="main"),"tight offset entrances connect in corner mode "+mode);
   Check(p.Routes.Where(r=>r.Use=="main").All(r=>r.CornerRadii.Count==0),"tight site uses angular fallback "+mode);
   Check(p.Parcels.All(r=>Layout.Contains(r,box.Select(p.Local).ToList())),"angular road stays within narrow site "+mode);
  }
  try{Layout.Generate(box,entries,assets,new LayoutOptions{MainRoadCornerMode=1},null,null);throw new Exception("strict radius unexpectedly fits");}catch(ArgumentException e){Check(e.Message=="CurveFit","strict curve mode reports tight corner");}
  var actual=new[]{new Point(-626.2667,-123.9687),new Point(-538.6446,-157.3504),new Point(-574.1082,-267.7516),new Point(-660.1896,-235.0818)};
  var native=Layout.Generate(actual,new Point(-586.1229,-139.7345),new[]{new Asset{Id="small",Width=16,Depth=16,Height=39,Homes=6}},new LayoutOptions(),null);
  foreach(var pc in native.ParkingConnections)if(!pc.Valid)Console.WriteLine("REPRO "+pc.Failure);
  Check(native.ParkingAccessFailures==0,"native fractional frame has connected parking");
  var square=new[]{new Point(0,0),new Point(100,0),new Point(100,100),new Point(0,100)};
  Check(BoundaryEditing.Hit(square,new Point(1,1),true,out bool edge)==0&&!edge,"vertex gets priority over edge");
  Check(BoundaryEditing.Hit(square,new Point(50,2),true,out edge)==0&&edge,"closed edge can be picked");
  Check(BoundaryEditing.Hit(square,new Point(50,2),false,out edge)==-1,"unfinished polygon has no closed edge drag");
  var moved=square.ToArray();BoundaryEditing.Move(square,moved,0,true,new Point(0,-10));
  var entrance=BoundaryEditing.Follow(square,moved,new Point(25,0));
  Check(entrance.X==25&&entrance.Y==-10&&moved[2].Y==100,"edge drag carries entrance and preserves other vertices");
  BoundaryEditing.Move(square,moved,1,false,new Point(20,0));entrance=BoundaryEditing.Follow(square,moved,new Point(25,0));
  Check(entrance.X==30&&entrance.Y==0,"vertex drag preserves entrance edge fraction");
  BoundaryEditing.Move(square,moved,1,false,new Point(-150,50));Check(!Layout.Valid(moved),"crossed drag is invalid for rollback");
 }
}
