using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using ApartmentPlan;
static class CourtInfillTests {
 public static void Run(){
  var p=Layout.Frame(new[]{new Point(0,0),new Point(120,0),new Point(120,220),new Point(0,220)},new Point(60,0));
  var o=new LayoutOptions{ContinuousFrontage=true,Road=8,ParkingAisle=4,AccessRoad=4,BayWidth=2.85,BayDepth=6,ParkingPavingMargin=0,PedestrianNetwork=false};
  var flags=BindingFlags.Instance|BindingFlags.NonPublic;
  var solver=(ResidentialLayout)Activator.CreateInstance(typeof(ResidentialLayout),flags,null,new object[]{p,o,new List<Asset>(),null},null);
  object Call(string name,params object[] args)=>typeof(ResidentialLayout).GetMethod(name,flags).Invoke(solver,args);
  void Road(Point a,Point b,double w,string use){int n=(int)Math.Ceiling((b-a).Length);Call("DrawRoute",Enumerable.Range(0,n+1).Select(i=>a+(b-a)*(i/(double)n)).ToList(),w,use);}
  Road(new Point(0,10),new Point(0,24),8,"main");Call("ProtectMainRoad");
  Road(new Point(-36,24),new Point(36,24),4,"aisle");Road(new Point(-36,180),new Point(36,180),4,"aisle");
  Road(new Point(-36,24),new Point(-36,180),4,"aisle");Road(new Point(36,24),new Point(36,180),4,"aisle");
  var exact=(List<Parcel>)typeof(ResidentialLayout).GetField("exact",flags).GetValue(solver);
  var row=new Parcel{Kind=2,X=28,Y=40,W=6,D=35*2.85,ParkingSpaces=35,ParkingAlongX=false};
  var lot=new Parcel{Kind=0,X=38,Y=40,W=16,D=100,AssetId="existing",Homes=100};
  foreach(var r in new[]{row,lot}){exact.Add(r);Call("Paint",r,10);}
  Call("PaveEnclosedParking");
  // Save the actual pre-infill raster, not an illustrative empty rectangle.
  var map=(int[,])typeof(ResidentialLayout).GetField("map",flags).GetValue(solver);
  double x0=(double)typeof(ResidentialLayout).GetField("x0",flags).GetValue(solver),y0=(double)typeof(ResidentialLayout).GetField("y0",flags).GetValue(solver),cell=(double)typeof(ResidentialLayout).GetField("cell",flags).GetValue(solver);
  var before=new List<Parcel>(exact);
  for(int y=0;y<map.GetLength(1);y++)for(int x=0;x<map.GetLength(0);x++)if(map[x,y]==1||map[x,y]==8)before.Add(new Parcel{Kind=map[x,y],X=x0+x*cell,Y=y0+y*cell,W=cell,D=cell});
  Call("FillPavedParkingCourts");Call("Finish");ParkingTraffic.Validate(p,o);
  var added=exact.Where(r=>r.Kind==2&&!ReferenceEquals(r,row)).ToArray();
  foreach(var c in p.ParkingConnections.Where(c=>!c.Valid))Console.WriteLine("FAIL "+JsonConvert.SerializeObject(c.Bay)+" "+c.Failure);Console.WriteLine("added="+added.Length);
  if(added.Length==0||!added.Any(r=>r.ParkingAlongX)||p.ParkingAccessFailures!=0)throw new Exception("perpendicular infill missing or inaccessible: "+p.ParkingAccessFailures);
  if(exact.Count(r=>r.Kind==0)!=1||lot.X!=38||lot.D!=100)throw new Exception("housing changed");
  if(!added.Any(a=>added.Any(b=>!ReferenceEquals(a,b)&&Math.Abs(a.Y+a.D-b.Y)<.001&&Math.Abs(a.X-b.X)<.001)))throw new Exception("not back to back");
  for(int i=0;i<exact.Count;i++)for(int j=0;j<i;j++)if(Layout.Overlap(exact[i],exact[j]))throw new Exception("overlapping lots or bays");
  File.WriteAllText("ApartmentPlan/docs/court-infill.json",JsonConvert.SerializeObject(new{Before=before,After=p,Added=added,BeforeBays=35,AfterBays=p.Parcels.Sum(r=>r.ParkingSpaces)}));
  Console.WriteLine("PASS enclosed court: 1 building unchanged, bays 35 -> "+p.Parcels.Sum(r=>r.ParkingSpaces)+"; perpendicular back-to-back rows, both aisle ends connected, no overlaps");
 }
}
