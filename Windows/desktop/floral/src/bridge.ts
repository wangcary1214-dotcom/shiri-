export interface Note {id:string;date:string;kind:'diary'|'note';title:string;content:string;mood:string;color:string;revision:number;updatedTicks:number;deleted:boolean}
export interface Snapshot {notes:Note[];date:string}
declare global {interface Window {chrome?:{webview:{postMessage:(v:unknown)=>void;addEventListener:(t:string,f:(e:{data:any})=>void)=>void}};shiriFlush?:()=>Promise<boolean>;shiriSelectDate?:(date:string)=>Promise<void>}}
let sequence=0;
const pending=new Map<number,{resolve:(x:any)=>void;reject:(e:Error)=>void}>();
export const listeners=new Set<(x:Snapshot)=>void>();
window.chrome?.webview.addEventListener('message',e=>{const m=e.data;if(m.event==='notes'){listeners.forEach(f=>f(m.data));return;}const p=pending.get(m.id);if(!p)return;pending.delete(m.id);m.error?p.reject(new Error(m.error)):p.resolve(m.data);});
export function rpc<T=any>(action:string,request:unknown={}):Promise<T>{return new Promise((resolve,reject)=>{if(!window.chrome?.webview)return reject(new Error('请从拾日打开花笺日记'));const id=++sequence;pending.set(id,{resolve,reject});window.chrome.webview.postMessage({id,action,request});});}
export function dirty(value:boolean){window.chrome?.webview.postMessage({action:'dirty',request:value});}
export function convertFileSrc(path:string){const normalized=path.replaceAll('\\','/');const name=normalized.split('/').pop();return /^[a-f\d]{32}\.png$/.test(name??'')?'https://shiri-images.local/'+name:'';}
export function openUrl(url:string){return rpc('openLink',{url});}
