using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Input;
using Game.Modding;
using Game.Settings;
using Newtonsoft.Json;
namespace ApartmentPlan {
[FileLocation("ApartmentPlan")]
[SettingsUIKeyboardAction("Longer",ActionType.Button,usages:new[]{"ApartmentPlan.Tool"})]
[SettingsUIKeyboardAction("Shorter",ActionType.Button,usages:new[]{"ApartmentPlan.Tool"})]
[SettingsUIKeyboardAction("Finish",ActionType.Button,usages:new[]{"ApartmentPlan.Tool"})]
[SettingsUIMouseAction("Click",ActionType.Button,usages:new[]{"ApartmentPlan.Tool"})]
[SettingsUIMouseAction("Back",ActionType.Button,usages:new[]{"ApartmentPlan.Tool"})]
public sealed class Settings:ModSetting {
 public Settings(IMod mod):base(mod){}
 [SettingsUISection("Display")] public bool DebugLogging{get;set;}=false;
 [SettingsUIHidden] public string RecentNetworksJson{get;set;}="{}";
 [SettingsUIHidden] public bool AutoCalculate{get;set;}=true;
 [SettingsUIHidden] public string RecentBuildingAssetsJson{get;set;}="[]";
 [SettingsUIHidden] public string TowerSelectedAssetsJson{get;set;}="[]";
 [SettingsUIHidden] public string TowerRulesJson{get;set;}="{}";
 [SettingsUIHidden] public string TowerParkingAsset{get;set;}="";
 [SettingsUIHidden] public string TowerSurface{get;set;}="Tiles Surface 01";
 [SettingsUIHidden] public string TowerEntranceSurface{get;set;}="Pavement Surface 01";
 [SettingsUIHidden] public bool TowerEntrancePaving{get;set;}=true;
 [SettingsUIHidden] public bool TowerFacility{get;set;}=false;
 [SettingsUIHidden] public int TowerTurnStyle{get;set;}=2;
 [SettingsUIHidden] public string VillaSelectedAssetsJson{get;set;}="";
 [SettingsUIHidden] public int PlanMode{get;set;}
 [SettingsUIHidden] public bool VillaTileLots{get;set;}=true;
 [SettingsUIHidden] public bool VillaCentralRoad{get;set;}=true;
 [SettingsUIHidden] public bool VillaFollowBoundary{get;set;}=true;
 [SettingsUIHidden] public bool VillaMesh{get;set;}=true;
 [SettingsUIHidden] public float VillaGap{get;set;}=0;
 [SettingsUIHidden] public int VillaSpacingVersion{get;set;}
 [SettingsUIHidden] public float VillaReduction{get;set;}=3;
 [SettingsUIHidden] public int VillaMainSpacing{get;set;}=120;
 [SettingsUIHidden] public int VillaRows{get;set;}=2;
 [SettingsUIHidden] public int VillaMaxBuildings{get;set;}=500;
 [SettingsUIHidden] public float VillaBoundary{get;set;}=2;
 [SettingsUIHidden] public float VillaSetback{get;set;}=0;
 [SettingsUIHidden] public float VillaConnectionDistance{get;set;}=4;
 [SettingsUIHidden] public int BuildingPattern{get;set;}=0;
 [SettingsUIHidden] public bool AllowMainTJunctions{get;set;}=true;
 [SettingsUIHidden] public bool GrassSurfaces{get;set;}=false;
 [SettingsUIHidden] public int LandscapeDefaultsVersion{get;set;}
 [SettingsUIHidden] public int VillaTreeDensity{get;set;}=30;
 [SettingsUIHidden] public bool TileBuildingLots{get;set;}=true;
 [SettingsUIHidden] public float SurfaceCornerRadius{get;set;}=2;
 [SettingsUIHidden] public bool PlantTrees{get;set;}=true;
 [SettingsUIHidden] public int TreeAges{get;set;}=6;
 [SettingsUIHidden] public int TreePlacementMode{get;set;}=0;
 [SettingsUIHidden] public int TreeDensity{get;set;}=12;
 [SettingsUIHidden] public int TreeSeed{get;set;}=73471;
 [SettingsUIHidden] public string TreeSpeciesJson{get;set;}="";
 [SettingsUIHidden] public int GrassMode{get;set;}=0;
 [SettingsUIHidden] public bool FlattenTerrain{get;set;}=false; // Legacy settings ID, ignored.
 [SettingsUIHidden] public bool RequireBuildingRoad{get;set;}=true;
 [SettingsUISection("Layout"),SettingsUISlider(min=0,max=2,step=1,unit="integer")] public int ParkingOrientation{get;set;}=0;
 [SettingsUISection("Layout"),SettingsUISlider(min=2,max=8,step=.5f,unit="floatSingleFraction")] public float ParkingEndClearance{get;set;}=3;
 [SettingsUISection("Layout"),SettingsUISlider(min=2,max=4,step=.5f,unit="floatSingleFraction")] public float VehiclePassageWidth{get;set;}=3;
 [SettingsUISection("Display")] public string InvisibleRoadColor{get;set;}="#D9DCDF";

