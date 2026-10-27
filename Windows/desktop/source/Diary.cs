using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Shiri {
 [DataContract]public sealed class DiaryEntry {
  [DataMember]public string Date;
  [DataMember]public string Text;
  [DataMember]public long UpdatedTicks;
  [DataMember(EmitDefaultValue=false)]public string Title;
  [DataMember(EmitDefaultValue=false)]public string Mood;
  [DataMember(EmitDefaultValue=false)]public List<DiaryParagraph> Blocks;
 }
 [DataContract]public sealed class DiaryData {
  [DataMember]public int Version=1;
  [DataMember]public List<DiaryEntry> Entries=new List<DiaryEntry>();
 }
 public static class DiaryStorage {
  public static DiaryData Parse(string json) {
   if(json.Length>32*1024*1024)throw new Exception("日记文件超过 32 MB。");
   DiaryData data;using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(json)))data=(DiaryData)new DataContractJsonSerializer(typeof(DiaryData)).ReadObject(stream);
   if(data==null||data.Version!=1||data.Entries==null||data.Entries.Count>36600)throw new Exception("日记文件格式无法识别。");
   var dates=new HashSet<string>();DateTime day;
   foreach(var entry in data.Entries){if(entry==null||!DateTime.TryParseExact(entry.Date,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out day)||!dates.Add(entry.Date)||entry.Text==null||entry.Text.Length>100000||entry.UpdatedTicks<0||entry.UpdatedTicks>DateTime.MaxValue.Ticks||(entry.Title!=null&&entry.Title.Length>80)||(entry.Mood!=null&&entry.Mood.Length>20))throw new Exception("日记文件包含无效或重复的记录。");
    if(entry.Blocks!=null){if(entry.Blocks.Count>20000)throw new Exception("日记段落过多。");int chars=0,parts=0,images=0;foreach(var block in entry.Blocks){if(block==null||block.Parts==null)throw new Exception("日记段落无效。");foreach(var part in block.Parts){if(part==null||part.Text==null||(part.Image!=null&&!DiaryDocument.ValidImage(part.Image)))throw new Exception("日记内容无效。");chars+=part.Text.Length;parts++;if(part.Image!=null)images++;}}if(chars>100000||parts>30000||images>30)throw new Exception("单篇日记最多 10 万字、30 张图片。");}
   }
   return data;
  }
  public static string Serialize(DiaryData data){using(var stream=new MemoryStream()){new DataContractJsonSerializer(typeof(DiaryData)).WriteObject(stream,data);return Encoding.UTF8.GetString(stream.ToArray());}}
  public static DiaryData Load(string path){if(path==null||!File.Exists(path))return new DiaryData();if(new FileInfo(path).Length>32*1024*1024)throw new Exception("日记文件超过 32 MB。");return Parse(File.ReadAllText(path,Encoding.UTF8));}
  public static void Save(string path,DiaryData data){if(path==null)return;byte[] bytes=Encoding.UTF8.GetBytes(Serialize(data));if(bytes.Length>32*1024*1024)throw new Exception("日记文件超过 32 MB，请先导出归档。");Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp";using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);}
 }
 public partial class Controller {
  DiaryData diary;
  string diaryPath,diaryDate,diaryLoadError;
  bool diaryDirty,loadingDiary;
  DispatcherTimer diarySaveTimer;
  RichTextBox diaryEditor;
  TextBox diaryTitle;
  ComboBox diaryMood;
  DatePicker diaryPicker;
  TextBlock diaryStatus,diaryCount,diaryHeading,diaryPlaceholder,diaryTitleHint;
  Button diaryHistory;
  // WPF native object undo replaces embedded pictures with blank controls; keep bounded model snapshots instead.
  sealed class DiarySnapshot {public List<DiaryParagraph> Blocks;public int Caret;}
  readonly List<DiarySnapshot> diaryUndo=new List<DiarySnapshot>(),diaryRedo=new List<DiarySnapshot>();
  DiarySnapshot diaryCurrent;
  DateTime diaryLastEdit=DateTime.MinValue;
  bool diaryAtomicEdit;
  readonly Dictionary<string,byte[]> diaryImageMemory=new Dictionary<string,byte[]>();
  void InitializeDiary() {
   diaryPath=path==null ? null : Path.Combine(Path.GetDirectoryName(path),"diary.json");LoadDiary();
   diarySaveTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(900)};diarySaveTimer.Tick+=delegate{diarySaveTimer.Stop();SaveDiary();};
   Window.Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e){if(!SaveDiary()){e.Cancel=true;MessageBox.Show(Window,"日记还未保存，窗口已保留。请检查存储位置后点击“保存”重试。","拾日",MessageBoxButton.OK,MessageBoxImage.Warning);}};
   Window.Closed+=delegate{diarySaveTimer.Stop();};
  }
  void LoadDiary(){try{diary=DiaryStorage.Load(diaryPath);diaryLoadError=null;}catch(Exception error){diary=new DiaryData();diaryLoadError=error.Message;}}
  bool SaveDiary() {
   if(!diaryDirty)return true;
   try{DiaryStorage.Parse(DiaryStorage.Serialize(diary));DiaryStorage.Save(diaryPath,diary);diaryDirty=false;RenderNavigation();if(C<TextBlock>("FooterMessage").Text.StartsWith("日记未保存："))C<TextBlock>("FooterMessage").Text="日记已保存到本机。";if(diaryStatus!=null)diaryStatus.Text="已保存到本机";return true;}
   catch(Exception error){if(diaryStatus!=null)diaryStatus.Text="保存失败 · 草稿仍保留";C<TextBlock>("FooterMessage").Text="日记未保存："+error.Message;return false;}
  }
  Button DiaryAction(string caption,string tip,Action action){var b=Btn(caption,delegate{action();});b.FontSize=11;b.Padding=new Thickness(9,7,9,7);b.Margin=new Thickness(0,0,4,0);b.Focusable=false;b.ToolTip=tip;return b;}
  object diaryWorkspace;
  void RenderLegacyDiary() {
   C<ScrollViewer>("TaskScroll").Visibility=Visibility.Collapsed;var host=C<ContentControl>("DiaryHost");host.Visibility=Visibility.Visible;
   C<StackPanel>("CompletedSection").Visibility=Visibility.Collapsed;C<Button>("SortButton").Visibility=Visibility.Collapsed;C<TextBlock>("ListHeading").Text="每日手记";C<TextBlock>("ListCount").Text=diary.Entries.Count+" 篇";
   if(diaryLoadError!=null){var body=new StackPanel{Margin=new Thickness(24)};var notice=Text("日记文件暂时无法读取。原文件已保留，没有覆盖。\n"+diaryLoadError,13,"#A17C67");notice.TextWrapping=TextWrapping.Wrap;body.Children.Add(notice);var retry=Btn("重试读取",delegate{LoadDiary();diaryEditor=null;RenderDiary();});retry.HorizontalAlignment=HorizontalAlignment.Left;retry.Margin=new Thickness(0,20,0,0);body.Children.Add(retry);host.Content=body;return;}
   if(diaryEditor==null){BuildDiary(host);diaryWorkspace=host.Content;}else host.Content=diaryWorkspace;
   string next=Day(selected);if(next!=diaryDate){SaveDiary();loadingDiary=true;diaryEditor.IsUndoEnabled=false;diaryDate=next;var entry=diary.Entries.FirstOrDefault(x=>x.Date==next);diaryTitle.Text=entry==null ? "" : entry.Title??"";diaryMood.SelectedItem=entry==null||String.IsNullOrEmpty(entry.Mood) ? "此刻心情" : entry.Mood;diaryPicker.SelectedDate=selected;
    diaryEditor.Document=MakeDiaryDocument(entry==null?null:entry.Blocks,entry==null?"":entry.Text);diaryEditor.IsUndoEnabled=false;diaryUndo.Clear();diaryRedo.Clear();diaryCurrent=new DiarySnapshot{Blocks=DiaryDocument.Capture(diaryEditor.Document)};diaryLastEdit=DateTime.MinValue;diaryEditor.ScrollToHome();loadingDiary=false;diaryHeading.Text=selected.ToString("dddd",CultureInfo.GetCultureInfo("zh-CN"));diaryStatus.Text=diaryDirty ? "有未保存的草稿" : entry==null ? "尚未开始记录" : "已保存到本机";UpdateDiaryCount();ResizeDiaryImages();
   }
   diaryHistory.Content="回看 · "+diary.Entries.Count+" 篇";
   C<TextBlock>("FooterMessage").Text="自动保存 · Ctrl + S 保存 · Ctrl + V 粘贴图片 · 支持拖入图片";
  }
  void BuildDiary(ContentControl host){
   diaryDate=null;
   var card=new Border{Background=Brushes.White,BorderBrush=B("#E1E7D8"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(14),Padding=new Thickness(23,18,23,16)};var body=new Grid();card.Child=body;for(int i=0;i<5;i++)body.RowDefinitions.Add(new RowDefinition{Height=i==3?new GridLength(1,GridUnitType.Star):GridLength.Auto});
   var dates=new DockPanel{LastChildFill=true,Margin=new Thickness(0,0,0,18)};body.Children.Add(dates);
   diaryHistory=DiaryAction("回看","按日期浏览最近 30 篇手记",OpenDiaryHistory);DockPanel.SetDock(diaryHistory,Dock.Right);dates.Children.Add(diaryHistory);
   var previous=DiaryAction("‹","前一天",delegate{if(selected>DateTime.MinValue.Date)ChooseDate(selected.AddDays(-1));});previous.FontSize=18;dates.Children.Add(previous);
   diaryPicker=new DatePicker{Width=132,MinHeight=30,FontSize=11};diaryPicker.SelectedDateChanged+=delegate{if(!loadingDiary&&diaryPicker.SelectedDate.HasValue)ChooseDate(diaryPicker.SelectedDate.Value);};dates.Children.Add(diaryPicker);
   var next=DiaryAction("›","后一天",delegate{if(selected<DateTime.MaxValue.Date)ChooseDate(selected.AddDays(1));});next.FontSize=18;dates.Children.Add(next);
   diaryHeading=Text("",10,"#8D9D88");diaryHeading.VerticalAlignment=VerticalAlignment.Center;dates.Children.Add(diaryHeading);
   var titleGrid=new Grid{Margin=new Thickness(0,0,0,14)};titleGrid.ColumnDefinitions.Add(new ColumnDefinition());titleGrid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});Grid.SetRow(titleGrid,1);body.Children.Add(titleGrid);
   diaryTitle=new TextBox{FontSize=23,FontWeight=FontWeights.Medium,MaxLength=80,Padding=new Thickness(0,4,10,6),BorderThickness=new Thickness(0),Background=Brushes.Transparent};titleGrid.Children.Add(diaryTitle);System.Windows.Automation.AutomationProperties.SetName(diaryTitle,"日记标题");
   diaryTitleHint=Text("给今天起个名字…",22,"#B6BFAE");diaryTitleHint.Margin=new Thickness(0,4,0,0);diaryTitleHint.IsHitTestVisible=false;titleGrid.Children.Add(diaryTitleHint);
   diaryMood=new ComboBox{Width=101,MinHeight=31,FontSize=11,VerticalAlignment=VerticalAlignment.Center,ItemsSource=new[]{"此刻心情","晴朗","平静","充实","有点累","有心事"}};Grid.SetColumn(diaryMood,1);titleGrid.Children.Add(diaryMood);
   var toolbar=new WrapPanel{Margin=new Thickness(0,0,0,13)};Grid.SetRow(toolbar,2);body.Children.Add(toolbar);
   var insert=DiaryAction("＋ 图片","选择图片，可多选；也可粘贴截图或拖入文件",PickDiaryImages);insert.Background=B("#EDF3E9");insert.Foreground=B("#356A51");toolbar.Children.Add(insert);
   var bold=DiaryAction("B","加粗 · Ctrl + B",delegate{diaryEditor.Focus();EditingCommands.ToggleBold.Execute(null,diaryEditor);});bold.FontWeight=FontWeights.Bold;toolbar.Children.Add(bold);
   var italic=DiaryAction("I","斜体 · Ctrl + I",delegate{diaryEditor.Focus();EditingCommands.ToggleItalic.Execute(null,diaryEditor);});italic.FontStyle=FontStyles.Italic;toolbar.Children.Add(italic);
   toolbar.Children.Add(DiaryAction("↶","撤销 · Ctrl + Z",delegate{UndoDiary(false);}));toolbar.Children.Add(DiaryAction("↷","重做 · Ctrl + Y",delegate{UndoDiary(true);}));
   var line=new Border{BorderBrush=B("#EEF1E9"),BorderThickness=new Thickness(0,1,0,0)};Grid.SetRow(line,3);body.Children.Add(line);var writing=new Grid{Margin=new Thickness(0,14,0,0)};line.Child=writing;
   diaryEditor=new RichTextBox{Name="DiaryEditor",AcceptsTab=true,Foreground=B("#405747"),FontSize=15,BorderThickness=new Thickness(0),Padding=new Thickness(0),Background=Brushes.Transparent,VerticalContentAlignment=VerticalAlignment.Top,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,AllowDrop=true,IsDocumentEnabled=true};writing.Children.Add(diaryEditor);System.Windows.Automation.AutomationProperties.SetName(diaryEditor,"日记正文");
   diaryPlaceholder=Text("今天，有什么想记下来的？\n\n写几句话，或放一张喜欢的照片。",14,"#ACB7A3");diaryPlaceholder.LineHeight=28;diaryPlaceholder.Margin=new Thickness(5,4,10,0);diaryPlaceholder.IsHitTestVisible=false;writing.Children.Add(diaryPlaceholder);
   var footer=new Grid{Margin=new Thickness(0,14,0,0)};footer.ColumnDefinitions.Add(new ColumnDefinition());footer.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});Grid.SetRow(footer,4);body.Children.Add(footer);
   var status=new StackPanel();footer.Children.Add(status);diaryStatus=Text("",10,"#819575");status.Children.Add(diaryStatus);diaryCount=Text("",9,"#A2AD96");diaryCount.Margin=new Thickness(0,4,0,0);status.Children.Add(diaryCount);
   var actions=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(actions,1);footer.Children.Add(actions);actions.Children.Add(DiaryAction("导出","导出包含图片的独立 HTML，或纯文本",ExportDiary));var save=DiaryAction("保存","保存日记 · Ctrl + S",delegate{SaveDiary();});save.Background=B("#E5EDDF");actions.Children.Add(save);
   diaryEditor.IsUndoEnabled=false;diaryEditor.TextChanged+=delegate{DiaryEditorChanged();};diaryEditor.SelectionChanged+=delegate{if(!loadingDiary&&diaryCurrent!=null)diaryCurrent.Caret=diaryEditor.Document.ContentStart.GetOffsetToPosition(diaryEditor.CaretPosition);};
   diaryEditor.CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo,delegate(object s,ExecutedRoutedEventArgs e){UndoDiary(false);e.Handled=true;},delegate(object s,CanExecuteRoutedEventArgs e){e.CanExecute=diaryUndo.Count>0;e.Handled=true;}));
   diaryEditor.CommandBindings.Add(new CommandBinding(ApplicationCommands.Redo,delegate(object s,ExecutedRoutedEventArgs e){UndoDiary(true);e.Handled=true;},delegate(object s,CanExecuteRoutedEventArgs e){e.CanExecute=diaryRedo.Count>0;e.Handled=true;}));diaryTitle.TextChanged+=delegate{DiaryChanged();};diaryMood.SelectionChanged+=delegate{DiaryChanged();};diaryEditor.SizeChanged+=delegate{ResizeDiaryImages();};
   DataObject.AddPastingHandler(diaryEditor,delegate(object sender,DataObjectPastingEventArgs e){e.CancelCommand();PasteDiary(e.DataObject);});
   diaryEditor.PreviewDragOver+=delegate(object sender,DragEventArgs e){e.Effects=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;};
   diaryEditor.PreviewDrop+=delegate(object sender,DragEventArgs e){e.Handled=true;if(e.Data.GetDataPresent(DataFormats.FileDrop)){var position=diaryEditor.GetPositionFromPoint(e.GetPosition(diaryEditor),true);if(position!=null)diaryEditor.CaretPosition=position;InsertDiaryFiles((string[])e.Data.GetData(DataFormats.FileDrop));}};
   host.Content=card;
  }
  FlowDocument MakeDiaryDocument(List<DiaryParagraph> blocks,string text){var document=new FlowDocument{FontFamily=new FontFamily("Microsoft YaHei UI"),FontSize=15,LineHeight=29,PagePadding=new Thickness(1,4,10,24)};var paragraphStyle=new Style(typeof(Paragraph));paragraphStyle.Setters.Add(new Setter(Block.MarginProperty,new Thickness(0,0,0,10)));document.Resources.Add(typeof(Paragraph),paragraphStyle);if(blocks!=null){foreach(var row in blocks){var p=new Paragraph{Margin=new Thickness(0,0,0,10)};foreach(var part in row.Parts){if(part.Image!=null)p.Inlines.Add(CreateDiaryImage(part.Image));else p.Inlines.Add(new Run(part.Text){FontWeight=part.Bold?FontWeights.Bold:FontWeights.Normal,FontStyle=part.Italic?FontStyles.Italic:FontStyles.Normal});}document.Blocks.Add(p);}}else if(!String.IsNullOrEmpty(text)){foreach(string line in text.Replace("\r\n","\n").Split('\n'))document.Blocks.Add(new Paragraph(new Run(line)){Margin=new Thickness(0,0,0,10)});}if(document.Blocks.Count==0)document.Blocks.Add(new Paragraph{Margin=new Thickness(0,0,0,10)});return document;}
  bool SameDiary(List<DiaryParagraph> a,List<DiaryParagraph> b){if(a.Count!=b.Count)return false;for(int i=0;i<a.Count;i++){if(a[i].Parts.Count!=b[i].Parts.Count)return false;for(int j=0;j<a[i].Parts.Count;j++){var x=a[i].Parts[j];var y=b[i].Parts[j];if(x.Text!=y.Text||x.Image!=y.Image||x.Bold!=y.Bold||x.Italic!=y.Italic)return false;}}return true;}
  void DiaryEditorChanged(){if(loadingDiary||diaryDate==null)return;var blocks=DiaryDocument.Capture(diaryEditor.Document);if(DiaryDocument.PlainText(blocks).Length>100000||blocks.Count>20000||blocks.Sum(p=>p.Parts.Count)>30000||DiaryDocument.ImageCount(blocks)>30){if(diaryCurrent!=null)RestoreDiarySnapshot(diaryCurrent);diaryStatus.Text="已达上限：10 万字 / 30 张图片";return;}if(diaryCurrent!=null&&!SameDiary(diaryCurrent.Blocks,blocks)){bool group=!diaryAtomicEdit&&(DateTime.UtcNow-diaryLastEdit).TotalMilliseconds<650&&DiaryDocument.ImageCount(blocks)==DiaryDocument.ImageCount(diaryCurrent.Blocks);if(!group||diaryUndo.Count==0)diaryUndo.Add(diaryCurrent);while(diaryUndo.Count>80)diaryUndo.RemoveAt(0);diaryRedo.Clear();diaryLastEdit=DateTime.UtcNow;}diaryCurrent=new DiarySnapshot{Blocks=blocks,Caret=diaryEditor.Document.ContentStart.GetOffsetToPosition(diaryEditor.CaretPosition)};DiaryChanged();}
  void RestoreDiarySnapshot(DiarySnapshot snapshot){loadingDiary=true;try{diaryEditor.Document=MakeDiaryDocument(snapshot.Blocks,"");int end=diaryEditor.Document.ContentStart.GetOffsetToPosition(diaryEditor.Document.ContentEnd);diaryEditor.CaretPosition=diaryEditor.Document.ContentStart.GetPositionAtOffset(Math.Min(Math.Max(0,snapshot.Caret),end),LogicalDirection.Forward);diaryCurrent=snapshot;}finally{loadingDiary=false;}DiaryChanged();ResizeDiaryImages();}
  void UndoDiary(bool redo){var from=redo?diaryRedo:diaryUndo;var to=redo?diaryUndo:diaryRedo;if(from.Count==0)return;to.Add(diaryCurrent);var snapshot=from[from.Count-1];from.RemoveAt(from.Count-1);RestoreDiarySnapshot(snapshot);diaryLastEdit=DateTime.MinValue;diaryEditor.Focus();CommandManager.InvalidateRequerySuggested();}
  void DiaryChanged(){if(loadingDiary||diaryDate==null||diaryEditor==null)return;var blocks=DiaryDocument.Capture(diaryEditor.Document);string text=DiaryDocument.PlainText(blocks);int images=DiaryDocument.ImageCount(blocks);if(text.Length>100000||images>30){diaryStatus.Text="已达上限：10 万字 / 30 张图片";return;}var entry=diary.Entries.FirstOrDefault(x=>x.Date==diaryDate);string title=diaryTitle.Text;string mood=diaryMood.SelectedItem as string;if(text.Length==0&&images==0&&title.Length==0&&(mood==null||mood=="此刻心情")){if(entry!=null)diary.Entries.Remove(entry);}else{if(entry==null){entry=new DiaryEntry{Date=diaryDate};diary.Entries.Add(entry);}entry.Text=text;entry.Title=title;entry.Mood=mood=="此刻心情"?null:mood;entry.Blocks=blocks;entry.UpdatedTicks=DateTime.UtcNow.Ticks;}diaryDirty=true;diaryStatus.Text="编辑中 · 稍后自动保存";UpdateDiaryCount();C<TextBlock>("ListCount").Text=diary.Entries.Count+" 篇";diaryHistory.Content="回看 · "+diary.Entries.Count+" 篇";diarySaveTimer.Stop();diarySaveTimer.Start();}
  void UpdateDiaryCount(){if(diaryCount==null)return;var blocks=DiaryDocument.Capture(diaryEditor.Document);string text=DiaryDocument.PlainText(blocks);int images=DiaryDocument.ImageCount(blocks);diaryCount.Text=text.Count(ch=>!Char.IsWhiteSpace(ch))+" 字"+(images==0 ? "" : " · "+images+" 张图片");diaryPlaceholder.Visibility=text.Length==0&&images==0?Visibility.Visible:Visibility.Collapsed;diaryTitleHint.Visibility=diaryTitle.Text.Length==0?Visibility.Visible:Visibility.Collapsed;}
  void OpenDiaryHistory(){var menu=new ContextMenu{MaxHeight=440};foreach(var entry in diary.Entries.OrderByDescending(x=>x.Date).Take(30)){var day=DateTime.ParseExact(entry.Date,"yyyy-MM-dd",CultureInfo.InvariantCulture);string title=String.IsNullOrWhiteSpace(entry.Title)?entry.Text.Replace("\r"," ").Replace("\n"," "):entry.Title;if(title.Length>18)title=title.Substring(0,18)+"…";if(title.Length==0)title="照片手记";var item=new MenuItem{Header=entry.Date+"   "+title,IsCheckable=true,IsChecked=entry.Date==diaryDate};item.Click+=delegate{ChooseDate(day);};menu.Items.Add(item);}if(menu.Items.Count==0)menu.Items.Add(new MenuItem{Header="写下第一篇手记后，在这里回看",IsEnabled=false});diaryHistory.ContextMenu=menu;menu.PlacementTarget=diaryHistory;menu.IsOpen=true;}
  byte[] DiaryImageBytes(string name){if(!DiaryDocument.ValidImage(name))return null;if(diaryPath==null){byte[] bytes;return diaryImageMemory.TryGetValue(name,out bytes)?bytes:null;}string file=Path.Combine(Path.GetDirectoryName(diaryPath),"diary-images",name);try{return File.Exists(file)&&new FileInfo(file).Length<=20*1024*1024?File.ReadAllBytes(file):null;}catch{return null;}}
  InlineUIContainer CreateDiaryImage(string name,TextPointer position=null){var bytes=DiaryImageBytes(name);FrameworkElement element;try{if(bytes==null)throw new IOException();BitmapSource source;if(diaryPath==null)source=DiaryDocument.Decode(bytes);else{var stored=new BitmapImage();stored.BeginInit();stored.CacheOption=BitmapCacheOption.OnLoad;stored.UriSource=new Uri(Path.Combine(Path.GetDirectoryName(diaryPath),"diary-images",name),UriKind.Absolute);stored.EndInit();stored.Freeze();source=stored;}var image=new Image{Source=source,Tag=name,Stretch=Stretch.Uniform,Width=Math.Min(560,source.PixelWidth),MaxHeight=440,Margin=new Thickness(0,9,0,9),Cursor=Cursors.Hand,ToolTip="双击放大 · 右键移除"};System.Windows.Automation.AutomationProperties.SetName(image,"日记图片");image.MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e){if(e.ClickCount==2){var viewer=new Window{Owner=Window,Title="日记图片",Width=Math.Min(980,SystemParameters.WorkArea.Width-70),Height=Math.Min(760,SystemParameters.WorkArea.Height-70),WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=B("#F8F9F6"),Content=new Image{Source=source,Stretch=Stretch.Uniform,Margin=new Thickness(20)}};viewer.Show();e.Handled=true;}};element=image;}catch{element=new Border{Tag=name,Background=B("#F4F1E9"),Padding=new Thickness(16),Margin=new Thickness(0,8,0,8),Child=Text("图片暂时无法读取 · 请保留 diary-images 文件夹",11,"#A17C67")};}
   var inline=position==null?new InlineUIContainer(element):new InlineUIContainer(element,position);inline.BaselineAlignment=BaselineAlignment.Center;var menu=new ContextMenu();var remove=new MenuItem{Header="移除此图片"};remove.Click+=delegate{diaryEditor.Focus();diaryEditor.Selection.Select(inline.ElementStart,inline.ElementEnd);diaryAtomicEdit=true;try{diaryEditor.Selection.Text="";}finally{diaryAtomicEdit=false;diaryLastEdit=DateTime.MinValue;}};menu.Items.Add(remove);element.ContextMenu=menu;return inline;
  }
  void ResizeDiaryImages(){if(diaryEditor==null)return;double width=Math.Max(120,diaryEditor.ActualWidth-36);foreach(var p in DiaryDocument.Paragraphs(diaryEditor.Document.Blocks))foreach(var ui in p.Inlines.OfType<InlineUIContainer>()){var image=ui.Child as Image;if(image!=null)image.Width=Math.Min(Math.Min(620,((BitmapSource)image.Source).PixelWidth),width);}}
  void PickDiaryImages(){var dialog=new Microsoft.Win32.OpenFileDialog{Title="插入日记图片",Filter="图片 (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff",Multiselect=true};if(dialog.ShowDialog(Window)==true)InsertDiaryFiles(dialog.FileNames);}
  void InsertDiaryFiles(string[] files){foreach(string file in files)try{InsertDiaryImage(DiaryDocument.ReadImage(file));}catch(Exception error){diaryStatus.Text="图片未插入："+error.Message;C<TextBlock>("FooterMessage").Text=diaryStatus.Text;break;}}
  void InsertDiaryImage(byte[] bytes){if(diaryDate==null)return;if(DiaryDocument.ImageCount(DiaryDocument.Capture(diaryEditor.Document))>=30)throw new Exception("单篇最多插入 30 张图片。");DiaryDocument.Decode(bytes);string name=Guid.NewGuid().ToString("N")+".png";if(diaryPath==null)diaryImageMemory[name]=bytes;else{string directory=Path.Combine(Path.GetDirectoryName(diaryPath),"diary-images");Directory.CreateDirectory(directory);string file=Path.Combine(directory,name);using(var stream=new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}}
   diaryEditor.Focus();diaryAtomicEdit=true;diaryEditor.BeginChange();try{diaryEditor.Selection.Text="";var before=new LineBreak(diaryEditor.CaretPosition);var image=CreateDiaryImage(name,before.ElementEnd);var after=new LineBreak(image.ElementEnd);diaryEditor.CaretPosition=after.ElementEnd;}finally{diaryEditor.EndChange();diaryAtomicEdit=false;diaryLastEdit=DateTime.MinValue;}ResizeDiaryImages();}
  void PasteDiary(IDataObject data){try{if(data.GetDataPresent(DataFormats.FileDrop)){InsertDiaryFiles((string[])data.GetData(DataFormats.FileDrop));return;}if(data.GetDataPresent(DataFormats.Bitmap)){var bitmap=data.GetData(DataFormats.Bitmap) as BitmapSource;if(bitmap!=null){InsertDiaryImage(DiaryDocument.EncodeImage(bitmap));return;}}if(data.GetDataPresent(DataFormats.UnicodeText)){string text=(string)data.GetData(DataFormats.UnicodeText);int count=DiaryDocument.PlainText(DiaryDocument.Capture(diaryEditor.Document)).Length-diaryEditor.Selection.Text.Length;if(count+text.Length>100000)throw new Exception("单篇日记最多 10 万字。");diaryEditor.Selection.Text=text;diaryEditor.CaretPosition=diaryEditor.Selection.End;}}catch(Exception error){diaryStatus.Text="未粘贴："+error.Message;}}
  void ExportDiary(){if(!SaveDiary())return;var entry=diary.Entries.FirstOrDefault(x=>x.Date==diaryDate);if(entry==null){diaryStatus.Text="这一天还没有日记内容";return;}var dialog=new Microsoft.Win32.SaveFileDialog{FileName="拾日手记-"+diaryDate,DefaultExt=".html",Filter="图文日记 (*.html)|*.html|纯文本 (*.txt)|*.txt"};if(dialog.ShowDialog(Window)==true)try{string content=dialog.FilterIndex==2?entry.Date+Environment.NewLine+entry.Title+Environment.NewLine+Environment.NewLine+entry.Text:DiaryDocument.ExportHtml(entry,DiaryImageBytes);File.WriteAllText(dialog.FileName,content,new UTF8Encoding(true));diaryStatus.Text="日记已导出";}catch(Exception error){MessageBox.Show(Window,"导出失败："+error.Message,"拾日");}}
 }
}
