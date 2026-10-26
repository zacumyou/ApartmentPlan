using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.IO;
using ApartmentPlan;
static class SetbackTests {
 static double Gap(Parcel a,Parcel b)=>Math.Sqrt(Math.Pow(Math.Max(0,Math.Max(a.X-b.X-b.W,b.X-a.X-a.W)),2)+Math.Pow(Math.Max(0,Math.Max(a.Y-b.Y-b.D,b.Y-a.Y-a.D)),2));
 static void Check(bool value,string text){if(!value)throw new Exception(text);}
 public static void Deletion(){
  Check(PlanDeletionPolicy.Target(false,1,2)==2&&PlanDeletionPolicy.Target(true,1,2)==1&&PlanDeletionPolicy.Target(true,-1,2)==-2,"delete visible target not hidden edit");
  Check(PlanDeletionPolicy.Allowed(true,false,false,false,false,true,false,false),"idle draft deletion");
  for(int i=0;i<7;i++){bool ready=i!=0,calc=i==1,native=i==2,demo=i==3,dialog=i==4,owned=i==5,built=i==6;Check(!PlanDeletionPolicy.Allowed(ready,calc,native,demo,dialog,true,owned,built),"unsafe deletion "+i);}
  Check(!PlanDeletionPolicy.Allowed(true,false,false,false,false,false,false,false),"empty deletion");
  var forced=PlanDeletionPolicy.ForceTargets(new[]{1,2,3,4,5,1},new HashSet<int>{1,2,3,4},new HashSet<int>{2},e=>e!=3).ToArray();
  Check(forced.SequenceEqual(new[]{1,4}),"force deletion excludes other ownership, missing entities, foreign roots and duplicates");
  Console.WriteLine("PASS deletion safety and visible target policy");
 }
 public static void Run(){
  Deletion();
  var boundary=new[]{new Point(0,0),new Point(160,0),new Point(160,275),new Point(0,325)};
  var entries=new[]{new Point(60,0),new Point(120,287.5)};
  var assets=new[]{new Asset{Id="Slab 40m",Width=40,Depth=12,Height=45,Homes=80},new Asset{Id="Slab 24m",Width=24,Depth=12,Height=30,Homes=40}};
  foreach(var pair in new[]{new[]{4,4},new[]{12,4},new[]{4,12},new[]{0,0}}){
   var o=new LayoutOptions{MainParkingSetback=pair[0],BoundaryGreenMargin=pair[1],ContinuousFrontage=true,RequireBuildingRoad=true,MainRoadPedestrian=true,AisleRoadPedestrian=true,Road=16,AccessRoad=4,ParkingAisle=4,MainCurveRadius=40,BayWidth=2.85,BayDepth=5.97292232,Play=0,Facility=0};
   var p=Layout.Generate(boundary,entries,assets,o,null,null);
   Check(p.Parcels.Any(r=>r.Kind==0),"no buildings");
   Check(p.RequiredAccessFailures==0&&p.ParkingAccessFailures==0&&FrontageNetwork.Missing(p,assets,4,1)==0,"access failed");
   var local=boundary.Select(p.Local).ToArray();double minMain=999,minEdge=999;
   foreach(var r in p.Parcels){
    Check(Layout.Contains(r,local),"outside");
    if(r.Kind!=0&&r.Kind!=2)continue;
    foreach(var road in p.MainRoadArea)minMain=Math.Min(minMain,Gap(r,road));
    foreach(var q in r.Corners){var near=SiteFrame.Nearest(local,q,out _);minEdge=Math.Min(minEdge,(q-near).Length);}
   }
   Check(minMain>=pair[0]-.01,"main setback violated: "+minMain);
   Check(minEdge>=pair[1]-.01,"edge setback violated: "+minEdge);
   for(int i=0;i<p.Parcels.Count;i++)for(int j=0;j<i;j++)Check(!Layout.Overlap(p.Parcels[i],p.Parcels[j]),"overlap");
   var graph=ConstructionGraph.Create(p).Where(e=>e.Use!="pedestrian").ToList();var visited=new HashSet<ConstructionEdge>{graph[0]};bool more=true;
   while(more){more=false;foreach(var e in graph)if(!visited.Contains(e)&&visited.Any(q=>new[]{q.A,q.B}.Any(a=>new[]{e.A,e.B}.Any(b=>(a-b).Length<.03)))){visited.Add(e);more=true;}}
   Check(visited.Count==graph.Count,"disconnected native graph");
   File.WriteAllText("ApartmentPlan/docs/setback-"+pair[0]+"-"+pair[1]+".json",JsonSerializer.Serialize(new{Plan=p,Assets=assets,Options=o,Entrances=entries},new JsonSerializerOptions{IncludeFields=true}));
   Console.WriteLine("PASS main="+pair[0]+" boundary="+pair[1]+" actualMin="+minMain+"/"+minEdge+" buildings="+p.Parcels.Count(r=>r.Kind==0)+" parking="+p.Parcels.Sum(r=>r.ParkingSpaces));
  }
  var forced=PlanDeletionPolicy.ForceTargets(new[]{1,2,3,4,5,1},new HashSet<int>{1,2,3,4},new HashSet<int>{2},e=>e!=3).ToArray();
  Check(forced.SequenceEqual(new[]{1,4}),"force deletion excludes other ownership, missing entities, foreign roots and duplicates");
  Console.WriteLine("PASS deletion safety gate matrix");
 }
}
