using System;
using System.Collections.Generic;
using System.Linq;
using ApartmentPlan;
class Program {
 static void Check(bool yes,string label){if(!yes)throw new Exception(label);Console.WriteLine("PASS "+label);}
 static void Verify(Plan p){var local=p.Boundary.Select(p.Local).ToList();Check(p.Parcels.All(r=>Layout.Contains(r,local)),"whole parcels contained");for(int i=0;i<p.Parcels.Count;i++)for(int j=i+1;j<p.Parcels.Count;j++)if(Layout.Overlap(p.Parcels[i],p.Parcels[j]))throw new Exception("overlap "+i+" "+j);Check(true,"nonoverlapping zones");Check(Math.Abs(p.Parcels.Where(r=>r.Kind==0).Sum(r=>r.FloorArea)/p.Area*100-p.Far)<.001,"FAR denominator and floor area");Check(p.Parcels.Sum(r=>r.Area)<=p.Area+.01,"area conservation");}
 static bool Touches(Parcel a,Parcel b)=>
  ((Math.Abs(a.X+a.W-b.X)<.01||Math.Abs(b.X+b.W-a.X)<.01)&&Math.Min(a.Y+a.D,b.Y+b.D)-Math.Max(a.Y,b.Y)>.01)||
  ((Math.Abs(a.Y+a.D-b.Y)<.01||Math.Abs(b.Y+b.D-a.Y)<.01)&&Math.Min(a.X+a.W,b.X+b.W)-Math.Max(a.X,b.X)>.01);
 public sealed class ReplayInput {public List<Point> Boundary {get;set;} public List<Point> Entrances {get;set;} public List<Asset> Assets {get;set;} public LayoutOptions Options {get;set;} public List<Parcel> Ramps {get;set;} public List<ManualZone> Zones {get;set;} public List<ManualRoad> Roads {get;set;}}
 static void Main(string[] args){if(args.Length>0&&args[0]=="--batch-audio"){
 var batch=new BatchSnapSound();var kept=new List<string>();
 foreach(var item in new[]{"Add","Snap","Remove","Snap","WrongPrefab","Temp","Snap"})if(batch.Keep(item=="Snap"))kept.Add(item);
 if(string.Join(",",kept)!="Add,Snap,Remove,WrongPrefab,Temp")throw new Exception("Non-snap audio/order changed");
 if(batch.Keep(true))throw new Exception("Later frame repeated snap");
 batch.Reset();if(!batch.Keep(true))throw new Exception("Next construction lost sound");
 Console.WriteLine("Batch audio: mixed queue order preserved, one snap across frames, new batch reset passed");return;
}
if(args.Length>0&&args[0]=="--road-migration"){
 var allowed=new[]{"AP one","AP two"};
 Check(CompoundRoadPolicy.MigrationTarget(false,"AP one",allowed,allowed)==null,"unowned protected");
 Check(CompoundRoadPolicy.MigrationTarget(true,"AP one",allowed,new string[0])=="AP one","known AP exact name");
 Check(CompoundRoadPolicy.MigrationTarget(true,"fallback",allowed,new[]{"AP two"})=="AP two","unique saved AP course recovered");
 Check(CompoundRoadPolicy.MigrationTarget(true,"fallback",allowed,allowed)==null,"ambiguous courses protected");
 Check(CompoundRoadPolicy.MigrationTarget(true,"Alley",allowed,new[]{"Alley"})==null,"ordinary road protected");
 Check(CompoundRoadPolicy.MigrationTarget(true,"",allowed,new string[0])==null,"missing record protected");return;}
if(args.Length>0&&args[0]=="--road-fill"){RoadFillTests.Run();return;}if(args.Length>0&&args[0]=="--tower-replay"){var input=Newtonsoft.Json.JsonConvert.DeserializeObject<ReplayInput>(System.IO.File.ReadAllText(args[1]));var timer=System.Diagnostics.Stopwatch.StartNew();Plan plan;try{plan=Layout.Generate(input.Boundary,input.Entrances,input.Assets,input.Options,input.Ramps,input.Zones,input.Roads);}catch(Exception e){Console.WriteLine("elapsedMs="+timer.ElapsedMilliseconds+" error="+e.Message);return;}Console.WriteLine("elapsedMs="+timer.ElapsedMilliseconds+" "+plan.Diagnostics);System.IO.File.WriteAllText("docs/tower-slow-output-021.json",Newtonsoft.Json.JsonConvert.SerializeObject(plan));return;}
if(args.Length>0&&args[0]=="--performance"){
 var sites=new[]{new[]{new Point(0,0),new Point(180,0),new Point(180,340),new Point(0,300)}};
 for(int run=0;run<3;run++){
 var options=new LayoutOptions{ContinuousFrontage=true,RequireBuildingRoad=true,ParkingAisle=7,AccessRoad=7,ParkingPavingMargin=0,BayWidth=3.7,BayDepth=6.2,ParkingOrientation=1,MainRoadPedestrian=true,AisleRoadPedestrian=true};
 long allocated=GC.GetAllocatedBytesForCurrentThread();var watch=System.Diagnostics.Stopwatch.StartNew();
 var result=Layout.Generate(sites[0],new[]{new Point(70,0),new Point(180,240)},new[]{new Asset{Id="Doan",Width=40,Depth=32,Height=111,Homes=144},new Asset{Id="Deep front",Width=24,Depth=40,Height=50,Homes=60}},options,null,null);
 var ms=watch.ElapsedMilliseconds;allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
 result.LayoutTimings.Clear();var json=Newtonsoft.Json.JsonConvert.SerializeObject(result);var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json)));
 Console.WriteLine("run="+run+" ms="+ms+" allocated="+allocated+" hash="+hash+" buildings="+result.Parcels.Count(p=>p.Kind==0));
 }return;}
