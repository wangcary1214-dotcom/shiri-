param([string]$DesktopRoot=(Join-Path $PSScriptRoot '..\desktop'))
$ErrorActionPreference='Stop'
$version='1.0.3650.58'
$expected='911A472128C82AC8BAA0C486C23342CC9DD6E7DC50D754E676726642CA065C60'
$dependencies=Join-Path $DesktopRoot 'dependencies'
New-Item -ItemType Directory -Force $dependencies | Out-Null
$package=Join-Path $dependencies "webview2.$version.nupkg"
if(-not(Test-Path -LiteralPath $package)){Invoke-WebRequest -Uri "https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/$version/microsoft.web.webview2.$version.nupkg" -OutFile $package}
if((Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash -ne $expected){throw 'WebView2 NuGet package hash mismatch.'}
$destination=Join-Path $dependencies 'webview2'
if(-not(Test-Path -LiteralPath (Join-Path $destination 'lib\net462\Microsoft.Web.WebView2.Wpf.dll'))){Add-Type -AssemblyName System.IO.Compression.FileSystem;[IO.Compression.ZipFile]::ExtractToDirectory($package,$destination)}
Write-Output "WebView2 SDK restored and SHA256 verified: $version"
