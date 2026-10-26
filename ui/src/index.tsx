import React from 'react';
import {ModRegistrar} from 'cs2/modding';
import {bindValue,useValue,trigger} from 'cs2/api';
import {Button,FloatingButton,Tooltip,Scrollable} from 'cs2/ui';
import {useLocalization} from 'cs2/l10n';
import styles from './plan.module.css';
import icon from './plan.svg';
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
const command=(c:string,v='')=>{trace('command.send',c+' value='+v.slice(0,500));trigger('ApartmentPlan','command',c,v)};
type Asset={Subscribed?:boolean;Id:string;Name:string;NameKey?:string;Icon:string;Width:number;Depth:number;GeometryWidth?:number;GeometryDepth?:number;Height:number;Footprint:number;Homes:number};
type Stored={name?:string;canDelete?:boolean;deleteReason?:string;parked?:number|null;occupiedHomes?:number|null;parkingCapacity?:number|null;homeCapacity?:number|null;owned?:number;built?:boolean;id:number;area:number;far:number;homes:number;buildings:number;parking:number|null;editable:boolean;legacy:boolean};
type Landscape={placement?:number;count?:number;locked?:boolean;open:boolean;trees:{Id:string;Name:string;NameKey?:string;Icon:string}[];selected:string[];enabled:boolean;ages:number;density:number;grassAvailable:boolean;grass:boolean;grassStatus:string};
type State={compoundEditing?:boolean;canForceDemolition?:boolean;canDeleteAllPlans?:boolean;layoutReasons?:Record<string,number>;migration?:{plan:number;count:number;skipped:number;applied:number;status:string};fillRadius?:number;canFill?:boolean;recentBuildingAssets?:string;entranceClose?:boolean;tower?:{rules?:{id:string;value:number;min:number;max:number;step:number}[];parking:string;surface:string;entranceSurface:string;paving:boolean;facility:boolean;turn:number;surfaces:{Id:string;NameKey?:string}[]};villaCentralRoad?:boolean;villaExistingRoadsOnly?:boolean;villaRoadDensity?:number;villaFollowBoundary?:boolean;planMode?:number;villaMesh?:boolean;villaGap?:number;villaReduction?:number;villaMainSpacing?:number;villaRows?:number;villaMaxBuildings?:number;villaBoundary?:number;villaSetback?:number;villaConnectionDistance?:number;villaConnections?:{Side:number;Junction:boolean;Disabled:boolean}[];limit?:{current:number;maximum:number};grassSurfaces?:boolean;nativeProgress?:number;nativeBusy?:boolean;buildingPattern?:number;mainTJunctions?:boolean;statusError?:boolean;diagnosticCopyState?:number;diagnosticShareStatus?:string;diagnosticShareBusy?:boolean;catalogLoading?:boolean;catalogCount?:number;lotTiles?:boolean;surfaceRadius?:number;landscape?:Landscape;canDelete?:boolean;deleteReason?:string;mainSetback?:number;boundaryMargin?:number;calculating?:boolean;population?:number|null;householdAverage?:number;pendingRoads?:number;roadCurved?:boolean;roadPoints?:number;roads?:number;nativeDemolition?:boolean;inspector?:Stored;editingPlan?:number;curveLength?:number;curveRadius?:number;decalGap?:number;lineOverlap?:number;parkingPitch?:number;parkingGreen?:boolean;parkingGreenWidth?:number;networks?:{Id:string;Name?:string;RoadBuilder?:boolean;Width:number;Path:boolean;Car:boolean;Walk:boolean;Internal:boolean}[];mainNetwork?:string;aisleNetwork?:string;walkNetwork?:string;nativeActive?:boolean;nativeValid?:boolean;nativeReport?:string;nativeMetrics?:Record<string,number>;nativeNotice?:string;nativeCommitted?:boolean;nativeAuditAvailable?:boolean;cornerMode?:number;requireBuildingRoad?:boolean;parkingAccessFailures?:number;requireFrontParking?:boolean;accessFailures?:number;entrances?:number;zones?:number;zonePoints?:number;replacement?:string;snaps?:boolean[];open?:boolean;mode?:string;status?:string;points?:number;entrance?:number;selected?:string[];area?:number;far?:number;homes?:number;shortfall?:number[];areas?:Record<string,number>;saved?:Stored[];colors?:string[];canSave?:boolean};
function useStateData():State{const raw=useValue(state$);return React.useMemo(()=>{try{return JSON.parse(raw)||{}}catch{return {}}},[raw]);}
const Glyph=({name}:{name:string})=><img className={styles.glyph} src={glyphs[name]||icon} alt=""/>;
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
 return {locked,progress:complete?100:s.nativeBusy?(s.nativeProgress??0):undefined,
  events:{'aria-busy':locked,onClickCapture:stop,onMouseDownCapture:stop,onMouseUpCapture:stop,onKeyDownCapture:stop,onKeyUpCapture:stop,onChangeCapture:stop,onWheelCapture:stop}};
}
function Loading({s,loading,t}:{s:State;loading:ReturnType<typeof useLoading>;t:(k:string)=>string}){
 const [visible,setVisible]=React.useState(loading.locked);
 React.useEffect(()=>{if(loading.locked){setVisible(true);return;}const timer=setTimeout(()=>setVisible(false),180);return()=>clearTimeout(timer);},[loading.locked]);
 if(!loading.locked&&!visible)return null;
 const title=s.nativeBusy?'AP.NativeLoadingTitle':s.catalogLoading?'AP.CatalogLoading':'AP.CalculatingTitle';
 return <div className={styles.loadingVeil+(!loading.locked?' '+styles.loadingExit:'')} role="status" aria-live="polite"><div className={styles.loadingCard}><Glyph name="sparkle"/><strong>{loading.progress===100?t('AP.LoadingComplete'):t(title)}</strong><div className={styles.loadingTrack} role="progressbar" aria-label={t(title)} aria-valuemin={0} aria-valuemax={100} aria-valuenow={loading.progress}><div className={styles.loadingFill+' '+(loading.progress===undefined?styles.indeterminate:'')} style={loading.progress===undefined?undefined:{width:loading.progress+'%'}}/></div><b>{loading.progress===undefined?t('AP.LoadingWorking'):loading.progress+'%'}</b><span>{t('AP.PanelLoadingHint')}</span></div></div>;
}
function MenuButton(){const s=useStateData();return <Tooltip tooltip="Apartment Plan"><FloatingButton src={icon} selected={!!s.open} onSelect={()=>command('menu')}/></Tooltip>}
function Panel(){
 const s=useStateData(),hint=useValue(hint$),rawCatalog=useValue(catalog$);
 const {translate}=useLocalization();const t=(k:string)=>translate(k,(en as Record<string,string>)[k]||k)||k;
 const {assets,loading:localizing}=useAssetCatalog<Asset>(rawCatalog,translate);
 const p=usePanelPlacement(!!s.open);const loading=useLoading(s,localizing);
 const [networkQuery,setNetworkQuery]=React.useState('');
 const [selectedOnly,setSelectedOnly]=React.useState(false),[networkOpen,setNetworkOpen]=React.useState('');
 const [query,setQuery]=React.useState(''),[all,setAll]=React.useState(false),[page,setPage]=React.useState(0),[tab,setTab]=React.useState('assets');
 const [correction,setCorrection]=React.useState<string|null>(null),[ready,setReady]=React.useState(false);
 React.useEffect(()=>{try{const v=JSON.parse(localStorage.getItem('ApartmentPlan.catalog')||localStorage.getItem('ApartmentPlan.panel')||'{}');setQuery(typeof v.query==='string'?v.query:'');setAll(!!v.all);if(['assets','rules','summary','saved'].includes(v.tab))setTab(v.tab);}catch{}setReady(true);},[]);
 React.useEffect(()=>{if(ready)try{localStorage.setItem('ApartmentPlan.catalog',JSON.stringify({query,all,tab}));}catch{}},[query,all,tab,ready]);
 const villa=s.planMode===1,tower=s.planMode===2;
 const selected=s.selected||[];const selectionKey=selected.join('\u0000');
 const orderedAssets=React.useMemo(()=>{
  let recent:unknown;try{recent=JSON.parse(s.recentBuildingAssets||'[]');}catch{recent=[];}
  const ranks=new Map<string,number>();if(Array.isArray(recent))recent.forEach((id,i)=>{if(typeof id==='string'&&!ranks.has(id))ranks.set(id,i);});
  return assets.map((asset,index)=>({asset,index})).sort((a,b)=>(ranks.get(a.asset.Id)??Infinity)-(ranks.get(b.asset.Id)??Infinity)||Number(!!b.asset.Subscribed)-Number(!!a.asset.Subscribed)||a.index-b.index).map(x=>x.asset);
 },[assets,s.recentBuildingAssets]);
 const matches=React.useMemo(()=>{const ids=new Set(selected);return orderedAssets.filter(a=>(!villa||!/Rural/i.test(a.Id+" "+a.Name))&&(!selectedOnly||ids.has(a.Id))&&(all||(villa?a.Homes>0&&Math.abs(a.Width-16)<.1&&Math.abs(a.Depth-16)<.1&&/CS[\s_-]*(KR|Korean)/i.test(a.Id+' '+a.Name):a.Homes>1))&&assetMatches(a,query));},[orderedAssets,selectedOnly,selectionKey,all,query,villa]);
 const pages=Math.max(1,Math.ceil(matches.length/8)),current=Math.min(page,pages-1);
 React.useEffect(()=>setPage(0),[query,all,selectedOnly]);
 React.useEffect(()=>setPage(v=>Math.min(v,pages-1)),[pages]);
 React.useEffect(()=>{if(s.mode==='replace')setTab('assets');},[s.mode]);
 if(!s.open)return null;
 const mini=(label:string,name:string,run:()=>void,disabled=false,active=false)=><Tooltip tooltip={t(label)}><Button className={styles.small+' '+(active?styles.selected:'')} aria-label={t(label)} disabled={disabled} selected={active} onSelect={run}><Glyph name={name}/></Button></Tooltip>;
 const action=(key:string,name:string,disabled=false,active=false)=><Tooltip tooltip={t(s.compoundEditing&&key==='nativePreview'?'AP.PreviewAdditions':s.compoundEditing&&key==='nativeApply'?'AP.ApplyAdditions':key==='finish'&&s.mode==='road'?'AP.RoadFinish':'AP.'+key)}><Button className={styles.tool+' '+(active?styles.selected:'')} aria-label={t(s.compoundEditing&&key==='nativePreview'?'AP.PreviewAdditions':s.compoundEditing&&key==='nativeApply'?'AP.ApplyAdditions':key==='finish'&&s.mode==='road'?'AP.RoadFinish':'AP.'+key)} disabled={disabled} selected={active} onSelect={()=>command(key)}><Glyph name={name}/><span>{t(s.compoundEditing&&key==='nativePreview'?'AP.PreviewAdditions':s.compoundEditing&&key==='nativeApply'?'AP.ApplyAdditions':key==='finish'&&s.mode==='road'?'AP.RoadFinish':'AP.'+key)}</span></Button></Tooltip>;
 const check=(label:string,on:boolean,run:()=>void,help?:string,disabled=false)=><Tooltip tooltip={t(help||label)}><Button className={styles.check+' '+(on?styles.selected:'')} role="switch" disabled={disabled} aria-checked={on} selected={on} onSelect={run}><span className={styles.switchTrack} aria-hidden="true"><span className={styles.switchThumb}/></span><span>{t(label)}</span></Button></Tooltip>;
 const unit=(value:number,kind='area')=><span className={styles.quantity}><span>{kind==='percent'?value.toFixed(1):Math.round(value).toLocaleString()}</span><Glyph name={kind==='percent'?'percent':'square-metre'}/></span>;
 const info=(label:string)=>mini(label,'info',()=>{});
 const networks=s.networks||[];
 const networkName=(id?:string)=>id?(translate(`Assets.NAME[${id}]`,id==='Small Road'?t('AP.TwoLaneRoad'):(networks.find(n=>n.Id===id)?.Name||id))||id):t('AP.Network.NotReady');
 const networkWidth=(id?:string)=>{const width=networks.find(n=>n.Id===id)?.Width;return width&&Number.isFinite(width)&&width>0?`${width} m`:t('AP.Network.NotReady');};
 const networkChoices=(role?:string)=>networks.filter(n=>assetMatches({Id:n.Id,Name:networkName(n.Id)},networkQuery)).filter(n=>role==='main'?n.Car&&!n.Path:role==='pedestrian'?n.Path&&n.Walk&&!n.Car:n.Internal).sort((a,b)=>role==='main'?Number(b.Id==='Small Road')-Number(a.Id==='Small Road'):0);
 const snaps=[['All',-1,'snap-all'],['Roads',0,'snap-road'],['Lots',2,'snap-lot'],['Directions',4,'angle'],['Buildings',1,'snap-building'],['Guides',5,'snap-guide'],['Grid',3,'grid']] as const;
 const snapOn=(index:number)=>index<0?[0,1,2,3,4,5].every(i=>!!s.snaps?.[i]):!!s.snaps?.[index];
 const chosen=assets.filter(a=>selected.includes(a.Id));
 const measure=/^([0-9.]+)m \/ (-?[0-9.]+) deg$/.exec(hint);
 return <><PanelDismiss close={()=>{if(!loading.locked)command(s.landscape?.open?'landscapeClose':'close')}}/><div ref={p.shell} data-ap-panel="true" role="region" aria-label="Apartment Plan" className={styles.panel+' '+((!p.collapsed||loading.locked)?styles.expanded:'')+' '+(p.layout&1?styles.wide:'')} style={p.position?{left:p.position.x,top:p.position.y,right:'auto'}:undefined} onMouseEnter={p.enter} onMouseLeave={p.leave} onFocusCapture={p.focus} onBlurCapture={p.blur} {...loading.events}>
  <div className={styles.header} onMouseDown={p.start}><img src={icon} alt=""/><strong>APARTMENT PLAN</strong><div className={styles.headerActions}>{mini('AP.Landscape','park',()=>command('landscapeOpen'),false,!!s.landscape?.open)}{mini('AP.UI.Wide','layout',()=>p.setLayout(p.layout^1),false,!!(p.layout&1))}{mini(p.collapsed?'AP.UI.Expand':'AP.UI.Collapse',p.collapsed?'down':'up',()=>p.setCollapsed(!p.collapsed))}{mini('AP.UI.Close','close',()=>command('close'))}</div></div>
  {(!p.collapsed||loading.locked)&&<Scrollable className={styles.scrollViewport} vertical horizontal={false} trackVisibility="always" smooth={false}><div className={styles.body}><div className={styles.primary}>
   <div className={styles.planTabs} role="tablist" aria-label={t('AP.PlanType')}>{[0,2,1].map(mode=><Button key={mode} role="tab" aria-selected={(s.planMode||0)===mode} selected={(s.planMode||0)===mode} className={styles.planTab+' '+((s.planMode||0)===mode?styles.planTabActive:'')} disabled={!!s.nativeActive||mode!==0} onSelect={()=>{if(mode!==0)return;command('planMode',String(mode));setQuery('');setPage(0)}}><span>{t(mode===0?'AP.ApartmentTab':mode===2?'AP.Tower.Tab':'AP.VillaTab')}</span>{mode!==0&&<span className={styles.comingSoonBadge}>{t('AP.ComingSoon')}</span>}</Button>)}</div>

   <div className={styles.steps}>{['boundary','entrance','preview'].map((k,i)=><span className={s.mode===k||k==='boundary'&&s.mode==='fill'||k==='preview'&&['replace','road'].includes(s.mode||'')?styles.current:''} key={k}><b>{i+1}</b>{t('AP.Step.'+k)}</span>)}</div>
   <div data-ap-tools="true" className={styles.tools+' '+styles.toolGrid}>{action('new','new')}{action('edit','edit',(s.points||0)<3,s.mode==='edit')}{action('finish','finish',!['boundary','edit','zone','road'].includes(s.mode||'')||(s.mode!=='road'&&(s.mode==='zone'?(s.zonePoints||0):(s.points||0))<3))}{action('entrance','entrance',(s.points||0)<3,s.mode==='entrance')}{action('finishEntrances','check',s.mode!=='entrance'||!s.entrances)}{!villa&&!tower&&action('road','road',!s.area||s.nativeCommitted,s.mode==='road')}{action('undo','undo')}{action('rebuild','refresh',!s.area)}{!villa&&!tower&&action('pickBuilding','replace',!s.canSave,s.mode==='replace')}</div>
   {s.canFill&&['boundary','fill'].includes(s.mode||'')&&<div className={styles.fillTools}>{check('AP.Fill',s.mode==='fill',()=>command('fill'),'AP.FillHint')}{s.mode==='fill'&&<Parameter label={t('AP.FillRadius')} value={s.fillRadius||250} min={50} max={600} step={10} commandName="fillRadius"/>}</div>}
   {s.mode==='road'&&<div data-ap-road-tools><div className={styles.roadModes} role="group" aria-label={t('AP.RoadMode')}>
    {mini('AP.RoadStraight','straight',()=>command('roadShape','straight'),!!s.calculating,!s.roadCurved)}
    {mini('AP.RoadCurve','curve',()=>command('roadShape','curve'),!!s.calculating,!!s.roadCurved)}
    <span className={styles.roadCount}>{t('AP.RoadPendingCount')} {s.pendingRoads||0}</span>
    {mini('AP.RoadFinish','check',()=>command('finish'),!!s.calculating)}
    {mini('AP.RoadCancel','close',()=>command('roadCancel'),!!s.calculating)}
   </div><Tooltip tooltip={t('AP.RoadNetworkHint')}><Button className={styles.disclosure} onSelect={()=>{setTab('rules');setNetworkOpen('main')}}><Glyph name="road"/><div className={styles.networkLabel}><small>{t('AP.Network.main')}</small><span>{networkName(s.mainNetwork)}</span></div><b className={styles.networkWidth}>{networkWidth(s.mainNetwork)}</b></Button></Tooltip></div>}
   <div data-ap-snaps="true" className={styles.snaps} aria-label={t('AP.UI.Snapping')}>{snaps.map(([key,i,glyph])=><Tooltip key={key} tooltip={t('AP.Snap.'+key)}><Button className={styles.snap+' '+(snapOn(i)?styles.selected:'')} aria-label={t('AP.Snap.'+key)} selected={snapOn(i)} aria-pressed={snapOn(i)} onSelect={()=>command('snap',key)}><Glyph name={glyph}/></Button></Tooltip>)}</div>
   <div role={s.statusError?"alert":"status"} className={styles.status+" "+(s.statusError?styles.statusError:"")}><Glyph name={s.statusError?'warning':'info'}/><span>{t(!s.statusError&&s.mode==='replace'?(s.replacement?'AP.ChooseReplacement':'AP.PickBuilding'):s.status||'AP.Draw').replace('{current}',Math.ceil(s.limit?.current??0).toLocaleString()).replace('{maximum}',(s.limit?.maximum??0).toLocaleString())}</span>{s.statusError?mini(s.diagnosticCopyState===1?'AP.DiagnosticsCopied':s.diagnosticCopyState===-1?'AP.DiagnosticsCopyFailed':'AP.CopyDiagnostics',s.diagnosticCopyState===1?'check':'copy',()=>command('copyDiagnostics')):info('AP.Keys')}{s.statusError&&mini(s.diagnosticShareStatus||'AP.ShareDiagnostics',s.diagnosticShareStatus==='AP.ShareSent'?'check':'share',()=>command('shareDiagnostics'),s.diagnosticShareBusy)}{s.canForceDemolition&&mini('AP.ForceDemolish','trash',()=>command('nativeForceDemolition'))}</div>
   {s.diagnosticShareStatus&&s.diagnosticShareStatus!=='AP.ShareDiagnostics'&&<div role="status" aria-live="polite" className={styles.status}><Glyph name={s.diagnosticShareStatus==='AP.ShareSent'?'check':'info'}/><span>{t(s.diagnosticShareStatus)}</span></div>}
   {s.statusError&&Object.keys(s.layoutReasons||{}).length>0&&<div className={styles.status}><span>{t('AP.LayoutReason.Counts')} {Object.entries(s.layoutReasons||{}).map(([key,count])=>`${t(key)}: ${count}`).join(' · ')}</span></div>}
   {s.entranceClose&&<div role="status" className={styles.measure}>{t('AP.EntranceNearbyAdvisory')}</div>}
   {hint&&!measure&&!(s.entranceClose&&hint==='AP.EntranceNearbyAdvisory')&&<div className={styles.measure}>{hint.startsWith('AP.')?t(hint):hint}</div>}
   <div className={styles.metrics}><Tooltip tooltip={t('AP.PopulationHint')+' '+(s.householdAverage||0).toFixed(2)}><div><span>{t('AP.Population')}</span><strong>{s.population==null?t('AP.NoPopulationData'):s.population.toLocaleString()}</strong></div></Tooltip><div><span>{t('AP.Far')}</span><strong>{unit(s.far||0,'percent')}</strong></div><div><span>{t('AP.Homes')}</span><strong>{(s.homes||0).toLocaleString()}</strong></div></div>
   <div className={styles.saveRow}><Tooltip tooltip={t('AP.SaveHint')}><Button className={styles.save} disabled={!s.canSave} onSelect={()=>command('save')}><Glyph name="save"/><span>{t('AP.save')}</span></Button></Tooltip>
   <Tooltip tooltip={t(s.canDelete?'AP.DeletePlanHint':s.deleteReason||'AP.DeleteBusyHint')}><Button data-ap-delete="true" aria-label={t('AP.DeleteEditingPlan')} className={styles.deletePlan} disabled={!s.canDelete||!!s.calculating||!!s.nativeActive||!!s.nativeDemolition} onSelect={()=>command('confirmDelete')}><Glyph name="trash"/></Button></Tooltip>
   {s.nativeActive?action(s.nativeDemolition?'nativeDemolishApply':'nativeApply',s.nativeDemolition?'trash':'building',!s.nativeValid):action('nativePreview','building',!s.canSave||s.nativeCommitted)}</div>
   {(s.nativeActive||s.nativeAuditAvailable)&&<div className={styles.tools}>{s.nativeActive&&action('nativeCancel','close')}{s.nativeAuditAvailable&&action('nativeAudit','search')}</div>}
   {(s.nativeActive||s.nativeAuditAvailable)&&s.nativeNotice&&<div className={styles.nativeReport+" "+(s.statusError?styles.statusError:"")}><div className={styles.nativeNotice}>{t(s.nativeNotice)}</div></div>}
  </div><div className={styles.secondary}>
   <div className={styles.tabs}>{[['assets','building'],['rules','sliders'],['summary','chart'],['saved','library']].map(([key,name])=><Tooltip key={key} tooltip={t('AP.UI.Tab.'+key)}><Button selected={tab===key} className={styles.tab+' '+(tab===key?styles.selected:'')} aria-label={t('AP.UI.Tab.'+key)} onSelect={()=>setTab(key)}><Glyph name={name}/><span>{t('AP.UI.Tab.'+key)}</span></Button></Tooltip>)}</div>
   <div className={styles.content}>
   {tab==='assets'&&<>{villa&&<Button className={styles.disclosure} onSelect={()=>command('villaSelect')}>{t('AP.Villa.Select')}</Button>}<div className={styles.sectionTitle}><span>{t('AP.Assets')}</span><b>{selected.length}</b>{info('AP.EstimateHint')}</div><div className={styles.searchRow}><Glyph name="search"/><input className={styles.search} value={query} aria-label={t('AP.Search')} placeholder={t('AP.Search')} onChange={e=>setQuery(e.target.value)} onKeyDown={e=>e.stopPropagation()}/>{query&&mini('AP.UI.Clear','close',()=>setQuery(''))}</div>
    <div className={styles.assetFilters}>{check('AP.UI.AllBuildings',all,()=>setAll(!all),'AP.AllBuildings')}{check('AP.UI.SelectedOnly',selectedOnly,()=>setSelectedOnly(!selectedOnly))}</div><Button className={styles.disclosure} aria-label={t('AP.UI.ClearAssets')} disabled={!selected.length} onSelect={()=>command('clearAssets')}><Glyph name="close"/><span>{t('AP.UI.ClearAssets')}</span></Button>
    <div data-ap-assets="true" className={styles.assetList}>{matches.slice(current*8,current*8+8).map(a=><Tooltip key={a.Id} tooltip={<div className={styles.tooltipText}><strong>{a.Name||a.Id}</strong><div className={styles.assetSize}>{a.Width}<Glyph name="multiply"/>{a.Depth} m</div><div>{a.Height.toFixed(1)} m</div>{villa&&<div>{t('AP.Villa.Geometry')}: {(a.GeometryWidth||0).toFixed(1)} × {(a.GeometryDepth||0).toFixed(1)} m</div>}<div>{a.Homes} {t('AP.Homes')}</div></div>}><Button aria-label={a.Name||a.Id} className={styles.asset+' '+(selected.includes(a.Id)?styles.selected:'')} selected={selected.includes(a.Id)} onSelect={()=>command('select',a.Id)}><img className={styles.assetImage} src={a.Icon||icon} alt="" onError={e=>{if(!e.currentTarget.dataset.fallback){e.currentTarget.dataset.fallback='1';e.currentTarget.src=icon;}}}/><span className={styles.assetName}>{a.Name||a.Id}</span><span className={styles.assetSize}>{a.Width}<Glyph name="multiply"/>{a.Depth} m</span>{selected.includes(a.Id)&&<span className={styles.badge}><Glyph name="check"/></span>}</Button></Tooltip>)}</div>
    {!matches.length&&<div className={styles.empty}>{t('AP.UI.NoMatches')}</div>}
    <div className={styles.pages}>{mini('AP.UI.Previous','left',()=>setPage(current-1),current===0)}<span className={styles.pageNumber}><span>{current+1}</span><Glyph name="slash"/><span>{pages}</span></span><span>{matches.length} {t('AP.UI.Results')}</span>{mini('AP.UI.Next','right',()=>setPage(current+1),current+1>=pages)}</div>
    <div className={styles.sectionTitle}><span>{t('AP.Corrections')}</span>{info('AP.EstimateHint')}</div>
    {chosen.map(a=><div key={a.Id}><Button className={styles.disclosure} aria-expanded={correction===a.Id} onSelect={()=>setCorrection(correction===a.Id?null:a.Id)}><Glyph name={correction===a.Id?'down':'right'}/><span>{a.Name||a.Id}</span></Button>{correction===a.Id&&<AssetEditor key={a.Id+':'+a.Height+':'+a.Footprint+':'+a.Homes} asset={a} t={t}/>}</div>)}
   </>}
   {tab==='rules'&&tower&&<><div>{t('AP.Tower.RulesHint')}</div><TowerControls s={s} assets={assets}/></>}
   {tab==='rules'&&!tower&&<><div className={styles.sectionTitle}>{t('AP.Networks')}</div>{(villa?[['main',s.mainNetwork]]:[['main',s.mainNetwork],['aisle',s.aisleNetwork],['pedestrian',s.walkNetwork]]).map(([role,id])=><div key={role}><Button className={styles.disclosure} onSelect={()=>setNetworkOpen(networkOpen===role?'':role||'')}><Glyph name={networkOpen===role?'down':'right'}/><div className={styles.networkLabel}><small>{t('AP.Network.'+role)}</small><span>{networkName(id)}</span></div><b className={styles.networkWidth}>{networkWidth(id)}</b></Button>{networkOpen===role&&<div className={styles.networkList}><div className={styles.searchRow}><Glyph name="search"/><input className={styles.search} value={networkQuery} aria-label={t('AP.SearchRoads')} placeholder={t('AP.SearchRoads')} onChange={e=>setNetworkQuery(e.target.value)} onKeyDown={e=>e.stopPropagation()}/></div>{!networkChoices(role).length&&<div className={styles.empty}>{t('AP.Network.Empty')}</div>}{networkChoices(role).map(n=><Button key={n.Id} className={styles.disclosure+' '+(id===n.Id?styles.selected:'')} selected={id===n.Id} disabled={s.nativeActive} onSelect={()=>command('network',role+'|'+n.Id)}><span>{networkName(n.Id)}</span><b className={styles.networkWidth}>{`${n.Width} m`}</b></Button>)}</div>}</div>)}{villa?<><div className={styles.sectionTitle}>{t('AP.Villa.Rules')}</div><div className={styles.status}>{t('AP.Villa.MeshHint')}</div>
    {check('AP.Villa.CentralRoad',s.villaCentralRoad!==false,()=>command('villaCentralRoad'))}
    {s.villaExistingRoadsOnly&&<div>{t('AP.Villa.ExistingRoads')} · {Math.round(s.villaRoadDensity||0)} m/ha</div>}
    {check('AP.Villa.FollowBoundary',s.villaFollowBoundary!==false,()=>command('villaFollowBoundary'))}
    <Parameter label={t('AP.Villa.Gap')} value={s.villaGap??0} min={-6} max={6} step={.5} commandName="villaGap"/>
    <Parameter label={t('AP.Villa.MainSpacing')} value={s.villaMainSpacing??120} min={60} max={300} step={10} commandName="villaMainSpacing"/>
    <Parameter label={t('AP.Villa.Rows')} value={s.villaRows??2} min={1} max={2} step={1} commandName="villaRows"/>
    <Parameter label={t('AP.Villa.Boundary')} value={s.villaBoundary??2} min={0} max={12} step={.5} commandName="villaBoundary"/>
    <Parameter label={t('AP.Villa.MaxBuildings')} value={s.villaMaxBuildings??500} min={10} max={1000} step={10} commandName="villaMaxBuildings" unit=""/>
    <Parameter label={t('AP.Villa.ConnectionDistance')} value={s.villaConnectionDistance??4} min={1} max={8} step={.5} commandName="villaConnectionDistance"/>
    <Button className={styles.disclosure} onSelect={()=>command('villaDetect')}>{t('AP.Villa.Detect')}</Button>
    <div className={styles.status}>{t('AP.Villa.ConnectionHint')}</div>
    {(s.villaConnections||[]).map((c,i)=><Button key={i} className={styles.disclosure+' '+(!c.Disabled?styles.selected:'')} selected={!c.Disabled} onSelect={()=>command('villaToggle',String(i))}><div className={styles.connectionLabel}>{`${t(c.Junction?'AP.Villa.Junction':'AP.Villa.Connection')} ${i+1} 쨌 ${t('AP.Villa.Side')} ${c.Side+1}`}</div><b className={styles.connectionState}>{t(c.Disabled?'AP.Villa.Excluded':'AP.Villa.Enabled')}</b></Button>)}
    <Button className={styles.disclosure} onSelect={()=>command('landscapeOpen')}>{t('AP.Landscape')}</Button>
    </>:<><div className={styles.sectionTitle}><span>{t('AP.UI.Rules')}</span>{info('AP.UI.OptionsHint')}</div><div className={styles.ruleChecks} data-ap-rule-checks>{check('AP.UI.BuildingRoad',!!s.requireBuildingRoad,()=>command('buildingRoad'),'AP.BuildingRoadScope',true)}{check('AP.UI.NearParking',!!s.requireFrontParking,()=>command('frontParking'),'AP.Desc.RequireFrontParking')}</div>
    <Tooltip tooltip={t("AP.Desc.MainRoadCornerMode")}><Button className={styles.disclosure} onSelect={()=>command("cornerMode")}><Glyph name="angle"/><span>{`${t("AP.Option.MainRoadCornerMode")}: ${t("AP.CornerMode."+(s.cornerMode||0))}`}</span></Button></Tooltip>
    <div className={styles.sectionTitle}>{t('AP.BuildingPattern')}</div>
    <div className={styles.patternChoices} role="group" aria-label={t('AP.BuildingPattern')}>{['grid','barsHorizontal','barsVertical','barsMixed'].map((glyph,i)=><Tooltip key={i} tooltip={t('AP.BuildingPatternHint')}><Button selected={(s.buildingPattern||0)===i} className={styles.tool+' '+((s.buildingPattern||0)===i?styles.selected:'')} disabled={!!s.nativeActive||!!s.nativeCommitted} onSelect={()=>command('buildingPattern',String(i))}><Glyph name={glyph}/><span>{t('AP.BuildingPattern.'+i)}</span></Button></Tooltip>)}</div>
    {check('AP.AllowMainTJunctions',s.mainTJunctions!==false,()=>command('mainTJunctions'),'AP.AllowMainTJunctionsHint',!!s.nativeActive||!!s.nativeCommitted)}
    <Tooltip tooltip={t('AP.Desc.MainParkingSetback')}><div><Parameter label={t('AP.Option.MainParkingSetback')} value={s.mainSetback??4} min={0} max={20} step={1} commandName="mainSetback"/></div></Tooltip>
    <Tooltip tooltip={t('AP.Desc.BoundaryGreenMargin')}><div><Parameter label={t('AP.Option.BoundaryGreenMargin')} value={s.boundaryMargin??4} min={0} max={20} step={1} commandName="boundaryMargin"/></div></Tooltip>
    <Parameter label={t('AP.Option.MainRoadCurveLengthPercent')} value={s.curveLength??50} min={25} max={100} step={5} commandName="curveLength" unit="percent"/>
    <Parameter label={t('AP.Option.MainRoadCurveRadius')} value={s.curveRadius||40} min={8} max={200} step={1} commandName="curveRadius"/>
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
   {tab==='summary'&&<><div className={styles.sectionTitle}><span>{t('AP.UI.Legend')}</span>{info('AP.EstimateHint')}</div><div className={styles.legend}>{[0,1,10,2,3,4,5,6,8,9].map(k=><div key={k}><i style={{backgroundColor:s.colors?.[k]}}/><span>{t('AP.Kind.'+k)}</span>{unit(s.areas?.[k]||0)}</div>)}</div>
    {!!s.parkingAccessFailures&&<div className={styles.warning}><Glyph name="warning"/><span>{t('AP.ParkingAccessIncomplete')} {s.parkingAccessFailures}</span></div>}{!!s.accessFailures&&<div className={styles.warning}><Glyph name="warning"/><span>{t('AP.AccessIncomplete')} {s.accessFailures}</span></div>}
    {!!s.shortfall?.some(v=>v>1)&&<><div className={styles.sectionTitle}>{t('AP.Deficit')}</div>{s.shortfall.map((v,k)=>v>1?<div className={styles.deficit} key={k}><span>{t(k===0?'AP.ParkingSpaces':'AP.Kind.'+k)}</span><b>{k===0?Math.ceil(v):unit(Math.ceil(v))}</b></div>:null)}</>}
   </>}
   {tab==='saved'&&<><div className={styles.sectionTitle}><span>{t('AP.SavedPlans')}</span>{mini('AP.DeleteAllPlans','trash',()=>command('deleteAllPlans'),!s.canDeleteAllPlans)}{info('AP.SaveHint')}</div>{!(s.saved||[]).length&&<div className={styles.empty}>{t('AP.UI.NoSaved')}</div>}{(s.saved||[]).map(v=><Tooltip key={v.id} tooltip={t('AP.InspectPlan')}><Button className={styles.disclosure} onSelect={()=>command('inspectPlan',String(v.id))}><Glyph name="library"/><span>{v.name||t('AP.UI.Plan')+' '+v.id}</span><Glyph name="right"/></Button></Tooltip>)}</>}
   </div>
  </div></div></Scrollable>}
 <Loading s={s} loading={loading} t={t}/>
 </div></>;
}
function NumericControl({label,text,setText,min,max,step=1,commit,unit="",disabled=false}:{label:string;text:string;setText:(v:string)=>void;min:number;max:number;step?:number;commit:(v:string)=>void;unit?:string;disabled?:boolean}){
 const track=React.useRef<HTMLDivElement>(null),latest=React.useRef(text),[dragging,drag]=React.useState(false);
 const change=(v:string)=>{latest.current=v;setText(v)};
 const at=(x:number)=>{const r=track.current?.getBoundingClientRect();if(!r||!r.width)return;const n=Math.min(max,Math.max(min,min+Math.round((Math.min(1,Math.max(0,(x-r.left)/r.width))*(max-min))/step)*step));change(String(Number(n.toFixed(4))))};
 React.useEffect(()=>{if(!dragging)return;if(disabled){drag(false);return;}const move=(e:MouseEvent)=>at(e.clientX),up=()=>{drag(false);commit(latest.current)};window.addEventListener('mousemove',move);window.addEventListener('mouseup',up);return()=>{window.removeEventListener('mousemove',move);window.removeEventListener('mouseup',up)}},[dragging,min,max,step,disabled]);
 const n=Number(text),pct=100*Math.min(1,Math.max(0,((Number.isFinite(n)?n:min)-min)/(max-min)));
 return <div className={styles.parameter}><span>{label}</span><div className={styles.numericRow}><div ref={track} className={styles.slider+(min<0&&max>0?' '+styles.signedSlider:'')} role="slider" aria-disabled={disabled} tabIndex={disabled?-1:0} aria-label={label} aria-valuemin={min} aria-valuemax={max} aria-valuenow={Number.isFinite(n)?n:min} onMouseDown={e=>{if(disabled||e.button!==0)return;e.preventDefault();e.stopPropagation();at(e.clientX);drag(true)}} onKeyDown={e=>{e.stopPropagation();if(disabled)return;if(['ArrowLeft','ArrowRight','Home','End'].includes(e.key)){e.preventDefault();const v=String(e.key==='Home'?min:e.key==='End'?max:Math.min(max,Math.max(min,(Number.isFinite(Number(text))?Number(text):min)+(e.key==='ArrowLeft'?-step:step))));change(v);commit(v)}}}><div className={styles.sliderFill} style={min<0&&max>0?{position:'absolute',left:Math.min(pct,100*(-min)/(max-min))+'%',width:Math.abs(pct-100*(-min)/(max-min))+'%'}:{width:pct+'%'}}/>{min<0&&max>0&&<div className={styles.sliderZero} style={{left:(100*(-min)/(max-min))+'%'}}><span>0</span></div>}<div className={styles.sliderThumb} style={{left:pct+'%'}}/></div><input disabled={disabled} aria-label={label} type="number" min={min} max={max} step={step} value={text} onFocus={e=>e.currentTarget.select()} onChange={e=>change(e.target.value)} onBlur={()=>{if(!disabled)commit(latest.current=text)}} onKeyDown={e=>{e.stopPropagation();if(e.key==='Enter')e.currentTarget.blur()}}/>{unit&&<b>{unit==='percent'?<Glyph name="percent"/>:unit}</b>}</div></div>;
}
function Parameter({label,value,min,max,step,commandName,unit="m",disabled=false}:{label:string;value:number;min:number;max:number;step:number;commandName:string;unit?:string;disabled?:boolean}){
 const [text,setText]=React.useState(String(value));React.useEffect(()=>setText(String(value)),[value]);
 const commit=(raw:string)=>{const n=Number(raw);if(raw.trim()&&Number.isFinite(n)&&n>=min&&n<=max&&n!==value)command(commandName,String(n));else setText(String(value));};
 return <NumericControl {...{label,text,setText,min,max,step,commit,unit,disabled}}/>;
}
function LandscapePanel(){
 const s=useStateData(),v=s.landscape;const {translate}=useLocalization();const t=(k:string)=>translate(k,(en as Record<string,string>)[k]||k)||k;
 const p=usePanelPlacement(!!v?.open,'ApartmentPlan.landscapePanel');const loading=useLoading(s);
 const [query,setQuery]=React.useState(''),[page,setPage]=React.useState(0);
 const items=(v?.trees||[]).map(a=>({...a,Name:assetName(a,translate)})).filter(a=>assetMatches(a,query));
 const pages=Math.max(1,Math.ceil(items.length/8)),current=Math.min(page,pages-1);React.useEffect(()=>setPage(0),[query]);
 if(!v?.open)return null;
 const busy=!!s.calculating||!!s.nativeCommitted||!!s.nativeDemolition||!!v.locked;
 const toggle=(label:string,on:boolean,run:()=>void,disabled=false,allowBusy=false)=><Button className={styles.check+' '+(on?styles.selected:'')} role="switch" aria-label={t(label)} aria-checked={on} selected={on} disabled={disabled||busy&&!allowBusy} onSelect={run}><span className={styles.switchTrack} aria-hidden="true"><span className={styles.switchThumb}/></span><span>{t(label)}</span></Button>;
 return <><div ref={p.shell} className={styles.panel+' '+styles.landscape} data-ap-landscape="true" style={p.position?{left:p.position.x,top:p.position.y,right:'auto'}:undefined} onMouseEnter={p.enter} onMouseLeave={p.leave} onFocusCapture={p.focus} onBlurCapture={p.blur} {...loading.events}>
  <div className={styles.header} onMouseDown={p.start}><Glyph name="park"/><strong>{t('AP.Landscape')}</strong><Tooltip tooltip={t('AP.UI.Close')}><Button className={styles.small} aria-label={t('AP.UI.Close')} onSelect={()=>command('landscapeClose')}><Glyph name="close"/></Button></Tooltip></div>
  <Scrollable className={styles.scrollViewport} vertical horizontal={false} trackVisibility="always" smooth={false}><div className={styles.body}>
   <div className={styles.switchRow}><Tooltip tooltip={t('AP.TreesHint')}>{toggle('AP.PlantTrees',v.enabled,()=>command('landscapeTrees'))}</Tooltip><Tooltip tooltip={t(v.grassAvailable?'AP.GrassHint':'AP.GrassMissing')}>{toggle('AP.GrassAuto',v.grass,()=>command('landscapeGrass'),!v.grassAvailable,true)}</Tooltip></div>
   <fieldset disabled={busy} className={styles.landscapeSettings}>
    <Tooltip tooltip={t('AP.TreePlacementHint')}><div className={styles.treePlacement}>{[0,1,2].map(i=><Button key={i} className={styles.tool+' '+((v.placement||0)===i?styles.selected:'')} selected={(v.placement||0)===i} disabled={busy} onSelect={()=>command('landscapePlacement',String(i))}><span>{t('AP.TreePlacement'+i)}</span></Button>)}</div></Tooltip>
    <Parameter label={t('AP.TreeDensity')} value={v.density} min={0} max={60} step={1} commandName="landscapeDensity" disabled={busy} unit={t('AP.TreeDensityUnit')}/>
    <div className={styles.sectionTitle}><span>{t('AP.TreeAges')}</span><span>{t('AP.PlannedTrees')} {v.count??0}</span></div><div className={styles.treeAges}>{[0,1,2,3].map(i=><Tooltip key={i} tooltip={t('AP.TreeAge'+i)}><Button className={styles.treeAge+' '+(v.ages&(1<<i)?styles.selected:'')} role="checkbox" aria-label={t('AP.TreeAge'+i)} aria-checked={!!(v.ages&(1<<i))} selected={!!(v.ages&(1<<i))} disabled={busy} onSelect={()=>command('landscapeAge',String(i))}><Glyph name={'treeAge'+i}/></Button></Tooltip>)}</div>
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
 const s=useStateData(),v=s.inspector;const {translate}=useLocalization();const t=(k:string)=>translate(k,(en as Record<string,string>)[k]||k)||k;
 const loading=useLoading(s);
 React.useEffect(()=>()=>trigger('ApartmentPlan','focus',false),[v?.id]);
 if(!v)return null;
 return <><PanelDismiss close={()=>{if(!loading.locked)command('closeInspector')}}/><div className={styles.panel+' '+styles.inspector} data-ap-inspector="true" onMouseEnter={()=>trigger('ApartmentPlan','focus',true)} onMouseLeave={()=>trigger('ApartmentPlan','focus',false)} {...loading.events}>
  <div className={styles.header}><Glyph name="library"/><strong>{v.name||t('AP.UI.Plan')+' '+v.id}</strong><Tooltip tooltip={t('AP.UI.Close')}><Button className={styles.small} aria-label={t('AP.UI.Close')} onSelect={()=>command('closeInspector')}><Glyph name="close"/></Button></Tooltip></div>
  <Scrollable className={styles.inspectorScroll} vertical horizontal={false} trackVisibility="scrollable" smooth={false}><div className={styles.body}>
  <div className={styles.inspectorMetrics}>{[['AP.BuildingCount',v.buildings,null],['AP.ParkingSpaces',v.built?(v.parkingCapacity??v.parking):v.parking,v.built?v.parked:null],['AP.Homes',v.built?(v.homeCapacity??v.homes):v.homes,v.built?v.occupiedHomes:null]].map(([label,total,used])=><div key={String(label)}><span>{t(String(label))}</span><b className={styles.occupancyText}>{`${used!=null?used+'/':''}${total??t('AP.Unknown')}${v.built&&label!=='AP.BuildingCount'?' ('+(used!=null&&Number(total)>0?(100*Number(used)/Number(total)).toFixed(1):'--.-')+'%)':''}`}</b></div>)}</div>
   {v.built&&s.nativeAuditAvailable&&s.nativeNotice&&<div className={styles.nativeReport+" "+(s.statusError?styles.statusError:"")}><div className={styles.nativeNotice}>{t(s.nativeNotice)}</div></div>}
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
 return <div className={styles.edit}><div className={styles.assetNumbers}>{[[t('AP.Height'),height,H],[t('AP.Footprint'),foot,F],[t('AP.Homes'),homes,N]].map(([label,value,set],i)=><NumericControl key={i} label={label as string} text={value as string} setText={set as (v:string)=>void} min={i===0?1:0} max={i===0?1000:i===1?a.Width*a.Depth:10000} step={i===2?1:.1} commit={()=>{}}/>)}</div>{!valid&&<div className={styles.warning}>{t('AP.InvalidAsset')}</div>}<Tooltip tooltip={t('AP.InvalidAsset')}><Button className={styles.apply} disabled={!valid} onSelect={()=>command('asset',JSON.stringify({Id:a.Id,Height:h,Footprint:f,Homes:n}))}><Glyph name="check"/><span>{t('AP.Apply')}</span></Button></Tooltip></div>;
}
class PlanBoundary extends React.Component<{}, {failed:boolean}>{state={failed:false};static getDerivedStateFromError(){return {failed:true}}componentDidCatch(error:Error,info:React.ErrorInfo){trace('render.failed',error.message+' '+info.componentStack)}render(){return this.state.failed?<div className={styles.panel}>Apartment Plan UI error. Logs/ApartmentPlan.</div>:<><Panel/><LandscapePanel/><Inspector/></>}}
const register:ModRegistrar=r=>{r.append('GameTopRight',MenuButton);r.append('Game',PlanBoundary);};
export const hasCSS=true;export default register;








function TowerControls({s,assets}:{s:State;assets:Asset[]}){
 const {translate}=useLocalization();const t=(k:string)=>translate(k,(en as Record<string,string>)[k]||k)||k;
 const [q,setQ]=React.useState('주차장');const [open,setOpen]=React.useState('');const v=s.tower;if(!v)return null;const locked=!!s.nativeActive||!!s.nativeCommitted;
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
 const toggle=(label:string,on:boolean,cmd:string)=><Button className={styles.check+' '+(on?styles.selected:'')} role="switch" aria-checked={on} disabled={locked} onSelect={()=>command(cmd)}><span className={styles.switchTrack}><span className={styles.switchThumb}/></span><span>{t(label)}</span></Button>;
 return <div className={styles.towerControls}>
 <label className={styles.towerField}><span>{t('AP.Tower.ParkingSearch')}</span><input value={q} disabled={locked} onChange={e=>setQ(e.target.value)}/></label>
 {select('AP.Tower.Parking',v.parking,'towerParking',parking.map(a=>({id:a.Id,name:a.Name+' · '+a.Width+'×'+a.Depth+'m'})))}


 <div className={styles.switchRow}>{toggle('AP.Tower.Paving',v.paving,'towerPaving')}{toggle('AP.Tower.Facility',v.facility,'towerFacility')}</div>
 {v.paving&&select('AP.Tower.EntranceSurface',v.entranceSurface,'towerEntranceSurface',surfaces.map(a=>({id:a.Id,name:surfaceName(a)})))}
 <small>{t('AP.Tower.AssetHint')}</small>
 <div className={styles.switchRow}>{(v.rules||[]).filter(r=>r.max===1).map(r=><Button key={r.id} className={styles.check+' '+(r.value?styles.selected:'')} role="switch" aria-checked={!!r.value} disabled={locked} onSelect={()=>command('towerRule',r.id+'|'+(r.value?0:1))}><span className={styles.switchTrack}><span className={styles.switchThumb}/></span><span>{t('AP.Tower.Rule.'+r.id)}</span></Button>)}</div>
 <div className={styles.sliderGroup}>{(v.rules||[]).filter(r=>r.max!==1).map(r=><TowerRule key={r.id} rule={r} locked={locked} label={t('AP.Tower.Rule.'+r.id)}/>)}</div>
 </div>;
}

function TowerRule({rule:r,locked,label}:{rule:{id:string;value:number;min:number;max:number;step:number};locked:boolean;label:string}){
 const [value,setValue]=React.useState(String(r.value));React.useEffect(()=>setValue(String(r.value)),[r.value]);
 return <NumericControl disabled={locked} label={label} text={value} setText={setValue} min={r.min} max={r.max} step={r.step} commit={text=>{if(!locked&&Number.isFinite(Number(text)))command('towerRule',r.id+'|'+text)}}/>;
}

