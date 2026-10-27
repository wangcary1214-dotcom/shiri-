using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Forms=System.Windows.Forms;

namespace Shiri {
 public sealed class CountdownDialog {
  public readonly Window Window;
  public readonly TextBox NameInput,ColorInput,IconInput,YearsInput;
  public readonly DatePicker DateInput;
  public readonly ComboBox ModeInput;
  public CountdownItem Result;
  public bool Delete;
  readonly TextBlock error,dateLabel;
  readonly StackPanel datePanel,yearsPanel;
  readonly Border colorPreview;
  readonly CountdownItem original;
  string previousDefault;
  public CountdownDialog(Window owner,CountdownItem item) {
   original=item;var value=item==null ? new CountdownItem() : item.Copy();
   Window=new Window{Owner=owner,Title=item==null ? "新增倒计时 · 拾日" : "编辑倒计时 · 拾日",Width=480,Height=735,MaxHeight=Math.Max(450,SystemParameters.WorkArea.Height-24),ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Controller.B("#F8F9F6"),Foreground=Controller.B("#3B5140"),FontFamily=owner.FontFamily,FontSize=13,Resources=owner.Resources};
   var root=new DockPanel{Margin=new Thickness(26)};Window.Content=root;
   var actions=new DockPanel{Margin=new Thickness(0,18,0,0)};DockPanel.SetDock(actions,Dock.Bottom);root.Children.Add(actions);
   if(item!=null){var delete=Controller.Btn("删除",delegate{Delete=true;Window.DialogResult=true;});delete.Foreground=Controller.B("#A17C67");DockPanel.SetDock(delete,Dock.Left);actions.Children.Add(delete);}
   var save=Controller.Btn("保存倒计时",delegate{Save(value);});save.Background=Controller.B("#356A51");save.Foreground=Brushes.White;save.IsDefault=true;DockPanel.SetDock(save,Dock.Right);actions.Children.Add(save);
   var cancel=Controller.Btn("取消",delegate{Window.DialogResult=false;});cancel.IsCancel=true;cancel.HorizontalAlignment=HorizontalAlignment.Right;actions.Children.Add(cancel);
   var body=new StackPanel{Margin=new Thickness(0,0,6,0)};root.Children.Add(new ScrollViewer{Content=body});body.Children.Add(Controller.Text("把时光排成一列",23,"#35523E"));
   Label(body,"类型");ModeInput=new ComboBox{ItemsSource=CountdownModel.Labels,SelectedIndex=Array.IndexOf(CountdownModel.Modes,value.Mode)};body.Children.Add(ModeInput);
   Label(body,"名称");NameInput=new TextBox{Text=item==null ? "" : value.Title,MaxLength=60,Height=40};body.Children.Add(NameInput);
   datePanel=new StackPanel();body.Children.Add(datePanel);dateLabel=Label(datePanel,"目标日期");DateTime date;if(!CountdownModel.TryDate(value.Date,out date))date=BeijingClock.Now.Date.AddDays(30);
   DateInput=new DatePicker{SelectedDate=date,Height=40,SelectedDateFormat=DatePickerFormat.Short,Language=System.Windows.Markup.XmlLanguage.GetLanguage("zh-CN")};datePanel.Children.Add(DateInput);DateInput.DateValidationError+=delegate(object sender,DatePickerDateValidationErrorEventArgs e){e.ThrowException=false;DateInput.SelectedDate=null;};
   yearsPanel=new StackPanel();body.Children.Add(yearsPanel);Label(yearsPanel,"预计年数（1–150 年，可自行设定）");YearsInput=new TextBox{Text=value.LifeYears.ToString(),MaxLength=3,Height=40};yearsPanel.Children.Add(YearsInput);
   Label(body,"颜色");var palette=new WrapPanel();body.Children.Add(palette);
   foreach(string color in new[]{"#356A51","#3889D8","#7965C1","#D89C32","#D76E91","#43A68C"}){string chosen=color;var swatch=Controller.Btn("",delegate{ColorInput.Text=chosen;});swatch.Background=Controller.B(color);swatch.Width=29;swatch.Height=25;swatch.Padding=new Thickness(0);swatch.Margin=new Thickness(0,0,9,0);swatch.ToolTip=color;AutomationProperties.SetName(swatch,"使用颜色 "+color);palette.Children.Add(swatch);}
   var colorRow=new Grid{Margin=new Thickness(0,10,0,0)};colorRow.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(34)});colorRow.ColumnDefinitions.Add(new ColumnDefinition());colorRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});body.Children.Add(colorRow);
   colorPreview=new Border{Width=24,Height=24,CornerRadius=new CornerRadius(6),VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Left,Background=Controller.B(value.Color)};colorRow.Children.Add(colorPreview);
   ColorInput=new TextBox{Text=value.Color,Height=40,MaxLength=7};Grid.SetColumn(ColorInput,1);colorRow.Children.Add(ColorInput);ColorInput.TextChanged+=delegate{if(FocusStorage.ValidColor(ColorInput.Text.Trim()))colorPreview.Background=Controller.B(ColorInput.Text.Trim());};
   var choose=Controller.Btn("调色板",delegate{PickColor();});choose.Margin=new Thickness(7,0,0,0);choose.FontSize=11;Grid.SetColumn(choose,2);colorRow.Children.Add(choose);
   var colorHint=Controller.Text("可点预设色、打开调色板，或输入 #RRGGBB。",10,"#96A38B");colorHint.Margin=new Thickness(0,7,0,0);body.Children.Add(colorHint);
   Label(body,"图标（可选，输入一个表情符号）");IconInput=new TextBox{Text=value.Icon??"",MaxLength=16,Height=40};body.Children.Add(IconInput);
   var note=Controller.Text("留空时：进度显示圆环，日期显示日历图标。\n按北京时间计算；当天显示 0 天。",10,"#96A38B");note.TextWrapping=TextWrapping.Wrap;note.Margin=new Thickness(0,13,0,0);body.Children.Add(note);
   error=Controller.Text("",11,"#B36E53");error.TextWrapping=TextWrapping.Wrap;error.Margin=new Thickness(0,10,0,0);body.Children.Add(error);
   previousDefault=CountdownModel.Labels[ModeInput.SelectedIndex];ModeInput.SelectionChanged+=delegate{UpdateFields(true);};UpdateFields(false);Window.Loaded+=delegate{NameInput.Focus();NameInput.SelectAll();};
  }
  static TextBlock Label(StackPanel body,string text){var label=Controller.Text(text,11,"#819476");label.Margin=new Thickness(0,17,0,8);body.Children.Add(label);return label;}
  void UpdateFields(bool changed) {
   int index=Math.Max(0,ModeInput.SelectedIndex);string mode=CountdownModel.Modes[index];
   datePanel.Visibility=mode=="Date"||mode=="Anniversary"||mode=="Life" ? Visibility.Visible : Visibility.Collapsed;yearsPanel.Visibility=mode=="Life" ? Visibility.Visible : Visibility.Collapsed;
   dateLabel.Text=mode=="Life" ? "出生日期" : mode=="Anniversary" ? "开始日期" : "目标日期";
   if(changed){if(NameInput.Text==previousDefault)NameInput.Text=CountdownModel.Labels[index];if(original==null&&(mode=="Anniversary"||mode=="Life"))DateInput.SelectedDate=BeijingClock.Now.Date;}
   previousDefault=CountdownModel.Labels[index];
  }
  sealed class HandleOwner:Forms.IWin32Window{public IntPtr Handle{get;private set;}public HandleOwner(Window window){Handle=new WindowInteropHelper(window).Handle;}}
  void PickColor(){using(var picker=new Forms.ColorDialog{FullOpen=true,AnyColor=true}){if(FocusStorage.ValidColor(ColorInput.Text.Trim()))picker.Color=System.Drawing.ColorTranslator.FromHtml(ColorInput.Text.Trim());if(picker.ShowDialog(new HandleOwner(Window))==Forms.DialogResult.OK)ColorInput.Text="#"+picker.Color.R.ToString("X2")+picker.Color.G.ToString("X2")+picker.Color.B.ToString("X2");}}
  void Save(CountdownItem value) {
   string mode=CountdownModel.Modes[Math.Max(0,ModeInput.SelectedIndex)];int years;Int32.TryParse(YearsInput.Text,out years);
   var result=value.Copy();result.Mode=mode;result.Title=String.IsNullOrWhiteSpace(NameInput.Text) ? CountdownModel.Labels[ModeInput.SelectedIndex] : NameInput.Text.Trim();result.Date=DateInput.SelectedDate.HasValue ? DateInput.SelectedDate.Value.ToString("yyyy-MM-dd") : null;result.Color=ColorInput.Text.Trim().ToUpperInvariant();result.Icon=IconInput.Text.Trim();result.LifeYears=years;
   string message=CountdownModel.Error(result);if(message!=null){error.Text=message;return;}Result=result;Window.DialogResult=true;
  }
 }
 public sealed class CountdownGlyph:FrameworkElement {
  double progress;bool ring;string color;
  public void Refresh(CountdownItem item,CountdownReading reading){if(color==item.Color&&ring==reading.HasProgress&&Math.Abs(progress-reading.Progress)<.00001)return;color=item.Color;ring=reading.HasProgress;progress=reading.Progress;InvalidateVisual();}
  protected override void OnRender(DrawingContext dc){
   base.OnRender(dc);if(color==null)return;double size=Math.Min(ActualWidth,ActualHeight);if(size<=0)return;var center=new Point(ActualWidth/2,ActualHeight/2);double r=size/2-3;
   var brush=Controller.B(color);var pen=new Pen(brush,Math.Max(2,size/10)){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};
   if(ring){var track=brush.Clone();track.Opacity=.15;dc.DrawEllipse(null,new Pen(track,pen.Thickness),center,r,r);if(progress>=.99999)dc.DrawEllipse(null,pen,center,r,r);else if(progress>0){double angle=progress*Math.PI*2-Math.PI/2;var geometry=new StreamGeometry();using(var g=geometry.Open()){g.BeginFigure(new Point(center.X,center.Y-r),false,false);g.ArcTo(new Point(center.X+Math.Cos(angle)*r,center.Y+Math.Sin(angle)*r),new Size(r,r),0,progress>.5,SweepDirection.Clockwise,true,false);}dc.DrawGeometry(null,pen,geometry);}}
   else{double w=size*.68,h=size*.63,x=center.X-w/2,y=center.Y-h/2+1;var thin=new Pen(brush,1.4);dc.DrawRoundedRectangle(null,thin,new Rect(x,y,w,h),2,2);dc.DrawLine(thin,new Point(x,y+h*.3),new Point(x+w,y+h*.3));dc.DrawLine(thin,new Point(x+w*.28,y-2),new Point(x+w*.28,y+2));dc.DrawLine(thin,new Point(x+w*.72,y-2),new Point(x+w*.72,y+2));dc.DrawEllipse(brush,null,new Point(center.X,center.Y+3),1.4,1.4);}
  }
 }
 public sealed class CountdownRow:Button {
  readonly TextBlock title,detail,value,unit,emoji;readonly CountdownGlyph glyph;readonly bool compact;string visualKey;
  public CountdownRow(bool small,bool quiet=false){
   SetResourceReference(StyleProperty,typeof(Button));BorderThickness=new Thickness(0);
   compact=small;Padding=new Thickness(0,12,0,12);HorizontalContentAlignment=HorizontalAlignment.Stretch;Background=Brushes.Transparent;
   var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(small ? 31 : 53)});grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});Content=grid;
   var icon=new Grid{Width=small ? 25 : 40,Height=small ? 25 : 40,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center};glyph=new CountdownGlyph();icon.Children.Add(glyph);emoji=new TextBlock{FontFamily=new FontFamily("Segoe UI Emoji, Microsoft YaHei UI"),FontSize=small ? 22 : 32,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};icon.Children.Add(emoji);grid.Children.Add(icon);
   var labels=new StackPanel{VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,small ? 4 : 16,0)};Grid.SetColumn(labels,1);grid.Children.Add(labels);
   title=Controller.Text("",quiet ? 11 : small ? 11 : 16,quiet ? "#829575" : "#3B5140");title.FontWeight=quiet ? FontWeights.Normal : FontWeights.Medium;title.TextTrimming=TextTrimming.CharacterEllipsis;labels.Children.Add(title);
   detail=Controller.Text("",small ? 8 : 11,"#98A58D");detail.Margin=new Thickness(0,small ? 5 : 8,0,0);detail.TextTrimming=TextTrimming.CharacterEllipsis;labels.Children.Add(detail);
   var number=new StackPanel{VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Right,MaxWidth=small ? 54 : 115};Grid.SetColumn(number,2);grid.Children.Add(number);
   var fit=new Viewbox{Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,Height=quiet ? 24 : small ? 28 : 36,HorizontalAlignment=HorizontalAlignment.Right};value=Controller.Text("",quiet ? 22 : small ? 26 : 34,"#356A51");value.FontFamily=new FontFamily("Segoe UI");value.FontWeight=FontWeights.Light;fit.Child=value;number.Children.Add(fit);
   unit=Controller.Text("",small ? 9 : 11,"#98A58D");unit.HorizontalAlignment=HorizontalAlignment.Right;number.Children.Add(unit);
  }
  public void Refresh(CountdownItem item,CountdownReading reading){
   string key=item.Title+"/"+item.Color+"/"+item.Icon+"/"+reading.Value+"/"+reading.Detail;if(key!=visualKey){visualKey=key;title.Text=item.Title;detail.Text=compact ? reading.CompactDetail : reading.Detail;value.Text=reading.Value;value.Foreground=Controller.B(item.Color);unit.Text=reading.Unit;emoji.Text=item.Icon??"";emoji.Foreground=Controller.B(item.Color);emoji.Visibility=String.IsNullOrEmpty(item.Icon) ? Visibility.Collapsed : Visibility.Visible;glyph.Visibility=String.IsNullOrEmpty(item.Icon) ? Visibility.Visible : Visibility.Collapsed;ToolTip=item.Title+"\n"+reading.Detail+"\n"+reading.Value+reading.Unit+" · 点击编辑，右键排序或删除";AutomationProperties.SetName(this,item.Title+"，"+reading.Value+reading.Unit+"，"+reading.Detail);}
   glyph.Refresh(item,reading);
  }
 }
 public partial class Controller {
  readonly Dictionary<string,CountdownRow> compactCountdownRows=new Dictionary<string,CountdownRow>(),expandedCountdownRows=new Dictionary<string,CountdownRow>();
  readonly Dictionary<string,CountdownRow> focusCountdownRows=new Dictionary<string,CountdownRow>();
  Window countdownWindow;StackPanel expandedCountdownList;
  void InitializeCountdown(){CountdownModel.Normalize(preferences);C<Button>("CountdownEdit").Click+=delegate{EditCountdown();};C<Button>("CountdownExpand").Click+=delegate{OpenCountdowns();};RenderCountdown();}
  void EditCountdown(){EditCountdownItem(null);}
  void EditCountdownItem(CountdownItem item){
   if(item==null&&preferences.Countdowns.Count>=100){C<TextBlock>("FooterMessage").Text="最多保留 100 个倒计时或进度项目。";return;}
   var dialog=new CountdownDialog(countdownWindow??Window,item);if(dialog.Window.ShowDialog()!=true)return;
   var list=preferences.Countdowns.Select(x=>x.Copy()).ToList();int index=item==null ? -1 : list.FindIndex(x=>x.Id==item.Id);
   if(dialog.Delete){if(index>=0)list.RemoveAt(index);}else if(index>=0)list[index]=dialog.Result;else list.Add(dialog.Result);SaveCountdowns(list);
  }
  bool SaveCountdowns(List<CountdownItem> list){
   var value=new AppPreferences{PetEnabled=preferences.PetEnabled,RightSidebarExpanded=preferences.RightSidebarExpanded,Countdowns=list};
   try{AppPreferences.Save(preferencesPath,value);}catch(Exception error){MessageBox.Show(countdownWindow??Window,"倒计时未能保存，原列表已保留："+error.Message,"拾日");return false;}
   preferences=value;RenderCountdown();return true;
  }
  void MoveCountdown(string id,int step){var list=preferences.Countdowns.Select(x=>x.Copy()).ToList();int index=list.FindIndex(x=>x.Id==id),next=index+step;if(index<0||next<0||next>=list.Count)return;var item=list[index];list.RemoveAt(index);list.Insert(next,item);SaveCountdowns(list);}
  void DeleteCountdown(string id){SaveCountdowns(preferences.Countdowns.Where(x=>x.Id!=id).Select(x=>x.Copy()).ToList());}
  void RenderCountdown(){if(preferences==null)return;CountdownModel.Normalize(preferences);RefreshCountdownRows(C<StackPanel>("CountdownList"),true,compactCountdownRows);if(expandedCountdownList!=null)RefreshCountdownRows(expandedCountdownList,false,expandedCountdownRows);if(focusWorkspace!=null&&C<ContentControl>("FocusOverlay").Visibility==Visibility.Visible){focusWorkspace.CountdownPanel.Visibility=preferences.Countdowns.Count>0 ? Visibility.Visible : Visibility.Collapsed;focusWorkspace.CountdownReservedBottom=C<Border>("PetDock").Visibility==Visibility.Visible ? C<Border>("PetDock").ActualHeight+24 : 16;focusWorkspace.FitCountdown();RefreshCountdownRows(focusWorkspace.CountdownList,true,focusCountdownRows,true);}}
  void RefreshCountdownRows(StackPanel host,bool compact,Dictionary<string,CountdownRow> rows,bool quiet=false){
   var list=preferences.Countdowns;string order=String.Join("|",list.Select(x=>x.Id));bool structure=(host.Tag as string)!=order;
   if(structure){host.Children.Clear();foreach(var stale in rows.Keys.Where(id=>!list.Any(x=>x.Id==id)).ToList())rows.Remove(stale);}
   if(list.Count==0){if(structure){var note=Text(compact ? "添加日期、纪念日或进度" : "还没有倒计时。点击右上角“新增”，设置一个日期或进度。",compact ? 10 : 13,"#98A58D");note.TextWrapping=TextWrapping.Wrap;note.Margin=new Thickness(0,18,0,18);host.Children.Add(note);}host.Tag=order;return;}
   DateTime now=BeijingClock.Now;
   for(int i=0;i<list.Count;i++){
    var item=list[i];CountdownRow row;if(!rows.TryGetValue(item.Id,out row)){
     string id=item.Id;row=new CountdownRow(compact,quiet);row.Click+=delegate{var current=preferences.Countdowns.FirstOrDefault(x=>x.Id==id);if(current!=null)EditCountdownItem(current);};
     var menu=new ContextMenu();var edit=new MenuItem{Header="编辑"};edit.Click+=delegate{var current=preferences.Countdowns.FirstOrDefault(x=>x.Id==id);if(current!=null)EditCountdownItem(current);};menu.Items.Add(edit);
     var up=new MenuItem{Header="上移"};up.Click+=delegate{MoveCountdown(id,-1);};menu.Items.Add(up);var down=new MenuItem{Header="下移"};down.Click+=delegate{MoveCountdown(id,1);};menu.Items.Add(down);menu.Items.Add(new Separator());var delete=new MenuItem{Header="删除"};delete.Click+=delegate{DeleteCountdown(id);};menu.Items.Add(delete);row.ContextMenu=menu;rows[id]=row;
    }
    row.Refresh(item,CountdownModel.Measure(item,now));if(structure){if(i>0)host.Children.Add(new Border{Height=1,Background=B("#EBEFE7")});host.Children.Add(row);}
   }
   host.Tag=order;
  }
  void OpenCountdowns(){
   if(countdownWindow!=null){countdownWindow.Activate();return;}
   var dialog=new Window{Owner=Window,Title="倒计时与时光进度 · 拾日",Width=600,Height=720,MinWidth=480,MinHeight=380,MaxHeight=SystemParameters.WorkArea.Height-24,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=B("#F8F9F6"),Foreground=B("#3B5140"),FontFamily=Window.FontFamily,FontSize=13,Resources=Window.Resources};
   var root=new DockPanel{Margin=new Thickness(26)};dialog.Content=root;var top=new Grid{Margin=new Thickness(0,0,0,18)};DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);top.Children.Add(Text("时光，正在发生",23,"#35523E"));var add=Btn("＋ 新增",delegate{EditCountdown();});add.Background=B("#356A51");add.Foreground=Brushes.White;add.HorizontalAlignment=HorizontalAlignment.Right;top.Children.Add(add);
   var hint=Text("点击编辑 · 右键可上移、下移或删除 · 颜色随你设定",10,"#98A58D");hint.TextWrapping=TextWrapping.Wrap;hint.Margin=new Thickness(0,16,0,0);DockPanel.SetDock(hint,Dock.Bottom);root.Children.Add(hint);
   expandedCountdownList=new StackPanel();root.Children.Add(new ScrollViewer{Content=expandedCountdownList,Padding=new Thickness(0,0,8,0)});countdownWindow=dialog;RenderCountdown();
   dialog.Closed+=delegate{expandedCountdownList=null;expandedCountdownRows.Clear();countdownWindow=null;};dialog.Show();
  }
 }
}
