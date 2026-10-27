using System;
using ApartmentPlan;
if(!GrowableProtectionPolicy.Protect(true,true,false))throw new Exception("Owned growable unprotected");
foreach(bool growable in new[]{false,true})foreach(bool signature in new[]{false,true})if(GrowableProtectionPolicy.Protect(false,growable,signature))throw new Exception("Unrelated city building modified");
if(GrowableProtectionPolicy.Protect(true,false,false)||GrowableProtectionPolicy.Protect(true,true,true))throw new Exception("Non-growable/signature changed");
Console.WriteLine("PASS AP growables only; unrelated, non-growable and signature excluded.");
