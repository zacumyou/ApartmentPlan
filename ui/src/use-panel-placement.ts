import React from 'react';
import {trigger} from 'cs2/api';

// Stored pixel coordinates are clamped again after layout, reopen and resize.
export function usePanelPlacement(open:boolean,storageKey="ApartmentPlan.panel") {
 const [layout,setLayout]=React.useState(0),[collapsed,setCollapsed]=React.useState(false);
 const [position,setPosition]=React.useState<{x:number;y:number}|null>(null);
 const [ready,setReady]=React.useState(false);
 const shell=React.useRef<HTMLDivElement>(null);
 const drag=React.useRef<{x:number;y:number;left:number;top:number}|null>(null);
 const hover=React.useRef(false),focus=React.useRef(false);
 const syncFocus=()=>trigger('ApartmentPlan','focus',!!(drag.current||hover.current||focus.current));
 React.useEffect(()=>{try{const p=JSON.parse(localStorage.getItem(storageKey)||'{}');setLayout(Number.isInteger(p.layout)?p.layout&3:0);setCollapsed(!!p.collapsed);if(p.position&&Number.isFinite(p.position.x)&&Number.isFinite(p.position.y))setPosition(p.position);}catch{}setReady(true);},[]);
 React.useEffect(()=>{if(ready)try{const p=JSON.parse(localStorage.getItem(storageKey)||'{}');localStorage.setItem(storageKey,JSON.stringify({...p,layout,collapsed,position}));}catch{}},[ready,layout,collapsed,position]);
 React.useEffect(()=>{
  const clamp=()=>{const r=shell.current?.getBoundingClientRect();if(!r)return;const x=Math.max(0,Math.min(r.left,window.innerWidth-r.width)),y=Math.max(0,Math.min(r.top,window.innerHeight-r.height));if(Math.abs(x-r.left)>.5||Math.abs(y-r.top)>.5)setPosition(p=>p&&Math.abs(p.x-x)<.5&&Math.abs(p.y-y)<.5?p:{x,y});};
  // Gameface can expose the previous layout's bounds during React's layout effect.
  // Recheck after its asynchronous layout pass, including viewport resize.
  let timers:ReturnType<typeof setTimeout>[]=[];
  const schedule=()=>{timers.forEach(clearTimeout);timers=[setTimeout(clamp,0),setTimeout(clamp,100),setTimeout(clamp,300)];};
  schedule();window.addEventListener('resize',schedule);
  return()=>{timers.forEach(clearTimeout);window.removeEventListener('resize',schedule);};
 });
 React.useEffect(()=>{
  const move=(e:MouseEvent)=>{const d=drag.current,r=shell.current?.getBoundingClientRect();if(!d||!r)return;setPosition({x:Math.max(0,Math.min(window.innerWidth-r.width,d.left+e.clientX-d.x)),y:Math.max(0,Math.min(window.innerHeight-r.height,d.top+e.clientY-d.y))});};
  const up=()=>{drag.current=null;syncFocus();};
  const blur=()=>{drag.current=null;hover.current=focus.current=false;syncFocus();};
  window.addEventListener('mousemove',move);window.addEventListener('mouseup',up);window.addEventListener('blur',blur);
  return()=>{window.removeEventListener('mousemove',move);window.removeEventListener('mouseup',up);window.removeEventListener('blur',blur);trigger('ApartmentPlan','focus',false);};
 },[]);
 React.useEffect(()=>{if(!open){drag.current=null;hover.current=focus.current=false;syncFocus();}},[open]);
 return {layout,setLayout,collapsed,setCollapsed,position,shell,
  start:(e:React.MouseEvent)=>{if(e.button!==0||(e.target as Element).closest('button'))return;const r=shell.current?.getBoundingClientRect();if(r){drag.current={x:e.clientX,y:e.clientY,left:r.left,top:r.top};syncFocus();e.preventDefault();}},
  enter:()=>{hover.current=true;syncFocus();},leave:()=>{hover.current=false;syncFocus();},
  focus:(e:React.FocusEvent)=>{focus.current=!!(e.target as Element).closest('input,textarea,[contenteditable="true"]');syncFocus();},blur:(e:React.FocusEvent)=>{focus.current=!!(e.relatedTarget&&shell.current?.contains(e.relatedTarget as Node)&&(e.relatedTarget as Element).closest('input,textarea,[contenteditable="true"]'));syncFocus();}
 };
}

