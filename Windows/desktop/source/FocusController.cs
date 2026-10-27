using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace Shiri {
 public partial class Controller {
  FocusEngine focus;
  FocusPie focusPie;
  FocusWorkspace focusWorkspace;
  string focusPath,yearCalendarKey;
  int calendarYear=DateTime.Today.Year;
  bool focusFullScreen;
  WindowStyle previousStyle;
  ResizeMode previousResize;
  WindowState previousState;
  Rect previousBounds;
  double previousMinWidth,previousMinHeight;
  FocusSaveQueue focusSaveQueue;
  bool focusRendering;long focusFrameCount;
  void InitializeFocus(string storage) {
   focusPath=storage==null ? null : Path.Combine(Path.GetDirectoryName(storage),"focus.json");
   focus=new FocusEngine(FocusStorage.Load(focusPath));if(focusPath!=null)focusSaveQueue=new FocusSaveQueue(focusPath);focusPie=new FocusPie{OverviewOnly=true};C<ContentControl>("FocusChart").Content=focusPie;
   C<Button>("FocusStatsOpen").Click+=delegate{OpenFocusStatistics();};
   C<Button>("FocusButton").Click+=delegate{ToggleFocus();};C<Button>("FocusReset").Click+=delegate{ResetFocus();};
   C<Button>("FocusSettings").Click+=delegate{EditFocusSettings();};C<Button>("FocusExpand").Click+=delegate{OpenFocus();};
   C<Expander>("CalendarExpander").Expanded+=delegate{RenderDates();C<Expander>("CalendarExpander").BringIntoView();C<ScrollViewer>("CalendarScroll").ScrollToTop();};
   C<Button>("PreviousYear").Click+=delegate{calendarYear=Math.Max(1,calendarYear-1);RenderYearCalendar();};
   C<Button>("NextYear").Click+=delegate{calendarYear=Math.Min(9998,calendarYear+1);RenderYearCalendar();};
   Window.IsVisibleChanged+=delegate{SyncFocusRendering();};Window.StateChanged+=delegate{SyncFocusRendering();};
   Window.Closed+=delegate{if(focusRendering){CompositionTarget.Rendering-=FocusFrame;focusRendering=false;}};
   Window.Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e){focus.Pause();SyncFocusRendering();if(!FlushFocus()){e.Cancel=true;MessageBox.Show(Window,"专注记录暂时无法保存，窗口已保留。请检查 data 文件夹的写入权限后再关闭。","拾日",MessageBoxButton.OK,MessageBoxImage.Warning);}};
  }
  bool SaveFocus() {
   if(focusPath==null)return true;
   try {focusSaveQueue.Enqueue(focus.Snapshot());return true;}
   catch(Exception error){C<TextBlock>("FooterMessage").Text="专注记录暂未保存："+error.Message;return false;}
  }
  bool FlushFocus(){if(focusPath==null)return true;try{return focusSaveQueue.Flush(focus.Snapshot());}catch(Exception error){C<TextBlock>("FooterMessage").Text="专注记录暂未保存："+error.Message;return false;}}
  void SyncFocusRendering(){
   bool wanted=focus!=null&&focus.Running&&Window.IsVisible&&Window.WindowState!=WindowState.Minimized;
   if(wanted==focusRendering)return;focusRendering=wanted;if(wanted){CompositionTarget.Rendering+=FocusFrame;RefreshFocusFrame();}else CompositionTarget.Rendering-=FocusFrame;
  }
  void FocusFrame(object sender,EventArgs e){RefreshFocusFrame();}
  void RefreshFocusFrame(){
   if(!focusRendering)return;focusFrameCount++;
   if(focus.Running&&focus.Remaining<=0){TickFocus();return;}
   string time=FocusEngine.FormatTime(focus.Remaining);if(C<TextBlock>("FocusTime").Text!=time)C<TextBlock>("FocusTime").Text=time;
   if(focusWorkspace!=null&&C<ContentControl>("FocusOverlay").Visibility==Visibility.Visible)focusWorkspace.RefreshClock(focus,DateTime.UtcNow);
  }
  void ToggleFocus(){TickFocus();focus.Toggle();RenderFocus();UpdatePetPage();}
  void ResetFocus(){TickFocus();focus.Reset();RenderFocus();UpdatePetPage();}
  void RestFocus(){TickFocus();if(focus.Phase==FocusPhase.Break)focus.EndBreak();else focus.StartBreak();RenderFocus();UpdatePetPage();}
  void EditFocusSettings() {
   var dialog=new FocusSettingsDialog(Window,focus,state.Tasks,state.Goals);
   if(dialog.Window.ShowDialog()!=true)return;
   focus.Configure(dialog.FocusMinutes,dialog.BreakMinutes,dialog.TaskName,dialog.Color,dialog.GoalId);SaveFocus();RenderFocus();
  }
  void TickFocus() {
   var signal=focus.Tick();
   if(signal==FocusSignal.FocusFinished){SaveFocus();Notify("专注完成",focus.Data.FocusMinutes+" 分钟的专注结束，已开始 "+focus.Data.BreakMinutes+" 分钟休息。");}
   else if(signal==FocusSignal.BreakFinished){SaveFocus();Notify("休息结束","准备好以后，点击开始下一轮专注吧。");}
   if(focusSaveQueue!=null&&focusSaveQueue.Error!=null)C<TextBlock>("FooterMessage").Text="专注记录暂未保存："+focusSaveQueue.Error;
   RenderFocus();
  }
  void RenderFocus() {
   if(focus==null)return;DateTime utc=DateTime.UtcNow;
   string time=FocusEngine.FormatTime(focus.Remaining);if(C<TextBlock>("FocusTime").Text!=time)C<TextBlock>("FocusTime").Text=time;
   string hint=focus.Phase==FocusPhase.Break ? "休息片刻，给自己一口呼吸" : focus.Project.Name;
   C<TextBlock>("FocusHint").Text=hint;C<TextBlock>("FocusHint").ToolTip=hint;
   C<Button>("FocusButton").Content=focus.Running ? "Ⅱ   暂停"+(focus.Phase==FocusPhase.Focus ? "专注" : "休息") : focus.Progress>0 ? "▷   继续" : focus.Phase==FocusPhase.Focus ? "▷   开始专注" : "▷   开始休息";
   var slices=focus.Statistics(utc);focusPie.Refresh(slices);double total=slices.Sum(x=>x.Seconds);
   if(focusStatistics!=null)focusStatistics.Refresh(focus,utc,slices);
   bool expanded=C<ContentControl>("FocusOverlay").Visibility==Visibility.Visible;
   if(focusWorkspace!=null&&expanded)focusWorkspace.Refresh(focus,utc,state.Tasks,focusFullScreen,total);
   if(!expanded)RefreshGoalEffort();SyncFocusRendering();
  }
  void OpenFocus() {if(floralView!=null)floralView.Visibility=Visibility.Collapsed;
   if(focusWorkspace==null)focusWorkspace=new FocusWorkspace(ToggleFocus,ResetFocus,EditFocusSettings,RestFocus,ToggleFocusFullScreen,CloseFocus,OpenCountdowns);
   var overlay=C<ContentControl>("FocusOverlay");overlay.Content=focusWorkspace;overlay.Visibility=Visibility.Visible;PageMotion.Enter(overlay);RenderCountdown();RenderFocus();focusWorkspace.PauseButton.Focus();UpdatePetPage();
  }
  void CloseFocus(){if(floralView!=null)floralView.Visibility=Visibility.Visible;if(focusFullScreen)ToggleFocusFullScreen();C<ContentControl>("FocusOverlay").Visibility=Visibility.Collapsed;PageMotion.Enter(C<Grid>("PageContent"));C<Button>("FocusExpand").Focus();if(view=="goal")RenderGoals();UpdatePetPage();}
  void ToggleFocusFullScreen() {
   if(!focusFullScreen) {
    previousStyle=Window.WindowStyle;previousResize=Window.ResizeMode;previousState=Window.WindowState;previousMinWidth=Window.MinWidth;previousMinHeight=Window.MinHeight;
    previousBounds=Window.WindowState==WindowState.Normal ? new Rect(Window.Left,Window.Top,Window.Width,Window.Height) : Window.RestoreBounds;
    var screen=System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(Window).Handle);
    var source=PresentationSource.FromVisual(Window);var transform=source==null ? Matrix.Identity : source.CompositionTarget.TransformFromDevice;
    Point corner=transform.Transform(new Point(screen.Bounds.Left,screen.Bounds.Top)),size=transform.Transform(new Point(screen.Bounds.Width,screen.Bounds.Height));
    Window.WindowState=WindowState.Normal;Window.WindowStyle=WindowStyle.None;Window.ResizeMode=ResizeMode.NoResize;Window.MinWidth=0;Window.MinHeight=0;
    Window.Left=corner.X;Window.Top=corner.Y;Window.Width=size.X;Window.Height=size.Y;focusFullScreen=true;
   } else {
    Window.WindowState=WindowState.Normal;Window.WindowStyle=previousStyle;Window.ResizeMode=previousResize;
    Window.Left=previousBounds.Left;Window.Top=previousBounds.Top;Window.Width=previousBounds.Width;Window.Height=previousBounds.Height;Window.MinWidth=previousMinWidth;Window.MinHeight=previousMinHeight;Window.WindowState=previousState;focusFullScreen=false;
   }
   RenderFocus();if(currentPetMessage!=null)pet.React(currentPetMessage.Expression,false);
  }
  void HandleFocusKey(KeyEventArgs e) {
   if(C<ContentControl>("FocusOverlay").Visibility!=Visibility.Visible)return;
   if(e.Key==Key.Escape){if(focusFullScreen)ToggleFocusFullScreen();else CloseFocus();e.Handled=true;return;}
   if(e.Key==Key.Space&&Keyboard.Modifiers==ModifierKeys.None&&!IsTypingSource(e.OriginalSource as DependencyObject)&&!(Keyboard.FocusedElement is TextBoxBase)&&!(Keyboard.FocusedElement is ComboBox)) {
    if(!e.IsRepeat)ToggleFocus();e.Handled=true;
   }
  }
  static bool IsTypingSource(DependencyObject source){while(source!=null){if(source is TextBoxBase||source is ComboBox)return true;source=source is Visual ? VisualTreeHelper.GetParent(source) : null;}return false;}
  void RenderYearCalendar() { UpdateYearTiles(); }
 }
}

