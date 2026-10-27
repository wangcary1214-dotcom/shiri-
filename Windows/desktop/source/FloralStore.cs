using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace Shiri {
 public sealed class FloralNote {
  public string id=Guid.NewGuid().ToString("N"),date=BeijingClock.Now.ToString("yyyy-MM-dd"),kind="diary",title="",content="",mood="",color="#F6F3EC";
  public int revision;
  public long updatedTicks;
  public bool deleted;
  public FloralNote Copy(){return (FloralNote)MemberwiseClone();}
 }
 public sealed class FloralData {public int version=1;public bool legacyImported;public List<FloralNote> notes=new List<FloralNote>();public List<string> pins=new List<string>();}
 public sealed class FloralStore {
  public static JavaScriptSerializer Json(){return new JavaScriptSerializer{MaxJsonLength=32*1024*1024,RecursionLimit=64};}
  public FloralData Data {get;private set;}
  public readonly string PathName;
  public string ImageDirectory {get{return PathName==null?null:Path.Combine(Path.GetDirectoryName(PathName),"journal-images");}}
  public readonly Dictionary<string,byte[]> MemoryImages=new Dictionary<string,byte[]>();
  public FloralStore(string path){PathName=path;Data=path!=null&&File.Exists(path)?Parse(File.ReadAllText(path,Encoding.UTF8)):new FloralData();}
  public static FloralData Parse(string text){if(text.Length>32*1024*1024)throw new Exception("日记与便签文件超过32 MB。");var data=Json().Deserialize<FloralData>(text);if(data==null||data.version!=1||data.notes==null||data.notes.Count>36600)throw new Exception("日记与便签格式无法识别，原文件已保留。");var ids=new HashSet<string>();foreach(var n in data.notes){Validate(n);if(!ids.Add(n.id))throw new Exception("便签编号重复。");}if(data.pins==null)data.pins=new List<string>();if(data.pins.Count>50||data.pins.Distinct().Count()!=data.pins.Count||data.pins.Any(x=>!ids.Contains(x)))throw new Exception("桌面便签状态无效。");return data;}
  public static void Validate(FloralNote n){DateTime d;Guid id;if(n==null||n.id==null||!(Guid.TryParseExact(n.id,"N",out id)||Regex.IsMatch(n.id,"^legacy-\\d{4}-\\d{2}-\\d{2}$"))||!DateTime.TryParseExact(n.date,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out d)||!(n.kind=="diary"||n.kind=="note")||n.title==null||n.title.Length>120||n.content==null||n.content.Length>100000||(n.mood??"").Length>20||!FocusStorage.ValidColor(n.color)||n.revision<0||n.updatedTicks<0||n.updatedTicks>DateTime.MaxValue.Ticks)throw new Exception("请检查便签日期、标题或正文（最多10万字）。");}
  public void Write(FloralData next){string text=Json().Serialize(next);Parse(text);if(PathName!=null){Directory.CreateDirectory(Path.GetDirectoryName(PathName));string temp=PathName+".tmp";try{using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){var bytes=new UTF8Encoding(false).GetBytes(text);stream.Write(bytes,0,bytes.Length);stream.Flush(true);}if(File.Exists(PathName))File.Replace(temp,PathName,PathName+".bak");else File.Move(temp,PathName);}catch{if(File.Exists(temp))try{File.Delete(temp);}catch{}throw;}}Data=next;}
  FloralData Clone(){return Parse(Json().Serialize(Data));}
  public FloralNote Save(FloralNote note){Validate(note);var next=Clone();var old=next.notes.FirstOrDefault(x=>x.id==note.id);if(old!=null&&old.revision!=note.revision)throw new Exception("此便签已在另一窗口更新，请先保留当前草稿并重新载入。");if(old==null&&note.revision!=0)throw new Exception("原便签已不存在，当前草稿仍保留。");var copy=note.Copy();copy.revision++;copy.updatedTicks=DateTime.UtcNow.Ticks;if(old!=null)next.notes.Remove(old);next.notes.Add(copy);Write(next);return copy;}
  public void SetPin(string id,bool pinned){var next=Clone();if(!next.notes.Any(x=>x.id==id))throw new Exception("便签不存在。");next.pins.Remove(id);if(pinned)next.pins.Add(id);Write(next);}
  public string AddImage(byte[] bytes){var normalized=DiaryDocument.EncodeImage(DiaryDocument.Decode(bytes));string name=Guid.NewGuid().ToString("N")+".png";if(ImageDirectory==null)MemoryImages[name]=normalized;else{Directory.CreateDirectory(ImageDirectory);File.WriteAllBytes(Path.Combine(ImageDirectory,name),normalized);}return "images/"+name;}
  public void Migrate(DiaryData diary,string legacyPath){if(Data.legacyImported)return;var next=Clone();if(diary!=null)foreach(var entry in diary.Entries){string id="legacy-"+entry.Date;if(next.notes.Any(x=>x.id==id))continue;var content=new StringBuilder();if(entry.Blocks==null)content.Append(Escape(entry.Text));else foreach(var p in entry.Blocks){foreach(var part in p.Parts){if(part.Image!=null){if(!DiaryDocument.ValidImage(part.Image))throw new Exception("旧日记图片路径无效。");if(ImageDirectory!=null){string source=Path.Combine(Path.GetDirectoryName(legacyPath),"diary-images",part.Image),target=Path.Combine(ImageDirectory,part.Image);Directory.CreateDirectory(ImageDirectory);if(File.Exists(source)&&!File.Exists(target))File.Copy(source,target);}content.Append("\n![日记图片](images/"+part.Image+")\n");}else{string value=Escape(part.Text);if(part.Bold)value="**"+value+"**";if(part.Italic)value="*"+value+"*";content.Append(value);}}content.Append("\n\n");}next.notes.Add(new FloralNote{id=id,date=entry.Date,title=entry.Title??"",content=content.ToString(),mood=entry.Mood??"",updatedTicks=entry.UpdatedTicks,revision=1});}if(legacyPath!=null&&File.Exists(legacyPath)){string backup=Path.Combine(Path.GetDirectoryName(legacyPath),"before-floral-diary.json");if(!File.Exists(backup))File.Copy(legacyPath,backup);}next.legacyImported=true;Write(next);}
  static string Escape(string text){return Regex.Replace((text??"").Replace("\r\n","\n"),@"([\\`*_{}\[\]()#+\-.!>|~])",@"\$1");}
 }
}
