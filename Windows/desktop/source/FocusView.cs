using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Shiri {
 public sealed class FocusDial : FrameworkElement {
  public double Progress;
  public string Color="#356A51";
  static readonly Pen Track=MakePen("#E6EBDF");
  Pen activePen;
  static Pen MakePen(string color){var pen=new Pen(Controller.B(color),10){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};pen.Freeze();return pen;}
  public void Refresh(double progress,string color){progress=Math.Max(0,Math.Min(1,progress));if(activePen!=null&&Math.Abs(Progress-progress)<.0000000001&&Color==color)return;if(activePen==null||Color!=color)activePen=MakePen(color);Progress=progress;Color=color;InvalidateVisual();}
  protected override void OnRender(DrawingContext dc) {
   base.OnRender(dc);double r=Math.Max(0,Math.Min(ActualWidth,ActualHeight)/2-14);var center=new Point(ActualWidth/2,ActualHeight/2);
   dc.DrawEllipse(null,Track,center,r,r);
   var pen=activePen??(activePen=MakePen(Color));
   if(Progress>=.99999)dc.DrawEllipse(null,pen,center,r,r);
   else if(Progress>0){double angle=Progress*Math.PI*2-Math.PI/2;var path=new StreamGeometry();using(var g=path.Open()){g.BeginFigure(new Point(center.X,center.Y-r),false,false);g.ArcTo(new Point(center.X+Math.Cos(angle)*r,center.Y+Math.Sin(angle)*r),new Size(r,r),0,Progress>.5,SweepDirection.Clockwise,true,false);}dc.DrawGeometry(null,pen,path);}
  }
 }
 public sealed class FocusPie : FrameworkElement {
  public bool OverviewOnly;
  List<FocusSlice> slices=new List<FocusSlice>();
  public void Refresh(List<FocusSlice> value){if(slices.Count==value.Count&&slices.Zip(value,(a,b)=>a.Id==b.Id&&a.Name==b.Name&&a.Color==b.Color&&a.Seconds==b.Seconds).All(x=>x))return;slices=value;AutomationProperties.SetName(this,"今日专注统计，共 "+FocusEngine.FormatDuration(value.Sum(x=>x.Seconds)));InvalidateVisual();}
  protected override void OnRender(DrawingContext dc) {
   base.OnRender(dc);double r=Math.Max(0,Math.Min(ActualWidth,ActualHeight)/2-3);var center=new Point(ActualWidth/2,ActualHeight/2);double total=slices.Sum(x=>x.Seconds),angle=-Math.PI/2;
   if(total<=0){dc.DrawEllipse(Controller.B("#E9EEE3"),null,center,r,r);if(OverviewOnly)return;var label=new FormattedText("暂无记录",CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),12,Controller.B("#9BA68D"),VisualTreeHelper.GetDpi(this).PixelsPerDip);dc.DrawText(label,new Point(center.X-label.Width/2,center.Y-label.Height/2));return;}
   foreach(var slice in slices) {
    double span=slice.Seconds/total*Math.PI*2,next=angle+span;
    if(span>=Math.PI*2-.00001)dc.DrawEllipse(Controller.B(slice.Color),null,center,r,r);
    else {var path=new StreamGeometry();using(var g=path.Open()){g.BeginFigure(center,true,true);g.LineTo(new Point(center.X+Math.Cos(angle)*r,center.Y+Math.Sin(angle)*r),true,false);g.ArcTo(new Point(center.X+Math.Cos(next)*r,center.Y+Math.Sin(next)*r),new Size(r,r),0,span>Math.PI,SweepDirection.Clockwise,true,false);}dc.DrawGeometry(Controller.B(slice.Color),new Pen(Controller.B("#FCFDF9"),1.5),path);}angle=next;
   }
  }
  protected override void OnMouseMove(MouseEventArgs e) {
   base.OnMouseMove(e);if(OverviewOnly){ToolTip="查看今日与一周专注统计";return;}Point p=e.GetPosition(this);double x=p.X-ActualWidth/2,y=p.Y-ActualHeight/2,r=Math.Min(ActualWidth,ActualHeight)/2-3;
   if(x*x+y*y>r*r){ToolTip=null;return;}double total=slices.Sum(s=>s.Seconds);if(total<=0){ToolTip="开始一次专注后，这里会按任务颜色统计时长。";return;}
   double angle=Math.Atan2(y,x)+Math.PI/2;if(angle<0)angle+=Math.PI*2;double accumulated=0;
   foreach(var slice in slices){accumulated+=slice.Seconds/total*Math.PI*2;if(angle<=accumulated){ToolTip=slice.Name+" · "+FocusEngine.FormatDuration(slice.Seconds)+" · "+(slice.Seconds/total).ToString("P0");break;}}
  }
 }
 public sealed class FocusWorkspace : Grid {
  public readonly FocusDial Dial=new FocusDial();
  public readonly TextBlock Time=new TextBlock(),PhaseLabel=new TextBlock(),TaskName=new TextBlock(),ClockText=new TextBlock(),DateText=new TextBlock(),Summary=new TextBlock();
  public readonly Button PauseButton,FullScreenButton,BreakButton;
  public readonly StackPanel Agenda=new StackPanel();
  public readonly StackPanel CountdownList=new StackPanel(),CountdownPanel=new StackPanel();
  readonly ScrollViewer countdownScroll;
  public double CountdownReservedBottom=205;
  readonly TextBlock agendaHeading=new TextBlock();
  string agendaKey;
  public FocusWorkspace(Action toggle,Action reset,Action settings,Action rest,Action fullScreen,Action back,Action countdowns=null) {
   Background=Controller.B("#F8F9F6");RowDefinitions.Add(new RowDefinition{Height=new GridLength(80)});RowDefinitions.Add(new RowDefinition());
   ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(240)});ColumnDefinitions.Add(new ColumnDefinition());ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(240)});
   var top=new Grid{Margin=new Thickness(28,12,28,8)};SetColumnSpan(top,3);Children.Add(top);
   top.ColumnDefinitions.Add(new ColumnDefinition());top.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});top.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var backButton=Controller.Btn("‹  返回日程",delegate{back();});backButton.HorizontalAlignment=HorizontalAlignment.Left;backButton.VerticalAlignment=VerticalAlignment.Center;top.Children.Add(backButton);
   var clock=new StackPanel{Margin=new Thickness(0,0,24,0),VerticalAlignment=VerticalAlignment.Center};SetColumn(clock,1);top.Children.Add(clock);
   Typography.SetNumeralAlignment(Time,FontNumeralAlignment.Tabular);Typography.SetNumeralAlignment(ClockText,FontNumeralAlignment.Tabular);
   ClockText.FontSize=23;ClockText.FontFamily=new FontFamily("Segoe UI");ClockText.HorizontalAlignment=HorizontalAlignment.Right;ClockText.Foreground=Controller.B("#3D5C47");clock.Children.Add(ClockText);
   DateText.FontSize=10;DateText.Foreground=Controller.B("#8B9982");DateText.HorizontalAlignment=HorizontalAlignment.Right;clock.Children.Add(DateText);
   FullScreenButton=Controller.Btn("⛶  电脑全屏",delegate{fullScreen();});FullScreenButton.Background=Controller.B("#E8EEDF");FullScreenButton.VerticalAlignment=VerticalAlignment.Center;SetColumn(FullScreenButton,2);top.Children.Add(FullScreenButton);
   var agendaPanel=new StackPanel{Margin=new Thickness(32,24,8,0),VerticalAlignment=VerticalAlignment.Top,Background=Brushes.Transparent};SetRow(agendaPanel,1);Children.Add(agendaPanel);
   agendaHeading.FontSize=13;agendaHeading.FontWeight=FontWeights.SemiBold;agendaHeading.Foreground=Controller.B("#66805D");agendaPanel.Children.Add(agendaHeading);
   var agendaScroll=new ScrollViewer{Content=Agenda,MaxHeight=450,Margin=new Thickness(0,18,0,0),Background=Brushes.Transparent,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};agendaPanel.Children.Add(agendaScroll);
   var center=new Grid{Margin=new Thickness(8,10,8,22)};SetColumn(center,1);SetRow(center,1);Children.Add(center);
   center.RowDefinitions.Add(new RowDefinition());center.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});center.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   var dialGrid=new Grid{Width=440,Height=440};dialGrid.Children.Add(Dial);var inside=new StackPanel{VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center,MaxWidth=310};dialGrid.Children.Add(inside);
   PhaseLabel.FontSize=13;PhaseLabel.Foreground=Controller.B("#91A184");PhaseLabel.HorizontalAlignment=HorizontalAlignment.Center;inside.Children.Add(PhaseLabel);
   Time.FontSize=76;Time.FontFamily=new FontFamily("Segoe UI");Time.FontWeight=FontWeights.Light;Time.Foreground=Controller.B("#35533D");Time.HorizontalAlignment=HorizontalAlignment.Center;Time.Margin=new Thickness(0,10,0,13);inside.Children.Add(Time);
   TaskName.FontSize=17;TaskName.TextWrapping=TextWrapping.Wrap;TaskName.TextAlignment=TextAlignment.Center;TaskName.Foreground=Controller.B("#607B56");inside.Children.Add(TaskName);
   var viewbox=new Viewbox{Child=dialGrid,Stretch=Stretch.Uniform,MaxWidth=500,MaxHeight=500,Margin=new Thickness(0,0,0,20)};center.Children.Add(viewbox);
   var controls=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Center};SetRow(controls,1);center.Children.Add(controls);
   PauseButton=Controller.Btn("▷  开始专注",delegate{toggle();});PauseButton.Foreground=Brushes.White;PauseButton.Padding=new Thickness(24,13,24,13);PauseButton.MinWidth=140;controls.Children.Add(PauseButton);
   var resetButton=Controller.Btn("↻  重置",delegate{reset();});resetButton.Margin=new Thickness(8,0,0,0);controls.Children.Add(resetButton);
   BreakButton=Controller.Btn("休息",delegate{rest();});controls.Children.Add(BreakButton);controls.Children.Add(Controller.Btn("设置",delegate{settings();}));
   var hint=Controller.Text("Space 暂停 / 继续   ·   Esc 返回",11,"#9BA68F");hint.HorizontalAlignment=HorizontalAlignment.Center;hint.Margin=new Thickness(0,20,0,0);SetRow(hint,2);center.Children.Add(hint);
   var right=new StackPanel{Margin=new Thickness(26,32,28,18),VerticalAlignment=VerticalAlignment.Top};SetColumn(right,2);SetRow(right,1);Children.Add(right);
   Summary.FontSize=11;Summary.Foreground=Controller.B("#93A18A");Summary.TextWrapping=TextWrapping.Wrap;Summary.LineHeight=24;right.Children.Add(Summary);
   CountdownPanel.Margin=new Thickness(0,22,0,0);CountdownPanel.Visibility=Visibility.Collapsed;right.Children.Add(CountdownPanel);
   CountdownPanel.Children.Add(new Border{Height=1,Background=Controller.B("#E6EBDF"),Margin=new Thickness(0,0,0,17)});
   var countdownHeader=new Grid();countdownHeader.Children.Add(Controller.Text("倒计时",11,"#91A184"));if(countdowns!=null){var expand=Controller.Btn("↗",delegate{countdowns();});expand.HorizontalAlignment=HorizontalAlignment.Right;expand.FontSize=13;expand.Padding=new Thickness(3,0,3,0);expand.Foreground=Controller.B("#A0AE97");expand.ToolTip="展开倒计时列表";countdownHeader.Children.Add(expand);}CountdownPanel.Children.Add(countdownHeader);
   countdownScroll=new ScrollViewer{Content=CountdownList,MaxHeight=190,Margin=new Thickness(0,6,0,0),VerticalScrollBarVisibility=ScrollBarVisibility.Auto};CountdownPanel.Children.Add(countdownScroll);
   SizeChanged+=delegate{FitCountdown();};countdownScroll.Loaded+=delegate{FitCountdown();};
  }
  public void FitCountdown(){if(!countdownScroll.IsLoaded||CountdownPanel.Visibility!=Visibility.Visible||ActualHeight<=0)return;double top=countdownScroll.TranslatePoint(new Point(0,0),this).Y;double height=Math.Max(50,Math.Min(190,ActualHeight-CountdownReservedBottom-20-top));if(Math.Abs(countdownScroll.MaxHeight-height)>.5)countdownScroll.MaxHeight=height;}
  public void RefreshClock(FocusEngine engine,DateTime utc){
   string clock=BeijingClock.FromUtc(utc).ToString("HH:mm:ss")+"  北京时间";if(ClockText.Text!=clock)ClockText.Text=clock;
   string time=FocusEngine.FormatTime(engine.Remaining);if(Time.Text!=time)Time.Text=time;Dial.Refresh(1-engine.Progress,Dial.Color);
  }
  public void Refresh(FocusEngine engine,DateTime utc,IEnumerable<TaskItem> tasks,bool fullScreen,double? totalSeconds=null) {
   DateTime beijing=BeijingClock.FromUtc(utc);ClockText.Text=beijing.ToString("HH:mm:ss")+"  北京时间";DateText.Text=beijing.ToString("yyyy 年 M 月 d 日  dddd",CultureInfo.GetCultureInfo("zh-CN"));
   Time.Text=FocusEngine.FormatTime(engine.Remaining);PhaseLabel.Text=engine.Phase==FocusPhase.Focus ? (engine.Running ? "正 在 专 注" : "准 备 好 再 出 发") : (engine.Running ? "休 息 片 刻" : "休 息 已 暂 停");
   TaskName.Text=engine.Phase==FocusPhase.Focus ? engine.Project.Name : "放松肩膀，给自己一口呼吸";
   string color=engine.Phase==FocusPhase.Focus ? engine.Project.Color : "#829CAA";Dial.Refresh(1-engine.Progress,color);PauseButton.Background=Controller.B(color);
   PauseButton.Content=engine.Running ? "Ⅱ  暂停" : engine.Progress>0 ? "▷  继续" : engine.Phase==FocusPhase.Focus ? "▷  开始专注" : "▷  开始休息";
   BreakButton.Content=engine.Phase==FocusPhase.Break ? "结束休息" : "休息";FullScreenButton.Content=fullScreen ? "⛶  退出全屏" : "⛶  电脑全屏";
   Summary.Text="本轮节奏\n"+engine.Data.FocusMinutes+" 分钟专注\n"+engine.Data.BreakMinutes+" 分钟休息\n\n今日累计（已完成）\n"+FocusEngine.FormatDuration(totalSeconds??engine.Statistics(utc).Sum(x=>x.Seconds));
   string selectedDay=beijing.ToString("yyyy-MM-dd");var today=tasks.Where(x=>x.Date==selectedDay).OrderBy(x=>x.Done).ThenBy(x=>x.Time).ToList();
   string key=beijing.ToString("yyyy-MM-dd")+String.Join("|",today.Select(x=>x.Id+x.Title+x.Time+x.Done));if(key==agendaKey)return;agendaKey=key;
   agendaHeading.Text="今日日程 · "+beijing.ToString("M月d日");Agenda.Children.Clear();
   if(today.Count==0){var empty=Controller.Text("今天没有安排\n留一点空白给自己。",12,"#A3AC9A");empty.LineHeight=25;Agenda.Children.Add(empty);}
   foreach(var task in today){var line=new TextBlock{Text=(task.Done ? "✓  " : "·  ")+(String.IsNullOrEmpty(task.Time) ? "全天" : task.Time)+"   "+task.Title,FontSize=12,Foreground=Controller.B(task.Done ? "#A8B29F" : "#738469"),TextWrapping=TextWrapping.Wrap,LineHeight=23,Margin=new Thickness(0,0,0,13)};if(task.Done)line.TextDecorations=TextDecorations.Strikethrough;Agenda.Children.Add(line);}
  }
 }
 public sealed class FocusSettingsDialog {
  public readonly Window Window;
  public int FocusMinutes,BreakMinutes;
  public string TaskName,Color,GoalId;
  public readonly TextBox FocusInput,BreakInput,ColorInput;
  public readonly ComboBox NameInput;
  public readonly ComboBox GoalInput;
  readonly TextBlock error;
  public FocusSettingsDialog(Window owner,FocusEngine engine,IEnumerable<TaskItem> tasks,IEnumerable<GoalItem> goals=null) {
   Window=new Window{Owner=owner,Title="专注设置 · 拾日",Width=480,Height=610,MaxHeight=Math.Max(500,SystemParameters.WorkArea.Height-30),WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize,Background=Controller.B("#F8F9F6"),FontFamily=owner.FontFamily,FontSize=13,Foreground=Controller.B("#3B5140"),Resources=owner.Resources};
   var root=new DockPanel{Margin=new Thickness(28)};Window.Content=root;var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,14,0,0)};DockPanel.SetDock(actions,Dock.Bottom);root.Children.Add(actions);
   var cancel=Controller.Btn("取消",delegate{Window.DialogResult=false;});cancel.IsCancel=true;actions.Children.Add(cancel);
   var save=Controller.Btn("保存设置",delegate{Save();});save.IsDefault=true;save.Background=Controller.B("#356A51");save.Foreground=Brushes.White;actions.Children.Add(save);
   var panel=new StackPanel();root.Children.Add(new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});panel.Children.Add(Controller.Text("给专注一个节奏",24,"#35523E"));
   var info=Controller.Text("保存会结束本轮计时；未完成的这一轮不计入统计。",11,"#9AA68F");info.Margin=new Thickness(0,9,0,8);panel.Children.Add(info);
   Label(panel,"专注任务名称");NameInput=new ComboBox{IsEditable=true,IsTextSearchEnabled=false,MaxDropDownHeight=180,ItemsSource=engine.Data.Projects.Select(x=>x.Name).Concat(tasks.Select(x=>x.Title)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),Text=engine.Project.Name};panel.Children.Add(NameInput);
   Label(panel,"计入哪个长期任务（可选）");var choices=new List<GoalChoice>{new GoalChoice{Id="",Title="不归类到长期任务"}};if(goals!=null)choices.AddRange(goals.Select(x=>new GoalChoice{Id=x.Id,Title=x.Title}));GoalInput=new ComboBox{ItemsSource=choices,SelectedItem=choices.FirstOrDefault(x=>x.Id==engine.Project.GoalId)??choices[0]};panel.Children.Add(GoalInput);
   Label(panel,"专注时长（1–360 分钟）");FocusInput=new TextBox{Text=engine.Data.FocusMinutes.ToString(),Height=40,MaxLength=3};panel.Children.Add(FocusInput);
   Label(panel,"休息时长（1–120 分钟）");BreakInput=new TextBox{Text=engine.Data.BreakMinutes.ToString(),Height=40,MaxLength=3};panel.Children.Add(BreakInput);
   Label(panel,"任务颜色 · 同名任务沿用同一颜色");var palette=new WrapPanel();panel.Children.Add(palette);ColorInput=new TextBox{Text=engine.Project.Color,Height=38,MaxLength=7,Margin=new Thickness(0,10,0,0)};
   foreach(string value in new[]{"#356A51","#688FBA","#C99559","#A17BB5","#CB7C80","#5EAA9A"}){string color=value;var button=Controller.Btn("",delegate{ColorInput.Text=color;});button.Background=Controller.B(color);button.Width=34;button.Height=27;button.Margin=new Thickness(0,0,9,0);button.ToolTip=color;palette.Children.Add(button);}panel.Children.Add(ColorInput);
   NameInput.SelectionChanged+=delegate{string name=NameInput.SelectedItem as string;var project=engine.Data.Projects.FirstOrDefault(x=>x.Name==name);if(project!=null)ColorInput.Text=project.Color;var task=tasks.FirstOrDefault(x=>x.Title==name);string belongs=task!=null ? task.GoalId : project==null ? "" : project.GoalId;GoalInput.SelectedItem=choices.FirstOrDefault(x=>x.Id==belongs)??choices[0];};
   var note=Controller.Text("也可输入 #RRGGBB 自定颜色。休息不计入专注统计。",10,"#9AA68F");note.TextWrapping=TextWrapping.Wrap;note.Margin=new Thickness(0,8,0,0);panel.Children.Add(note);error=Controller.Text("",11,"#B36E53");error.TextWrapping=TextWrapping.Wrap;error.Margin=new Thickness(0,10,0,0);panel.Children.Add(error);
  }
  static void Label(StackPanel panel,string text){var label=Controller.Text(text,11,"#7E8A72");label.Margin=new Thickness(0,16,0,8);panel.Children.Add(label);}
  void Save() {
   int focus,rest;if(!Int32.TryParse(FocusInput.Text,out focus)||focus<1||focus>360||!Int32.TryParse(BreakInput.Text,out rest)||rest<1||rest>120){error.Text="请输入有效分钟数：专注 1–360，休息 1–120。";return;}
   if(String.IsNullOrWhiteSpace(NameInput.Text)||NameInput.Text.Trim().Length>120){error.Text="请填写 1–120 字的专注任务名称。";return;}
   if(!FocusStorage.ValidColor(ColorInput.Text.Trim())){error.Text="颜色请使用 #RRGGBB，例如 #356A51。";return;}
   FocusMinutes=focus;BreakMinutes=rest;TaskName=NameInput.Text.Trim();Color=ColorInput.Text.Trim().ToUpperInvariant();GoalId=((GoalChoice)GoalInput.SelectedItem).Id;Window.DialogResult=true;
  }
 }
}
