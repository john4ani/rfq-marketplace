# Generates the real PDF fixtures used by the Company Vault smoke tests
# (tests/Fixtures/vault/*.pdf) from their HTML sources (tests/Fixtures/vault/src/*.html)
# using headless Chrome. Chrome embeds a font subset and a ToUnicode CMap, so the PDFs
# carry the Romanian diacritics as real selectable text (not outlines).
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File scripts/generate-vault-fixtures.ps1
#   powershell -ExecutionPolicy Bypass -File scripts/generate-vault-fixtures.ps1 -ChromePath "C:\...\chrome.exe"
#
# The PDFs are committed, so this only needs to run when the fixture content changes.

param(
    [string]$ChromePath = 'C:\Program Files\Google\Chrome\Application\chrome.exe'
)

$ErrorActionPreference = 'Continue'
$repoRoot = Split-Path -Parent $PSScriptRoot
$srcDir = Join-Path $repoRoot 'tests\Fixtures\vault\src'
$outDir = Join-Path $repoRoot 'tests\Fixtures\vault'

if (-not (Test-Path $ChromePath)) {
    throw "Chrome not found at '$ChromePath'. Pass -ChromePath to the msedge/chrome executable."
}
if (-not (Test-Path $srcDir)) {
    throw "Fixture HTML sources not found under '$srcDir'."
}
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

Get-ChildItem -Path $srcDir -Filter *.html | Sort-Object Name | ForEach-Object {
    $out = Join-Path $outDir ($_.BaseName + '.pdf')
    # --no-pdf-header-footer (new Chrome) / --print-to-pdf-no-header (older Chrome): unknown switches are ignored.
    # Chrome writes "N bytes written to file …" to stderr; 2>$null keeps PS 5.1 from treating it as an error.
    & $ChromePath --headless --disable-gpu --no-sandbox `
        --no-pdf-header-footer --print-to-pdf-no-header `
        "--print-to-pdf=$out" $_.FullName 2>$null | Out-Null
    if (-not (Test-Path $out)) {
        throw "Chrome did not produce '$out'."
    }
    Write-Host "Generated $out ($((Get-Item $out).Length) bytes)"
}