if(args.Length>0&&args[0]=="--villa-existing"){VillaTests.ExistingRoads();return;}
if(args.Length>0&&args[0]=="--villa-junction"){ConstructionTests.VillaJunction();ConstructionTests.VillaNodeSimplify();return;}
if(args.Length>0&&args[0]=="--tower"){TowerTests.Run();return;}
if(args.Length>0&&args[0]=="--compound-road-policy"){
 PlanBezier Line(double x,double y,double xx,double yy){var a=new Point(x,y);var d=new Point(xx,yy);return new PlanBezier{A=a,B=a+(d-a)*(1d/3),C=a+(d-a)*(2d/3),D=d};}
 var oldRoad=Line(-100,0,100,0);var newRoad=Line(0,0,0,80);
 Check(CompoundRoadPolicy.RecoverLegacyInvisible(false,true,true,false,newRoad.Slice(.2,.8),new[]{newRoad}),"owned legacy AP split road recovered");
 Check(!CompoundRoadPolicy.RecoverLegacyInvisible(false,false,true,false,newRoad,new[]{newRoad}),"unowned existing AP road protected");
 Check(!CompoundRoadPolicy.RecoverLegacyInvisible(true,true,true,false,newRoad,new[]{newRoad}),"explicit verified-road decisions preserved");
 Check(!CompoundRoadPolicy.RecoverLegacyInvisible(false,true,false,false,newRoad,new[]{newRoad}),"legacy ordinary roads protected");
 Check(!CompoundRoadPolicy.RecoverLegacyInvisible(false,true,true,true,newRoad,new[]{newRoad}),"existing-road-only plan protected");
 Check(!CompoundRoadPolicy.RecoverLegacyInvisible(false,true,true,false,oldRoad,new[]{newRoad}),"unplanned AP road protected");
 Check(!CompoundRoadPolicy.NewlyPlanned(oldRoad.Slice(.5,.8),new[]{newRoad},new[]{oldRoad}),"split existing road protected");
 Check(CompoundRoadPolicy.NewlyPlanned(newRoad.Slice(.1,.8),new[]{newRoad},new[]{oldRoad}),"planned new branch removable");
 Check(!CompoundRoadPolicy.NewlyPlanned(oldRoad,new[]{oldRoad},new[]{oldRoad}),"planned overlap does not claim old road");
 Check(!CompoundRoadPolicy.NewlyPlanned(Line(5,5,50,50),new[]{newRoad},new[]{oldRoad}),"unplanned new fragment protected");
 Check(!CompoundRoadPolicy.NewlyPlanned(newRoad,new PlanBezier[0],new[]{oldRoad}),"missing provenance fails closed");return;}
