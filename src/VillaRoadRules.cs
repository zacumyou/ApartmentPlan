using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
public static class VillaRoadRules {
 static string Key(Point p)=>Math.Round(p.X*100)+","+Math.Round(p.Y*100);
 public static bool Through(List<ConstructionEdge> edges,List<Point> roots,List<Point> directions,double width){
  var nodes=new Dictionary<string,Point>();var adjacent=new Dictionary<string,List<ConstructionEdge>>();
  foreach(var e in edges.Where(e=>e.Use=="main"))foreach(var p in new[]{e.A,e.B}){string k=Key(p);nodes[k]=p;if(!adjacent.ContainsKey(k))adjacent[k]=new List<ConstructionEdge>();adjacent[k].Add(e);}
  List<ConstructionEdge> best=null;double score=double.MaxValue;
  for(int i=0;i<roots.Count;i++)for(int j=i+1;j<roots.Count;j++){
   if(SiteFrame.Dot(directions[i],directions[j])>.5||(roots[i]-roots[j]).Length<32)continue;
   string start=Key(roots[i]),end=Key(roots[j]);if(!nodes.ContainsKey(start)||!nodes.ContainsKey(end))continue;
   var distance=new Dictionary<string,double>{{start,0}};var previous=new Dictionary<string,ConstructionEdge>();var done=new HashSet<string>();
   while(true){var pending=distance.Where(k=>!done.Contains(k.Key)).OrderBy(k=>k.Value).FirstOrDefault();if(pending.Key==null)break;string at=pending.Key;if(at==end)break;done.Add(at);
    foreach(var e in adjacent[at]){string next=Key(e.A)==at?Key(e.B):Key(e.A);double cost=pending.Value+(e.B-e.A).Length*(e.Use=="main"?.7:1);if(!distance.TryGetValue(next,out var old)||cost<old){distance[next]=cost;previous[next]=e;}}
   }
   if(!distance.ContainsKey(end)||distance[end]>=score)continue;var path=new List<ConstructionEdge>();string current=end;
   while(current!=start){if(!previous.TryGetValue(current,out var edge)){path.Clear();break;}path.Add(edge);current=Key(edge.A)==current?Key(edge.B):Key(edge.A);}
   if(path.Count>0){best=path;score=distance[end];}
  }
  if(best==null)return false;foreach(var e in best){e.Use="main";e.Width=width;}return true;
 }
 public static List<List<Point>> EmptyBlocks(Plan plan,IList<ConstructionEdge> edges){
  var adjacent=new Dictionary<string,List<Point>>();var positions=new Dictionary<string,Point>();
  foreach(var e in edges)foreach(var pair in new[]{Tuple.Create(e.A,e.B),Tuple.Create(e.B,e.A)}){var k=Key(pair.Item1);positions[k]=pair.Item1;if(!adjacent.ContainsKey(k))adjacent[k]=new List<Point>();if(!adjacent[k].Any(q=>Key(q)==Key(pair.Item2)))adjacent[k].Add(pair.Item2);}
  foreach(var k in adjacent.Keys.ToArray()){var p=positions[k];adjacent[k]=adjacent[k].OrderBy(q=>Math.Atan2(q.Y-p.Y,q.X-p.X)).ToList();}
  var seen=new HashSet<string>();var result=new List<List<Point>>();var houses=plan.Parcels.Where(r=>r.Kind==0).Select(r=>plan.World(new Point(r.X+r.W/2,r.Y+r.D/2))).ToArray();
  foreach(var edge in edges)foreach(var pair in new[]{Tuple.Create(edge.A,edge.B),Tuple.Create(edge.B,edge.A)}){
   var a=pair.Item1;var b=pair.Item2;var face=new List<Point>();string start=Key(a)+"/"+Key(b);bool closed=false;
   for(int step=0;step<edges.Count*2+2;step++){
    string directed=Key(a)+"/"+Key(b);if(!seen.Add(directed)){closed=directed==start;break;}face.Add(a);
    var neighbours=adjacent[Key(b)];int reverse=neighbours.FindIndex(q=>Key(q)==Key(a));if(reverse<0)break;var next=neighbours[(reverse+neighbours.Count-1)%neighbours.Count];a=b;b=next;
   }
   if(closed&&face.Count>=3&&Layout.SignedArea(face)>1&&!houses.Any(h=>Layout.Inside(h,face)))result.Add(face);
  }return result;
 }
}
}
