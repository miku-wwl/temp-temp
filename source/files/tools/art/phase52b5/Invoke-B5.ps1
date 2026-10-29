[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidateSet('Survey','Generate','Build','Preview')][string]$Task,
    [string]$BuildDirectory='',
    [string]$PreviewDirectory='',
    [string]$Scene='',
    [string]$LogDirectory='artifacts/visual-qa/phase-5.2/b5/logs'
)
# Phase 5.2-B5 Unity tasks (one Unity instance at a time; nothing under game/Assets may change while it runs).
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
# relative paths are taken from the repository root (never the caller's directory: a log written under game/Assets loops the importer)
function Get-RepoPath([string]$path){ if([IO.Path]::IsPathRooted($path)){ return [IO.Path]::GetFullPath($path) } return [IO.Path]::GetFullPath((Join-Path $repo $path)) }
$logs=Get-RepoPath $LogDirectory
if($logs.StartsWith((Join-Path $repo 'game'),[StringComparison]::OrdinalIgnoreCase)){throw "Log directory must be outside the Unity project: $logs"}
New-Item -ItemType Directory -Force -Path $logs | Out-Null
$running=@(Get-Process -Name 'Unity' -ErrorAction SilentlyContinue)
if($running.Count -gt 0){throw "Another Unity process is running (PID $($running.Id -join ', ')); one Unity instance at a time."}
$method=@{Survey='Echoes.EditorTools.Phase52B5Survey.Run';Generate='Echoes.EditorTools.Phase52B5Builder.Generate';Build='Echoes.EditorTools.Phase52B5Builder.BuildWindowsPilot';Preview='Echoes.EditorTools.Phase52B5Preview.Capture'}[$Task]
$log=Join-Path $logs ($Task+'.log')
if(Test-Path -LiteralPath $log){ $log=Join-Path $logs ($Task+'-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.log') }
$unityArgs=@('-batchmode','-quit','-projectPath',('"'+(Join-Path $repo 'game')+'"'),'-executeMethod',$method,'-logFile',('"'+$log+'"'))
if($Task -eq 'Build'){
    if(-not $BuildDirectory){throw 'Build requires a fresh -BuildDirectory'}
    $target=Get-RepoPath $BuildDirectory
    if(Test-Path -LiteralPath $target){throw "Build target already exists: $target"}
    $unityArgs+=@('-echoesBuildPath',('"'+$target+'"'))
}
if($Task -eq 'Preview' -or $Task -eq 'Survey'){
    if(-not $PreviewDirectory){throw "$Task requires -PreviewDirectory"}
    $unityArgs+=@('-echoesPreviewDir',('"'+(Get-RepoPath $PreviewDirectory)+'"'))
}
if($Scene){ $unityArgs+=@('-echoesScene',$Scene) }
$env:BEE_BUILD_THREADS='4'
$process=Start-Process -FilePath 'D:/Unity/Downloads/6000.3.27f1/Editor/Unity.exe' -ArgumentList $unityArgs -WindowStyle Hidden -PassThru -Wait
Write-Output "PHASE52B5_TASK=$Task EXIT=$($process.ExitCode) LOG=$log"
exit $process.ExitCode
