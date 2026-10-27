using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Shiri {
 public static class FloralFiles {
  public static string ImportImages(string content,string markdownPath,FloralStore store){string directory=Path.GetFullPath(Path.GetDirectoryName(markdownPath))+Path.DirectorySeparatorChar;
   return Regex.Replace(content,@"(!\[[^\]\r\n]*\]\()([^\s)]+)(\))",delegate(Match match){string relative;try{relative=Uri.UnescapeDataString(match.Groups[2].Value);}catch{return match.Value;}
    if(relative.StartsWith("http:",StringComparison.OrdinalIgnoreCase)||relative.StartsWith("https:",StringComparison.OrdinalIgnoreCase)||relative.StartsWith("data:",StringComparison.OrdinalIgnoreCase))return match.Value;
    string path=Path.GetFullPath(Path.Combine(directory,relative.Replace('/',Path.DirectorySeparatorChar)));if(!path.StartsWith(directory,StringComparison.OrdinalIgnoreCase)||Path.IsPathRooted(relative))throw new Exception("Markdown图片必须位于所选文件所在目录中。");
    if(!File.Exists(path))return match.Value;string managed=store.AddImage(DiaryDocument.ReadImage(path));return match.Groups[1].Value+managed+match.Groups[3].Value;
   });
  }
  public static void Export(FloralNote note,string target,FloralStore store){string directory=Path.GetDirectoryName(target);foreach(Match match in Regex.Matches(note.content,@"images/([a-f0-9]{32}\.png)")){string name=match.Groups[1].Value;byte[] bytes;if(store.ImageDirectory==null){if(!store.MemoryImages.TryGetValue(name,out bytes))throw new IOException("图片缺失，未导出。");}else{string source=Path.Combine(store.ImageDirectory,name);if(!File.Exists(source))throw new IOException("图片缺失，未导出。");bytes=File.ReadAllBytes(source);}Directory.CreateDirectory(Path.Combine(directory,"images"));string output=Path.Combine(directory,"images",name);if(!File.Exists(output))File.WriteAllBytes(output,bytes);}
   File.WriteAllText(target,note.content,new UTF8Encoding(true));
  }
 }
}
