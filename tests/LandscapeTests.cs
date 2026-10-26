using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ApartmentPlan;
static class LandscapeTests {
 static void Check(bool v,string label){if(!v)throw new Exception(label);}
 public static void Run(){
  var fixture=JObject.Parse(File.ReadAllText("ApartmentPlan/docs/loop-after.json"));var plan=fixture["Plan"].ToObject<Plan>();
  var green=new GreenSpace(plan);var types=new[]{new TreeSpecies{Id="EU_AlderTree01",Radius=2.5},new TreeSpecies{Id="NA_LindenTree01",Radius=3},new TreeSpecies{Id="NA_HickoryTree01",Radius=3}};
  var counts=new List<int>();
  foreach(int density in new[]{6,12,30}){
   var trees=green.Trees(types,density,6,73471);counts.Add(trees.Count);Check(trees.Count>0,"empty planting");
   Check(trees.All(t=>t.Age==1||t.Age==2)&&trees.Select(t=>t.Species).Distinct().Count()==3,"multi species / ages");
   foreach(var tree in trees)Check(green.Fits(plan.Local(tree.Position),types.Single(t=>t.Id==tree.Species).Radius),"tree footprint outside green");
   Check(JsonConvert.SerializeObject(trees)==JsonConvert.SerializeObject(green.Trees(types,density,6,73471)),"unstable regeneration");
   File.WriteAllText("ApartmentPlan/docs/landscape-"+density+".json",JsonConvert.SerializeObject(new{Plan=plan,Trees=trees,Density=density,Species=types,Entrances=fixture["Entrances"]}));
  }
  Check(counts[0]<counts[1]&&counts[1]<counts[2],"density response");
  Check(green.Trees(types,0,6,1).Count==0&&green.Trees(types,12,0,1).Count==0&&green.Trees(new TreeSpecies[0],12,6,1).Count==0,"empty settings");
  var patch=new Plan{Right=new Point(1,0),Forward=new Point(0,1),Parcels=new List<Parcel>{new Parcel{Kind=4,X=0,Y=0,W=10,D=20},new Parcel{Kind=4,X=10,Y=0,W=10,D=8},new Parcel{Kind=4,X=10,Y=12,W=10,D=8}}};
  var mask=new GreenSpace(patch);Check(mask.Fits(new Point(10,4),2),"green seam rejected");Check(!mask.Fits(new Point(10,10),2)&&!mask.Fits(new Point(.5,5),2),"hole/exterior admitted");
  var grass=mask.Grass(1,0,0).ToList();Check(grass.Count>0&&grass.All(q=>mask.Fits(q,2.2)),"grass brush leaks");
  Check(mask.GrassCandidates(1,0,0).Count()>grass.Count,"candidate budget not separable");
  Console.WriteLine("PASS landscape: density counts "+string.Join("/",counts)+"; multi ages/species, deterministic, green coverage, hole/edge protection, bounded grass candidates");
 }
}
