using System;
using System.Collections.Generic;
using ApartmentPlan;
static class FrameWorkTests {
 public static void Run(){
  int units=0;bool disposed=false;double now=0;
  IEnumerable<int> Items(){try{for(int i=0;i<100;i++){units++;yield return 0;}}finally{disposed=true;}}
  using(var work=new FrameWork(Items(),()=>now)){
   if(work.Step()||units!=16)throw new Exception("unbounded frame units");
   work.Dispose();if(!disposed||!work.Step()||units!=16)throw new Exception("cancel resumed old work");
  }
  units=0;IEnumerable<int> Slow(){for(int i=0;i<10;i++){units++;now+=1;yield return 0;}}
  using(var work=new FrameWork(Slow(),()=>now))if(work.Step()||units!=2)throw new Exception("time budget ignored");
  int applied=0,waits=0;
  IEnumerable<int> Pipeline(){for(int i=0;i<8;i++){waits++;yield return 1;}for(int i=0;i<40;i++)yield return 0;applied++;}
  using(var work=new FrameWork(Pipeline(),()=>0)){
   for(int i=0;i<8;i++)if(work.Step()||waits!=i+1||applied!=0)throw new Exception("native settle frames collapsed");
   if(work.Step()||work.Step()||applied!=0||!work.Step()||applied!=1)throw new Exception("premature completion");
  }
  applied=0;using(var work=new FrameWork(Pipeline(),()=>0)){work.Step();work.Dispose();work.Step();}
  if(applied!=0)throw new Exception("cancelled work committed");
  Console.WriteLine("PASS cooperative work: unit/time limits, eight distinct settle frames, completion gating and cancellation. Not a live Unity timing test.");
 }
}
