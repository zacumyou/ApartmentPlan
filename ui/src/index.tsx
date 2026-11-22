import {detailScene} from './detail-scene';
import snapAll from './icons/native-snap/All.svg';
import snapNetSide from './icons/native-snap/NetSide.svg';
import snapLotGrid from './icons/native-snap/LotGrid.svg';
import snapStraightDirection from './icons/native-snap/StraightDirection.svg';
import snapObjectSide from './icons/native-snap/ObjectSide.svg';
import snapGuideLines from './icons/native-snap/GuideLines.svg';
import snapZoneGrid from './icons/native-snap/ZoneGrid.svg';
import './panel-layout.js';
import React from 'react';
import {orderNetworks,roadWidthLabel,NetworkFilter} from './network-list';
import {ModRegistrar} from 'cs2/modding';
import {bindValue,useValue,trigger} from 'cs2/api';
import {Button,FloatingButton,Tooltip,Scrollable} from 'cs2/ui';
import {useLocalization} from 'cs2/l10n';
import styles from './plan.module.css';
import icon from './plan.svg';
import menuIcon from './apartment-menu.svg';

const formatInteger=(value:number)=>Math.round(value).toString().replace(/\B(?=(\d{3})+(?!\d))/g,',');
import glyphs from './icons';
import en from '../../lang/en-US.json';
import {trace} from './trace';
import {PanelDismiss} from './panel-dismiss';
import {assetName,assetMatches} from './asset-name';
import {useAssetCatalog} from './use-asset-catalog';
import {usePanelPlacement} from './use-panel-placement';
const state$=bindValue<string>('ApartmentPlan','state','{}');
const hint$=bindValue<string>('ApartmentPlan','hint','');
const catalog$=bindValue<string>('ApartmentPlan','catalog','[]');
const nameLabels$=bindValue<string>('ApartmentPlan','nameLabels','[]');
type NameLabel={text:string;x:number;y:number;size:number;scale:number;index:number;token:number;editable:boolean};
function NameLabels(){
 const raw=useValue(nameLabels$);
 const labels=React.useMemo<NameLabel[]>(()=>{try{return JSON.parse(raw)||[]}catch{return []}},[raw]);
 const editable=labels.some(label=>label.editable);
 React.useEffect(()=>()=>trigger('ApartmentPlan','focus',false),[editable]);
 return <div className={styles.nameLayer}>{labels.map((label,i)=><div key={i} className={styles.nameLabel} role={label.editable?'button':undefined} style={{left:label.x*100+'%',top:label.y*100+'%',fontSize:label.size*100+'vh',transform:'translate(-50%,-50%) scale('+(label.scale??1)+')',pointerEvents:label.editable?'auto':'none'}} onMouseEnter={()=>{if(label.editable)trigger('ApartmentPlan','focus',true)}} onMouseLeave={()=>{if(label.editable)trigger('ApartmentPlan','focus',false)}} onClick={e=>{if(label.editable){e.stopPropagation();command('pickLabel',label.token+'|'+label.index)}}}>{label.text.split('\n').map((line,n)=><span key={n}>{line}</span>)}</div>)}</div>;
}
const command=(c:string,v='')=>{trace('command.send',c+' value='+v.slice(0,500));trigger('ApartmentPlan','command',c,v)};
type Asset={Subscribed?:boolean;Id:string;Name:string;NameKey?:string;Icon:string;Width:number;Depth:number;GeometryWidth?:number;GeometryDepth?:number;Height:number;Footprint:number;Homes:number};
type Stored={cleanupShapes?:number;name?:string;canDelete?:boolean;deleteReason?:string;parked?:number|null;occupiedHomes?:number|null;parkingCapacity?:number|null;homeCapacity?:number|null;owned?:number;built?:boolean;id:number;area:number;far:number;homes:number;buildings:number;parking:number|null;editable:boolean;legacy:boolean};
type Landscape={placement?:number;count?:number;locked?:boolean;open:boolean;trees:{Id:string;Name:string;NameKey?:string;Icon:string}[];selected:string[];enabled:boolean;ages:number;density:number;grassAvailable:boolean;grass:boolean;grassStatus:string};
type State={boundaryValid?:boolean;canReviewLayout?:boolean;canRedo?:boolean;hasDraft?:boolean;canForceBuilding?:boolean;buildingEditor?:{x:number;y:number;current:string};recentNetworks?:string;autoCalculate?:boolean;layoutOutdated?:boolean;generateSurfaceParking?:boolean;parkingInOpenSpace?:boolean;compoundEditing?:boolean;canForceDemolition?:boolean;canDeleteAllPlans?:boolean;layoutReasons?:Record<string,number>;migration?:{plan:number;count:number;skipped:number;applied:number;status:string};fillRadius?:number;canFill?:boolean;recentBuildingAssets?:string;entranceClose?:boolean;tower?:{rules?:{id:string;value:number;defaultValue:number;min:number;max:number;step:number}[];parking:string;surface:string;entranceSurface:string;paving:boolean;facility:boolean;turn:number;surfaces:{Id:string;NameKey?:string}[]};villaCentralRoad?:boolean;villaExistingRoadsOnly?:boolean;villaRoadDensity?:number;villaFollowBoundary?:boolean;planMode?:number;villaMesh?:boolean;villaGap?:number;villaReduction?:number;villaMainSpacing?:number;villaRows?:number;villaMaxBuildings?:number;villaBoundary?:number;villaSetback?:number;villaConnectionDistance?:number;villaConnections?:{Side:number;Junction:boolean;Disabled:boolean}[];limit?:{current:number;maximum:number};grassSurfaces?:boolean;nativeProgress?:number;nativeBusy?:boolean;buildingPattern?:number;mainTJunctions?:boolean;statusError?:boolean;diagnosticCopyState?:number;diagnosticShareStatus?:string;diagnosticShareBusy?:boolean;catalogLoading?:boolean;catalogCount?:number;lotTiles?:boolean;surfaceRadius?:number;landscape?:Landscape;canDelete?:boolean;deleteReason?:string;mainSetback?:number;boundaryMargin?:number;calculating?:boolean;population?:number|null;householdAverage?:number;pendingRoads?:number;roadCurved?:boolean;roadPoints?:number;roads?:number;nativeDemolition?:boolean;inspector?:Stored;editingPlan?:number;curveLength?:number;curveRadius?:number;decalGap?:number;lineOverlap?:number;parkingPitch?:number;parkingGreen?:boolean;parkingGreenWidth?:number;networks?:{Id:string;Name?:string;RoadBuilder?:boolean;Builtin?:boolean;Icon?:string;Width:number;Path:boolean;Car:boolean;Walk:boolean;BuildingAccess:boolean;Internal:boolean}[];mainNetwork?:string;aisleNetwork?:string;walkNetwork?:string;nativeActive?:boolean;nativeValid?:boolean;nativeReport?:string;nativeMetrics?:Record<string,number>;nativeNotice?:string;nativeCommitted?:boolean;nativeAuditAvailable?:boolean;cornerMode?:number;requireBuildingRoad?:boolean;parkingAccessFailures?:number;requireFrontParking?:boolean;accessFailures?:number;entrances?:number;zones?:number;zonePoints?:number;replacement?:string;snaps?:boolean[];open?:boolean;mode?:string;status?:string;points?:number;entrance?:number;selected?:string[];area?:number;far?:number;homes?:number;shortfall?:number[];areas?:Record<string,number>;suggestCleanup?:boolean;saved?:Stored[];colors?:string[];canSave?:boolean};
function useStateData():State{const raw=useValue(state$);return React.useMemo(()=>{try{return JSON.parse(raw)||{}}catch{return {}}},[raw]);}
const Glyph=({name}:{name:string})=><img className={styles.glyph} src={glyphs[name]||icon} alt=""/>;
// Shared switch: label left, dedicated 42x24 button right (TunnelStylePacks baseline).
function Switch({label,on,run,disabled=false}:{label:string;on:boolean;run:()=>void;disabled?:boolean}){
 return <div className={styles.check}><span className={styles.switchLabel+' '+(disabled?styles.switchDisabled:'')}>{label}</span><Button className={styles.switchTrack} role="switch" aria-label={label} aria-checked={on} disabled={disabled} onSelect={run}><span className={styles.switchThumb} aria-hidden="true"/></Button></div>;
}
// Completion is shown briefly; percentages are native work milestones, not elapsed time.
function useLoading(s:State,localizing=false){
 const busy=!!(s.nativeBusy||s.calculating||s.catalogLoading||localizing);
 const previous=React.useRef(false),[complete,setComplete]=React.useState(false);
 React.useEffect(()=>{let timer:ReturnType<typeof setTimeout>|undefined;
  if(busy||s.statusError)setComplete(false);
  else if(previous.current&&!s.statusError){setComplete(true);timer=setTimeout(()=>setComplete(false),350);}
  previous.current=busy;return()=>{if(timer)clearTimeout(timer)};
 },[busy,s.statusError]);
 const locked=busy||complete;
 const stop=(e:React.SyntheticEvent)=>{if(locked){e.preventDefault();e.stopPropagation();}};
 return {locked,catalog:!!(s.catalogLoading||localizing),progress:complete?100:s.nativeBusy?s.nativeProgress:undefined,
  events:{'aria-busy':locked,onClickCapture:stop,onMouseDownCapture:stop,onMouseUpCapture:stop,onKeyDownCapture:stop,onKeyUpCapture:stop,onChangeCapture:stop,onWheelCapture:stop}};
}
function visibleNotice(s:State){
 return s.nativeNotice&&s.nativeNotice!==s.status&&s.status!=='AP.DemolitionApplied'&&s.nativeNotice!=='AP.DemolishHint';
}
function Loading({s,loading,t}:{s:State;loading:ReturnType<typeof useLoading>;t:(k:string)=>string}){
 const [visible,setVisible]=React.useState(loading.locked);
 React.useEffect(()=>{if(loading.locked){setVisible(true);return;}const timer=setTimeout(()=>setVisible(false),180);return()=>clearTimeout(timer);},[loading.locked]);
 if(!loading.locked&&!visible)return null;
 const title=s.nativeBusy?'AP.NativeLoadingTitle':loading.catalog?'AP.CatalogLoading':'AP.CalculatingTitle';
 const progress=loading.progress;
 return <div className={styles.loadingVeil+(!loading.locked?' '+styles.loadingExit:'')} role="status">
  <div className={styles.progressRing} role="progressbar" aria-label={t(title)} aria-valuemin={0} aria-valuemax={100} aria-valuenow={progress}>
   <div className={styles.rayWheel+(progress===undefined?' '+styles.turning:'')} aria-hidden="true">
    {Array.from({length:60},(_,i)=>{
     const level=progress===undefined?Math.max(0,1-i/18):Math.max(0,Math.min(1,(progress*.63-i)/4));
     const palette=[[155,147,237],[152,156,236],[137,169,237],[123,181,236],[117,189,222],[117,198,210]];
     const color=palette[Math.floor(i/10)].map((channel,k)=>Math.round([72,82,95][k]+(channel-[72,82,95][k])*level)).join(',');
     return <div key={i} className={styles.rayAxis} style={{transform:'rotate('+i*6+'deg)'}}><i className={styles.rayColor} style={{transform:'scaleY('+(0.67+0.33*level)+')',backgroundImage:'linear-gradient(to top,rgba('+color+',0),rgba('+color+',0.26) 40%,rgb('+color+'))'}}/></div>
    })}
   </div>
   <div className={styles.ringCaption}>{progress!==undefined&&<b>{Math.round(progress)+'%'}</b>}<span>{progress===100?t('AP.LoadingComplete'):t(title)}</span></div>
  </div></div>;
}
function MenuButton(){const s=useStateData();return <Tooltip tooltip="Apartment Plan"><FloatingButton src={menuIcon} tinted={false} selected={!!s.open} onSelect={()=>command('menu')}/></Tooltip>}
function Panel(){
 React.useLayoutEffect(()=>{const node=document.querySelector('[data-ap-panel]'); window.csPanelLayoutV1.add(node); window.csPanelLayoutV1.moved(node);});
 const s=useStateData(),hint=useValue(hint$),rawCatalog=useValue(catalog$);
 const {translate}=useLocalization();const t=(k:string)=>translate(k,(en as Record<string,string>)[k]||k)||k;
 const {assets,loading:localizing}=useAssetCatalog<Asset>(rawCatalog,translate);
 const p=usePanelPlacement(!!s.open);const loading=useLoading(s,localizing);
 const [networkQuery,setNetworkQuery]=React.useState('');
 const [networkPage,setNetworkPage]=React.useState(0);
 const [networkFilter,setNetworkFilter]=React.useState<NetworkFilter>('all');
 const [selectedOnly,setSelectedOnly]=React.useState(false),[networkOpen,setNetworkOpen]=React.useState('');
 React.useEffect(()=>setNetworkPage(0),[networkQuery,networkOpen,networkFilter,s.mainNetwork,s.aisleNetwork,s.walkNetwork]);
 const [showCleanup,setShowCleanup]=React.useState(false);
 const [query,setQuery]=React.useState(''),[all,setAll]=React.useState(false),[page,setPage]=React.useState(0),[tab,setTab]=React.useState('assets');
 const [correction,setCorrection]=React.useState<string|null>(null),[ready,setReady]=React.useState(false);
 React.useEffect(()=>{try{const v=JSON.parse(localStorage.getItem('ApartmentPlan.catalog')||localStorage.getItem('ApartmentPlan.panel')||'{}');setQuery(typeof v.query==='string'?v.query:'');setAll(!!v.all);}catch{}setReady(true);},[]);
 React.useEffect(()=>{if(ready)try{localStorage.setItem('ApartmentPlan.catalog',JSON.stringify({query,all,tab}));}catch{}},[query,all,tab,ready]);
 const villa=s.planMode===1,tower=s.planMode===2;
 const selected=s.selected||[];const selectionKey=selected.join('\u0000');
 const orderedAssets=React.useMemo(()=>{
  let recent:unknown;try{recent=JSON.parse(s.recentBuildingAssets||'[]');}catch{recent=[];}
  const ranks=new Map<string,number>();if(Array.isArray(recent))recent.forEach((id,i)=>{if(typeof id==='string'&&!ranks.has(id))ranks.set(id,i);});
  return assets.map((asset,index)=>({asset,index})).sort((a,b)=>(ranks.get(a.asset.Id)??Infinity)-(ranks.get(b.asset.Id)??Infinity)||Number(!!b.asset.Subscribed)-Number(!!a.asset.Subscribed)||a.index-b.index).map(x=>x.asset);
 },[assets,s.recentBuildingAssets]);
 const matches=React.useMemo(()=>{const ids=new Set(selected);return orderedAssets.filter(a=>(!villa||!/Rural/i.test(a.Id+" "+a.Name))&&(!selectedOnly||ids.has(a.Id))&&(all||(villa?a.Homes>0&&Math.abs(a.Width-16)<.1&&Math.abs(a.Depth-16)<.1&&/CS[\s_-]*(KR|Korean)/i.test(a.Id+' '+a.Name):a.Homes>1))&&assetMatches(a,query));},[orderedAssets,selectedOnly,selectionKey,all,query,villa]);
 const assetsPerPage=6;
 const pages=Math.max(1,Math.ceil(matches.length/assetsPerPage)),current=Math.min(page,pages-1);
 React.useEffect(()=>setPage(0),[query,all,selectedOnly]);
 React.useEffect(()=>setPage(v=>Math.min(v,pages-1)),[pages]);
 React.useEffect(()=>{if(s.mode==='replace')setTab('assets');},[s.mode]);
 const sideRef=React.useRef<HTMLDivElement>(null);
 React.useLayoutEffect(()=>{
  const main=p.shell.current,side=sideRef.current;
  if(!main||!side)return;
  let shift=0;
  const place=()=>{
   const gap=8,viewport=window.innerWidth;
   main.style.maxWidth=Math.floor(viewport*.65-gap)+'px';
   side.style.maxWidth=Math.floor(viewport*.35)+'px';
   const r=main.getBoundingClientRect(),w=side.getBoundingClientRect().width;
   if(!r.width||!w)return;
   const origin=r.left-shift,freeRight=viewport-origin-r.width;
   const leftSide=origin>=w+gap||(freeRight<w+gap&&origin>=freeRight);
   const x=leftSide?Math.max(w+gap,Math.min(origin,viewport-r.width)):Math.max(0,Math.min(origin,viewport-r.width-w-gap));
   shift=x-origin;
   main.style.transform='translateX('+shift+'px)';
   side.style.left=(leftSide?x-w-gap:x+r.width+gap)+'px';
   side.style.right='auto';
   side.style.top=Math.max(0,r.top)+'px';
   side.style.maxHeight=Math.min(window.innerHeight*.70,Math.max(40,window.innerHeight-r.top-gap))+'px';
  };
  place();
  const timer=window.setInterval(place,150);
  window.addEventListener('resize',place);
  return()=>{window.clearInterval(timer);window.removeEventListener('resize',place);main.style.transform='';main.style.maxWidth='';};
 },[tab,s.open,p.collapsed,loading.locked,p.layout,p.position]);
 const [correctionsOpen,setCorrectionsOpen]=React.useState(false);
 const [parkingPicker,setParkingPicker]=React.useState(false);
 const [manage,setManage]=React.useState(false),[confirmNew,setConfirmNew]=React.useState(false);
 React.useEffect(()=>{setManage(false);setConfirmNew(false)},[s.mode,s.planMode,s.open,s.nativeActive]);
 if(!s.open)return null;
 const mini=(label:string,name:string,run:()=>void,disabled=false,active=false)=><Tooltip tooltip={t(label)}><Button className={styles.small+' '+(active?styles.selected:'')} aria-label={t(label)} disabled={disabled} selected={active} onSelect={run}><Glyph name={name}/></Button></Tooltip>;
 const action=(key:string,name:string,disabled=false,active=false)=><Tooltip tooltip={t(s.compoundEditing&&key==='nativePreview'?'AP.PreviewReplacement':s.compoundEditing&&key==='nativeApply'?'AP.ApplyReplacement':key==='finish'&&s.mode==='road'?'AP.RoadFinish':'AP.'+key)}><Button className={styles.tool+' '+(key==='nativeApply'&& !disabled?styles.confirmBuild+' ':'')+(active?styles.selected:'')+(((key==='finish'&&s.mode==='road')||(key==='finishEntrances'&&s.mode==='entrance'))&&!disabled?' '+styles.roadConfirm:'')} aria-label={t(s.compoundEditing&&key==='nativePreview'?'AP.PreviewReplacement':s.compoundEditing&&key==='nativeApply'?'AP.ApplyReplacement':key==='finish'&&s.mode==='road'?'AP.RoadFinish':'AP.'+key)} disabled={disabled} selected={active} onSelect={()=>command(key)}><Glyph name={name}/><span>{t(s.compoundEditing&&key==='nativePreview'?'AP.PreviewReplacement':s.compoundEditing&&key==='nativeApply'?'AP.ApplyReplacement':key==='finish'&&s.mode==='road'?'AP.RoadFinish':'AP.'+key)}</span></Button></Tooltip>;
 const check=(label:string,on:boolean,run:()=>void,help?:string,disabled=false)=><Tooltip tooltip={t(help||label)}><Switch label={t(label)} on={on} run={run} disabled={disabled}/></Tooltip>;
 const unit=(value:number,kind='area')=><span className={styles.quantity}><span>{kind==='percent'?value.toFixed(1):formatInteger(value)}</span><Glyph name={kind==='percent'?'percent':'square-metre'}/></span>;
 const info=(label:string)=>mini(label,'info',()=>{});
 const networks=s.networks||[];
 const networkName=(id?:string)=>id?(translate(`Assets.NAME[${id}]`,id==='Small Road'?t('AP.TwoLaneRoad'):(networks.find(n=>n.Id===id)?.Name||id))||id):t('AP.Network.NotReady');
 const networkWidth=(id?:string)=>roadWidthLabel(networks.find(n=>n.Id===id)?.Width,t('AP.Network.NotReady'));
 let recentNetworks:Record<string,unknown>={};try{const parsed=JSON.parse(s.recentNetworks||'{}');if(parsed&&typeof parsed==='object'&&!Array.isArray(parsed))recentNetworks=parsed;}catch{}
 const networkCache=new Map<string,typeof networks>();
 const networkChoices=(role='aisle')=>{
  if(networkCache.has(role))return networkCache.get(role)!;
  const selectedId=role==='main'?s.mainNetwork:role==='pedestrian'?s.walkNetwork:s.aisleNetwork;
  const width=networks.find(n=>n.Id===selectedId)?.Width??(role==='main'?16:role==='pedestrian'?3:8);
  const eligible=networks.filter(n=>assetMatches({Id:n.Id,Name:networkName(n.Id)},networkQuery)).filter(n=>role==='main'?n.Car&&n.Walk&&!n.Path&&n.BuildingAccess:role==='pedestrian'?n.Path&&n.Walk&&!n.Car:n.Internal);
  const result=orderNetworks(eligible,networkFilter,selectedId,recentNetworks[role],width,networkName);networkCache.set(role,result);return result;
 };
 const snaps=[['All',-1,snapAll],['Roads',0,snapNetSide],['Lots',2,snapLotGrid],['Directions',4,snapStraightDirection],['Buildings',1,snapObjectSide],['Guides',5,snapGuideLines],['Grid',3,snapZoneGrid]] as const;
 const snapOn=(index:number)=>index<0?[0,1,2,3,4,5].every(i=>!!s.snaps?.[i]):!!s.snaps?.[index];
 const stage=s.nativeActive||s.nativeCommitted?'preview':['boundary','edit','fill'].includes(s.mode||'')?'boundary':s.mode==='entrance'?'entrance':'preview';
 const stageTitle=stage==='boundary'?'AP.BoundaryStage':stage==='entrance'?'AP.EntranceStage':'AP.PreviewStage';
 const chosen=assets.filter(a=>selected.includes(a.Id));
 const statusKey=s.status||'AP.Draw';
 const showStatus=s.statusError||s.canForceDemolition||!['AP.Draw','AP.EditBoundary','AP.PickEntrance','AP.DrawZone','AP.PickBuilding','AP.ChooseReplacement','AP.RoadStart','AP.RoadControl','AP.RoadEnd','AP.FillReady','AP.CloseSnap','AP.RoadFinishAtEnd','AP.BoundaryDrag'].includes(statusKey);
 const showHint=['AP.FillEmpty','AP.ManualRoadFit','AP.ExistingEntrance','AP.EntranceNearbyAdvisory'].includes(hint)&&hint!==statusKey&&!(s.entranceClose&&hint==='AP.EntranceNearbyAdvisory');
 const repair=(key:string,glyph:string,run:()=>void)=><Button className={styles.tool} onSelect={run}><Glyph name={glyph}/><span>{t(key)}</span></Button>;
 const openRules=()=>{setParkingPicker(false);setTab('rules')};
 const flowAction=()=>{
  if(!s.entrances)return villa?repair('AP.Villa.Detect','refresh',()=>command('villaDetect')):action('finishEntrances','right',true);
  if(!selected.length)return repair('AP.Flow.Assets','library',()=>{setSelectedOnly(false);setQuery('');document.getElementById('ap-section-assets')?.scrollIntoView()});
  if(tower&&!assets.some(a=>a.Id===s.tower?.parking))return repair('AP.Flow.Parking','library',()=>{setParkingPicker(true);setTab('rules')});
  if(!tower&&statusKey==='AP.NetworkUnavailable')return repair('AP.Networks','road',()=>{setNetworkOpen('main');openRules()});
  if(statusKey==='AP.RoadPackMissing')return action('rebuild','refresh',true);
  if(!s.hasDraft||s.layoutOutdated)return action('rebuild','refresh',!!s.calculating);
  if(s.mode==='entrance')return action('finishEntrances','right',!s.canReviewLayout);
  if(!s.canSave&&(s.accessFailures||['AP.NoBuildingFit','AP.NeedBuildings','AP.Villa.NoConnection'].includes(statusKey)))return repair('AP.MoreRules','right',openRules);
  return action('nativePreview','building',!s.canSave);
 };
 const workflow=<>
   <div className={styles.planTabs} role="tablist" aria-label={t('AP.PlanType')}>{[0,2,1].map(mode=><Button key={mode} role="tab" aria-selected={(s.planMode||0)===mode} selected={(s.planMode||0)===mode} className={styles.planTab+' '+((s.planMode||0)===mode?styles.planTabActive:'')} disabled={!!s.nativeActive} onSelect={()=>{command('planMode',String(mode));setQuery('');setPage(0)}}><span>{t(mode===0?'AP.ApartmentTab':mode===2?'AP.Tower.Tab':'AP.VillaTab')}</span></Button>)}</div>

   <div className={styles.steps}>{['boundary','entrance','preview'].map((k,i)=><React.Fragment key={k}>{i>0&&<Glyph name="right"/>}<span className={stage===k?styles.current:''} aria-current={stage===k?'step':undefined}><b>{i+1}</b>{t('AP.Step.'+k)}</span></React.Fragment>)}</div>
   <div data-ap-tools="true" className={styles.flowHead}><strong>{t(s.nativeActive?(s.nativeDemolition?'AP.Demolish':'AP.nativePreview'):s.nativeCommitted?'AP.LoadingComplete':s.mode==='road'?'AP.road':s.mode==='zone'?'AP.ZoneStage':s.mode==='replace'?'AP.BuildingEditor':villa&&stage==='entrance'&&s.entrances?'AP.Flow.Gates':stageTitle)}{villa&&stage==='entrance'&&!!s.entrances?' · '+s.entrances:''}</strong>
    <div>{(s.mode==='boundary'||s.mode==='entrance')&&!s.nativeActive&&!s.nativeCommitted&&<>{mini(s.mode==='boundary'?'AP.UndoPoint':'AP.UndoEntrance','undo',()=>command('undo'),!(s.mode==='boundary'?s.points:s.entrances))}{mini(s.mode==='boundary'?'AP.RedoPoint':'AP.RedoEntrance','redo',()=>command('redo'),!s.canRedo)}</>}
    {!s.nativeActive&&<Button className={styles.detailOpen} aria-label={t('AP.Manage')} aria-expanded={manage} onSelect={()=>{setManage(!manage);setConfirmNew(false)}}><Glyph name={manage?'up':'down'}/></Button>}</div></div>
   {manage&&<div className={styles.manageMenu}>
    {confirmNew?<>{action('new','new')}<Button onSelect={()=>setConfirmNew(false)}>{t('AP.UI.Close')}</Button></>:<Button onSelect={()=>setConfirmNew(true)}><Glyph name="new"/>{t('AP.new')}</Button>}
    {!s.nativeCommitted&&<>{action('edit','edit',(s.points||0)<3)}{action('entrance','entrance',!s.boundaryValid)}{!villa&&!tower&&action('road','road',!s.entrances)}{!villa&&!tower&&action('pickBuilding','replace',!s.canSave)}</>}
    {mini('AP.save','save',()=>command('save'),!s.canSave)}{mini('AP.DeleteEditingPlan','trash',()=>command('confirmDelete'),!s.canDelete)}
   </div>}
   <div className={styles.flowNav}>
   {s.nativeActive?<>{mini('AP.nativeCancel','left',()=>command('nativeCancel'))}{action(s.nativeDemolition?'nativeDemolishApply':'nativeApply',s.nativeDemolition?'trash':'building',!s.nativeValid&&!(s.nativeDemolition&&s.canForceDemolition))}</>:s.nativeCommitted?action('new','new'):s.mode==='entrance'?<>{mini('AP.BackBoundary','left',()=>command('edit'))}{flowAction()}</>:s.mode==='preview'?<>{mini('AP.BackEntrance','left',()=>command('entrance'))}{flowAction()}</>:s.mode==='replace'?action('undo','left'):action('finish','right',s.mode==='zone'?(s.zonePoints||0)<3:s.mode==='road'?!!s.calculating:!s.boundaryValid)}
   {!s.nativeActive&&!s.nativeCommitted&&!['entrance','preview'].includes(s.mode||'')&&!!s.entrances&&(s.autoCalculate===false||s.layoutOutdated)&&mini('AP.rebuild','refresh',()=>command('rebuild'))}
   </div>
   {s.canFill&&['boundary','fill'].includes(s.mode||'')&&<div className={styles.fillTools}>{check('AP.Fill',s.mode==='fill',()=>command('fill'),'AP.FillHint')}{s.mode==='fill'&&<Parameter label={t('AP.FillRadius')} value={s.fillRadius||250} min={50} max={600} step={10} commandName="fillRadius"/>}</div>}
   {s.mode==='road'&&<div data-ap-road-tools><div className={styles.roadModes} role="group" aria-label={t('AP.RoadMode')}>
    {mini('AP.RoadStraight','straight',()=>command('roadShape','straight'),!!s.calculating,!s.roadCurved)}
    {mini('AP.RoadCurve','curve',()=>command('roadShape','curve'),!!s.calculating,!!s.roadCurved)}
    <span className={styles.roadCount}>{t('AP.RoadPendingCount')} {s.pendingRoads||0}</span>
    
    {mini('AP.RoadCancel','close',()=>command('roadCancel'),!!s.calculating)}
   </div><Tooltip tooltip={t('AP.RoadNetworkHint')}><Button className={styles.disclosure} onSelect={()=>{setTab('rules');setNetworkOpen('main')}}><Glyph name="road"/><div className={styles.networkLabel}><small>{t('AP.Network.main')}</small><span>{networkName(s.mainNetwork)}</span></div><b className={styles.networkWidth}>{networkWidth(s.mainNetwork)}</b></Button></Tooltip></div>}
   {!s.nativeActive&&!s.nativeCommitted&&['boundary','edit','fill','road','zone'].includes(s.mode||'')&&<div data-ap-snaps="true" className={styles.snaps} aria-label={t('AP.UI.Snapping')}>{snaps.map(([key,i,glyph])=><Tooltip key={key} tooltip={t('AP.Snap.'+key)}><Button className={styles.snap+' '+(snapOn(i)?styles.selected:'')} aria-label={t('AP.Snap.'+key)} selected={snapOn(i)} aria-pressed={snapOn(i)} onSelect={()=>command('snap',key)}><img className={styles.glyph} src={glyph} alt=""/></Button></Tooltip>)}</div>}
   {((!s.nativeActive&&stage==='boundary'&&(s.points||0)>=3&&!s.boundaryValid)&&statusKey!=='AP.InvalidBoundary')&&<div className={styles.status}>{t('AP.InvalidBoundary')}</div>}
   {stage==='entrance'&&!s.entrances&&!s.nativeActive&&statusKey==='AP.PickEntrance'&&<div className={styles.measure}>{t('AP.Flow.AddEntrance')}</div>}
   {showStatus&&<div role={s.statusError?"alert":"status"} className={styles.status+" "+(s.statusError?styles.statusError:"")}><Glyph name={s.statusError?'warning':'info'}/><span>{t(statusKey).replace('{current}',formatInteger(Math.ceil(s.limit?.current??0))).replace('{maximum}',formatInteger(s.limit?.maximum??0))}</span>{s.statusError?mini(s.diagnosticCopyState===1?'AP.DiagnosticsCopied':s.diagnosticCopyState===-1?'AP.DiagnosticsCopyFailed':'AP.CopyDiagnostics',s.diagnosticCopyState===1?'check':'copy',()=>command('copyDiagnostics')):info('AP.Keys')}{mini(s.diagnosticShareStatus||'AP.ShareDiagnostics',s.diagnosticShareStatus==='AP.ShareSent'?'check':'share',()=>command('shareDiagnostics'),s.diagnosticShareBusy)}{s.canForceDemolition&&mini('AP.ForceDemolish','trash',()=>command('nativeForceDemolition'))}</div>}
   {s.diagnosticShareStatus&&s.diagnosticShareStatus!=='AP.ShareDiagnostics'&&<div role="status" aria-live="polite" className={styles.status}><Glyph name={s.diagnosticShareStatus==='AP.ShareSent'?'check':'info'}/><span>{t(s.diagnosticShareStatus)}</span></div>}
   {s.statusError&&Object.keys(s.layoutReasons||{}).length>0&&<div className={styles.status}><span>{t('AP.LayoutReason.Counts')} {Object.entries(s.layoutReasons||{}).map(([key,count])=>`${t(key)}: ${count}`).join(' · ')}</span></div>}
   {s.entranceClose&&<div role="status" className={styles.measure}>{t('AP.EntranceNearbyAdvisory')}</div>}
   {showHint&&<div role="status" className={styles.measure}>{t(hint)}</div>}
   {s.suggestCleanup&&(s.saved||[]).some(v=>v.cleanupShapes&&v.canDelete)&&<div>
    <Button className={styles.disclosure} selected={showCleanup} aria-expanded={showCleanup} onSelect={()=>setShowCleanup(!showCleanup)}><Glyph name="library"/><span>{t('AP.CleanupOffer')}</span></Button>
    {showCleanup&&(s.saved||[]).filter(v=>v.cleanupShapes&&v.canDelete).sort((a,b)=>b.cleanupShapes!-a.cleanupShapes!).map(v=><Button key={v.id} className={styles.disclosure} onSelect={()=>command('confirmDelete',String(v.id))}><Glyph name="trash"/><span>{v.name||t('AP.UI.Plan')+' '+v.id}</span></Button>)}
   </div>}
   {(s.nativeActive||s.nativeAuditAvailable)&&visibleNotice(s)&&<div className={styles.nativeReport+" "+(s.statusError?styles.statusError:"")}><div className={styles.nativeNotice}>{t(s.nativeNotice!)}</div></div>}
 </>;
 const rules=<>
   <div className={styles.switchRow}>{check('AP.AutoCalculate',s.autoCalculate!==false,()=>command('autoCalculate'),'AP.AutoCalculateHint',!!s.nativeActive||!!s.nativeCommitted||!!s.pendingRoads)}{s.layoutOutdated&&!s.calculating&&<span className={styles.calculationNeeded}>{t('AP.CalculationNeeded')}</span>}</div>
   <div id="ap-section-rules" className={styles.sectionTitle}><span>{t('AP.UI.Tab.rules')}</span><Button className={styles.detailOpen} onSelect={()=>{setParkingPicker(false);setTab(tab==='rules'?'assets':'rules')}}>{t('AP.MoreRules')}<Glyph name="right"/></Button></div>
   <div className={styles.primarySliders}>{tower?<>{(s.tower?.rules||[]).filter(r=>['BuildingGap','Angle','BuildingBoundary'].includes(r.id)).map(r=><TowerRule key={r.id} rule={r} locked={!!s.nativeActive||!!s.nativeCommitted} label={t('AP.Tower.Rule.'+r.id)}/>)}</>:villa?<>
    <Parameter label={t('AP.Villa.Gap')} value={s.villaGap??0} min={-6} max={6} step={.5} commandName="villaGap"/>
    <Parameter label={t('AP.Villa.MainSpacing')} value={s.villaMainSpacing??120} min={60} max={300} step={10} commandName="villaMainSpacing"/>
    <Parameter label={t('AP.Villa.Boundary')} value={s.villaBoundary??2} min={0} max={12} step={.5} commandName="villaBoundary"/></>:<>
    <Parameter label={t('AP.Option.MainParkingSetback')} value={s.mainSetback??4} min={0} max={20} step={1} commandName="mainSetback"/>
    <Parameter label={t('AP.Option.BoundaryGreenMargin')} value={s.boundaryMargin??4} min={0} max={20} step={1} commandName="boundaryMargin"/>
    <Parameter label={t('AP.Option.MainRoadCurveRadius')} value={s.curveRadius||40} min={8} max={200} step={1} commandName="curveRadius"/></> }</div>
 </>;
 const assetBrowser=<>
   <div id="ap-section-assets"><div className={styles.sectionTitle}><span>{t('AP.Assets')}</span><b>{t('AP.Flow.Selected').replace('{n}',String(selected.length))}</b></div><div className={styles.searchRow}><Glyph name="search"/><input className={styles.search} value={query} aria-label={t('AP.Search')} placeholder={t('AP.Search')} onChange={e=>setQuery(e.target.value)} onKeyDown={e=>e.stopPropagation()}/>{query&&mini('AP.UI.Clear','close',()=>setQuery(''))}</div>
    <div className={styles.assetFilters}>{check('AP.UI.AllBuildings',all,()=>setAll(!all),'AP.AllBuildings')}{check('AP.UI.SelectedOnly',selectedOnly,()=>setSelectedOnly(!selectedOnly))}</div><Button className={styles.disclosure} aria-label={t('AP.UI.ClearAssets')} disabled={!selected.length} onSelect={()=>command('clearAssets')}><Glyph name="close"/><span>{t('AP.UI.ClearAssets')}</span></Button>
    {villa&&<Button className={styles.disclosure} onSelect={()=>command('villaSelect')}>{t('AP.Villa.Select')}</Button>}
    <div data-ap-assets="true" className={styles.assetList}>{matches.slice(current*assetsPerPage,(current+1)*assetsPerPage).map(a=><Tooltip key={a.Id} tooltip={<div className={styles.tooltipText}><strong>{a.Name||a.Id}</strong><div className={styles.tooltipMeasure}>{`${a.Width} × ${a.Depth} m`}</div><div className={styles.tooltipMeasure}>{`${a.Height.toFixed(1)} m`}</div>{villa&&<div>{t('AP.Villa.Geometry')}: <span className={styles.tooltipMeasure}>{`${(a.GeometryWidth||0).toFixed(1)} × ${(a.GeometryDepth||0).toFixed(1)} m`}</span></div>}<div>{formatInteger(a.Homes)} {t('AP.Homes')}</div></div>}><Button aria-label={a.Name||a.Id} className={styles.asset+' '+(selected.includes(a.Id)?styles.selected:'')} selected={selected.includes(a.Id)} onSelect={()=>command('select',a.Id)}><img className={styles.assetImage} src={a.Icon||icon} alt="" onError={e=>{if(!e.currentTarget.dataset.fallback){e.currentTarget.dataset.fallback='1';e.currentTarget.src=icon;}}}/><span className={styles.assetName}>{a.Name||a.Id}</span><span className={styles.assetSize}>{a.Width}<Glyph name="multiply"/>{a.Depth} m</span>{selected.includes(a.Id)&&<span className={styles.badge}><Glyph name="check"/></span>}</Button></Tooltip>)}</div>
    {!matches.length&&<div className={styles.empty}>{t('AP.UI.NoMatches')}</div>}
    <div className={styles.pages}>{mini('AP.UI.Previous','left',()=>setPage(current-1),current===0)}<span className={styles.pageNumber}><span>{current+1}</span><Glyph name="slash"/><span>{pages}</span></span><span>{matches.length} {t('AP.UI.Results')}</span>{mini('AP.UI.Next','right',()=>setPage(current+1),current+1>=pages)}</div>
    {!!chosen.length&&<Button className={styles.disclosure} aria-expanded={correctionsOpen} onSelect={()=>setCorrectionsOpen(!correctionsOpen)}><Glyph name={correctionsOpen?'down':'right'}/>{t('AP.Corrections')}</Button>}
    {correctionsOpen&&chosen.map(a=><div key={a.Id}><Button className={styles.disclosure} aria-expanded={correction===a.Id} onSelect={()=>setCorrection(correction===a.Id?null:a.Id)}><Glyph name={correction===a.Id?'down':'right'}/><span>{a.Name||a.Id}</span></Button>{correction===a.Id&&<AssetEditor key={a.Id+':'+a.Height+':'+a.Footprint+':'+a.Homes} asset={a} t={t}/>}</div>)}
   </div>
 </>;
 const warnings=<>
    {!!s.parkingAccessFailures&&<div className={styles.warning}><Glyph name="warning"/><span>{t('AP.ParkingAccessIncomplete')} {s.parkingAccessFailures}</span></div>}{!!s.accessFailures&&<div className={styles.warning}><Glyph name="warning"/><span>{t('AP.AccessIncomplete')} {s.accessFailures}</span></div>}
    {!!s.shortfall?.some(v=>v>1)&&<><div className={styles.sectionTitle}>{t('AP.Deficit')}</div><div className={styles.deficitGrid}>{s.shortfall.map((v,k)=>v>1?<div className={styles.deficit} key={k}><span>{t(k===0?'AP.ParkingSpaces':'AP.Kind.'+k)}</span><b>{k===0?formatInteger(Math.ceil(v)):unit(Math.ceil(v))}</b></div>:null)}</div></>} </>;
 const scrollColumn=(children:React.ReactNode)=><div className={styles.workspaceColumn}><Scrollable className={styles.scrollViewport} vertical horizontal={false} trackVisibility="always" smooth={false}><div className={styles.body}>{children}</div></Scrollable></div>;
 return <>{s.mode==='replace'&&s.replacement&&<div className={styles.panel+' '+styles.buildingEditor} role="dialog" aria-label={t('AP.BuildingEditor')} style={{left:Math.min(68,Math.max(1,(s.buildingEditor?.x??.35)*100))+'%',top:Math.min(58,Math.max(2,(s.buildingEditor?.y??.35)*100))+'%'}} onMouseEnter={p.enter} onMouseLeave={p.leave} onFocusCapture={p.focus} onBlurCapture={p.blur}>
 <div className={styles.editorTitle}><strong>{t('AP.BuildingEditor')}</strong>{mini('AP.UI.Close','close',()=>command('dismissBuilding'))}</div>
 <div className={styles.editorAssets}>{chosen.map(a=><Button key={a.Id} aria-label={a.Name||a.Id} selected={s.replacement===a.Id} className={styles.asset+' '+(s.replacement===a.Id?styles.selected:'')} disabled={loading.locked} onSelect={()=>command('replaceAsset',a.Id)}><img className={styles.assetImage} src={a.Icon||icon} alt=""/><span className={styles.assetName}>{a.Name||a.Id}</span><span>{a.Width} × {a.Depth} m</span></Button>)}</div>
 <div role="status" className={styles.editorStatus}><span>{t(s.status||'AP.ChooseReplacement')}</span>{s.canForceBuilding&&<Button className={styles.forceChip} disabled={loading.locked} onSelect={()=>command('forceBuilding')}>{t('AP.ForceBuilding')}</Button>}</div>
 </div>}<PanelDismiss close={()=>{if(!loading.locked){if(tab!=='assets')setTab('assets');else command(s.landscape?.open?'landscapeClose':'close')}}}/><div ref={p.shell} data-ap-panel="true" role="region" aria-label="Apartment Plan" className={styles.panel+' '+((!p.collapsed||loading.locked)?styles.expanded:'')+' '+(p.layout&1?styles.wide:'')} style={p.position?{left:p.position.x,top:p.position.y,right:'auto'}:undefined} onMouseEnter={p.enter} onMouseLeave={p.leave} onFocusCapture={p.focus} onBlurCapture={p.blur} {...loading.events}>
  <div className={styles.header} onMouseDown={p.start}><strong>Apartment Plan</strong><div className={styles.headerActions}>{mini('AP.SavedPlans','list',()=>setTab(tab==='saved'?'assets':'saved'),false,tab==='saved')}{mini('AP.Landscape','park',()=>command(s.landscape?.open?'landscapeClose':'landscapeOpen'),false,!!s.landscape?.open)}{mini('AP.UI.Wide','layout',()=>p.setLayout(p.layout^1),false,!!(p.layout&1))}{mini(p.collapsed?'AP.UI.Expand':'AP.UI.Collapse',p.collapsed?'down':'up',()=>p.setCollapsed(!p.collapsed))}{mini('AP.UI.Close','close',()=>command('close'))}</div></div>
  {!p.collapsed&&<div className={styles.metrics}><Tooltip tooltip={t('AP.PopulationHint')+' '+(s.householdAverage||0).toFixed(2)}><div><span>{t('AP.Population')}</span><strong>{!s.hasDraft?'—':s.population==null?t('AP.NoPopulationData'):formatInteger(s.population)}</strong></div></Tooltip><div><span>{t('AP.Far')}</span><strong>{s.hasDraft?unit(s.far||0,'percent'):'—'}</strong></div><div><span>{t('AP.Homes')}</span><strong>{s.hasDraft?formatInteger(s.homes||0):'—'}</strong></div></div>}
  {(!p.collapsed||loading.locked)&&(p.layout&1
   ? <div className={styles.workspace}>
      {scrollColumn(<>{workflow}{rules}{warnings}</>)}
      {scrollColumn(assetBrowser)}
     </div>
   : <Scrollable className={styles.scrollViewport} vertical horizontal={false} trackVisibility="always" smooth={false}><div className={styles.body}>{workflow}<div className={styles.content}>{rules}{assetBrowser}{warnings}</div></div></Scrollable>)}
 <Loading s={s} loading={loading} t={t}/>
 </div>{!p.collapsed&&!loading.locked&&(tab==='rules'||tab==='saved')&&<div ref={sideRef} data-ap-panel="side" className={styles.panel+' '+styles.auxPanel} onMouseEnter={p.enter} onMouseLeave={p.leave} onFocusCapture={p.focus} onBlurCapture={p.blur}>
 <div className={styles.header}><strong>{t(tab==='rules'?'AP.MoreRules':'AP.SavedPlans')}</strong>{mini('AP.UI.Close','close',()=>setTab('assets'))}</div><Scrollable className={styles.scrollViewport} vertical horizontal={false} trackVisibility="always" smooth={false}><div className={styles.body}>
   {tab==='rules'&&<>{s.planMode===0&&check('AP.Option.GenerateSurfaceParking',s.generateSurfaceParking!==false,()=>command('surfaceParking'),'AP.Desc.GenerateSurfaceParking',!!s.nativeActive||!!s.nativeCommitted)}{check('AP.ParkingInOpenSpace',!!s.parkingInOpenSpace,()=>command('parkingInOpenSpace'),'AP.ParkingInOpenSpaceHint',s.generateSurfaceParking===false||!!s.nativeActive||!!s.nativeCommitted)}</>}
   {tab==='rules'&&tower&&<><TowerControls key={String(parkingPicker)} parkingPicker={parkingPicker} s={s} assets={assets}/></>}
   {tab==='rules'&&!tower&&<><div className={styles.sectionTitle}>{t('AP.Networks')}</div>{(villa?[['main',s.mainNetwork]]:[['main',s.mainNetwork],['aisle',s.aisleNetwork],['pedestrian',s.walkNetwork]]).map(([role,id])=><div key={role}><Button className={styles.disclosure} onSelect={()=>setNetworkOpen(networkOpen===role?'':role||'')}><Glyph name={networkOpen===role?'down':'right'}/><div className={styles.networkLabel}><small>{t('AP.Network.'+role)}</small><span>{networkName(id)}</span></div><b className={styles.networkWidth}>{networkWidth(id)}</b></Button>{networkOpen===role&&<div className={styles.networkList}><div className={styles.searchRow}><Glyph name="search"/><input className={styles.search} value={networkQuery} aria-label={t('AP.SearchRoads')} placeholder={t('AP.SearchRoads')} onChange={e=>setNetworkQuery(e.target.value)} onKeyDown={e=>e.stopPropagation()}/></div><div className={styles.switchRow}>{check('AP.Network.RoadBuilderOnly',networkFilter==='roadBuilder',()=>setNetworkFilter(networkFilter==='roadBuilder'?'all':'roadBuilder'))}{check('AP.Network.BuiltinOnly',networkFilter==='builtin',()=>setNetworkFilter(networkFilter==='builtin'?'all':'builtin'))}</div>{!networkChoices(role).length&&<div className={styles.empty}>{t('AP.Network.Empty')}</div>}<div className={styles.assetList}>{networkChoices(role).slice(Math.min(networkPage,Math.max(0,Math.ceil(networkChoices(role).length/8)-1))*8,Math.min(networkPage,Math.max(0,Math.ceil(networkChoices(role).length/8)-1))*8+8).map(n=><Tooltip key={n.Id} tooltip={networkName(n.Id)+(n.RoadBuilder?' · Road Builder':'')}><Button aria-label={networkName(n.Id)} className={styles.asset+' '+(id===n.Id?styles.selected:'')} selected={id===n.Id} disabled={s.nativeActive} onSelect={()=>command('network',role+'|'+n.Id)}><img className={styles.assetImage} src={n.Icon||glyphs.road} alt="" onError={e=>{if(!e.currentTarget.dataset.fallback){e.currentTarget.dataset.fallback='1';e.currentTarget.src=glyphs.road;}}}/><span className={styles.assetName}>{networkName(n.Id)}</span><span className={styles.assetSize}>{roadWidthLabel(n.Width,t('AP.Network.NotReady'))}{n.RoadBuilder?' · RB':''}</span>{id===n.Id&&<span className={styles.badge}><Glyph name="check"/></span>}</Button></Tooltip>)}</div><div className={styles.pages}>{mini('AP.Previous','left',()=>setNetworkPage(Math.max(0,networkPage-1)),networkPage===0)}<span>{Math.min(networkPage+1,Math.max(1,Math.ceil(networkChoices(role).length/8)))} / {Math.max(1,Math.ceil(networkChoices(role).length/8))}</span>{mini('AP.Next','right',()=>setNetworkPage(networkPage+1),(networkPage+1)*8>=networkChoices(role).length)}</div></div>}</div>)}{villa?<><div className={styles.sectionTitle}>{t('AP.Villa.Rules')}</div>
    {check('AP.Villa.CentralRoad',s.villaCentralRoad!==false,()=>command('villaCentralRoad'))}
    {s.villaExistingRoadsOnly&&<div>{t('AP.Villa.ExistingRoads')} · {Math.round(s.villaRoadDensity||0)} m/ha</div>}
    {check('AP.Villa.FollowBoundary',s.villaFollowBoundary!==false,()=>command('villaFollowBoundary'))}


    <Parameter label={t('AP.Villa.Rows')} value={s.villaRows??2} min={1} max={2} step={1} commandName="villaRows" unit=""/>

    <Parameter label={t('AP.Villa.MaxBuildings')} value={s.villaMaxBuildings??500} min={10} max={1000} step={10} commandName="villaMaxBuildings" unit=""/>
    <Parameter label={t('AP.Villa.ConnectionDistance')} value={s.villaConnectionDistance??4} min={1} max={8} step={.5} commandName="villaConnectionDistance"/>
    <Button className={styles.disclosure} onSelect={()=>command('villaDetect')}>{t('AP.Villa.Detect')}</Button>
    
    {(s.villaConnections||[]).map((c,i)=><Button key={i} className={styles.disclosure+' '+(!c.Disabled?styles.selected:'')} selected={!c.Disabled} onSelect={()=>command('villaToggle',String(i))}><div className={styles.connectionLabel}>{`${t(c.Junction?'AP.Villa.Junction':'AP.Villa.Connection')} ${i+1} · ${t('AP.Villa.Side')} ${c.Side+1}`}</div><b className={styles.connectionState}>{t(c.Disabled?'AP.Villa.Excluded':'AP.Villa.Enabled')}</b></Button>)}
    <Button className={styles.disclosure} onSelect={()=>command('landscapeOpen')}>{t('AP.Landscape')}</Button>
    </>:<><div className={styles.sectionTitle}><span>{t('AP.UI.Rules')}</span>{info('AP.UI.OptionsHint')}</div><div className={styles.ruleChecks} data-ap-rule-checks>{check('AP.UI.BuildingRoad',!!s.requireBuildingRoad,()=>command('buildingRoad'),'AP.BuildingRoadScope',true)}</div>
    <Tooltip tooltip={t("AP.Desc.MainRoadCornerMode")}><Button className={styles.disclosure} onSelect={()=>command("cornerMode")}><Glyph name="angle"/><span>{`${t("AP.Option.MainRoadCornerMode")}: ${t("AP.CornerMode."+(s.cornerMode||0))}`}</span></Button></Tooltip>
    <div className={styles.sectionTitle}>{t('AP.BuildingPattern')}</div>
    <div className={styles.patternChoices} role="group" aria-label={t('AP.BuildingPattern')}>{['grid','barsHorizontal','barsVertical','barsMixed'].map((glyph,i)=><Tooltip key={i} tooltip={t('AP.BuildingPatternHint')}><Button selected={(s.buildingPattern||0)===i} className={styles.tool+' '+((s.buildingPattern||0)===i?styles.selected:'')} disabled={!!s.nativeActive||!!s.nativeCommitted} onSelect={()=>command('buildingPattern',String(i))}><Glyph name={glyph}/><span>{t('AP.BuildingPattern.'+i)}</span></Button></Tooltip>)}</div>
    {check('AP.AllowMainTJunctions',s.mainTJunctions!==false,()=>command('mainTJunctions'),'AP.AllowMainTJunctionsHint',!!s.nativeActive||!!s.nativeCommitted)}


    <Parameter label={t('AP.Option.MainRoadCurveLengthPercent')} value={s.curveLength??50} min={25} max={100} step={5} commandName="curveLength" unit="percent"/>

    <Parameter label={`${t('AP.Option.ParkingLineOverlap')} (${(s.parkingPitch??2.85).toFixed(2)} m)`} value={s.lineOverlap??.15} min={0} max={.3} step={.01} commandName="lineOverlap"/>
    {!!s.parkingGreen&&<Parameter label={t('AP.Option.ParkingIslandWidth')} value={s.parkingGreenWidth??3} min={0} max={6} step={1} commandName="parkingGreenWidth"/>}
</>}
    <div className={styles.switchRow+' '+(!villa?styles.switchThree:'')}>
    {check('AP.GrassSurfaces',s.grassSurfaces===true,()=>command('grassSurfaces'),'AP.GrassSurfacesHint',!!s.nativeCommitted)}
    {check(villa?'AP.Villa.TileLots':'AP.TileLots',s.lotTiles!==false,()=>command('lotTiles'),villa?'AP.Villa.TileLotsHint':'AP.TileLotsHint',!!s.nativeCommitted)}
    {!villa&&check('AP.Option.ParkingGreenIslands',!!s.parkingGreen,()=>command('parkingGreen'),'AP.Desc.ParkingGreenIslands')}
    </div>
    <Parameter label={t('AP.SurfaceRadius')} value={s.surfaceRadius??2} min={0} max={4} step={.5} commandName="surfaceRadius"/>
    {!villa&&<Parameter label={t('AP.EntryLength')} value={s.entrance||15} min={3} max={99} step={1} commandName="entranceLength"/>}
    <div className={styles.sectionTitle}><span>{t('AP.Zones')}</span><b>{s.zones||0}</b><span className={styles.countLabel}>{t('AP.Entrances')}</span><b>{s.entrances||0}</b></div>
    <div className={styles.zoneTools}>{[3,5,4].map((k,i)=><Tooltip key={k} tooltip={t('AP.Zone.'+k)}><Button className={styles.zone} selected={s.mode==='zone'} disabled={(s.points||0)<3} onSelect={()=>command('zone',String(k))}><Glyph name={['play','exclude','park'][i]}/><span>{t('AP.Zone.'+k)}</span></Button></Tooltip>)}{mini('AP.RemoveZone','undo',()=>command('removeZone'),!s.zones)}</div>

   </>}
   {tab==='saved'&&<><div className={styles.sectionTitle}><span>{t('AP.SavedPlans')}</span>{mini('AP.DeleteAllPlans','trash',()=>command('deleteAllPlans'),!s.canDeleteAllPlans)}{info('AP.SaveHint')}</div>{!(s.saved||[]).length&&<div className={styles.empty}>{t('AP.UI.NoSaved')}</div>}{(s.saved||[]).map(v=><div key={v.id} className={styles.savedRow}><Tooltip tooltip={t('AP.InspectPlan')}><Button className={styles.disclosure} onSelect={()=>command('inspectPlan',String(v.id))}><Glyph name="library"/><span>{v.name||t('AP.UI.Plan')+' '+v.id}</span><Glyph name="right"/></Button></Tooltip><Tooltip tooltip={t('AP.DeletePlanHint')}><Button className={styles.deletePlan} aria-label={t('AP.DeleteEditingPlan')} disabled={!v.canDelete} onSelect={()=>command('confirmDelete',String(v.id))}><Glyph name="trash"/></Button></Tooltip></div>)}</>}
 </div></Scrollable></div>}</>;
}
const diagramDefaults:Record<string,number>={fillRadius:250,villaGap:0,villaMainSpacing:120,villaBoundary:2,mainSetback:4,boundaryMargin:4,curveRadius:40,villaRows:2,villaMaxBuildings:500,villaConnectionDistance:4,curveLength:50,lineOverlap:.15,parkingGreenWidth:3,surfaceRadius:2,entranceLength:15,landscapeDensity:12};
function DimensionDiagram({kind,label,example,reference,unit}:{kind:string;label:string;example:number;reference:number;unit:string}){
 const {translate}=useLocalization();
 const radius=['curveRadius','surfaceRadius','RoadRadius','WalkRadius'].includes(kind),angle=['Angle','WalkApproachAngle'].includes(kind),boundary=['BuildingBoundary','boundaryMargin','villaBoundary'].includes(kind);
 const extent=Math.max(1,Math.abs(example),Math.abs(reference));
 const scale=radius?68/extent:48/extent;
 const accent='#ffce79',building='#91b5d2';
 function drawing(v:number){
  const length=v*scale,r=Math.abs(length),cx=14+r,cy=12+r;
  const radians=v*Math.PI/180;
  const gridStep=radius?scale*Math.pow(10,Math.floor(Math.log10(extent))):14;
  const ticks=(x:number,y:number,end:number)=><path d={`M${x} ${y}H${end} M${x} ${y-4}v8 M${end} ${y-4}v8`} stroke={accent} strokeWidth="1.7"/>;
  const face=boundary?28:54,edge=face+length;
  return <svg viewBox="0 0 144 98" aria-hidden="true">
   <rect width="144" height="98" rx="4" fill="#1e2b33"/>
   <path d={Array.from({length:Math.ceil(144/gridStep)},(_,j)=>`M${14+j*gridStep} 0v98 M0 ${12+j*gridStep}h144`).join(' ')} stroke="#344750" strokeWidth=".65"/>
   {detailScene(kind,v,reference,example)??(kind==='mainSetback'?<>
    <rect x="8" y="4" width="30" height="90" rx="2" fill="#506371"/><path d="M23 7v84" stroke="#b2c3ca" strokeDasharray="5 4"/>
    <rect x={38+length} y="16" width="35" height="23" fill={building}/><rect x={38+length+3} y="19" width="29" height="17" fill="none" stroke="#566e81"/>
    <path d={`M${38+length} 47h29v12h-29v12h29v-24`} fill="none" stroke={building} strokeWidth="2"/>
    <rect x="38" y="12" width={length} height="64" fill={accent} opacity=".14"/>{ticks(38,80,38+length)}
    <path d={`M38 70v14 M${38+length} 70v14`} stroke={accent}/></>:
    kind==='fillRadius'?<><circle cx="72" cy="49" r={r*.85} fill="#355560" stroke={building}/><circle cx="72" cy="49" r="2" fill={accent}/>{ticks(72,49,72+r*.85)}</>:
    ['PackingStep','WalkCell','villaMainSpacing','ParallelGap','WalkParallelGap','RoadWalkGap'].includes(kind)?<>
     {[0,1].map(i=><path key={i} d={`M8 ${22+i*length}h128`} stroke={building} strokeWidth="4"/>)}<path d={`M80 22v${length}m-5 0h10 M75 22h10`} stroke={accent} strokeWidth="2"/></>:
    kind==='villaRows'?<>{Array.from({length:Math.round(v)},(_,j)=>[0,1,2,3].map(i=><rect key={j+'-'+i} x={14+i*30} y={14+j*32} width="20" height="22" fill={building}/>))}</>:
    ['Height','FacilityDepth','FacilityWidth','Footprint'].includes(kind)?<><rect x="24" y={kind==='Height'?80-length:22} width={kind==='FacilityDepth'?45:kind==='Height'?44:kind==='Footprint'?Math.sqrt(v/extent)*70:length} height={kind==='FacilityDepth'||kind==='Height'?length:kind==='Footprint'?Math.sqrt(v/extent)*58:35} fill={building}/><path d="M12 80h120" stroke={accent}/></>:
    kind==='landscapeDensity'?<>{Array.from({length:Math.round(48*v/extent)},(_,i)=><circle key={i} cx={12+(i*53)%121} cy={12+(i*31)%73} r="4" fill="#83b9a3"/>)}</>:
    kind==='lineOverlap'?<><rect x="18" y="21" width="60" height="52" fill={building}/><rect x={78-length} y="21" width="60" height="52" fill="#76968f"/><rect x={78-length} y="21" width={length} height="52" fill={accent} opacity=".7"/>{ticks(78-length,81,78)}</>:
    kind==='parkingGreenWidth'?<><rect x="8" y="22" width="128" height="52" fill="#52636e"/><rect x={72-length/2} y="22" width={length} height="52" fill="#81b593"/>{ticks(72-length/2,82,72+length/2)}</>:
    !radius&&!angle&&!boundary&&!['BuildingGap','villaGap'].includes(kind)?<>
     <path d="M12 63h120" stroke="#687b88" strokeWidth="3"/><rect x="12" y="44" width={120*v/extent} height="18" fill={building}/>
     {[0,1,2,3,4].map(i=><path key={i} d={`M${12+i*30} 63v6`} stroke="#a6b6c1"/>)}<path d={`M${12+120*v/extent} 37v31`} stroke={accent} strokeWidth="2"/></>:
    radius?<><path d={`M14 96V${cy} A${r} ${r} 0 0 1 ${cx} 12 H140`} fill="none" stroke={building} strokeWidth="6"/>
    <path d={`M14 ${cy}H${cx}V12`} fill="none" stroke={accent} strokeDasharray="3 2" strokeWidth="1.6"/>
    <circle cx={cx} cy={cy} r="2.7" fill={accent}/><circle cx="14" cy={cy} r="2" fill={accent}/></>:
    angle?<><path d="M72 58h62" stroke={accent} strokeDasharray="3 3"/><rect x="46" y="50" width="52" height="16" fill={building} transform={`rotate(${-v} 72 58)`}/><path d={`M107 58 A35 35 0 ${Math.abs(v)>180?1:0} ${v<0?1:0} ${72+35*Math.cos(radians)} ${58-35*Math.sin(radians)} M72 58l${46*Math.cos(radians)} ${-46*Math.sin(radians)}`} fill="none" stroke={accent} strokeWidth="2"/><circle cx="72" cy="58" r="2" fill={accent}/></>:
    <>{boundary?<path d={`M${face} 10v80`} stroke={building} strokeDasharray="4 3" strokeWidth="2"/>:<rect x={face-30} y="26" width="30" height="35" fill={building}/>}
     <rect x={edge} y="26" width="30" height="35" fill={building} opacity=".8"/>{ticks(face,69,edge)}
     <path d={`M${face} 60v14 M${edge} 60v14`} stroke={accent}/></>)}
  </svg>
 }
 return <div className={styles.diagram}><strong>{label}</strong><div>{[reference,example].map((v,i)=><div key={i}>{drawing(v)}<span className={styles.diagramCaption}>{`${i?translate('AP.DiagramExample','Example'):['Height','Footprint','Homes'].includes(kind)?translate('AP.DiagramReference','Reference'):translate('AP.DiagramDefault','Default')}\u00a0·\u00a0${radius?'R\u00a0':''}${v}${unit==='percent'?'%':unit?'\u00a0'+unit:''}`}</span></div>)}</div></div>;
}
function NumericControl({label,text,setText,min,max,step=1,commit,unit="",disabled=false,diagram,baseline}:{diagram:string;baseline:number;label:string;text:string;setText:(v:string)=>void;min:number;max:number;step?:number;commit:(v:string)=>void;unit?:string;disabled?:boolean}){
 const track=React.useRef<HTMLDivElement>(null),latest=React.useRef(text),[dragging,drag]=React.useState(false);
 const change=(v:string)=>{latest.current=v;setText(v)};
 const at=(x:number)=>{const r=track.current?.getBoundingClientRect();if(!r||!r.width)return;const n=Math.min(max,Math.max(min,min+Math.round((Math.min(1,Math.max(0,(x-r.left)/r.width))*(max-min))/step)*step));change(String(Number(n.toFixed(4))))};
 React.useEffect(()=>{if(!dragging)return;if(disabled){drag(false);return;}const move=(e:MouseEvent)=>at(e.clientX),up=()=>{drag(false);commit(latest.current)};window.addEventListener('mousemove',move);window.addEventListener('mouseup',up);return()=>{window.removeEventListener('mousemove',move);window.removeEventListener('mouseup',up)}},[dragging,min,max,step,disabled]);
 const n=Number(text),pct=100*Math.min(1,Math.max(0,((Number.isFinite(n)?n:min)-min)/(max-min)));
 const control=<div className={styles.oneSlider}><div ref={track} className={styles.rowTrack} role="slider" aria-disabled={disabled} tabIndex={disabled?-1:0} aria-label={label} aria-valuemin={min} aria-valuemax={max} aria-valuenow={Number.isFinite(n)?n:min} onMouseDown={e=>{if(disabled||e.button!==0)return;e.preventDefault();e.stopPropagation();at(e.clientX);drag(true)}} onKeyDown={e=>{e.stopPropagation();if(disabled)return;if(['ArrowLeft','ArrowRight','Home','End'].includes(e.key)){e.preventDefault();const v=String(Number((e.key==='Home'?min:e.key==='End'?max:Math.min(max,Math.max(min,(Number.isFinite(Number(text))?Number(text):min)+(e.key==='ArrowLeft'?-step:step)))).toFixed(4)));change(v);commit(v)}}}><div className={styles.rowFill} style={{width:pct+'%'}}/><i className={styles.rowMarker} style={{left:pct+'%'}}/><span className={styles.rowLabel}>{unit?label.replace(/\s*\((m|%|°)\)$/, ''):label}</span><b className={styles.rowValue}>{unit?text+'\u00a0'+(unit==='percent'?'%':unit):text}</b></div></div>;

 return <Tooltip tooltip={<DimensionDiagram kind={diagram} label={label} reference={baseline} example={max-baseline>=baseline-min?max:min} unit={unit}/>}>{control}</Tooltip>;
}
function Parameter({label,value,min,max,step,commandName,unit="m",disabled=false,baseline=diagramDefaults[commandName]}:{baseline?:number;label:string;value:number;min:number;max:number;step:number;commandName:string;unit?:string;disabled?:boolean}){
 const [text,setText]=React.useState(String(value));React.useEffect(()=>setText(String(value)),[value]);
 const commit=(raw:string)=>{const n=Number(raw);if(raw.trim()&&Number.isFinite(n)&&n>=min&&n<=max&&n!==value)command(commandName,String(n));else setText(String(value));};
 return <NumericControl diagram={commandName} baseline={baseline} {...{label,text,setText,min,max,step,commit,unit,disabled}}/>;
}
function LandscapePanel(){
 React.useLayoutEffect(()=>{const node=document.querySelector('[data-ap-landscape]'); window.csPanelLayoutV1.add(node); window.csPanelLayoutV1.moved(node);});
 const s=useStateData(),v=s.landscape;const {translate}=useLocalization();const t=(k:string)=>translate(k,(en as Record<string,string>)[k]||k)||k;
 const p=usePanelPlacement(!!v?.open,'ApartmentPlan.landscapePanel');const loading=useLoading(s);
 const [showCleanup,setShowCleanup]=React.useState(false);
 const [query,setQuery]=React.useState(''),[page,setPage]=React.useState(0);
 const items=(v?.trees||[]).map(a=>({...a,Name:assetName(a,translate)})).filter(a=>assetMatches(a,query));
 const pages=Math.max(1,Math.ceil(items.length/8)),current=Math.min(page,pages-1);React.useEffect(()=>setPage(0),[query]);
 if(!v?.open)return null;
 const busy=!!s.calculating||!!s.nativeCommitted||!!s.nativeDemolition||!!v.locked;
 const toggle=(label:string,on:boolean,run:()=>void,disabled=false,allowBusy=false)=><Switch label={t(label)} on={on} run={run} disabled={disabled||busy&&!allowBusy}/>;
 return <><div ref={p.shell} className={styles.panel+' '+styles.landscape} data-ap-landscape="true" style={p.position?{left:p.position.x,top:p.position.y,right:'auto'}:undefined} onMouseEnter={p.enter} onMouseLeave={p.leave} onFocusCapture={p.focus} onBlurCapture={p.blur} {...loading.events}>
  <div className={styles.header} onMouseDown={p.start}><Glyph name="park"/><strong>{t('AP.Landscape')}</strong><Tooltip tooltip={t('AP.UI.Close')}><Button className={styles.small} aria-label={t('AP.UI.Close')} onSelect={()=>command('landscapeClose')}><Glyph name="close"/></Button></Tooltip></div>
  <Scrollable className={styles.scrollViewport} vertical horizontal={false} trackVisibility="always" smooth={false}><div className={styles.body}>
   <div className={styles.switchRow}><Tooltip tooltip={t('AP.TreesHint')}>{toggle('AP.PlantTrees',v.enabled,()=>command('landscapeTrees'))}</Tooltip><Tooltip tooltip={t(v.grassAvailable?'AP.GrassHint':'AP.GrassMissing')}>{toggle('AP.GrassAuto',v.grass,()=>command('landscapeGrass'),!v.grassAvailable,true)}</Tooltip></div>
   <fieldset disabled={busy} className={styles.landscapeSettings}>
    <Tooltip tooltip={t('AP.TreePlacementHint')}><div className={styles.treePlacement}>{[0,1,2].map(i=><Button key={i} className={styles.tool+' '+((v.placement||0)===i?styles.selected:'')} selected={(v.placement||0)===i} disabled={busy} onSelect={()=>command('landscapePlacement',String(i))}><span>{t('AP.TreePlacement'+i)}</span></Button>)}</div></Tooltip>
    <Parameter label={t('AP.TreeDensity')} value={v.density} min={0} max={60} step={1} commandName="landscapeDensity" baseline={s.planMode===1?30:12} disabled={busy} unit={t('AP.TreeDensityUnit')}/>
    <div className={styles.sectionTitle}><span>{t('AP.TreeAges')}</span><span>{t('AP.PlannedTrees')} {formatInteger(v.count??0)}</span></div><div className={styles.treeAges}>{[0,1,2,3].map(i=><Tooltip key={i} tooltip={t('AP.TreeAge'+i)}><Button className={styles.treeAge+' '+(v.ages&(1<<i)?styles.selected:'')} role="checkbox" aria-label={t('AP.TreeAge'+i)} aria-checked={!!(v.ages&(1<<i))} selected={!!(v.ages&(1<<i))} disabled={busy} onSelect={()=>command('landscapeAge',String(i))}><Glyph name={'treeAge'+i}/></Button></Tooltip>)}</div>
    <div className={styles.sectionTitle}><span>{t('AP.TreeSpecies')}</span><b>{v.selected.length}</b><Tooltip tooltip={t('AP.RandomTrees')}><Button className={styles.small} aria-label={t('AP.RandomTrees')} disabled={busy} onSelect={()=>command('landscapeRandom')}><Glyph name="refresh"/></Button></Tooltip></div>
    <div className={styles.searchRow}><Glyph name="search"/><input className={styles.search} aria-label={t('AP.SearchTrees')} placeholder={t('AP.SearchTrees')} value={query} onChange={e=>setQuery(e.target.value)} onKeyDown={e=>e.stopPropagation()}/></div>
    <div className={styles.assetList}>{items.slice(current*8,current*8+8).map(a=><Tooltip key={a.Id} tooltip={a.Name}><Button className={styles.asset+' '+(v.selected.includes(a.Id)?styles.selected:'')} aria-label={a.Name} aria-pressed={v.selected.includes(a.Id)} selected={v.selected.includes(a.Id)} disabled={busy} onSelect={()=>command('landscapeSpecies',a.Id)}><img className={styles.assetImage} src={a.Icon||glyphs.park} alt="" onError={e=>{if(!e.currentTarget.dataset.fallback){e.currentTarget.dataset.fallback='1';e.currentTarget.src=glyphs.park;}}}/><span className={styles.assetName}>{a.Name}</span>{v.selected.includes(a.Id)&&<span className={styles.badge}><Glyph name="check"/></span>}</Button></Tooltip>)}</div>
    {!items.length&&<div className={styles.empty}>{t('AP.UI.NoMatches')}</div>}
    <div className={styles.pages}><Button className={styles.small} aria-label={t('AP.UI.Previous')} disabled={!current} onSelect={()=>setPage(current-1)}><Glyph name="left"/></Button><span className={styles.pageNumber}><span>{current+1}</span><Glyph name="slash"/><span>{pages}</span></span><Button className={styles.small} aria-label={t('AP.UI.Next')} disabled={current+1>=pages} onSelect={()=>setPage(current+1)}><Glyph name="right"/></Button></div>
   </fieldset>
   {!v.grassAvailable&&<div className={styles.warning}>{t('AP.GrassMissing')}</div>}
   {v.grassStatus&&<div className={styles.status}>{t(v.grassStatus)}</div>}
  </div></Scrollable>
 <Loading s={s} loading={loading} t={t}/></div></>;
}
function Inspector(){
 React.useLayoutEffect(()=>{const node=document.querySelector('[data-ap-inspector]'); window.csPanelLayoutV1.add(node); window.csPanelLayoutV1.moved(node);});
 const s=useStateData(),v=s.inspector;const {translate}=useLocalization();const t=(k:string)=>translate(k,(en as Record<string,string>)[k]||k)||k;
 const loading=useLoading(s);
 React.useEffect(()=>()=>trigger('ApartmentPlan','focus',false),[v?.id]);
 if(!v)return null;
 return <><PanelDismiss close={()=>{if(!loading.locked)command('closeInspector')}}/><div className={styles.panel+' '+styles.inspector} data-ap-inspector="true" onMouseEnter={()=>trigger('ApartmentPlan','focus',true)} onMouseLeave={()=>trigger('ApartmentPlan','focus',false)} {...loading.events}>
  <div className={styles.header}><Glyph name="library"/><strong>{v.name||t('AP.UI.Plan')+' '+v.id}</strong><Tooltip tooltip={t('AP.UI.Close')}><Button className={styles.small} aria-label={t('AP.UI.Close')} onSelect={()=>command('closeInspector')}><Glyph name="close"/></Button></Tooltip></div>
  <Scrollable className={styles.inspectorScroll} vertical horizontal={false} trackVisibility="scrollable" smooth={false}><div className={styles.body}>
  <div className={styles.inspectorMetrics}>{[['AP.BuildingCount',v.buildings,null],['AP.ParkingSpaces',v.built?(v.parkingCapacity??v.parking):v.parking,v.built?v.parked:null],['AP.Homes',v.built?(v.homeCapacity??v.homes):v.homes,v.built?v.occupiedHomes:null]].map(([label,total,used])=><div key={String(label)}><span>{t(String(label))}</span><b className={styles.occupancyText}>{`${used!=null?formatInteger(Number(used))+'/':''}${total!=null?formatInteger(Number(total)):t('AP.Unknown')}${v.built&&label!=='AP.BuildingCount'?' ('+(used!=null&&Number(total)>0?(100*Number(used)/Number(total)).toFixed(1):'--.-')+'%)':''}`}</b></div>)}</div>
   {v.built&&s.nativeAuditAvailable&&visibleNotice(s)&&<div className={styles.nativeReport+" "+(s.statusError?styles.statusError:"")}><div className={styles.nativeNotice}>{t(s.nativeNotice!)}</div></div>}
   {v.built&&<Tooltip tooltip={t(v.owned?'AP.DemolishHint':'AP.NoOwnership')}><Button className={styles.save+' '+styles.demolishCompound} disabled={!v.owned} onSelect={()=>command('demolishSaved')}><Glyph name="trash"/><span>{t('AP.Demolish')}</span></Button></Tooltip>}
   {v.legacy&&<Tooltip tooltip={t('AP.LegacyEdit')}><div className={styles.status}><Glyph name="info"/><span>{t('AP.LegacyPlan')}</span></div></Tooltip>}
   <div className={styles.tools}><Tooltip tooltip={t(v.built?'AP.EditCompoundHint':'AP.EditPlanHint')}><Button className={styles.tool} disabled={!v.editable} onSelect={()=>command('editSaved')}><Glyph name="edit"/><span>{t(v.built?'AP.EditCompound':'AP.EditPlan')}</span></Button></Tooltip><Tooltip tooltip={t(v.canDelete?'AP.DeletePlanHint':v.deleteReason||'AP.DeleteBusyHint')}><Button className={styles.deletePlan} aria-label={t('AP.DeleteEditingPlan')} disabled={!v.canDelete} onSelect={()=>command('confirmDelete',String(v.id))}><Glyph name="trash"/></Button></Tooltip></div>
   <Tooltip tooltip={t(v.built?'AP.AlreadyBuilt':v.legacy?'AP.LegacyBuild':'AP.BuildSavedHint')}><Button className={styles.save} disabled={!!v.built} onSelect={()=>command('buildSaved')}><Glyph name="building"/><span>{t(v.built?'AP.AlreadyBuilt':'AP.BuildSaved')}</span></Button></Tooltip>
  </div></Scrollable>
 <Loading s={s} loading={loading} t={t}/></div></>;
}
function AssetEditor({asset:a,t}:{asset:Asset;t:(key:string)=>string}){
 const [height,H]=React.useState(String(a.Height)),[foot,F]=React.useState(String(a.Footprint)),[homes,N]=React.useState(String(a.Homes));
 const h=Number(height),f=Number(foot),n=Number(homes),valid=!!height.trim()&&!!foot.trim()&&!!homes.trim()&&Number.isFinite(h)&&h>=1&&h<=1000&&Number.isFinite(f)&&f>=0&&f<=a.Width*a.Depth&&Number.isInteger(n)&&n>=0&&n<=10000;
 return <div className={styles.edit}><div className={styles.assetNumbers}>{[[t('AP.Height'),height,H],[t('AP.Footprint'),foot,F],[t('AP.Homes'),homes,N]].map(([label,value,set],i)=><NumericControl key={i} diagram={['Height','Footprint','Homes'][i]} baseline={[30,Math.round(a.Width*a.Depth*.5),100][i]} unit={['m','m²',''][i]} label={label as string} text={value as string} setText={set as (v:string)=>void} min={i===0?1:0} max={i===0?1000:i===1?a.Width*a.Depth:10000} step={i===2?1:.1} commit={()=>{}}/>)}</div>{!valid&&<div className={styles.warning}>{t('AP.InvalidAsset')}</div>}<Tooltip tooltip={t('AP.InvalidAsset')}><Button className={styles.apply} disabled={!valid} onSelect={()=>command('asset',JSON.stringify({Id:a.Id,Height:h,Footprint:f,Homes:n}))}><Glyph name="check"/><span>{t('AP.Apply')}</span></Button></Tooltip></div>;
}
class PlanBoundary extends React.Component<{}, {failed:boolean}>{state={failed:false};static getDerivedStateFromError(){return {failed:true}}componentDidCatch(error:Error,info:React.ErrorInfo){trace('render.failed',error.message+' '+info.componentStack)}render(){return this.state.failed?<div className={styles.panel}>Apartment Plan UI error. Logs/ApartmentPlan.</div>:<><NameLabels/><Panel/><LandscapePanel/><Inspector/></>}}
const register:ModRegistrar=r=>{r.append('GameTopRight',MenuButton);r.append('Game',PlanBoundary);};
export const hasCSS=true;export default register;








