using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
// Advisory only: never used by the entrance acceptance/placement checks.
public static class EntranceSpacing {
 public const double Radius=100;
 public static bool Near(Point candidate,IEnumerable<Point> entrances)=>entrances.Any(e=>(e-candidate).Length<=Radius);
 public static bool HasClosePair(IList<Point> entrances){for(int i=0;i<entrances.Count;i++)for(int j=0;j<i;j++)if((entrances[i]-entrances[j]).Length<=Radius)return true;return false;}
}
}
