using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using ApartmentPlan;

static partial class LayoutSweep {
 public static void SlabGallery(string folder,bool tower=false){
  Directory.CreateDirectory(folder);
  var rows=new List<object>();
  Asset Bar(int w,int d,int homes)=>new Asset{Id=w+"x"+d,Width=w,Depth=d,Height=40,Homes=homes};
  bool matrix=tower&&Environment.GetEnvironmentVariable("AP_TOWER_MATRIX")=="1";
  for(int id=1;id<=(matrix?32:30);id++){
   if(matrix&&id!=3&&id<30)continue;
   var boundary=Outline(0,0,240,0,240,180,0,180);
   var gates=Outline(90,0);
   var assets=new[]{Bar(32,16,50)};
   var o=new LayoutOptions{ContinuousFrontage=true,Road=16,ParkingAisle=8,ParallelGap=24,MainRoadPedestrian=true,AisleRoadPedestrian=true};
   var zones=new List<ManualZone>();
   string label;
   if(tower){
    o.Tower=true;
    o.TowerParking=new Asset{Id="garage",Width=16,Depth=24};
   }
   if(tower&&id>=16&&id<=26){
    switch(id){
     case 16:label="주동 방향 0°";o.TowerRules.Set("Angle",0);break;
     case 17:label="주동 방향 90°";o.TowerRules.Set("Angle",90);break;
     case 18:label="동간 간격 40 m";o.TowerRules.Set("BuildingGap",40);break;
     case 19:label="부지 경계 이격 14 m";o.TowerRules.Set("BuildingBoundary",14);break;
     case 20:label="주동 상한 5동";o.TowerRules.Set("MaxBuildings",5);break;
     case 21:label="주차장 에셋 미선택";o.TowerParking=null;break;
     case 22:label="대형 주차장 32 × 40 m";o.TowerParking.Width=32;o.TowerParking.Depth=40;break;
     case 23:label="입구 안쪽 이격 44 m";o.TowerRules.Set("EntranceInset",44);break;
     case 24:label="보행로 생성 끄기";o.TowerRules.Set("Walks",0);break;
     case 25:label="도로 회전 반경 10 m";o.TowerRules.Set("RoadRadius",10);break;
     default:label="보행로 경계 이격 12 m";o.TowerRules.Set("WalkBoundary",12);break;
    }
   }else switch(id){
    case 1:label="기준 구역";break;
    case 2:label="소형 96 × 80 m";boundary=Outline(0,0,96,0,96,80,0,80);gates=Outline(48,0);break;
    case 3:label="대형 420 × 300 m";boundary=Outline(0,0,420,0,420,300,0,300);break;
    case 4:label="폭 66 m 긴 구역";boundary=Outline(0,0,340,0,340,66,0,66);break;
    case 5:label="U자형 구역";boundary=Outline(0,0,240,0,240,180,165,180,165,62,75,62,75,180,0,180);break;
    case 6:label="사다리꼴 구역";boundary=Outline(0,0,240,0,185,180,34,180);break;
    case 7:label="입구를 모서리 2 m로";gates=Outline(2,0);break;
    case 8:label="맞은편 입구 추가";gates=Outline(90,0,90,180);break;
    case 9:label="측면 입구 추가";gates=Outline(90,0,240,110);break;
    case 10:label="같은 변에 8 m 간격 입구";gates=Outline(90,0,98,0);break;
    case 11:label="세 방향 입구";gates=Outline(90,0,240,110,105,180);break;
    case 12:label="에셋 선택 해제";assets=Array.Empty<Asset>();break;
    case 13:label="소형 동 24 × 16 m";assets=new[]{Bar(24,16,30)};break;
    case 14:label="장대형 동 72 × 24 m";assets=new[]{Bar(72,24,100)};break;
    case 15:label="세 크기 에셋 혼합";assets=new[]{Bar(24,16,30),Bar(32,16,50),Bar(56,24,80)};break;
    case 16:label="가로형 동만";o.BuildingPattern=1;break;
    case 17:label="세로형 동만";o.BuildingPattern=2;break;
    case 18:label="평행 동간 간격 40 m";o.ParallelGap=40;break;
    case 19:label="경계 녹지 여유 12 m";o.BoundaryGreenMargin=12;break;
    case 20:label="메인 도로 폭 24 m";o.Road=24;break;
    case 21:label="지상 주차 끄기";o.GenerateSurfaceParking=false;break;
    case 22:label="주차 통로 폭 12 m";o.ParkingAisle=12;break;
    case 23:label="주차 섬 3 m";o.ParkingIsland=3;break;
    case 24:label="보행로 생성 끄기";o.PedestrianNetwork=false;break;
    case 25:label="동 측면 보행 출입구";assets[0].PedestrianAccess.Add(new Point(16,0));break;
    case 26:label="목표 용적률 100% 적용";o.RespectTargetFar=true;o.TargetFar=100;break;
    case 27:label="중앙 시설 구역";zones.Add(new ManualZone{Kind=5,Points=Outline(95,66,145,66,145,112,95,112).ToList()});break;
    case 28:label="입구를 막는 시설 구역";zones.Add(new ManualZone{Kind=5,Points=Outline(64,0,116,0,116,52,64,52).ToList()});break;
    case 29:label="대형 동 + 좁은 구역";boundary=Outline(0,0,180,0,180,70,0,70);assets=new[]{Bar(72,24,100)};break;
    case 31:label="대형 부지 + 두 입구 + 단일 동";boundary=Outline(0,0,420,0,420,300,0,300);gates=Outline(90,0,420,120);break;
    case 32:label="대형 부지 + 한 입구 + 혼합 동";boundary=Outline(0,0,420,0,420,300,0,300);assets=new[]{Bar(24,16,30),Bar(56,24,80)};break;
    default:label="대형 부지 + 두 입구 + 혼합 동";boundary=Outline(0,0,420,0,420,300,0,300);gates=Outline(90,0,420,120);assets=new[]{Bar(24,16,30),Bar(56,24,80)};break;
   }
   var input=new{id,label,boundary,gates,zones,assets,options=o};
   var watch=Stopwatch.StartNew();Plan p;
   try{p=Layout.GenerateHousing(boundary,gates,assets,o,null,zones);}
   catch(ArgumentException ex){rows.Add(new{input,ms=watch.ElapsedMilliseconds,rejection=ex.Message,planning=ex.Data["planning"],reasons=ex.Data["layoutReasons"]});continue;}
   watch.Stop();
   var lots=p.Parcels.Where(q=>q.Kind==0).ToList();
   var houses=lots.Select(q=>(q.PlacementOutline??q.Corners).Select(p.World).ToArray()).ToArray();
   var roads=ConstructionCurves.Create(p).ToArray();var vehicles=roads.Where(c=>c.Use!="pedestrian").ToArray();
   var conflicts=new List<string>();
   for(int i=0;i<vehicles.Length;i++)for(int j=0;j<i;j++){var issue=Conflict(vehicles[i],vehicles[j]);if(issue==null&&ParallelOverlap(vehicles[i],vehicles[j]))issue="parallel";if(issue!=null)conflicts.Add(issue+" "+i+" "+j);}
   if(tower){
    var garages=p.Parcels.Where(q=>q.Kind==6).Select(q=>(q.PlacementOutline??q.Corners).Select(p.World).ToArray()).ToArray();
    // Follow course endpoints from every designated entrance to each parking loop.
    var reached=new HashSet<ConstructionCourse>();
    bool Touch(Point a,Point b)=>(a-b).Length<.05;
    foreach(var c in vehicles)if(gates.Any(g=>Touch(g,c.Curve.A)||Touch(g,c.Curve.D)))reached.Add(c);
    int previous;
    do{previous=reached.Count;foreach(var c in vehicles)if(!reached.Contains(c)&&reached.Any(r=>Touch(r.Curve.A,c.Curve.A)||Touch(r.Curve.A,c.Curve.D)||Touch(r.Curve.D,c.Curve.A)||Touch(r.Curve.D,c.Curve.D)))reached.Add(c);}while(previous!=reached.Count);
    var loops=vehicles.Where(c=>c.Use=="turnaround").ToList();
    int disconnectedLoops=loops.Count(c=>!reached.Contains(c));
    rows.Add(new{input,ms=watch.ElapsedMilliseconds,houses,roads,garages,disconnectedLoops,loopCourses=loops.Count,conflicts,outside=houses.Count(h=>!VillaGeometry.Inside(h,boundary)),missing=FrontageNetwork.Missing(p,assets,o.ParkingAisle,0),access=p.RequiredAccessFailures,walks=roads.Count(c=>c.Use=="pedestrian")});
    continue;
   }
   rows.Add(new{input,ms=watch.ElapsedMilliseconds,houses,roads,conflicts,outside=houses.Count(h=>!VillaGeometry.Inside(h,boundary)),missing=FrontageNetwork.Missing(p,assets,o.ParkingAisle,0),access=p.RequiredAccessFailures,parkingFailures=p.ParkingAccessFailures,spaces=p.Parcels.Sum(q=>q.ParkingSpaces),far=p.Far});
  }
  File.WriteAllText(Path.Combine(folder,"results.json"),JsonConvert.SerializeObject(rows));
 }
}