 [SettingsUISection("Layout"),SettingsUISlider(min=20,max=200,step=10,unit="integer")] public int PerimeterPathSpacing{get;set;}=80;
 [SettingsUIHidden] public int FrontParkingCoverage{get;set;}=20;
 [SettingsUISection("Layout"),SettingsUISlider(min=0,max=20,step=1,unit="integer")] public int BoundaryGreenMargin{get;set;}=4;
 [SettingsUISection("Layout"),SettingsUISlider(min=0,max=20,step=1,unit="integer")] public int MainParkingSetback{get;set;}=4;
 [SettingsUISection("Layout"),SettingsUISlider(min=0,max=3,step=1,unit="integer")] public int ParkingPavingMargin{get;set;}=1;
 [SettingsUISection("Layout"),SettingsUISlider(min=0,max=8,step=1,unit="integer")] public int BuildingStagger{get;set;}=2;
 [SettingsUISection("Layout"),SettingsUISlider(min=8,max=100,step=2,unit="integer")] public int MaxAccessLength{get;set;}=40;
 [SettingsUISection("Layout"),SettingsUISlider(min=8,max=80,step=2,unit="integer")] public int FrontParkingDistance{get;set;}=40;
 [SettingsUISection("Layout"),SettingsUISlider(min=0,max=100,step=1,unit="integer")] public int VerticalPreference{get;set;}=38;
 [SettingsUISection("Layout"),SettingsUISlider(min=1,max=8,step=1,unit="integer")] public int SearchStep{get;set;}=2;
 [SettingsUISection("Layout"),SettingsUISlider(min=1,max=12,step=1,unit="integer")] public int MinFrontParkingSpaces{get;set;}=4;
 [SettingsUISection("Layout"),SettingsUISlider(min=0,max=10,step=1,unit="integer")] public int MaxRingCourts{get;set;}=2;
 [SettingsUISection("Layout"),SettingsUISlider(min=10,max=500,step=10,unit="integer")] public int MaxBuildings{get;set;}=100;
 [SettingsUISection("Layout")] public bool RequireFrontParking{get;set;}=true;
 [SettingsUISection("Layout")] public bool AllowRingCourts{get;set;}=true;
 [SettingsUISection("Layout")] public bool AllowOpenCourts{get;set;}=true;
 [SettingsUISection("Layout")] public bool AllowParallelPairs{get;set;}=true;
 [SettingsUISection("Layout")] public bool PedestrianNetwork{get;set;}=true;
 [SettingsUIHidden] public string MainNetwork{get;set;}="";
 [SettingsUIHidden] public string AisleNetwork{get;set;}="";
 [SettingsUIHidden] public string WalkNetwork{get;set;}="";
 [SettingsUIHidden] public int NetworkDefaultsVersion{get;set;}
 [SettingsUIHidden] public int InternalRoadDefaultsVersion{get;set;}
 [SettingsUIHidden] public string SelectedAssetsJson{get;set;}="[]";
 [SettingsUIHidden] public string AssetCorrectionsJson{get;set;}="[]";

