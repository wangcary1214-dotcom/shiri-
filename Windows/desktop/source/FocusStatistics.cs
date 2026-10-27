using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Shiri {
 public sealed class FocusStatisticsWindow {
  public readonly Window Window;
  public readonly FocusPie TodayPie=new FocusPie();
  public readonly TextBlock TodayTotal,WeekTotal;
  public readonly StackPanel TodayLegend=new StackPanel();
  public readonly Grid WeekBars=new Grid();
  public readonly Button PreviousWeek,NextWeek,CurrentWeek;
  readonly TextBlock dayLabel,weekLabel;
  FocusEngine engine;string renderedKey;DateTime monday;int weekOffset;
  public double[] WeekValues{get;private set;}
  public FocusStatisticsWindow(Window owner){
   Window=new Window{Owner=owner,Title="专注统计 · 拾日",Width=820,Height=850,MinWidth=650,MinHeight=540,MaxHeight=SystemParameters.WorkArea.Height-24,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Controller.B("#F8F9F6"),Foreground=Controller.B("#3B5140"),FontFamily=owner.FontFamily,FontSize=13,Resources=owner.Resources};
   var body=new StackPanel{Margin=new Thickness(28)};Window.Content=new ScrollViewer{Content=body,Padding=new Thickness(0,0,5,0)};
   body.Children.Add(Controller.Text("专注统计",25,"#35523E"));var note=Controller.Text("只计入完整完成的专注 · 北京时间 04:00 开始新的一天",11,"#96A38B");note.Margin=new Thickness(0,10,0,20);note.TextWrapping=TextWrapping.Wrap;body.Children.Add(note);
   var todayCard=new Border{Background=Brushes.White,CornerRadius=new CornerRadius(14),BorderBrush=Controller.B("#E3E9DD"),BorderThickness=new Thickness(1),Padding=new Thickness(22)};body.Children.Add(todayCard);var today=new StackPanel();todayCard.Child=today;
   var titleRow=new Grid();today.Children.Add(titleRow);titleRow.Children.Add(Controller.Text("今日专注",15,"#526C47"));TodayTotal=Controller.Text("",22,"#356A51");TodayTotal.FontFamily=new FontFamily("Segoe UI");TodayTotal.HorizontalAlignment=HorizontalAlignment.Right;titleRow.Children.Add(TodayTotal);
   dayLabel=Controller.Text("",10,"#9DA88F");dayLabel.Margin=new Thickness(0,8,0,14);today.Children.Add(dayLabel);
   var pieRow=new Grid();pieRow.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(210)});pieRow.ColumnDefinitions.Add(new ColumnDefinition());today.Children.Add(pieRow);TodayPie.Width=180;TodayPie.Height=180;TodayPie.HorizontalAlignment=HorizontalAlignment.Left;pieRow.Children.Add(TodayPie);
   var legendScroll=new ScrollViewer{Content=TodayLegend,MaxHeight=180,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(6,0,0,0),VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(legendScroll,1);pieRow.Children.Add(legendScroll);
   var weekCard=new Border{Background=Brushes.White,CornerRadius=new CornerRadius(14),BorderBrush=Controller.B("#E3E9DD"),BorderThickness=new Thickness(1),Padding=new Thickness(22),Margin=new Thickness(0,16,0,0)};body.Children.Add(weekCard);var weekly=new StackPanel();weekCard.Child=weekly;
   var weekHeader=new Grid();weekly.Children.Add(weekHeader);weekHeader.Children.Add(Controller.Text("一周专注",15,"#526C47"));var nav=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};weekHeader.Children.Add(nav);
   PreviousWeek=Controller.Btn("‹",delegate{weekOffset--;RefreshWeek();});PreviousWeek.FontSize=18;PreviousWeek.Padding=new Thickness(8,2,8,2);PreviousWeek.ToolTip="上一周";nav.Children.Add(PreviousWeek);
   CurrentWeek=Controller.Btn("本周",delegate{weekOffset=0;RefreshWeek();});CurrentWeek.FontSize=10;CurrentWeek.Padding=new Thickness(8,4,8,4);nav.Children.Add(CurrentWeek);
   NextWeek=Controller.Btn("›",delegate{if(weekOffset<0)weekOffset++;RefreshWeek();});NextWeek.FontSize=18;NextWeek.Padding=new Thickness(8,2,8,2);NextWeek.ToolTip="下一周";nav.Children.Add(NextWeek);
   weekLabel=Controller.Text("",10,"#9DA88F");weekLabel.Margin=new Thickness(0,8,0,0);weekly.Children.Add(weekLabel);WeekTotal=Controller.Text("",11,"#829575");WeekTotal.Margin=new Thickness(0,9,0,18);weekly.Children.Add(WeekTotal);WeekBars.Height=194;weekly.Children.Add(WeekBars);
  }
  public void Refresh(FocusEngine current,DateTime utc,IList<FocusSlice> slices){
   bool different=!Object.ReferenceEquals(engine,current);engine=current;var tail=current.Data.Segments.LastOrDefault();string key=BeijingClock.DayStartUtc(utc).Ticks+"/"+current.Data.Segments.Count+"/"+(tail==null ? 0 : tail.EndTicks)+"/"+String.Join("|",current.Data.Projects.Select(x=>x.Id+x.Name+x.Color));
   if(!different&&renderedKey==key)return;renderedKey=key;TodayPie.Refresh(slices.ToList());double total=slices.Sum(x=>x.Seconds);TodayTotal.Text=FocusEngine.FormatDuration(total);var day=BeijingClock.FromUtc(BeijingClock.DayStartUtc(utc));dayLabel.Text=day.ToString("M月d日 04:00")+" — "+day.AddDays(1).ToString("M月d日 04:00");
   TodayLegend.Children.Clear();if(slices.Count==0){var empty=Controller.Text("今天还没有完成的专注。\n完成一整轮后，会显示在这里。",12,"#A0AC95");empty.LineHeight=24;empty.TextWrapping=TextWrapping.Wrap;TodayLegend.Children.Add(empty);}
   foreach(var slice in slices){var line=new Grid{Margin=new Thickness(0,8,0,8)};line.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(18)});line.ColumnDefinitions.Add(new ColumnDefinition());line.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});line.Children.Add(new System.Windows.Shapes.Ellipse{Width=7,Height=7,Fill=Controller.B(slice.Color),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center});var name=Controller.Text(slice.Name,12,"#71846A");name.TextTrimming=TextTrimming.CharacterEllipsis;name.ToolTip=slice.Name;Grid.SetColumn(name,1);line.Children.Add(name);var duration=Controller.Text(FocusEngine.FormatDuration(slice.Seconds)+" · "+(slice.Seconds/total).ToString("P0"),10,"#96A38B");duration.Margin=new Thickness(8,0,0,0);Grid.SetColumn(duration,2);line.Children.Add(duration);TodayLegend.Children.Add(line);}
   DateTime today=day.Date;monday=today.AddDays(-((int)today.DayOfWeek+6)%7);RefreshWeek();
  }
  void RefreshWeek(){
   if(engine==null)return;DateTime start=monday.AddDays(weekOffset*7);WeekValues=engine.WeekSeconds(DateTime.SpecifyKind(start.AddHours(-4),DateTimeKind.Utc));weekLabel.Text=start.ToString("yyyy年M月d日")+" — "+start.AddDays(6).ToString("M月d日")+" · 每日 04:00 分界";WeekTotal.Text=(weekOffset==0 ? "本周合计  " : "所选周合计  ")+FocusEngine.FormatDuration(WeekValues.Sum());NextWeek.IsEnabled=weekOffset<0;
   WeekBars.Children.Clear();WeekBars.ColumnDefinitions.Clear();double max=Math.Max(60,WeekValues.Max());var weekdays=new[]{"周一","周二","周三","周四","周五","周六","周日"};DateTime today=BeijingClock.FromUtc(BeijingClock.DayStartUtc(DateTime.UtcNow)).Date;
   for(int i=0;i<7;i++){WeekBars.ColumnDefinitions.Add(new ColumnDefinition());var cell=new Grid{Margin=new Thickness(4,0,4,0)};cell.RowDefinitions.Add(new RowDefinition{Height=new GridLength(23)});cell.RowDefinitions.Add(new RowDefinition{Height=new GridLength(138)});cell.RowDefinitions.Add(new RowDefinition());Grid.SetColumn(cell,i);WeekBars.Children.Add(cell);double seconds=WeekValues[i];var value=Controller.Text(seconds>0 ? FocusEngine.FormatDuration(seconds) : "—",9,"#829575");value.HorizontalAlignment=HorizontalAlignment.Center;cell.Children.Add(value);bool isToday=start.AddDays(i)==today;var bar=new Border{Width=30,Height=Math.Max(3,seconds/max*132),CornerRadius=new CornerRadius(5),Background=Controller.B(seconds<=0 ? "#EBF0E6" : isToday ? "#356A51" : "#A2BA95"),VerticalAlignment=VerticalAlignment.Bottom,HorizontalAlignment=HorizontalAlignment.Center,ToolTip=start.AddDays(i).ToString("M月d日")+" · "+FocusEngine.FormatDuration(seconds)};Grid.SetRow(bar,1);cell.Children.Add(bar);var label=new TextBlock{Text=weekdays[i]+"\n"+start.AddDays(i).ToString("M/d"),FontSize=9,Foreground=Controller.B(isToday ? "#356A51" : "#9BA78E"),TextAlignment=TextAlignment.Center,Margin=new Thickness(0,9,0,0)};Grid.SetRow(label,2);cell.Children.Add(label);}
  }
 }
 public partial class Controller {
  FocusStatisticsWindow focusStatistics;
  void OpenFocusStatistics(){if(focusStatistics!=null){focusStatistics.Window.Activate();return;}var value=new FocusStatisticsWindow(Window);focusStatistics=value;value.Window.Closed+=delegate{focusStatistics=null;};value.Refresh(focus,DateTime.UtcNow,focus.Statistics(DateTime.UtcNow));value.Window.Show();}
 }
}
