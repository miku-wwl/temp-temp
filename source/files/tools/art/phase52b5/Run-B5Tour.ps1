[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$PlayerDirectory,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [int]$Width=1280,[int]$Height=720,
    [switch]$Video,
    [switch]$Before,
    [int]$TimeoutSeconds=480
)
# One automated B5 evidence run in the real Windows player (AUTOMATED input, isolated save folder, fresh output folder).
# -Before starts the same build in the B4 presentation ("-echoes-b5-before") for a matched comparison on the same route.
# While the player runs, Windows counters are sampled once a second for this process only: GPU memory (dedicated and shared; the
# Radeon 780M is an integrated GPU, so most of its memory is shared system memory), 3D engine utilisation and the private working set.
# The time from launch to the tour's ready flag is the loading time (menu, scene load, first frames).
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
function Get-RepoPath([string]$path){ if([IO.Path]::IsPathRooted($path)){ return [IO.Path]::GetFullPath($path) } return [IO.Path]::GetFullPath((Join-Path $repo $path)) }
$exe=(Resolve-Path (Join-Path (Get-RepoPath $PlayerDirectory) 'EchoesOfTheValley.exe')).Path
$root=Get-RepoPath $OutputDirectory
if(Test-Path -LiteralPath $root){throw "Preserve prior evidence; fresh output required: $root"}
New-Item -ItemType Directory -Path $root | Out-Null
$log=Join-Path $root 'player.log'
$arguments=@('-screen-fullscreen','0','-screen-width',$Width,'-screen-height',$Height,'-echoes-b5-tour','-echoes-tour-output',('"'+$root+'"'),'-echoes-save-dir',('"'+(Join-Path $root 'save')+'"'),'-logFile',('"'+$log+'"'))
if($Video){$arguments+='-echoes-b5-record-frames'}
if($Before){$arguments+='-echoes-b5-before'}
$others=@(Get-Process -Name EchoesOfTheValley -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
$started=Get-Date
$process=Start-Process -FilePath $exe -ArgumentList $arguments -WorkingDirectory (Split-Path $exe) -PassThru
$id=$process.Id
$samples=New-Object System.Collections.Generic.List[object]
$readySeconds=$null
$deadline=$started.AddSeconds($TimeoutSeconds)
while(-not $process.HasExited -and (Get-Date) -lt $deadline){
    if($null -eq $readySeconds -and (Test-Path -LiteralPath (Join-Path $root 'ready.flag'))){ $readySeconds=[math]::Round(((Get-Date)-$started).TotalSeconds,2) }
    try{
        $c=Get-Counter -Counter @("\GPU Process Memory(pid_$($id)_*)\Dedicated Usage","\GPU Process Memory(pid_$($id)_*)\Shared Usage","\GPU Engine(pid_$($id)_*engtype_3D)\Utilization Percentage") -ErrorAction Stop
        $dedicated=0.0;$shared=0.0;$gpu=0.0
        foreach($s in $c.CounterSamples){
            if($s.Path -like '*dedicated usage'){$dedicated+=$s.CookedValue}
            elseif($s.Path -like '*shared usage'){$shared+=$s.CookedValue}
            else{$gpu+=$s.CookedValue}
        }
        $process.Refresh()
        $samples.Add([ordered]@{t=[math]::Round(((Get-Date)-$started).TotalSeconds,2);dedicatedMB=[math]::Round($dedicated/1MB,1);sharedMB=[math]::Round($shared/1MB,1);gpu3dPercent=[math]::Round($gpu,1);privateMB=[math]::Round($process.PrivateMemorySize64/1MB,1);ready=($null -ne $readySeconds)})
    }catch{}
    Start-Sleep -Milliseconds 700
}
$natural=$process.HasExited
if(-not $natural){$process.Kill();$process.WaitForExit()}
$seconds=[math]::Round(((Get-Date)-$started).TotalSeconds,1)
$exceptions=@(Select-String -LiteralPath $log -Pattern 'Exception|NullReference|error CS|Shader error|LogError|Assertion failed' -CaseSensitive:$false).Count
if($Video -and $natural -and $process.ExitCode -eq 0){
    & python -I (Join-Path $PSScriptRoot 'encode_b5_video.py') $root
    if($LASTEXITCODE -ne 0){throw 'Framebuffer video encode/validation failed'}
}
$running=@($samples | Where-Object { $_.ready })
function Stat($list,[string]$key){ if($list.Count -eq 0){return $null}; $v=@($list | ForEach-Object { $_[$key] } | Sort-Object); return [ordered]@{mean=[math]::Round(($v|Measure-Object -Average).Average,1);p95=$v[[math]::Min($v.Count-1,[int]($v.Count*0.95))];max=$v[-1]} }
$resources=[ordered]@{note='Windows performance counters for this process only, sampled about once a second after the tour was ready; integrated GPU: shared memory is the GPU memory in use';samples=$running.Count;gpuDedicatedMB=(Stat $running 'dedicatedMB');gpuSharedMB=(Stat $running 'sharedMB');gpu3dUtilisationPercent=(Stat $running 'gpu3dPercent');privateMB=(Stat $running 'privateMB')}
$samples | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $root 'resource-samples.json') -Encoding UTF8
$result=[ordered]@{width=$Width;height=$Height;naturalExit=$natural;exitCode=$(if($natural){$process.ExitCode}else{124});seconds=$seconds;secondsToReady=$readySeconds;exceptionLines=$exceptions;video=$Video.IsPresent;before=$Before.IsPresent;automatedInput=$true;otherGameProcessesRunning=$others;resources=$resources}
$result|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $root 'process-result.json') -Encoding UTF8
$result|ConvertTo-Json -Compress -Depth 4|Write-Output
if(-not $natural -or $process.ExitCode -ne 0 -or $exceptions -ne 0){exit 1}
