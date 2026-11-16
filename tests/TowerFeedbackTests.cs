using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using ApartmentPlan;
public static class TowerFeedbackTests {
 public static void Run(){
  foreach(double size in new[]{32d,48d,64d}){
   var assets=new[]{new Asset{Id="A",Width=size,Depth=size,Height=80,Homes=100},new Asset{Id="B",Width=size,Depth=size,Height=80,Homes=100}};
   var rules=new TowerRules();rules.Set("Walks",0);
   var o=new LayoutOptions{Tower=true,TowerParking=new Asset{Id="parking",Width=16,Depth=24},ParkingAisle=8,Road=24,Walkway=2,Gap=10,TowerRules=rules,Diagnostic=Console.WriteLine};
   {var p=Layout.Generate(new[]{new Point(0,0),new Point(300,0),new Point(300,240),new Point(0,240)},new[]{new Point(150,0)},assets,o,new List<Parcel>(),new List<ManualZone>());
    Console.WriteLine("CASE "+size+" "+string.Join(",",p.Parcels.Where(q=>q.Kind==0).GroupBy(q=>q.AssetId).Select(g=>g.Key+":"+g.Count())));
    var buildings=p.Parcels.Where(q=>q.Kind==0).ToList();
    if(buildings.Count!=(size==32?20:size==48?10:6)||buildings.Select(q=>q.AssetId).Distinct().Count()!=2)throw new Exception("capacity/variety");
    if(Math.Abs(buildings.Count(q=>q.AssetId=="A")-buildings.Count(q=>q.AssetId=="B"))>1)throw new Exception("asset balance");
    if(FrontageNetwork.Missing(p,assets,8,1)!=0)throw new Exception("unconnected frontage");
    for(int i=0;i<buildings.Count;i++)for(int j=0;j<i;j++)if(TowerGeometry.Distance(buildings[i].PlacementOutline,buildings[j].PlacementOutline)<17.999)throw new Exception("building gap");
    if(size==48){
     var reversed=Layout.Generate(new[]{new Point(0,0),new Point(300,0),new Point(300,240),new Point(0,240)},new[]{new Point(150,0)},assets.Reverse().ToArray(),o,new List<Parcel>(),new List<ManualZone>());
     string Signature(Plan plan)=>Newtonsoft.Json.JsonConvert.SerializeObject(new{plan.Parcels,plan.Routes});
     if(Signature(p)!=Signature(reversed))throw new Exception("selection order changed layout");
    }
    File.WriteAllText("docs/tower-feedback-"+size+".json",Newtonsoft.Json.JsonConvert.SerializeObject(p));
   }
  }
  var mixed=new[]{new Asset{Id="A",Width=40,Depth=32,Height=80,Homes=100},new Asset{Id="B",Width=48,Depth=40,Height=80,Homes=120},new Asset{Id="TooLarge",Width=500,Depth=500,Height=80,Homes=200}};
  var rules2=new TowerRules();rules2.Set("Walks",0);
  var mixedPlan=Layout.Generate(new[]{new Point(0,0),new Point(360,0),new Point(360,300),new Point(0,300)},new[]{new Point(180,0)},mixed,new LayoutOptions{Tower=true,TowerParking=new Asset{Id="parking",Width=16,Depth=24},ParkingAisle=8,Road=24,TowerRules=rules2},new List<Parcel>(),new List<ManualZone>());
  Console.WriteLine("MIXED "+string.Join(",",mixedPlan.Parcels.Where(q=>q.Kind==0).GroupBy(q=>q.AssetId).Select(g=>g.Key+":"+g.Count()))+" missing="+FrontageNetwork.Missing(mixedPlan,mixed,8,1));
  if(mixedPlan.Parcels.Any(q=>q.AssetId=="TooLarge")||mixedPlan.Parcels.Where(q=>q.Kind==0).Select(q=>q.AssetId).Distinct().Count()!=2||FrontageNetwork.Missing(mixedPlan,mixed,8,1)!=0)throw new Exception("mixed-size feasible selection");
  Console.WriteLine("mixed sizes, infeasible asset exclusion and frontage passed");
  Console.WriteLine("TOWER_FEEDBACK_PASSED");
 }
}
