using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows;
using System.Windows.Controls;
using System.Globalization;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
namespace Shiri {
 [DataContract] public class Course {
 [DataMember] public string Id=Guid.NewGuid().ToString("N");
 [DataMember] public string Title="",Location="",Start="08:00",End="09:40";
 [DataMember] public string WeekPattern="All";
 [DataMember] public int FirstPeriod,LastPeriod;
 [DataMember] public int Day=1,FirstWeek=1,LastWeek=20;
 }
 public partial class Controller {
 int courseWeek=0;
 void RenderTimetable(){
  if(courseWeek==0)courseWeek=state.Timetable.CurrentWeek(BeijingClock.Now.Date);
  C<Border>("WeekCard").Visibility=Visibility.Collapsed;C<Grid>("ListToolbar").Visibility=Visibility.Collapsed;C<ScrollViewer>("TaskScroll").Visibility=Visibility.Collapsed;
  var host=C<ContentControl>("DiaryHost");host.Visibility=Visibility.Visible;var panel=new DockPanel{LastChildFill=true};
  var bar=new Grid{Height=76,HorizontalAlignment=HorizontalAlignment.Left,Width=Math.Max(420,host.ActualWidth),Margin=new Thickness(0,0,0,14)};
  panel.SizeChanged+=delegate{bar.Width=Math.Max(420,panel.ActualWidth);};
  bar.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(104)});bar.ColumnDefinitions.Add(new ColumnDefinition());bar.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(48)});
  var title=Text("课程表",26,"#294C37");title.FontWeight=FontWeights.SemiBold;bar.Children.Add(title);
  var navigation=new StackPanel{Orientation=Orientation.Horizontal};
  var previous=Btn("‹",delegate{courseWeek=Math.Max(1,courseWeek-1);RenderTimetable();});previous.Name="PreviousTeachingWeek";previous.IsEnabled=courseWeek>1;previous.FontSize=32;previous.Width=48;previous.Height=48;previous.Padding=new Thickness(0);previous.ToolTip="上一教学周";navigation.Children.Add(previous);
  DateTime anchor=DateTime.Parse(state.Timetable.WeekAnchorDate),monday=anchor.AddDays(-((int)anchor.DayOfWeek+6)%7+(courseWeek-state.Timetable.WeekAnchorNumber)*7);
  var current=new StackPanel{Width=154,VerticalAlignment=VerticalAlignment.Center};
  var heading=Text("第 "+courseWeek+" 教学周",22,"#356A51");heading.Name="TeachingWeekLabel";heading.FontWeight=FontWeights.SemiBold;heading.HorizontalAlignment=HorizontalAlignment.Center;current.Children.Add(heading);
  var range=Text(monday.ToString("M月d日")+" — "+monday.AddDays(6).ToString(monday.Month==monday.AddDays(6).Month?"d日":"M月d日"),11,"#91A08F");range.Margin=new Thickness(0,3,0,0);range.HorizontalAlignment=HorizontalAlignment.Center;current.Children.Add(range);navigation.Children.Add(current);
  var next=Btn("›",delegate{courseWeek=Math.Min(state.Timetable.TotalWeeks,courseWeek+1);RenderTimetable();});next.Name="NextTeachingWeek";next.IsEnabled=courseWeek<state.Timetable.TotalWeeks;next.FontSize=32;next.Width=48;next.Height=48;next.Padding=new Thickness(0);next.ToolTip="下一教学周";navigation.Children.Add(next);
  var navigationCard=new Border{Child=navigation,Background=System.Windows.Media.Brushes.White,BorderBrush=B("#E3EAE1"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(14),Padding=new Thickness(4),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(navigationCard,1);bar.Children.Add(navigationCard);
  var settings=Btn("",delegate{CourseMenu();});settings.Name="CourseSettingsButton";settings.Content=new Image{Source=(System.Windows.Media.ImageSource)Window.Resources["SettingsGlyph"],Width=22,Height=22};settings.Width=44;settings.Height=44;settings.Padding=new Thickness(0);settings.Background=System.Windows.Media.Brushes.White;settings.ToolTip="课程表设置";Grid.SetColumn(settings,2);bar.Children.Add(settings);
  DockPanel.SetDock(bar,Dock.Top);panel.Children.Add(bar);
  var board=new CourseBoard{Courses=state.Courses,Settings=state.Timetable,Week=courseWeek,Open=EditCourse,MinHeight=100,Name="WeeklyCourseBoard"};panel.Children.Add(board);host.Content=panel;
  C<TextBlock>("FooterMessage").Text="整周全天同屏 · 点击课程查看或编辑 · 无课位置留白";
 }

 Popup courseMenu;
 void CourseMenu(){
  if(courseMenu!=null&&courseMenu.IsOpen){courseMenu.IsOpen=false;return;}
  var target=FindCourseSettings(C<ContentControl>("DiaryHost"));if(target==null)return;
  var popup=new Popup{PlacementTarget=target,Placement=PlacementMode.Bottom,HorizontalOffset=-238,VerticalOffset=8,AllowsTransparency=true,StaysOpen=false,PopupAnimation=PopupAnimation.None};courseMenu=popup;
  var items=new StackPanel{Width=258};
  var label=Text("课程表设置",12,"#8A998D");label.Margin=new Thickness(12,6,0,8);items.Children.Add(label);
  Action separator=delegate{items.Children.Add(new Border{Height=1,Background=B("#EDF1E9"),Margin=new Thickness(10,7,10,7)});};
  Action<string,string,Action,bool> add=delegate(string caption,string icon,Action action,bool primary){
   var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(30)});row.ColumnDefinitions.Add(new ColumnDefinition());
   var glyph=Text(icon,18,primary?"#356A51":"#79907D");glyph.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(glyph);
   var text=Text(caption,14,primary?"#356A51":"#405849");text.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(text,1);row.Children.Add(text);
   var button=Btn("",delegate{popup.IsOpen=false;action();});button.Content=row;button.Height=42;button.Margin=new Thickness(2);button.Padding=new Thickness(12,0,12,0);button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.Background=B(primary?"#EAF2E7":"#FFFFFF");
   System.Windows.Automation.AutomationProperties.SetName(button,caption);
   button.MouseEnter+=delegate{button.Background=B("#EDF3E9");};button.MouseLeave+=delegate{button.Background=B(primary?"#EAF2E7":"#FFFFFF");};items.Children.Add(button);
  };
  add("添加课程","+",delegate{EditCourse(null);},true);
  add("从图片离线识别","▧",ImportCourseImage,false);add("继续识别草稿","≡",ResumeOcrDraft,false);separator();
  add("作息与教学周","◷",EditTimetableSettings,false);add("学期设置","▦",EditSemester,false);separator();
  add("回到本周","↶",delegate{courseWeek=state.Timetable.CurrentWeek(BeijingClock.Now.Date);RenderTimetable();},false);
  add(rightExpanded?"收起右侧工具栏":"展开右侧工具栏",rightExpanded?"›":"‹",delegate{SetRightSidebar(!rightExpanded);SaveSidebarChoice();RenderTimetable();},false);
  var surface=new Border{Background=Brushes.White,BorderBrush=B("#E1E9DC"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(14),Padding=new Thickness(6),Child=items,Margin=new Thickness(8),Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=14,ShadowDepth=3,Opacity=.13,Color=Colors.Black}};
  surface.Resources=Window.Resources;
  KeyboardNavigation.SetTabNavigation(surface,KeyboardNavigationMode.Cycle);
  surface.PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Escape){popup.IsOpen=false;e.Handled=true;target.Focus();}};
  popup.Child=surface;popup.Opened+=delegate{items.Children.OfType<Button>().First().Focus();};popup.IsOpen=true;
 }
 static Button FindCourseSettings(DependencyObject parent){for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);var button=child as Button;if(button!=null&&button.Name=="CourseSettingsButton")return button;var found=FindCourseSettings(child);if(found!=null)return found;}return null;}
 void EditCourse(Course original){
  var dialog=new CourseDialog(Window,state.Timetable,original);
  dialog.Commit=delegate(Course course){
   int conflicts=state.Courses.Count(x=>x.Id!=course.Id&&x.Day==course.Day&&String.CompareOrdinal(x.Start,course.End)<0&&String.CompareOrdinal(x.End,course.Start)>0&&Enumerable.Range(Math.Max(x.FirstWeek,course.FirstWeek),Math.Max(0,Math.Min(x.LastWeek,course.LastWeek)-Math.Max(x.FirstWeek,course.FirstWeek)+1)).Any(week=>(x.WeekPattern=="All"||String.IsNullOrEmpty(x.WeekPattern)||(x.WeekPattern=="Odd"?week%2==1:week%2==0))&&(course.WeekPattern=="All"||(course.WeekPattern=="Odd"?week%2==1:week%2==0))));
   if(conflicts>0&&MessageBox.Show(dialog.Window,"与 "+conflicts+" 条课程时间重叠，仍保留并列课程？","课程冲突",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return false;
   return Mutate(delegate{state.Courses.RemoveAll(x=>x.Id==course.Id);state.Courses.Add(course);},"课程已保存");
  };
  dialog.DeleteCommit=delegate{return Mutate(delegate{state.Courses.RemoveAll(x=>x.Id==original.Id);},"课程已删除，可按 Ctrl+Z 撤销");};
  dialog.Window.ShowDialog();
 }
 public static void ValidateCourses(List<Course> courses){if(courses==null||courses.Count>2000)throw new Exception("课程数据无效");var ids=new HashSet<string>();foreach(var c in courses){TimeSpan s,e;if(c!=null&&String.IsNullOrEmpty(c.WeekPattern))c.WeekPattern="All";if(c==null||!new[]{"All","Odd","Even"}.Contains(c.WeekPattern)||String.IsNullOrWhiteSpace(c.Id)||!ids.Add(c.Id)||String.IsNullOrWhiteSpace(c.Title)||c.Title.Length>120||(c.Location??"").Length>200||c.Day<1||c.Day>7||c.FirstWeek<1||c.LastWeek<c.FirstWeek||c.LastWeek>60||!TimeSpan.TryParseExact(c.Start,@"hh\:mm",CultureInfo.InvariantCulture,out s)||!TimeSpan.TryParseExact(c.End,@"hh\:mm",CultureInfo.InvariantCulture,out e)||e<=s||e.TotalHours>=24)throw new Exception("请检查课程名称、星期、时间及周次（1–60）。");}}
  void Transfer(bool importing){
   try{
    var local=Data.Parse(Data.Serialize(state));local.Countdowns=preferences.Countdowns;
    if(!importing){var d=new Microsoft.Win32.SaveFileDialog{Filter="拾日交换文件|*.json",FileName="拾日-双端.json"};if(d.ShowDialog(Window)==true)File.WriteAllText(d.FileName,Data.Serialize(local));return;}
    var file=new Microsoft.Win32.OpenFileDialog{Filter="拾日交换文件|*.json"};if(file.ShowDialog(Window)!=true)return;if(new FileInfo(file.FileName).Length>10*1024*1024)throw new Exception("文件超过10 MB");var incoming=Data.Parse(File.ReadAllText(file.FileName));
    var counts=new[]{TransferModel.Count(local.Tasks,incoming.Tasks,x=>x.Id),TransferModel.Count(local.Goals,incoming.Goals,x=>x.Id),TransferModel.Count(local.Courses,incoming.Courses,x=>x.Id),TransferModel.Count(local.Countdowns,incoming.Countdowns,x=>x.Id)};
    bool? choice=null;var w=new Window{Owner=Window,Title="导入预览",Width=430,Height=310,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=B("#F8F9F6"),Resources=Window.Resources};var body=new StackPanel{Margin=new Thickness(24)};w.Content=body;body.Children.Add(Text("新增 "+counts.Sum(x=>x[0])+" 条 · 内容冲突 "+counts.Sum(x=>x[1])+" 条",18,"#356A51"));var notice=Text((incoming.HasTimetable?"文件含作息与学期设置。":"保留本机作息与学期设置。")+"\n导入前自动备份，不会同步删除。",13,"#71836A");notice.Margin=new Thickness(0,12,0,14);body.Children.Add(notice);body.Children.Add(Btn("使用导入内容",delegate{choice=true;w.Close();}));body.Children.Add(Btn("保留本机冲突项",delegate{choice=false;w.Close();}));body.Children.Add(Btn("取消",delegate{w.Close();}));w.ShowDialog();if(!choice.HasValue)return;
    var merged=TransferModel.Merge(local,incoming,choice.Value);var next=new AppPreferences{PetEnabled=preferences.PetEnabled,RightSidebarExpanded=preferences.RightSidebarExpanded,Countdowns=merged.Countdowns};CountdownModel.Normalize(next);
    if(path==null||preferencesPath==null)throw new Exception("预览模式不写入数据。");Directory.CreateDirectory(Path.GetDirectoryName(path));string backup=Path.Combine(Path.GetDirectoryName(path),"before-import-tasks.json"),settingsBackup=Path.Combine(Path.GetDirectoryName(path),"before-import-settings.json");Data.Save(backup,local);AppPreferences.Save(settingsBackup,preferences);
    byte[] oldTasks=File.Exists(path)?File.ReadAllBytes(path):null,oldSettings=File.Exists(preferencesPath)?File.ReadAllBytes(preferencesPath):null;
    try{Data.Save(path,merged);AppPreferences.Save(preferencesPath,next);}catch{if(oldTasks!=null)File.WriteAllBytes(path,oldTasks);else if(File.Exists(path))File.Delete(path);if(oldSettings!=null)File.WriteAllBytes(preferencesPath,oldSettings);else if(File.Exists(preferencesPath))File.Delete(preferencesPath);throw;}
    state=merged;preferences=next;undoJson=null;courseWeek=0;Render();RenderCountdown();C<TextBlock>("FooterMessage").Text="已合并双端数据，可从软件设置恢复导入前备份。";
   }catch(Exception ex){MessageBox.Show(Window,"导入导出未完成："+ex.Message);}
  }

 }
}