 [SettingsUISection("Input")] public bool SnapRoads{get;set;}=true;
 [SettingsUISection("Input")] public bool SnapBuildings{get;set;}=true;
 [SettingsUISection("Input")] public bool SnapLots{get;set;}=true;
 [SettingsUISection("Input")] public bool SnapGrid{get;set;}=true;
 [SettingsUISection("Input")] public bool SnapDirections{get;set;}=true;
 [SettingsUIHidden] public bool SnapClose{get;set;}=true;
 [SettingsUISection("Input")] public bool SnapGuides{get;set;}=true;
 [SettingsUISection("Input")] public bool ShowGuides{get;set;}=true;
 [SettingsUISection("Input"),SettingsUISlider(min=1,max=16,step=1,unit="integer")] public int SnapDistance{get;set;}=8;
 [SettingsUISection("Input"),SettingsUISlider(min=2,max=20,step=1,unit="integer")] public int CloseDistance{get;set;}=12;
 [SettingsUISection("Layout"),SettingsUISlider(min=4,max=20,step=1,unit="integer")] public int RoadWidth{get;set;}=8;
 [SettingsUISection("Layout"),SettingsUISlider(min=3,max=100,step=2,unit="integer")] public int EntranceLength{get;set;}=15;
 [SettingsUISection("Layout"),SettingsUISlider(min=0,max=30,step=1,unit="integer")] public int BuildingGap{get;set;}=8;
 [SettingsUISection("Layout")] public bool MixedBuildingDirections{get;set;}=true;
 [SettingsUISection("Layout"),SettingsUISlider(min=0,max=2,step=1,unit="integer")] public int MainRoadCornerMode{get;set;}=0;
 [SettingsUISection("Layout"),SettingsUISlider(min=25,max=100,step=5,unit="percentage")] public int MainRoadCurveLengthPercent{get;set;}=50;
 [SettingsUISection("Layout"),SettingsUISlider(min=8,max=200,step=2,unit="integer")] public int MainRoadCurveRadius{get;set;}=40;
 [SettingsUISection("Layout"),SettingsUISlider(min=16,max=80,step=2,unit="integer")] public int ParallelBuildingGap{get;set;}=24;
 [SettingsUISection("Layout"),SettingsUISlider(min=2,max=5,step=.1f,unit="floatSingleFraction")] public float FloorHeight{get;set;}=3;
 [SettingsUISection("Layout"),SettingsUISlider(min=10,max=100,step=5,unit="percentage")] public int FootprintPercent{get;set;}=70;
 [SettingsUISection("Layout"),SettingsUISlider(min=0,max=.3f,step=.01f,unit="floatTwoFractions")] public float ParkingLineOverlap{get;set;}=.15f;
 [SettingsUIHidden] public float ParkingDecalGap{get;set;}=.5f;
 [SettingsUISection("Layout"),SettingsUISlider(min=2,max=4,step=.1f,unit="floatSingleFraction")] public float ParkingBayWidth{get;set;}=2.5f;
 [SettingsUISection("Layout"),SettingsUISlider(min=4,max=7,step=.5f,unit="floatSingleFraction")] public float ParkingBayDepth{get;set;}=5;
 [SettingsUISection("Layout"),SettingsUISlider(min=5,max=10,step=1,unit="integer")] public int ParkingAisleWidth{get;set;}=6;
 [SettingsUISection("Layout"),SettingsUISlider(min=2,max=6,step=1,unit="integer")] public int WalkwayWidth{get;set;}=3;
 [SettingsUISection("Layout")] public bool WideSlabParking{get;set;}=true;
 [SettingsUISection("Layout"),SettingsUISlider(min=0,max=100,step=5,unit="percentage")] public int SurfaceParkingShare{get;set;}=40;
 [SettingsUISection("Layout"),SettingsUISlider(min=1,max=3,step=1,unit="integer")] public int MaxParkingAisles{get;set;}=3;
 [SettingsUISection("Layout")] public bool ParkingGreenIslands{get;set;}=false;
 [SettingsUISection("Layout"),SettingsUISlider(min=0,max=6,step=1,unit="integer")] public int ParkingIslandWidth{get;set;}=3;
 [SettingsUIHidden] public bool ParkingInOpenSpace{get;set;}=false;
 [SettingsUISection("Layout")] public bool EdgeInfill{get;set;}=true;
 [SettingsUISection("Layout")] public bool AlignBlocks{get;set;}=true;
 [SettingsUISection("Layout")] public bool RespectTargetFar{get;set;}=false;
 [SettingsUISection("Layout"),SettingsUISlider(min=50,max=600,step=10,unit="percentage")] public int TargetFar{get;set;}=220;
 [SettingsUISection("Layout"),SettingsUISlider(min=0,max=40,step=1,unit="percentage")] public int ParkingPercent{get;set;}=18;
 [SettingsUISection("Layout"),SettingsUISlider(min=0,max=40,step=1,unit="percentage")] public int ParkPercent{get;set;}=15;
 [SettingsUIHidden] public int PlaygroundPercent{get;set;}=3;
 [SettingsUIHidden] public int FacilityPercent{get;set;}=3;
 [SettingsUISection("Layout"),SettingsUISlider(min=0,max=3,step=.1f,unit="floatSingleFraction")] public float ParkingPerHome{get;set;}=1;
 [SettingsUISection("Layout"),SettingsUISlider(min=20,max=40,step=1,unit="integer")] public int ParkingSpaceArea{get;set;}=30;
 [SettingsUIHidden] public int RampWidth{get;set;}=8;
 [SettingsUIHidden] public int RampLength{get;set;}=20;
 [SettingsUISection("Display"),SettingsUISlider(min=.1f,max=1,step=.05f,unit="floatTwoFractions")] public float EditOpacity{get;set;}=.8f;
 [SettingsUISection("Display"),SettingsUISlider(min=.1f,max=1,step=.05f,unit="floatTwoFractions")] public float SavedOpacity{get;set;}=.6f;
 [SettingsUISection("Display")] public bool ShowParkingLines{get;set;}=true;
 [SettingsUISection("Display")] public bool ShowSaved{get;set;}=true;
 [SettingsUISection("Display")] public string BuildingColor{get;set;}="#F09A4B";
 [SettingsUISection("Display")] public string RoadColor{get;set;}="#89949D";
 [SettingsUISection("Display")] public string ParkingColor{get;set;}="#438FE3";
 [SettingsUISection("Display")] public string PlaygroundColor{get;set;}="#F5DA50";
 [SettingsUISection("Display")] public string ParkColor{get;set;}="#62BE76";
 [SettingsUISection("Display")] public string FacilityColor{get;set;}="#B080DD";
 [SettingsUISection("Display")] public string PavedColor{get;set;}="#C4BCA6";
 [SettingsUIHidden] public string RampColor{get;set;}="#42D3D0";
 [SettingsUISection("Display"),SettingsUISlider(min=200,max=6000,step=200,unit="integer")] public int ViewDistance{get;set;}=3000;
 [SettingsUISection("Input"),SettingsUIKeyboardBinding(BindingKeyboard.R,"Longer",shift:true)] public ProxyBinding LongerKey{get;set;}
 [SettingsUISection("Input"),SettingsUIKeyboardBinding(BindingKeyboard.T,"Shorter",shift:true)] public ProxyBinding ShorterKey{get;set;}
 [SettingsUISection("Input"),SettingsUIKeyboardBinding(BindingKeyboard.Enter,"Finish")] public ProxyBinding FinishKey{get;set;}
 [SettingsUISection("Input"),SettingsUIMouseBinding(BindingMouse.Left,"Click")] public ProxyBinding ClickKey{get;set;}
 [SettingsUISection("Input"),SettingsUIMouseBinding(BindingMouse.Right,"Back")] public ProxyBinding BackKey{get;set;}
 public LayoutOptions Layout(double entrance)=>new LayoutOptions{BuildingPattern=Math.Max(0,Math.Min(3,BuildingPattern)),AllowMainTJunctions=AllowMainTJunctions,MainCurveLengthRatio=Math.Max(.25,Math.Min(1,MainRoadCurveLengthPercent/100.0)),RequireBuildingRoad=RequireBuildingRoad,ParkingOrientation=Math.Max(0,Math.Min(2,ParkingOrientation)),ParkingEndClearance=Math.Max(2,Math.Min(8,ParkingEndClearance)),VehiclePassageWidth=Math.Max(2,Math.Min(4,VehiclePassageWidth)),PerimeterPathSpacing=Math.Max(20,Math.Min(200,PerimeterPathSpacing)),FrontParkingCoverage=Math.Max(.1,Math.Min(1,FrontParkingCoverage/100.0)),BoundaryGreenMargin=Math.Max(0,Math.Min(20,BoundaryGreenMargin)),MainParkingSetback=Math.Max(0,Math.Min(20,MainParkingSetback)),ParkingPavingMargin=Math.Max(0,Math.Min(3,ParkingPavingMargin)),BuildingStagger=Math.Max(0,Math.Min(8,BuildingStagger)),MaxAccessLength=Math.Max(8,Math.Min(100,MaxAccessLength)),FrontParkingDistance=Math.Max(8,Math.Min(80,FrontParkingDistance)),VerticalPreference=Math.Max(0,Math.Min(100,VerticalPreference))/100.0,SearchStep=Math.Max(1,Math.Min(8,SearchStep)),MinFrontParkingSpaces=Math.Max(1,Math.Min(12,MinFrontParkingSpaces)),MaxRingCourts=Math.Max(0,Math.Min(10,MaxRingCourts)),MaxBuildings=Math.Max(10,Math.Min(500,MaxBuildings)),RequireFrontParking=RequireFrontParking,AllowRingCourts=AllowRingCourts,AllowOpenCourts=AllowOpenCourts,AllowParallelPairs=AllowParallelPairs,PedestrianNetwork=PedestrianNetwork,Road=Math.Max(4,RoadWidth),Entrance=Math.Max(3,entrance),Gap=Math.Max(0,BuildingGap),ParallelGap=Math.Max(16,ParallelBuildingGap),MixedDirections=MixedBuildingDirections,MainRoadCornerMode=Math.Max(0,Math.Min(2,MainRoadCornerMode)),MainCurveRadius=Math.Max(8,Math.Min(200,MainRoadCurveRadius)),FloorHeight=Math.Max(2,FloorHeight),FootprintRatio=Math.Max(.1,Math.Min(1,FootprintPercent/100.0)),TargetFar=TargetFar,RespectTargetFar=RespectTargetFar,ParkingInOpenSpace=ParkingInOpenSpace,AlignBlocks=AlignBlocks,EdgeInfill=EdgeInfill,WideSlabParking=WideSlabParking,SurfaceParkingShare=SurfaceParkingShare/100.0,MaxParkingAisles=MaxParkingAisles,ParkingIsland=ParkingGreenIslands?Math.Max(0,Math.Min(6,ParkingIslandWidth)):0,BayWidth=Math.Max(2,ParkingBayWidth),BayDepth=Math.Max(4,ParkingBayDepth),ParkingAisle=Math.Max(5,ParkingAisleWidth),Walkway=Math.Max(2,WalkwayWidth),Park=ParkPercent/100.0,Play=0,Facility=0,Parking=ParkingPercent/100.0,ParkingPerHome=ParkingPerHome,SpaceArea=Math.Max(20,ParkingSpaceArea)};
 internal string TraceSnapshot(){var d=new Dictionary<string,object>();foreach(var p in typeof(Settings).GetProperties(BindingFlags.Instance|BindingFlags.Public|BindingFlags.DeclaredOnly))if((p.PropertyType.IsPrimitive||p.PropertyType==typeof(string))&&!p.Name.EndsWith("Json"))d[p.Name]=p.GetValue(this);foreach(var name in new[]{"Longer","Shorter","Finish","Click","Back"}){var bindings=new List<string>();foreach(var b in GetAction(name).bindings){string text=b.path;foreach(var m in b.modifiers)text+="+"+m.m_Path;bindings.Add(text);}d["binding."+name]=bindings;}return JsonConvert.SerializeObject(d);} public string[] Colors=>new[]{BuildingColor,RoadColor,ParkingColor,PlaygroundColor,ParkColor,FacilityColor,RampColor,"#FFFFFF",PavedColor,"#F0DDB5",InvisibleRoadColor};
 public override void SetDefaults(){DebugLogging=false;RecentNetworksJson="{}";AutoCalculate=true;ParkingInOpenSpace=false;RecentBuildingAssetsJson="[]";TowerRulesJson="{}";TowerSelectedAssetsJson="[]";TowerParkingAsset="";TowerSurface="Tiles Surface 01";TowerEntranceSurface="Pavement Surface 01";TowerEntrancePaving=true;TowerFacility=false;TowerTurnStyle=2;VillaTileLots=true;VillaFollowBoundary=true;VillaCentralRoad=true;PlanMode=0;VillaMesh=true;VillaGap=0;VillaSpacingVersion=1;VillaReduction=3;VillaMainSpacing=120;VillaRows=2;VillaMaxBuildings=500;VillaBoundary=2;VillaSetback=0;VillaConnectionDistance=4;GrassSurfaces=false;LandscapeDefaultsVersion=1;VillaTreeDensity=30;BuildingPattern=0;AllowMainTJunctions=true;TileBuildingLots=true;SurfaceCornerRadius=2;PlantTrees=true;TreePlacementMode=0;TreeAges=6;TreeDensity=12;TreeSeed=73471;TreeSpeciesJson="";GrassMode=0;FlattenTerrain=false;ParkingLineOverlap=.15f;ParkingGreenIslands=false;MainRoadCurveLengthPercent=50;SnapClose=true;ParkingDecalGap=.5f;RequireBuildingRoad=true;ParkingOrientation=0;ParkingEndClearance=3;VehiclePassageWidth=3;InvisibleRoadColor="#D9DCDF";PerimeterPathSpacing=80;FrontParkingCoverage=20;BoundaryGreenMargin=4;MainParkingSetback=4;ParkingPavingMargin=1;BuildingStagger=2;MaxAccessLength=40;FrontParkingDistance=40;VerticalPreference=38;SearchStep=2;MinFrontParkingSpaces=4;MaxRingCourts=2;MaxBuildings=100;RequireFrontParking=AllowRingCourts=AllowOpenCourts=AllowParallelPairs=PedestrianNetwork=true;SnapRoads=SnapBuildings=SnapLots=SnapGrid=SnapDirections=SnapGuides=ShowGuides=true;SnapDistance=8;CloseDistance=12;RoadWidth=8;EntranceLength=15;BuildingGap=8;ParallelBuildingGap=24;MixedBuildingDirections=true;MainRoadCornerMode=0;MainRoadCurveRadius=40;FloorHeight=3;FootprintPercent=70;TargetFar=220;RespectTargetFar=false;AlignBlocks=true;EdgeInfill=true;WideSlabParking=true;SurfaceParkingShare=40;MaxParkingAisles=3;ParkingIslandWidth=3;ParkingBayWidth=2.5f;ParkingBayDepth=5;ParkingAisleWidth=6;WalkwayWidth=3;PavedColor="#C4BCA6";ParkingPercent=18;ParkPercent=15;PlaygroundPercent=3;FacilityPercent=3;ParkingPerHome=1;ParkingSpaceArea=30;RampWidth=8;RampLength=20;EditOpacity=.8f;SavedOpacity=.6f;ShowSaved=true;ShowParkingLines=true;ViewDistance=3000;BuildingColor="#F09A4B";RoadColor="#89949D";ParkingColor="#438FE3";PlaygroundColor="#F5DA50";ParkColor="#62BE76";FacilityColor="#B080DD";RampColor="#42D3D0";}
}
public sealed class Locale:IDictionarySource {
 readonly Dictionary<string,string> data;
 public Locale(Settings s,string lang){Trace.Event("locale.begin",lang);using(var stream=typeof(Mod).Assembly.GetManifestResourceStream("ApartmentPlan.lang."+lang+".json")){if(stream==null)throw new InvalidOperationException("Missing embedded ApartmentPlan locale: "+lang);using(var reader=new StreamReader(stream))data=JsonConvert.DeserializeObject<Dictionary<string,string>>(reader.ReadToEnd());}data[s.GetSettingsLocaleID()]="Apartment Plan";
  foreach(var group in new[]{"Layout","Display","Input"})data[s.GetOptionTabLocaleID(group)]=data["AP."+group];
  foreach(var p in typeof(Settings).GetProperties()){if(data.TryGetValue("AP.Option."+p.Name,out var label)){data[s.GetOptionLabelLocaleID(p.Name)]=label;data[s.GetOptionDescLocaleID(p.Name)]=data.TryGetValue("AP.Desc."+p.Name,out var desc)?desc:label;}}
  Trace.Event("locale.ready",lang+" entries="+data.Count);
 }
 public IEnumerable<KeyValuePair<string,string>> ReadEntries(IList<IDictionaryEntryError> errors,Dictionary<string,int> indexCounts)=>data;
 public void Unload(){}
}
}





