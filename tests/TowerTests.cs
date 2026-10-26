using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using ApartmentPlan;
public static class TowerTests {
 static void WalkPolicy(){
 var a=TowerGeometry.Line(new Point(0,0),new Point(20,0));
 var b=TowerGeometry.Line(new Point(20,5),new Point(0,5));
 var c=TowerGeometry.Line(new Point(0,0),new Point(0,20));
 var orphan=TowerGeometry.Fillet(new[]{new Point(50,50),new Point(70,50),new Point(70,70),new Point(50,70)},2,true);
 var p=new Plan();p.Routes.Add(TowerGeometry.Route(new[]{a},"pedestrian",2));p.Routes.Add(TowerGeometry.Route(new[]{b},"pedestrian",2));p.Routes.Add(TowerGeometry.Route(new[]{c},"pedestrian",2));p.Routes.Add(TowerGeometry.Route(orphan,"pedestrian",2));
 TowerWalkPolicy.Apply(p,new HashSet<PlanBezier>{a},new TowerRules());
 if(p.Routes.Count!=2||p.Routes.Any(q=>q.Curves.Contains(b))||!p.Routes.Any(q=>q.Curves.Contains(c)))throw new Exception("walk chronology/reversed parallel/orphan/indirect connection");
 if(!TowerWalkPolicy.ParallelConflict(a,2,TowerGeometry.Line(new Point(0,6),new Point(20,6)),2,4)||TowerWalkPolicy.ParallelConflict(a,2,TowerGeometry.Line(new Point(0,6.1),new Point(20,6.1)),2,4))throw new Exception("walk edge gap");
 var bend=TowerGeometry.Fillet(new[]{new Point(0,0),new Point(10,0),new Point(10,10)},4,false).First(q=>!TowerConstruction.Straight(q));
 var crossing=TowerGeometry.Line(new Point(6,2),new Point(12,2));
 if(!TowerWalkPolicy.CurveConflict(bend,2,crossing,2))throw new Exception("curved walk crossing missed");
 var tangent=TowerGeometry.Line(new Point(-10,0),bend.A);
 if(TowerWalkPolicy.CurveConflict(bend,2,tangent,2))throw new Exception("continuous tangent walk rejected");
 Console.WriteLine("walk chronology, edge gap, disconnected component and indirect access passed");
 }
 static void Clearances(Plan p){
 foreach(var lot in p.Parcels.Where(l=>l.Kind==0)){
  var v=lot.PlacementForward.Value;var u=new Point(-v.Y,v.X);var mid=new Point(lot.X+lot.W/2,lot.Y+lot.D/2)+v*(lot.D/2+4);var front=TowerGeometry.Line(mid-u*(lot.W/2-4),mid+u*(lot.W/2-4));
  var actual=p.Routes.Where(q=>q.Use=="aisle"&&q.Curves.Count==1).Select(q=>q.Curves[0]).Where(c=>TowerConstruction.Straight(c)&&Math.Abs(Layout.Cross(VillaGeometry.Unit(c.D-c.A),u))<.001&&(VillaGeometry.Nearest(mid,c.A,c.D)-mid).Length<.01).OrderBy(c=>(c.A-front.A).Length+(c.D-front.D).Length).FirstOrDefault();
  if(actual!=null)front=actual;
  foreach(var curve in p.Routes.Where(q=>q.Use=="aisle").SelectMany(q=>q.Curves).Where(c=>!TowerConstruction.Straight(c)))if(!TowerRouting.FrontCurveClear(curve,front,10))throw new Exception("frontage squeezed against curve");
 }

 var green=new GreenSpace(p);var trees=green.Trees(new[]{new TreeSpecies{Id="simulation-tree",Radius=2}},12,6,73471);
 var blockers=p.Parcels.Where(q=>q.Kind==0||q.Kind==5||q.Kind==6||q.Kind==1||q.Kind==9).ToList();
 foreach(var parcel in p.Parcels.Where(q=>q.Kind==4))if(blockers.Any(b=>VillaGeometry.Overlap(parcel.Corners,b.PlacementOutline??b.Corners)))throw new Exception("park overlaps building or network");
 if(green.Area<=0||trees.Count==0)throw new Exception("remaining park/tree mask empty");
 foreach(var tree in trees)if(!green.Fits(p.Local(tree.Position),2))throw new Exception("tree outside park");
 var localPoly=p.Boundary.Select(p.Local).ToList();int outward=0;
 var segments=p.Routes.Where(q=>q.Use=="pedestrian").SelectMany(q=>q.Curves).ToList();
 foreach(var c in segments.Where(TowerConstruction.Straight))if(Math.Abs(TowerGeometry.Distance(c.D,localPoly)-1)<.01){
  if(!segments.Any(other=>!ReferenceEquals(other,c)&&TowerConstruction.Straight(other)&&new[]{.25,.75}.Any(t=>(other.A+(other.D-other.A)*t-c.A).Length<.01)))throw new Exception("outward spur not at quarter point");outward++;
 }
 if(outward==0)throw new Exception("outward spurs missing");
 foreach(var outline in p.TowerEntryOutlines)for(int i=0;i<outline.Count;i++)if((outline[i]-outline[(i+1)%outline.Count]).Length<=.01)throw new Exception("zero-length pavement boundary");
 File.WriteAllText("docs/tower-trees-"+p.Parcels.Count(q=>q.Kind==0)+"-022.json",Newtonsoft.Json.JsonConvert.SerializeObject(trees));
 Console.WriteLine("parkArea="+green.Area+" trees="+trees.Count+" outward="+outward);
 var walks=p.Routes.Where(q=>q.Use=="pedestrian").ToArray();
 for(int i=0;i<walks.Length;i++)for(int j=0;j<i;j++)foreach(var a in walks[i].Curves)foreach(var b in walks[j].Curves)if(TowerWalkPolicy.ParallelConflict(a,walks[i].Width,b,walks[j].Width,4))throw new Exception("remaining parallel walks");
 for(int i=0;i<walks.Length;i++)for(int j=0;j<i;j++)foreach(var a in walks[i].Curves)foreach(var b in walks[j].Curves)if(TowerWalkPolicy.CurveConflict(a,walks[i].Width,b,walks[j].Width))throw new Exception("remaining curved walk conflict");
 var rings=p.Routes.Where(q=>q.Use=="turnaround").ToArray();int branches=0;
 foreach(var ring in rings){
  var parking=p.Parcels.Where(q=>q.Kind==6).OrderBy(q=>(new Point(q.X+q.W/2,q.Y+q.D/2)-ring.Curves[0].A).Length).First();
  var axis=parking.PlacementForward.Value;
  if(ring.Curves.Where(TowerConstruction.Straight).Count(c=>Math.Abs(Math.Abs(SiteFrame.Dot(VillaGeometry.Unit(c.D-c.A),axis))-Math.Sqrt(.5))<.001)!=2)throw new Exception("turnaround must have two diagonal entrance shoulders");
  for(int i=0;i<ring.Curves.Count;i++)if((ring.Curves[i].D-ring.Curves[(i+1)%ring.Curves.Count].A).Length>.001)throw new Exception("turnaround continuity");
 }

 foreach(var road in p.Routes.Where(q=>q.Use=="aisle")){
  // Only the explicit one-segment branch beginning on a court is exempt.
  bool branch=road.Curves.Count==1&&rings.Any(q=>q.Curves.Any(c=>(c.At(c.Parameter(road.Curves[0].A))-road.Curves[0].A).Length<.001));
  if(branch){branches++;continue;}
  foreach(var ring in rings)foreach(var a in road.Curves)foreach(var b in ring.Curves){var aa=VillaGeometry.Sample(a,.5);var bb=VillaGeometry.Sample(b,.5);for(int i=1;i<aa.Count;i++)for(int j=1;j<bb.Count;j++)if(TowerRouting.SegmentDistance(aa[i-1],aa[i],bb[j-1],bb[j])-(road.Width+ring.Width)/2<3.999)throw new Exception("nonbranch turnaround clearance");}
 }
 if(branches!=rings.Length)throw new Exception("one explicit branch per court");
 Console.WriteLine(p.Diagnostics);
 }
 static void IndependentEntrances(){
  var p=new Plan{Tower=true,TowerAlgorithmVersion=1,Origin=new Point(),Right=new Point(1,0),Forward=new Point(0,1),Boundary=new List<Point>{new Point(-40,0),new Point(140,0),new Point(140,60),new Point(-40,60)}};
  var asset=new Asset{Id="test",Width=16,Depth=16};
  foreach(double x in new[]{0d,100d}){
   p.Parcels.Add(new Parcel{Kind=0,AssetId="test",X=x-8,Y=0,W=16,D=16,PlacementForward=new Point(0,1),PlacementOutline=VillaGeometry.Box(new Point(x,8),new Point(0,1),16,16)});
   p.Routes.Add(TowerGeometry.Route(new[]{TowerGeometry.Line(new Point(x,0),new Point(x,20))},"main",8));
   p.Routes.Add(TowerGeometry.Route(new[]{TowerGeometry.Line(new Point(x-12,20),new Point(x+12,20))},"aisle",8));
  }
  if(FrontageNetwork.Missing(p,new[]{asset},8,1)!=0)throw new Exception("independent entrance networks rejected");
  p.Routes.RemoveAt(2);
  if(FrontageNetwork.Missing(p,new[]{asset},8,1)!=1)throw new Exception("single entrance accepted isolated building");
  Console.WriteLine("independent entrance roots accepted; orphan building still rejected");
 }
 public static void Run(){
 IndependentEntrances();
 WalkPolicy();
 if(!EntranceSpacing.Near(new Point(100,0),new[]{new Point(0,0)})||EntranceSpacing.Near(new Point(100.01,0),new[]{new Point(0,0)})||!EntranceSpacing.HasClosePair(new[]{new Point(0,0),new Point(60,80)})||EntranceSpacing.HasClosePair(new[]{new Point(0,0),new Point(101,0)}))throw new Exception("100m entrance advisory radius");
 Console.WriteLine("100m inclusive advisory boundary passed (nonblocking helper)");
 var line=TowerGeometry.Line(new Point(0,0),new Point(20,0));
 foreach(double angle in new[]{0d,44,45,90}){double rad=angle*Math.PI/180;var arm=TowerGeometry.Line(new Point(0,0),new Point(Math.Cos(rad)*10,Math.Sin(rad)*10));if(TowerWalkPolicy.ApproachAngle(arm,line,45)!=(angle>=45))throw new Exception("walk chord angle "+angle);}
 var bend=TowerGeometry.Fillet(new[]{new Point(10,0),new Point(24,0),new Point(24,20)},4,false).First(c=>!TowerConstruction.Straight(c));
 if(!TowerRouting.FrontCurveClear(bend,line,10)||TowerRouting.FrontCurveClear(bend,TowerGeometry.Line(new Point(10,0),new Point(30,0)),10))throw new Exception("frontage tangent continuation / intruding bend");
 Console.WriteLine("walk 0/44/45/90 degree chord and frontage bend guards passed");
 var reported=new[]{new Point(1907.28125,-1051.85986328125),new Point(2046.2860107421875,-1180.6102294921875),new Point(2157.411376953125,-1060.263427734375),new Point(1959.2457275390625,-877.28204345703125)};
 var reportPlan=Layout.Generate(reported,new[]{new Point(1998.7098795313386,-1136.5437940435679)},new[]{new Asset{Id="CS Doan Xi Apt 30",Width=40,Depth=32,Height=111,Homes=144}},new LayoutOptions{Tower=true,TowerParking=new Asset{Id="TheSharp Parking",Width=16,Depth=40},ParkingAisle=8,Road=24,Walkway=2,Gap=10},new List<Parcel>(),new List<ManualZone>());
 if(reportPlan.Parcels.Count(q=>q.Kind==0)!=7)throw new Exception("entrance taper consumed existing tower capacity");
 Clearances(reportPlan);
 if(!reportPlan.Diagnostics.Contains("frontageFillets=1"))throw new Exception("reported terminal corner not rounded");
 foreach(var route in reportPlan.Routes.Where(q=>q.Use=="aisle")){
  for(int i=1;i+1<route.Curves.Count;i++)if(TowerConstruction.Straight(route.Curves[i])&&!TowerConstruction.Straight(route.Curves[i-1])&&!TowerConstruction.Straight(route.Curves[i+1])&&(route.Curves[i].D-route.Curves[i].A).Length<9.999)throw new Exception("rounded frontage consumes 10m bend gap");
  foreach(var curve in route.Curves.Where(c=>!TowerConstruction.Straight(c))){var a=VillaGeometry.Unit(curve.B-curve.A);var b=VillaGeometry.Unit(curve.D-curve.C);double angle=Math.Acos(Math.Max(-1,Math.Min(1,SiteFrame.Dot(a,b))));double actual=(curve.B-curve.A).Length/(4d/3*Math.Tan(angle/4));if(Math.Abs(actual-4.1)>.001)throw new Exception("front join radius changed");}
 }
 Console.WriteLine("reported endpoint fillet, 4.1m radius and 10m straight gap passed");
 File.WriteAllText("docs/tower-reported-current.json",Newtonsoft.Json.JsonConvert.SerializeObject(reportPlan));Console.WriteLine("reported site buildings="+reportPlan.Parcels.Count(r=>r.Kind==0));
var boundary=new[]{new Point(0,0),new Point(360,0),new Point(360,260),new Point(320,310),new Point(40,300),new Point(-20,200)};var assets=new[]{new Asset{Id="test-tower",Width=44,Depth=44,Height=80,Homes=100}};var parking=new Asset{Id="test-parking",Width=16,Depth=24,Height=6};
 foreach(int count in new[]{1,2})foreach(int turn in new[]{2}){var o=new LayoutOptions{Tower=true,TowerParking=parking,ParkingAisle=8,Road=24,Walkway=2,Gap=10,TowerTurnStyle=turn};var gates=count==1?new[]{new Point(170,0)}:new[]{new Point(94,0),new Point(246,0)};var p=Layout.Generate(boundary,gates,assets,o,new List<Parcel>(),new List<ManualZone>());if(!p.Tower||p.Parcels.Count(r=>r.Kind==6)!=count||!p.Parcels.Any(r=>r.Kind==0)||p.Parcels.Any(r=>r.Kind==5))throw new Exception("tower contents");
 Clearances(p);
 var bearing=new Point(Math.Sqrt(.5),-Math.Sqrt(.5));var rotations=p.Parcels.Where(q=>q.Kind==0&&SiteFrame.Dot(q.PlacementForward.Value,bearing)<.99).ToArray();if(rotations.Any(q=>Math.Abs(SiteFrame.Dot(q.PlacementForward.Value,bearing))>.001))throw new Exception("boundary quarter-turn fixture");
 Console.WriteLine("boundary quarter-turn buildings="+rotations.Length);
 var lots=p.Parcels.Where(r=>r.Kind==0||r.Kind==6).ToArray();for(int i=0;i<lots.Length;i++)for(int j=0;j<i;j++)if(VillaGeometry.Overlap(lots[i].PlacementOutline,lots[j].PlacementOutline))throw new Exception("lot overlap");
 var details=new List<string>();File.WriteAllText("docs/tower-native-"+count+"-"+turn+".json",Newtonsoft.Json.JsonConvert.SerializeObject(p));if(FrontageNetwork.Missing(p,assets,8,1,details)>0)throw new Exception("frontage check failed "+string.Join(";",details));var courses=ConstructionCurves.Create(p);if(courses.Count==0||!courses.Any(c=>c.Curved))throw new Exception("native Beziers missing");
 var straight=p.Routes.Where(q=>q.Use=="aisle").SelectMany(q=>q.Curves).Where(TowerConstruction.Straight).ToList();
 for(int i=0;i<straight.Count;i++)for(int j=0;j<i;j++){var a=straight[i];var b=straight[j];var dir=VillaGeometry.Unit(a.D-a.A);if(Math.Abs(SiteFrame.Dot(dir,VillaGeometry.Unit(b.D-b.A)))<Math.Cos(Math.PI/18))continue;double distance=Math.Abs(Layout.Cross(dir,b.A-a.A));double begin=Math.Max(0,Math.Min(SiteFrame.Dot(b.A-a.A,dir),SiteFrame.Dot(b.D-a.A,dir))),end=Math.Min((a.D-a.A).Length,Math.Max(SiteFrame.Dot(b.A-a.A,dir),SiteFrame.Dot(b.D-a.A,dir)));if(distance>.1&&distance<9.99&&end-begin>.1)throw new Exception("parallel road clearance "+distance);}
 if(p.TowerEntryOutlines.Count!=count*2)throw new Exception("two pavement polygons per entrance");
 foreach(var route in p.Routes)foreach(var curve in route.Curves.Where(c=>!TowerConstruction.Straight(c))){var a=p.World(curve.A);var d=p.World(curve.D);if(courses.Count(c=>c.Curved&&(c.Curve.A-a).Length<.001&&(c.Curve.D-d).Length<.001)!=1)throw new Exception("one native segment per bend");}
 if(count==2&&turn==2){
 var encoded=PlanEditCodec.Encode(new PlanEditData{Boundary=boundary.ToList(),Entrances=gates.ToList(),Draft=p,Selected=new[]{"test-tower"},Settings=new Dictionary<string,object>{{"PlanMode",2},{"TowerParkingAsset",parking.Id}}});if(encoded.Length>2000000)throw new Exception("edit byte budget");var restored=PlanEditCodec.Decode(encoded);if(!restored.Draft.Tower||restored.Draft.TowerEntryOutlines.Count!=p.TowerEntryOutlines.Count||restored.Draft.Parcels.Count(r=>r.Kind==6)!=2)throw new Exception("tower save roundtrip");
 var off=new LayoutOptions{Tower=true,TowerParking=parking,ParkingAisle=8,Road=24,Walkway=2,Gap=10,TowerEntrancePaving=false,TowerFacility=true};var q=Layout.Generate(boundary,gates,assets,off,new List<Parcel>(),new List<ManualZone>());if(q.TowerEntryOutlines.Count!=0||!q.Parcels.Any(r=>r.Kind==5)||q.Parcels.Count(r=>r.Kind==0)!=p.Parcels.Count(r=>r.Kind==0))throw new Exception("tower switches");Console.WriteLine("tower save/surface-off/optional-facility passed");
 }
 File.WriteAllText("docs/tower-native-"+count+"-"+turn+".json",Newtonsoft.Json.JsonConvert.SerializeObject(p));Console.WriteLine("tower gates="+count+" turn="+turn+" buildings="+p.Parcels.Count(r=>r.Kind==0)+" courses="+courses.Count);
 }
 }
}