function TowerControls({s,assets,parkingPicker}:{s:State;assets:Asset[];parkingPicker:boolean}){
 const {translate}=useLocalization();const t=(k:string)=>translate(k,(en as Record<string,string>)[k]||k)||k;
 const [q,setQ]=React.useState(t('AP.Tower.ParkingSearchDefault'));const [open,setOpen]=React.useState(parkingPicker?'towerParking':'');const v=s.tower;if(!v)return null;const locked=!!s.nativeActive||!!s.nativeCommitted;
 const parking=assets.filter(a=>assetMatches(a,q));const chosen=assets.find(a=>a.Id===v.parking);if(chosen&&!parking.some(a=>a.Id===chosen.Id))parking.unshift(chosen);
 const surfaces=v.surfaces||[];const surfaceName=(a:{Id:string;NameKey?:string})=>translate(a.NameKey||`Assets.NAME[${a.Id}]`,a.Id)||a.Id;
 // Gameface does not provide the native select.options contract used by React.
 const select=(label:string,value:string,cmd:string,items:{id:string;name:string}[],disabled=false)=><div className={styles.towerField}>
  <span className={label==='AP.Tower.Parking'?styles.hiddenLabel:undefined}>{t(label)}</span>
  <Button className={styles.towerChoice} disabled={locked||disabled} aria-expanded={open===cmd} onSelect={()=>setOpen(open===cmd?'':cmd)}>
   <span>{items.find(a=>a.id===value)?.name||value||t('AP.Tower.Choose')}</span><span className={open===cmd?styles.towerArrowOpen:styles.towerArrow}><Glyph name="down"/></span>
  </Button>
  {open===cmd&&<Scrollable className={styles.towerChoices} vertical horizontal={false} trackVisibility="always" smooth={false}>
   {items.map(a=><Button key={a.id} className={styles.towerOption+' '+(a.id===value?styles.selected:'')} disabled={locked||disabled} onSelect={()=>{command(cmd,a.id);setOpen('')}}>{a.name}</Button>)}
  </Scrollable>}
 </div>;
 const toggle=(label:string,on:boolean,cmd:string)=><Switch label={t(label)} on={on} run={()=>command(cmd)} disabled={locked}/>;
 return <div className={styles.towerControls}>
 <label className={styles.towerField}><span>{t('AP.Tower.ParkingSearch')}</span><input value={q} disabled={locked} onChange={e=>setQ(e.target.value)}/></label>
 {select('AP.Tower.Parking',chosen?.Id||'','towerParking',parking.map(a=>({id:a.Id,name:a.Name+' · '+a.Width+'×'+a.Depth+'m'})))}


 <div className={styles.switchRow}>{toggle('AP.Tower.Paving',v.paving,'towerPaving')}{toggle('AP.Tower.Facility',v.facility,'towerFacility')}</div>
 {v.paving&&select('AP.Tower.EntranceSurface',v.entranceSurface,'towerEntranceSurface',surfaces.map(a=>({id:a.Id,name:surfaceName(a)})))}
 <small>{t('AP.Tower.AssetHint')}</small>
 <div className={styles.switchRow}>{(v.rules||[]).filter(r=>r.max===1).map(r=><Switch key={r.id} label={t('AP.Tower.Rule.'+r.id)} on={!!r.value} disabled={locked} run={()=>command('towerRule',r.id+'|'+(r.value?0:1))}/>)}</div>
 <div className={styles.sliderGroup}>{(v.rules||[]).filter(r=>r.max!==1&&!['BuildingGap','Angle','BuildingBoundary'].includes(r.id)).map(r=><TowerRule key={r.id} rule={r} locked={locked} label={t('AP.Tower.Rule.'+r.id)}/>)}</div>
 </div>;
}

function TowerRule({rule:r,locked,label}:{rule:{id:string;value:number;defaultValue:number;min:number;max:number;step:number};locked:boolean;label:string}){
 const [value,setValue]=React.useState(String(r.value));React.useEffect(()=>setValue(String(r.value)),[r.value]);
 return <NumericControl diagram={r.id} baseline={r.defaultValue} unit={['Angle','WalkApproachAngle'].includes(r.id)?'°':r.id==='CrossAreaPercent'?'percent':r.id==='MaxBuildings'?'':'m'} disabled={locked} label={label} text={value} setText={setValue} min={r.min} max={r.max} step={r.step} commit={text=>{if(!locked&&Number.isFinite(Number(text)))command('towerRule',r.id+'|'+text)}}/>;
}
