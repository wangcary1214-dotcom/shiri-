using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Shiri {
 public sealed class FloralView:Grid {
  public readonly WebView2 Browser=new WebView2();
  public bool Dirty,Ready;
  public string Date;
  readonly FloralStore store;readonly string noteId;readonly Action Changed;readonly Action<string> DateChanged;readonly Action<string> Pin;
  readonly string profile;
  public FloralView(FloralStore value,string date,string id,string browserProfile,Action changed,Action<string> dateChanged,Action<string> pin){store=value;Date=date;noteId=id;profile=browserProfile;Changed=changed;DateChanged=dateChanged;Pin=pin;Children.Add(new TextBlock{Text="正在打开花笺日记…",Margin=new Thickness(24),Foreground=Controller.B("#819575")});Children.Add(Browser);Loaded+=Start;}
  bool started;
  async void Start(object sender,RoutedEventArgs args){if(started)return;started=true;try{
   string assets=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"floral","dist");if(!File.Exists(Path.Combine(assets,"index.html")))throw new Exception("缺少花笺界面文件，请使用完整运行包升级。");
   var env=await CoreWebView2Environment.CreateAsync(null,profile,new CoreWebView2EnvironmentOptions("--disable-background-networking"));
   await Browser.EnsureCoreWebView2Async(env);var core=Browser.CoreWebView2;core.Settings.AreDefaultContextMenusEnabled=true;core.Settings.AreDevToolsEnabled=false;core.Settings.IsStatusBarEnabled=false;core.Settings.IsZoomControlEnabled=true;
   core.SetVirtualHostNameToFolderMapping("shiri-journal.local",assets,CoreWebView2HostResourceAccessKind.DenyCors);
   string images=store.ImageDirectory??Path.Combine(profile,"preview-images");Directory.CreateDirectory(images);foreach(var p in store.MemoryImages)File.WriteAllBytes(Path.Combine(images,p.Key),p.Value);
   core.SetVirtualHostNameToFolderMapping("shiri-images.local",images,CoreWebView2HostResourceAccessKind.Allow);
   core.WebMessageReceived+=Message;core.NavigationStarting+=delegate(object s,CoreWebView2NavigationStartingEventArgs e){Uri uri;if(!Uri.TryCreate(e.Uri,UriKind.Absolute,out uri)||uri.Host!="shiri-journal.local")e.Cancel=true;};
   core.NewWindowRequested+=delegate(object s,CoreWebView2NewWindowRequestedEventArgs e){e.Handled=true;OpenLink(e.Uri);};
   core.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All);
   core.WebResourceRequested+=delegate(object s,CoreWebView2WebResourceRequestedEventArgs e){Uri uri;bool local=Uri.TryCreate(e.Request.Uri,UriKind.Absolute,out uri)&&(uri.Host=="shiri-journal.local"||uri.Host=="shiri-images.local"||uri.Scheme=="data"||uri.Scheme=="blob");if(!local)e.Response=env.CreateWebResourceResponse(new MemoryStream(),403,"Remote requests disabled","");};
   Browser.DefaultBackgroundColor=System.Drawing.Color.FromArgb(248,249,246);core.Navigate("https://shiri-journal.local/index.html"+(noteId==null?"":"?note="+Uri.EscapeDataString(noteId)));
  }catch(Exception error){Browser.Visibility=Visibility.Collapsed;var panel=new StackPanel{Margin=new Thickness(24)};panel.Children.Add(new TextBlock{Text="花笺日记暂时无法打开",FontSize=20,Foreground=Controller.B("#356A51")});panel.Children.Add(new TextBlock{Text=error.Message+"\n请确认已安装 Microsoft Edge WebView2 Runtime，并保留完整运行包中的 floral 与依赖文件。\n原有日记及草稿未被删除。",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,0)});Children.Add(panel);}}
  void OpenLink(string value){Uri uri;if(Uri.TryCreate(value,UriKind.Absolute,out uri)&&(uri.Scheme=="https"||uri.Scheme=="http"))Process.Start(new ProcessStartInfo(value){UseShellExecute=true});}
  object Snapshot(){return new{notes=store.Data.notes,date=Date};}
  public void Notify(){if(Ready&&Browser.CoreWebView2!=null)Browser.CoreWebView2.PostWebMessageAsJson(FloralStore.Json().Serialize(new{ @event="notes",data=Snapshot()}));}
  readonly Dictionary<string,TaskCompletionSource<bool>> flushes=new Dictionary<string,TaskCompletionSource<bool>>();
  public async Task<bool> Flush(){if(!Ready)return !Dirty;string token=Guid.NewGuid().ToString("N");var completion=new TaskCompletionSource<bool>();flushes[token]=completion;try{await Browser.ExecuteScriptAsync("(window.shiriFlush ? window.shiriFlush() : Promise.resolve(true)).then(v=>chrome.webview.postMessage({action:'flushed',request:{token:"+FloralStore.Json().Serialize(token)+",ok:v}})).catch(()=>chrome.webview.postMessage({action:'flushed',request:{token:"+FloralStore.Json().Serialize(token)+",ok:false}}));");var finished=await Task.WhenAny(completion.Task,Task.Delay(8000));return finished==completion.Task&&await completion.Task;}catch{return false;}finally{flushes.Remove(token);}}
  public async Task SelectDate(string date){Date=date;if(Ready)await Browser.ExecuteScriptAsync("window.shiriSelectDate && window.shiriSelectDate("+FloralStore.Json().Serialize(date)+").catch(()=>{});");}
  async void Message(object sender,CoreWebView2WebMessageReceivedEventArgs e){Uri origin;if(!Uri.TryCreate(e.Source,UriKind.Absolute,out origin)||origin.Host!="shiri-journal.local")return;
   int messageId=0;try{var json=FloralStore.Json();var message=json.Deserialize<Dictionary<string,object>>(e.WebMessageAsJson);string action=(string)message["action"];if(message.ContainsKey("id"))messageId=Convert.ToInt32(message["id"]);object raw=message.ContainsKey("request")?message["request"]:null;
    if(action=="dirty"){Dirty=Convert.ToBoolean(raw);return;}if(action=="flushed"){var ack=raw as Dictionary<string,object>;TaskCompletionSource<bool> pending;if(ack!=null&&flushes.TryGetValue(Convert.ToString(ack["token"]),out pending))pending.TrySetResult(Convert.ToBoolean(ack["ok"]));return;}
    var request=raw as Dictionary<string,object>??new Dictionary<string,object>();Func<string,string> str=key=>request.ContainsKey(key)?Convert.ToString(request[key]):"";object result=null;
    if(action=="load"){Ready=true;result=Snapshot();}
    else if(action=="save"){var note=json.Deserialize<FloralNote>(json.Serialize(request["note"]));result=store.Save(note);Changed();}
    else if(action=="create"){var note=new FloralNote{date=str("date"),kind=str("kind")};result=store.Save(note);Changed();}
    else if(action=="date"){Date=str("date");DateTime parsed;if(!DateTime.TryParseExact(Date,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out parsed))throw new Exception("日期无效。");if(DateChanged!=null)DateChanged(Date);}
    else if(action=="pin"){if(Pin!=null)Pin(str("id"));}
    else if(action=="topmost"){var window=System.Windows.Window.GetWindow(this);window.Topmost=!window.Topmost;}
    else if(action=="openLink")OpenLink(str("url"));
    else if(action=="image"){string data=str("data");if(data.Length>28*1024*1024||!data.StartsWith("data:image/",StringComparison.Ordinal))throw new Exception("图片格式无效或过大。");result=store.AddImage(Convert.FromBase64String(data.Substring(data.IndexOf(',')+1)));}
    else if(action=="pickImage"){var dialog=new Microsoft.Win32.OpenFileDialog{Title="插入图片",Filter="图片|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff"};result=dialog.ShowDialog(System.Windows.Window.GetWindow(this))==true?store.AddImage(DiaryDocument.ReadImage(dialog.FileName)):"";}
    else if(action=="import"){var dialog=new Microsoft.Win32.OpenFileDialog{Title="导入到所选日期",Filter="Markdown|*.md;*.markdown"};if(dialog.ShowDialog(System.Windows.Window.GetWindow(this))==true){if(new FileInfo(dialog.FileName).Length>512*1024)throw new Exception("Markdown文件过大，最多10万字。");var note=new FloralNote{date=str("date"),kind="note",title=Path.GetFileNameWithoutExtension(dialog.FileName),content=FloralFiles.ImportImages(File.ReadAllText(dialog.FileName,Encoding.UTF8),dialog.FileName,store)};result=store.Save(note);Changed();}}
    else if(action=="export"){var note=store.Data.notes.Single(n=>n.id==str("id"));var dialog=new Microsoft.Win32.SaveFileDialog{FileName="拾日-"+note.date+".md",Filter="Markdown|*.md"};if(dialog.ShowDialog(System.Windows.Window.GetWindow(this))==true){FloralFiles.Export(note,dialog.FileName,store);}}
    else throw new Exception("不支持的日记操作。");
    Browser.CoreWebView2.PostWebMessageAsJson(json.Serialize(new{id=messageId,data=result}));
   }catch(Exception error){if(Browser.CoreWebView2!=null)Browser.CoreWebView2.PostWebMessageAsJson(FloralStore.Json().Serialize(new{id=messageId,error=error.Message}));}await Task.FromResult(0);
  }
 }
 public partial class Controller {
  FloralStore floralStore;FloralView floralView;readonly List<FloralView> floralViews=new List<FloralView>();readonly Dictionary<string,Window> floralPins=new Dictionary<string,Window>();bool floralClosing;
  public bool FloralLegacyChecks;
  void RenderDiary(){if(FloralLegacyChecks){RenderLegacyDiary();return;}C<ScrollViewer>("TaskScroll").Visibility=Visibility.Collapsed;C<Border>("WeekCard").Visibility=Visibility.Collapsed;C<Grid>("ListToolbar").Visibility=Visibility.Collapsed;var host=C<ContentControl>("DiaryHost");host.Visibility=Visibility.Visible;
   try{EnsureFloralStore();
    if(floralView==null){floralView=MakeFloralView(null);floralViews.Add(floralView);}host.Content=floralView;if(floralView.Date!=Day(selected)){var unused=floralView.SelectDate(Day(selected));}C<TextBlock>("FooterMessage").Text="花笺日记与便签 · 按日期归档 · Markdown · 本机自动保存";
   }catch(Exception error){floralStore=null;host.Content=new TextBlock{Text="日记暂时无法读取，原文件已保留。\n"+error.Message,Margin=new Thickness(24),TextWrapping=TextWrapping.Wrap};}
  }
  void InitializeFloral(){Window.Loaded+=delegate{try{if(path!=null&&File.Exists(Path.Combine(Path.GetDirectoryName(path),"floral-notes.json"))){EnsureFloralStore();foreach(string id in floralStore.Data.pins.ToList())PinFloral(id);}}catch(Exception error){C<TextBlock>("FooterMessage").Text="桌面便签未恢复，原数据已保留："+error.Message;}};}
  void EnsureFloralStore(){if(floralStore!=null)return;floralStore=new FloralStore(path==null?null:Path.Combine(Path.GetDirectoryName(path),"floral-notes.json"));if(!floralStore.Data.legacyImported&&diaryLoadError!=null)throw new Exception(diaryLoadError);floralStore.Migrate(diary,diaryPath);Window.Closing+=CloseFloral;Window.Closed+=delegate{foreach(var p in floralPins.Values.ToList())p.Close();foreach(var v in floralViews.ToList())v.Browser.Dispose();};}
  FloralView MakeFloralView(string id){string root=path==null?Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview-data"):Path.GetDirectoryName(path);return new FloralView(floralStore,Day(selected),id,Path.Combine(root,".journal-webview2"),delegate{foreach(var v in floralViews)v.Notify();RenderNavigation();},delegate(string value){selected=DateTime.ParseExact(value,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);weekStart=Monday(selected);month=new DateTime(selected.Year,selected.Month,1);calendarYear=selected.Year;RenderDates();},PinFloral);}
  void PinFloral(string id){EnsureFloralStore();if(floralPins.ContainsKey(id)){floralPins[id].Activate();return;}var view=MakeFloralView(id);var window=new Window{Title="拾日 · 桌面便签",Width=390,Height=510,MinWidth=300,MinHeight=300,Background=B("#F6F3EC"),Content=view,Topmost=true};floralStore.SetPin(id,true);floralViews.Add(view);floralPins[id]=window;bool closing=false;window.Closing+=async delegate(object s,System.ComponentModel.CancelEventArgs e){if(closing||floralClosing)return;e.Cancel=true;await Task.Yield();if(await view.Flush()){try{floralStore.SetPin(id,false);closing=true;window.Close();}catch(Exception error){MessageBox.Show(window,"便签状态未保存："+error.Message);}}};window.Closed+=delegate{floralPins.Remove(id);floralViews.Remove(view);view.Browser.Dispose();};window.Show();}
  async void CloseFloral(object sender,System.ComponentModel.CancelEventArgs e){if(floralClosing)return;e.Cancel=true;await Task.Yield();foreach(var v in floralViews.ToList())if(!await v.Flush()){MessageBox.Show(Window,"日记或便签尚未保存，窗口已保留。请检查存储位置后重试。","拾日");return;}floralClosing=true;Window.Close();}
  void FocusFloral(){if(floralView!=null)floralView.Browser.Focus();}
 }
}


