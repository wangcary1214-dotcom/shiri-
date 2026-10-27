using System;
using System.Threading.Tasks;

namespace Shiri {
 // One writer per file. Rapid changes coalesce to the latest full snapshot;
 // atomic replace and .bak remain the responsibility of FocusStorage.Save.
 public sealed class FocusSaveQueue {
  readonly object gate=new object();readonly Action<FocusData> write;
  FocusData pending;Task worker;bool processing;Exception error;
  public FocusSaveQueue(string path):this(delegate(FocusData snapshot){FocusStorage.Save(path,snapshot);}){}
  internal FocusSaveQueue(Action<FocusData> save){write=save;}
  public string Error{get{lock(gate)return error==null ? null : error.Message;}}
  public Task Enqueue(FocusData snapshot){lock(gate){pending=snapshot;if(!processing){processing=true;worker=Task.Run((Action)Drain);}return worker;}}
  void Drain(){while(true){FocusData snapshot;lock(gate){if(pending==null){processing=false;return;}snapshot=pending;pending=null;}try{write(snapshot);lock(gate)error=null;}catch(Exception failure){lock(gate)error=failure;}}}
  public bool Flush(FocusData snapshot){Enqueue(snapshot).Wait();lock(gate)return error==null;}
 }
}
