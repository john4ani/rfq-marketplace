$ProgressPreference = 'SilentlyContinue'
$ua = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126 Safari/537.36'
$root = 'e:\Business Development\rfq-marketplace\tools\SeapDataSheetPoc'
$corpus = Join-Path $root 'corpus'
New-Item -ItemType Directory -Force -Path $corpus | Out-Null

# noticeNo | sysNoticeTypeId | cNoticeId | noticeId
$targets = @(
  @{ no = 'SCN1179319'; type = 17; cN = 100244197; id = 101390283 },
  @{ no = 'SCN1179220'; type = 17; cN = 100244100; id = 101389289 },
  @{ no = 'SCN1179004'; type = 17; cN = 100243865; id = 101386993 },
  @{ no = 'SCN1179015'; type = 17; cN = 100243879; id = 101387046 },
  @{ no = 'CN1096139';  type = 2;  cN = 100211725; id = 101392312 },
  @{ no = 'CN1096126';  type = 2;  cN = 100211715; id = 101392143 },
  @{ no = 'CN1096093';  type = 2;  cN = 100211682; id = 101391938 }
)

$summary = @()
foreach ($t in $targets) {
  Start-Sleep -Seconds 2
  $s = New-Object Microsoft.PowerShell.Commands.WebRequestSession
  $view = "https://e-licitatie.ro/pub/notices/contract-notices/view/$($t.id)"
  try { Invoke-WebRequest -Uri $view -Headers @{ 'User-Agent' = $ua } -WebSession $s -UseBasicParsing -TimeoutSec 45 | Out-Null } catch { Write-Output "$($t.no): view bootstrap warn" }
  $manUrl = "https://e-licitatie.ro/api-pub/NoticeCommon/GetDfNoticeSectionFiles/?initNoticeId=$($t.cN)&sysNoticeTypeId=$($t.type)"
  $doc = $null
  $listed = @()
  try {
    $man = Invoke-WebRequest -Uri $manUrl -Headers @{ 'User-Agent' = $ua; Referer = $view; Accept = 'application/json' } -WebSession $s -UseBasicParsing -TimeoutSec 45
    $j = $man.Content | ConvertFrom-Json
    $listed = @($j.dfNoticeDocs)
    Write-Output "$($t.no): manifest docs=$($listed.Count)"
    $doc = $listed | Where-Object { $_.noticeDocumentName -match 'Instructiuni_ofertanti_FisaDate|FisaDate' } | Select-Object -First 1
    if (-not $doc) { $doc = $listed | Where-Object { $_.noticeDocumentName -match '\.pdf$' } | Select-Object -First 1 }
  } catch { Write-Output "$($t.no): manifest ERR $($_.Exception.Message)" }

  if (-not $doc) {
    $summary += [pscustomobject]@{ noticeNo = $t.no; type = $t.type; ok = $false; reason = "no df pdf ($($listed.Count) docs)"; size = 0 }
    $listed | Select-Object -First 6 | ForEach-Object { Write-Output "   - $($_.noticeDocumentName)" }
    continue
  }

  $safe = ($doc.noticeDocumentName -replace '[^A-Za-z0-9._-]', '_')
  $path = Join-Path $corpus "$($t.no)__$safe"
  $dlUrl = 'https://e-licitatie.ro/' + $doc.noticeDocumentUrl
  try {
    Invoke-WebRequest -Uri $dlUrl -Headers @{ 'User-Agent' = $ua; Referer = $view; Accept = '*/*' } -WebSession $s -UseBasicParsing -TimeoutSec 90 -OutFile $path
    $bytes = [System.IO.File]::ReadAllBytes($path)
    $head = [System.Text.Encoding]::ASCII.GetString($bytes[0..[Math]::Min(7, $bytes.Length - 1)])
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { $hashBytes = $sha.ComputeHash($bytes) } finally { $sha.Dispose() }
    $hash = [System.BitConverter]::ToString($hashBytes).Replace('-', '').ToLowerInvariant()
    $ok = $head.StartsWith('%PDF') -and $bytes.Length -gt 1000
    Write-Output "$($t.no): bytes=$($bytes.Length) head=$head ok=$ok docCode=$($doc.noticeDocumentCode)"
    $summary += [pscustomobject]@{ noticeNo = $t.no; type = $t.type; ok = $ok; reason = if ($ok) { "doc=$($doc.noticeDocumentCode)" } else { "bad head" }; size = $bytes.Length; sha256 = $hash }
    if (-not $ok) { Remove-Item $path -Force }
  } catch {
    Write-Output "$($t.no): download ERR $($_.Exception.Message)"
    $summary += [pscustomobject]@{ noticeNo = $t.no; type = $t.type; ok = $false; reason = "download err"; size = 0 }
    Remove-Item $path -Force -ErrorAction SilentlyContinue
  }
}

$summary | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $root 'corpus-summary.json') -Encoding utf8
Write-Output '--- summary ---'
$summary | Format-Table -AutoSize | Out-String -Width 220
