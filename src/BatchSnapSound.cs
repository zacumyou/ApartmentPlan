namespace ApartmentPlan {
internal sealed class BatchSnapSound {
 bool heard;
 internal void Reset(){heard=false;}
 internal bool Keep(bool snap){if(!snap)return true;if(heard)return false;heard=true;return true;}
}
}
