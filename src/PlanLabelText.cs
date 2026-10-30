using System;
using System.Text;
namespace ApartmentPlan {
internal static class PlanLabelText {
 internal static string Wrap(string name){
  var words=(name??"").Split((char[])null,StringSplitOptions.RemoveEmptyEntries);
  var result=new StringBuilder();
  for(int i=0;i<words.Length;i++){
   if(i>0)result.Append(i%2==0?'\n':' ');
   // Names without spaces still need bounded line lengths.
   var word=words[i];for(int j=0;j<word.Length;j++){if(j>0&&j%14==0)result.Append('\n');result.Append(word[j]);}
  }
  return result.ToString();
 }
}
}
