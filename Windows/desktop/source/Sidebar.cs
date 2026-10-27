using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Shiri {
 [DataContract]public sealed class AppPreferences {
  [DataMember]public int Version=1;
  [DataMember]public bool PetEnabled=true;
  [DataMember]public bool RightSidebarExpanded;
  [DataMember(EmitDefaultValue=false)]public List<CountdownItem> Countdowns;
  [DataMember(EmitDefaultValue=false)]public string CountdownTitle;
  [DataMember(EmitDefaultValue=false)]public string CountdownDate;
  public static AppPreferences Load(string path){if(path==null||!File.Exists(path))return new AppPreferences();using(var stream=File.OpenRead(path)){var value=(AppPreferences)new DataContractJsonSerializer(typeof(AppPreferences)).ReadObject(stream);if(value==null||value.Version!=1)throw new Exception("软件设置格式无法识别。");CountdownModel.Normalize(value);return value;}}
  public static void Save(string path,AppPreferences value){CountdownModel.Normalize(value);if(path==null)return;Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp";using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){new DataContractJsonSerializer(typeof(AppPreferences)).WriteObject(stream,value);stream.Flush(true);}if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);}
 }
 public partial class Controller {
  AppPreferences preferences;
  string preferencesPath,lastPetPage,lastPetRoot;
  bool petTemporarilyHidden,rightExpanded=true;
  void InitializeSidebar() {
   preferencesPath=path==null ? null : Path.Combine(Path.GetDirectoryName(path),"settings.json");preferences=AppPreferences.Load(preferencesPath);
   C<Button>("SideToggle").Click+=delegate{SetRightSidebar(!rightExpanded);SaveSidebarChoice();};
   C<Button>("CompactCountdowns").Click+=delegate{OpenCountdowns();};C<Button>("CompactStatistics").Click+=delegate{OpenFocusStatistics();};
   C<Button>("SideFocusShortcut").Click+=delegate{ShowSidebarTab(0);};C<Button>("SideCalendarShortcut").Click+=delegate{ShowSidebarTab(1);};C<Button>("SidePetShortcut").Click+=delegate{ShowSidebarTab(2);};
   var tabs=C<TabControl>("SideTabs");tabs.SelectionChanged+=delegate(object sender,SelectionChangedEventArgs e){if(e.Source!=tabs)return;if(tabs.SelectedIndex==1)C<Expander>("CalendarExpander").IsExpanded=true;tabs.ApplyTemplate();PageMotion.Enter(tabs.Template.FindName("PART_SelectedContentHost",tabs) as FrameworkElement);UpdatePetPage();};
   bool pointer=false;var chat=C<Button>("PetChatButton");chat.PreviewMouseLeftButtonDown+=delegate{pointer=true;};chat.PreviewKeyDown+=delegate{pointer=false;};chat.Click+=delegate{pet.Interact(pointer);pointer=false;};
   C<Button>("PetClose").Click+=delegate{petTemporarilyHidden=true;ApplyPetVisibility();};C<Button>("AppSettings").Click+=delegate{EditAppSettings();};
   Window.Loaded+=delegate{SetRightSidebar(preferences.RightSidebarExpanded);};
   Window.Activated+=delegate{if(currentPetMessage!=null)pet.React(currentPetMessage.Expression,false);};ApplyPetVisibility();
  }
  void SaveSidebarChoice(){preferences.RightSidebarExpanded=rightExpanded;try{AppPreferences.Save(preferencesPath,preferences);}catch(Exception ex){C<TextBlock>("FooterMessage").Text="侧栏偏好未保存："+ex.Message;}}
  void SetRightSidebar(bool expanded) {
   rightExpanded=expanded;ApplySidebarLayout();
   if(currentPetMessage!=null)pet.React(currentPetMessage.Expression,false);RenderGoalHeader();ApplyPetVisibility();
  }
  void ApplySidebarLayout() {
   // Keep the collapsed rail reachable on every page, including after Render() and startup.
   C<Border>("RightSidebar").Visibility=Visibility.Visible;
   C<ColumnDefinition>("RightColumn").Width=new GridLength(rightExpanded ? 380 : 44);
   C<TabControl>("SideTabs").Visibility=rightExpanded ? Visibility.Visible : Visibility.Collapsed;
   C<StackPanel>("SidebarShortcuts").Visibility=rightExpanded ? Visibility.Collapsed : Visibility.Visible;
   var toggle=C<Button>("SideToggle");toggle.Content=rightExpanded ? "›  收起右栏" : "‹";
   toggle.ToolTip=rightExpanded ? "收起右侧工具栏" : "展开右侧工具栏";
   toggle.FontSize=rightExpanded ? 10 : 20;toggle.Padding=rightExpanded ? new Thickness(10,5,10,5) : new Thickness(0);
   toggle.MinWidth=32;toggle.MinHeight=32;
   System.Windows.Automation.AutomationProperties.SetName(toggle,(string)toggle.ToolTip);
  }
  void ShowSidebarTab(int index){SetRightSidebar(true);SaveSidebarChoice();C<TabControl>("SideTabs").SelectedIndex=index;if(index==1)C<Expander>("CalendarExpander").IsExpanded=true;UpdatePetPage();}
  void ApplyPetVisibility(){bool show=preferences.PetEnabled&&!petTemporarilyHidden&&((view!="courses"&&view!="diary")||rightExpanded);C<Border>("PetDock").Visibility=show ? Visibility.Visible : Visibility.Collapsed;C<TabControl>("SideTabs").Margin=new Thickness(16,4,16,show ? 200 : 12);if(currentPetMessage!=null)pet.React(currentPetMessage.Expression,false);}
  string CurrentPetPage() {
   if(C<ContentControl>("FocusOverlay").Visibility==Visibility.Visible){if(!focus.Running&&focus.Progress>0)return "paused";return focus.Phase==FocusPhase.Break ? "rest" : "focus";}
   if(view=="diary")return "diary";
   if(view=="goal")return "goal";if(view=="all")return "goals";
   if(C<TabControl>("SideTabs").SelectedIndex==1&&rightExpanded)return "calendar";
   if(C<TabControl>("SideTabs").SelectedIndex==2&&rightExpanded)return "companion";
   return "today";
  }
  void UpdatePetPage() {
   if(preferences==null||pet==null)return;
   string root=C<ContentControl>("FocusOverlay").Visibility==Visibility.Visible ? "focus" : view=="goal" ? "goal:"+selectedGoalId : view;
   if(root!=lastPetRoot){if(root=="today")petTemporarilyHidden=false;lastPetRoot=root;}
   string page=CurrentPetPage();ApplyPetVisibility();if(page==lastPetPage)return;lastPetPage=page;
   if(preferences.PetEnabled)PetTalk(false);
  }
  PetMessage PagePetMessage(int total,int done,string next,int variation,int streak) {
   string page=CurrentPetPage(),name="远方计划",effort="0秒";
   if(page=="goal"){var goal=state.Goals.Find(x=>x.Id==selectedGoalId);if(goal!=null){name=goal.Title;effort=FocusEngine.FormatDuration(focus.GoalSeconds(goal.Id,DateTime.UtcNow));}}
   else if(page=="focus"||page=="rest"||page=="paused")name=focus.Project.Name;
   else if(page=="calendar")name=month.ToString("yyyy年M月");
   return PetMessage.ForPage(page,total,done,next,name,effort,FocusEngine.FormatTime(focus.Remaining),variation,streak);
  }
  void EditAppSettings() {
   var dialog=new Window{Owner=Window,Title="软件设置 · 拾日",Width=470,Height=620,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=B("#F8F9F6"),FontFamily=Window.FontFamily,Foreground=B("#3B5140"),Resources=Window.Resources};var body=new StackPanel{Margin=new Thickness(28)};dialog.Content=body;
   body.Children.Add(Text("软件设置",23,"#35523E"));var enabled=new CheckBox{Content="显示大肥鱼",IsChecked=preferences.PetEnabled,FontSize=14,Margin=new Thickness(0,27,0,12)};body.Children.Add(enabled);
   var note=Text("卡片 × 只临时隐藏，返回“我的一天”会恢复。\n在这里关闭后，所有页面和下次启动都保持隐藏。",11,"#8E9D82");note.TextWrapping=TextWrapping.Wrap;note.LineHeight=22;body.Children.Add(note);
   body.Children.Add(Text("数据管理",17,"#35523E"));
   body.Children.Add(Btn("导出双端数据",delegate{Transfer(false);}));body.Children.Add(Btn("导入双端数据（预览冲突）",delegate{Transfer(true);}));body.Children.Add(Btn("恢复上次导入前的数据",delegate{RestoreBeforeImport();}));body.Children.Add(Btn("导出完整本机备份 ZIP",delegate{ExportLocalBackup();}));body.Children.Add(Btn("日程 JSON 备份 / 恢复与数据位置",delegate{BackupMenu();}));
   var save=Btn("保存设置",delegate{var value=new AppPreferences{PetEnabled=enabled.IsChecked==true,RightSidebarExpanded=rightExpanded,Countdowns=preferences.Countdowns};try{AppPreferences.Save(preferencesPath,value);}catch(Exception error){MessageBox.Show(dialog,"设置未能保存："+error.Message,"拾日");return;}preferences=value;petTemporarilyHidden=false;ApplyPetVisibility();if(preferences.PetEnabled)PetTalk(false);dialog.DialogResult=true;});save.Background=B("#356A51");save.Foreground=Brushes.White;save.HorizontalAlignment=HorizontalAlignment.Right;save.Margin=new Thickness(0,18,0,0);body.Children.Add(save);dialog.ShowDialog();
  }
 }
}

