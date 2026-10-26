using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
public sealed class JunctionReserve {public Point Center;public double Radius;}
public static class MainJunctionClearance {
 public static List<JunctionReserve> Zones(Plan plan){
  var result=new List<JunctionReserve>();
  var main=ConstructionGraph.Create(plan,false).Where(e=>e.Use=="main").ToList();
  var curves=plan.Routes.Concat(plan.ConstructionRoutes).Where(r=>r.Use=="main").SelectMany(r=>(r.Curves??new List<PlanBezier>()).Select(c=>new{Curve=c,Width=r.Width})).ToArray();
  foreach(var group in main.SelectMany(e=>new[]{e.A,e.B}).GroupBy(p=>Math.Round(p.X*100)+","+Math.Round(p.Y*100))){
   var p=group.First();var edges=main.Where(e=>(e.A-p).Length<.03||(e.B-p).Length<.03).ToArray();
   // Curve samples are not real junctions; short curves are reserved below.
   if(edges.Length==2){
    if(curves.Any(c=>{var q=plan.Local(p);return(c.Curve.At(c.Curve.Parameter(q))-q).Length<.4;}))continue;
    var u=(edges[0].A-p).Length<.03?edges[0].B-p:edges[0].A-p;
    var v=(edges[1].A-p).Length<.03?edges[1].B-p:edges[1].A-p;
    if(SiteFrame.Dot(u,v)/(u.Length*v.Length)<-.98)continue;
   }
   if(edges.Length<2)continue;
   result.Add(new JunctionReserve{Center=p,Radius=Math.Max(8,edges.Max(e=>e.Width))});
  }
  foreach(var item in curves){double length=0;for(int i=1;i<=16;i++)length+=(item.Curve.At(i/16.0)-item.Curve.At((i-1)/16.0)).Length;
   if(length>Math.Max(24,item.Width*3))continue;
   for(int i=0;i<=16;i++)result.Add(new JunctionReserve{Center=plan.World(item.Curve.At(i/16.0)),Radius=item.Width/2+6});
  }
  return result;
 }
}
}
