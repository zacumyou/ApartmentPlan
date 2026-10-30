using System;
using System.Collections.Generic;
using Game.Rendering;
using Game.SceneFlow;
using UnityEngine;
namespace ApartmentPlan {
public partial class PlanOverlay {
 internal sealed class NameLabel {public string text;public float x,y,size;public int index,token;public bool editable;}
 readonly List<NameLabel> labels=new List<NameLabel>();
 readonly Dictionary<string,Asset> labelAssets=new Dictionary<string,Asset>(StringComparer.Ordinal);
 readonly Dictionary<string,string> labelNames=new Dictionary<string,string>(StringComparer.Ordinal);
 readonly List<Rect> labelRects=new List<Rect>();
 int labelCatalogRevision=-1,labelToken;Plan labelDraft;float nextLabelUpdate;bool updateLabels;
 internal string LabelJson {get;private set;}="[]";
 void BeginLabels(){updateLabels=UnityEngine.Time.unscaledTime>=nextLabelUpdate;if(updateLabels){nextLabelUpdate=UnityEngine.Time.unscaledTime+1f/30;labels.Clear();labelRects.Clear();}}
 void EndLabels(){if(measuring)submittedCount+=labels.Count;if(updateLabels){var json=Newtonsoft.Json.JsonConvert.SerializeObject(labels);if(json!=LabelJson)LabelJson=json;}}
 protected override void OnDestroy(){ClearLabels();base.OnDestroy();}
 internal void ClearLabels(){labels.Clear();labelNames.Clear();labelDraft=null;labelToken++;LabelJson="[]";}
 internal bool PickLabel(string value,Plan current,out Point point){
  point=default;var parts=(value??"").Split('|');
  if(current==null||current!=labelDraft||parts.Length!=2||!int.TryParse(parts[0],out int token)||token!=labelToken||!int.TryParse(parts[1],out int index)||index<0||index>=current.Parcels.Count)return false;
  var lot=current.Parcels[index];if(lot.Kind!=0)return false;
  point=current.World(new Point(lot.X+lot.W*.5,lot.Y+lot.D*.5));return true;
 }
 void DrawLabels(OverlayRenderSystem overlay,PlanSystem s,Plan plan,Camera camera,float opacity){
  if(!updateLabels)return;
  if(!measuring){DrawLabelsCore(s,plan,camera,opacity);return;}
  long start=System.Diagnostics.Stopwatch.GetTimestamp();try{DrawLabelsCore(s,plan,camera,opacity);}finally{labelCpuMs+=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency;}
 }
 void DrawLabelsCore(PlanSystem s,Plan plan,Camera camera,float opacity){
  if(camera==null||opacity<=0||plan==null)return;
  if(labelDraft!=s.Draft){labelDraft=s.Draft;labelToken++;}
  if(labelCatalogRevision!=s.CatalogRevision){labelCatalogRevision=s.CatalogRevision;labelAssets.Clear();labelNames.Clear();foreach(var asset in s.Assets)labelAssets[asset.Id]=asset;}
  var terrain=World.GetOrCreateSystemManaged<Game.Simulation.TerrainSystem>().GetHeightData();
  for(int index=0;index<plan.Parcels.Count;index++){
   if(labels.Count>=96)break;var lot=plan.Parcels[index];
   if(lot.Kind!=0||!labelAssets.TryGetValue(lot.AssetId??"",out var asset))continue;
   var point=plan.World(new Point(lot.X+lot.W*.5,lot.Y+lot.D*.5));
   var position=new Unity.Mathematics.float3((float)point.X,s.Elevation,(float)point.Y);
   if(!InView(position,position,(float)Math.Max(lot.W,lot.D),Mod.Options.ViewDistance)||!DetailVisible((float)Math.Min(lot.W,lot.D),position,20))continue;
   position.y=Game.Simulation.TerrainUtils.SampleHeight(ref terrain,position)+.35f;
   var screen=camera.WorldToScreenPoint(position);if(screen.z<=0||screen.x<0||screen.x>Screen.width||screen.y<0||screen.y>Screen.height)continue;
   string name=asset.Name;
   if(!string.IsNullOrEmpty(asset.NameKey)&&GameManager.instance.localizationManager.activeDictionary.TryGetValue(asset.NameKey,out var localized)&&!string.IsNullOrWhiteSpace(localized))name=localized;
   if(string.IsNullOrWhiteSpace(name))continue;
   if(!labelNames.TryGetValue(name,out var wrapped)){wrapped=PlanLabelText.Wrap(name);if(labelNames.Count>=128)labelNames.Clear();labelNames[name]=wrapped;}
   float size=Mathf.Clamp(Screen.height/60f,16,24)*.85f,width=0;var lines=wrapped.Split('\n');
   foreach(var line in lines){float em=0;foreach(char c in line)em+=c<128?.58f:1;width=Mathf.Max(width,em*size);}
   var rect=new Rect(screen.x-width/2-8,screen.y-lines.Length*size*.525f-5,width+16,lines.Length*size*1.05f+10);
   bool overlap=false;foreach(var used in labelRects)if(used.Overlaps(rect)){overlap=true;break;}if(overlap)continue;labelRects.Add(rect);
   labels.Add(new NameLabel{text=wrapped,x=(float)Math.Round(screen.x/Screen.width,4),y=(float)Math.Round(1-screen.y/Screen.height,4),size=size/Screen.height,index=index,token=labelToken,editable=s.Open&&s.Mode=="replace"&&plan==s.Draft&&!s.LayoutPending});

  }
 }
}
}


