using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Shiri {
 [DataContract]public sealed class DiaryPart {
  [DataMember]public string Text="";
  [DataMember(EmitDefaultValue=false)]public string Image;
  [DataMember(EmitDefaultValue=false)]public bool Bold;
  [DataMember(EmitDefaultValue=false)]public bool Italic;
 }
 [DataContract]public sealed class DiaryParagraph {
  [DataMember]public List<DiaryPart> Parts=new List<DiaryPart>();
 }
 // Only explicit text, styling flags and managed image names are persisted. No executable XAML.
 public static class DiaryDocument {
  public static bool ValidImage(string name){Guid id;return name!=null&&name.Length==36&&name.EndsWith(".png",StringComparison.Ordinal)&&Guid.TryParseExact(name.Substring(0,32),"N",out id);}
  public static IEnumerable<Paragraph> Paragraphs(BlockCollection blocks){foreach(Block b in blocks){var p=b as Paragraph;if(p!=null)yield return p;var list=b as List;if(list!=null)foreach(ListItem item in list.ListItems)foreach(var child in Paragraphs(item.Blocks))yield return child;var section=b as Section;if(section!=null)foreach(var child in Paragraphs(section.Blocks))yield return child;}}
  static void ReadInline(InlineCollection inlines,List<DiaryPart> parts){foreach(Inline inline in inlines){var run=inline as Run;var span=inline as Span;var ui=inline as InlineUIContainer;if(run!=null)parts.Add(new DiaryPart{Text=run.Text,Bold=run.FontWeight==FontWeights.Bold,Italic=run.FontStyle==FontStyles.Italic});else if(inline is LineBreak)parts.Add(new DiaryPart{Text="\n"});else if(span!=null)ReadInline(span.Inlines,parts);else if(ui!=null&&ui.Child is Image){string name=((Image)ui.Child).Tag as string;if(ValidImage(name))parts.Add(new DiaryPart{Image=name});}else if(ui!=null&&ui.Child is Border){string name=((Border)ui.Child).Tag as string;if(ValidImage(name))parts.Add(new DiaryPart{Image=name});}}}
  public static List<DiaryParagraph> Capture(FlowDocument document){var result=new List<DiaryParagraph>();foreach(var p in Paragraphs(document.Blocks)){var row=new DiaryParagraph();ReadInline(p.Inlines,row.Parts);result.Add(row);}return result;}
  public static string PlainText(List<DiaryParagraph> blocks){return String.Join("\r\n",blocks.Select(p=>String.Concat(p.Parts.Where(r=>r.Image==null).Select(r=>r.Text)))).TrimEnd('\r','\n');}
  public static int ImageCount(List<DiaryParagraph> blocks){return blocks==null ? 0 : blocks.Sum(p=>p.Parts.Count(r=>r.Image!=null));}
  public static byte[] EncodeImage(BitmapSource source){if(source==null||source.PixelWidth<=0||source.PixelHeight<=0||(long)source.PixelWidth*source.PixelHeight>50000000)throw new Exception("图片尺寸过大，请选择小于 5000 万像素的图片。");double scale=Math.Min(1,1600.0/Math.Max(source.PixelWidth,source.PixelHeight));if(scale<1)source=new TransformedBitmap(source,new ScaleTransform(scale,scale));var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(source));using(var stream=new MemoryStream()){encoder.Save(stream);if(stream.Length>20*1024*1024)throw new Exception("图片过大，请压缩后再插入。");return stream.ToArray();}}
  public static byte[] ReadImage(string path){if(new FileInfo(path).Length>25*1024*1024)throw new Exception("单张图片不能超过 25 MB。");using(var stream=File.OpenRead(path)){var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None);var frame=decoder.Frames[0];if((long)frame.PixelWidth*frame.PixelHeight>50000000)throw new Exception("图片像素过大，请缩小后再插入。");stream.Position=0;var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=stream;double scale=Math.Min(1,1600.0/Math.Max(frame.PixelWidth,frame.PixelHeight));image.DecodePixelWidth=Math.Max(1,(int)(frame.PixelWidth*scale));image.EndInit();image.Freeze();return EncodeImage(image);}}
  public static BitmapSource Decode(byte[] bytes){using(var stream=new MemoryStream(bytes)){var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=stream;image.EndInit();image.Freeze();return image;}}
  static string Html(string value){return (value??"").Replace("&","&amp;").Replace("<","&lt;").Replace(">","&gt;").Replace("\"","&quot;");}
  public static string ExportHtml(DiaryEntry entry,Func<string,byte[]> image){var html=new StringBuilder("<!doctype html><html lang=\"zh-CN\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>"+Html(entry.Title??entry.Date)+"</title><style>body{max-width:760px;margin:48px auto;padding:0 24px;background:#f8f9f6;color:#30493d;font:17px/1.9 sans-serif}header{color:#81917e;font-size:14px}h1{font-size:30px}p{white-space:pre-wrap}img{display:block;max-width:100%;height:auto;border-radius:10px;margin:20px 0}</style><header>"+Html(entry.Date)+" · "+Html(entry.Mood)+"</header><h1>"+Html(String.IsNullOrWhiteSpace(entry.Title)?"每日手记":entry.Title)+"</h1>");if(entry.Blocks==null)html.Append("<p>"+Html(entry.Text)+"</p>");else foreach(var p in entry.Blocks){html.Append("<p>");foreach(var part in p.Parts){if(part.Image!=null){byte[] bytes=image(part.Image);if(bytes==null)throw new IOException("图片暂时无法读取，未导出。请先恢复 diary-images 文件夹。");html.Append("<img alt=\"日记图片\" src=\"data:image/png;base64,"+Convert.ToBase64String(bytes)+"\">");}else{string text=Html(part.Text);if(part.Bold)text="<strong>"+text+"</strong>";if(part.Italic)text="<em>"+text+"</em>";html.Append(text);}}html.Append("</p>");}return html.Append("</html>").ToString();}
 }
}
