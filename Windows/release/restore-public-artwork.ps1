param([string]$SourceRoot=(Join-Path $PSScriptRoot '..\desktop\source'))
$ErrorActionPreference='Stop'
# Existing publicly hosted frames only; no local artwork is exported to this repository.
$commit='804f7353ab1a98936b50ce6a065f99357fd8c9fe'
$manifest=Join-Path $PSScriptRoot 'artwork.sha256'
$frames=Join-Path $SourceRoot 'cels'
New-Item -ItemType Directory -Force $frames | Out-Null
foreach($line in Get-Content -LiteralPath $manifest){
 if($line -notmatch '^([a-fA-F0-9]{64})\s+(cel-\d{3}\.png)$'){throw 'Invalid artwork manifest entry'}
 $expected=$Matches[1];$name=$Matches[2];$target=Join-Path $frames $name
 if(Test-Path -LiteralPath $target){if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $expected){throw "Local frame differs from the approved frame: $name"};continue}
 $temporary=$target+'.download'
 try{Invoke-WebRequest -Uri "https://raw.githubusercontent.com/wangcary1214-dotcom/shiri-web/$commit/assets/pet/$name" -OutFile $temporary
 if((Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash -ne $expected){throw "Public artwork hash mismatch: $name"}
 Move-Item -LiteralPath $temporary -Destination $target
 }finally{if(Test-Path -LiteralPath $temporary){Remove-Item -LiteralPath $temporary -Force}}
}
Write-Output 'Restored and verified 71 frames from the existing public web repository.'
