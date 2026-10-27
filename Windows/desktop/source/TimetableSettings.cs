using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Controls;
namespace Shiri {
 [DataContract] public class ClassPeriod {
 [DataMember] public string Name="",Start="08:00",End="08:45";
 }
 [DataContract] public class TimetablePreferences {
 [DataMember] public string WeekAnchorDate=BeijingClock.Now.Date.ToString("yyyy-MM-dd");
 [DataMember] public int TotalWeeks=20;
 [DataMember] public int WeekAnchorNumber=1;
 [DataMember] public List<ClassPeriod> Periods=new List<ClassPeriod>();
 public static TimetablePreferences Default(){var s=new TimetablePreferences();string[] starts={"08:00","08:55","10:00","10:55","14:00","14:55","16:00","16:55","19:00","19:55"};for(int i=0;i<starts.Length;i++)s.Periods.Add(new ClassPeriod{Name="第"+(i+1)+"节",Start=starts[i],End=TimeSpan.Parse(starts[i]).Add(TimeSpan.FromMinutes(45)).ToString(@"hh\:mm")});return s;}
 public int CurrentWeek(DateTime today){DateTime anchor=DateTime.ParseExact(WeekAnchorDate,"yyyy-MM-dd",CultureInfo.InvariantCulture);anchor=anchor.AddDays(-((int)anchor.DayOfWeek+6)%7);today=today.Date.AddDays(-((int)today.DayOfWeek+6)%7);return Math.Max(1,Math.Min(TotalWeeks,WeekAnchorNumber+(today-anchor).Days/7));}
 public static void Validate(TimetablePreferences s){if(s!=null&&s.TotalWeeks==0)s.TotalWeeks=60;DateTime anchor;if(s==null||!DateTime.TryParseExact(s.WeekAnchorDate,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out anchor)||s.WeekAnchorNumber<1||s.WeekAnchorNumber>s.TotalWeeks||s.TotalWeeks<1||s.TotalWeeks>60||s.Periods==null||s.Periods.Count<1||s.Periods.Count>24)throw new Exception("请检查教学周及节次（1–24节）。");TimeSpan previous=TimeSpan.Zero;foreach(var p in s.Periods){TimeSpan start,end;if(p==null||String.IsNullOrWhiteSpace(p.Name)||p.Name.Length>30||!TimeSpan.TryParseExact(p.Start,@"hh\:mm",CultureInfo.InvariantCulture,out start)||!TimeSpan.TryParseExact(p.End,@"hh\:mm",CultureInfo.InvariantCulture,out end)||end<=start||end.TotalHours>=24||start<previous)throw new Exception("节次时间须按顺序排列，结束晚于开始且不可重叠。");previous=end;}}
 public static void Apply(Store store){Validate(store.Timetable);foreach(var c in store.Courses){if(c.FirstPeriod==0&&c.LastPeriod==0)continue;if(c.FirstPeriod<1||c.LastPeriod<c.FirstPeriod||c.LastPeriod>store.Timetable.Periods.Count)throw new Exception("课程关联的节次不存在，请先调整该课程。");c.Start=store.Timetable.Periods[c.FirstPeriod-1].Start;c.End=store.Timetable.Periods[c.LastPeriod-1].End;}}
 }
 public partial class Controller {
 void EditSemester(){var w=new Window{Owner=Window,Title="学期设置",Width=400,Height=300,WindowStartupLocation=WindowStartupLocation.CenterOwner};var p=new StackPanel{Margin=new Thickness(24)};w.Content=p;p.Children.Add(Text("学期第一周的周一",14,"#356A51"));var start=new TextBox{Text=DateTime.Parse(state.Timetable.WeekAnchorDate).AddDays(-((int)DateTime.Parse(state.Timetable.WeekAnchorDate).DayOfWeek+6)%7).AddDays(-(state.Timetable.WeekAnchorNumber-1)*7).ToString("yyyy-MM-dd")};p.Children.Add(start);p.Children.Add(Text("总教学周数",14,"#356A51"));var total=new TextBox{Text=state.Timetable.TotalWeeks.ToString()};p.Children.Add(total);p.Children.Add(Btn("保存",delegate{try{DateTime date=DateTime.ParseExact(start.Text,"yyyy-MM-dd",CultureInfo.InvariantCulture);if(date.DayOfWeek!=DayOfWeek.Monday)throw new Exception("请选择学期第一周的周一。");int count=int.Parse(total.Text);if(count<1||count>60||state.Courses.Any(x=>x.LastWeek>count))throw new Exception("总周数须为1–60，且覆盖已有课程周次。");Mutate(delegate{state.Timetable.WeekAnchorDate=start.Text;state.Timetable.WeekAnchorNumber=1;state.Timetable.TotalWeeks=count;courseWeek=0;},"已保存学期");w.Close();}catch(Exception ex){MessageBox.Show(w,ex.Message);}}));w.ShowDialog();} void EditTimetableSettings(){
 var w=new Window{Title="教学周与作息设置",Owner=Window,Width=480,Height=650,WindowStartupLocation=WindowStartupLocation.CenterOwner};var p=new StackPanel{Margin=new Thickness(24)};w.Content=new ScrollViewer{Content=p,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
 p.Children.Add(Text("当前教学周（本周一开始）",14,"#356A51"));var current=new TextBox{Text=state.Timetable.CurrentWeek(BeijingClock.Now.Date).ToString()};p.Children.Add(current);p.Children.Add(Text("节次名称           开始时间          课长（分钟）",12,"#718B65"));
 var rows=new StackPanel();p.Children.Add(rows);var fields=new List<TextBox[]>();
 Action<string,string,int> add=delegate(string name,string start,int duration){var row=new StackPanel{Orientation=Orientation.Horizontal};var a=new TextBox{Text=name,Width=110,Margin=new Thickness(0,5,8,5)};var b=new TextBox{Text=start,Width=100,Margin=new Thickness(0,5,8,5)};var c=new TextBox{Text=duration.ToString(),Width=90,Margin=new Thickness(0,5,0,5)};row.Children.Add(a);row.Children.Add(b);row.Children.Add(c);rows.Children.Add(row);fields.Add(new[]{a,b,c});};
 foreach(var item in state.Timetable.Periods)add(item.Name,item.Start,(int)(TimeSpan.Parse(item.End)-TimeSpan.Parse(item.Start)).TotalMinutes);
 p.Children.Add(Btn("＋ 增加一节",delegate{try{if(fields.Count>=24)throw new Exception("最多24节。");var previous=fields.Last();var time=TimeSpan.Parse(previous[1].Text).Add(TimeSpan.FromMinutes(int.Parse(previous[2].Text)+10));add("第"+(fields.Count+1)+"节",time.ToString(@"hh\:mm"),45);}catch(Exception ex){MessageBox.Show(w,ex.Message);}}));
 p.Children.Add(Btn("减少最后一节",delegate{if(fields.Count<=1)return;if(state.Courses.Any(x=>x.LastPeriod>=fields.Count)){MessageBox.Show(w,"最后一节已被课程使用，请先修改课程。");return;}fields.RemoveAt(fields.Count-1);rows.Children.RemoveAt(rows.Children.Count-1);}));
 p.Children.Add(Btn("保存设置",delegate{try{var next=new TimetablePreferences{WeekAnchorDate=BeijingClock.Now.Date.ToString("yyyy-MM-dd"),WeekAnchorNumber=int.Parse(current.Text),TotalWeeks=state.Timetable.TotalWeeks,Periods=new List<ClassPeriod>()};foreach(var row in fields){TimeSpan start;if(!TimeSpan.TryParseExact(row[1].Text,@"hh\:mm",CultureInfo.InvariantCulture,out start))throw new Exception("开始时间应为 HH:mm。");int duration=int.Parse(row[2].Text);if(duration<1||duration>360||start.TotalMinutes+duration>=1440)throw new Exception("每节课长应为1–360分钟且不可跨午夜。");next.Periods.Add(new ClassPeriod{Name=row[0].Text.Trim(),Start=row[1].Text,End=start.Add(TimeSpan.FromMinutes(duration)).ToString(@"hh\:mm")});}TimetablePreferences.Validate(next);var proposed=Data.Parse(Data.Serialize(state));proposed.Timetable=next;TimetablePreferences.Apply(proposed);if(Mutate(delegate{state.Timetable=next;TimetablePreferences.Apply(state);courseWeek=next.CurrentWeek(BeijingClock.Now.Date);},"已保存教学周与作息"))w.Close();}catch(Exception ex){MessageBox.Show(w,ex.Message);}}));w.ShowDialog();
 }

 }
}


