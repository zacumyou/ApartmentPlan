import React from 'react';
import {InputActionConsumer} from 'cs2/input';

// Native input stack consumes ESC before Pause Menu, even with the pointer over the world.
// Mount only while an AP panel is visible; unmounting restores normal game input.
export function PanelDismiss({close}:{close:()=>void}) {
 const dismiss=React.useCallback(()=>{close();return true;},[close]);
 const actions=React.useMemo(()=>({'Pause Menu':dismiss,Close:dismiss,Back:dismiss}),[dismiss]);
 return <InputActionConsumer actions={actions} ignoreFocusState/>;
}
