using System;
using System.Collections.Generic;
namespace ApartmentPlan {
internal static class DiagnosticException {
 internal static InvalidOperationException Parking(string message,int reason,int ordinal=-1,int total=0){
  var e=new InvalidOperationException(message);e.Data["apReason"]=reason;e.Data["apOrdinal"]=ordinal;e.Data["apTotal"]=total;return e;
 }
 internal static Dictionary<string,int> Record(Exception e,int stage){
  int kind=e is ArgumentException?1:e is InvalidOperationException?2:e is NullReferenceException?3:e is IndexOutOfRangeException?4:e is KeyNotFoundException?5:e is OperationCanceledException?6:e is OutOfMemoryException?7:0;
  int reason=e.Data["apReason"] is int r&&r>=51&&r<=53?r:50;
  return new Dictionary<string,int>{{"reason",reason},{"ordinal",e.Data["apOrdinal"] is int n?n:-1},{"connected",e.Data["apTotal"] is int total?total:0},{"kind",kind},{"flags",stage},{"errorCode",e.HResult}};
 }
}
}
