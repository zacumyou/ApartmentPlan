using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
public static class VillaVariety {
 // Nearest house on either side of a frontage row, including gradual bends.
 public static List<int>[] Neighbours(IList<Parcel> lots){
  var result=Enumerable.Range(0,lots.Count).Select(_=>new List<int>()).ToArray();
  for(int i=0;i<lots.Count;i++)foreach(int sign in new[]{-1,1}){
   var a=lots[i];var f=a.PlacementForward??new Point(0,1);var side=new Point(f.Y,-f.X);int best=-1;double distance=double.MaxValue;
   for(int j=0;j<lots.Count;j++){if(i==j)continue;var b=lots[j];var g=b.PlacementForward??new Point(0,1);if(SiteFrame.Dot(f,g)<.7)continue;
    var delta=new Point(b.X+b.W/2-a.X-a.W/2,b.Y+b.D/2-a.Y-a.D/2);
    if(SiteFrame.Dot(delta,side)*sign<=0||Math.Abs(SiteFrame.Dot(delta,f))>Math.Min(a.D,b.D)*.6||delta.Length>(a.W+b.W)/2+10)continue;
    if(delta.Length<distance){distance=delta.Length;best=j;}
   }
   if(best>=0){if(!result[i].Contains(best))result[i].Add(best);if(!result[best].Contains(i))result[best].Add(i);}
  }return result;
 }
 public static void Assign(IList<Parcel> lots,IList<Asset> assets){
  var links=Neighbours(lots);var assigned=new HashSet<int>();var usage=new Dictionary<string,int>();
  while(assigned.Count<lots.Count){
   int i=Enumerable.Range(0,lots.Count).Where(k=>!assigned.Contains(k)).OrderByDescending(k=>links[k].Where(assigned.Contains).Select(j=>lots[j].AssetId).Distinct().Count()).ThenByDescending(k=>links[k].Count).ThenBy(k=>lots[k].X).ThenBy(k=>lots[k].Y).First();
   var lot=lots[i];var choices=assets.Where(a=>!VillaLayout.Excluded(a)&&Math.Abs(a.Width-lot.W)<.001&&Math.Abs(a.Depth-lot.D)<.001).GroupBy(a=>a.Id).Select(g=>g.First()).ToArray();
   // Never change dimensions/frontage merely to vary appearance. With too few
   // compatible choices, minimize repeats rather than dropping a building.
   var asset=choices.OrderBy(a=>links[i].Count(j=>assigned.Contains(j)&&lots[j].AssetId==a.Id)).ThenBy(a=>usage.TryGetValue(a.Id,out var count)?count:0).ThenBy(a=>a.Id,StringComparer.Ordinal).FirstOrDefault();
   if(asset!=null){lot.AssetId=asset.Id;lot.Homes=asset.Homes;lot.FloorArea=Math.Min(asset.Width*asset.Depth,asset.GeometryWidth>0&&asset.GeometryDepth>0?asset.GeometryWidth*asset.GeometryDepth:asset.Width*asset.Depth*.7)*Math.Max(1,Math.Floor(asset.Height/3));usage[asset.Id]=usage.TryGetValue(asset.Id,out var n)?n+1:1;}
   assigned.Add(i);
  }
 }
}
}
