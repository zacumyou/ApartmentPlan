namespace ApartmentPlan {
internal static class GrowableProtectionPolicy {
 internal static bool Protect(bool owned,bool growable,bool signature)=>owned&&growable&&!signature;
}
}
