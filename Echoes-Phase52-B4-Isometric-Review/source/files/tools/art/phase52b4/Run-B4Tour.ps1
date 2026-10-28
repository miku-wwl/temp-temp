[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$PlayerDirectory,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [int]$Width=1280,[int]$Height=720,
    [switch]$Video,
    [switch]$Before,
    [int]$TimeoutSeconds=420
)
# One automated B4 evidence run in the real Windows player (AUTOMATED input, isolated save folder, fresh output folder).
# -Before starts the same build in the B3 presentation ("-echoes-b4-before") for a matched performance comparison.
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
function Get-RepoPath([string]$path){ if([IO.Path]::IsPathRooted($path)){ return [IO.Path]::GetFullPath($path) } return [IO.Path]::GetFullPath((Join-Path $repo $path)) }
$exe=(Resolve-Path (Join-Path (Get-RepoPath $PlayerDirectory) 'EchoesOfTheValley.exe')).Path
$root=Get-RepoPath $OutputDirectory
if(Test-Path -LiteralPath $root){throw "Preserve prior evidence; fresh output required: $root"}
New-Item -ItemType Directory -Path $root | Out-Null
$log=Join-Path $root 'player.log'
$arguments=@('-screen-fullscreen','0','-screen-width',$Width,'-screen-height',$Height,'-echoes-b4-tour','-echoes-tour-output',('"'+$root+'"'),'-echoes-save-dir',('"'+(Join-Path $root 'save')+'"'),'-logFile',('"'+$log+'"'))
if($Video){$arguments+='-echoes-b4-record-frames'}
if($Before){$arguments+='-echoes-b4-before'}
$others=@(Get-Process -Name EchoesOfTheValley -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
$started=Get-Date
$process=Start-Process -FilePath $exe -ArgumentList $arguments -WorkingDirectory (Split-Path $exe) -PassThru
$natural=$process.WaitForExit($TimeoutSeconds*1000)
if(-not $natural){$process.Kill()}
$seconds=[math]::Round(((Get-Date)-$started).TotalSeconds,1)
$exceptions=@(Select-String -LiteralPath $log -Pattern 'Exception|NullReference|error CS|Shader error|LogError|Assertion failed' -CaseSensitive:$false).Count
if($Video -and $natural -and $process.ExitCode -eq 0){
    & python -I (Join-Path $PSScriptRoot 'encode_b4_video.py') $root
    if($LASTEXITCODE -ne 0){throw 'Framebuffer video encode/validation failed'}
}
$result=[ordered]@{width=$Width;height=$Height;naturalExit=$natural;exitCode=$(if($natural){$process.ExitCode}else{124});seconds=$seconds;exceptionLines=$exceptions;video=$Video.IsPresent;before=$Before.IsPresent;automatedInput=$true;otherGameProcessesRunning=$others}
$result|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $root 'process-result.json') -Encoding UTF8
$result|ConvertTo-Json -Compress|Write-Output
if(-not $natural -or $process.ExitCode -ne 0 -or $exceptions -ne 0){exit 1}
