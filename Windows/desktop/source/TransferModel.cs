using System;using System.Collections.Generic;using System.Linq;using System.IO;using System.Runtime.Serialization.Json;
namespace Shiri {
 public static class TransferModel {
  public static string Fingerprint<T>(T value){using(var s=new MemoryStream()){new DataContractJsonSerializer(typeof(T)).WriteObject(s,value);return Convert.ToBase64String(s.ToArray());}}
  public static List<T> MergeRows<T>(List<T> local,List<T> incoming,Func<T,string> id,bool wins){var result=new List<T>(local);foreach(var item in incoming){int index=result.FindIndex(x=>id(x)==id(item));if(index<0)result.Add(item);else if(wins)result[index]=item;}return result;}
  public static int[] Count<T>(List<T> local,List<T> incoming,Func<T,string> id){int added=0,changed=0;foreach(var item in incoming){var matches=local.Where(x=>id(x)==id(item)).ToList();if(matches.Count==0)added++;else if(Fingerprint(matches[0])!=Fingerprint(item))changed++;}return new[]{added,changed};}
  public static Store Merge(Store local,Store incoming,bool wins){var result=Data.Parse(Data.Serialize(local));result.Tasks=MergeRows(result.Tasks,incoming.Tasks,x=>x.Id,wins);result.Goals=MergeRows(result.Goals,incoming.Goals,x=>x.Id,wins);result.Courses=MergeRows(result.Courses,incoming.Courses,x=>x.Id,wins);result.Countdowns=MergeRows(result.Countdowns,incoming.Countdowns,x=>x.Id,wins);if(incoming.HasTimetable&&wins)result.Timetable=incoming.Timetable;return Data.Parse(Data.Serialize(result));}
 }
}
