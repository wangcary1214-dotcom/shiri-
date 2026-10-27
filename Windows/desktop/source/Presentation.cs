using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Shiri {
 public static class PageMotion {
  static readonly DependencyProperty GenerationProperty=DependencyProperty.RegisterAttached("Generation",typeof(int),typeof(PageMotion),new PropertyMetadata(0));
  // Animate the new live content only. Never snapshot, blend pages or touch the pet.
  public static void Enter(FrameworkElement target, bool horizontal = false) {
   if(target==null)return;
   target.BeginAnimation(UIElement.OpacityProperty,null);target.Opacity=1;
   var move=target.RenderTransform as TranslateTransform;
   if(move==null){move=new TranslateTransform();target.RenderTransform=move;}
   move.BeginAnimation(TranslateTransform.XProperty,null);move.BeginAnimation(TranslateTransform.YProperty,null);move.X=0;move.Y=0;
   if(!target.IsLoaded||!SystemParameters.ClientAreaAnimation)return;
   int generation=(int)target.GetValue(GenerationProperty)+1;target.SetValue(GenerationProperty,generation);
   var duration=TimeSpan.FromMilliseconds(180);
   var ease=new CubicEase{EasingMode=EasingMode.EaseOut};
   var fade=new DoubleAnimation(.82,1,duration){EasingFunction=ease,FillBehavior=FillBehavior.Stop};
   fade.Completed+=delegate{if((int)target.GetValue(GenerationProperty)!=generation)return;target.BeginAnimation(UIElement.OpacityProperty,null);move.BeginAnimation(TranslateTransform.XProperty,null);move.BeginAnimation(TranslateTransform.YProperty,null);};
   target.BeginAnimation(UIElement.OpacityProperty,fade,HandoffBehavior.SnapshotAndReplace);
   move.BeginAnimation(horizontal ? TranslateTransform.XProperty : TranslateTransform.YProperty,new DoubleAnimation(horizontal ? 5 : 7,0,duration){EasingFunction=ease,FillBehavior=FillBehavior.Stop},HandoffBehavior.SnapshotAndReplace);
  }
 }
 public partial class Controller {
  sealed class DateTile {
   public DateTime Date;
   public Button Button;
   public TextBlock Number,Weekday;
   public System.Windows.Shapes.Ellipse Dot;
   public string StyleKey;
  }
  readonly List<DateTile> weekTiles=new List<DateTile>(),monthTiles=new List<DateTile>(),yearTiles=new List<DateTile>();
  HashSet<string> pendingCalendarDates=new HashSet<string>();
  DateTime renderedMonth=DateTime.MinValue;
  string renderedPage;
  int yearCalendarBuilds;
  DateTile CreateDateTile(bool week) {
   var tile=new DateTile();tile.Button=Btn("",delegate{ChooseDate(tile.Date);});tile.Button.Padding=new Thickness(0);tile.Button.Margin=new Thickness(1);
   var body=new StackPanel{VerticalAlignment=VerticalAlignment.Center};
   if(week){tile.Weekday=new TextBlock{HorizontalAlignment=HorizontalAlignment.Center,FontSize=10};body.Children.Add(tile.Weekday);tile.Button.Padding=new Thickness(0,9,0,8);tile.Button.Margin=new Thickness(3,0,3,0);}
   else tile.Button.Height=31;
   tile.Number=new TextBlock{HorizontalAlignment=HorizontalAlignment.Center,FontSize=week ? 23 : 11};
   if(week){tile.Number.FontFamily=new FontFamily("Segoe UI");tile.Number.FontWeight=FontWeights.SemiBold;tile.Number.Margin=new Thickness(0,5,0,5);}
   body.Children.Add(tile.Number);tile.Dot=new System.Windows.Shapes.Ellipse{Width=week ? 3 : 2,Height=week ? 3 : 2,Margin=new Thickness(0,week ? 0 : 2,0,0)};body.Children.Add(tile.Dot);tile.Button.Content=body;return tile;
  }
  void StyleDateTile(DateTile tile,bool week,bool year) {
   bool active=tile.Date==selected,today=tile.Date==DateTime.Today,pending=pendingCalendarDates.Contains(Day(tile.Date)),outside=!week&&!year&&tile.Date.Month!=month.Month;
   string key=tile.Date.Ticks+"/"+active+"/"+today+"/"+pending+"/"+outside;
   if(tile.StyleKey==key)return;tile.StyleKey=key;
   tile.Button.Background=active ? green : today&&!week ? B("#E8EFDF") : Brushes.Transparent;
   tile.Button.ToolTip=tile.Date.ToString("yyyy年M月d日")+(pending ? " · 有待办" : "");
   if(year){tile.Button.Foreground=active ? Brushes.White : pending ? green : B("#8C9981");tile.Button.FontWeight=pending ? FontWeights.SemiBold : FontWeights.Normal;return;}
   tile.Number.Text=tile.Date.Day.ToString(week ? "00" : "0");tile.Number.Foreground=active ? Brushes.White : outside ? B("#CCD2C3") : week ? B("#607057") : B("#7B886F");
   tile.Dot.Fill=active ? (pending||week ? B("#D2E0CB") : Brushes.Transparent) : pending ? B("#91A47E") : Brushes.Transparent;
   if(week){tile.Weekday.Text=new[]{"周一","周二","周三","周四","周五","周六","周日"}[((int)tile.Date.DayOfWeek+6)%7];tile.Weekday.Foreground=active ? B("#D2E0CB") : muted;}
  }
  void UpdateDateTiles() {
   // One task scan per refresh, then O(1) marker lookups for all date cells.
   pendingCalendarDates=new HashSet<string>(state.Tasks.Where(t=>!t.Done).Select(t=>t.Date));
   C<TextBlock>("CalendarSummary").Text=selected.ToString("M月d日 dddd",System.Globalization.CultureInfo.GetCultureInfo("zh-CN"));
   var strip=C<UniformGrid>("WeekStrip");
   if(weekTiles.Count==0)for(int i=0;i<7;i++){var tile=CreateDateTile(true);weekTiles.Add(tile);strip.Children.Add(tile.Button);}
   for(int i=0;i<7;i++){weekTiles[i].Date=weekStart.AddDays(i);StyleDateTile(weekTiles[i],true,false);}
   C<TextBlock>("MonthLabel").Text=month.ToString("yyyy 年 M 月");var grid=C<UniformGrid>("MonthGrid");
   if(monthTiles.Count==0)for(int i=0;i<42;i++){var tile=CreateDateTile(false);monthTiles.Add(tile);grid.Children.Add(tile.Button);}
   DateTime first=Monday(month);
   for(int i=0;i<42;i++){monthTiles[i].Date=first.AddDays(i);StyleDateTile(monthTiles[i],false,false);}
   if(renderedMonth!=DateTime.MinValue&&renderedMonth!=month)PageMotion.Enter(grid,true);
   renderedMonth=month;RenderYearCalendar();
  }
  void UpdateYearTiles() {
   if(!C<Expander>("CalendarExpander").IsExpanded)return;
   // Selection and task markers update existing cells; only changing the year builds a new tree.
   string key=calendarYear.ToString();
   if(yearCalendarKey!=key) {
    yearCalendarKey=key;yearCalendarBuilds++;yearTiles.Clear();C<TextBlock>("CalendarYearLabel").Text=calendarYear+" 年 · 全年日历";
    var host=C<UniformGrid>("YearMonths");host.Children.Clear();
    for(int m=1;m<=12;m++) {
     DateTime first=new DateTime(calendarYear,m,1);var card=new StackPanel{Margin=new Thickness(2,3,4,14)};
     var label=Btn(m+" 月",delegate{month=first;RenderDates();C<ScrollViewer>("CalendarScroll").ScrollToTop();});label.FontSize=11;label.Padding=new Thickness(0,5,0,7);label.HorizontalAlignment=HorizontalAlignment.Left;card.Children.Add(label);
     var weekdays=new UniformGrid{Columns=7};foreach(string day in new[]{"一","二","三","四","五","六","日"})weekdays.Children.Add(new TextBlock{Text=day,FontSize=8,Foreground=B("#A2AD96"),HorizontalAlignment=HorizontalAlignment.Center});card.Children.Add(weekdays);
     var days=new UniformGrid{Columns=7,Rows=6};card.Children.Add(days);int offset=((int)first.DayOfWeek+6)%7,count=DateTime.DaysInMonth(calendarYear,m);
     for(int n=0;n<42;n++) {
      int number=n-offset+1;if(number<1||number>count){days.Children.Add(new TextBlock());continue;}
      var tile=new DateTile{Date=first.AddDays(number-1)};tile.Button=Btn(number.ToString(),delegate{ChooseDate(tile.Date);});tile.Button.Padding=new Thickness(0);tile.Button.Height=18;tile.Button.FontSize=9;yearTiles.Add(tile);days.Children.Add(tile.Button);
     }
     host.Children.Add(card);
    }
   }
   foreach(var tile in yearTiles)StyleDateTile(tile,false,true);
  }
  void AnimatePageIfChanged() {
   string key=view+"/"+(view=="goal" ? selectedGoalId : view=="date"||view=="today"||view=="diary" ? Day(selected) : "");
   if(renderedPage!=null&&key!=renderedPage)PageMotion.Enter(C<Grid>("PageContent"));
   renderedPage=key;
  }
 }
}
