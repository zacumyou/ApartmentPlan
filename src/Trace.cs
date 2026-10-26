using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
namespace ApartmentPlan {
internal static class Trace {
 static readonly List<string> recent=new List<string>();static int recentBytes;
 internal static string Recent(){lock(gate)return string.Join("\n",recent);}
 internal static string Redact(string value){var profile=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);if(string.IsNullOrEmpty(profile))return value;return value.Replace(profile,"<USERPROFILE>").Replace(profile.Replace("\\","/"),"<USERPROFILE>").Replace(profile.Replace("\\","\\\\"),"<USERPROFILE>");}
 static void Remember(string action,string line){
  if(action.StartsWith("overlay.",StringComparison.Ordinal)||action.StartsWith("snap.",StringComparison.Ordinal)||action=="input.blocked"||action=="binding.state"||action=="ui.focus")return;
  recent.Add(line);recentBytes+=line.Length;
  while(recent.Count>180||recentBytes>131072){recentBytes-=recent[0].Length;recent.RemoveAt(0);}
 }
 static readonly object gate=new object();static StreamWriter writer;static string directory,session;static int part,batchDepth;static bool stopped;
 static readonly Dictionary<string,Tuple<string,DateTime,int>> samples=new Dictionary<string,Tuple<string,DateTime,int>>();
 internal static void Start(){lock(gate){stopped=false;samples.Clear();recent.Clear();recentBytes=0;part=0;directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)+"Low","Colossal Order","Cities Skylines II","Logs","ApartmentPlan");session=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");try{Directory.CreateDirectory(directory);Open();}catch(Exception e){Mod.Log.Error(e,"APTRACE log.open.failed");}}Event("session.start","version="+typeof(Mod).Assembly.GetName().Version+" path="+directory);}
 internal static string WriteSnapshot(string name,object data){lock(gate){
  if(string.IsNullOrEmpty(directory))throw new InvalidOperationException("Trace directory unavailable");
  string path=Path.Combine(directory,name+"-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff")+".json");
  File.WriteAllText(path,Newtonsoft.Json.JsonConvert.SerializeObject(data,Newtonsoft.Json.Formatting.Indented),new UTF8Encoding(false));return path;
 }}
 static void Open(){writer=new StreamWriter(new FileStream(Path.Combine(directory,session+"-"+(part++)+".log"),FileMode.CreateNew,FileAccess.Write,FileShare.ReadWrite),new UTF8Encoding(false)){AutoFlush=batchDepth==0};}
 internal static IDisposable Batch(){lock(gate){batchDepth++;if(writer!=null)writer.AutoFlush=false;return new BatchScope();}}
 sealed class BatchScope:IDisposable {bool disposed;public void Dispose(){lock(gate){if(disposed)return;disposed=true;batchDepth=Math.Max(0,batchDepth-1);if(batchDepth==0&&writer!=null){try{writer.Flush();writer.AutoFlush=true;}catch(Exception e){Mod.Log.Error(e,"APTRACE log.flush.failed");}}}}}
 internal static void Event(string action,string detail=""){lock(gate){if(stopped)return;string line=DateTime.UtcNow.ToString("O")+" | "+action+" | "+Clean(detail,16000);Remember(action,line);try{if(writer!=null&&writer.BaseStream.Length>=8*1024*1024){writer.Dispose();Open();}if(writer!=null)writer.WriteLine(line);else Mod.Log.Info("APTRACE "+line);}catch(Exception e){try{writer?.Dispose();}catch{}writer=null;Mod.Log.Error(e,"APTRACE log.write.failed");}}}
 internal static string Clean(string value,int limit=512){value=(value??"").Replace("\r","\\r").Replace("\n","\\n");return value.Length>limit?value.Substring(0,limit)+"…":value;}
 // Continuous observations: log transitions immediately and identical states at most every 5 seconds.
 internal static void Sample(string action,string state){lock(gate){var now=DateTime.UtcNow;if(samples.TryGetValue(action,out var old)){if(old.Item1==state&&(now-old.Item2).TotalSeconds<5){samples[action]=Tuple.Create(state,old.Item2,old.Item3+1);return;}Event(action,state+" suppressed="+old.Item3);}else Event(action,state);samples[action]=Tuple.Create(state,now,0);}}
 internal static void Failure(string action,Exception e)=>Sample(action,e.ToString());
 internal static void UnityLog(string message,string stack,UnityEngine.LogType type){if((message??"").Contains("APTRACE"))return;if((message??"").Contains("ApartmentPlan")||(stack??"").Contains("ApartmentPlan"))Sample("unity."+type,Clean(message,4000)+" "+Clean(stack,10000));}
 internal static void Stop(){Event("session.end");lock(gate){foreach(var kv in samples)if(kv.Value.Item3>0)Event("sample.remaining",kv.Key+" suppressed="+kv.Value.Item3);stopped=true;writer?.Dispose();writer=null;samples.Clear();}}
}
}


