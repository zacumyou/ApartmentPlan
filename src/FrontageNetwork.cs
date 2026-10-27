using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
public static class FrontageNetwork {
 static string Key(Point p)=>Math.Round(p.X*100)+","+Math.Round(p.Y*100);
 public static int Missing(Plan plan,IEnumerable<Asset> assets,double nativeWidth,double margin,List<string> details=null,List<Parcel> rejected=null,List<Dictionary<string,int>> evidence=null){
  int ordinal=0;
  void Record(int reason,Parcel lot=null,double width=0,double coverage=0,int connectedCount=0,int edgeCount=0){
   if(evidence==null||evidence.Count>=24)return;
   evidence.Add(new Dictionary<string,int>{{"reason",reason},{"ordinal",ordinal},{"widthCm",(int)Math.Round(width*100)},{"depthCm",(int)Math.Round((lot?.D??0)*100)},{"coverageCm",(int)Math.Round(coverage*100)},{"roadWidthCm",(int)Math.Round(nativeWidth*100)},{"marginCm",(int)Math.Round(margin*100)},{"connected",connectedCount},{"edges",edgeCount}});
  }
  var graph=(plan.TowerAlgorithmVersion>=1?TowerConstruction.Create(plan).Select(c=>new ConstructionEdge{A=c.Curve.A,B=c.Curve.D,Use=c.Use,Width=c.Width}).ToList():ConstructionGraph.Create(plan)).Where(e=>e.Use!="pedestrian").ToList();
  if(graph.Count==0){Record(10);details?.Add("reason=no-vehicle-network");return plan.Parcels.Count(p=>p.Kind==0);}
  var incident=new Dictionary<string,List<ConstructionEdge>>();
  foreach(var e in graph)foreach(var p in new[]{e.A,e.B}){string k=Key(p);if(!incident.TryGetValue(k,out var list))incident[k]=list=new List<ConstructionEdge>();list.Add(e);}
  var root=graph.OrderBy(e=>Math.Min((e.A-plan.Origin).Length,(e.B-plan.Origin).Length)).First();
  var connected=new HashSet<ConstructionEdge>();var queue=new List<ConstructionEdge>();
  if(plan.TowerAlgorithmVersion>=1){
   // Every boundary entrance can root its own independent vehicle network.
   foreach(var edge in graph.Where(e=>e.Use=="main"))if(new[]{edge.A,edge.B}.Any(q=>TowerGeometry.Distance(q,plan.Boundary)<.1)&&connected.Add(edge))queue.Add(edge);
  }else{connected.Add(root);queue.Add(root);}
if(plan.Villa)foreach(var edge in graph)if(plan.VillaRoots.Any(q=>(q-edge.A).Length<.1||(q-edge.B).Length<.1)&&connected.Add(edge))queue.Add(edge);
  for(int i=0;i<queue.Count;i++)foreach(var p in new[]{queue[i].A,queue[i].B})foreach(var e in incident[Key(p)])if(connected.Add(e))queue.Add(e);
  var local=plan.Boundary.Select(plan.Local).ToArray();double cell=Math.Max(1,Math.Ceiling(Math.Max(local.Max(p=>p.X)-local.Min(p=>p.X),local.Max(p=>p.Y)-local.Min(p=>p.Y))/400));int missing=0;
  foreach(var lot in plan.Parcels.Where(p=>p.Kind==0)){
   ordinal++;var asset=assets.FirstOrDefault(a=>a.Id==lot.AssetId);if(asset==null){missing++;Record(11,lot);details?.Add("reason=asset-unavailable asset="+lot.AssetId);continue;}
   var f=BuildingPlacement.Forward(lot,asset);var side=new Point(f.Y,-f.X);var front=new Point(lot.X+lot.W/2,lot.Y+lot.D/2)+f*(asset.Depth/2);
   if((plan.Villa||plan.TowerAlgorithmVersion>=1)&&lot.PlacementOutline!=null){
    bool ok=true;foreach(double u in new[]{-asset.Width*.45,0,asset.Width*.45}){
     var target=plan.World(front+side*u);bool found=connected.Any(e=>{var dir=VillaGeometry.Unit(plan.Local(e.B)-plan.Local(e.A));if(Math.Abs(SiteFrame.Dot(dir,side))<.94)return false;var q=VillaGeometry.Nearest(target,e.A,e.B);var delta=plan.Local(q)-plan.Local(target);double gap=delta.Length-e.Width/2;return SiteFrame.Dot(delta,f)>0&&gap>=-.5&&gap<=7.9;});if(!found)ok=false;
    }if(!ok){missing++;Record(12,lot,asset.Width,0,connected.Count,graph.Count);rejected?.Add(lot);details?.Add("reason=villa-frontage-disconnected asset="+lot.AssetId+" lot=("+lot.X+","+lot.Y+","+lot.W+","+lot.D+")");}continue;
   }
   var spans=new List<Tuple<double,double>>();var allSpans=new List<Tuple<double,double>>();
   foreach(var e in graph.Where(e=>(e.Use=="aisle"||e.Use=="aisleWide"||plan.Villa&&e.Use=="main"))){
    var a=plan.Local(e.A)-front;var b=plan.Local(e.B)-front;if(Math.Abs(SiteFrame.Dot(b-a,f))>.01)continue;
    double gap=SiteFrame.Dot((a+b)*.5,f)-(e.Use=="aisleWide"||e.Use=="main"?e.Width:nativeWidth)/2;if(gap<-.05||gap>Math.Min(7.9,margin+cell)+.05)continue;
    double u=SiteFrame.Dot(a,side),v=SiteFrame.Dot(b,side);var span=Tuple.Create(Math.Min(u,v),Math.Max(u,v));allSpans.Add(span);if(connected.Contains(e))spans.Add(span);
   }
   double cursor=-asset.Width/2;foreach(var span in spans.OrderBy(t=>t.Item1))if(span.Item1<=cursor+.03)cursor=Math.Max(cursor,span.Item2);
   if(cursor<asset.Width/2-.03){
    missing++;double full=-asset.Width/2;foreach(var span in allSpans.OrderBy(t=>t.Item1))if(span.Item1<=full+.03)full=Math.Max(full,span.Item2);
    string reason=full>=asset.Width/2-.03?"disconnected-from-entrance":"frontage-coverage";
    Record(reason=="disconnected-from-entrance"?13:14,lot,asset.Width,Math.Max(0,cursor+asset.Width/2),connected.Count,graph.Count);
    details?.Add(FormattableString.Invariant($"reason={reason} asset={asset.Id} lot=({lot.X:F3},{lot.Y:F3},{lot.W:F3},{lot.D:F3}) forward=({f.X:F1},{f.Y:F1}) coverageEnd={cursor:F3} requiredEnd={asset.Width/2:F3} connectedEdges={connected.Count}/{graph.Count} spans=")+string.Join(";",spans.Select(s=>FormattableString.Invariant($"[{s.Item1:F3},{s.Item2:F3}]"))));
   }
  }
  return missing;
 }
}
}
