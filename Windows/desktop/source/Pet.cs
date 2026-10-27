using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Shiri {
 public enum PetMood {Relaxed,Encouraging,Celebrating}
 public enum PetExpression {Curious,Happy,Sulky}
 public sealed class PetExpressionEventArgs : EventArgs {
  public readonly PetExpression Expression;
  public readonly bool Animate;
  public PetExpressionEventArgs(PetExpression value,bool animate=false){Expression=value;Animate=animate;}
 }
 [DataContract] public sealed class DialogueLine {
  [DataMember(IsRequired=true)]public string Id {get;private set;}
  [DataMember(IsRequired=true)]public string Group {get;private set;}
  [DataMember(IsRequired=true)]public string Text {get;private set;}
  [DataMember(Name="Expression",IsRequired=true)]public string ExpressionName {get;private set;}
  public PetExpression Expression {get{return (PetExpression)Enum.Parse(typeof(PetExpression),ExpressionName);}}
 }
 public static class PetCatalog {
  static readonly List<DialogueLine> lines=Load();
  public static ReadOnlyCollection<DialogueLine> All {get{return lines.AsReadOnly();}}
  static List<DialogueLine> Load() {
   List<DialogueLine> result;
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("pet-dialogue.json")) {
    if(stream==null)throw new InvalidOperationException("大肥鱼台词库缺失，请重新解压程序。");
    result=(List<DialogueLine>)new DataContractJsonSerializer(typeof(List<DialogueLine>)).ReadObject(stream);
   }
   var ids=new HashSet<string>();var texts=new HashSet<string>();
   foreach(var line in result) {
    if(String.IsNullOrWhiteSpace(line.Id)||!ids.Add(line.Id)||String.IsNullOrWhiteSpace(line.Text)||!texts.Add(line.Text)||
     !new[]{"relaxed","completed","pending","casual","petting","goals","goal","focus","rest","paused","calendar"}.Contains(line.Group)||
     !new[]{"Curious","Happy","Sulky"}.Contains(line.ExpressionName))throw new InvalidOperationException("大肥鱼台词或表情对应关系无效。");
    string plain=line.Text.Replace("{remaining}","").Replace("{next}","").Replace("{context}","").Replace("{goal}","").Replace("{effort}","").Replace("{time}","");
    if(plain.Contains("{")||plain.Contains("}"))throw new InvalidOperationException("大肥鱼台词占位符无效。");
   }
   foreach(string group in new[]{"relaxed","completed","pending","casual","petting"})if(!result.Any(x=>x.Group==group))throw new InvalidOperationException("大肥鱼台词分组缺失。");
   return result;
  }
  public static DialogueLine Pick(string group,int variation) {
   var choices=lines.Where(x=>x.Group==group).ToArray();int index=(int)((uint)variation%(uint)choices.Length);return choices[index];
  }
 }
 public sealed class PetMessage {
  public readonly string Id,Text;
  public readonly PetMood Mood;
  public readonly PetExpression Expression;
  PetMessage(string id,string text,PetExpression expression,PetMood mood){Id=id;Text=text;Expression=expression;Mood=mood;}
  static string Group(int total,int done){return total<=0 ? "relaxed" : done>=total ? "completed" : "pending";}
  static PetMood MoodFor(int total,int done){return total<=0 ? PetMood.Relaxed : done>=total ? PetMood.Celebrating : PetMood.Encouraging;}
  public static string Context(int total,int done) {
   return total<=0 ? "今天没有任务，轻松惬意地过吧。" : done>=total ? "今天的任务已全部完成，辛苦啦！" : "今天还有 "+Math.Max(0,total-done)+" 件未完成，我们慢慢来。";
  }
  static PetMessage Format(DialogueLine line,int total,int done,string nextTitle) {
   string title=String.IsNullOrWhiteSpace(nextTitle) ? "眼前的一小步" : nextTitle.Length>26 ? nextTitle.Substring(0,26)+"…" : nextTitle;
   // Replace user text last: braces inside a task title remain literal text.
   string text=line.Text.Replace("{remaining}",Math.Max(0,total-done).ToString()).Replace("{context}",Context(total,done)).Replace("{next}",title);
   return new PetMessage(line.Id,text,line.Expression,MoodFor(total,done));
  }
  public static PetMessage ForTasks(int total,int done,string nextTitle,int variation) {
   return Format(PetCatalog.Pick(Group(total,done),variation),total,done,nextTitle);
  }
  public static PetMessage ForInteraction(int total,int done,string nextTitle,int variation,int streak) {
   int v=(int)((uint)variation%2147483647u);
   // Congratulations always use the happy artwork, including repeated clicks.
   if(total>0&&done>=total)return ForTasks(total,done,nextTitle,v);
   if(streak>=4)return Format(PetCatalog.Pick("petting",v),total,done,nextTitle);
   if(v>0&&v%5==0)return Format(PetCatalog.Pick("casual",v/5-1),total,done,nextTitle);
   return ForTasks(total,done,nextTitle,v-v/5);
  }
  public static PetMessage ForPreview(PetExpression expression) {
   switch(expression) {
    case PetExpression.Happy:return new PetMessage("preview-happy","嘿嘿！>< 被你发现了，大肥鱼现在超级开心！",expression,PetMood.Relaxed);
    case PetExpression.Sulky:return new PetMessage("preview-sulky","唔，眉毛自己皱起来了……本鱼只是有一点点小委屈，摸摸就好。",expression,PetMood.Relaxed);
    default:return new PetMessage("preview-curious","嗯……？让本鱼想一下，你是不是想到什么新点子啦？",PetExpression.Curious,PetMood.Relaxed);
   }
  }
  public static PetMessage ForPage(string page,int total,int done,string next,string goal,string effort,string time,int variation,int streak) {
   if(page=="today")return ForInteraction(total,done,next,variation,streak);
   if(page=="diary") {
    string[] replies={"唔，在写日记呀？本鱼安安静静坐在旁边。","今天有什么想记住的小事？一两句话也可以呀。","嘿嘿，给今天留下一点痕迹了！本鱼给你比个小心心 ><","不知道从哪里写？先记下今天吃了什么也不错～","有些心情慢慢写就好，本鱼不催你。","本鱼今天的日记：陪你，陪你，还有陪你！><","写累了就伸个懒腰吧，本鱼替你守着这页。","嗯……这一页属于你，想怎么写就怎么写。"};
    int index=(int)((uint)variation%(uint)replies.Length);return new PetMessage("diary-"+index,replies[index],index==2||index==5 ? PetExpression.Happy : PetExpression.Curious,PetMood.Relaxed);
   }
   var line=PetCatalog.Pick(page=="companion" ? "casual" : page,variation);
   string name=String.IsNullOrWhiteSpace(goal) ? "眼前的小目标" : goal.Length>22 ? goal.Substring(0,22)+"…" : goal;
   string text=line.Text.Replace("{context}",Context(total,done)).Replace("{effort}",effort).Replace("{time}",time).Replace("{goal}",name);
   return new PetMessage(line.Id,text,line.Expression,page=="rest"||page=="paused" ? PetMood.Relaxed : MoodFor(total,done));
  }
  public static PetMessage ForNotice(string title,string text) {
   bool completed=title=="专注完成";
   return new PetMessage(completed ? "notice-focus" : "notice-task",completed ? "专注完成啦！本鱼认真给你鼓掌 >< "+text : "嗯？时间到啦～"+title+"："+text,
    completed ? PetExpression.Happy : PetExpression.Curious,completed ? PetMood.Celebrating : PetMood.Encouraging);
  }
 }
 // Speech is the only source of expression changes. Idle has no timeline.
 public sealed class PetMotion {
  public const double TransitionSeconds=.075;
  public static readonly int[] TransitionA={0,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,63,64,65,66,67,68,69,70,1};
  public static readonly int[] TransitionB={1,27,28,29,30,31,32,33,34,35,36,37,38,39,40,41,42,43,44,45,46,47,48,49,50,51,52,53,54,55,56,57,58,59,60,61,62,2};
  static readonly int[] Chain=TransitionA.Concat(TransitionB.Skip(1)).ToArray();
  static readonly int[] PosePositions={0,TransitionA.Length-1,Chain.Length-1};
  public PetMood Mood;
  public PetExpression Expression {get;private set;}
  public int FrameIndex {get;private set;}
  public int StablePose {get;private set;}
  public int RequestedPose {get;private set;}
  public bool Transitioning {get;private set;}
  public long TransitionId {get;private set;}
  public long Ticks {get;private set;}
  int position,start,end;
  double elapsed;
  bool paused;
  public bool Paused {get{return paused;}set{paused=value;if(value&&Transitioning)Finish();}}
  public static int PoseFor(PetExpression expression){return expression==PetExpression.Happy ? 2 : expression==PetExpression.Sulky ? 1 : 0;}
  public void Express(PetExpression expression,bool animate=true) {
   if(!Enum.IsDefined(typeof(PetExpression),expression))throw new ArgumentOutOfRangeException("expression");
   int target=PoseFor(expression);Expression=expression;
   if(!animate){RequestedPose=target;end=PosePositions[target];if(Transitioning||position!=end)TransitionId++;Finish();return;}
   if(RequestedPose==target&&(Transitioning||position==PosePositions[target]))return;
   RequestedPose=target;start=position;end=PosePositions[target];elapsed=0;TransitionId++;
   if(paused||start==end){Finish();return;}
   Transitioning=true;
  }
  public void Advance(double seconds) {
   if(paused||!Transitioning||Double.IsNaN(seconds)||Double.IsInfinity(seconds)||seconds<=0)return;
   elapsed+=seconds;Ticks++;
   if(elapsed+1e-9>=TransitionSeconds){Finish();return;}
   double t=elapsed/TransitionSeconds;t=t*t*(3-2*t);
   position=(int)Math.Round(start+(end-start)*t);FrameIndex=Chain[position];
  }
  void Finish(){position=PosePositions[RequestedPose];FrameIndex=RequestedPose;StablePose=RequestedPose;Transitioning=false;elapsed=0;}
 }
 public sealed class PetCels {
  public const int Count=71;
  readonly BitmapSource[] images=new BitmapSource[Count];
  static PetCels shared;
  public static PetCels Get(){if(shared==null)shared=new PetCels();return shared;}
  public BitmapSource this[int index] {get{return images[index];}}
  PetCels() {
   // Preload and freeze every cel. Rendering never touches disk, decodes a PNG,
   // creates a bitmap, or mixes neighbouring expressions.
   for(int i=0;i<Count;i++) {
    using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("pet.cel."+i.ToString("000")+".png")) {
     if(stream==null)throw new InvalidOperationException("大肥鱼逐帧素材缺失，请重新解压程序。");
     var frame=new PngBitmapDecoder(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];
     if(frame.PixelWidth!=512||frame.PixelHeight!=512)throw new InvalidOperationException("大肥鱼画帧尺寸不一致。");
     frame.Freeze();images[i]=frame;
    }
   }
   // Approved fixed poses are byte-identical artwork at these indices.
   images[0]=images[3];images[1]=images[27];images[2]=images[62];
  }
 }
 public sealed class PetView : FrameworkElement,IDisposable {
  public readonly PetMotion Motion=new PetMotion();
  public event EventHandler Interacted;
  public event EventHandler<PetExpressionEventArgs> ExpressionPreviewed;
  public int InteractionStreak {get;private set;}
  public bool LastInteractionWasPointer {get;private set;}
  readonly PetCels cels;
  readonly Window owner;
  readonly Stopwatch stopwatch=new Stopwatch();
  readonly DispatcherTimer deadline=new DispatcherTimer(DispatcherPriority.Send);
  DateTime lastInteraction=DateTime.MinValue;
  double lastFrame;
  bool listening,disposed;
  public PetView(Window window) {
   owner=window;cels=PetCels.Get();Focusable=true;Cursor=Cursors.Hand;ClipToBounds=true;
   deadline.Interval=TimeSpan.FromSeconds(PetMotion.TransitionSeconds);
   deadline.Tick+=delegate {deadline.Stop();Motion.Advance(PetMotion.TransitionSeconds);InvalidateVisual();};
   ToolTip="摸摸大肥鱼，听今日任务提醒 · 右键选择表情 · Enter / 空格互动";
   AutomationProperties.SetName(this,"大肥鱼，日程宠物，点击听今日任务提醒");
   RenderOptions.SetBitmapScalingMode(this,BitmapScalingMode.HighQuality);
   Loaded+=delegate {Subscribe();};Unloaded+=delegate {Unsubscribe();};
   var menu=new ContextMenu();
   string[] labels={"疑惑 · 嗯？","小委屈 · 唔……","开心 ><"};
   PetExpression[] expressions={PetExpression.Curious,PetExpression.Sulky,PetExpression.Happy};
   for(int i=0;i<expressions.Length;i++) {
    var expression=expressions[i];var item=new MenuItem{Header=labels[i]};bool fromMouse=false;
    item.PreviewMouseLeftButtonDown+=delegate{fromMouse=true;};item.PreviewKeyDown+=delegate{fromMouse=false;};
    item.Click+=delegate {if(ExpressionPreviewed!=null)ExpressionPreviewed(this,new PetExpressionEventArgs(expression,fromMouse));fromMouse=false;};menu.Items.Add(item);
   }
   ContextMenuService.SetContextMenu(this,menu);
  }
  void Subscribe(){if(listening||disposed)return;listening=true;stopwatch.Restart();lastFrame=0;CompositionTarget.Rendering+=Frame;}
  void Unsubscribe(){deadline.Stop();if(!listening)return;CompositionTarget.Rendering-=Frame;listening=false;stopwatch.Stop();}
  void Frame(object sender,EventArgs args) {
   double now=stopwatch.Elapsed.TotalSeconds,dt=now-lastFrame;lastFrame=now;
   if(!Motion.Transitioning||Paused||!IsVisible||owner.WindowState==WindowState.Minimized||ContextMenuService.GetContextMenu(this).IsOpen)return;
   int before=Motion.FrameIndex;Motion.Advance(dt);if(before!=Motion.FrameIndex)InvalidateVisual();
  }
  public bool Paused {get{return Motion.Paused;}set{Motion.Paused=value;lastFrame=stopwatch.Elapsed.TotalSeconds;if(value)deadline.Stop();InvalidateVisual();}}
  public void React(PetExpression expression,bool animate=true){
   long id=Motion.TransitionId;Motion.Express(expression,animate);
   if(id!=Motion.TransitionId){lastFrame=stopwatch.Elapsed.TotalSeconds;deadline.Stop();if(Motion.Transitioning)deadline.Start();}
   InvalidateVisual();
   if(!animate&&IsLoaded)Dispatcher.BeginInvoke(DispatcherPriority.Render,new Action(delegate{if(!disposed)InvalidateVisual();}));
  }
  public void Interact(bool fromPointer=false) {
   LastInteractionWasPointer=fromPointer;
   InteractionStreak=(DateTime.UtcNow-lastInteraction).TotalSeconds<.9 ? InteractionStreak+1 : 1;lastInteraction=DateTime.UtcNow;
   if(Interacted!=null)Interacted(this,EventArgs.Empty);
  }
  protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e){base.OnMouseLeftButtonDown(e);Focus();Interact(true);e.Handled=true;}
  protected override void OnKeyDown(KeyEventArgs e){if(e.Key==Key.Space||e.Key==Key.Enter){Interact();e.Handled=true;}base.OnKeyDown(e);}
  Rect ImageBounds(){double side=Math.Min(ActualWidth,ActualHeight);return new Rect((ActualWidth-side)/2,(ActualHeight-side)/2,side,side);}
  protected override HitTestResult HitTestCore(PointHitTestParameters p){return ImageBounds().Contains(p.HitPoint) ? new PointHitTestResult(this,p.HitPoint) : null;}
  protected override void OnRender(DrawingContext dc) {
   base.OnRender(dc);var bounds=ImageBounds();if(bounds.Width<=0)return;
   dc.DrawImage(cels[Motion.FrameIndex],bounds);
   if(IsKeyboardFocused) {
    var pen=new Pen(Controller.B("#90A8C6"),1){DashStyle=DashStyles.Dot};
    dc.DrawRoundedRectangle(null,pen,new Rect(bounds.X+1,bounds.Y+1,bounds.Width-2,bounds.Height-2),10,10);
   }
  }
  public void Dispose(){disposed=true;Unsubscribe();}
 }
}
