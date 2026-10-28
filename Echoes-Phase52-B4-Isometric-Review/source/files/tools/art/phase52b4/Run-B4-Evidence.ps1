[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$PlayerDirectory,
    [string]$EvidenceRoot='artifacts/visual-qa/phase-5.2/b4/player'
)
# Runs every Windows-player evidence capture for the B4 pilot, one player at a time, recording each result honestly.
# All input is AUTOMATED (PlayerController.InputOverride, FixedFollowCamera25D zoom, InteractionDetector); no human played.
$ErrorActionPreference='Continue'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
Set-Location $repo
$summary=[ordered]@{}
function Step([string]$name,[scriptblock]$body){
    Write-Output "=== $name $(Get-Date -Format HH:mm:ss)"
    try{& $body;$summary[$name]=if($LASTEXITCODE -eq 0 -or $null -eq $LASTEXITCODE){'EXIT_0'}else{'EXIT_'+$LASTEXITCODE}}catch{$summary[$name]='ERROR: '+$_.Exception.Message;Write-Output $_.Exception.Message}
}
$tour=Join-Path $repo 'tools/art/phase52b4/Run-B4Tour.ps1'
$smoke=Join-Path $repo 'tools/art/phase52b2/Run-PilotSmoke.ps1'
Step 'tour720-video'             {& pwsh -NoProfile -ExecutionPolicy Bypass -File $tour -PlayerDirectory $PlayerDirectory -OutputDirectory "$EvidenceRoot/tour720-video" -Video}
Step 'tour1080'                  {& pwsh -NoProfile -ExecutionPolicy Bypass -File $tour -PlayerDirectory $PlayerDirectory -OutputDirectory "$EvidenceRoot/tour1080" -Width 1920 -Height 1080}
Step 'tour1080-unrecorded'       {& pwsh -NoProfile -ExecutionPolicy Bypass -File $tour -PlayerDirectory $PlayerDirectory -OutputDirectory "$EvidenceRoot/tour1080-unrecorded" -Width 1920 -Height 1080}
Step 'tour1080-before-unrecorded'{& pwsh -NoProfile -ExecutionPolicy Bypass -File $tour -PlayerDirectory $PlayerDirectory -OutputDirectory "$EvidenceRoot/tour1080-before-unrecorded" -Width 1920 -Height 1080 -Before}
Step 'tour720-unrecorded'        {& pwsh -NoProfile -ExecutionPolicy Bypass -File $tour -PlayerDirectory $PlayerDirectory -OutputDirectory "$EvidenceRoot/tour720-unrecorded"}
Step 'smoke720'                  {& pwsh -NoProfile -ExecutionPolicy Bypass -File $smoke -PlayerDirectory $PlayerDirectory -OutputDirectory "$EvidenceRoot/smoke720"}
Step 'smoke1080'                 {& pwsh -NoProfile -ExecutionPolicy Bypass -File $smoke -PlayerDirectory $PlayerDirectory -OutputDirectory "$EvidenceRoot/smoke1080" -Width 1920 -Height 1080}
foreach($folder in 'tour720-video','tour1080','tour1080-unrecorded','tour1080-before-unrecorded','tour720-unrecorded'){
    $path=Join-Path $EvidenceRoot $folder
    if(Test-Path (Join-Path $path 'tour.json')){& python -I (Join-Path $repo 'tools/art/phase52b4/analyze_b4_tour.py') $path $(if($folder -eq 'tour720-video'){'--frames'}) | Out-Null}
}
$summary|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $EvidenceRoot 'evidence-run-summary.json') -Encoding UTF8
$summary|ConvertTo-Json
