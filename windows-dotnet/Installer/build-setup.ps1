param(
 [Parameter(Mandatory=$true)][string]$Iscc,
 [Parameter(Mandatory=$true)][string]$PayloadDir,
 [Parameter(Mandatory=$true)][string]$PrerequisiteDir,
 [Parameter(Mandatory=$true)][string]$OutputDir
)
$ErrorActionPreference='Stop'
$runtime=Join-Path $PrerequisiteDir 'MicrosoftEdgeWebView2RuntimeInstallerX64.exe'
$signature=Get-AuthenticodeSignature -LiteralPath $runtime
if($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation'){
 throw 'The offline WebView2 installer must carry a valid Microsoft signature.'
}
foreach($file in @('ANKER.exe','ANKER.dll','coreclr.dll','UI/assets/AppIcon.ico','Bridge/node.exe','Bridge/anker-tls.mjs')){
 if(!(Test-Path -LiteralPath (Join-Path $PayloadDir $file))){throw "Missing payload: $file"}
}
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
& $Iscc ('/DPayloadDir='+[IO.Path]::GetFullPath($PayloadDir)) ('/DPrerequisiteDir='+[IO.Path]::GetFullPath($PrerequisiteDir)) ('/DOutputDir='+[IO.Path]::GetFullPath($OutputDir)) (Join-Path $PSScriptRoot 'ANKER.iss')
if($LASTEXITCODE -ne 0){throw 'Setup compilation failed'}
