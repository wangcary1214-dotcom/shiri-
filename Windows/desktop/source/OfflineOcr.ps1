param([Parameter(Mandatory=$true)][string]$ImagePath,[Parameter(Mandatory=$true)][string]$ResultPath)
$ErrorActionPreference='Stop'
$engine=Join-Path $PSScriptRoot 'ocr\PaddleOCR-json.exe'
if(-not (Test-Path -LiteralPath $engine)){$engine=Join-Path (Split-Path -Parent $PSScriptRoot) 'ocr\PaddleOCR-json.exe'}
if(Test-Path -LiteralPath $engine){& (Join-Path $PSScriptRoot 'PaddleOcr.ps1') -ImagePath $ImagePath -ResultPath $ResultPath -EnginePath $engine;exit}

Add-Type -AssemblyName System.Runtime.WindowsRuntime
[Windows.Storage.StorageFile,Windows.Storage,ContentType=WindowsRuntime] | Out-Null
[Windows.Graphics.Imaging.BitmapDecoder,Windows.Foundation,ContentType=WindowsRuntime] | Out-Null
[Windows.Media.Ocr.OcrEngine,Windows.Foundation,ContentType=WindowsRuntime] | Out-Null
$asTask=([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.IsGenericMethod -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' } | Select-Object -First 1)
function Await($operation,[Type]$type){$task=$asTask.MakeGenericMethod($type).Invoke($null,@($operation));$task.Wait();$task.Result}
$file=Await ([Windows.Storage.StorageFile]::GetFileFromPathAsync($ImagePath)) ([Windows.Storage.StorageFile])
$stream=Await ($file.OpenReadAsync()) ([Windows.Storage.Streams.IRandomAccessStreamWithContentType])
try {
 $decoder=Await ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
  $originalWidth=$decoder.PixelWidth;$originalHeight=$decoder.PixelHeight
 $scale=[Math]::Min(2.5,[Windows.Media.Ocr.OcrEngine]::MaxImageDimension/[double][Math]::Max($originalWidth,$originalHeight))
 $transform=New-Object Windows.Graphics.Imaging.BitmapTransform
 $transform.ScaledWidth=[uint32]($originalWidth*$scale);$transform.ScaledHeight=[uint32]($originalHeight*$scale)
 $bitmap=Await ($decoder.GetSoftwareBitmapAsync([Windows.Graphics.Imaging.BitmapPixelFormat]::Bgra8,[Windows.Graphics.Imaging.BitmapAlphaMode]::Premultiplied,$transform,[Windows.Graphics.Imaging.ExifOrientationMode]::RespectExifOrientation,[Windows.Graphics.Imaging.ColorManagementMode]::DoNotColorManage)) ([Windows.Graphics.Imaging.SoftwareBitmap])
 try {
 [Windows.Globalization.Language,Windows.Globalization,ContentType=WindowsRuntime] | Out-Null
 $engine=[Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage([Windows.Globalization.Language]::new('zh-Hans-CN'))
 if($null -eq $engine){$engine=[Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()}
 if($null -eq $engine){throw 'Windows 未安装可用 OCR 语言包，请在系统语言设置中安装中文文字识别。'}
 $result=Await ($engine.RecognizeAsync($bitmap)) ([Windows.Media.Ocr.OcrResult])
 $rows=@()
 foreach($line in $result.Lines){$words=@($line.Words);if($words.Count -eq 0){continue};$rows+=@{Text=$line.Text;Left=(($words|ForEach-Object {$_.BoundingRect.X}|Measure-Object -Minimum).Minimum)/$scale;Top=(($words|ForEach-Object {$_.BoundingRect.Y}|Measure-Object -Minimum).Minimum)/$scale;Right=(($words|ForEach-Object {$_.BoundingRect.X+$_.BoundingRect.Width}|Measure-Object -Maximum).Maximum)/$scale;Bottom=(($words|ForEach-Object {$_.BoundingRect.Y+$_.BoundingRect.Height}|Measure-Object -Maximum).Maximum)/$scale}}
 $json=@{Width=$originalWidth;Height=$originalHeight;Lines=@($rows)}|ConvertTo-Json -Depth 5
 [IO.File]::WriteAllText($ResultPath,$json,[Text.Encoding]::UTF8)
 } finally {$bitmap.Dispose()}
} finally {$stream.Dispose()}
