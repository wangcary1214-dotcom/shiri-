param([string]$DesktopRoot=(Join-Path $PSScriptRoot '..\desktop'),[Parameter(Mandatory=$true)][string]$OutputDirectory,[string]$SignedExe)
$ErrorActionPreference='Stop'
$DesktopRoot=(Resolve-Path -LiteralPath $DesktopRoot).Path
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force $OutputDirectory,(Join-Path $OutputDirectory 'floral'),(Join-Path $OutputDirectory 'licenses') | Out-Null
foreach($name in @('Shiri.exe','Shiri.exe.config','OfflineOcr.ps1','PaddleOcr.ps1','Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.Wpf.dll','WebView2Loader.dll')){Copy-Item -LiteralPath (Join-Path $DesktopRoot $name) -Destination $OutputDirectory -Force}
if($SignedExe){Copy-Item -LiteralPath $SignedExe -Destination (Join-Path $OutputDirectory 'Shiri.exe') -Force}
Copy-Item -LiteralPath (Join-Path $DesktopRoot 'floral\dist') -Destination (Join-Path $OutputDirectory 'floral') -Recurse -Force
foreach($name in @('README.md','THIRD_PARTY_NOTICES.md','LICENSE.floral-notepaper')){Copy-Item -LiteralPath (Join-Path $DesktopRoot "floral\$name") -Destination (Join-Path $OutputDirectory 'floral') -Force}
foreach($name in @('LICENSE.txt','NOTICE.txt')){Copy-Item -LiteralPath (Join-Path $DesktopRoot "dependencies\webview2\$name") -Destination (Join-Path $OutputDirectory 'licenses') -Force}
$repoRoot=Split-Path -Parent (Split-Path -Parent $DesktopRoot)
Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE'),(Join-Path $repoRoot 'LICENSE_SCOPE.md'),(Join-Path $repoRoot 'PRIVACY.md'),(Join-Path $repoRoot 'CODE_SIGNING_POLICY.md') -Destination $OutputDirectory -Force
if(Get-ChildItem -LiteralPath $OutputDirectory -Directory -Recurse | Where-Object {$_.Name -in @('data','node_modules','.journal-webview2','preview-data')}){throw 'Private/cache directory in CI runtime output'}
$state=if($SignedExe){'Verified signed executable'}else{'UNSIGNED build; no SignPath approval is claimed'}
@"
Shiri Windows 2.0.0 x64
$state

Requires .NET Framework 4.7.2+ and Microsoft Edge WebView2 Runtime.
Keep all runtime files together. Back up and preserve your data folder before upgrading.
This source-build package uses the default application icon and contains no bundled PaddleOCR engine.
Course image recognition falls back to Windows OCR; install an appropriate OCR language if needed.
Source and license: https://github.com/wangcary1214-dotcom/shiri-
"@ | Set-Content -LiteralPath (Join-Path $OutputDirectory 'BUILD-INFO.txt') -Encoding utf8
Get-ChildItem -LiteralPath $OutputDirectory -File -Recurse | ForEach-Object {(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash+'  '+$_.FullName.Substring($OutputDirectory.Length+1)} | Set-Content -LiteralPath (Join-Path $OutputDirectory 'SHA256SUMS.txt') -Encoding utf8

