export type NetworkItem={Id:string;Width:number;RoadBuilder?:boolean;Builtin?:boolean};
export type NetworkFilter='all'|'roadBuilder'|'builtin';
export function orderNetworks<T extends NetworkItem>(items:T[],filter:NetworkFilter,selected:string|undefined,recent:unknown,targetWidth:number,name:(id:string)=>string):T[]{
 const ranks=new Map<string,number>();
 if(selected)ranks.set(selected,0);
 if(Array.isArray(recent))recent.forEach(id=>{if(typeof id==='string'&&!ranks.has(id))ranks.set(id,ranks.size);});
 const compare={compare:compareNetworkNames};
 const distance=(n:T)=>Number.isFinite(n.Width)&&n.Width>0&&Number.isFinite(targetWidth)&&targetWidth>0?Math.abs(n.Width-targetWidth):Infinity;
 return items.filter(n=>filter==='all'||(filter==='roadBuilder'?n.RoadBuilder:n.Builtin&&!n.RoadBuilder)).slice().sort((a,b)=>{
  const ar=ranks.get(a.Id)??Infinity,br=ranks.get(b.Id)??Infinity;
  if(ar!==br)return ar<br?-1:1;
  const ad=distance(a),bd=distance(b);if(ad!==bd)return ad<bd?-1:1;
  return compare.compare(name(a.Id),name(b.Id))||compare.compare(a.Id,b.Id);
 });
}
export function roadWidthLabel(width:number|undefined,unknown:string):string{
 return typeof width==='number'&&Number.isFinite(width)&&width>0?`${Number(width.toFixed(2))} m`:unknown;
}

// Coherent UI does not provide Intl. Hangul syllables sort in 가나다 order by code point.
export function compareNetworkNames(a:string,b:string):number{
 const left=a.toLowerCase().match(/\d+|\D+/g)||[],right=b.toLowerCase().match(/\d+|\D+/g)||[];
 for(let i=0;i<Math.min(left.length,right.length);i++){
  let x=left[i],y=right[i];
  if(/^\d+$/.test(x)&&/^\d+$/.test(y)){
   x=x.replace(/^0+(?=\d)/,'');y=y.replace(/^0+(?=\d)/,'');
   if(x.length!==y.length)return x.length<y.length?-1:1;
  }
  if(x!==y)return x<y?-1:1;
 }
 return left.length-right.length;
}
