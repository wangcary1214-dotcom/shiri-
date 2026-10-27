using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Shiri {
 public sealed class CourseDialog {
  public Window Window;public Course Result;public bool Delete;
  public Func<Course,bool> Commit;public Func<bool> DeleteCommit;
  public TextBox TitleInput,LocationInput;
  public ComboBox DayInput,ModeInput,FirstPeriod,LastPeriod,StartInput,EndInput,FirstWeek,LastWeek,Pattern;
  public Button SaveButton;public TextBlock Error;
  readonly TimetablePreferences settings;readonly Course source;
  public CourseDialog(Window owner,TimetablePreferences timetable,Course course){
   settings=timetable;source=course??new Course();
   Window=new Window{Owner=owner,Title=course==null?"添加课程":"编辑课程",Width=660,Height=734,MaxHeight=Math.Max(540,SystemParameters.WorkArea.Height-32),ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Controller.B("#F7F9F5"),FontFamily=owner.FontFamily,Foreground=Controller.B("#355447"),Resources=owner.Resources};
   var root=new DockPanel{Margin=new Thickness(28,22,28,24)};Window.Content=root;
   var heading=new StackPanel{Margin=new Thickness(0,0,0,14)};var title=Controller.Text(course==null?"安排一门课程":"编辑课程安排",25,"#294C37");title.FontWeight=FontWeights.SemiBold;heading.Children.Add(title);var subtitle=Controller.Text("课程信息、上课时间与教学周，一次设置。",12,"#98A59A");subtitle.Margin=new Thickness(0,5,0,0);heading.Children.Add(subtitle);DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
   var footer=new StackPanel();DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);Error=Controller.Text("",12,"#B57660");Error.TextWrapping=TextWrapping.Wrap;Error.MinHeight=20;Error.Margin=new Thickness(0,6,0,6);footer.Children.Add(Error);
   var actions=new Grid();actions.ColumnDefinitions.Add(new ColumnDefinition());actions.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});actions.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});footer.Children.Add(actions);
   if(course!=null){var remove=Controller.Btn("删除课程",delegate{if(MessageBox.Show(Window,"删除这门课程？删除后可按 Ctrl+Z 撤销。","删除课程",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;if(DeleteCommit!=null&&!DeleteCommit())return;Delete=true;Window.DialogResult=true;});remove.Foreground=Controller.B("#AC7867");remove.HorizontalAlignment=HorizontalAlignment.Left;actions.Children.Add(remove);}
   var cancel=Controller.Btn("取消",delegate{Window.DialogResult=false;});cancel.IsCancel=true;cancel.Width=84;cancel.Height=44;cancel.Margin=new Thickness(0,0,10,0);Grid.SetColumn(cancel,1);actions.Children.Add(cancel);
   SaveButton=Controller.Btn("保存课程",delegate{Save();});SaveButton.IsDefault=true;SaveButton.Width=124;SaveButton.Height=44;SaveButton.Background=Controller.B("#356A51");SaveButton.Foreground=Brushes.White;Grid.SetColumn(SaveButton,2);actions.Children.Add(SaveButton);
   var scroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};var body=new StackPanel{Margin=new Thickness(0,0,8,0)};scroll.Content=body;root.Children.Add(scroll);
   var basic=Section(body,"基本信息");TitleInput=Input(source.Title,120);TitleInput.Name="CourseTitleInput";LocationInput=Input(source.Location,200);LocationInput.Name="CourseLocationInput";
   TitleInput.TextChanged+=delegate{Error.Text="";};LocationInput.TextChanged+=delegate{Error.Text="";};
   var basicRow=Columns(2);basicRow.Children.Add(Field("课程名称",TitleInput));var location=Field("上课地点",LocationInput);Grid.SetColumn(location,1);basicRow.Children.Add(location);basic.Children.Add(basicRow);
   var time=Section(body,"上课时间");var choices=Columns(2);DayInput=Select(new[]{"周一","周二","周三","周四","周五","周六","周日"},source.Day-1);ModeInput=Select(new[]{"按节次安排","自定义时间"},course!=null&&source.FirstPeriod==0?1:0);choices.Children.Add(Field("星期",DayInput));var mode=Field("时间方式",ModeInput);Grid.SetColumn(mode,1);choices.Children.Add(mode);time.Children.Add(choices);
   var periods=settings.Periods.Select((p,n)=>"第"+(n+1)+"节 · "+p.Start+"–"+p.End).ToList();FirstPeriod=Select(periods,Math.Max(0,source.FirstPeriod-1));LastPeriod=Select(periods,source.LastPeriod>0?source.LastPeriod-1:Math.Min(1,periods.Count-1));
   var periodRow=Columns(2);periodRow.Children.Add(Field("起始节次",FirstPeriod));var last=Field("结束节次",LastPeriod);Grid.SetColumn(last,1);periodRow.Children.Add(last);time.Children.Add(periodRow);
   var times=new List<string>();for(int minute=0;minute<1440;minute+=5)times.Add(TimeSpan.FromMinutes(minute).ToString(@"hh\:mm"));StartInput=Select(times,0);EndInput=Select(times,0);StartInput.IsEditable=EndInput.IsEditable=true;StartInput.Text=source.Start;EndInput.Text=source.End;
   var customRow=Columns(2);customRow.Children.Add(Field("开始时间",StartInput));var end=Field("结束时间",EndInput);Grid.SetColumn(end,1);customRow.Children.Add(end);time.Children.Add(customRow);
   var summary=Controller.Text("",12,"#6D8775");summary.TextWrapping=TextWrapping.Wrap;summary.Margin=new Thickness(0,12,0,0);time.Children.Add(summary);
   Action update=delegate{bool custom=ModeInput.SelectedIndex==1;periodRow.Visibility=custom?Visibility.Collapsed:Visibility.Visible;customRow.Visibility=custom?Visibility.Visible:Visibility.Collapsed;if(!custom&&FirstPeriod.SelectedIndex>=0&&LastPeriod.SelectedIndex>=0)summary.Text=settings.Periods[FirstPeriod.SelectedIndex].Start+" — "+settings.Periods[LastPeriod.SelectedIndex].End+"  ·  随作息时间自动调整";else summary.Text="选择时间，或直接输入准确的 HH:mm。";};ModeInput.SelectionChanged+=delegate{update();};FirstPeriod.SelectionChanged+=delegate{update();};LastPeriod.SelectionChanged+=delegate{update();};update();
   var weeks=Section(body,"教学周");var weekLabels=Enumerable.Range(1,settings.TotalWeeks).Select(n=>"第 "+n+" 周").ToList();FirstWeek=Select(weekLabels,source.FirstWeek-1);LastWeek=Select(weekLabels,source.LastWeek-1);Pattern=Select(new[]{"每周上课","仅单周","仅双周"},source.WeekPattern=="Odd"?1:source.WeekPattern=="Even"?2:0);
   var weekRow=Columns(3);weekRow.Children.Add(Field("开始周",FirstWeek));var ending=Field("结束周",LastWeek);Grid.SetColumn(ending,1);weekRow.Children.Add(ending);var pattern=Field("上课周次",Pattern);Grid.SetColumn(pattern,2);weekRow.Children.Add(pattern);weeks.Children.Add(weekRow);
   Window.Loaded+=delegate{TitleInput.Focus();TitleInput.CaretIndex=TitleInput.Text.Length;};
  }
  static Grid Columns(int count){var grid=new Grid();for(int n=0;n<count;n++)grid.ColumnDefinitions.Add(new ColumnDefinition());return grid;}
  static StackPanel Field(string label,Control control){var panel=new StackPanel{Margin=new Thickness(0,8,12,0)};var caption=Controller.Text(label,12,"#87978B");caption.Margin=new Thickness(0,0,0,7);panel.Children.Add(caption);panel.Children.Add(control);return panel;}
  static StackPanel Section(StackPanel parent,string name){var content=new StackPanel();var title=Controller.Text(name,13,"#4D6858");title.FontWeight=FontWeights.SemiBold;title.Margin=new Thickness(0,0,0,2);content.Children.Add(title);parent.Children.Add(new Border{Background=Brushes.White,BorderBrush=Controller.B("#E6ECE3"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(14),Padding=new Thickness(18,12,6,12),Margin=new Thickness(0,0,0,10),Child=content});return content;}
  static TextBox Input(string text,int limit){return new TextBox{Text=text??"",MaxLength=limit,MinHeight=42,FontSize=14};}
  static ComboBox Select(IEnumerable<string> values,int index){var items=values.ToList();return new ComboBox{ItemsSource=items,SelectedIndex=Math.Max(0,Math.Min(items.Count-1,index)),MinHeight=42};}
  void Save(){Error.Text="";
   try{
    var result=new Course{Id=source.Id,Title=TitleInput.Text.Trim(),Location=LocationInput.Text.Trim(),Day=DayInput.SelectedIndex+1,Start=StartInput.Text.Trim(),End=EndInput.Text.Trim(),FirstPeriod=ModeInput.SelectedIndex==1?0:FirstPeriod.SelectedIndex+1,LastPeriod=ModeInput.SelectedIndex==1?0:LastPeriod.SelectedIndex+1,FirstWeek=FirstWeek.SelectedIndex+1,LastWeek=LastWeek.SelectedIndex+1,WeekPattern=new[]{"All","Odd","Even"}[Pattern.SelectedIndex]};
    TimetablePreferences.Apply(new Store{Timetable=settings,Courses=new List<Course>{result}});Controller.ValidateCourses(new List<Course>{result});if(Commit!=null&&!Commit(result))return;Result=result;Window.DialogResult=true;
   }catch(Exception error){Error.Text=String.IsNullOrWhiteSpace(TitleInput.Text)?"请先填写课程名称。":error.Message;}
  }
 }
}