if(args.Length>0&&args[0]=="--villa-variety"){
 var lots=Enumerable.Range(0,12).Select(i=>new Parcel{Kind=0,X=i*13,Y=0,W=16,D=16,AssetId="A",PlacementForward=new Point(0,1)}).ToList();
 var varietyAssets=new[]{new Asset{Id="A",Width=16,Depth=16,Homes=2,Height=9},new Asset{Id="B",Width=16,Depth=16,Homes=4,Height=12},new Asset{Id="Rural C",Width=16,Depth=16},new Asset{Id="Wrong size",Width=24,Depth=16}};
 VillaVariety.Assign(lots,varietyAssets);var links=VillaVariety.Neighbours(lots);
 Check(links.SelectMany((ns,i)=>ns.Select(j=>lots[i].AssetId!=lots[j].AssetId)).All(b=>b),"adjacent row varietyAssets differ with two choices");
 Check(lots.All(p=>p.AssetId=="A"||p.AssetId=="B")&&lots.Select((p,i)=>p.X==i*13&&p.W==16&&p.D==16).All(b=>b),"positions and dimensions retained; excluded varietyAssets unused");
 var ids=string.Join(",",lots.Select(p=>p.AssetId));VillaVariety.Assign(lots,varietyAssets);Check(ids==string.Join(",",lots.Select(p=>p.AssetId)),"stable assignment");
 VillaVariety.Assign(lots,new[]{varietyAssets[0]});Check(lots.Count==12&&lots.All(p=>p.AssetId=="A"&&p.Homes==2),"single asset preserves buildings and metadata");return;}
if(args.Length>1&&(args[0]=="--villa-replay"||args[0]=="--villa-block-layout")){
 var input=Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(args[1]));var options=input["Options"].ToObject<LayoutOptions>();options.VillaMesh=true;
 var replayPlan=Layout.Generate(input["Boundary"].ToObject<List<Point>>(),input["Entrances"].ToObject<List<Point>>(),input["Assets"].ToObject<List<Asset>>(),options,input["Ramps"].ToObject<List<Parcel>>(),input["Zones"].ToObject<List<ManualZone>>());
 var houses=replayPlan.Parcels.Where(r=>r.Kind==0).ToList();var adjacent=VillaVariety.Neighbours(houses);Console.WriteLine("adjacent repeated pairs="+adjacent.SelectMany((ns,i)=>ns.Where(j=>j>i&&houses[i].AssetId==houses[j].AssetId)).Count());
 var courses=ConstructionCurves.Create(replayPlan);if(args[0]=="--villa-block-layout"){
 Check(replayPlan.VillaThrough&&replayPlan.VillaEmptyBlocks==0&&replayPlan.RequiredAccessFailures==0,"elongated site through route and occupied blocks");
 var junctions=new HashSet<string>();foreach(var a in courses.Where(c=>c.Use=="main"))foreach(var b in courses.Where(c=>c.Use=="aisle"))foreach(var q in new[]{a.Curve.A,a.Curve.D})if(new[]{b.Curve.A,b.Curve.D}.Any(v=>(v-q).Length<.05))junctions.Add(Math.Round(q.X,1)+","+Math.Round(q.Y,1));
 Check(junctions.Count>=3,"distributed alley/arterial junctions: "+junctions.Count);
 Check(replayPlan.Parcels.Count(r=>r.Kind==0)>=100,"housing retained on elongated fixture");
 Check(adjacent.SelectMany((ns,i)=>ns.Where(j=>j>i&&houses[i].AssetId==houses[j].AssetId)).Count()==0,"neighbour variety retained");
 }
System.IO.File.WriteAllText("docs/villa-access-replay.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{plan=replayPlan,courses}));
 Console.WriteLine("buildings="+replayPlan.Parcels.Count(r=>r.Kind==0)+" paths="+courses.Count(c=>c.Use=="pedestrian")+" warnings="+replayPlan.VillaPedestrianWarnings+" failures="+replayPlan.RequiredAccessFailures+" "+replayPlan.Diagnostics);return;}
if(args.Length>0&&args[0]=="--villa-sketch"){VillaTests.Sketch();return;}if(args.Length>0&&args[0]=="--villa"){VillaTests.Run();return;}if(args.Length>0&&args[0]=="--limits"){
 Check(new LayoutOptions().MaxParcels==4800,"raised parcel budget");
 foreach(var c in new[]{new{W=1100d,H=1000d,Key="AP.LimitSiteArea",Max=1000000d},new{W=2100d,H=100d,Key="AP.LimitSiteExtent",Max=2048d}}){
 try{Layout.Generate(new List<Point>{new Point(0,0),new Point(c.W,0),new Point(c.W,c.H),new Point(0,c.H)},new List<Point>{new Point(c.W/2,0)},new List<Asset>(),new LayoutOptions(),new List<Parcel>(),new List<ManualZone>());throw new Exception("Expected limit");}
 catch(PlanLimitException e){Check(e.Status==c.Key&&e.Maximum==c.Max&&e.Current>e.Maximum,"specific site rejection with measured values");}}
 return;}
