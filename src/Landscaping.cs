using System;
using System.Collections.Generic;
using System.Linq;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Newtonsoft.Json;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
namespace ApartmentPlan {
public partial class PlanSystem {
 internal readonly List<TreeSpecies> Trees=new List<TreeSpecies>();
 internal readonly Dictionary<string,Entity> TreePrefabs=new Dictionary<string,Entity>();
 internal bool LandscapeOpen;
 internal readonly List<PlannedTree> PlannedTrees=new List<PlannedTree>();
 Plan treePlan;string treeKey;
 internal int ActiveTreeDensity=>Villa?Mod.Options.VillaTreeDensity:Mod.Options.TreeDensity;
 internal void RefreshPlannedTrees(){
  if(Draft==null&&!Saved.Any(p=>!p.TreePreviewReady&&!IsBuilt(p)&&p.Edit?.Draft!=null))return;
  CatalogTrees();RefreshStoredTreePreviews();
  if(Draft==null){PlannedTrees.Clear();treePlan=null;return;}var o=Mod.Options;
  string key=o.PlantTrees+"|"+ActiveTreeDensity+"|"+o.TreeAges+"|"+o.TreeSeed+"|"+o.TreeSpeciesJson+"|"+o.TreePlacementMode;
  if(ReferenceEquals(treePlan,Draft)&&treeKey==key)return;
  treePlan=Draft;treeKey=key;PlannedTrees.Clear();
  if(o.PlantTrees){var ids=new HashSet<string>(SelectedTrees());PlannedTrees.AddRange(new GreenSpace(Draft).Trees(Trees.Where(t=>ids.Contains(t.Id)).ToArray(),ActiveTreeDensity,o.TreeAges,o.TreeSeed,o.TreePlacementMode));}
  Revision++;
 }
 void RefreshStoredTreePreviews(){
  if(Trees.Count==0)return;
  foreach(var stored in Saved){if(stored.TreePreviewReady||IsBuilt(stored)||stored.Edit?.Draft==null)continue;
   var settings=stored.Edit.Settings;var o=Mod.Options;
   T Read<T>(string key,T fallback){try{return settings!=null&&settings.TryGetValue(key,out var value)?(T)Convert.ChangeType(value,typeof(T),System.Globalization.CultureInfo.InvariantCulture):fallback;}catch{return fallback;}}
   if(Read("PlantTrees",true)){
    string ids=Read("TreeSpeciesJson",o.TreeSpeciesJson);string[] chosen;try{chosen=JsonConvert.DeserializeObject<string[]>(ids)??new string[0];}catch{chosen=new string[0];}
    var selected=new HashSet<string>(chosen);stored.TreePreview.AddRange(new GreenSpace(stored.Edit.Draft).Trees(Trees.Where(t=>selected.Contains(t.Id)).ToArray(),stored.Edit.Draft.Villa?Read("VillaTreeDensity",30):Read("TreeDensity",12),Read("TreeAges",6),Read("TreeSeed",73471),Read("TreePlacementMode",0)));
   }stored.TreePreviewReady=true;break;
  }
 }
 internal string[] SelectedTrees(){try{return JsonConvert.DeserializeObject<string[]>(Mod.Options.TreeSpeciesJson)??new string[0];}catch{return new string[0];}}
 internal void CatalogTrees(){
  if(Trees.Count>0){if(string.IsNullOrEmpty(Mod.Options.TreeSpeciesJson)&&Trees.Any(t=>t.DefaultTheme))RandomTrees();return;}
  var ps=World.GetOrCreateSystemManaged<PrefabSystem>();
  using(var q=EntityManager.CreateEntityQuery(ComponentType.ReadOnly<TreeData>(),ComponentType.ReadOnly<ObjectGeometryData>(),ComponentType.ReadOnly<ObjectData>(),ComponentType.Exclude<Deleted>()))
  using(var es=q.ToEntityArray(Allocator.Temp))foreach(var e in es){
   if(!ps.TryGetPrefab<PrefabBase>(e,out var p)||!(p is StaticObjectPrefab)||ps.GetEntity(p)!=e||TreePrefabs.ContainsKey(p.name)||EntityManager.HasComponent<PlaceholderObjectData>(e)||EntityManager.HasBuffer<PlaceholderObjectElement>(e))continue;
   var g=EntityManager.GetComponentData<ObjectGeometryData>(e);if(g.m_Size.y<3)continue;
   var ui=p.GetComponent<UIObject>();var theme=p.GetComponent<ThemeObject>()?.m_Theme?.name??"";
   var tree=new TreeSpecies{Id=p.name,Name=p.name,Icon=ui?.m_Icon??p.thumbnailUrl??"",Radius=Math.Max(1.5,Math.Max(g.m_Size.x,g.m_Size.z)*.5),DefaultTheme=p.name.StartsWith("NA_",StringComparison.Ordinal)||p.name.StartsWith("EU_",StringComparison.Ordinal)||theme=="European"||theme=="North American"};
   World.GetOrCreateSystemManaged<Game.UI.InGame.PrefabUISystem>().GetTitleAndDescription(e,out tree.NameKey,out _);Trees.Add(tree);TreePrefabs[p.name]=e;
  }
  Trees.Sort((a,b)=>StringComparer.Ordinal.Compare(a.Id,b.Id));
  if(string.IsNullOrEmpty(Mod.Options.TreeSpeciesJson)&&Trees.Any(t=>t.DefaultTheme))RandomTrees();
  Revision++;
 }
 void RandomTrees(){
  var random=new System.Random(Guid.NewGuid().GetHashCode());var pool=Trees.Where(t=>t.DefaultTheme).OrderBy(_=>random.Next()).Take(3).Select(t=>t.Id).ToArray();
  Mod.Options.TreeSpeciesJson=JsonConvert.SerializeObject(pool);Mod.Options.TreeSeed=random.Next(1,int.MaxValue);Mod.Options.ApplyAndSave();Revision++;
 }
 bool LandscapeCommand(string command,string value){
  if(!command.StartsWith("landscape",StringComparison.Ordinal))return false;
  if(command=="landscapeClose"){LandscapeOpen=false;Revision++;return true;}
  if(command=="landscapeOpen"){CatalogTrees();LandscapeOpen=true;Revision++;return true;}
  if(command=="landscapeGrass"){
   if(World.GetOrCreateSystemManaged<GrassIntegration>().Available){Mod.Options.GrassMode=GrassEnabled?1:2;Mod.Options.ApplyAndSave();Revision++;}return true;
  }
  var preview=World.GetExistingSystemManaged<ToolSystem>()?.activeTool as ConstructionTool;
  if(LayoutPending||NativeCommitted||preview!=null&&!preview.CanRefreshLandscape)return true;
  switch(command){
   case "landscapeTrees":Mod.Options.PlantTrees=!Mod.Options.PlantTrees;break;
   case "landscapeRandom":RandomTrees();break;
   case "landscapeSpecies":var list=SelectedTrees().ToList();if(!TreePrefabs.ContainsKey(value))return true;if(!list.Remove(value))list.Add(value);Mod.Options.TreeSpeciesJson=JsonConvert.SerializeObject(list);break;
   case "landscapeAge":if(int.TryParse(value,out int age)&&age>=0&&age<4){int mask=Mod.Options.TreeAges^(1<<age);if(mask!=0)Mod.Options.TreeAges=mask;}break;
   case "landscapePlacement":if(int.TryParse(value,out int placement)&&placement>=0&&placement<=2)Mod.Options.TreePlacementMode=placement;break;
   case "landscapeDensity":if(int.TryParse(value,out int density)&&density>=0&&density<=60){if(Villa)Mod.Options.VillaTreeDensity=density;else Mod.Options.TreeDensity=density;}break;
  }
  Mod.Options.ApplyAndSave();treeKey=null;RefreshPlannedTrees();if(preview!=null)preview.RefreshLandscape();Revision++;return true;
 }
 internal bool GrassEnabled=>Mod.Options.GrassMode!=1&&World.GetOrCreateSystemManaged<GrassIntegration>().Available;
 object LandscapeState()=>new{placement=Mod.Options.TreePlacementMode,count=PlannedTrees.Count,locked=World.GetExistingSystemManaged<ToolSystem>()?.activeTool is ConstructionTool ct&&!ct.CanRefreshLandscape,open=LandscapeOpen,trees=Trees,selected=SelectedTrees(),enabled=Mod.Options.PlantTrees,ages=Mod.Options.TreeAges,density=ActiveTreeDensity,grassAvailable=World.GetOrCreateSystemManaged<GrassIntegration>().Available,grass=GrassEnabled,grassStatus=World.GetOrCreateSystemManaged<GrassIntegration>().Status};
}
public partial class ConstructionTool {
 // Debounce slider changes; native Clear/recreate is owned by the tool update.
 // A regenerated preview always requires a fresh explicit Apply.
 double previewRefreshAt;
 internal bool CanRefreshLandscape=>!Busy&&!committed&&!applyRequested&&!Demolishing;
 internal void RefreshLandscape(){if(!CanRefreshLandscape)return;ResetProgress();Valid=false;previewRefreshAt=UnityEngine.Time.realtimeSinceStartupAsDouble+.2;Status("AP.NativeChecking");}
 bool AdvanceLandscapePreview(){
  if(previewRefreshAt<=0)return false;
  if(UnityEngine.Time.realtimeSinceStartupAsDouble<previewRefreshAt)return true;
  previewRefreshAt=0;Plans.Request("nativeSaveLandscape",Plans.EditingPlan.ToString());ClearDefinitions();applyMode=ApplyMode.Clear;
  submitted=false;frame=0;attachmentPass=0;surfacePass=0;applyRequested=false;Valid=false;Metrics.Clear();Notice="";
  expectedPavement.Clear();appliedPavement.Clear();pavementPrefab=Entity.Null;
  expectedTrees.Clear();appliedTrees.Clear();expected.Clear();expectedBays.Clear();bayDefinitions.Clear();appliedBuildings.Clear();appliedBays.Clear();OwnedEntities.Clear();
  return true;
 }
 readonly List<Tuple<Entity,float3,int>> expectedTrees=new List<Tuple<Entity,float3,int>>();
 readonly List<Entity> appliedTrees=new List<Entity>();
 IEnumerable<int> SubmitTreesSteps(PlanSystem s){
  expectedTrees.Clear();appliedTrees.Clear();if(!Mod.Options.PlantTrees)yield break;s.CatalogTrees();
  s.RefreshPlannedTrees();var trees=s.PlannedTrees;
  foreach(var t in trees){
   if(s.ExistingCompoundPoint(t.Position))continue;
   var prefab=s.TreePrefabs[t.Species];var position=Ground(t.Position);var e=Definition(prefab);
   EntityManager.AddComponentData(e,new ObjectDefinition{m_Position=position,m_Rotation=quaternion.RotateY((float)t.Rotation),m_Probability=100,m_PrefabSubIndex=-1,m_Scale=1,m_Intensity=1,m_ParentMesh=-1,m_Age=new[]{.05f,.175f,.425f,.775f}[t.Age]});
   expectedTrees.Add(Tuple.Create(prefab,position,t.Age));yield return expectedTrees.Count%4==0?1:0;
  }
  Trace.Event("construction.trees.preview","count="+expectedTrees.Count+" species="+s.SelectedTrees().Length);
 }
 IEnumerable<int> ValidateTreesSteps(){
  appliedTrees.Clear();if(expectedTrees.Count==0)yield break;
  var buckets=new Dictionary<Tuple<Entity,int,int>,List<Entity>>();var matched=new HashSet<Entity>();
  using(var es=temps.ToEntityArray(Allocator.Temp))foreach(var e in es){
   if(!EntityManager.HasComponent<Game.Objects.Tree>(e)||!EntityManager.HasComponent<Game.Objects.Transform>(e)||!EntityManager.HasComponent<PrefabRef>(e)||EntityManager.GetComponentData<Temp>(e).m_Original!=Entity.Null)continue;
   var p=EntityManager.GetComponentData<Game.Objects.Transform>(e).m_Position;var key=Tuple.Create(EntityManager.GetComponentData<PrefabRef>(e).m_Prefab,(int)Math.Floor(p.x),(int)Math.Floor(p.z));
   if(!buckets.TryGetValue(key,out var list))buckets[key]=list=new List<Entity>();list.Add(e);
  }
  foreach(var t in expectedTrees){
   yield return 0;
   Entity found=Entity.Null;int x=(int)Math.Floor(t.Item2.x),z=(int)Math.Floor(t.Item2.z);
   for(int dz=-1;dz<=1&&found==Entity.Null;dz++)for(int dx=-1;dx<=1&&found==Entity.Null;dx++)if(buckets.TryGetValue(Tuple.Create(t.Item1,x+dx,z+dz),out var list))foreach(var e in list){
    if(matched.Contains(e)||math.distancesq(EntityManager.GetComponentData<Game.Objects.Transform>(e).m_Position,t.Item2)>.01f)continue;
    found=e;break;
   }
   if(found!=Entity.Null){var e=found;
    // Match PLT's native age-state correction for selected initial life stages.
    EntityManager.SetComponentData(e,new Game.Objects.Tree{m_State=(Game.Objects.TreeState)(t.Item3==0?0:1<<(t.Item3-1)),m_Growth=128});
    if(!EntityManager.HasComponent<BatchesUpdated>(e))EntityManager.AddComponent<BatchesUpdated>(e);
    appliedTrees.Add(e);matched.Add(e);
   }
  }
 }
}
}



