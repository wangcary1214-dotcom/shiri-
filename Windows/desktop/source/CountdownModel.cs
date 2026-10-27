using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;

namespace Shiri {
 [DataContract]public sealed class CountdownItem {
  [DataMember]public string Id=Guid.NewGuid().ToString("N");
  [DataMember]public string Title="期待的日子";
  [DataMember]public string Mode="Date";
  [DataMember]public string Date=BeijingClock.Now.Date.AddDays(30).ToString("yyyy-MM-dd");
  [DataMember]public string Color="#356A51";
  [DataMember]public string Icon="";
  [DataMember]public int LifeYears=90;
  public CountdownItem Copy(){return (CountdownItem)MemberwiseClone();}
 }
 public sealed class CountdownReading {
  public string Value,Unit,Detail,CompactDetail;
  public double Progress;
  public bool HasProgress;
 }
 public static class CountdownModel {
  public static readonly string[] Modes={"Date","Anniversary","Year","Quarter","Month","Day","Life"};
  public static readonly string[] Labels={"日期倒计时","纪念日正计时","年进度","季度进度","月进度","日进度","人生进度"};
  public static bool TryDate(string value,out DateTime date){return DateTime.TryParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out date);}
  public static void Normalize(AppPreferences preferences) {
   if(preferences.Countdowns==null) {
    preferences.Countdowns=new List<CountdownItem>();DateTime legacy;
    if(TryDate(preferences.CountdownDate,out legacy))preferences.Countdowns.Add(new CountdownItem{Id="legacy-countdown",Title=String.IsNullOrWhiteSpace(preferences.CountdownTitle) ? "期待的日子" : preferences.CountdownTitle,Date=preferences.CountdownDate});
   }
   if(preferences.Countdowns.Count>100)throw new Exception("最多保留 100 个倒计时或进度项目。");
   var ids=new HashSet<string>();
   foreach(var item in preferences.Countdowns){string error=Error(item);if(error!=null||!ids.Add(item.Id))throw new Exception(error??"倒计时编号重复。");item.Icon=item.Icon??"";}
   preferences.CountdownTitle=null;preferences.CountdownDate=null;
  }
  public static string Error(CountdownItem item) {
   if(item==null||String.IsNullOrWhiteSpace(item.Id)||item.Id.Length>80)return "倒计时编号无效。";
   if(String.IsNullOrWhiteSpace(item.Title)||item.Title.Length>60)return "名称需要 1–60 个字。";
   if(!Modes.Contains(item.Mode))return "倒计时类型无法识别。";
   if(!FocusStorage.ValidColor(item.Color))return "请输入有效颜色，例如 #688FBA。";
   if(item.Icon!=null&&item.Icon.Length>16)return "图标请使用一个表情或短符号。";
   DateTime date;
   if(new[]{"Date","Anniversary","Life"}.Contains(item.Mode)&&!TryDate(item.Date,out date))return "请选择有效日期。";
   if(item.Mode=="Life"&&(item.LifeYears<1||item.LifeYears>150||!TryDate(item.Date,out date)||date.Year+item.LifeYears>9999))return "人生进度的预计年数需要为 1–150，且结束日期须有效。";
   return null;
  }
  public static CountdownReading Measure(CountdownItem item,DateTime beijing) {
   string error=Error(item);if(error!=null)throw new ArgumentException(error);
   DateTime date;
   if(item.Mode=="Date"||item.Mode=="Anniversary") {
    TryDate(item.Date,out date);int delta=(date-beijing.Date).Days;
    string suffix=delta>0 ? "还有" : delta<0 ? "已过去" : "就是今天";
    return new CountdownReading{Value=Math.Abs(delta).ToString(),Unit="天",Detail=date.ToString("yyyy 年 M 月 d 日")+" · "+suffix,CompactDetail=date.ToString("M月d日")+" · "+suffix};
   }
   DateTime start,end;string prefix,detail;
   if(item.Mode=="Year"){start=new DateTime(beijing.Year,1,1);end=start.AddYears(1);prefix="按年";}
   else if(item.Mode=="Quarter"){start=new DateTime(beijing.Year,(beijing.Month-1)/3*3+1,1);end=start.AddMonths(3);prefix="按季度";}
   else if(item.Mode=="Month"){start=new DateTime(beijing.Year,beijing.Month,1);end=start.AddMonths(1);prefix="按月";}
   else if(item.Mode=="Day"){start=beijing.Date;end=start.AddDays(1);prefix="按日";}
   else{TryDate(item.Date,out start);end=start.AddYears(item.LifeYears);prefix="一生";}
   double elapsed=Math.Max(0,(beijing-start).TotalSeconds),total=(end-start).TotalSeconds,progress=Math.Max(0,Math.Min(1,elapsed/total));
   if(item.Mode=="Day")detail="按日 · "+Math.Min(24,elapsed/3600).ToString("0.#",CultureInfo.InvariantCulture)+"时 / 24时 · 已过去";
   else if(item.Mode=="Life")detail="一生 · "+(elapsed/86400/365.2425).ToString("0.##",CultureInfo.InvariantCulture)+"岁 / "+item.LifeYears+"岁 · 已过去";
   else detail=prefix+" · "+(int)Math.Min((end-start).TotalDays,elapsed/86400)+"天 / "+(int)(end-start).TotalDays+"天 · 已过去";
   return new CountdownReading{HasProgress=true,Progress=progress,Value=Math.Floor(progress*100+.00000001).ToString("0"),Unit="%",Detail=detail,CompactDetail=prefix+" · 已过去"};
  }
 }
}
