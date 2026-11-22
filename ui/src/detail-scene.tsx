import React from 'react';

// Each pair shares a camera and scale. Yellow identifies the measured feature.
export function detailScene(kind:string,v:number,base:number,example:number){
 const extent=Math.max(base,example,1),t=v/extent,gold='#ffce79',road='#687d8a',walk='#81b89b',lot='#96b6cc';
 const path=(d:string,color:string,width=2,dash?:string)=><path d={d} fill="none" stroke={color} strokeWidth={width} strokeDasharray={dash}/>;
 const rect=(x:number,y:number,w:number,h:number,color=lot)=><rect x={x} y={y} width={w} height={h} fill={color}/>;
 const dim=(x:number,y:number,end:number)=><>{path(`M${x} ${y}H${end} M${x} ${y-4}v8 M${end} ${y-4}v8`,gold)}</>;
 const mark=(x:number,y:number)=><circle cx={x} cy={y} r="2.7" fill={gold}/>;
 const district=(count:number)=>Array.from({length:count},(_,i)=><rect key={i} x={10+i%10*12} y={13+Math.floor(i/10)*13} width="8" height="9" fill={lot}/>);
 switch(kind){
 case 'curveLength':case 'RoadRadius':case 'WalkRadius':case 'curveRadius':{
  const r=55*t,d=`M16 ${20+r} A${r} ${r} 0 0 1 ${16+r} 20`,w=kind==='WalkRadius'?5:10;
  return <>{path('M16 90V20H137','#59717d',1,'3 3')}{path(`M16 90V${20+r} A${r} ${r} 0 0 1 ${16+r} 20H137`,kind==='WalkRadius'?walk:road,w)}{path(d,gold,3)}{mark(16,20+r)}{mark(16+r,20)}{kind!=='curveLength'&&path(`M${16+r} 20v${r}H16`,gold,1,'3 2')}</>;
 }
 case 'surfaceRadius':{const r=24*t;return <><rect x="23" y="14" width="98" height="70" rx={r} fill="#557a63"/>{path(`M23 ${14+r}A${r} ${r} 0 0 1 ${23+r} 14`,gold,3)}{path('M23 44V14H54',lot,1,'3 2')}</>}
 case 'lineOverlap':{const overlap=v*85;return <>{rect(15,18,114,64,road)}{path('M20 32h102 M20 68h102','#dbe6e9',2)}{rect(24,32,49,36,'#536b77')}{rect(73-overlap,32,49,36,'#79918d')}{rect(73-overlap,32,overlap,36,gold)}{dim(73-overlap,78,73)}</>}
 case 'parkingGreenWidth':{const w=32*t;return <>{rect(12,12,120,72,road)}{[26,49,92,115].map(x=><path key={x} d={`M${x} 19v33`} stroke="#c1cfd6"/>)}{rect(72-w/2,16,w,39,walk)}{dim(72-w/2,68,72+w/2)}</>}
 case 'villaRows':return <>{path('M8 78H136',road,10)}{Array.from({length:v*5},(_,i)=><rect key={i} x={13+i%5*25} y={51-Math.floor(i/5)*30} width="18" height="21" fill={lot}/>)}</>;
 case 'villaMaxBuildings':case 'MaxBuildings':return <>{path('M7 87H137',road,6)}{district(Math.max(1,Math.round(t*50)))}</>;
 case 'villaConnectionDistance':case 'BuildingBoundary':case 'WalkBoundary':case 'BoundaryParallelRange':{
  const x=20+63*t;return <>{path('M20 6V91',lot,2,'4 3')}{kind==='villaConnectionDistance'?path(`M${x+5} 8V91`,road,10):kind==='BuildingBoundary'?rect(x,20,32,38):path(`M${x+2} 12V61Q${x+2} 71 ${x+14} 71H133`,walk,4)}{dim(20,77,x)}{kind==='BoundaryParallelRange'&&rect(20,11,x-20,8,'#536e61')}</>;
 }
 case 'entranceLength':case 'EntranceInset':{const y=76-50*t;return <>{path('M4 82H140',road,10)}{path('M63 82V8',road,9)}{kind==='EntranceInset'?path(`M63 ${y}H126`,road,7):mark(63,y)}{path(`M52 77V${y} M48 77h8 M48 ${y}h8`,gold)}</>}
 case 'PackingStep':case 'WalkCell':{const pitch=9+24*t;return <>{Array.from({length:Math.ceil(136/pitch)},(_,i)=><g key={i}>{path(`M${7+i*pitch} 9V89 M7 ${9+i*pitch}H135`,'#66808c',1)}{kind==='PackingStep'&&Array.from({length:Math.floor(78/pitch)+1},(_,j)=><circle key={j} cx={7+i*pitch} cy={9+j*pitch} r="2" fill={lot}/>)}</g>)}{dim(7,69,7+pitch)}</>}
 case 'GapTolerance':{const nominal=50,actual=nominal-v*5;return <>{rect(10,27,26,40)}{path(`M${36+nominal} 16v60`,gold,1,'3 2')}{rect(36+actual,27,26,40)}{dim(36+actual,77,36+nominal)}</>}
 case 'FacilityWidth':case 'FacilityDepth':{const w=kind==='FacilityWidth'?80*t:55,h=kind==='FacilityDepth'?56*t:40;return <>{path('M6 81H138',road,8)}{rect(36,72-h,w,h,'#709b91')}{kind==='FacilityWidth'?dim(36,73-h-8,36+w):path(`M27 72v-${h} M23 72h8 M23 ${72-h}h8`,gold)}</>}
 case 'CurveGap':{const gap=48*t;return <>{path(`M9 79V62Q9 47 24 47H${24+gap}Q${40+gap} 47 ${40+gap} 31V9`,road,9)}{path(`M24 47h${gap}`,gold,3)}{dim(24,63,24+gap)}</>}
 case 'BranchGap':case 'TurnaroundGap':{const x=36+52*t;return <>{path('M34 76V37a18 18 0 1 1 36 0v6',road,8)}{kind==='BranchGap'?<>{path('M34 76H135',road,8)}{path(`M${x} 76V49`,road,7)}{dim(36,88,x)}</>:<>{path(`M${x+38} 14V86`,road,8)}{dim(74,45,x+34)}</>}</>}
 case 'ApproachExclusion':case 'JunctionExclusion':{const r=31*t;return <>{path('M9 50H136',kind==='ApproachExclusion'?road:walk,kind==='ApproachExclusion'?9:4)}{path('M63 87V50',walk,4)}<circle cx="63" cy="50" r={r} fill="none" stroke={gold} strokeDasharray="3 2"/>{mark(63,50)}{dim(63,50,63+r)}</>}
 case 'LoopBridgeGap':return <>{[10,85].map(x=><rect key={x} x={x} y="24" width="43" height="48" rx="9" fill="none" stroke={walk} strokeWidth="4"/>)}{v===Math.max(base,example)&&path('M53 48H85',gold,4)}{dim(55,80,83)}</>;
 case 'CrossAreaPercent':return <>{rect(28,22,86,51,'#344c40')}<rect x="28" y="22" width="86" height="51" rx="6" fill="none" stroke={walk} strokeWidth="4"/>{v===Math.min(base,example)&&path('M28 48h86',gold,4)}{rect(5,31,15,22)}</>;
 case 'WalkApproachAngle':{const a=v*Math.PI/180,x=63+60*Math.cos(a),y=77-60*Math.sin(a);return <>{path('M5 83H139',road,10)}{path(`M63 77L${x} ${y}`,walk,5)}{path(`M88 77 A25 25 0 0 0 ${63+25*Math.cos(a)} ${77-25*Math.sin(a)}`,gold)}</>}
 case 'ParallelGap':case 'WalkParallelGap':case 'RoadWalkGap':case 'CrossWalkGap':case 'FrontCurveGap':{
  const gap=36*t,first=kind==='RoadWalkGap'||kind==='ParallelGap'||kind==='FrontCurveGap'?10:4,second=kind==='ParallelGap'||kind==='FrontCurveGap'?10:4;
  const center=kind==='ParallelGap',y=24+gap+(center?0:(first+second)/2);
  return <>{kind==='FrontCurveGap'&&rect(38,4,35,14)}{path('M8 24H136',first===10?road:walk,first)}{path(kind==='FrontCurveGap'?`M8 ${y+20}Q47 ${y-20} 88 ${y+20}T136 ${y+20}`:`M8 ${y}H136`,second===10?road:walk,second)}{path(`M47 ${24+(center?0:first/2)}V${y-(center?0:second/2)} m-4 0h8 M43 ${24+(center?0:first/2)}h8`,gold)}</>;
 }
 default:return null;
 }
}
