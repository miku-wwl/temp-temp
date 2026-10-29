[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$PlayerDirectory,
    [string]$EvidenceRoot='artifacts/visual-qa/phase-5.2/b5/player',
    [switch]$Minimal
)
# Runs every Windows-player evidence capture for the B5 pilot, one player at a time, recording each result honestly.
# All input is AUTOMATED (PlayerController.InputOverride, FixedFollowCamera25D zoom, InteractionDetector); no human played.
# The B5 and B4 (before mode) presentations run the same route in the same build, alternately, twice each, for the comparison.
$ErrorActionPreference='Continue'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
Set-Location $repo
$summary=[ordered]@{}
function Step([string]$name,[scriptblock]$body){
    Write-Output "=== $name $(Get-Date -Format HH:mm:ss)"
    try{& $body;$summary[$name]=if($LASTEXITCODE -eq 0 -or $null -eq $LASTEXITCODE){'EXIT_0'}else{'EXIT_'+$LASTEXITCODE}}catch{$summary[$name]='ERROR: '+$_.Exception.Message;Write-Output $_.Exception.Message}
}
$tour=Join-Path $repo 'tools/art/phase52b5/Run-B5Tour.ps1'
$smoke=Join-Path $repo 'tools/art/phase52b2/Run-PilotSmoke.ps1'
Step 'tour1080-video'               {& pwsh -NoProfile -ExecutionPolicy Bypass -File $tour -PlayerDirectory $PlayerDirectory -OutputDirectory "$EvidenceRoot/tour1080-video" -Width 1920 -Height 1080 -Video}
Step 'tour1080-unrecorded'          {& pwsh -NoProfile -ExecutionPolicy Bypass -File $tour -PlayerDirectory $PlayerDirectory -OutputDirectory "$EvidenceRoot/tour1080-unrecorded" -Width 1920 -Height 1080}
Step 'tour1080-before-unrecorded'   {& pwsh -NoProfile -ExecutionPolicy Bypass -File $tour -PlayerDirectory $PlayerDirectory -OutputDirectory "$EvidenceRoot/tour1080-before-unrecorded" -Width 1920 -Height 1080 -Before}
# -Minimal (the finalisation set): one recorded route, one unrecorded B5 and one unrecorded B4 route, the 1080p whole-game smoke
if(-not $Minimal){
Step 'tour1080-before-unrecorded-2' {& pwsh -NoProfile -ExecutionPolicy Bypass -File $tour -PlayerDirectory $PlayerDirectory -OutputDirectory "$EvidenceRoot/tour1080-before-unrecorded-2" -Width 1920 -Height 1080 -Before}
Step 'tour1080-unrecorded-2'        {& pwsh -NoProfile -ExecutionPolicy Bypass -File $tour -PlayerDirectory $PlayerDirectory -OutputDirectory "$EvidenceRoot/tour1080-unrecorded-2" -Width 1920 -Height 1080}
}
Step 'smoke1080'                    {& pwsh -NoProfile -ExecutionPolicy Bypass -File $smoke -PlayerDirectory $PlayerDirectory -OutputDirectory "$EvidenceRoot/smoke1080" -Width 1920 -Height 1080}
if(-not $Minimal){
Step 'smoke720'                     {& pwsh -NoProfile -ExecutionPolicy Bypass -File $smoke -PlayerDirectory $PlayerDirectory -OutputDirectory "$EvidenceRoot/smoke720"}
}
foreach($folder in 'tour1080-video','tour1080-unrecorded','tour1080-before-unrecorded','tour1080-before-unrecorded-2','tour1080-unrecorded-2'){
    $path=Join-Path $EvidenceRoot $folder
    if(Test-Path (Join-Path $path 'tour.json')){& python -I (Join-Path $repo 'tools/art/phase52b5/analyze_b5_tour.py') $path $(if($folder -eq 'tour1080-video'){'--frames'}) | Out-Null}
}
$summary|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $EvidenceRoot 'evidence-run-summary.json') -Encoding UTF8
$summary|ConvertTo-Json
if (@($summary.Values | Where-Object { $_ -ne 'EXIT_0' }).Count -gt 0) { exit 1 }
