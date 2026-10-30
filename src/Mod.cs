using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Unity.Entities;
namespace ApartmentPlan {
public sealed class Mod:IMod {
 internal static Settings Options; internal static readonly ILog Log=LogManager.GetLogger("ApartmentPlan").SetShowsErrorsInUI(true);
 Locale en,ko;
 public void OnLoad(UpdateSystem updates){Trace.Start();UnityEngine.Application.logMessageReceivedThreaded+=Trace.UnityLog;try{Trace.Event("mod.load.begin");Options=new Settings(this);AssetDatabase.global.LoadSettings("ApartmentPlan",Options,new Settings(this));Options.DebugLogging=false;Options.ParkingInOpenSpace=false;if(Options.VillaSpacingVersion<1){if(Options.VillaGap==1)Options.VillaGap=0;Options.VillaSpacingVersion=1;}if(Options.LandscapeDefaultsVersion<1){Options.GrassSurfaces=false;Options.LandscapeDefaultsVersion=1;Options.ApplyAndSave();}Options.RegisterKeyBindings();Options.RegisterInOptionsUI();Trace.Event("settings.registered");
  en=new Locale(Options,"en-US");ko=new Locale(Options,"ko-KR");GameManager.instance.localizationManager.AddSource("en-US",en);GameManager.instance.localizationManager.AddSource("ko-KR",ko);
  updates.UpdateBefore<InvisibleRoadPrefabs,Game.Prefabs.PrefabInitializeSystem>(SystemUpdatePhase.PrefabUpdate);
  updates.UpdateAfter<InvisibleRoadPrefabAudit,Game.Prefabs.NetInitializeSystem>(SystemUpdatePhase.PrefabUpdate);
  updates.UpdateBefore<BeforePlanSerialization,Game.Serialization.BeginPrefabSerializationSystem>(SystemUpdatePhase.Serialize);
  updates.UpdateAfter<AfterPlanSerialization,Game.Serialization.EndPrefabSerializationSystem>(SystemUpdatePhase.Serialize);
  updates.UpdateBefore<CompoundGrowableProtection,Game.Simulation.CondemnedBuildingSystem>(SystemUpdatePhase.GameSimulation);
  updates.UpdateBefore<PlanAudio,Game.Audio.AudioManager>(SystemUpdatePhase.Cleanup);
  updates.UpdateAt<PlanSystem>(SystemUpdatePhase.Modification1);updates.UpdateAt<LotSurfaceReplacement>(SystemUpdatePhase.Modification2);updates.UpdateAt<InvisibleRoadRefresh>(SystemUpdatePhase.Modification1);updates.UpdateAt<GrassIntegration>(SystemUpdatePhase.Modification2);updates.UpdateAt<PlanTool>(SystemUpdatePhase.ToolUpdate);updates.UpdateAt<ConstructionTool>(SystemUpdatePhase.ToolUpdate);updates.UpdateAt<PlanUI>(SystemUpdatePhase.UIUpdate);updates.UpdateAt<PlanSelection>(SystemUpdatePhase.ToolUpdate);updates.UpdateAt<PlanOverlay>(SystemUpdatePhase.ToolUpdate);updates.UpdateAfter<InvisibleRoadHover,Game.Tools.BulldozeToolSystem>(SystemUpdatePhase.ToolUpdate);Trace.Event("mod.load.complete","systems=PlanSystem,PlanTool,PlanUI,PlanOverlay");Log.Info("ApartmentPlan "+typeof(Mod).Assembly.GetName().Version+" loaded; standard diagnostic logging enabled.");}catch(System.Exception e){Trace.Failure("mod.load.failed",e);throw;}
 }
 public void OnDispose(){Trace.Event("mod.dispose.begin");try{var w=World.DefaultGameObjectInjectionWorld;if(w!=null&&w.IsCreated){var tools=w.GetExistingSystemManaged<Game.Tools.ToolSystem>();if(tools?.activeTool is PlanTool)tools.activeTool=w.GetOrCreateSystemManaged<Game.Tools.DefaultToolSystem>();w.GetExistingSystemManaged<PlanSystem>()?.ClearTransient();w.GetExistingSystemManaged<PlanOverlay>()?.ClearLabels();}Options?.UnregisterInOptionsUI();Options=null;if(en!=null)GameManager.instance.localizationManager.RemoveSource("en-US",en);if(ko!=null)GameManager.instance.localizationManager.RemoveSource("ko-KR",ko);}catch(System.Exception e){Trace.Failure("mod.dispose.failed",e);throw;}finally{UnityEngine.Application.logMessageReceivedThreaded-=Trace.UnityLog;Trace.Stop();}}
}
}


