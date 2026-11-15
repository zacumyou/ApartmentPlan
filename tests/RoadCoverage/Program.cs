using System;
using System.Collections.Generic;
using ApartmentPlan;
using Colossal.Mathematics;
using Unity.Mathematics;
class Program {
 static Bezier4x3 Line(float x,float end,float z=0)=>new Bezier4x3(new float3(x,0,z),new float3(x+(end-x)/3,0,z),new float3(x+2*(end-x)/3,0,z),new float3(end,0,z));
 static void Check(bool b,string name){if(!b)throw new Exception(name);Console.WriteLine("PASS "+name);}
 static void Main(){
  var source=Line(0,100);var refit=source;refit.b.y=3;refit.c.y=-2;
  Check(!RoadCurveCoverage.Covers(source,new List<Bezier4x3>{refit},false),"3D coverage rejects native-style vertical refit");
  Check(RoadCurveCoverage.Covers(source,new List<Bezier4x3>{refit},true),"ground projection retains full horizontal road under vertical refit");
  Check(RoadCurveCoverage.Covers(source,new List<Bezier4x3>{Line(0,40),Line(40,100)},true),"continuous split road union");
  Check(!RoadCurveCoverage.Covers(source,new List<Bezier4x3>{Line(0,40),Line(42,100)},true),"missing middle blocked");
  Check(!RoadCurveCoverage.Covers(source,new List<Bezier4x3>{Line(2,100)},true),"missing endpoint blocked");
  Check(!RoadCurveCoverage.Covers(source,new List<Bezier4x3>{Line(0,100,1)},true),"nearby parallel road blocked");
  Check(RoadCurveCoverage.Covers(source,new List<Bezier4x3>{Line(100,0)},true),"reversed survivor");
 }
}
