using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ApartmentPlan;
static class EnhancementTests {
 static void Check(bool b,string text){if(!b)throw new Exception(text);}
 public static void Run(){
  var input=JObject.Parse(File.ReadAllText("ApartmentPlan/docs/loop-before-layout-last-input.json"));
  var boundary=input["Boundary"].ToObject<List<Point>>();var entrances=input["Entrances"].ToObject<List<Point>>();var assets=input["Assets"].ToObject<List<Asset>>();var o=input["Options"].ToObject<LayoutOptions>();
  var zones=input["Zones"].ToObject<List<ManualZone>>();var ramps=input["Ramps"].ToObject<List<Parcel>>();var roads=input["Roads"]?.ToObject<List<ManualRoad>>();
  o.WideParkingAisle=0;Verify(Layout.Generate(boundary,entrances,assets,o,ramps,zones,roads),assets,o);o.WideParkingAisle=8;
  var compact=Layout.Generate(boundary,entrances,assets,o,ramps,zones,roads);Verify(compact,assets,o);
  Check(compact.ConstructionRoutes.Any(r=>r.Use=="aisleWide"&&r.Width==8),"no native 8m aisle");
  var stalls=compact.Parcels.Where(r=>r.Kind==2).ToList();int paired=0;
  foreach(var a in stalls)foreach(var b in stalls){if(ReferenceEquals(a,b)||a.ParkingAlongX!=b.ParkingAlongX||a.ParkingEntrySide!=-b.ParkingEntrySide)continue;
   bool h=a.ParkingAlongX;double rearA=h?(a.ParkingEntrySide<0?a.Y+a.D:a.Y):(a.ParkingEntrySide<0?a.X+a.W:a.X);
   double rearB=h?(b.ParkingEntrySide<0?b.Y+b.D:b.Y):(b.ParkingEntrySide<0?b.X+b.W:b.X);
   if(Math.Abs(rearA-rearB)<.01)paired++;
  }Check(paired>0,"back-to-back rows not closed");
  double winding=Layout.SignedArea(boundary)>0?1:-1;
  for(int i=0;i<boundary.Count;i++){var a=boundary[i];var d=boundary[(i+1)%boundary.Count]-a;var normal=new Point(-d.Y/d.Length*winding,d.X/d.Length*winding);int n=Math.Max(1,(int)Math.Ceiling(d.Length/40));for(int j=0;j<n;j++){var p=a+d*((j+.5)/n);if(entrances.Any(e=>(e-p).Length<24))continue;o.ExternalWalkTargets.Add(new ExternalWalkTarget{Boundary=p,Inside=p+normal*(o.Walkway+3),Road=p-normal*6});}}
  var walks=Layout.Generate(boundary,entrances,assets,o,ramps,zones,roads);Verify(walks,assets,o);
  int links=walks.ConstructionRoutes.Count(r=>r.Use=="pedestrian"&&r.Points.Any(p=>!Layout.Inside(walks.World(p),boundary)));
  Check(links>0&&links<=4,"outside road links missing or excessive");
  Check(compact.Parcels.Sum(p=>p.ParkingSpaces)==walks.Parcels.Sum(p=>p.ParkingSpaces),"walk removed parking");
  var species=new[]{new TreeSpecies{Id="EU_AlderTree01",Radius=2.5},new TreeSpecies{Id="NA_LindenTree01",Radius=3},new TreeSpecies{Id="NA_HickoryTree01",Radius=3}};
  foreach(var pair in new[]{Tuple.Create("compact",compact),Tuple.Create("walks",walks)}){
   var trees=new GreenSpace(pair.Item2).Trees(species,12,6,73471);
   File.WriteAllText("ApartmentPlan/docs/enhancement-"+pair.Item1+".json",JsonConvert.SerializeObject(new{Plan=pair.Item2,Assets=assets,Options=o,Entrances=entrances,Zones=zones,Trees=trees,Species=species}));
  }
  Console.WriteLine("PASS enhancement: buildings="+walks.Parcels.Count(r=>r.Kind==0)+" parking="+walks.Parcels.Sum(r=>r.ParkingSpaces)+" touchingRearPairs="+paired/2+" wideRoads="+walks.ConstructionRoutes.Count(r=>r.Use=="aisleWide")+" externalWalks="+links);
 }
 internal static void Verify(Plan p,List<Asset> assets,LayoutOptions o){
  Check(p.ParkingAccessFailures==0&&p.RequiredAccessFailures==0,"access failures");
  var local=p.Boundary.Select(p.Local).ToArray();foreach(var r in p.Parcels)Check(Layout.Contains(r,local),"outside parcel");
  for(int i=0;i<p.Parcels.Count;i++)for(int j=0;j<i;j++)Check(!Layout.Overlap(p.Parcels[i],p.Parcels[j]),"overlap");
  Check(FrontageNetwork.Missing(p,assets,o.ParkingAisle,o.ParkingPavingMargin)==0,"native frontage");
  var graph=ConstructionGraph.Create(p);var seen=new HashSet<ConstructionEdge>{graph[0]};bool changed=true;
  while(changed){changed=false;foreach(var e in graph)if(!seen.Contains(e)&&seen.Any(q=>new[]{e.A,e.B}.Any(a=>new[]{q.A,q.B}.Any(b=>(a-b).Length<.03)))){seen.Add(e);changed=true;}}
  Check(seen.Count==graph.Count,"disconnected native graph "+string.Join(";",graph.Where(e=>!seen.Contains(e)).Select(JsonConvert.SerializeObject)));
  foreach(var e in graph.Where(e=>e.Use=="pedestrian"))for(double t=0;t<=1;t+=.05){var p0=p.Local(e.A+(e.B-e.A)*t);Check(!p.Parcels.Any(r=>(r.Kind==0||r.Kind==2||r.Kind==5)&&Layout.Overlap(r,new Parcel{X=p0.X-e.Width/2+.05,Y=p0.Y-e.Width/2+.05,W=e.Width-.1,D=e.Width-.1})),"walk hits occupied area "+JsonConvert.SerializeObject(e)+" local="+JsonConvert.SerializeObject(p0));}
 }
}
