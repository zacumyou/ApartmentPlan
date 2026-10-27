import {trigger} from 'cs2/api';
export function trace(name:string,value=''){
 const detail=String(value).slice(0,2000);
 // Console survives missing backend bindings, which is essential for initialization failures.
 if(/failed|error|failure/i.test(name))console.error('[ApartmentPlan] '+name+' '+detail);
 try{trigger('ApartmentPlan','trace',name,detail)}catch(e){console.error('[ApartmentPlan] trace bridge unavailable '+String(e))}
}