if(args.Length>0&&args[0]=="--court-infill"){CourtInfillTests.Run();return;}if(args.Length>0&&args[0]=="--nearby-entrances"){NearbyEntranceTests.Run(args.Length>1?args[1]:"after");return;}if(args.Length>0&&args[0]=="--road-clearance"){RoadClearanceTests.Run();return;} if(args.Length>0&&args[0]=="--surface-merge"){SurfaceMergeTests.Run();return;} if(args.Length>0&&args[0]=="--walk-curves"){ConnectionUpdateTests.WalkCurves();return;} if(args.Length>0&&args[0]=="--entrance-walks"){ConnectionUpdateTests.EntranceWalks(args.Length>1?args[1]:"after");return;} if(args.Length>0&&args[0]=="--connection-update"){ConnectionUpdateTests.Run();return;} if(args.Length>0&&args[0]=="--parking-finish"){ParkingFinishTests.Run(args.Length>1?args[1]:"after");return;}
  if(args.Length>0&&args[0]=="--patterns-junctions"){PatternJunctionTests.Run();return;}
  if(args.Length>0&&args[0]=="--frontage-diagnostics"){FrontageReportTests.Run();return;}
  if(args.Length>1&&args[0]=="--frontage-report"){FrontageReportTests.Capture(args[1]);return;}
  if(args.Length>0&&args[0]=="--diagonal"){DiagonalRoadTests.Run(args.Length>1?args[1]:"after");return;}
  if(args.Length>0&&args[0]=="--wide-access"){WideAccessTests.Run();return;}
  if(args.Length>0&&args[0]=="--network-replacement"){NetworkReplacementTests.Run();return;}
  if(args.Length>0&&args[0]=="--native-geometry"){NativeGeometryTests.Run();return;}
  if(args.Length>0&&args[0]=="--enhancement"){EnhancementTests.Run();return;}
  if(args.Length>1&&args[0]=="--villa-trees"){
   var input=Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText("docs/villa-access-replay.json"));var plan=input["plan"].ToObject<Plan>();
   var species=new[]{new TreeSpecies{Id="small",Radius=1.5},new TreeSpecies{Id="medium",Radius=2.5},new TreeSpecies{Id="large",Radius=3}};
   var green=new GreenSpace(plan);int density=args[1]=="before"?12:30;var trees=green.Trees(species,density,6,73471);
   foreach(var tree in trees)if(!green.Fits(plan.Local(tree.Position),species.Single(t=>t.Id==tree.Species).Radius))throw new Exception("villa tree footprint");
   if(Newtonsoft.Json.JsonConvert.SerializeObject(trees)!=Newtonsoft.Json.JsonConvert.SerializeObject(green.Trees(species,density,6,73471)))throw new Exception("unstable trees");
   System.IO.File.WriteAllText("docs/villa-trees-"+args[1]+".json",Newtonsoft.Json.JsonConvert.SerializeObject(new{Plan=plan,Trees=trees,Density=density,Edge=trees.Count(t=>green.BoundaryDistance(t.Position)<=12)}));
   Console.WriteLine("Trees="+trees.Count+" edge="+trees.Count(t=>green.BoundaryDistance(t.Position)<=12));return;
  }
  if(args.Length>0&&args[0]=="--landscape"){LandscapeTests.Run();return;}
  if(args.Length>0&&args[0]=="--terrain-settlement"){LoopTests.Terrain();return;}
  if(args.Length>0&&args[0]=="--loops"){LoopTests.Run(args.Length>1?args[1]:"after");return;}
  if(args.Length>0&&args[0]=="--layout-worker"){LayoutWorkerTests.Run();return;}
  if(args.Length>0&&args[0]=="--single-entry"){ParkingDecalPreview.SingleEntry(args.Length>1?args[1]:"before");return;}
  if(args.Length>0&&args[0]=="--compound-additions"){CompoundAdditionTests.Run();return;}
  if(args.Length>0&&args[0]=="--deletion-policy"){SetbackTests.Deletion();return;}
  if(args.Length>0&&args[0]=="--setbacks"){SetbackTests.Run();return;}
  if(args.Length>0&&args[0]=="--management"){ManagementTests.Run();return;}
  if(args.Length>0&&args[0]=="--alley-preview"){ParkingDecalPreview.Run(8);return;}
  if(args.Length>0&&args[0]=="--narrow-preview"){ParkingDecalPreview.Run(4);return;}
  if(args.Length>0&&args[0]=="--cs-kr-preview"){ParkingDecalPreview.Run();return;}
  if(args.Length>0&&args[0]=="--frontage-road"){FrontageRoadTests.Run();return;}
  if(args.Length>0&&args[0]=="--manual-road"){ManualRoadTests.Run();return;}
  if(args.Length>0&&args[0]=="--short-curves"){ShortCurveTests.Run();return;}
  if(args.Length>0&&args[0]=="--plan-edit"){PlanEditTests.Run();return;}
  if(args.Length>0&&args[0]=="--native-access"){ConstructionTests.NativeAccess();return;}
  if(args.Length>0&&args[0]=="--doan-repro"){ConstructionTests.DoanRepro();return;}
  if(args.Length>0&&args[0]=="--construction"){ConstructionTests.Run();return;}
  if(args.Length>0&&args[0]=="--corners-editing"){CornerEditingTests.Run();return;}
  if(args.Length>0&&args[0]=="--review"){ReviewTests.Run();return;}
  if(args.Length>0&&args[0]=="--codec"){Codec.Test();return;}
  if(args.Length>0&&args[0]=="--parking-traffic"){ParkingTrafficTests.Run();return;}
  if(args.Length>0&&args[0]=="--parking-options"){ParkingOptionsTests.Run();return;}
  if(args.Length>0&&args[0]=="--offset-preview"){MasterPlanTests.Preview(args.Length>1?args[1]:"offset-reference");return;}

  if(args.Length>0&&args[0]=="--preview"){
   var polygon=new[]{new Point(0,0),new Point(280,0),new Point(280,210),new Point(210,245),new Point(60,200),new Point(0,130)};
   var watch=System.Diagnostics.Stopwatch.StartNew();var sample=Layout.Generate(polygon,new Point(140,0),new[]{new Asset{Id="Slab",Width=52,Depth=16,Height=45,Homes=80},new Asset{Id="Short slab",Width=36,Depth=16,Height=45,Homes=60}},new LayoutOptions(),null);
   var bars=sample.Parcels.Where(r=>r.Kind==0&&r.W>r.D).ToList();Console.WriteLine("Rows="+string.Join(",",bars.Select(r=>r.Y))+" "+sample.Diagnostics);
   var directory=System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory,"../../../../docs"));System.IO.Directory.CreateDirectory(directory);System.IO.File.WriteAllText(System.IO.Path.Combine(directory,"layout-reference.json"),System.Text.Json.JsonSerializer.Serialize(sample,new System.Text.Json.JsonSerializerOptions{IncludeFields=true}));Console.WriteLine("Generation ms="+watch.ElapsedMilliseconds+" buildings="+sample.Parcels.Count(r=>r.Kind==0)+" developed="+sample.Parcels.Where(r=>r.Kind!=4).Sum(r=>r.Area)/sample.Area);return;
  }
