param([Parameter(Mandatory=$true)][string]$ProgramDirectory,[switch]$Tls,[switch]$Ui)
$ErrorActionPreference='Stop'
$ProgramDirectory=[IO.Path]::GetFullPath($ProgramDirectory)
$previous=$env:CHARKER_DATA_DIRECTORY
$env:CHARKER_DATA_DIRECTORY=Join-Path ([IO.Path]::GetTempPath()) ('ANKER-tests-'+[Guid]::NewGuid().ToString('N'))
try{
 $modes=@('--self-test');if($Tls){$modes+='--tls-self-test'};if($Ui){$modes+='--ui-test'}
 foreach($mode in $modes){
  $p=Start-Process -FilePath (Join-Path $ProgramDirectory 'ANKER.exe') -ArgumentList $mode -PassThru -WindowStyle Hidden
  if(!$p.WaitForExit(180000)){$p.Kill($true);throw "Test timeout: $mode"}
  if($p.ExitCode -ne 0){throw "Failed: $mode. See the test report in $ProgramDirectory"}
  Write-Output "PASS $mode"
 }
}finally{$env:CHARKER_DATA_DIRECTORY=$previous}
