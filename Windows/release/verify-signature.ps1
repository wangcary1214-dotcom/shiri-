param([Parameter(Mandatory=$true)][string]$File,[string]$ExpectedSubject,[switch]$RequireTimestamp)
$ErrorActionPreference='Stop'
$signature=Get-AuthenticodeSignature -LiteralPath $File
if($signature.Status -ne 'Valid'){throw "Signature is not valid: $($signature.Status) - $($signature.StatusMessage)"}
if($ExpectedSubject -and $signature.SignerCertificate.Subject -ne $ExpectedSubject){throw "Unexpected publisher: $($signature.SignerCertificate.Subject)"}
if($RequireTimestamp -and -not $signature.TimeStamperCertificate){throw 'A trusted timestamp is required.'}
[PSCustomObject]@{File=(Resolve-Path -LiteralPath $File).Path;Status=$signature.Status;Publisher=$signature.SignerCertificate.Subject;TimestampAuthority=$signature.TimeStamperCertificate.Subject;SHA256=(Get-FileHash -LiteralPath $File -Algorithm SHA256).Hash}
