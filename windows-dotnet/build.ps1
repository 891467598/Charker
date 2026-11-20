param(
 [string]$Dotnet='dotnet', [string]$Node='node', [string]$Npm='npm',
 [string]$Output='dist', [switch]$SkipRestore, [switch]$SkipNpm, [switch]$SkipTests
)
$ErrorActionPreference='Stop'
$target=if([IO.Path]::IsPathRooted($Output)){$Output}else{Join-Path $PSScriptRoot $Output}
$project=Join-Path $PSScriptRoot 'ANKER.Windows.csproj'
if(!$SkipNpm){
 Push-Location (Join-Path $PSScriptRoot 'Bridge')
 try { & $Npm ci --ignore-scripts --no-audit --no-fund; if($LASTEXITCODE -ne 0){throw 'npm dependency restore failed'} } finally { Pop-Location }
}
if(!$SkipRestore){
 & $Dotnet restore $project --locked-mode --runtime win-x64 --configfile (Join-Path $PSScriptRoot 'NuGet.Config')
 if($LASTEXITCODE -ne 0){throw '.NET dependency restore failed'}
}
& $Dotnet publish $project -c Release -r win-x64 --self-contained true --no-restore -o $target -p:DebugType=None -p:DebugSymbols=false ('-p:PathMap='+$PSScriptRoot+'=/_/src/windows-dotnet')
if($LASTEXITCODE -ne 0){throw '.NET publish failed'}
New-Item -ItemType Directory -Force (Join-Path $target 'Bridge') | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Bridge/node_modules') -Destination (Join-Path $target 'Bridge') -Recurse -Force
Copy-Item -Path (Join-Path $PSScriptRoot 'Bridge/*.mjs'),(Join-Path $PSScriptRoot 'Bridge/package.json'),(Join-Path $PSScriptRoot 'Bridge/package-lock.json') -Destination (Join-Path $target 'Bridge') -Force
Copy-Item -LiteralPath (Get-Command $Node).Source -Destination (Join-Path $target 'Bridge/node.exe') -Force
foreach($file in @('LICENSE','THIRD-PARTY-NOTICES.md','WINDOWS-NOTICES.md','README.md')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $target -Force}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'licenses') -Destination $target -Recurse -Force
if(!$SkipTests){ & (Join-Path $PSScriptRoot 'Tests/run-native.ps1') -ProgramDirectory $target }
Write-Output "Ready: $target/ANKER.exe"
