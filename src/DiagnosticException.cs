using System;
using System.Collections.Generic;
namespace ApartmentPlan {
internal static class DiagnosticException {
 internal static InvalidOperationException Parking(string message,int reason,int ordinal=-1,int total=0){
  var e=new InvalidOperationException(message);e.Data["apReason"]=reason;e.Data["apOrdinal"]=ordinal;e.Data["apTotal"]=total;return e;
 }
 internal static InvalidOperationException ParkingLookup(int ordinal,int total,int candidates,int rejected,double distance,double limit,double width){
  var e=Parking("Parking bay has no matching native aisle edge",52,ordinal,total);
  e.Data["apCandidates"]=candidates;e.Data["apRejected"]=rejected;
  e.Data["apDistance"]=Centimetres(distance);e.Data["apLimit"]=Centimetres(limit);e.Data["apWidth"]=Centimetres(width);return e;
 }
 static int Centimetres(double value)=>double.IsNaN(value)||double.IsInfinity(value)||value<0||value>int.MaxValue/100.0?-1:(int)Math.Round(value*100);
 internal static Dictionary<string,int> Record(Exception e,int stage){
  int kind=e is ArgumentException?1:e is InvalidOperationException?2:e is NullReferenceException?3:e is IndexOutOfRangeException?4:e is KeyNotFoundException?5:e is OperationCanceledException?6:e is OutOfMemoryException?7:0;
  int reason=e.Data["apReason"] is int r&&r>=51&&r<=53?r:50;
  var record=new Dictionary<string,int>{{"reason",reason},{"ordinal",e.Data["apOrdinal"] is int n?n:-1},{"connected",e.Data["apTotal"] is int total?total:0},{"kind",kind},{"flags",stage},{"errorCode",e.HResult}};
  if(reason==52&&e.Data["apCandidates"] is int candidates){
   record["edges"]=candidates;
   foreach(var pair in new[]{("ownerKind","apRejected"),("coverageCm","apDistance"),("marginCm","apLimit"),("roadWidthCm","apWidth")})if(e.Data[pair.Item2] is int value)record[pair.Item1]=value;
  }
  return record;
 }
}
}
