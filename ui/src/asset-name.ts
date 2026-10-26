type NamedAsset={Id:string;Name?:string;NameKey?:string};
type Translate=(key:string,fallback?:string|null)=>string|null;

export function assetName(asset:NamedAsset,translate:Translate):string {
 const original=asset.Name||asset.Id;
 const key=asset.NameKey||`Assets.NAME[${asset.Id}]`;
 // CS2's active dictionary already merges missing entries from its English fallback.
 const localized=translate(key,original);
 return localized&&localized.trim()&&localized!==key?localized:original;
}

export function assetMatches(asset:NamedAsset,query:string):boolean {
 const needle=query.trim().toLocaleLowerCase();
 return (asset.Name||asset.Id).toLocaleLowerCase().includes(needle)||asset.Id.toLocaleLowerCase().includes(needle);
}
