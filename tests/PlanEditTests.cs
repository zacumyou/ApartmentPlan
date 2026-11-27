using System;
using System.Linq;
using System.Collections.Generic;
using ApartmentPlan;
static class PlanEditTests {
 static void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);}
 public static void Run(){
  var boundary=new[]{new Point(0,0),new Point(220,0),new Point(220,300),new Point(0,300)};
  var entries=new[]{new Point(80,0),new Point(150,300)};
  var assets=new[]{new Asset{Id="도안자이 시험",Width=40,Depth=16,Height=45,Homes=80}};
  var options=new LayoutOptions{BayWidth=3,MainCurveRadius=80};
  var zones=new List<ManualZone>{new ManualZone{Kind=5,Points=new List<Point>{new Point(5,110),new Point(25,110),new Point(25,130),new Point(5,130)}}};
  var plan=Layout.Generate(boundary,entries,assets,options,null,zones);
  var data=new PlanEditData{Boundary=boundary.ToList(),Entrances=entries.ToList(),Zones=zones,Ramps=new List<Parcel>(),Draft=plan,Elevation=123.5f,Selected=new[]{assets[0].Id},NativeCommitted=true,Settings=new Dictionary<string,object>{{"MainRoadCurveRadius",80},{"ParkingDecalGap",.5f},{"MainNetwork","Small Road"}}};
  var encoded=PlanEditCodec.Encode(data);var restored=PlanEditCodec.Decode(encoded);
  Check(restored.Version==1&&restored.Selected[0]==assets[0].Id,"edit codec preserves Unicode prefab identity");
  Check(restored.Boundary.Count==4&&restored.Entrances[1].X==150&&restored.Zones[0].Kind==5,"edit codec preserves boundary offset entrances and subtract zone");
  Check(restored.Elevation==123.5f&&restored.NativeCommitted,"edit codec preserves elevation and duplicate-construction guard");
  Check(restored.Draft.Parcels.Count==plan.Parcels.Count&&restored.Draft.Parcels.Sum(r=>r.ParkingSpaces)==plan.Parcels.Sum(r=>r.ParkingSpaces),"edit codec preserves exact layout and parking counts");
  Check(restored.Draft.Parcels.Where(r=>r.Kind==0).All(r=>r.Frontage!=null),"edit codec preserves per-building frontage for replacements");
  Check(restored.Draft.Routes.Sum(r=>r.Points.Count)==plan.Routes.Sum(r=>r.Points.Count),"edit codec preserves road routes");
  Check(Convert.ToInt32(restored.Settings["MainRoadCurveRadius"])==80&&Convert.ToDouble(restored.Settings["ParkingDecalGap"])==.5,"edit codec preserves plan settings");
  var wire=new Codec();foreach(var value in encoded)new PlanEditByte{Value=value}.Serialize(wire);wire.Reset();var bytes=new byte[encoded.Length];for(int i=0;i<bytes.Length;i++){var item=new PlanEditByte();item.Deserialize(wire);bytes[i]=(byte)item.Value;}
  Check(bytes.SequenceEqual(encoded)&&PlanEditCodec.Decode(bytes).Draft.Homes==plan.Homes,"edit companion buffer roundtrip through production serializers");
  var archive=PlanArchive.Encode(data,encoded);
  var header=PlanArchive.Header(i=>archive[i],archive.Length);
  Check(header.NativeCommitted&&header.Exclusions.Count==1&&header.Parking==plan.Parcels.Sum(r=>r.ParkingSpaces),"archive header selection, ownership and parking");
  Check(PlanEditCodec.Encode(restored).SequenceEqual(PlanEditCodec.Encode(PlanArchive.Read(i=>archive[i],archive.Length))),"lossless legacy to gzip roundtrip");
  var damaged=(int[])archive.Clone();damaged[3]=PlanArchive.Limit+1;
  try{PlanArchive.Read(i=>damaged[i],damaged.Length);throw new Exception("Corrupt size accepted");}catch(System.IO.InvalidDataException){}
  var compound=PlanEditCodec.Decode(encoded);compound.Pending=restored;compound.AppliedPlans=new List<Plan>{plan,restored.Draft};
  var compoundWords=PlanArchive.Encode(compound,PlanEditCodec.Encode(compound));
  var compoundHeader=PlanArchive.Header(i=>compoundWords[i],compoundWords.Length);
  Check(compoundHeader.Pending&&compoundHeader.Buildings==2*plan.Parcels.Count(q=>q.Kind==0)&&
   PlanArchive.Read(i=>compoundWords[i],compoundWords.Length).Pending.NativeCommitted,"compound history and pending construction survive migration");
  var ids=Enumerable.Range(1,256).ToArray();
  Check(PlanDeletionPolicy.OldestForNewPlan(ids.Take(255),256,i=>i,i=>true).Count==0&&
   PlanDeletionPolicy.OldestForNewPlan(ids,256,i=>i,i=>i>5).SequenceEqual(new[]{6})&&
   PlanDeletionPolicy.OldestForNewPlan(ids,256,i=>i,i=>false).Count==0,"256th slot available; 257th honors protected plans");
  var grid=new PlanPickGrid();
  for(int y=0;y<16;y++)for(int x=0;x<16;x++)grid.Add(y*16+x,new Parcel{X=(x-8)*256+10,Y=(y-8)*256+10,W=100,D=100});
  for(int y=0;y<16;y++)for(int x=0;x<16;x++){
   var cursor=new Point((x-8)*256+50,(y-8)*256+50);
   CheckPick(grid.Query(cursor,cursor),y*16+x);
  }
  var diagonal=grid.Query(new Point(-2048,-2048),new Point(2048,2048)).ToArray();
  Check(diagonal.SequenceEqual(Enumerable.Range(0,16).Select(i=>i*17)),"256 plots: vertical ray checks 1, diagonal ray checks 16, original order preserved");
  Check(grid.Query(new Point(2048,2048),new Point(-2048,-2048)).SequenceEqual(diagonal),"reverse ray preserves candidates");
  Check(PlanLimits.SavedPlans==256,"visible saved plan limit 256");
  if(Environment.GetEnvironmentVariable("AP_ARCHIVE_BENCH")=="1")Benchmark(data);
  Check(encoded.Length<2000000,"representative edit payload fits budget: "+encoded.Length);
  Check(plan.Parcels.Where(r=>r.Kind==2).All(r=>(r.ParkingAlongX?r.W:r.D)/r.ParkingSpaces>=3-.001),"3 metre pitch leaves half metre beyond 2.5 metre decals");
  var broad=Layout.Generate(boundary,new[]{new Point(110,0),new Point(220,170)},assets,new LayoutOptions{MainCurveRadius=100,MainRoadCornerMode=1},null,null);
  Check(broad.Routes.SelectMany(r=>r.CornerRadii).Any(r=>r>40),"long single corner uses radius above old 40 metre limit");
  Codec.Test();SnapTests.Run();
 }
 static void CheckPick(List<int> found,int expected){
  if(found.Count!=1||found[0]!=expected)throw new Exception("Spatial index lost or added a plan");
 }
 static void Benchmark(PlanEditData sample){
  const int count=256;
  var old=new int[count][];var packed=new int[count][];
  for(int i=0;i<count;i++){
   sample.Name="설계도 "+i;var bytes=PlanEditCodec.Encode(sample);
   old[i]=Array.ConvertAll(bytes,b=>(int)b);packed[i]=PlanArchive.Encode(sample,bytes);
  }
  Console.WriteLine($"256 edit buffers old={old.Sum(w=>w.Length*4L)} new={packed.Sum(w=>w.Length*4L)} bytes");
  for(int trial=0;trial<3;trial++)foreach(bool lazy in new[]{false,true}){
   GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
   var live=new object[count];long start=GC.GetAllocatedBytesForCurrentThread();
   var timer=System.Diagnostics.Stopwatch.StartNew();
   for(int i=0;i<count;i++){
    var words=packed[i];
    if(lazy)live[i]=PlanArchive.Header(j=>words[j],words.Length);
    else live[i]=PlanEditCodec.Decode(Array.ConvertAll(old[i],v=>(byte)v));
   }
   timer.Stop();long alloc=GC.GetAllocatedBytesForCurrentThread()-start;
   GC.KeepAlive(live);
   Console.WriteLine($"restore {(lazy?"headers":"legacy")} {timer.Elapsed.TotalMilliseconds:F2} ms allocated={alloc}");
  }
  var migration=System.Diagnostics.Stopwatch.StartNew();
  long migrated=0;
  foreach(var legacy in old){
   var bytes=Array.ConvertAll(legacy,v=>(byte)v);var d=PlanEditCodec.Decode(bytes);
   migrated+=PlanArchive.Encode(d,bytes).Length*4L;
  }
  migration.Stop();Console.WriteLine($"legacy migration {migration.Elapsed.TotalMilliseconds:F2} ms new={migrated}");
  long OuterCompressed(int[][] buffers){
   using var output=new System.IO.MemoryStream();
   using(var stream=new System.IO.Compression.GZipStream(output,System.IO.Compression.CompressionLevel.Optimal,true)){
    foreach(var buffer in buffers){
     var bytes=new byte[buffer.Length*4];Buffer.BlockCopy(buffer,0,bytes,0,bytes.Length);
     stream.Write(bytes,0,bytes.Length);
    }
   }
   return output.Length;
  }
  Console.WriteLine($"same outer gzip simulation old={OuterCompressed(old)} new={OuterCompressed(packed)} bytes");
  var first=packed[0];var watch=System.Diagnostics.Stopwatch.StartNew();
  var edit=PlanArchive.Read(j=>first[j],first.Length);watch.Stop();
  Check(edit.Name=="설계도 0","read one deferred plan");Console.WriteLine($"first detail {watch.Elapsed.TotalMilliseconds:F2} ms");
 }

}
