$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $root

$buildDir = Join-Path $root "book\build"
$outDir = Join-Path $root "book\dist"
New-Item -ItemType Directory -Path $buildDir,$outDir -Force | Out-Null

pandoc book/chapters/01-prod-config-safety.md --from markdown+east_asian_line_breaks --standalone --to html5 --css book/assets/print.css --embed-resources --resource-path book/chapters --output book/build/01-prod-config-safety.html
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$edgeCandidates = @(
  "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
  "C:\Program Files\Microsoft\Edge\Application\msedge.exe"
)
$edge = $edgeCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $edge) { throw "Microsoft Edge was not found in its standard installation paths." }

$htmlPath = (Resolve-Path "book/build/01-prod-config-safety.html").Path
$htmlUri = "file:///" + ($htmlPath -replace "\\","/")
$profilePath = Join-Path $buildDir "edge-profile"
$stagePdf = Join-Path $buildDir ("render-" + [Guid]::NewGuid().ToString("N") + ".pdf")
$pdfArg = "--print-to-pdf=$stagePdf"
& $edge --headless --disable-gpu --no-first-run --no-default-browser-check --no-pdf-header-footer "--user-data-dir=$profilePath" $pdfArg $htmlUri
$edgeExit = $LASTEXITCODE
if ($edgeExit -ne 0) { throw "Edge headless returned exit code $edgeExit." }

$deadline = (Get-Date).AddSeconds(20)
$previousSize = 0
$stableReads = 0
while ((Get-Date) -lt $deadline -and $stableReads -lt 2) {
  if (Test-Path -LiteralPath $stagePdf) {
    $currentSize = (Get-Item -LiteralPath $stagePdf).Length
    if ($currentSize -gt 0 -and $currentSize -eq $previousSize) {
      $stableReads++
    } else {
      $stableReads = 0
    }
    $previousSize = $currentSize
  }
  Start-Sleep -Milliseconds 500
}
if ($stableReads -lt 2) { throw "Edge did not finish writing a stable PDF within 20 seconds: $stagePdf" }

$pdfPath = Join-Path $outDir "01-prod-config-safety.pdf"
Copy-Item -LiteralPath $stagePdf -Destination $pdfPath -Force
$pdf = Get-Item -LiteralPath $pdfPath
Write-Output "PASS: generated $($pdf.FullName) ($($pdf.Length) bytes)"
