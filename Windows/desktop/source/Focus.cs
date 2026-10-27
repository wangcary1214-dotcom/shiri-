using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace Shiri {
 public static class BeijingClock {
  public static DateTime FromUtc(DateTime utc){return DateTime.SpecifyKind(utc.ToUniversalTime().AddHours(8),DateTimeKind.Unspecified);}
  public static DateTime Now {get{return FromUtc(DateTime.UtcNow);}}
  public static DateTime DayStartUtc(DateTime utc){return DateTime.SpecifyKind(FromUtc(utc).AddHours(-4).Date.AddHours(-4),DateTimeKind.Utc);}
 }
 [DataContract] public sealed class FocusProject {
  [DataMember]public string Id=Guid.NewGuid().ToString("N");
  [DataMember]public string Name="日常专注";
  [DataMember]public string Color="#356A51";
  [DataMember]public string GoalId="";
 }
 [DataContract] public sealed class FocusSegment {
  [DataMember]public string ProjectId;
  [DataMember]public long StartTicks;
  [DataMember]public long EndTicks;
  [DataMember]public string GoalId="";
 }
 [DataContract] public sealed class FocusData {
  [DataMember]public int Version=1;
  [DataMember]public int FocusMinutes=25;
  [DataMember]public int BreakMinutes=5;
  [DataMember]public string SelectedId;
  [DataMember]public List<FocusProject> Projects=new List<FocusProject>();
  [DataMember]public List<FocusSegment> Segments=new List<FocusSegment>();
  public static FocusData Fresh(){var data=new FocusData();var project=new FocusProject();data.Projects.Add(project);data.SelectedId=project.Id;return data;}
 }
 public static class FocusStorage {
  public static bool ValidColor(string color){return color!=null&&Regex.IsMatch(color,"^#[0-9a-fA-F]{6}$");}
  public static string Serialize(FocusData data){using(var stream=new MemoryStream()){new DataContractJsonSerializer(typeof(FocusData)).WriteObject(stream,data);return Encoding.UTF8.GetString(stream.ToArray());}}
  public static FocusData Parse(string json) {
   if(json.Length>32*1024*1024)throw new Exception("专注数据文件过大。");
   FocusData data;using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(json)))data=(FocusData)new DataContractJsonSerializer(typeof(FocusData)).ReadObject(stream);
   if(data==null||data.Version!=1||data.FocusMinutes<1||data.FocusMinutes>360||data.BreakMinutes<1||data.BreakMinutes>120||data.Projects==null||data.Segments==null||data.Projects.Count>5000||data.Segments.Count>200000)throw new Exception("专注记录格式不正确，未覆盖原文件。");
   var ids=new HashSet<string>();var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(var p in data.Projects)if(p==null||String.IsNullOrWhiteSpace(p.Id)||!ids.Add(p.Id)||String.IsNullOrWhiteSpace(p.Name)||p.Name.Length>120||!names.Add(p.Name)||!ValidColor(p.Color))throw new Exception("专注任务名称或颜色无效。");
   if(!ids.Contains(data.SelectedId))throw new Exception("专注任务选择无效。");
   foreach(var s in data.Segments)if(s==null||!ids.Contains(s.ProjectId)||s.StartTicks<DateTime.MinValue.Ticks||s.EndTicks>DateTime.MaxValue.Ticks||s.StartTicks>=s.EndTicks)throw new Exception("专注时段数据无效。");
   return data;
  }
  public static FocusData Load(string path){return path==null||!File.Exists(path) ? FocusData.Fresh() : Parse(File.ReadAllText(path,Encoding.UTF8));}
  public static void Save(string path,FocusData data) {
   Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp";
   using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){byte[] bytes=Encoding.UTF8.GetBytes(Serialize(data));stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
   if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);
  }
 }
 public enum FocusPhase {Focus,Break}
 public enum FocusSignal {None,FocusFinished,BreakFinished}
 public sealed class FocusSlice {
  public string Id,Name,Color;
  public double Seconds;
 }
 public sealed class FocusEngine {
  public readonly FocusData Data;
  readonly Func<DateTime> utcNow;
  DateTime deadline,activeStart;
  double remaining;
  readonly List<FocusSegment> pendingIntervals=new List<FocusSegment>();
  DateTime statisticsDay=DateTime.MinValue;
  readonly Dictionary<string,double> historySeconds=new Dictionary<string,double>();
  int historyCount;FocusSegment historyLast;long historyLastStart,historyLastEnd;string historyLastProject;
  internal long HistoryEntriesScanned;
  public FocusPhase Phase {get;private set;}
  public bool Running {get;private set;}
  public double Remaining {get{return Running ? Math.Max(0,(deadline-utcNow()).TotalSeconds) : remaining;}}
  public FocusProject Project {get{return Data.Projects.Single(x=>x.Id==Data.SelectedId);}}
  public double Duration {get{return (Phase==FocusPhase.Focus ? Data.FocusMinutes : Data.BreakMinutes)*60.0;}}
  public double Progress {get{return Math.Max(0,Math.Min(1,1-Remaining/Duration));}}
  public FocusEngine(FocusData data):this(data,delegate{return DateTime.UtcNow;}){}
  public FocusEngine(FocusData data,Func<DateTime> clock){Data=data;utcNow=clock;remaining=data.FocusMinutes*60;}
  public void Toggle() {
   DateTime now=utcNow();
   if(Running&&now>=deadline){Tick();return;}
   if(Running){Capture(now);remaining=Math.Max(0,(deadline-now).TotalSeconds);Running=false;}
   else {if(remaining<=0)remaining=Duration;deadline=now.AddSeconds(remaining);activeStart=now;Running=true;}
  }
  void Capture(DateTime until) {
   if(!Running)return;
   DateTime end=until<deadline ? until : deadline;
   if(Phase==FocusPhase.Focus&&end>activeStart) {
    var previous=pendingIntervals.LastOrDefault();
    if(previous!=null&&previous.ProjectId==Project.Id&&previous.GoalId==Project.GoalId&&previous.EndTicks==activeStart.Ticks)previous.EndTicks=end.Ticks;
    else pendingIntervals.Add(new FocusSegment{ProjectId=Project.Id,StartTicks=activeStart.Ticks,EndTicks=end.Ticks,GoalId=Project.GoalId??""});
   }
   if(end>activeStart)activeStart=end;
  }
  public void Checkpoint(){Capture(utcNow());}
  public void Pause(){if(Running&&utcNow()>=deadline)Tick();if(Running){DateTime now=utcNow();Capture(now);remaining=Math.Max(0,(deadline-now).TotalSeconds);Running=false;}}
  public void Reset(){Pause();pendingIntervals.Clear();Phase=FocusPhase.Focus;remaining=Data.FocusMinutes*60;}
  public void StartBreak(){Pause();pendingIntervals.Clear();Phase=FocusPhase.Break;remaining=Data.BreakMinutes*60;Toggle();}
  public void EndBreak(){Reset();}
  public FocusSignal Tick() {
   if(!Running||utcNow()<deadline)return FocusSignal.None;
   Capture(deadline);Running=false;
   if(Phase==FocusPhase.Focus){foreach(var interval in pendingIntervals){var previous=Data.Segments.LastOrDefault();if(previous!=null&&previous.ProjectId==interval.ProjectId&&previous.GoalId==interval.GoalId&&previous.EndTicks==interval.StartTicks)previous.EndTicks=interval.EndTicks;else Data.Segments.Add(interval);}pendingIntervals.Clear();Phase=FocusPhase.Break;remaining=Data.BreakMinutes*60;Toggle();return FocusSignal.FocusFinished;}
   Phase=FocusPhase.Focus;remaining=Data.FocusMinutes*60;return FocusSignal.BreakFinished;
  }
  public void Configure(int focusMinutes,int breakMinutes,string name,string color,string goalId="") {
   if(focusMinutes<1||focusMinutes>360||breakMinutes<1||breakMinutes>120||String.IsNullOrWhiteSpace(name)||name.Trim().Length>120||!FocusStorage.ValidColor(color))throw new ArgumentException("请检查专注时长、休息时长、任务名称与颜色。");
   Pause();name=name.Trim();var project=Data.Projects.FirstOrDefault(x=>String.Equals(x.Name,name,StringComparison.OrdinalIgnoreCase));
   if(project==null){project=new FocusProject{Name=name,Color=color.ToUpperInvariant()};Data.Projects.Add(project);}else project.Color=color.ToUpperInvariant();
   project.GoalId=goalId??"";Data.SelectedId=project.Id;Data.FocusMinutes=focusMinutes;Data.BreakMinutes=breakMinutes;Reset();
  }
  public List<FocusSlice> Statistics(DateTime atUtc) {
   DateTime start=BeijingClock.DayStartUtc(atUtc),end=start.AddDays(1);
   if(statisticsDay!=start||historyCount>Data.Segments.Count||(historyCount>0&&!Object.ReferenceEquals(historyLast,Data.Segments[historyCount-1]))){historySeconds.Clear();historyCount=0;historyLast=null;statisticsDay=start;}
   // Capture only extends the last interval or appends new intervals. Update that
   // difference instead of rescanning years of saved history every second.
   if(historyCount>0){double old=Clipped(historyLastStart,historyLastEnd,start.Ticks,end.Ticks),current=Clipped(historyLast.StartTicks,historyLast.EndTicks,start.Ticks,end.Ticks);if(old!=current||historyLastProject!=historyLast.ProjectId){if(!historySeconds.ContainsKey(historyLastProject))historySeconds[historyLastProject]=0;historySeconds[historyLastProject]-=old;if(!historySeconds.ContainsKey(historyLast.ProjectId))historySeconds[historyLast.ProjectId]=0;historySeconds[historyLast.ProjectId]+=current;}}
   for(int i=historyCount;i<Data.Segments.Count;i++){var segment=Data.Segments[i];Add(historySeconds,segment.ProjectId,segment.StartTicks,segment.EndTicks,start.Ticks,end.Ticks);HistoryEntriesScanned++;}
   historyCount=Data.Segments.Count;if(historyCount>0){historyLast=Data.Segments[historyCount-1];historyLastStart=historyLast.StartTicks;historyLastEnd=historyLast.EndTicks;historyLastProject=historyLast.ProjectId;}
   var seconds=new Dictionary<string,double>(historySeconds);
   return Data.Projects.Where(x=>seconds.ContainsKey(x.Id)&&seconds[x.Id]>=1).Select(x=>new FocusSlice{Id=x.Id,Name=x.Name,Color=x.Color,Seconds=seconds[x.Id]}).OrderByDescending(x=>x.Seconds).ToList();
  }
  static double Clipped(long from,long to,long start,long end){return Math.Max(0,(Math.Min(to,end)-Math.Max(from,start))/(double)TimeSpan.TicksPerSecond);}
  public double[] WeekSeconds(DateTime weekStartUtc){var totals=new double[7];long start=weekStartUtc.Ticks,end=weekStartUtc.AddDays(7).Ticks;foreach(var segment in Data.Segments){if(segment.EndTicks<=start||segment.StartTicks>=end)continue;for(int i=0;i<7;i++){long day=start+i*TimeSpan.TicksPerDay;totals[i]+=Clipped(segment.StartTicks,segment.EndTicks,day,day+TimeSpan.TicksPerDay);}}return totals;}
  public FocusData Snapshot(){
   // Older intervals are immutable in the app; Capture can only extend the last.
   // Copy that mutable tail and all editable projects before handing off to I/O.
   var copy=new FocusData{Version=Data.Version,FocusMinutes=Data.FocusMinutes,BreakMinutes=Data.BreakMinutes,SelectedId=Data.SelectedId,Projects=Data.Projects.Select(x=>new FocusProject{Id=x.Id,Name=x.Name,Color=x.Color,GoalId=x.GoalId}).ToList(),Segments=new List<FocusSegment>(Data.Segments)};
   if(copy.Segments.Count>0){var tail=copy.Segments[copy.Segments.Count-1];copy.Segments[copy.Segments.Count-1]=new FocusSegment{ProjectId=tail.ProjectId,StartTicks=tail.StartTicks,EndTicks=tail.EndTicks,GoalId=tail.GoalId};}return copy;
  }
  static void Add(Dictionary<string,double> result,string id,long from,long to,long start,long end) {
   double seconds=(Math.Min(to,end)-Math.Max(from,start))/(double)TimeSpan.TicksPerSecond;
   if(seconds<=0)return;if(!result.ContainsKey(id))result[id]=0;result[id]+=seconds;
  }
  public double GoalSeconds(string goalId,DateTime atUtc) {
   var projects=Data.Projects.ToDictionary(x=>x.Id);double seconds=0;foreach(var segment in Data.Segments){FocusProject project;projects.TryGetValue(segment.ProjectId,out project);string belongs=String.IsNullOrEmpty(segment.GoalId)&&project!=null ? project.GoalId : segment.GoalId;if(belongs==goalId)seconds+=(segment.EndTicks-segment.StartTicks)/(double)TimeSpan.TicksPerSecond;}
   return seconds;
  }
  public List<FocusSegment> GoalSegments(string goalId) {return Data.Segments.Where(segment=>{var project=Data.Projects.FirstOrDefault(x=>x.Id==segment.ProjectId);return (String.IsNullOrEmpty(segment.GoalId)&&project!=null ? project.GoalId : segment.GoalId)==goalId;}).ToList();}
  public static string FormatTime(double value){int seconds=(int)Math.Ceiling(Math.Max(0,value));return seconds>=3600 ? (seconds/3600).ToString("00")+":"+((seconds/60)%60).ToString("00")+":"+(seconds%60).ToString("00") : (seconds/60).ToString("00")+":"+(seconds%60).ToString("00");}
  public static string FormatDuration(double value){int seconds=(int)Math.Floor(Math.Max(0,value));return seconds>=3600 ? seconds/3600+"小时 "+(seconds%3600)/60+"分" : seconds>=60 ? seconds/60+"分 "+seconds%60+"秒" : seconds+"秒";}
 }
}
