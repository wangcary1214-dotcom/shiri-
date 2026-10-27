using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Globalization;
namespace Shiri {
 public static class CourseLanes {
 public static Dictionary<string,Tuple<int,int>> Assign(List<Course> courses){var result=new Dictionary<string,Tuple<int,int>>();foreach(var day in courses.GroupBy(c=>c.Day)){var group=new List<Course>();string end="";Action flush=delegate{var ends=new List<string>();var indices=new Dictionary<string,int>();foreach(var c in group){int lane=ends.FindIndex(t=>String.CompareOrdinal(t,c.Start)<=0);if(lane<0){lane=ends.Count;ends.Add(c.End);}else ends[lane]=c.End;indices[c.Id]=lane;}foreach(var c in group)result[c.Id]=Tuple.Create(indices[c.Id],Math.Max(1,ends.Count));group.Clear();};foreach(var c in day.OrderBy(c=>c.Start).ThenBy(c=>c.Id)){if(group.Count>0&&String.CompareOrdinal(c.Start,end)>=0)flush();group.Add(c);if(String.CompareOrdinal(c.End,end)>0)end=c.End;}flush();}return result;}
 }
 public sealed class CourseBoard:FrameworkElement {
  public List<Course> Courses; public TimetablePreferences Settings; public int Week; public Action<Course> Open;
  public readonly List<Tuple<Rect,Course>> HitTargets=new List<Tuple<Rect,Course>>();
  static readonly string[] Colors={"#EAF2E3","#E0F0ED","#F5E6ED","#E4EFF8","#EEE8F6","#F7EEDC"};
  static readonly string[] Ink={"#56753E","#3F7C72","#965A7B","#4A7399","#796297","#967334"};
  string hoveredId;
  sealed class Slot {public string Name,Start,End;public int A,B;}
  List<Slot> Slots(){var slots=Settings.Periods.Select((p,i)=>new Slot{Name=(i+1).ToString(),Start=p.Start,End=p.End,A=(int)TimeSpan.Parse(p.Start).TotalMinutes,B=(int)TimeSpan.Parse(p.End).TotalMinutes}).ToList();foreach(var c in Courses){int a=(int)TimeSpan.Parse(c.Start).TotalMinutes,b=(int)TimeSpan.Parse(c.End).TotalMinutes;if(!slots.Any(s=>a<s.B&&b>s.A))slots.Add(new Slot{Name="自定",Start=c.Start,End=c.End,A=a,B=b});else {if(a<slots.Min(s=>s.A))slots.Add(new Slot{Name="自定",Start=c.Start,End=TimeSpan.FromMinutes(slots.Min(s=>s.A)).ToString(@"hh\:mm"),A=a,B=slots.Min(s=>s.A)});if(b>slots.Max(s=>s.B))slots.Add(new Slot{Name="自定",Start=TimeSpan.FromMinutes(slots.Max(s=>s.B)).ToString(@"hh\:mm"),End=c.End,A=slots.Max(s=>s.B),B=b});}}return slots.OrderBy(s=>s.A).ToList();}
  double Position(List<Slot> slots,int minutes){for(int i=0;i<slots.Count;i++){if(minutes<slots[i].A)return i;if(minutes<=slots[i].B)return i+(minutes-slots[i].A)/(double)Math.Max(1,slots[i].B-slots[i].A);}return slots.Count;}
  protected override void OnRender(DrawingContext dc){
   base.OnRender(dc);HitTargets.Clear();if(Settings==null||ActualWidth<100||ActualHeight<80)return;
   var slots=Slots();double left=70,top=62,width=ActualWidth,col=(width-left)/7,row=(ActualHeight-top-4)/Math.Max(1,slots.Count);
   DateTime anchor=DateTime.Parse(Settings.WeekAnchorDate),monday=anchor.AddDays(-((int)anchor.DayOfWeek+6)%7).AddDays((Week-Settings.WeekAnchorNumber)*7);
   Draw(dc,"节次",2,9,11,"#7D8C80",62,17,false);Draw(dc,"时间",2,28,10,"#9AA69B",62,16,false);
   for(int d=0;d<7;d++){
    DateTime date=monday.AddDays(d);bool today=date.Date==BeijingClock.Now.Date;double x=left+d*col;
    double badge=Math.Min(48,col-8);if(today)dc.DrawRoundedRectangle(Controller.B("#E3EDE2"),null,new Rect(x+(col-badge)/2,2,badge,46),11,11);
    Draw(dc,"周"+"一二三四五六日"[d],x+4,7,13,today?"#356A51":"#63756A",col-8,19,today,true);
    Draw(dc,date.ToString("M/d"),x+4,29,10.5,today?"#356A51":"#98A59B",col-8,16,false,true);
   }
   dc.DrawLine(new Pen(Controller.B("#E5EBE3"),1),new Point(left,54),new Point(width-3,54));
   for(int i=0;i<slots.Count;i++){
    double y=top+i*row;Draw(dc,slots[i].Name=="自定"?"自定":"第"+slots[i].Name+"节",2,y+3,Math.Max(9,Math.Min(12,row*.32)),"#63766A",65,Math.Max(13,row*.42),true);
    if(row>=33)Draw(dc,slots[i].Start+"–"+slots[i].End,2,y+22,9.5,"#9AA69B",66,14,false);
   }
   var visible=Courses.Where(c=>c.FirstWeek<=Week&&c.LastWeek>=Week&&(String.IsNullOrEmpty(c.WeekPattern)||c.WeekPattern=="All"||(c.WeekPattern=="Odd"&&Week%2==1)||(c.WeekPattern=="Even"&&Week%2==0))).OrderBy(c=>c.Start).ToList();
   var lanes=CourseLanes.Assign(visible);
   foreach(var c in visible){
    int start=(int)TimeSpan.Parse(c.Start).TotalMinutes,end=(int)TimeSpan.Parse(c.End).TotalMinutes,lane=lanes[c.Id].Item1;
    double laneWidth=(col-5)/lanes[c.Id].Item2,a=Position(slots,start),b=Position(slots,end);
    var r=new Rect(left+(c.Day-1)*col+lane*laneWidth,top+a*row,laneWidth-3,Math.Max(4,(b-a)*row-6));
    int hash=0;foreach(char ch in c.Title)hash=(hash*31+ch)&0x7fffffff;int hue=hash%Colors.Length;
    dc.DrawRoundedRectangle(Controller.B(Colors[hue]),null,r,9,9);HitTargets.Add(Tuple.Create(r,c));
    dc.PushClip(new RectangleGeometry(r,9,9));
    var accent=new SolidColorBrush(((SolidColorBrush)Controller.B(Ink[hue])).Color);accent.Opacity=.68;dc.DrawRectangle(accent,null,new Rect(r.X,r.Y,3,r.Height));
    if(hoveredId==c.Id)dc.DrawRoundedRectangle(null,new Pen(Controller.B(Ink[hue]),1.2),r,9,9);
    double estimatedTitle=c.Title.Sum(ch=>ch>255?14:8.4);bool wrapped=estimatedTitle>r.Width-24;bool time=r.Height>=(wrapped?98:76)&&r.Width>=76,place=r.Height>=64&&r.Width>=76;
    double padding=r.Width<60?5:9,size=r.Height<38?10.5:14,titleHeight=Math.Min(39,r.Height-14-(time?40:place?23:0));
    Draw(dc,c.Title,r.X+padding,r.Y+7,size,Ink[hue],r.Width-padding-6,titleHeight,true);
    if(time)Draw(dc,c.Start+"–"+c.End,r.X+padding,r.Bottom-39,10.5,Ink[hue],r.Width-padding-6,16,false);
    if(place)Draw(dc,c.Location,r.X+padding,r.Bottom-22,10.5,Ink[hue],r.Width-padding-6,16,false);
    dc.Pop();
   }
  }

