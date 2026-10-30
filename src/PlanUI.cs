using Colossal.UI.Binding;
using Game;
using Game.Input;
using Game.Tools;
using UnityEngine;
namespace ApartmentPlan {
public partial class PlanUI:Game.UI.UISystemBase {
 ValueBinding<string> nameLabels;string lastNameLabels="";
 ValueBinding<string> state,catalog,hint;string lastHint="";float nextHint;PlanSystem plans;int revision=-1,catalogRevision=-1;ProxyAction longer,shorter,finish;float nextSettingsTrace;string settingsSnapshot="";float nextState;
 public override GameMode gameMode=>GameMode.Game;
 protected override void OnCreate(){base.OnCreate();AddBinding(nameLabels=new ValueBinding<string>("ApartmentPlan","nameLabels","[]"));Trace.Event("binding.create.begin");plans=World.GetOrCreateSystemManaged<PlanSystem>();AddBinding(state=new ValueBinding<string>("ApartmentPlan","state","{}"));AddBinding(catalog=new ValueBinding<string>("ApartmentPlan","catalog","[]"));AddBinding(new TriggerBinding<string,string>("ApartmentPlan","command",(c,v)=>plans.Request(c,v)));AddBinding(new TriggerBinding<bool>("ApartmentPlan","focus",v=>{if(plans.Focused!=v)Trace.Event("ui.focus",v.ToString());plans.Focused=v;}));longer=Mod.Options.GetAction("Longer");shorter=Mod.Options.GetAction("Shorter");finish=Mod.Options.GetAction("Finish");AddBinding(new TriggerBinding<string,string>("ApartmentPlan","trace",(name,value)=>Trace.Event("ui."+Trace.Clean(name,64),Trace.Clean(value,2000))));AddBinding(hint=new ValueBinding<string>("ApartmentPlan","hint",""));Trace.Event("binding.create.complete");}
 protected override void OnUpdate(){base.OnUpdate();var labelJson=World.GetExistingSystemManaged<PlanOverlay>()?.LabelJson??"[]";if(labelJson!=lastNameLabels){lastNameLabels=labelJson;nameLabels.Update(labelJson);}if(Trace.Detailed&&UnityEngine.Time.unscaledTime>=nextSettingsTrace){nextSettingsTrace=UnityEngine.Time.unscaledTime+1;var snapshot=Mod.Options.TraceSnapshot();if(snapshot!=settingsSnapshot){settingsSnapshot=snapshot;Trace.Event("settings.changed",snapshot);}}if(UnityEngine.Time.unscaledTime>=nextHint){nextHint=UnityEngine.Time.unscaledTime+.1f;var text=World.GetExistingSystemManaged<PlanTool>()?.Hint??"";if(text!=lastHint){lastHint=text;hint.Update(text);}}
  bool enabled=plans.Open&&!plans.Focused&&Application.isFocused&&!InputManager.instance.hasInputFieldFocus&&!InputManager.instance.overlayActive&&World.GetExistingSystemManaged<ToolSystem>()?.activeTool is PlanTool;
  longer.shouldBeEnabled=shorter.shouldBeEnabled=enabled&&plans.Mode!="boundary"&&plans.Mode!="road";finish.shouldBeEnabled=enabled&&(plans.Mode=="road"||plans.Mode=="boundary"||plans.Mode=="zone"||plans.Mode=="entrance"&&plans.Entrances.Count>0);
  if(longer.shouldBeEnabled&&longer.WasPressedThisFrame())plans.Request("longer");if(shorter.shouldBeEnabled&&shorter.WasPressedThisFrame())plans.Request("shorter");if(finish.shouldBeEnabled&&finish.WasPressedThisFrame())plans.Request("finish");
  if(revision!=plans.Revision&&(!plans.NativeBusy&&!plans.LayoutPending||UnityEngine.Time.realtimeSinceStartup>=nextState)){nextState=UnityEngine.Time.realtimeSinceStartup+.1f;revision=plans.Revision;var json=plans.State();state.Update(json);Trace.Event("binding.state","revision="+revision+" bytes="+json.Length+" open="+plans.Open+" mode="+plans.Mode+" status="+plans.Status);}
  if(catalogRevision!=plans.CatalogRevision){catalogRevision=plans.CatalogRevision;var watch=System.Diagnostics.Stopwatch.StartNew();var json=plans.CatalogJson();catalog.Update(json);Trace.Event("binding.catalog","revision="+catalogRevision+" count="+plans.Assets.Count+" bytes="+json.Length+" ms="+watch.ElapsedMilliseconds);}
 }
 protected override void OnDestroy(){Trace.Event("binding.destroy");if(longer!=null)longer.shouldBeEnabled=false;if(shorter!=null)shorter.shouldBeEnabled=false;if(finish!=null)finish.shouldBeEnabled=false;base.OnDestroy();}
}
}













