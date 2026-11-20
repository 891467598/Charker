// Explicit --startup-test only; never opens real devices or writes the real Run key.
(async()=>{const results=[],check=(name,ok)=>{if(!ok)throw Error(name);results.push('PASS '+name);},wait=ms=>new Promise(r=>setTimeout(r,ms));try{
 let state=await call('__testStartup');check('background flag is applied',state.background);check('main window starts hidden with no taskbar button',!state.visible&&!state.showInTaskbar&&state.opacity===0);check('tray icon stays available',state.tray);
 await wait(1200);state=await call('__testStartup');check('saved desktop widget appears while main stays hidden',state.widget&&!state.visible);const first=await call('init');await wait(1200);check('hidden app continues servicing requests',!!(await call('init')).prefs);
 state=await call('__testStartup',{restore:true});check('unlock restores the native widget without opening main window',state.widget&&!state.visible);
 state=await call('__testStartup',{restore:true});check('repeated resume notifications keep widget available and main hidden',state.widget&&!state.visible);
 state=await call('__testStartup',{open:true});check('manual tray-open path restores visible main window',state.visible&&state.showInTaskbar&&state.opacity===1);await wait(600);check('widget remains shown after manually opening app',(await call('__testStartup')).widget);
 window.chrome.webview.postMessage({id:'startup-qa',action:'__testResult',args:{ok:true,results}});
 }catch(e){window.chrome.webview.postMessage({id:'startup-qa',action:'__testResult',args:{ok:false,results,error:e.stack}});}})();
