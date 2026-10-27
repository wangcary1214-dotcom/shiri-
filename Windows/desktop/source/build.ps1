$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path -LiteralPath (Join-Path $framework 'csc.exe'))) {
    $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
}
$sourceDir = $PSScriptRoot
$outputDir = Split-Path -Parent $sourceDir
$references = @('System.dll', 'System.Web.Extensions.dll', 'System.Core.dll', 'System.Xaml.dll', 'System.Runtime.Serialization.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll', 'WPF\WindowsBase.dll', 'WPF\PresentationCore.dll', 'WPF\PresentationFramework.dll')
$compileArgs = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/utf8output', ('/out:' + (Join-Path $outputDir 'Shiri.exe')), ('/resource:' + (Join-Path $sourceDir 'Main.xaml') + ',Main.xaml'))
foreach ($reference in $references) { $compileArgs += '/reference:' + (Join-Path $framework $reference) }
$iconPath = Join-Path $sourceDir 'Shiri.ico'
if (Test-Path -LiteralPath $iconPath) { $compileArgs += '/win32icon:' + $iconPath }
$compileArgs += Join-Path $sourceDir 'Shiri.cs'
$compileArgs += Join-Path $sourceDir 'Pet.cs'
$compileArgs += Join-Path $sourceDir 'Focus.cs'
$compileArgs += Join-Path $sourceDir 'FocusView.cs'
$compileArgs += Join-Path $sourceDir 'FocusController.cs'
$compileArgs += Join-Path $sourceDir 'FocusPersistence.cs'
$compileArgs += Join-Path $sourceDir 'FocusStatistics.cs'
$compileArgs += Join-Path $sourceDir 'Goals.cs'
$compileArgs += Join-Path $sourceDir 'Sidebar.cs'
$compileArgs += Join-Path $sourceDir 'Presentation.cs'
$compileArgs += Join-Path $sourceDir 'Countdown.cs'
$compileArgs += Join-Path $sourceDir 'CountdownModel.cs'
$compileArgs += Join-Path $sourceDir 'Diary.cs'
$compileArgs += Join-Path $sourceDir 'DiaryDocument.cs'
$compileArgs += Join-Path $sourceDir 'FloralStore.cs'
$compileArgs += Join-Path $sourceDir 'FloralHost.cs'
$compileArgs += Join-Path $sourceDir 'FloralFiles.cs'
foreach($name in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.Wpf.dll')){
 $dependency=Join-Path $outputDir ('dependencies\webview2\lib\net462\'+$name)
 if(-not(Test-Path $dependency)){throw 'WebView2 dependencies missing; see floral/README.md'}
 $compileArgs += '/reference:'+$dependency
 Copy-Item -LiteralPath $dependency -Destination $outputDir -Force
}
Copy-Item -LiteralPath (Join-Path $outputDir 'dependencies\webview2\runtimes\win-x64\native\WebView2Loader.dll') -Destination $outputDir -Force
$compileArgs += Join-Path $sourceDir 'Timetable.cs'
$compileArgs += Join-Path $sourceDir 'CourseBoard.cs'
$compileArgs += Join-Path $sourceDir 'CourseDialog.cs'
$compileArgs += Join-Path $sourceDir 'LocalBackup.cs'
$compileArgs += Join-Path $sourceDir 'TransferModel.cs'
$compileArgs += Join-Path $sourceDir 'TimetableSettings.cs'
$compileArgs += Join-Path $sourceDir 'CourseImage.cs'
$compileArgs += Join-Path $sourceDir 'CourseRecognition.cs'
$compileArgs += '/resource:' + (Join-Path $sourceDir 'pet-dialogue.json') + ',pet-dialogue.json'
for ($cel = 0; $cel -lt 71; $cel++) {
    $number = $cel.ToString('000')
    $compileArgs += '/resource:' + (Join-Path $sourceDir ('cels\cel-' + $number + '.png')) + ',pet.cel.' + $number + '.png'
}
& (Join-Path $framework 'csc.exe') @compileArgs
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Copy-Item -LiteralPath (Join-Path $sourceDir 'Shiri.exe.config') -Destination (Join-Path $outputDir 'Shiri.exe.config')
Write-Output ('Built: ' + (Join-Path $outputDir 'Shiri.exe'))

Copy-Item -LiteralPath (Join-Path $sourceDir 'OfflineOcr.ps1') -Destination $outputDir






Copy-Item -LiteralPath (Join-Path $sourceDir 'PaddleOcr.ps1') -Destination $outputDir -Force
