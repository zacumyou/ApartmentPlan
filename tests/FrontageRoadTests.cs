using System;
using System.Collections.Generic;
using System.Linq;
using ApartmentPlan;
static class FrontageRoadTests {
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);}
 public static void Run(){
  var polygon=new[]{new Point(0,0),new Point(180,0),new Point(180,340),new Point(0,300)};
  var entrances=new[]{new Point(70,0),new Point(180,240)};
  var assets=new[]{new Asset{Id="Doan",Width=40,Depth=32,Height=111,Homes=144},new Asset{Id="Deep front",Width=24,Depth=40,Height=50,Homes=60}};
  foreach(int direction in new[]{1,2}){
   var o=new LayoutOptions{ContinuousFrontage=true,RequireBuildingRoad=true,ParkingAisle=7,AccessRoad=7,ParkingPavingMargin=0,BayWidth=3.7,BayDepth=6.2,ParkingOrientation=direction,MainRoadPedestrian=true,AisleRoadPedestrian=true};
   var p=Layout.Generate(polygon,entrances,assets,o,null,null);
   Check(p.Parcels.Count(b=>b.Kind==0)>=4,"frontage mode fits multiple real-size buildings, parking orientation="+direction);
   Check(p.ParkingAccessFailures==0,"all parking rows retain vehicle access to main entrance");
   Check(FrontageNetwork.Missing(p,assets,7,0)==0,"production frontage preflight accepts every connected lot");
   var g=ConstructionGraph.Create(p).Where(e=>e.Use!="pedestrian").ToList();
   var visited=new HashSet<ConstructionEdge>{g.OrderBy(e=>Math.Min((e.A-p.Origin).Length,(e.B-p.Origin).Length)).First()};
   bool changed=true;while(changed){changed=false;foreach(var e in g)if(!visited.Contains(e)&&visited.Any(q=>new[]{q.A,q.B}.Any(a=>new[]{e.A,e.B}.Any(b=>(a-b).Length<.03)))){visited.Add(e);changed=true;}}
   Check(visited.Count==g.Count,"every internal road joins the entrance component");
   foreach(var lot in p.Parcels.Where(b=>b.Kind==0)){
    var asset=assets.First(a=>a.Id==lot.AssetId);var f=BuildingPlacement.Forward(lot,asset);var side=new Point(f.Y,-f.X);var front=new Point(lot.X+lot.W/2,lot.Y+lot.D/2)+f*(asset.Depth/2);
    var spans=new List<Tuple<double,double>>();
    foreach(var e in visited.Where(e=>e.Use=="aisle")){
     var a=p.Local(e.A)-front;var b=p.Local(e.B)-front;if(Math.Abs(SiteFrame.Dot(b-a,f))>.01)continue;
     double gap=SiteFrame.Dot((a+b)*.5,f)-7/2.0;if(gap<-.01||gap>1.1)continue;
     double u=SiteFrame.Dot(a,side),v=SiteFrame.Dot(b,side);spans.Add(Tuple.Create(Math.Min(u,v),Math.Max(u,v)));
    }
    double cursor=-asset.Width/2;foreach(var span in spans.OrderBy(t=>t.Item1))if(span.Item1<=cursor+.03)cursor=Math.Max(cursor,span.Item2);
    Check(cursor>=asset.Width/2-.03,"actual prefab front is fully passed by a connected invisible road: "+asset.Id);
   }
   for(int i=0;i<p.Parcels.Count;i++)for(int j=0;j<i;j++)if(Layout.Overlap(p.Parcels[i],p.Parcels[j]))throw new Exception("frontage layout overlaps parcels "+System.Text.Json.JsonSerializer.Serialize(new[]{p.Parcels[i],p.Parcels[j]},new System.Text.Json.JsonSerializerOptions{IncludeFields=true}));
   Check(true,"frontage roads do not overlap building or parking footprints");
   System.IO.File.WriteAllText("ApartmentPlan/docs/connected-frontage-"+direction+".json",System.Text.Json.JsonSerializer.Serialize(p,new System.Text.Json.JsonSerializerOptions{IncludeFields=true}));
  }
  var actual=new Asset{Id="Doan real access",Width=40,Depth=32,Height=111,Homes=144,PedestrianAccess=new List<Point>{new Point(-3.48265839,-14.8937654),new Point(14.417119,-14.7238178),new Point(-2.70267868,14.6536751),new Point(14.5901165,14.7767353)}};
  foreach(double width in new[]{6.0,7.0}){
   var options=new LayoutOptions{ContinuousFrontage=true,RequireBuildingRoad=true,RequireFrontParking=false,ParkingAisle=width,AccessRoad=width,ParkingPavingMargin=1,MainRoadPedestrian=true,AisleRoadPedestrian=true,BayWidth=3.7,BayDepth=6.2};
   var p=Layout.Generate(polygon,entrances,new[]{actual},options,null,null);
   Check(p.Parcels.Any(b=>b.Kind==0)&&FrontageNetwork.Missing(p,new[]{actual},width,1)==0,"actual 6/7m AP road frontage includes residual building infill");
   Check(p.ParkingAccessFailures==0&&p.RequiredAccessFailures==0,"four native Doan access points and parking remain connected");
   foreach(var route in p.Routes.Where(r=>r.Use=="access"))route.Points.Clear();
   Check(FrontageNetwork.Missing(p,new[]{actual},width,1)>0,"severing main-road links is rejected despite visually adjacent frontage roads");
  }
 }
}
