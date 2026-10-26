using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
public static class PlanLimits {
 public const int Parcels=4800,TotalShapes=48000,SavedPlans=32,Zones=32,Roads=64;
}
public sealed class PlanLimitException:ArgumentException {
 public readonly string Status;public readonly double Current,Maximum;
 public PlanLimitException(string status,double current,double maximum):base(status){Status=status;Current=current;Maximum=maximum;}
}
public struct Point {
 public double X,Y;
 public Point(double x,double y){X=x;Y=y;}
 public static Point operator +(Point a,Point b)=>new Point(a.X+b.X,a.Y+b.Y);
 public static Point operator -(Point a,Point b)=>new Point(a.X-b.X,a.Y-b.Y);
 public static Point operator *(Point a,double b)=>new Point(a.X*b,a.Y*b);
 public double Length=>Math.Sqrt(X*X+Y*Y);
}
public sealed class Asset {
 public List<Point> PedestrianAccess=new List<Point>();
 public int EntranceSide=-1;public double GeometryWidth,GeometryDepth;
 public string Id,Name,Icon,NameKey; public double Width,Depth,Height,Footprint; public int Homes;
}
public static class BuildingPlacement {
 // Keep the plan's access targets and native ObjectDefinition rotation identical.
 public static Point Forward(Parcel parcel,Asset asset){
  if(parcel.PlacementForward.HasValue)return parcel.PlacementForward.Value;
  var center=new Point(parcel.X+parcel.W/2,parcel.Y+parcel.D/2);
  var outward=parcel.Frontage!=null?new Point(parcel.Frontage.X+parcel.Frontage.W/2,parcel.Frontage.Y+parcel.Frontage.D/2)-center:new Point(0,-1);
  double best=double.MinValue;Point forward=new Point(0,1);
  for(int k=0;k<4;k++){bool swap=k%2==1;if(Math.Abs((swap?asset.Depth:asset.Width)-parcel.W)>.1||Math.Abs((swap?asset.Width:asset.Depth)-parcel.D)>.1)continue;var f=new Point(Math.Sin(k*Math.PI/2),Math.Cos(k*Math.PI/2));double score=SiteFrame.Dot(f,outward);if(score>best){best=score;forward=f;}}
  if(best==double.MinValue)throw new InvalidOperationException("Lot dimensions do not match "+asset.Id);
  return forward;
 }
 public static Point AccessTarget(Parcel parcel,Asset asset,Point local,double padding){
  var f=Forward(parcel,asset);var p=new Point(parcel.X+parcel.W/2,parcel.Y+parcel.D/2)+new Point(f.Y,-f.X)*local.X+f*local.Y;
  // The native waypoint search respects the lot edge nearest its access object.
  // Route outside that edge, without crossing the building to reach its front.
  var edges=new[]{new Point(parcel.X-padding,Math.Max(parcel.Y,Math.Min(parcel.Y+parcel.D,p.Y))),new Point(parcel.X+parcel.W+padding,Math.Max(parcel.Y,Math.Min(parcel.Y+parcel.D,p.Y))),new Point(Math.Max(parcel.X,Math.Min(parcel.X+parcel.W,p.X)),parcel.Y-padding),new Point(Math.Max(parcel.X,Math.Min(parcel.X+parcel.W,p.X)),parcel.Y+parcel.D+padding)};
  return edges.OrderBy(q=>(q-p).Length).First();
 }
}
public sealed class Parcel {
 public Point? PlacementForward;public Parcel CompactBounds;public Point[] PlacementOutline;
 public bool InvisibleRoad;public int ParkingEntrySide;public Parcel Frontage;public int ClusterId=-1;public Point? Access;public bool AccessEstimated=true;
 public int Kind; public double X,Y,W,D; public string AssetId=""; public double FloorArea; public int Homes;public int ParkingSpaces;public bool ParkingAlongX=true;
 public double Area=>W*D;
 public Point[] Corners=>new[]{new Point(X,Y),new Point(X+W,Y),new Point(X+W,Y+D),new Point(X,Y+D)};
}
public static class ReplacementRules {
 // Keep the estimated frontage edge and its centre fixed when a lot shrinks.
 public static void KeepFront(Parcel old,Parcel next){
  var f=old.Frontage;if(f==null)return;
  if(old.W>=old.D)next.Y=f.Y+f.D/2>=old.Y+old.D/2?old.Y+old.D-next.D:old.Y;
  else next.X=f.X+f.W/2>=old.X+old.W/2?old.X+old.W-next.W:old.X;
 }
 public static IEnumerable<Parcel> Vacated(Parcel old,Parcel next){
  var pieces=new[]{new Parcel{Kind=8,X=old.X,Y=old.Y,W=next.X-old.X,D=old.D},new Parcel{Kind=8,X=next.X+next.W,Y=old.Y,W=old.X+old.W-next.X-next.W,D=old.D},new Parcel{Kind=8,X=next.X,Y=old.Y,W=next.W,D=next.Y-old.Y},new Parcel{Kind=8,X=next.X,Y=next.Y+next.D,W=next.W,D=old.Y+old.D-next.Y-next.D}};
  return pieces.Where(p=>p.W>.0001&&p.D>.0001);
 }
}
public sealed class LayoutOptions {
 public TowerRules TowerRules=new TowerRules();public double TowerTurnWidth=4;
 public bool Tower,TowerFacility,TowerEntrancePaving=true;public int TowerTurnStyle=2;public Asset TowerParking;public string TowerSurface="Tiles Surface 01",TowerEntranceSurface="Pavement Surface 01";
 public bool Villa,VillaMesh=true,VillaFollowBoundary=true,VillaCentralRoad=true;
 public List<PlanRoute> VillaExistingRoads=new List<PlanRoute>();public double VillaGap=0,VillaReduction=3,VillaMainSpacing=120,VillaBoundary=2,VillaSetback=0;
 public int VillaRows=2,VillaMaxBuildings=500;
 public List<VillaConnection> VillaConnections=new List<VillaConnection>();
 internal System.Threading.CancellationToken Cancellation{get;set;}
 public bool RequireBuildingRoad=true;public int ParkingOrientation=0;public double ParkingEndClearance=3,VehiclePassageWidth=3;
 public double PerimeterPathSpacing=80,FrontParkingCoverage=.2,BoundaryGreenMargin=4,ParkingPavingMargin=1,BuildingStagger=2,MaxAccessLength=40,FrontParkingDistance=40,VerticalPreference=.38,SearchStep=2;
 public bool RequireFrontParking=true,AllowRingCourts=true,AllowOpenCourts=true,AllowParallelPairs=true;public int MinFrontParkingSpaces=4,MaxRingCourts=2,MaxBuildings=100;
 // Runtime capabilities of the selected native networks, not a user override.
 public bool MainRoadPedestrian,AisleRoadPedestrian;
 public bool ContinuousFrontage;
 public double AccessRoad=0,WideParkingAisle=0;
 public List<ExternalWalkTarget> ExternalWalkTargets=new List<ExternalWalkTarget>();
 public double Road=8,Entrance=15,Gap=8,FloorHeight=3,FootprintRatio=.7,TargetFar=220,Park=.15,Play=.03,Facility=.03,Parking=.18,ParkingPerHome=1,SpaceArea=30;
 public double MainCurveLengthRatio=.5;
 public int MainRoadCornerMode=0;
 public bool AllowMainTJunctions=true;
 // 0 existing mix, 1 horizontal bars, 2 vertical bars, 3 both bar axes without courts.
 public int BuildingPattern;
 public double ParallelGap=24,MainParkingSetback=4,MainCurveRadius=16;public bool MixedDirections=true;
 public double BayWidth=2.5,BayDepth=5,ParkingAisle=6,Walkway=3;
 public bool WideSlabParking=true;public double SurfaceParkingShare=.4,ParkingIsland=0;public int MaxParkingAisles=3;
 public bool EdgeInfill=true;
 public bool AlignBlocks=true;
 public bool RespectTargetFar=false;
 public double TurnClearance=6;public bool PedestrianNetwork=true;
 public int MaxParcels=PlanLimits.Parcels;
}
public sealed class Plan {
 public List<ConstructionCourse> AdditionCourses;
 public int TowerAlgorithmVersion;
 public List<List<Point>> TowerGrassOutlines=new List<List<Point>>();
 public List<List<Point>> TowerEntryOutlines=new List<List<Point>>();public double TowerWalkGap=1;
 public bool Tower;public string TowerSurface="Tiles Surface 01",TowerEntranceSurface="Pavement Surface 01";public List<Parcel> TowerPaving=new List<Parcel>(),TowerEntryPaving=new List<Parcel>();
 public int VillaPedestrianWarnings;
 public bool Villa;public bool VillaExistingRoadsOnly;public double VillaExistingRoadLength,VillaRoadDensity;public bool VillaThrough;public int VillaEmptyBlocks;public List<Point> VillaRoots=new List<Point>();public List<Point> VillaRejectedConnections=new List<Point>();
 public List<Parcel> PavementPads=new List<Parcel>();
 public List<Parcel> GrassPockets=new List<Parcel>();
 public Dictionary<string,long> LayoutTimings=new Dictionary<string,long>();
 public List<PlanRoute> ConstructionRoutes=new List<PlanRoute>();
 public List<Parcel> MainRoadArea=new List<Parcel>();public List<CourtGroup> Courts=new List<CourtGroup>();public List<PlanRoute> Routes=new List<PlanRoute>();public List<Point> UnreachableAccess=new List<Point>();
 public List<ParkingConnection> ParkingConnections=new List<ParkingConnection>();public int ParkingAccessFailures;public int RequiredAccessFailures;public string Diagnostics="";
 public List<Point> Boundary=new List<Point>(); public List<Parcel> Parcels=new List<Parcel>();
 public Point Origin,Right,Forward; public double Area,Far; public int Homes; public bool Limited; public int RequestedParcels,ParcelLimit; public double[] Shortfall=new double[7];
 public Point World(Point p)=>Origin+Right*p.X+Forward*p.Y;
 public Point Local(Point p){var q=p-Origin;return new Point(q.X*Right.X+q.Y*Right.Y,q.X*Forward.X+q.Y*Forward.Y);}
}
public sealed class CourtGroup {
 public int Id;public string Pattern;public Parcel Bounds;public Point Gate;
 public List<Parcel> Buildings=new List<Parcel>();public List<Parcel> Parking=new List<Parcel>();
 public List<Point> AccessPoints=new List<Point>();public int Capacity;
}
public sealed class ExternalWalkTarget { public Point Boundary,Inside,Road; }
public sealed class PlanRoute {
 public List<PlanBezier> Curves=new List<PlanBezier>();
 public List<double> CornerRadii=new List<double>();public string Use;public double Width;public List<Point> Points=new List<Point>();
}
public static class Layout {
 const double E=.0001;
 public static double Cross(Point a,Point b)=>a.X*b.Y-a.Y*b.X;
 public static double SignedArea(IList<Point> p){double a=0;for(int i=0;i<p.Count;i++)a+=Cross(p[i],p[(i+1)%p.Count]);return a*.5;}
 static bool On(Point p,Point a,Point b)=>Math.Abs(Cross(b-a,p-a))<E&&p.X>=Math.Min(a.X,b.X)-E&&p.X<=Math.Max(a.X,b.X)+E&&p.Y>=Math.Min(a.Y,b.Y)-E&&p.Y<=Math.Max(a.Y,b.Y)+E;
 static bool Intersects(Point a,Point b,Point c,Point d){double x=Cross(b-a,c-a),y=Cross(b-a,d-a),z=Cross(d-c,a-c),w=Cross(d-c,b-c);return x*y<0&&z*w<0||On(c,a,b)||On(d,a,b)||On(a,c,d)||On(b,c,d);}
 public static bool CanAppend(IList<Point> p,Point next){if(double.IsNaN(next.X)||double.IsNaN(next.Y)||double.IsInfinity(next.X)||double.IsInfinity(next.Y)||p.Count>=128)return false;if(p.Count==0)return true;var last=p[p.Count-1];if((last-next).Length<1)return false;for(int i=0;i<p.Count-2;i++)if(Intersects(last,next,p[i],p[i+1]))return false;return true;}
 public static bool ValidOpen(IList<Point> p){var accepted=new List<Point>();foreach(var point in p){if(!CanAppend(accepted,point))return false;accepted.Add(point);}return true;}
 public static bool Valid(IList<Point> p){if(p.Count<3||p.Count>128||p.Any(v=>double.IsNaN(v.X)||double.IsNaN(v.Y)||double.IsInfinity(v.X)||double.IsInfinity(v.Y)))return false;
  for(int i=0;i<p.Count;i++){if((p[i]-p[(i+1)%p.Count]).Length<1)return false;for(int j=i+1;j<p.Count;j++){if(j==i+1||i==0&&j==p.Count-1)continue;if(Intersects(p[i],p[(i+1)%p.Count],p[j],p[(j+1)%p.Count]))return false;}}return Math.Abs(SignedArea(p))>=100;
 }
 public static bool Inside(Point p,IList<Point> poly){bool inside=false;for(int i=0,j=poly.Count-1;i<poly.Count;j=i++){var a=poly[j];var b=poly[i];if(On(p,a,b))return true;if((a.Y>p.Y)!=(b.Y>p.Y)&&p.X<(b.X-a.X)*(p.Y-a.Y)/(b.Y-a.Y)+a.X)inside=!inside;}return inside;}
 public static bool Contains(Parcel r,IList<Point> p){var c=r.Corners;if(c.Any(v=>!Inside(v,p)))return false;
  // Reject a concave boundary crossing the rectangle even when all corners are inside.
  for(int i=0;i<p.Count;i++){var a=p[i];var b=p[(i+1)%p.Count];if(a.X>r.X+E&&a.X<r.X+r.W-E&&a.Y>r.Y+E&&a.Y<r.Y+r.D-E)return false;for(int k=0;k<4;k++)if(Cross(c[(k+1)%4]-c[k],a-c[k])*Cross(c[(k+1)%4]-c[k],b-c[k])<-E&&Cross(b-a,c[k]-a)*Cross(b-a,c[(k+1)%4]-a)<-E)return false;}return true;
 }
 public static bool Overlap(Parcel a,Parcel b,double gap=0)=>a.X<b.X+b.W+gap-E&&a.X+a.W+gap>b.X+E&&a.Y<b.Y+b.D+gap-E&&a.Y+a.D+gap>b.Y+E;
 public static Plan Frame(IList<Point> boundary,Point entrance)=>SiteFrame.Create(boundary,entrance);
 public static Plan Generate(IList<Point> boundary,IList<Point> entrances,IList<Asset> assets,LayoutOptions o,IList<Parcel> ramps,IList<ManualZone> zones,IList<ManualRoad> roads=null)=>o.Tower?TowerLayout.Generate(boundary,entrances,assets,o,zones):o.Villa?VillaLayout.Generate(boundary,entrances,assets,o,zones):ResidentialLayout.Generate(boundary,entrances,assets,o,ramps,zones,roads);
 public static Plan Generate(IList<Point> boundary,Point entrance,IList<Asset> assets,LayoutOptions o,IList<Parcel> ramps)=>ResidentialLayout.Generate(boundary,entrance,assets,o,ramps);
}
}
