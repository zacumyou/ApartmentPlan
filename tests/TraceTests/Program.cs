using System;using System.IO;using System.Text;using System.Reflection;
namespace UnityEngine {public enum LogType {Error}}
namespace ApartmentPlan {static class Mod {internal static Logger Log=new Logger();}class Logger {public void Error(Exception e,string m){throw new Exception(m,e);}public void Info(string m){}}}
class CountingStream:MemoryStream {public int Flushes;public override void Flush(){Flushes++;base.Flush();}}
class Program {
 static void Main(){var stream=new CountingStream();var writer=new StreamWriter(stream,new UTF8Encoding(false),4096,true){AutoFlush=true};typeof(ApartmentPlan.Trace).GetField("writer",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,writer);
 int before=stream.Flushes;for(int i=0;i<500;i++)ApartmentPlan.Trace.Event("test","row="+i);int immediate=stream.Flushes-before;
 stream.SetLength(0);before=stream.Flushes;
 try{using(ApartmentPlan.Trace.Batch()){for(int i=0;i<500;i++)ApartmentPlan.Trace.Event("test","row="+i);using(ApartmentPlan.Trace.Batch())ApartmentPlan.Trace.Event("test","nested");throw new InvalidOperationException("expected");}}catch(InvalidOperationException){}
 int batch=stream.Flushes-before;var text=Encoding.UTF8.GetString(stream.ToArray());if(text.Split('\n',StringSplitOptions.RemoveEmptyEntries).Length!=501||!text.Contains("row=499")||!text.Contains("nested")||!writer.AutoFlush||batch>=immediate)throw new Exception("batch lost logs or flush state");
 Console.WriteLine("PASS 501 lines retained; nested scope + exception flush; flush calls "+immediate+" -> "+batch);writer.Dispose();}
}