  void Draw(DrawingContext dc,string value,double x,double y,double size,string color,double width,double height,bool bold,bool center=false){if(height<4||width<4)return;var t=new FormattedText(value??"",CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface(new FontFamily("Microsoft YaHei UI"),FontStyles.Normal,bold?FontWeights.SemiBold:FontWeights.Normal,FontStretches.Normal),Math.Max(8,size),Controller.B(color),VisualTreeHelper.GetDpi(this).PixelsPerDip);t.LineHeight=Math.Max(10,size*1.35);if(center)t.TextAlignment=TextAlignment.Center;t.MaxTextWidth=width;t.MaxTextHeight=Math.Max(10,height);t.Trimming=TextTrimming.CharacterEllipsis;dc.DrawText(t,new Point(x,y));}
  public CourseBoard(){Cursor=System.Windows.Input.Cursors.Arrow;MouseLeftButtonUp+=delegate(object s,System.Windows.Input.MouseButtonEventArgs e){var p=e.GetPosition(this);var hits=HitTargets.Where(x=>x.Item1.Contains(p)).Select(x=>x.Item2).ToList();if(hits.Count>0&&Open!=null)Open(hits.Last());};MouseMove+=delegate(object sender,System.Windows.Input.MouseEventArgs e){var hit=HitTargets.LastOrDefault(x=>x.Item1.Contains(e.GetPosition(this)));string next=hit==null?null:hit.Item2.Id;if(next==hoveredId)return;hoveredId=next;Cursor=hit==null?System.Windows.Input.Cursors.Arrow:System.Windows.Input.Cursors.Hand;ToolTip=hit==null?null:hit.Item2.Title+"\n"+hit.Item2.Start+"–"+hit.Item2.End+"\n"+hit.Item2.Location;InvalidateVisual();};MouseLeave+=delegate{hoveredId=null;ToolTip=null;Cursor=System.Windows.Input.Cursors.Arrow;InvalidateVisual();};SizeChanged+=delegate{InvalidateVisual();};}
 }
}
