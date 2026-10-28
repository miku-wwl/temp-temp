[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidateSet('Generate','Build','Preview')][string]$Task,
    [string]$BuildDirectory='',
    [string]$PreviewDirectory='',
    [string]$LogDirectory='artifacts/visual-qa/phase-5.2/b4/logs'
)
# Phase 5.2-B4 Unity tasks (one Unity instance at a time; nothing under game/Assets may change while it runs).
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
# relative paths are taken from the repository root (never the caller's directory: a log written under game/Assets loops the importer)
function Get-RepoPath([string]$path){ if([IO.Path]::IsPathRooted($path)){ return [IO.Path]::GetFullPath($path) } return [IO.Path]::GetFullPath((Join-Path $repo $path)) }
$logs=Get-RepoPath $LogDirectory
if($logs.StartsWith((Join-Path $repo 'game'),[StringComparison]::OrdinalIgnoreCase)){throw "Log directory must be outside the Unity project: $logs"}
New-Item -ItemType Directory -Force -Path $logs | Out-Null
$method=@{Generate='Echoes.EditorTools.Phase52B4Builder.Generate';Build='Echoes.EditorTools.Phase52B4Builder.BuildWindowsPilot';Preview='Echoes.EditorTools.Phase52B4Preview.Capture'}[$Task]
$unityArgs=@('-batchmode','-quit','-projectPath',('"'+(Join-Path $repo 'game')+'"'),'-executeMethod',$method,'-logFile',('"'+(Join-Path $logs ($Task+'.log'))+'"'))
if($Task -eq 'Build'){
    if(-not $BuildDirectory){throw 'Build requires a fresh -BuildDirectory'}
    $target=Get-RepoPath $BuildDirectory
    if(Test-Path -LiteralPath $target){throw "Build target already exists: $target"}
    $unityArgs+=@('-echoesBuildPath',('"'+$target+'"'))
}
if($Task -eq 'Preview'){
    if(-not $PreviewDirectory){throw 'Preview requires -PreviewDirectory'}
    $unityArgs+=@('-echoesPreviewDir',('"'+(Get-RepoPath $PreviewDirectory)+'"'))
}
$env:BEE_BUILD_THREADS='4'
$process=Start-Process -FilePath 'D:/Unity/Downloads/6000.3.27f1/Editor/Unity.exe' -ArgumentList $unityArgs -WindowStyle Hidden -PassThru -Wait
Write-Output "PHASE52B4_TASK=$Task EXIT=$($process.ExitCode) LOG=$(Join-Path $logs ($Task+'.log'))"
exit $process.ExitCode
