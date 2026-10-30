using System;
using System.IO;
using System.Text;
namespace ApartmentPlan {
// Local opt-in file only. Never routed to Trace.Recent or diagnostic sharing.
internal sealed class LocalPerformanceLog {
 internal static string Folder=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)+"Low","Colossal Order","Cities Skylines II","Logs","ApartmentPlan");
 bool enabled;double nextCheck,nextWrite;int samples;double cpuSum,cpuMax,frameSum,waitSum,labelSum;long tested,culled,lod,submitted;
 internal bool Enabled(double now){if(now>=nextCheck){nextCheck=now+5;try{enabled=File.Exists(Path.Combine(Folder,"enable-local-performance.flag"));}catch{enabled=false;}if(!enabled)Reset();}return enabled;}
 void Reset(){samples=0;cpuSum=cpuMax=frameSum=waitSum=labelSum=0;tested=culled=lod=submitted=0;}
 internal void Add(double now,double cpuMs,double frameMs,int testedCount,int culledCount,int lodCount,int submittedCount,double writerWaitMs,double labelsMs){
  waitSum+=writerWaitMs;labelSum+=labelsMs;samples++;cpuSum+=cpuMs;cpuMax=Math.Max(cpuMax,cpuMs);frameSum+=frameMs;tested+=testedCount;culled+=culledCount;submitted+=submittedCount;lod+=lodCount;
  if(now<nextWrite)return;nextWrite=now+5;
  try{Directory.CreateDirectory(Folder);var path=Path.Combine(Folder,"local-performance.jsonl");
   if(File.Exists(path)&&new FileInfo(path).Length>=4*1024*1024){var old=path+".previous";if(File.Exists(old))File.Delete(old);File.Move(path,old);}
   var row=new{utc=DateTime.UtcNow.ToString("O"),kst=DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(9)).ToString("O"),version=typeof(LocalPerformanceLog).Assembly.GetName().Version.ToString(),samples,overlayCpuMeanMs=cpuSum/samples,overlayCpuMaxMs=cpuMax,overlayWriterWaitMeanMs=waitSum/samples,labelCpuMeanMs=labelSum/samples,gameFrameMeanMs=frameSum/samples,boundsTestsMean=(double)tested/samples,culledMean=(double)culled/samples,lodSkippedMean=(double)lod/samples,submittedPrimitivesMean=(double)submitted/samples,gpuTimeAvailable=false,excludesNativePreviewAndWarnings=true};
   File.AppendAllText(path,Newtonsoft.Json.JsonConvert.SerializeObject(row)+Environment.NewLine,new UTF8Encoding(false));
  }catch{enabled=false;nextCheck=now+60;}finally{Reset();}
 }
}
}
