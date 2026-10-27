param([Parameter(Mandatory=$true)][string]$ImagePath,[Parameter(Mandatory=$true)][string]$ResultPath,[Parameter(Mandatory=$true)][string]$EnginePath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$info=New-Object System.Diagnostics.ProcessStartInfo
$info.FileName=$EnginePath
$info.WorkingDirectory=Split-Path -Parent $EnginePath
$info.Arguments='-limit_side_len=1920 -cpu_threads=4'
$info.UseShellExecute=$false;$info.CreateNoWindow=$true
$info.RedirectStandardInput=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
$process=New-Object System.Diagnostics.Process
$process.StartInfo=$info
function Read-Line { $pending=$process.StandardOutput.ReadLineAsync();if(-not $pending.Wait(45000)){throw '离线引擎超时，请换用较小的图片。'};return $pending.Result }
try {
 [void]$process.Start();$errors=$process.StandardError.ReadToEndAsync()
 do{$line=Read-Line;if($null -eq $line){throw '离线引擎未能启动。'}}while($line -notmatch 'OCR init completed')
 # Base64 uses an anonymous local pipe, so Unicode paths never enter the native CLI parser.
 $request=@{image_base64=[Convert]::ToBase64String([IO.File]::ReadAllBytes($ImagePath))}|ConvertTo-Json -Compress
 $process.StandardInput.WriteLine($request)
 do{$line=Read-Line;if($null -eq $line){throw '离线引擎没有返回识别结果。'}}while(-not $line.StartsWith('{'))
 $result=$line|ConvertFrom-Json;if($result.code -ne 100){throw ('没有识别到可用文本：'+$result.data)}
 $bitmap=[Drawing.Image]::FromFile($ImagePath)
 try{$width=$bitmap.Width;$height=$bitmap.Height}finally{$bitmap.Dispose()}
 $rows=@();foreach($item in $result.data){$xs=@($item.box|ForEach-Object{$_[0]});$ys=@($item.box|ForEach-Object{$_[1]});$rows+=@{Text=$item.text;Score=$item.score;Left=($xs|Measure-Object -Minimum).Minimum;Right=($xs|Measure-Object -Maximum).Maximum;Top=($ys|Measure-Object -Minimum).Minimum;Bottom=($ys|Measure-Object -Maximum).Maximum}}
 $json=@{Engine='PaddleOCR-json 1.4.1';Width=$width;Height=$height;Lines=$rows}|ConvertTo-Json -Depth 6
 [IO.File]::WriteAllText($ResultPath,$json,[Text.Encoding]::UTF8)
} finally {if($process.Id -and -not $process.HasExited){$process.Kill()};$process.Dispose()}
