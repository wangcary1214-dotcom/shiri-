using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Reflection;
using System.Threading;
using System.Security.Cryptography;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

[assembly: AssemblyTitle("拾日 · 日程与待办")]
[assembly: AssemblyProduct("拾日 Shiri")]
[assembly: AssemblyDescription("本地日程待办与大肥鱼逐帧宠物")]
[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyFileVersion("2.0.0.0")]

namespace Shiri {
 [DataContract] public class TaskItem {
  [DataMember] public string Id = Guid.NewGuid().ToString("N");
  [DataMember] public string Title = "";
  [DataMember] public string Notes = "";
  [DataMember] public string Date = "";
  [DataMember] public string Time = "";
  [DataMember] public string Category = "工作";
  [DataMember] public int Priority = 1;
  [DataMember] public bool Done;
  [DataMember] public string Repeat = "不重复";
  [DataMember] public string Series = "";
  [DataMember] public string AutoParent = "";
  [DataMember] public bool Reminder;
  [DataMember] public bool Reminded;
  [DataMember] public bool IsExample;
  [DataMember] public string GoalId = "";
  [DataMember] public long CompletedTicks;
 }
 [DataContract] public class Store {
  public bool HasTimetable=true;
 [DataMember] public TimetablePreferences Timetable=TimetablePreferences.Default();
 [DataMember] public List<Course> Courses = new List<Course>();
  [DataMember] public List<CountdownItem> Countdowns = new List<CountdownItem>();
  [DataMember] public int Version = 2;
  [DataMember] public List<TaskItem> Tasks = new List<TaskItem>();
  [DataMember] public List<GoalItem> Goals = new List<GoalItem>();
 }
 public static class Data {
  public static string[] Categories = { "工作", "生活", "学习", "健康" };
  public static string[] Repeats = { "不重复", "每天", "每周" };
  public static string Serialize(Store state) {
   using (var ms = new MemoryStream()) { new DataContractJsonSerializer(typeof(Store)).WriteObject(ms, state); return Encoding.UTF8.GetString(ms.ToArray()); }
  }
  public static Store Parse(string json) {
   if (json.Length > 10 * 1024 * 1024) throw new Exception("备份文件过大（上限 10 MB）。");
   Store state;
   using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json))) state = (Store)new DataContractJsonSerializer(typeof(Store)).ReadObject(ms);
   if (state == null || (state.Version != 1&&state.Version!=2) || state.Tasks == null || state.Tasks.Count > 20000) throw new Exception("无法识别的拾日备份文件。");
   state.Version=2; if(state.Courses==null)state.Courses=new List<Course>(); state.HasTimetable=state.Timetable!=null;if(state.Timetable==null)state.Timetable=TimetablePreferences.Default();TimetablePreferences.Apply(state);Controller.ValidateCourses(state.Courses); if(state.Countdowns==null)state.Countdowns=new List<CountdownItem>(); CountdownModel.Normalize(new AppPreferences{Countdowns=state.Countdowns});
   if(state.Goals==null)state.Goals=new List<GoalItem>();
   var goalIds=new HashSet<string>();
   foreach(var goal in state.Goals)if(goal==null||String.IsNullOrWhiteSpace(goal.Id)||!goalIds.Add(goal.Id)||String.IsNullOrWhiteSpace(goal.Title)||goal.Title.Length>120||(goal.Notes!=null&&goal.Notes.Length>4000)||!FocusStorage.ValidColor(goal.Color))throw new Exception("长期任务数据无效。");
   var ids = new HashSet<string>();
   foreach (var t in state.Tasks) {
    DateTime date; TimeSpan time;
    if (t == null || String.IsNullOrWhiteSpace(t.Id) || !ids.Add(t.Id) || String.IsNullOrWhiteSpace(t.Title) || t.Title.Length > 120 ||
     !DateTime.TryParseExact(t.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date) ||
     (!String.IsNullOrEmpty(t.Time) && (!TimeSpan.TryParseExact(t.Time, @"hh\:mm", CultureInfo.InvariantCulture, out time) || time.TotalHours >= 24)) ||
     !Categories.Contains(t.Category) || !Repeats.Contains(t.Repeat) || t.Priority < 0 || t.Priority > 2 || (t.Notes != null && t.Notes.Length > 4000)) throw new Exception("文件包含不合法的任务数据，未导入。");
    t.Notes = t.Notes ?? ""; t.Time = t.Time ?? ""; t.Series = t.Series ?? ""; t.AutoParent = t.AutoParent ?? "";
    t.GoalId=t.GoalId??"";if(t.GoalId.Length>0&&!goalIds.Contains(t.GoalId))throw new Exception("日程所属的长期任务不存在。");
   }
   return state;
  }
  public static void Save(string path, Store state) {
   Directory.CreateDirectory(Path.GetDirectoryName(path));
   string temp = path + ".tmp";
   using (var f = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None)) {
    byte[] bytes = Encoding.UTF8.GetBytes(Serialize(state)); f.Write(bytes, 0, bytes.Length); f.Flush(true);
   }
   if (File.Exists(path)) File.Replace(temp, path, path + ".bak"); else File.Move(temp, path);
  }
  public static void Complete(Store state, TaskItem task) {
   task.Done = !task.Done;task.CompletedTicks=task.Done ? DateTime.UtcNow.Ticks : 0;
   if (!task.Done) { state.Tasks.RemoveAll(x => x.AutoParent == task.Id && !x.Done); return; }
   if (task.Repeat == "不重复") return;
   if (String.IsNullOrEmpty(task.Series)) task.Series = Guid.NewGuid().ToString("N");
   string next = DateTime.ParseExact(task.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture).AddDays(task.Repeat == "每天" ? 1 : 7).ToString("yyyy-MM-dd");
   if (!state.Tasks.Any(x => x.Series == task.Series && x.Date == next)) state.Tasks.Add(new TaskItem { Title = task.Title, Notes = task.Notes, Date = next, Time = task.Time, Category = task.Category, Priority = task.Priority, Repeat = task.Repeat, Series = task.Series, AutoParent = task.Id, Reminder = task.Reminder, IsExample = task.IsExample,GoalId=task.GoalId });
  }
  public static Store Examples(DateTime today) {
   string day = today.ToString("yyyy-MM-dd");
   return new Store { Tasks = new List<TaskItem> {
    new TaskItem { Title="示例：安排今天最重要的一件事", Date=day, Notes="这是一条示例任务。点击正文可以编辑；点击左侧方框完成，看看大肥鱼会怎样回应你～", Category="生活", Priority=1, IsExample=true }
   }};
  }
  public static bool ReduceExamples(Store state) {
   var examples=state.Tasks.Where(t=>t.IsExample).ToList();if(examples.Count<=1)return false;
   var keep=examples.OrderBy(t=>t.Done).ThenBy(t=>t.Date==DateTime.Today.ToString("yyyy-MM-dd") ? 0 : 1).First();
   state.Tasks.RemoveAll(t=>t.IsExample&&t.Id!=keep.Id);return true;
  }
 }
 public class Program {
  [STAThread] public static int Main(string[] args) {
   Mutex mutex = null; bool ownsMutex = false;
   try {
    if (args.Contains("--self-test")) { SelfTest.Run(); return 0; }
    bool screenshot = args.Length >= 2 && args[0] == "--screenshot";
    if (!screenshot) {
     string identity;
     using (var hash = SHA256.Create()) identity = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(AppDomain.CurrentDomain.BaseDirectory.ToUpperInvariant()))).Replace("-", "").Substring(0, 24);
     mutex = new Mutex(false, "Local\\Shiri-" + identity);
     try { ownsMutex = mutex.WaitOne(0); } catch (AbandonedMutexException) { ownsMutex = true; }
     if (!ownsMutex) { MessageBox.Show("拾日已在运行，请从任务栏打开现有窗口。", "拾日"); return 0; }
    }
    var app = new Application(); app.ShutdownMode = ShutdownMode.OnMainWindowClose;
    string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "tasks.json");
    var controller = new Controller(screenshot ? null : path);
    app.DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e) { MessageBox.Show("操作未完成：" + e.Exception.Message, "拾日", MessageBoxButton.OK, MessageBoxImage.Warning); e.Handled = true; };
    if (screenshot) {
     controller.Window.ShowInTaskbar = false; controller.Window.Left = -3000; controller.Window.Top = -3000; controller.Window.WindowStartupLocation = WindowStartupLocation.Manual;
     controller.Window.Loaded += delegate {
      controller.Window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(delegate {
       controller.Window.UpdateLayout();
       var content = (FrameworkElement)controller.Window.Content;
       var target = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32);
       var visual = new DrawingVisual(); using (var dc = visual.RenderOpen()) { dc.DrawRectangle(controller.Window.Background, null, new Rect(0, 0, content.ActualWidth, content.ActualHeight)); dc.DrawRectangle(new VisualBrush(content){ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(0,0,content.ActualWidth,content.ActualHeight),Stretch=Stretch.Fill}, null, new Rect(0, 0, content.ActualWidth, content.ActualHeight)); }
       target.Render(visual); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(target));
       using (var output = File.Create(Path.GetFullPath(args[1]))) encoder.Save(output);
       controller.Window.Close();
      }));
     };
    }
    app.Run(controller.Window); return 0;
   } catch (Exception e) {
    if (args.Contains("--self-test") || args.Contains("--screenshot")) { Console.Error.WriteLine(e.ToString()); return 1; }
    MessageBox.Show("拾日无法启动。\n\n" + e.Message + "\n\n原有数据不会被覆盖。若为存储权限问题，请把整个文件夹移到可写的位置。", "拾日", MessageBoxButton.OK, MessageBoxImage.Error); return 1;
   } finally { if (ownsMutex) mutex.ReleaseMutex(); if (mutex != null) mutex.Dispose(); }
  }
 }
 public partial class Controller {
  public Window Window;
  Store state;
  string path;
  string undoJson;
  string view = "today";
  DateTime selected = DateTime.Today, month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), weekStart;
  bool prioritySort, dateNavigation;
  PetView pet;
  int petClicks;
  PetMessage currentPetMessage;
  string petContextKey;
  Forms.NotifyIcon tray;
  DispatcherTimer clock;
  DateTime lastReminderCheck = DateTime.MinValue;
  DateTime knownToday = DateTime.Today;
  Brush green = B("#356A51"), muted = B("#94A08A");
  T C<T>(string name) where T : class { return Window.FindName(name) as T; }
  public static Brush B(string color) { return (Brush)new BrushConverter().ConvertFromString(color); }
  public static TextBlock Text(string value, double size, string color) { return new TextBlock { Text = value, FontSize = size, Foreground = B(color), VerticalAlignment = VerticalAlignment.Center }; }
  public static Button Btn(string value, RoutedEventHandler click) { var b = new Button { Content = value }; b.Click += click; return b; }
  public Controller(string storage) {
   path = storage;
   bool fresh = path == null || !File.Exists(path);
   state = fresh ? Data.Examples(DateTime.Today) : Data.Parse(File.ReadAllText(path, Encoding.UTF8));
   if(!fresh&&Data.ReduceExamples(state))Data.Save(path,state);
   if (fresh && path != null) Data.Save(path, state);
   using (Stream xaml = Assembly.GetExecutingAssembly().GetManifestResourceStream("Main.xaml")) Window = (Window)XamlReader.Load(xaml);
   InitializeFocus(storage);
   pet=new PetView(Window);
   C<ContentControl>("PetHost").Content=pet;
   C<Border>("SearchContainer").MouseLeftButtonDown+=delegate {C<TextBox>("SearchBox").Focus();};
   pet.Interacted+=delegate {PetTalk(true);};
   pet.ExpressionPreviewed+=delegate(object sender,PetExpressionEventArgs e){PresentPetMessage(PetMessage.ForPreview(e.Expression),e.Animate);};
   C<Button>("PetMotionButton").Click+=delegate {
    pet.Paused=!pet.Paused;C<Button>("PetMotionButton").Content=pet.Paused ? "▷" : "Ⅱ";
    C<Button>("PetMotionButton").ToolTip=pet.Paused ? "继续宠物动画" : "暂停宠物动画";
    C<TextBlock>("PetHint").Text=pet.Paused ? "动画已暂停，仍可点击互动" : "点击互动 · 右键挑选小表情";
   };
   InitializeSidebar();InitializeCountdown();InitializeDiary();InitializeFloral();
   weekStart = Monday(selected);
   C<Button>("AddButton").Click += delegate {if(view=="courses"){view="today";selected=DateTime.Today;Render();} Edit(null); };
   C<Button>("EmptyAddButton").Click += delegate { Edit(null); };
   C<Button>("TodayButton").Click += delegate {if(view=="diary"){ChooseDate(DateTime.Today);return;}if(view=="goal"){view="all";Render();return;} selected = DateTime.Today; month = new DateTime(selected.Year, selected.Month, 1); calendarYear=selected.Year; weekStart = Monday(selected); view = "today"; Render(); };
   C<Button>("PreviousWeek").Click += delegate { weekStart = weekStart.AddDays(-7); RenderDates(); };
   C<Button>("NextWeek").Click += delegate { weekStart = weekStart.AddDays(7); RenderDates(); };
   C<Button>("PreviousMonth").Click += delegate { month = month.AddMonths(-1); RenderDates(); };
   C<Button>("NextMonth").Click += delegate { month = month.AddMonths(1); RenderDates(); };
   C<TextBox>("SearchBox").TextChanged += delegate { C<TextBlock>("SearchPlaceholder").Visibility = String.IsNullOrEmpty(C<TextBox>("SearchBox").Text) ? Visibility.Visible : Visibility.Collapsed; RenderTasks(); };
   C<Button>("SortButton").Click += delegate { prioritySort = !prioritySort; C<Button>("SortButton").Content = prioritySort ? "↕  优先级排序" : "↕  时间排序"; RenderTasks(); };
   C<Button>("BackupButton").Click += delegate { BackupMenu(); };
   Window.PreviewKeyDown += delegate(object s, KeyEventArgs e) {
    HandleFocusKey(e);if(e.Handled)return;
    if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) {
     if (e.Key == Key.N) { Edit(null); e.Handled = true; }
     if (e.Key == Key.S && view=="diary") { if(floralView!=null){var pendingSave=floralView.Flush();}else SaveDiary(); e.Handled=true; }
     if (e.Key == Key.F) { if(view=="diary"){if(FloralLegacyChecks)diaryEditor.Focus();else FocusFloral();}else C<TextBox>("SearchBox").Focus(); e.Handled = true; }
     if (e.Key == Key.Z && !(Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase)) { Undo(); e.Handled = true; }
    }
    if (e.Key == Key.Escape) { C<TextBox>("SearchBox").Clear(); Keyboard.ClearFocus(); }
   };
   if (path != null) {
    tray = new Forms.NotifyIcon { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location), Text = "拾日 · 日程与待办", Visible = true };
    var menu = new Forms.ContextMenuStrip(); menu.Items.Add("打开拾日", null, delegate { Window.Show(); Window.WindowState = WindowState.Normal; Window.Activate(); }); menu.Items.Add("退出", null, delegate { Window.Close(); }); tray.ContextMenuStrip = menu;
    tray.DoubleClick += delegate { Window.Show(); Window.WindowState = WindowState.Normal; Window.Activate(); };
   }
   Window.Closed += delegate { if (clock != null) clock.Stop(); if(pet!=null)pet.Dispose(); if (tray != null) { tray.Visible = false; tray.Dispose(); } };
   clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) }; clock.Tick += delegate { Tick(); }; clock.Start();
   Render();
   if (fresh) C<TextBlock>("FooterMessage").Text = "已放入示例任务，可在「数据备份与恢复」中一键清除。";
  }
  static DateTime Monday(DateTime d) { return d.AddDays(-((int)d.DayOfWeek + 6) % 7).Date; }
  string Day(DateTime d) { return d.ToString("yyyy-MM-dd"); }
  async void ChooseDate(DateTime d) { if(view=="diary"&&floralView!=null&&selected!=d.Date&&!await floralView.Flush())return; string next=view=="diary" ? "diary" : d.Date==DateTime.Today ? "today" : "date";if(selected==d.Date&&view==next)return;selected=d.Date;weekStart=Monday(d);month=new DateTime(d.Year,d.Month,1);calendarYear=d.Year;view=next;dateNavigation=true;try{Render();}finally{dateNavigation=false;} }
  string CatColor(string cat) { return cat == "工作" ? "#739582" : cat == "生活" ? "#C3A271" : cat == "学习" ? "#8C91B4" : "#9EAC72"; }
  bool Mutate(Action change, string message) {
   string previous = Data.Serialize(state);
   try { change(); if (path != null) Data.Save(path, state); undoJson = previous; Render(); C<TextBlock>("FooterMessage").Text = message + "   ·   Ctrl + Z 撤销"; return true; }
   catch (Exception e) { state = Data.Parse(previous); Render(); MessageBox.Show(Window, "保存失败，本次修改已还原。\n" + e.Message, "拾日", MessageBoxButton.OK, MessageBoxImage.Warning); return false; }
  }
  void Undo() {
   if (undoJson == null) return;
   Store previous = Data.Parse(undoJson);
   try { if (path != null) Data.Save(path, previous); state = previous; undoJson = null; Render(); C<TextBlock>("FooterMessage").Text = "已撤销上一次操作。"; }
   catch (Exception e) { MessageBox.Show(Window, "撤销未能保存：" + e.Message, "拾日"); }
  }
  void Render() { C<TextBlock>("FooterHint").Visibility=view=="diary"?Visibility.Collapsed:Visibility.Visible;
   RenderNavigation(); RenderDates(); RenderTasks(); if(!dateNavigation){RenderSummary();RenderFocus();}
   C<TextBlock>("TodayLabel").Text = DateTime.Today.ToString("yyyy 年 M 月 d 日   ·   dddd", CultureInfo.GetCultureInfo("zh-CN"));
   string title = view == "today" ? "我的一天" : view == "date" ? selected.ToString("M 月 d 日") : view == "upcoming" ? "未来七天" : view == "all" ? "远方计划" : view == "important" ? "重要事项" : view == "completed" ? "已完成" : view + "清单";
   C<TextBlock>("ViewTitle").Text = title;
   C<TextBlock>("ViewTag").Text = view == "today" ? "TODAY" : view == "upcoming" ? "UPCOMING" : view == "completed" ? "DONE" : view == "date" ? selected.ToString("dddd", CultureInfo.GetCultureInfo("zh-CN")) : "MY TASKS";
   C<TextBlock>("ViewSubtitle").Text = view == "today" ? "把重要的事，放进今天。" : view == "upcoming" ? "提前安排，从容迎接接下来的每一天。" : view == "completed" ? "每一个完成，都是认真生活的印记。" : "给每件事一个位置，也给自己一点余地。";
   RenderGoalHeader();if(view=="courses"){C<TextBlock>("ViewTitle").Text="课程表";C<TextBlock>("ViewTag").Text="TIMETABLE";C<Button>("AddButton").Content="＋    新建";C<TextBlock>("ViewSubtitle").Text="一周的安排，从容看见。";}if(view=="diary"){C<TextBlock>("ViewTitle").Text="日记与便签";C<TextBlock>("ViewTag").Text="JOURNAL";C<TextBlock>("ViewSubtitle").Text="按日期收藏文字、照片与灵感。";C<Button>("AddButton").Content="✎    写今日日记";}C<Button>("AddButton").Visibility=Visibility.Visible;ApplySidebarLayout();foreach(string name in new[]{"BrandEnglish","BrandTagline","WorkspaceLabel"})C<TextBlock>(name).Visibility=view=="courses"?Visibility.Collapsed:Visibility.Visible;C<TextBlock>("StorageStatus").Visibility=view=="courses"?Visibility.Collapsed:Visibility.Visible;C<Grid>("PageTitleHeader").Visibility=view=="courses"?Visibility.Collapsed:Visibility.Visible;C<Border>("CountdownCard").Visibility=view=="courses"?Visibility.Collapsed:Visibility.Visible;C<Border>("FocusStatsCard").Visibility=view=="courses"?Visibility.Collapsed:Visibility.Visible;C<StackPanel>("CompactSidebarTools").Visibility=view=="courses"?Visibility.Visible:Visibility.Collapsed;C<Border>("PageFooter").Background=view=="courses"?Brushes.Transparent:B("#EEF2E9");C<TextBlock>("FooterHint").Visibility=view=="courses"?Visibility.Collapsed:Visibility.Visible;C<Button>("TodayButton").Visibility=view=="courses"?Visibility.Collapsed:Visibility.Visible;C<TextBlock>("TodayLabel").Visibility=view=="courses"?Visibility.Collapsed:Visibility.Visible;C<TextBlock>("ViewSubtitle").Visibility=view=="courses"?Visibility.Collapsed:Visibility.Visible;C<Border>("SearchContainer").Visibility=view=="diary"||view=="courses" ? Visibility.Collapsed : Visibility.Visible;UpdatePetPage();AnimatePageIfChanged();
  }
  void RenderNavigation() {
   var nav = C<StackPanel>("Navigation"); nav.Children.Clear();
   string[] keys = { "today", "all", "diary", "courses" };
   string[] labels = { "☀   我的一天", "☷   远方计划", "✎   日记", "▦   课程表" };
   for (int i = 0; i < keys.Length; i++) {
    string key = keys[i]; int count = key=="courses" ? state.Courses.Count : key=="diary" ? (floralStore==null?diary.Entries.Count:floralStore.Data.notes.Count(x=>!x.deleted)) : key=="all" ? state.Goals.Count(x=>!x.Done) : Filter(key).Count(x => key == "completed" || !x.Done);
    var b = Btn("", delegate { view = key; if (key == "today") { selected = DateTime.Today; weekStart = Monday(selected); } Render(); });
    b.HorizontalContentAlignment = HorizontalAlignment.Stretch; b.Padding = new Thickness(13, 12, 13, 12); b.Margin = new Thickness(0, 2, 0, 2);
    b.Background = view == key||(view=="goal"&&key=="all") ? B("#DFE9D8") : Brushes.Transparent;
    var grid = new Grid(); grid.Children.Add(Text(labels[i], 12, view == key ? "#355C41" : "#7C8876")); var n = Text(count.ToString(), 10, "#8B987E"); n.HorizontalAlignment = HorizontalAlignment.Right; grid.Children.Add(n); b.Content = grid; nav.Children.Add(b);
   }
  }
  void RenderDates() { UpdateDateTiles(); }
  IEnumerable<TaskItem> Filter(string key) {
   if (key == "today") return state.Tasks.Where(t => t.Date == Day(DateTime.Today));
   if (key == "date") return state.Tasks.Where(t => t.Date == Day(selected));
   if (key == "upcoming") return state.Tasks.Where(t => String.CompareOrdinal(t.Date, Day(DateTime.Today)) >= 0 && String.CompareOrdinal(t.Date, Day(DateTime.Today.AddDays(7))) < 0);
   if (key == "important") return state.Tasks.Where(t => t.Priority == 2);
   if (key == "completed") return state.Tasks.Where(t => t.Done);
   if (Data.Categories.Contains(key)) return state.Tasks.Where(t => t.Category == key);
   return state.Tasks;
  }
  void RenderTasks() { C<Border>("WeekCard").Visibility=view=="diary"?Visibility.Collapsed:Visibility.Visible;C<Grid>("ListToolbar").Visibility=view=="diary"?Visibility.Collapsed:Visibility.Visible;
   if(view=="courses"){RenderTimetable();return;}if(view=="diary"){RenderDiary();return;}
   C<ContentControl>("DiaryHost").Visibility=Visibility.Collapsed;C<ScrollViewer>("TaskScroll").Visibility=Visibility.Visible;C<Button>("SortButton").Visibility=Visibility.Visible;
   if(view=="all"||view=="goal"){RenderGoals();return;}
   C<Button>("SortButton").Content=prioritySort ? "↕  优先级排序" : "↕  时间排序";
   string q = C<TextBox>("SearchBox").Text.Trim();
   var tasks = Filter(view).Where(t => q.Length == 0 || t.Title.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0 || t.Notes.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0);
   var sorted = prioritySort ? tasks.OrderByDescending(t => t.Priority).ThenBy(t => t.Date).ThenBy(t => String.IsNullOrEmpty(t.Time) ? "00:00" : t.Time) : tasks.OrderBy(t => t.Date).ThenBy(t => String.IsNullOrEmpty(t.Time) ? "00:00" : t.Time).ThenByDescending(t => t.Priority);
   var active = sorted.Where(t => !t.Done).ToList(); var done = sorted.Where(t => t.Done).ToList();
   var list = C<StackPanel>("TaskList"); list.Children.Clear(); var completed = C<StackPanel>("CompletedList"); completed.Children.Clear();
   foreach (var task in active) list.Children.Add(TaskCard(task));
   foreach (var task in done) completed.Children.Add(TaskCard(task));
   C<StackPanel>("CompletedSection").Visibility = done.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
   C<TextBlock>("CompletedHeading").Text = "已完成  ·  " + done.Count;
   C<StackPanel>("EmptyState").Visibility = active.Count + done.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
   C<TextBlock>("EmptyTitle").Text = q.Length > 0 ? "没有找到匹配的任务" : view == "completed" ? "完成的每一步，都会留在这里" : "给今天留一点可能";
   C<TextBlock>("ListHeading").Text = view == "completed" ? "完成记录" : view == "today" ? "今日任务" : view == "date" ? "当天任务" : "任务清单";
   C<TextBlock>("ListCount").Text = (active.Count + done.Count).ToString();
  }
  Border TaskCard(TaskItem t) {
   bool overdue = !t.Done && String.CompareOrdinal(t.Date, Day(DateTime.Today)) < 0;
   var card = new Border { Background = t.Done ? B("#F1F4EE") : Brushes.White, BorderBrush = B("#E6EAE0"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(11), Padding = new Thickness(13, 10, 12, 10), Margin = new Thickness(0, 0, 0, 10) };
   var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(29) }); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) }); card.Child = grid;
   var check = Btn(t.Done ? "✓" : "", delegate { Mutate(delegate { Data.Complete(state, t); }, t.Done ? "已恢复任务" : (t.Repeat == "不重复" ? "又完成了一件事。" : "已完成，并创建下一次任务。")); });
   check.Width = 19; check.Height = 19; check.Padding = new Thickness(0); check.VerticalAlignment = VerticalAlignment.Top; check.HorizontalAlignment = HorizontalAlignment.Left; check.Margin = new Thickness(0, 2, 0, 0); check.Background = t.Done ? B("#B3C4A2") : Brushes.White; check.Foreground = Brushes.White; check.ToolTip = t.Done ? "恢复为未完成" : "标记完成";
   var rim = new Border { BorderBrush = t.Done ? Brushes.Transparent : B("#CFD9C7"), BorderThickness = new Thickness(1.3), CornerRadius = new CornerRadius(6), Width = 21, Height = 21, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left, Child = check }; grid.Children.Add(rim);
   var edit = Btn("", delegate { Edit(t); }); edit.Padding = new Thickness(0); edit.HorizontalContentAlignment = HorizontalAlignment.Stretch; edit.ToolTip = "点击编辑任务"; Grid.SetColumn(edit, 1); grid.Children.Add(edit);
   var body = new StackPanel(); var title = Text(t.Title, 13, t.Done ? "#9DA994" : "#3B5140"); title.TextWrapping = TextWrapping.Wrap; if (t.Done) title.TextDecorations = TextDecorations.Strikethrough; body.Children.Add(title);
   if(!String.IsNullOrWhiteSpace(t.Notes))edit.ToolTip=t.Notes;
   var meta = new WrapPanel { Margin = new Thickness(0, 5, 0, 0) }; string when = t.Time.Length == 0 ? "全天" : t.Time;
   if (view != "today" || overdue) when = DateTime.ParseExact(t.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("M月d日") + "  " + when;
   meta.Children.Add(Text((overdue ? "逾期 · " : "◷  ") + when, 10, overdue ? "#BD876A" : "#9AA68F"));
   if (t.Repeat != "不重复") { var repeat = Text("↻ " + t.Repeat, 10, "#9AA68F"); repeat.Margin = new Thickness(10, 0, 0, 0); meta.Children.Add(repeat); }
   if (t.Reminder) { var reminder = Text("♧ 提醒", 10, "#9AA68F"); reminder.Margin = new Thickness(10, 0, 0, 0); meta.Children.Add(reminder); }
   var goal=state.Goals.FirstOrDefault(x=>x.Id==t.GoalId);if(goal!=null){var belongs=Text("↻ "+goal.Title,10,goal.Color);belongs.Margin=new Thickness(10,0,0,0);meta.Children.Add(belongs);}
   body.Children.Add(meta); edit.Content = body;
   var star = Btn(t.Priority == 2 ? "★" : "☆", delegate { Mutate(delegate { t.Priority = t.Priority == 2 ? 1 : 2; }, t.Priority == 2 ? "已取消重要标记" : "已标为重要事项"); }); star.Padding = new Thickness(0); star.FontSize = 19; star.Foreground = t.Priority == 2 ? B("#C4A55D") : B("#CED6C5"); star.VerticalAlignment = VerticalAlignment.Top; star.ToolTip = "切换重要事项"; Grid.SetColumn(star, 2); grid.Children.Add(star);
   var context = new ContextMenu(); var editItem = new MenuItem { Header = "编辑任务" }; editItem.Click += delegate { Edit(t); }; context.Items.Add(editItem); var deleteItem = new MenuItem { Header = "删除任务（可撤销）" }; deleteItem.Click += delegate { Mutate(delegate { state.Tasks.Remove(t); }, "已删除任务"); }; context.Items.Add(deleteItem); card.ContextMenu = context;
   var startFocus=new MenuItem{Header="为此开始专注"};startFocus.Click+=delegate{BeginTaskFocus(t);};context.Items.Add(startFocus);
   return card;
  }
  void RenderSummary() {
   var today = state.Tasks.Where(t => t.Date == Day(DateTime.Today)).ToList(); int done = today.Count(t => t.Done); int total = today.Count;
   var nextPetTask=today.Where(t=>!t.Done).OrderByDescending(t=>t.Priority).ThenBy(t=>t.Time).FirstOrDefault();
   string contextKey=Day(DateTime.Today)+":"+total+":"+done+":"+(nextPetTask==null ? "" : nextPetTask.Id+nextPetTask.Title);
   if(contextKey!=petContextKey) {
    petContextKey=contextKey;PetTalk(false);
   }
   C<TextBlock>("ProgressPercent").Text = total == 0 ? "0%" : ((int)(100.0 * done / total)).ToString() + "%";
   C<ProgressBar>("DayProgress").Value = total == 0 ? 0 : 100.0 * done / total;
   C<TextBlock>("ProgressDescription").Text = total == 0 ? "从一件小事开始，慢慢来。" : "已完成 " + done + " / " + total + " 件事" + (done == total ? "，今天很棒。" : "，继续保持自己的节奏。");
   var list = C<StackPanel>("UpcomingList"); list.Children.Clear();
   DateTime now = DateTime.Now;
   var next = state.Tasks.Where(t => !t.Done && String.CompareOrdinal(t.Date, Day(now)) >= 0 && (t.Date != Day(now) || t.Time.Length == 0 || String.CompareOrdinal(t.Time, now.ToString("HH:mm")) >= 0)).OrderBy(t => t.Date).ThenBy(t => t.Time).Take(3).ToList();
   if (next.Count == 0) { list.Children.Add(Text("暂时没有安排，享受一点留白。", 10, "#A1AA96")); return; }
   foreach (var t in next) {
    var b = Btn("", delegate { Edit(t); }); b.Padding = new Thickness(0, 9, 0, 9); b.HorizontalContentAlignment = HorizontalAlignment.Stretch;
    var g = new Grid(); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) }); g.ColumnDefinitions.Add(new ColumnDefinition());
    g.Children.Add(new Border { Background = B(CatColor(t.Category)), CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 1, 0, 1) });
    var p = new StackPanel { Margin = new Thickness(11, 0, 0, 0) }; Grid.SetColumn(p, 1); var title = Text(t.Title, 11, "#6E7D63"); title.TextTrimming = TextTrimming.CharacterEllipsis; p.Children.Add(title); p.Children.Add(new TextBlock { Text = (t.Date == Day(now) ? "今天" : DateTime.ParseExact(t.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("M月d日")) + "  ·  " + (t.Time.Length == 0 ? "全天" : t.Time), FontSize = 9, Foreground = muted, Margin = new Thickness(0, 6, 0, 0) }); g.Children.Add(p); b.Content = g; list.Children.Add(b);
   }
  }
  void PetTalk(bool clicked) {
   if(clicked)petClicks++;
   var today=state.Tasks.Where(t=>t.Date==Day(DateTime.Today)).ToList();
   var next=today.Where(t=>!t.Done).OrderByDescending(t=>t.Priority).ThenBy(t=>t.Time).FirstOrDefault();
   int done=today.Count(t=>t.Done);string title=next==null ? null : next.Title;
   var message=PagePetMessage(today.Count,done,title,clicked ? petClicks : 0,clicked ? pet.InteractionStreak : 0);
   PresentPetMessage(message,clicked&&pet.LastInteractionWasPointer);
  }
  void ShowPetMessage(PetMessage message) {
   PresentPetMessage(message,false);
  }
  void PresentPetMessage(PetMessage message,bool animate) {
   // A phrase and its expression are applied together. Redrawing a list,
   // refreshing the clock, or showing the same phrase cannot change the face.
   if(currentPetMessage!=null&&currentPetMessage.Text==message.Text){if(!animate)pet.React(currentPetMessage.Expression,false);return;}
   currentPetMessage=message;
   C<TextBlock>("PetSpeech").Text=message.Text;pet.Motion.Mood=message.Mood;pet.React(message.Expression,animate);
  }
  void Edit(TaskItem original) { if(view=="courses"){EditCourse(null);return;}
   if(original==null&&view=="diary"){ChooseDate(DateTime.Today);if(FloralLegacyChecks)diaryEditor.Focus();else FocusFloral();return;}
   if(original==null&&view=="all"){EditGoal(null);return;}
   var dialog = new TaskDialog(Window, original, view == "today"||view=="goal" ? DateTime.Today : selected, Data.Categories.Contains(view) ? view : "工作",state.Goals,view=="goal" ? selectedGoalId : "");
   if (dialog.Window.ShowDialog() != true) return;
   if (dialog.Delete) { Mutate(delegate { state.Tasks.Remove(original); }, "已删除任务"); return; }
   TaskItem result = dialog.Result;
   Mutate(delegate { if (original != null) state.Tasks[state.Tasks.IndexOf(original)] = result; else state.Tasks.Add(result); }, original == null ? "任务已添加。" : "任务已更新。");
  }
  void BackupMenu() {
   var menu = new ContextMenu();
   var export = new MenuItem { Header = "导出日程与计划 JSON…" }; export.Click += delegate {
    var dialog = new Microsoft.Win32.SaveFileDialog { FileName = "拾日备份-" + DateTime.Now.ToString("yyyyMMdd-HHmm"), DefaultExt = ".json", Filter = "拾日备份 (*.json)|*.json" };
    if (dialog.ShowDialog(Window) == true) { File.WriteAllText(dialog.FileName, Data.Serialize(state), new UTF8Encoding(false)); C<TextBlock>("FooterMessage").Text = "备份已导出。"; }
   }; menu.Items.Add(export);
   var import = new MenuItem { Header = "从备份合并恢复…" }; import.Click += delegate {
    var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "拾日备份 (*.json)|*.json", CheckFileExists = true };
    if (dialog.ShowDialog(Window) != true) return;
    try { if (new FileInfo(dialog.FileName).Length > 10 * 1024 * 1024) throw new Exception("文件超过 10 MB。"); Store imported = Data.Parse(File.ReadAllText(dialog.FileName, Encoding.UTF8)); var ids = new HashSet<string>(state.Tasks.Select(t => t.Id)); var additions = imported.Tasks.Where(t => !ids.Contains(t.Id)).ToList();var goalIds=new HashSet<string>(state.Goals.Select(x=>x.Id));var newGoals=imported.Goals.Where(x=>!goalIds.Contains(x.Id)).ToList(); if (state.Tasks.Count + additions.Count > 20000) throw new Exception("合并后的任务数量超过 20,000 条。"); Mutate(delegate {state.Goals.AddRange(newGoals); state.Tasks.AddRange(additions); }, "已合并 " + additions.Count + " 条日程与 "+newGoals.Count+" 个长期任务；相同编号的现有任务予以保留。"); }
    catch (Exception e) { MessageBox.Show(Window, "未导入：" + e.Message, "拾日", MessageBoxButton.OK, MessageBoxImage.Warning); }
   }; menu.Items.Add(import);
   var clear = new MenuItem { Header = "清除示例任务（可撤销）", IsEnabled = state.Tasks.Any(t => t.IsExample) }; clear.Click += delegate { Mutate(delegate { state.Tasks.RemoveAll(t => t.IsExample); }, "已清除示例任务"); }; menu.Items.Add(clear);
   menu.Items.Add(new Separator()); var location = new MenuItem { Header = "查看数据保存位置" }; location.Click += delegate { MessageBox.Show(Window, path == null ? "预览模式，不写入数据。" : path + "\n\n关闭程序后，可复制整个 data 文件夹进行备份。\ntasks.json.bak 是上一次保存的数据。", "数据保存在本机"); }; menu.Items.Add(location);
   var about = new MenuItem { Header = "关于拾日 / 提醒说明" }; about.Click += delegate { MessageBox.Show(Window, "拾日 2.0.0\n让每一天，井然有序。\n\n大肥鱼：点击宠物查看今日任务回应，右上角可暂停动画。\nCtrl + N 新建任务 · Ctrl + F 搜索\nCtrl + Z 撤销上一次任务操作（焦点在输入框外）\n右键任务可编辑或删除；星标表示重要。\n\n提醒仅在程序运行时生效，最小化后仍可提醒。关闭窗口会退出程序。系统通知被关闭时，通知可能不显示。\n\n重复任务在本次标记完成后生成下一次。全天任务不发送定时提醒。恢复备份采用合并方式，相同编号保留当前数据。", "关于拾日"); }; menu.Items.Add(about);
   menu.PlacementTarget = C<Button>("AppSettings"); menu.IsOpen = true;
  }
  void Tick() {
   RenderCountdown();
   TickFocus();
   if (DateTime.Today != knownToday) { bool follow = selected == knownToday; knownToday = DateTime.Today; if (follow) { selected = knownToday; weekStart = Monday(selected); month = new DateTime(selected.Year, selected.Month, 1); } Render(); }
   if ((DateTime.Now - lastReminderCheck).TotalSeconds < 15) return;
   lastReminderCheck = DateTime.Now; RenderSummary(); if (path == null) return;
   var due = state.Tasks.Where(t => !t.Done && t.Reminder && !t.Reminded && t.Time.Length > 0).Where(t => { DateTime at = DateTime.ParseExact(t.Date + " " + t.Time, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture); return at <= DateTime.Now && at >= DateTime.Now.AddHours(-24); }).ToList();
   if (due.Count == 0) return;
   foreach (var t in due) t.Reminded = true;
   try { Data.Save(path, state); } catch { foreach (var t in due) t.Reminded = false; return; }
   if (due.Count == 1) Notify("日程提醒 · " + due[0].Time, due[0].Title); else Notify("有 " + due.Count + " 项日程待处理", String.Join("；", due.Take(3).Select(t => t.Title).ToArray()));
  }
  void Notify(string title, string text) { if (tray != null) tray.ShowBalloonTip(8000, title, text.Length > 200 ? text.Substring(0, 200) : text, Forms.ToolTipIcon.Info); C<TextBlock>("FooterMessage").Text = title + " · " + text; if(pet!=null)ShowPetMessage(PetMessage.ForNotice(title,text)); }
 }

 public class TaskDialog {
  public Window Window;
  public TaskItem Result;
  public bool Delete;
  TextBox title, time, notes;
  DatePicker date;
  ComboBox priority, repeat;
  ComboBox goalChoice;
  CheckBox reminder;
  TextBlock error;
  public TaskDialog(Window owner, TaskItem original, DateTime chosen, string chosenCategory,IEnumerable<GoalItem> goals=null,string chosenGoal="") {
   Window = new Window { Owner = owner, Title = original == null ? "新建任务 · 拾日" : "编辑任务 · 拾日", Width = 540, Height = 740, MaxHeight = Math.Max(500, SystemParameters.WorkArea.Height - 32), ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Controller.B("#F8F9F5"), FontFamily = owner.FontFamily, FontSize = 13, Foreground = Controller.B("#3B5140") };
   Window.Resources = owner.Resources;
   var root = new DockPanel { Margin = new Thickness(28) }; Window.Content = root;
   var actions = new Grid { Margin = new Thickness(0, 15, 0, 0) }; DockPanel.SetDock(actions, Dock.Bottom); root.Children.Add(actions);
   if (original != null) { var delete = Controller.Btn("删除任务", delegate { Delete = true; Window.DialogResult = true; }); delete.Foreground = Controller.B("#AF7960"); delete.HorizontalAlignment = HorizontalAlignment.Left; actions.Children.Add(delete); }
   var save = Controller.Btn("保存任务", delegate { Save(original); }); save.Background = Controller.B("#356A51"); save.Foreground = Brushes.White; save.HorizontalAlignment = HorizontalAlignment.Right; save.IsDefault = true; actions.Children.Add(save);
   var cancel = Controller.Btn("取消", delegate { Window.DialogResult = false; }); cancel.HorizontalAlignment = HorizontalAlignment.Right; cancel.Margin = new Thickness(0, 0, 105, 0); cancel.IsCancel = true; actions.Children.Add(cancel);
   var content = new StackPanel { Margin = new Thickness(0, 0, 8, 0) }; root.Children.Add(new ScrollViewer { Content = content }); content.Children.Add(new TextBlock { Text = original == null ? "安排一件小事" : "让计划更清晰", FontSize = 24, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) }); content.Children.Add(Controller.Text("具体一点，就更容易开始。", 11, "#9AA68F"));
   Label(content, "任务名称"); title = new TextBox { MaxLength = 120, Height = 40, Text = original == null ? "" : original.Title }; content.Children.Add(title);
   var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) }); row.ColumnDefinitions.Add(new ColumnDefinition()); content.Children.Add(row);
   var left = new StackPanel(); row.Children.Add(left); var right = new StackPanel(); Grid.SetColumn(right, 2); row.Children.Add(right);
   Label(left, "日期"); date = new DatePicker { SelectedDate = original == null ? chosen : DateTime.ParseExact(original.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture), Height = 38, VerticalContentAlignment = VerticalAlignment.Center, Language = System.Windows.Markup.XmlLanguage.GetLanguage("zh-CN") }; left.Children.Add(date);
   Label(right, "时间（留空为全天）"); time = new TextBox { Text = original == null ? "" : original.Time, Height = 40, MaxLength = 5, ToolTip = "24 小时制，例如 09:30" }; right.Children.Add(time);
   Label(content, "优先级"); priority = Combo(new[] { "低 · 慢慢来", "普通 · 按计划", "高 · 重要事项" }, null); priority.SelectedIndex = original == null ? 1 : original.Priority; content.Children.Add(priority);
   Label(content, "重复"); repeat = Combo(Data.Repeats, original == null ? "不重复" : original.Repeat); content.Children.Add(repeat);
   Label(content,"所属长期任务（可选）");var options=new List<GoalChoice>{new GoalChoice{Id="",Title="不归类到长期任务"}};if(goals!=null)options.AddRange(goals.Select(x=>new GoalChoice{Id=x.Id,Title=x.Title}));goalChoice=new ComboBox{ItemsSource=options,SelectedItem=options.FirstOrDefault(x=>x.Id==(original==null ? chosenGoal : original.GoalId))??options[0]};content.Children.Add(goalChoice);
   Label(content, "备注"); notes = new TextBox { Text = original == null ? "" : original.Notes, MaxLength = 4000, Height = 70, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, VerticalContentAlignment = VerticalAlignment.Top, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; content.Children.Add(notes);
   reminder = new CheckBox { Content = "到点提醒（程序运行时有效，需填写时间）", IsChecked = original != null && original.Reminder, FontSize = 11, Foreground = Controller.B("#88977B"), Margin = new Thickness(0, 17, 0, 0) }; content.Children.Add(reminder);
   error = Controller.Text("", 11, "#B36E53"); error.TextWrapping = TextWrapping.Wrap; error.Margin = new Thickness(0, 12, 0, 0); content.Children.Add(error);
   Window.Loaded += delegate { title.Focus(); title.CaretIndex = title.Text.Length; };
  }
  void Label(StackPanel panel, string value) { var label = Controller.Text(value, 11, "#7E8A72"); label.Margin = new Thickness(0, 17, 0, 8); panel.Children.Add(label); }
  ComboBox Combo(string[] values, string selected) { var c = new ComboBox { ItemsSource = values, SelectedIndex = selected == null ? 0 : Array.IndexOf(values, selected) }; return c; }
  void Save(TaskItem original) {
   if (String.IsNullOrWhiteSpace(title.Text)) { error.Text = "请先给这件事起个名字。"; title.Focus(); return; }
   if (!date.SelectedDate.HasValue) { error.Text = "请选择有效的日期。"; return; }
   string when = time.Text.Trim(); TimeSpan parsed;
   if (when.Length != 0 && (!TimeSpan.TryParseExact(when, @"hh\:mm", CultureInfo.InvariantCulture, out parsed) || parsed.TotalHours >= 24)) { error.Text = "时间请使用 24 小时制，例如 09:30 或 18:00。"; time.Focus(); return; }
   if (reminder.IsChecked == true && when.Length == 0) { error.Text = "到点提醒需要填写具体时间。"; time.Focus(); return; }
   bool changedSchedule = original == null || original.Date != date.SelectedDate.Value.ToString("yyyy-MM-dd") || original.Time != when || original.Reminder != (reminder.IsChecked == true);
   Result = new TaskItem { Id = original == null ? Guid.NewGuid().ToString("N") : original.Id, Title = title.Text.Trim(), Notes = notes.Text.Trim(), Date = date.SelectedDate.Value.ToString("yyyy-MM-dd"), Time = when, Category = original==null ? "工作" : original.Category, Priority = priority.SelectedIndex, Repeat = (string)repeat.SelectedItem, Reminder = reminder.IsChecked == true, Reminded = !changedSchedule && original.Reminded, Done = original != null && original.Done, Series = original == null ? "" : original.Series, IsExample = false, AutoParent = "" };
   Result.GoalId=((GoalChoice)goalChoice.SelectedItem).Id;Result.CompletedTicks=original==null ? 0 : original.CompletedTicks;
   Window.DialogResult = true;
  }
 }

 public static class SelfTest {
  static int count;
  static void Assert(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); count++; Console.WriteLine("PASS: " + name); }
  public static void Run() {
   var store = new Store(); var t = new TaskItem { Title = "中文任务 <&> \"测试\"", Date = "2026-12-31", Time = "23:59", Repeat = "每天", Reminder = true, Notes = "第一行\n第二行" }; store.Tasks.Add(t);
   string original = Data.Serialize(store); var parsed = Data.Parse(original);
   Assert(parsed.Tasks[0].Title == t.Title && parsed.Tasks[0].Notes == t.Notes, "Chinese and special characters survive JSON round trip");
   Data.Complete(store, t); Assert(t.Done && store.Tasks.Count == 2 && store.Tasks[1].Date == "2027-01-01", "Daily recurrence crosses year boundary");
   Assert(store.Tasks[1].Reminder && !store.Tasks[1].Done && !store.Tasks[1].Reminded, "Recurring reminder resets");
   Data.Complete(store, t); Assert(!t.Done && store.Tasks.Count == 1, "Reopening removes untouched generated occurrence");
   Data.Complete(store, t); Assert(store.Tasks.Count == 2, "Completing again does not duplicate occurrence");
   var weekly = new TaskItem { Title = "每周", Date = "2028-02-28", Repeat = "每周" }; store.Tasks.Add(weekly); Data.Complete(store, weekly); Assert(store.Tasks.Last().Date == "2028-03-06", "Weekly recurrence crosses leap day");
   var restored = Data.Parse(original); Assert(restored.Tasks.Count == 1 && !restored.Tasks[0].Done, "Snapshot undo restores full state including recurrence");
   foreach (string invalid in new[] { original.Replace("23:59", "24:00"), original.Replace("2026-12-31", "2026-02-30"), original.Replace("\"Version\":2", "\"Version\":99"), "{}", "{bad json}" }) {
    bool rejected = false; try { Data.Parse(invalid); } catch { rejected = true; } Assert(rejected, "Malformed or unsupported data rejected");
   }
   var dupe = Data.Parse(original); dupe.Tasks.Add(dupe.Tasks[0]); bool duplicateRejected = false; try { Data.Parse(Data.Serialize(dupe)); } catch { duplicateRejected = true; } Assert(duplicateRejected, "Duplicate task IDs rejected");
   string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "self-test-data"); string testPath = Path.Combine(testDir, "tasks.json");
   try { Data.Save(testPath, restored); restored.Tasks[0].Title = "更新后"; Data.Save(testPath, restored); Assert(Data.Parse(File.ReadAllText(testPath)).Tasks[0].Title == "更新后", "Atomic save loads updated state"); Assert(Data.Parse(File.ReadAllText(testPath + ".bak")).Tasks[0].Title == t.Title, "Previous save retained as backup"); }
   finally { if (File.Exists(testPath)) File.Delete(testPath); if (File.Exists(testPath + ".bak")) File.Delete(testPath + ".bak"); if (Directory.Exists(testDir) && Directory.GetFileSystemEntries(testDir).Length == 0) Directory.Delete(testDir); }
   Console.WriteLine("ALL " + count + " CHECKS PASSED");
  }
 }
}




