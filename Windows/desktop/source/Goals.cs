using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Shiri {
 [DataContract]public sealed class GoalItem {
  [DataMember]public string Id=Guid.NewGuid().ToString("N");
  [DataMember]public string Title="";
  [DataMember]public string Notes="";
  [DataMember]public string Color="#356A51";
  [DataMember]public long CreatedTicks=DateTime.UtcNow.Ticks;
  [DataMember]public bool Done;
 }
 public sealed class GoalChoice {
  public string Id,Title;
  public override string ToString(){return Title;}
 }
 public sealed class GoalDialog {
  public readonly Window Window;
  public readonly TextBox TitleInput,NotesInput,ColorInput;
  public GoalItem Result;
  public GoalDialog(Window owner,GoalItem original) {
   Window=new Window{Owner=owner,Title=original==null ? "新建长期任务 · 拾日" : "编辑长期任务 · 拾日",Width=500,Height=550,MaxHeight=Math.Max(450,SystemParameters.WorkArea.Height-32),WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize,Background=Controller.B("#F8F9F6"),FontFamily=owner.FontFamily,FontSize=13,Foreground=Controller.B("#3B5140"),Resources=owner.Resources};
   var root=new DockPanel{Margin=new Thickness(28)};Window.Content=root;var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};DockPanel.SetDock(actions,Dock.Bottom);root.Children.Add(actions);
   var cancel=Controller.Btn("取消",delegate{Window.DialogResult=false;});cancel.IsCancel=true;actions.Children.Add(cancel);var save=new Button{Content="保存长期任务"};save.IsDefault=true;save.Background=Controller.B("#356A51");save.Foreground=Brushes.White;actions.Children.Add(save);
   var body=new StackPanel();root.Children.Add(new ScrollViewer{Content=body});body.Children.Add(Controller.Text("让努力有一个方向",24,"#35523E"));var hint=Controller.Text("不设日期，慢慢积累；每日安排可以归到这里。",11,"#99A38F");hint.Margin=new Thickness(0,10,0,20);body.Children.Add(hint);
   body.Children.Add(Controller.Text("长期任务名称",11,"#7E8A72"));TitleInput=new TextBox{Text=original==null ? "" : original.Title,MaxLength=120,Height=40,Margin=new Thickness(0,8,0,18)};body.Children.Add(TitleInput);
   body.Children.Add(Controller.Text("想达成什么（可选）",11,"#7E8A72"));NotesInput=new TextBox{Text=original==null ? "" : original.Notes,MaxLength=4000,Height=100,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(0,8,0,18)};body.Children.Add(NotesInput);
   body.Children.Add(Controller.Text("目标颜色 · #RRGGBB",11,"#7E8A72"));ColorInput=new TextBox{Text=original==null ? "#356A51" : original.Color,MaxLength=7,Height=40,Margin=new Thickness(0,8,0,10)};body.Children.Add(ColorInput);var error=Controller.Text("",11,"#B36E53");error.TextWrapping=TextWrapping.Wrap;body.Children.Add(error);
   save.Click+=delegate {if(String.IsNullOrWhiteSpace(TitleInput.Text)){error.Text="请先给这个长期任务起个名字。";return;}if(!FocusStorage.ValidColor(ColorInput.Text.Trim())){error.Text="请填写有效颜色，例如 #356A51。";return;}Result=new GoalItem{Id=original==null ? Guid.NewGuid().ToString("N") : original.Id,Title=TitleInput.Text.Trim(),Notes=NotesInput.Text.Trim(),Color=ColorInput.Text.Trim().ToUpperInvariant(),CreatedTicks=original==null ? DateTime.UtcNow.Ticks : original.CreatedTicks,Done=original!=null&&original.Done};Window.DialogResult=true;};
   Window.Loaded+=delegate{TitleInput.Focus();};
  }
 }
 public partial class Controller {
  string selectedGoalId="";
  TextBlock goalEffortText;
  void EditGoal(GoalItem original) {
   var dialog=new GoalDialog(Window,original);if(dialog.Window.ShowDialog()!=true)return;
   Mutate(delegate {if(original==null)state.Goals.Add(dialog.Result);else state.Goals[state.Goals.IndexOf(original)]=dialog.Result;},original==null ? "已创建长期任务。" : "已更新长期任务。");
  }
  void OpenGoal(GoalItem goal){selectedGoalId=goal.Id;view="goal";C<TextBox>("SearchBox").Clear();Render();}
  void RenderGoalHeader() {
   bool detail=view=="goal",goals=view=="all";var goal=state.Goals.FirstOrDefault(x=>x.Id==selectedGoalId);
   C<Button>("AddButton").Content=view=="courses"?"＋    新建":goals ? "＋    新建长期任务" : "＋    新建任务";C<Button>("TodayButton").Content=detail ? "返回远方计划" : "回到今天";
   C<TextBlock>("ViewSubtitle").MaxWidth=detail ? 500 : Double.PositiveInfinity;
   C<TextBlock>("ViewTitle").MaxWidth=detail ? 340 : Double.PositiveInfinity;C<TextBlock>("ViewTitle").TextTrimming=TextTrimming.CharacterEllipsis;
   C<TextBlock>("SearchPlaceholder").Text=goals ? "搜索长期任务…" : "搜索任务…";
   if(goals){C<TextBlock>("ViewTag").Text="LONG TERM";C<TextBlock>("ViewSubtitle").Text="不设截止日，让每一点努力都有去处。";}
   if(detail&&goal!=null){C<TextBlock>("ViewTitle").Text=goal.Title;C<TextBlock>("ViewTag").Text="LONG TERM";C<TextBlock>("ViewSubtitle").Text=String.IsNullOrWhiteSpace(goal.Notes) ? "每一次完成、每一段专注，都在慢慢靠近。" : goal.Notes.Replace("\r", " ").Replace("\n"," ");C<TextBlock>("ViewSubtitle").MaxWidth=500;C<TextBlock>("ViewSubtitle").TextTrimming=TextTrimming.CharacterEllipsis;}
  }
  void RenderGoals() {
   goalEffortText=null;var list=C<StackPanel>("TaskList");list.Children.Clear();var completed=C<StackPanel>("CompletedList");completed.Children.Clear();C<StackPanel>("CompletedSection").Visibility=Visibility.Collapsed;C<StackPanel>("EmptyState").Visibility=Visibility.Collapsed;
   string query=C<TextBox>("SearchBox").Text.Trim();
   if(view=="all") {
    C<Button>("SortButton").Content=prioritySort ? "↕  名称排序" : "↕  最近创建";C<TextBlock>("ListHeading").Text="长期任务";
    var found=state.Goals.Where(x=>x.Title.IndexOf(query,StringComparison.CurrentCultureIgnoreCase)>=0||(x.Notes??"").IndexOf(query,StringComparison.CurrentCultureIgnoreCase)>=0);
    var ordered=prioritySort ? found.OrderBy(x=>x.Title) : found.OrderByDescending(x=>x.CreatedTicks);int count=0,done=0;
    foreach(var goal in ordered){if(goal.Done){completed.Children.Add(GoalCard(goal));done++;}else list.Children.Add(GoalCard(goal));count++;}
    C<TextBlock>("ListCount").Text=count.ToString();C<StackPanel>("CompletedSection").Visibility=done>0 ? Visibility.Visible : Visibility.Collapsed;C<TextBlock>("CompletedHeading").Text="已达成的长期任务 · "+done;
    if(count==0){var empty=Text(query.Length>0 ? "没有匹配的长期任务" : "先种下一个长期目标",19,"#718B65");empty.Margin=new Thickness(16,45,0,12);list.Children.Add(empty);var hint=Text("无需设置日期。以后安排每日任务时，可以选择归到这里。",12,"#98A58D");hint.TextWrapping=TextWrapping.Wrap;hint.Margin=new Thickness(16,0,16,20);list.Children.Add(hint);var add=Btn("＋  创建长期任务",delegate{EditGoal(null);});add.HorizontalAlignment=HorizontalAlignment.Left;add.Margin=new Thickness(16,0,0,0);add.Background=B("#E5EDDF");list.Children.Add(add);}return;
   }
   var selectedGoal=state.Goals.FirstOrDefault(x=>x.Id==selectedGoalId);if(selectedGoal==null){view="all";RenderGoals();return;}
   var related=state.Tasks.Where(x=>x.GoalId==selectedGoal.Id).ToList();int achieved=related.Count(x=>x.Done);
   var metrics=new UniformGrid{Columns=3,Margin=new Thickness(0,0,0,18)};
   goalEffortText=Text(FocusEngine.FormatDuration(focus.GoalSeconds(selectedGoal.Id,DateTime.UtcNow)),23,selectedGoal.Color);metrics.Children.Add(GoalMetric("累计专注",goalEffortText));metrics.Children.Add(GoalMetric("已完成日程",Text(achieved+" / "+related.Count,23,selectedGoal.Color)));
   int days=related.Where(x=>x.Done).Select(x=>x.CompletedTicks>0 ? BeijingClock.FromUtc(new DateTime(x.CompletedTicks,DateTimeKind.Utc)).ToString("yyyy-MM-dd") : x.Date).Distinct().Count();metrics.Children.Add(GoalMetric("有完成记录的日子",Text(days+" 天",23,selectedGoal.Color)));list.Children.Add(metrics);
   var actions=new WrapPanel{Margin=new Thickness(0,0,0,22)};var addDay=Btn("＋  安排一天",delegate{Edit(null);});addDay.Background=B("#E5EDDF");actions.Children.Add(addDay);actions.Children.Add(Btn("▷  为此开始专注",delegate{BeginGoalFocus(selectedGoal);}));actions.Children.Add(Btn("编辑目标",delegate{EditGoal(selectedGoal);}));actions.Children.Add(Btn(selectedGoal.Done ? "重新开启目标" : "标记目标达成",delegate{Mutate(delegate{selectedGoal.Done=!selectedGoal.Done;},"已更新长期任务状态。");}));list.Children.Add(actions);
   C<TextBlock>("ListHeading").Text="我的努力记录";C<TextBlock>("ListCount").Text=related.Count.ToString();
   var heading=Text("关联日程 · 已完成与进行中",12,"#8A9C7D");heading.Margin=new Thickness(0,0,0,12);list.Children.Add(heading);
   foreach(var task in related.Where(x=>x.Title.IndexOf(query,StringComparison.CurrentCultureIgnoreCase)>=0||(x.Notes??"").IndexOf(query,StringComparison.CurrentCultureIgnoreCase)>=0).OrderByDescending(x=>x.Date).ThenBy(x=>x.Time)){list.Children.Add(TaskCard(task));if(task.Done&&task.CompletedTicks>0){var stamp=Text("完成于 "+BeijingClock.FromUtc(new DateTime(task.CompletedTicks,DateTimeKind.Utc)).ToString("yyyy年M月d日 HH:mm"),10,"#9AA58F");stamp.Margin=new Thickness(16,-3,0,13);list.Children.Add(stamp);}}
   if(related.Count==0){var hint=Text("还没有关联日程。从“安排一天”开始，给这个目标留下一小步。",12,"#9AA58F");hint.TextWrapping=TextWrapping.Wrap;hint.Margin=new Thickness(0,14,0,0);list.Children.Add(hint);}
   var sessions=focus.GoalSegments(selectedGoal.Id);if(sessions.Count>0){var focusHeading=Text("专注足迹",12,"#8A9C7D");focusHeading.Margin=new Thickness(0,24,0,12);list.Children.Add(focusHeading);foreach(var entry in sessions.OrderByDescending(x=>x.StartTicks).Take(30)){var project=focus.Data.Projects.FirstOrDefault(x=>x.Id==entry.ProjectId);var line=Text(BeijingClock.FromUtc(new DateTime(entry.StartTicks,DateTimeKind.Utc)).ToString("yyyy年M月d日 HH:mm")+"  ·  "+(project==null ? "专注" : project.Name)+"  ·  "+FocusEngine.FormatDuration((entry.EndTicks-entry.StartTicks)/(double)TimeSpan.TicksPerSecond),11,"#7E9272");line.TextWrapping=TextWrapping.Wrap;line.Margin=new Thickness(4,0,0,10);list.Children.Add(line);}}
   var totals=Text("完成整轮后计入；提前结束的轮次不计入。",10,"#A2AD96");totals.Margin=new Thickness(0,20,0,12);list.Children.Add(totals);
  }
  Border GoalMetric(string label,TextBlock value){var panel=new StackPanel();value.Margin=new Thickness(0,0,0,9);panel.Children.Add(value);panel.Children.Add(Text(label,10,"#9AA68F"));return new Border{Child=panel,Background=Brushes.White,BorderBrush=B("#E5EADF"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10),Padding=new Thickness(14),Margin=new Thickness(0,0,8,0)};}
  Border GoalCard(GoalItem goal) {
   var body=new StackPanel();body.Children.Add(Text((goal.Done ? "✓  " : "◷  ")+goal.Title,16,goal.Color));if(!String.IsNullOrWhiteSpace(goal.Notes)){var notes=Text(goal.Notes.Replace("\n"," "),11,"#98A58D");notes.TextTrimming=TextTrimming.CharacterEllipsis;notes.Margin=new Thickness(0,10,0,0);body.Children.Add(notes);}var related=state.Tasks.Where(x=>x.GoalId==goal.Id).ToList();var progress=Text("不设截止日  ·  已完成 "+related.Count(x=>x.Done)+" / "+related.Count+" 项日程  ·  专注 "+FocusEngine.FormatDuration(focus.GoalSeconds(goal.Id,DateTime.UtcNow)),10,"#93A087");progress.TextWrapping=TextWrapping.Wrap;progress.Margin=new Thickness(0,12,0,0);body.Children.Add(progress);
   var button=Btn("",delegate{OpenGoal(goal);});button.Content=body;button.Padding=new Thickness(0);button.HorizontalContentAlignment=HorizontalAlignment.Stretch;var card=new Border{Background=Brushes.White,BorderBrush=B("#E5EADF"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(11),Padding=new Thickness(18),Margin=new Thickness(0,0,0,12),Child=button};var menu=new ContextMenu();var edit=new MenuItem{Header="编辑长期任务"};edit.Click+=delegate{EditGoal(goal);};menu.Items.Add(edit);var delete=new MenuItem{Header="删除目标，保留每日任务（可撤销）"};delete.Click+=delegate{Mutate(delegate{state.Goals.Remove(goal);foreach(var task in state.Tasks.Where(x=>x.GoalId==goal.Id))task.GoalId="";},"长期任务已删除，关联日程已保留。");};menu.Items.Add(delete);card.ContextMenu=menu;return card;
  }
  void RefreshGoalEffort(){if(view=="goal"&&goalEffortText!=null)goalEffortText.Text=FocusEngine.FormatDuration(focus.GoalSeconds(selectedGoalId,DateTime.UtcNow));}
  void BeginGoalFocus(GoalItem goal){focus.Configure(focus.Data.FocusMinutes,focus.Data.BreakMinutes,goal.Title,goal.Color,goal.Id);focus.Toggle();SaveFocus();OpenFocus();}
  void BeginTaskFocus(TaskItem task){var goal=state.Goals.FirstOrDefault(x=>x.Id==task.GoalId);focus.Configure(focus.Data.FocusMinutes,focus.Data.BreakMinutes,task.Title,goal==null ? focus.Project.Color : goal.Color,task.GoalId);focus.Toggle();SaveFocus();OpenFocus();}
 }
}