static partial class LayoutSweep {
 public static void MixedLimits(string folder){
  Directory.CreateDirectory(folder);
  using var output=new StreamWriter(Path.Combine(folder,"results.jsonl"));
  foreach(string mode in new[]{"slab","tower","villa"})
  foreach(string site in new[]{"standard","large","thin","notch","large-two"})
  foreach(int count in (Environment.GetEnvironmentVariable("AP_MIX_DESC")=="1"?new[]{5,3,1}:new[]{1,3,5})){
   string targetSite=Environment.GetEnvironmentVariable("AP_MIX_SITE"),targetMode=Environment.GetEnvironmentVariable("AP_MIX_MODE");
   if(targetSite!=null&&targetSite!=site||targetMode!=null&&targetMode!=mode)continue;
   if(Environment.GetEnvironmentVariable("AP_MIX_LARGE_ONLY")=="1"&&!site.StartsWith("large"))continue;
   bool large=site.StartsWith("large");int w=large?420:240,h=large?300:site=="thin"?84:180;
   var boundary=site=="notch"?Outline(0,0,w,0,w,h,160,h,160,72,80,72,80,h,0,h):Outline(0,0,w,0,w,h,0,h);
   var gates=site=="large-two"?Outline(90,0,w,120):Outline(90,0);
   bool villa=mode=="villa";
   bool reverseVilla=Environment.GetEnvironmentVariable("AP_MIX_REVERSE_VILLA")=="1";
   if(reverseVilla&&(!villa||count==1))continue;
   var sizes=villa?new[]{(16,16),(12,8),(24,16),(8,8),(32,24)}:new[]{(32,16),(24,12),(56,24),(16,16),(72,32)};
   var assets=sizes.Take(count).Select(a=>new Asset{Id=a.Item1+"x"+a.Item2,Width=a.Item1,Depth=a.Item2,Homes=10,Height=villa?8:40}).ToArray();
   if(reverseVilla)Array.Reverse(assets);
   var stages=new Dictionary<string,long>();
   var o=new LayoutOptions{Tower=mode=="tower",Villa=villa,TowerParking=new Asset{Id="garage",Width=16,Depth=24},ContinuousFrontage=true,MainRoadPedestrian=true,AisleRoadPedestrian=true,Road=16,ParkingAisle=8,ParallelGap=24};
   if(villa)foreach(var g in gates){var normal=g.Y==0?new Point(0,-1):new Point(1,0);o.VillaConnections.Add(new VillaConnection{Boundary=g,Road=g+normal*8,Tangent=new Point(-normal.Y,normal.X),Manual=true});}
   Dictionary<string,int> failureMetrics=null;
   o.Timing=(k,v)=>stages[k]=stages.GetValueOrDefault(k)+v;
   if(Environment.GetEnvironmentVariable("AP_MIX_PROFILE")=="1")o.FunctionTiming=new LayoutFunctionTiming();
   var watch=Stopwatch.StartNew();long allocated=GC.GetAllocatedBytesForCurrentThread();Plan plan=null;string error=null;
   try{plan=Layout.GenerateHousing(boundary,gates,assets,o,null,null);}catch(ArgumentException e){error=e.Message;failureMetrics=e.Data["planning"] as Dictionary<string,int>;}
   watch.Stop();long bytes=GC.GetAllocatedBytesForCurrentThread()-allocated;
   var metrics=plan?.Planning??failureMetrics;
   var functions=new List<string>();o.FunctionTiming?.Flush(functions.Add);
   var mix=plan?.Parcels.Where(p=>p.Kind==0).GroupBy(p=>p.AssetId).ToDictionary(g=>g.Key,g=>g.Count());
   output.WriteLine(JsonConvert.SerializeObject(new{mode,site,count,ms=watch.ElapsedMilliseconds,bytes,error,mix,outlines=count==5&&site=="standard"?plan?.Parcels.Where(q=>q.Kind==0).Select(q=>new{q.AssetId,points=(q.PlacementOutline??q.Corners).Select(plan.World).Select(v=>new[]{v.X,v.Y})}):null,access=plan?.RequiredAccessFailures,parking=plan?.ParkingAccessFailures,metrics,stages,functions}));output.Flush();
   bool knownFit=mode!="tower"||site=="standard"||site.StartsWith("large");
   if(knownFit&&(mix==null||mix.Count!=count||plan.RequiredAccessFailures!=0||plan.ParkingAccessFailures!=0))
    throw new InvalidOperationException($"Mixed footprint regression: {mode} {site} {count} ({error})");
  }
 }
}

