import React from 'react';
import {assetName} from './asset-name';
import {trace} from './trace';
type Named={Id:string;Name?:string;NameKey?:string};
type Translate=(key:string,fallback?:string|null)=>string|null;
export function useAssetCatalog<T extends Named>(raw:string,translate:Translate){
 const [value,setValue]=React.useState<{assets:T[];loading:boolean}>({assets:[],loading:false});
 React.useEffect(()=>{
  let canceled=false,timer:ReturnType<typeof setTimeout>;let input:T[];
  try{input=JSON.parse(raw);if(!Array.isArray(input))input=[];}catch{input=[];}
  if(!input.length){setValue({assets:[],loading:false});return;}
  setValue({assets:[],loading:true});const result:T[]=[];let index=0,maxMs=0,cpuMs=0,slices=0;
  const batch=()=>{
   if(canceled)return;const start=performance.now();let count=0;
   while(index<input.length){const item=input[index++];result.push({...item,Name:assetName(item,translate)});if(++count>=128||performance.now()-start>=2)break;}
   const elapsed=performance.now()-start;maxMs=Math.max(maxMs,elapsed);cpuMs+=elapsed;slices++;
   if(index<input.length)timer=setTimeout(batch,8);
   else{setValue({assets:result,loading:false});trace('catalog.localized',`count=${result.length} slices=${slices} cpuMs=${cpuMs.toFixed(1)} maxSliceMs=${maxMs.toFixed(1)}`);}
  };
  timer=setTimeout(batch,0);
  return()=>{canceled=true;clearTimeout(timer);};
 },[raw,translate]);
 return value;
}
