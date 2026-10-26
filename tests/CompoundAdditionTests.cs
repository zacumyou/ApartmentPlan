using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using ApartmentPlan;
static class CompoundAdditionTests {
 static void Check(bool yes,string why){if(!yes)throw new Exception(why);}
 static Plan Plan()=>new Plan{Right=new Point(1,0),Forward=new Point(0,1),Area=20000};
 static Parcel Lot(double x,double y)=>new Parcel{Kind=0,X=x,Y=y,W=20,D=40,Homes=50,FloorArea=2000};
 static ConstructionCourse Road(double a,double b)=>new ConstructionCourse{Use="aisle",Width=8,Curve=new PlanBezier{A=new Point(a,0),B=new Point(a+(b-a)/3,0),C=new Point(a+2*(b-a)/3,0),D=new Point(b,0)}};
 public static void Run(){
  var old=Plan();old.Parcels.Add(Lot(10,10));old.AdditionCourses=new List<ConstructionCourse>{Road(0,100)};
  var next=Plan();next.Parcels.AddRange(new[]{Lot(10,10),Lot(20,20),Lot(70,10),Lot(110,10)});next.AdditionCourses=new List<ConstructionCourse>{Road(100,0),Road(100,160)};
  CompoundAdditions.Filter(next,new[]{old});
  Check(next.Parcels.Count==2&&next.Homes==100,"existing or moved overlapping lot must not be added");
  Check(old.Parcels.Count==1&&old.Parcels[0].X==10,"original immutable");
  Check(next.AdditionCourses.Count==1&&next.AdditionCourses[0].Curve.A.X==100,"reversed existing road excluded");
  var rotated=Plan();rotated.Origin=new Point(50,0);rotated.Right=new Point(0,1);rotated.Forward=new Point(-1,0);Check(!CompoundAdditions.FreeLot(rotated,Lot(10,10),new[]{old}),"world-space rotation overlap");
  var applied=new PlanEditData{Draft=old,NativeCommitted=true,Pending=new PlanEditData{Draft=next,NativeCommitted=false},AppliedPlans=new List<Plan>{old}};
  var restored=PlanEditCodec.Decode(PlanEditCodec.Encode(applied));Check(restored.NativeCommitted&&restored.Pending.Draft.Parcels.Count==2&&restored.AppliedPlans.Count==1,"pending save roundtrip keeps applied state");
  var legacy=PlanEditCodec.Decode(System.Text.Encoding.UTF8.GetBytes("{\"Version\":1,\"NativeCommitted\":true}"));Check(legacy.Pending==null&&legacy.AppliedPlans==null,"old save optional fields");
  Directory.CreateDirectory("ApartmentPlan/docs");File.WriteAllText("ApartmentPlan/docs/compound-additions-example.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{existing=old.Parcels,added=next.Parcels}));
  Console.WriteLine("PASS compound additions: overlap, rotation, reversed road, original preservation, pending/legacy serialization");
 }
}
