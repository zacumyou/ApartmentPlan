using System;
using System.Collections.Generic;
using System.Linq;
using ApartmentPlan;
static class NetworkReplacementTests {
 public static void Run(){
  FrameWorkTests.Run();
  var through=TowerGeometry.Line(new Point(-20,0),new Point(20,0));
  if(!TowerFrontJoins.HasJunction(new Point(0,0),new[]{through})||TowerFrontJoins.HasJunction(new Point(0,5),new[]{through}))throw new Exception("interior T junction fillet protection");
  Console.WriteLine("PASS through-road interior blocks corner rounding; isolated bend remains eligible.");
  if(!CompoundRoadPolicy.DeleteOwnedInvisible(true,true)||CompoundRoadPolicy.DeleteOwnedInvisible(false,true)||CompoundRoadPolicy.DeleteOwnedInvisible(true,false))throw new Exception("owned AP demolition scope");
  if(!NetworkReplacementPolicy.CoversIntervals(new List<Tuple<double,double>>{Tuple.Create(.4,1d),Tuple.Create(0d,.4)},.001))throw new Exception("split neighbour union rejected");
  if(NetworkReplacementPolicy.CoversIntervals(new List<Tuple<double,double>>{Tuple.Create(0d,.4),Tuple.Create(.45,1d)},.001))throw new Exception("missing road interval allowed");
  if(NetworkReplacementPolicy.CoversIntervals(new List<Tuple<double,double>>{Tuple.Create(.1,1d)},.001))throw new Exception("missing start allowed");
  Console.WriteLine("PASS split road coverage accepts continuous pieces and rejects missing spans.");
  var plan=new Plan{Right=new Point(1,0),Forward=new Point(0,1)};
  plan.Routes.Add(TowerGeometry.Route(new[]{TowerGeometry.Line(new Point(0,0),new Point(20,0))},"aisle",8));
  plan.Routes.Add(TowerGeometry.Route(new[]{TowerGeometry.Line(new Point(20,.0004),new Point(0,.0004))},"aisle",8));
  plan.Routes.Add(TowerGeometry.Route(new[]{TowerGeometry.Line(new Point(10,0),new Point(10,10))},"aisle",8));
  var graph=TowerConstruction.Create(plan);
  System.IO.File.WriteAllText("ApartmentPlan/docs/t-junction-048.svg","<svg xmlns='http://www.w3.org/2000/svg' width='600' height='360' viewBox='-5 -5 30 18'><rect x='-5' y='-5' width='30' height='18' fill='#eef1e8'/>"+string.Join("",graph.Select(c=>"<path d='M "+c.Curve.A.X.ToString(System.Globalization.CultureInfo.InvariantCulture)+" "+c.Curve.A.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)+" L "+c.Curve.D.X.ToString(System.Globalization.CultureInfo.InvariantCulture)+" "+c.Curve.D.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)+"' stroke='#879297' stroke-width='2' fill='none'/>"))+"<circle cx='10' cy='0' r='.3' fill='#007f89'/></svg>");
  if(graph.Count!=3||graph.Count(c=>(c.Curve.A-new Point(10,0)).Length<.00001||(c.Curve.D-new Point(10,0)).Length<.00001)!=3)throw new Exception("tower duplicate/T junction normalization");
  Console.WriteLine("PASS tower reversed sub-mm duplicates removed and T junction shares exact endpoint.");
  for(int mask=0;mask<16;mask++){bool n=(mask&1)!=0,o=(mask&2)!=0,j=(mask&4)!=0,c=(mask&8)!=0;if(NetworkReplacementPolicy.VerifiedSurvivor(n,o,j,c)!=(j&&(n||o&&c)))throw new Exception("survivor identity/coverage regression");}
  var retainedRoots=new HashSet<int>();NetworkReplacementPolicy.ExpandMerges(retainedRoots,new List<NetworkReplacementPolicy.Merge<int>>{new NetworkReplacementPolicy.Merge<int>{Removed=1603281,Joint=199359,Neighbour=42,Survivor=42,Verified=true,NewSurvivorVerified=NetworkReplacementPolicy.VerifiedSurvivor(false,true,true,true)}},new HashSet<int>{42});
  if(!retainedRoots.Contains(1603281)||!retainedRoots.Contains(199359))throw new Exception("existing survivor without Combine blocked");
  for(int mask=0;mask<8;mask++)if(NetworkReplacementPolicy.RetainedNeighbour((mask&1)!=0,(mask&2)!=0,(mask&4)!=0)!=(mask==7))throw new Exception("retained neighbour protection");

  // A joint-only native reduction has no removed-edge candidate: the old path
  // leaves it blocked even though both roads survive and a single curve bridges it.
  var jointOnly=new HashSet<int>();NetworkReplacementPolicy.ExpandMerges(jointOnly,new List<NetworkReplacementPolicy.Merge<int>>());
  if(jointOnly.Contains(77))throw new Exception("old joint-only reproduction invalid");
  for(int mask=0;mask<16;mask++)if(NetworkReplacementPolicy.RetainedJoint((mask&1)!=0,(mask&2)!=0,(mask&4)!=0,(mask&8)!=0)!=(mask==15))throw new Exception("joint-only protection predicate");
  if(NetworkReplacementPolicy.RetainedJoint(true,true,true,true))jointOnly.Add(77);
  if(!jointOnly.Contains(77))throw new Exception("verified joint-only reduction blocked");
  Console.WriteLine("PASS joint-only reduction requires degree two, scope, full preservation and a bridging survivor; all 15 incomplete cases blocked.");
  var roots=new HashSet<int>{100,101};var owners=new Dictionary<int,int>();
  for(int i=1;i<=21;i++)owners[i]=100;owners[22]=101;
  owners[30]=200;owners[31]=100;owners[32]=31;owners[40]=41;owners[41]=40;
  Func<int,bool> allowed=e=>NetworkReplacementPolicy.Allows(e,roots,x=>x>0,x=>x==31,x=>owners.TryGetValue(x,out int p)?p:0);
  for(int i=1;i<=22;i++)if(!allowed(i))throw new Exception("road child incorrectly blocked "+i);
  foreach(int e in new[]{0,30,31,32,40,200})if(allowed(e))throw new Exception("unsafe deletion permitted "+e);
  roots.Remove(101);if(allowed(22))throw new Exception("unreplaced node allowed");
  // Reproduce the runtime topology: the removed road is not a CoursePos root,
  // and its middle node disappears rather than getting a same-position replacement.
  roots=new HashSet<int>{479695};owners.Clear();for(int i=1;i<=21;i++)owners[i]=479692;
  if(allowed(1))throw new Exception("old root filter reproduction failed");
  var candidates=new List<NetworkReplacementPolicy.Merge<int>>{
   new NetworkReplacementPolicy.Merge<int>{Removed=479692,Joint=482184,Neighbour=479695,Survivor=479695,Verified=true},
   new NetworkReplacementPolicy.Merge<int>{Removed=600,Joint=601,Neighbour=700,Survivor=700,Verified=true},
   new NetworkReplacementPolicy.Merge<int>{Removed=800,Joint=801,Neighbour=479695,Survivor=479695,Verified=false}};
  NetworkReplacementPolicy.ExpandMerges(roots,candidates);
  for(int i=1;i<=21;i++)if(!allowed(i))throw new Exception("merged road child blocked "+i);
  if(!allowed(479692)||!allowed(482184))throw new Exception("native merged edge or removed joint blocked");
  if(allowed(600)||allowed(800))throw new Exception("unrelated or uncovered road allowed");
  // The current report has two deleted joints around a split central road.
  // CourseSplit rewrites the original definition to its first piece, so its
  // last connection must be recovered from the submission / split descendants.
  roots=new HashSet<int>{541540};owners.Clear();owners[1]=541594;owners[2]=541575;
  var splitMerges=new List<NetworkReplacementPolicy.Merge<int>>{
   new NetworkReplacementPolicy.Merge<int>{Removed=541594,Joint=1229543,Neighbour=541540,Survivor=0,Verified=true,NewSurvivorVerified=true},
   new NetworkReplacementPolicy.Merge<int>{Removed=541575,Joint=1229544,Neighbour=541540,Survivor=0,Verified=true,NewSurvivorVerified=true},
   new NetworkReplacementPolicy.Merge<int>{Removed=900,Joint=901,Neighbour=902,Survivor=0,Verified=true,NewSurvivorVerified=true},
   new NetworkReplacementPolicy.Merge<int>{Removed=910,Joint=911,Neighbour=541540,Survivor=0,Verified=true},
   new NetworkReplacementPolicy.Merge<int>{Removed=920,Joint=921,Neighbour=541540,Survivor=0,Verified=false,NewSurvivorVerified=true}};
  NetworkReplacementPolicy.ExpandMerges(roots,splitMerges);
  foreach(int e in new[]{1,2,541594,541575,1229543,1229544})if(!allowed(e))throw new Exception("verified new split survivor blocked "+e);
  foreach(int e in new[]{0,900,901,910,911,920,921})if(allowed(e))throw new Exception("unproved split survivor allowed "+e);
  roots.Clear();NetworkReplacementPolicy.ExpandMerges(roots,splitMerges);if(roots.Count!=0)throw new Exception("unscoped split seeded roots");
  // 2026-10-19 13:53/13:54 runtime: touched=3 but roots=0. The earlier test
  // incorrectly pre-seeded the neighbour as a replacement, hiding this failure.
  var anchors=new HashSet<int>{674294};
  var noOriginalMerges=new List<NetworkReplacementPolicy.Merge<int>>{
   new NetworkReplacementPolicy.Merge<int>{Removed=674330,Joint=404449,Neighbour=674294,Survivor=0,Verified=true,NewSurvivorVerified=true},
   new NetworkReplacementPolicy.Merge<int>{Removed=674246,Joint=404448,Neighbour=674294,Survivor=0,Verified=true,NewSurvivorVerified=true},
   new NetworkReplacementPolicy.Merge<int>{Removed=600,Joint=601,Neighbour=700,Survivor=0,Verified=true,NewSurvivorVerified=true},
   new NetworkReplacementPolicy.Merge<int>{Removed=610,Joint=611,Neighbour=674294,Survivor=0,Verified=true},
   new NetworkReplacementPolicy.Merge<int>{Removed=620,Joint=621,Neighbour=674294,Survivor=0,NewSurvivorVerified=true}};
  NetworkReplacementPolicy.ExpandMerges(roots,noOriginalMerges,anchors);
  foreach(int e in new[]{674330,674246,404449,404448})if(!roots.Contains(e))throw new Exception("zero-original split merge blocked "+e);
  foreach(int e in new[]{674294,600,601,610,611,620,621})if(roots.Contains(e))throw new Exception("anchor or unverified deletion permitted "+e);
  Console.WriteLine("PASS initially EMPTY replacement roots + captured connection anchor: logged two merges allowed; anchor itself, foreign neighbour and incomplete evidence blocked.");
  roots.Clear();var committed=new HashSet<int>{471592,471314};
  if(!NetworkReplacementPolicy.SeedRetainedRoad(roots,committed,471592,true))throw new Exception("retained stage road lost");
  if(NetworkReplacementPolicy.SeedRetainedRoad(roots,committed,471314,false)||NetworkReplacementPolicy.SeedRetainedRoad(roots,committed,999,true))throw new Exception("stage scope authorized an absent or foreign road");
  NetworkReplacementPolicy.ExpandMerges(roots,new List<NetworkReplacementPolicy.Merge<int>>{new NetworkReplacementPolicy.Merge<int>{Removed=471314,Joint=470549,Neighbour=471592,Survivor=471592,Verified=true}},committed);
  if(!roots.Contains(471314)||!roots.Contains(470549)||roots.Contains(999))throw new Exception("stage retained-road merge failed");
  Console.WriteLine("PASS stage transition: retained committed road seeds verified merge; missing replacement and foreign roads stay blocked.");
  var spatial=new SpatialLookup<int>(32);var boxes=new List<(double x,double z)>();
  for(int i=0;i<10000;i++){double x=(i%100-50)*40,z=(i/100-50)*40;boxes.Add((x,z));spatial.Add(i,x-1.5,z-1.5,x+12,z+20);}
  var random=new Random(19);int examined=0;
  for(int i=0;i<400;i++){
   double x=random.NextDouble()*4000-2000,z=random.NextDouble()*4000-2000;var near=spatial.At(x,z).ToHashSet();examined+=near.Count;
   for(int j=0;j<boxes.Count;j++){var b=boxes[j];if(x>=b.x-1.5&&x<=b.x+12&&z>=b.z-1.5&&z<=b.z+20&&!near.Contains(j))throw new Exception("spatial broad phase lost a valid candidate");}
  }
  spatial.Add(10001,-100000,-100000,100000,100000);if(!spatial.At(-.1,-.1).Contains(10001))throw new Exception("large hull fallback lost");
  if(examined>=400*10000/10)throw new Exception("spatial pruning ineffective");
  Console.WriteLine("PASS spatial broad phase: 400 endpoints / 10000 road bounds, candidates="+examined+" instead of 4000000; no lost exact-bound candidates (CPU fixture, not game FPS).");
  if(!NetworkReplacementPolicy.MatchesSplitIdentity(12345,12347,true)||NetworkReplacementPolicy.MatchesSplitIdentity(12345,12344,true)||NetworkReplacementPolicy.MatchesSplitIdentity(12345,20264,true)||NetworkReplacementPolicy.MatchesSplitIdentity(12345,12345,false))throw new Exception("split identity isolation failed");
  Console.WriteLine("PASS new split survivor covers removed road and scoped neighbour; foreign, missing joint evidence, failed coverage, and empty scope remain blocked. Native lifecycle inspected, runtime preview not executed.");
  Console.WriteLine("PASS runtime-topology regression: previously blocked23 originals accepted only with scoped, verified native merge; unrelated and uncovered roads still blocked. Fixture, not live game.");
  Console.WriteLine("PASS scoped road children allowed; foreign objects, buildings and their children, missing roots, owner cycles and unreplaced nodes blocked. Synthetic ownership fixture, not game preview.");
 }
}
