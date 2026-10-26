using System;
using System.Linq;
using System.Collections.Generic;
using ApartmentPlan;
static class PlanEditTests {
 static void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);}
 public static void Run(){
  var boundary=new[]{new Point(0,0),new Point(220,0),new Point(220,300),new Point(0,300)};
  var entries=new[]{new Point(80,0),new Point(150,300)};
  var assets=new[]{new Asset{Id="도안자이 시험",Width=40,Depth=16,Height=45,Homes=80}};
  var options=new LayoutOptions{BayWidth=3,MainCurveRadius=80};
  var zones=new List<ManualZone>{new ManualZone{Kind=5,Points=new List<Point>{new Point(5,110),new Point(25,110),new Point(25,130),new Point(5,130)}}};
  var plan=Layout.Generate(boundary,entries,assets,options,null,zones);
  var data=new PlanEditData{Boundary=boundary.ToList(),Entrances=entries.ToList(),Zones=zones,Ramps=new List<Parcel>(),Draft=plan,Elevation=123.5f,Selected=new[]{assets[0].Id},NativeCommitted=true,Settings=new Dictionary<string,object>{{"MainRoadCurveRadius",80},{"ParkingDecalGap",.5f},{"MainNetwork","Small Road"}}};
  var encoded=PlanEditCodec.Encode(data);var restored=PlanEditCodec.Decode(encoded);
  Check(restored.Version==1&&restored.Selected[0]==assets[0].Id,"edit codec preserves Unicode prefab identity");
  Check(restored.Boundary.Count==4&&restored.Entrances[1].X==150&&restored.Zones[0].Kind==5,"edit codec preserves boundary offset entrances and subtract zone");
  Check(restored.Elevation==123.5f&&restored.NativeCommitted,"edit codec preserves elevation and duplicate-construction guard");
  Check(restored.Draft.Parcels.Count==plan.Parcels.Count&&restored.Draft.Parcels.Sum(r=>r.ParkingSpaces)==plan.Parcels.Sum(r=>r.ParkingSpaces),"edit codec preserves exact layout and parking counts");
  Check(restored.Draft.Parcels.Where(r=>r.Kind==0).All(r=>r.Frontage!=null),"edit codec preserves per-building frontage for replacements");
  Check(restored.Draft.Routes.Sum(r=>r.Points.Count)==plan.Routes.Sum(r=>r.Points.Count),"edit codec preserves road routes");
  Check(Convert.ToInt32(restored.Settings["MainRoadCurveRadius"])==80&&Convert.ToDouble(restored.Settings["ParkingDecalGap"])==.5,"edit codec preserves plan settings");
  var wire=new Codec();foreach(var value in encoded)new PlanEditByte{Value=value}.Serialize(wire);wire.Reset();var bytes=new byte[encoded.Length];for(int i=0;i<bytes.Length;i++){var item=new PlanEditByte();item.Deserialize(wire);bytes[i]=(byte)item.Value;}
  Check(bytes.SequenceEqual(encoded)&&PlanEditCodec.Decode(bytes).Draft.Homes==plan.Homes,"edit companion buffer roundtrip through production serializers");
  Check(encoded.Length<2000000,"representative edit payload fits budget: "+encoded.Length);
  Check(plan.Parcels.Where(r=>r.Kind==2).All(r=>(r.ParkingAlongX?r.W:r.D)/r.ParkingSpaces>=3-.001),"3 metre pitch leaves half metre beyond 2.5 metre decals");
  var broad=Layout.Generate(boundary,new[]{new Point(110,0),new Point(220,170)},assets,new LayoutOptions{MainCurveRadius=100,MainRoadCornerMode=1},null,null);
  Check(broad.Routes.SelectMany(r=>r.CornerRadii).Any(r=>r>40),"long single corner uses radius above old 40 metre limit");
  Codec.Test();SnapTests.Run();
 }
}
