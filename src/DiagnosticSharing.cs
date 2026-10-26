using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Game.SceneFlow;
using Game.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace ApartmentPlan {
public partial class PlanSystem {
 static readonly HashSet<string> SharedMetricKeys=new HashSet<string>{"DeleteObjects","ForeignObjects","MissingObjects","Errors","CarJunctions","Buildings","Frontage","Bays","ParkingLinks","PedestrianWarnings","Power","Water","Sewage","PowerDemand","PowerSupplied","WaterDemand","WaterSupplied","Trees","RoadCandidates","AisleCandidates","Edges"};
 const string ReportEndpoint="https://chosho.mycafe24.com/ap-report/submit.php";
 string diagnosticShareStatus="AP.ShareDiagnostics",pendingReport;
 bool diagnosticShareDialog,diagnosticShareDisposed;
 int diagnosticShareGeneration;
 Task<string> diagnosticShareTask;
 CancellationTokenSource diagnosticShareCancel;
 DateTime diagnosticShareNext;
 // Explicit field allowlist: never send clipboard reports, trace text, paths or save data.
 string CreateSharedDiagnostic(){
  var metrics=HasCurrentNativeReport?World.GetExistingSystemManaged<ConstructionTool>()?.Metrics:null;
  var counts=new Dictionary<string,int>{
   {"boundaryPoints",Boundary.Count},{"entrances",Entrances.Count},{"selectedAssets",Selected.Count},
   {"buildings",Draft?.Parcels.Count(p=>p.Kind==0)??0},{"parking",Draft?.Parcels.Sum(p=>p.ParkingSpaces)??0},
   {"accessFailures",Draft?.RequiredAccessFailures??0},{"parkingFailures",Draft?.ParkingAccessFailures??0}};
  return JsonConvert.SerializeObject(new{
   schema=1,consent="ap-diagnostics-v1",reportId=Guid.NewGuid().ToString("D"),
   modVersion=typeof(Mod).Assembly.GetName().Version.ToString(),
   gameVersion=SafeReportCode(UnityEngine.Application.version),status=SafeReportCode(Status),mode=SafeReportCode(Mode),
   layoutPending=LayoutPending,layoutOutdated=LayoutOutdated,buildingPattern=Mod.Options.BuildingPattern,
   counts,nativeMetrics=(metrics??new Dictionary<string,int>()).Where(p=>SharedMetricKeys.Contains(p.Key)).Take(64).ToDictionary(p=>p.Key,p=>p.Value)
  });
 }
 static string SafeReportCode(string value)=>value!=null&&Regex.IsMatch(value,@"\A[A-Za-z0-9][A-Za-z0-9_.-]{0,95}\z")?value:"unknown";
 void ConfirmDiagnosticSharing(){
  if(diagnosticShareDisposed||diagnosticShareDialog||diagnosticShareTask!=null)return;
  if(DateTime.UtcNow<diagnosticShareNext){diagnosticShareStatus="AP.ShareWait";Revision++;return;}
  pendingReport=CreateSharedDiagnostic();int generation=++diagnosticShareGeneration;
  diagnosticShareDialog=true;Revision++;
  try{GameManager.instance.userInterface.appBindings.ShowConfirmationDialog(new ConfirmationDialog("AP.ShareTitle","AP.ShareConsent","AP.ShareAgree","AP.ShareCancel"),answer=>{
   if(diagnosticShareDisposed||generation!=diagnosticShareGeneration)return;
   Request("shareDiagnosticsAnswer",generation+"|"+answer);
  });}catch{diagnosticShareDialog=false;pendingReport=null;diagnosticShareStatus="AP.ShareFailed";Revision++;}
 }
 void ApplyDiagnosticSharingAnswer(string value){
  var parts=value.Split('|');
  if(parts.Length!=2||!int.TryParse(parts[0],out int generation)||generation!=diagnosticShareGeneration||!diagnosticShareDialog)return;
  diagnosticShareDialog=false;var payload=pendingReport;pendingReport=null;
  if(parts[1]!="0"||payload==null){Revision++;return;}
  diagnosticShareNext=DateTime.UtcNow.AddSeconds(60);diagnosticShareStatus="AP.ShareSending";
  diagnosticShareCancel=new CancellationTokenSource();
  diagnosticShareTask=SendDiagnostic(payload,diagnosticShareCancel.Token);Revision++;
 }
 static async Task<string> SendDiagnostic(string payload,CancellationToken token){
  try{
   using(var handler=new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false,UseDefaultCredentials=false})
   using(var client=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(15),MaxResponseContentBufferSize=2048})
   using(var body=new StringContent(payload,Encoding.UTF8,"application/json")){
    using(var response=await client.PostAsync(ReportEndpoint,body,token).ConfigureAwait(false)){
     if((int)response.StatusCode==429)return "AP.ShareWait";
     if((int)response.StatusCode==404||(int)response.StatusCode==503)return "AP.ShareServerUnavailable";
     if(!response.IsSuccessStatusCode)return "AP.ShareFailed";
     var result=JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
     var sent=JObject.Parse(payload);
     return result.Value<bool?>("accepted")==true&&result.Value<string>("reportId")==sent.Value<string>("reportId")?"AP.ShareSent":"AP.ShareFailed";
    }
   }
  }catch{return "AP.ShareFailed";}
 }
 void PollDiagnosticSharing(){
  if(diagnosticShareTask==null||!diagnosticShareTask.IsCompleted)return;
  diagnosticShareStatus=diagnosticShareTask.Status==TaskStatus.RanToCompletion?diagnosticShareTask.Result:"AP.ShareFailed";
  diagnosticShareTask=null;diagnosticShareCancel?.Dispose();diagnosticShareCancel=null;Revision++;
 }
 protected override void OnDestroy(){
  diagnosticShareDisposed=true;diagnosticShareGeneration++;pendingReport=null;
  diagnosticShareCancel?.Cancel();diagnosticShareCancel?.Dispose();base.OnDestroy();
 }
}
}