CornerEditingTests.Run();
var box=new List<Point>{new Point(0,0),new Point(400,0),new Point(400,400),new Point(0,400)};var assets=new[]{new Asset{Id="A",Width=32,Depth=40,Height=45,Homes=90},new Asset{Id="B",Width=24,Depth=32,Height=30,Homes=60,Footprint=500}};var o=new LayoutOptions();var p=Layout.Generate(box,new Point(200,0),assets,o,null);Verify(p);Check(p.Parcels.Any(r=>r.Kind==0)&&p.Parcels.Where(r=>r.Kind==0).Select(r=>r.AssetId).Distinct().Count()==2,"multiple assets placed");Check(new[]{1,2,4}.All(k=>p.Parcels.Any(r=>r.Kind==k))&&!p.Parcels.Any(r=>r.Kind==3||r.Kind==5),"roads parking green without automatic facilities");Check(p.Parcels[0].W==8&&p.Parcels[0].D==15,"default entrance 8 x 15");
  box.Reverse();Verify(Layout.Generate(box,new Point(200,0),assets,o,null));Check(true,"clockwise boundary");
  var concave=new List<Point>{new Point(0,0),new Point(300,0),new Point(300,100),new Point(150,100),new Point(150,300),new Point(0,300)};Verify(Layout.Generate(concave,new Point(75,0),assets,o,null));
  Check(!Layout.Valid(new[]{new Point(0,0),new Point(100,100),new Point(0,100),new Point(100,0)}),"self crossing rejected");
  var notch=new[]{new Point(0,0),new Point(100,0),new Point(100,100),new Point(60,100),new Point(60,20),new Point(40,20),new Point(40,100),new Point(0,100)};Check(!Layout.Contains(new Parcel{X=10,Y=10,W=80,D=80},notch),"concave notch cannot enter parcel");
  var ramp=new Parcel{Kind=6,X=80,Y=20,W=8,D=20};Verify(Layout.Generate(box,new Point(200,0),assets,o,new[]{ramp}));
  try{Layout.Generate(box,new Point(0,0),assets,o,null);throw new Exception("bad entrance accepted");}catch(ArgumentException){Check(true,"corner entrance rejected");}
  var small=new[]{new Point(0,0),new Point(40,0),new Point(40,40),new Point(0,40)};var tiny=Layout.Generate(small,new Point(20,0),assets,o,null);Check(!tiny.Parcels.Any(r=>r.Kind==0),"small site does not force oversized buildings");
  var reference=new[]{new Point(0,0),new Point(280,0),new Point(280,210),new Point(210,245),new Point(60,200),new Point(0,130)};
  var slabs=new[]{new Asset{Id="Slab",Width=52,Depth=16,Height=45,Homes=80},new Asset{Id="Tower",Width=24,Depth=24,Height=55,Homes=100}};
  var residential=Layout.Generate(reference,new Point(140,0),slabs,new LayoutOptions{TargetFar=50},null);Verify(residential);
  Check(residential.Parcels.Count(r=>r.Kind==0)>=6,"reference site forms multiple residential rows");
  Check(residential.Parcels.Sum(r=>r.Area)/residential.Area>.98,"irregular polygon coverage above 98 percent");
  Check(residential.Parcels.Count<=1200,"coalesced overlay budget");
  var roads=residential.Parcels.Where(r=>r.Kind==1).ToList();var reached=new HashSet<Parcel>{roads[0]};bool changed=true;
  while(changed){changed=false;foreach(var road in roads)if(!reached.Contains(road)&&reached.Any(r=>Touches(r,road))){reached.Add(road);changed=true;}}
  Check(reached.Count==roads.Count,"all circulation connects to entrance");
  Check(residential.Parcels.Where(r=>r.Kind==2).All(r=>r.ParkingSpaces>=4&&Math.Min(r.W,r.D)>=5&&r.ParkingSpaces==Math.Floor((r.ParkingAlongX?r.W:r.D)/2.5+.001)),"parking bay depth width and capacity");
  var high=Layout.Generate(reference,new Point(140,0),slabs,new LayoutOptions{TargetFar=600},null);
  Check(high.Parcels.Count(r=>r.Kind==0)==residential.Parcels.Count(r=>r.Kind==0),"default composition independent of FAR target");
  var limited=Layout.Generate(reference,new Point(140,0),slabs,new LayoutOptions{TargetFar=50,RespectTargetFar=true},null);
  Check(limited.Parcels.Count(r=>r.Kind==0)<residential.Parcels.Count(r=>r.Kind==0),"optional FAR limit retained");
  foreach(var bay in residential.Parcels.Where(r=>r.Kind==2))Check(residential.Parcels.Any(r=>r.Kind==1&&Layout.Overlap(r,bay,.01)),"parking touches access road");
  var docs=System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory,"../../../../docs"));System.IO.Directory.CreateDirectory(docs);System.IO.File.WriteAllText(System.IO.Path.Combine(docs,"layout-reference.json"),System.Text.Json.JsonSerializer.Serialize(residential,new System.Text.Json.JsonSerializerOptions{IncludeFields=true}));
  var lowDemand=Layout.Generate(reference,new Point(140,0),slabs,new LayoutOptions{SurfaceParkingShare=.1},null);
  var highDemand=Layout.Generate(reference,new Point(140,0),slabs,new LayoutOptions{SurfaceParkingShare=1},null);
  Check(new[]{lowDemand,highDemand}.All(plan=>plan.Parcels.Where(r=>r.Kind==0).All(b=>ParkingFrontRules.Served(b,plan.Parcels,40,4))),"parking demand alternatives preserve mandatory front parking");
  ReviewTests.Run();MasterPlanTests.Run();ParkingOptionsTests.Run();ParkingTrafficTests.Run();Codec.Test();SnapTests.Run();Console.WriteLine("Representative geometry checks passed; game runtime not tested.");
 }
}







