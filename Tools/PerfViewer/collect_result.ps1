# PerfViewer: 計測結果の収集(読み取り専用)
# 新しい記録を、1 つのテキストにまとめて画面に出し、perf_result_<日時>.txt として保存する(既定の保存先は、perf フォルダの 1 つ上 = Player.log と同じ場所)。
#   .\collect_result.ps1                  既定の perf フォルダの、いちばん新しい記録 1 つ
#   .\collect_result.ps1 -Last 3          新しい順に 3 つ
#   .\collect_result.ps1 -Root D:\copied\perf
param(
  [string]$Root = '',      # perf フォルダ。省略すると、AppData\LocalLow の下から自動で探す
  [int]$Last = 1,          # 新しい順に、何回分の記録を出すか
  [string]$LogPath = '',   # Player.log。省略すると、perf フォルダの 1 つ上を探す
  [string]$OutDir = ''     # レポート(txt)の保存先。省略すると、perf フォルダの 1 つ上
)

$ErrorActionPreference = 'Stop'
$productName = 'AimRacing2026_Unity6.3'  # ProjectSettings の productName
$inv = [Globalization.CultureInfo]::InvariantCulture
$report = New-Object System.Collections.Generic.List[string]

function Out-Line([string]$text) {
  $report.Add($text)
  Write-Host $text
}

# 1 つの記録の、列ごとの -1(取得できなかった値)と 0 の行数を数える
function Get-ColumnTable([string[]]$paths) {
  $rows = @()
  foreach ($p in $paths) { $rows += Import-Csv $p }
  if ($rows.Count -eq 0) { return '(データ行がありません)' }
  $table = $rows[0].PSObject.Properties.Name | ForEach-Object {
    $col = $_; $m1 = 0; $zero = 0; $numeric = 0; $d = 0.0
    foreach ($r in $rows) {
      if ([double]::TryParse($r.$col, [Globalization.NumberStyles]::Float, $inv, [ref]$d)) {
        $numeric++
        if ($d -eq -1) { $m1++ } elseif ($d -eq 0) { $zero++ }
      }
    }
    if ($numeric -gt 0) { [pscustomobject]@{ column = $col; rows = $rows.Count; minus1 = $m1; zero = $zero } }
  }
  return ($table | Format-Table -AutoSize | Out-String -Width 200)
}

# perf フォルダを探す
if ($Root -eq '') {
  $localLow = Join-Path $env:USERPROFILE 'AppData\LocalLow'
  foreach ($company in Get-ChildItem $localLow -Directory -ErrorAction SilentlyContinue) {
    $candidate = Join-Path (Join-Path $company.FullName $productName) 'perf'
    if (Test-Path $candidate) { $Root = $candidate; break }
  }
}
if ($Root -eq '' -or -not (Test-Path $Root)) {
  Write-Host 'perf フォルダが見つかりません。ゲームを -perf で起動して計測したか、-Root で場所を指定してください。'
  exit 1
}
if ($LogPath -eq '') { $LogPath = Join-Path (Split-Path $Root -Parent) 'Player.log' }

Out-Line ('=== 環境 / ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
Out-Line ('PC 名: ' + $env:COMPUTERNAME)
Out-Line ('OS: ' + [Environment]::OSVersion.VersionString)
try {
  foreach ($gpu in Get-CimInstance Win32_VideoController) { Out-Line ('GPU: ' + $gpu.Name + ' / ドライバー ' + $gpu.DriverVersion) }
} catch { Out-Line 'GPU: 取得できません' }
Out-Line ('nvml.dll(System32): ' + $(if (Test-Path (Join-Path $env:windir 'System32\nvml.dll')) { 'あり' } else { 'なし' }))
Out-Line ('perf フォルダ: ' + $Root)

$runs = Get-ChildItem $Root -Directory | Sort-Object LastWriteTime | Select-Object -Last $Last
if (-not $runs) { Out-Line '記録(フォルダ)がありません。'; }

foreach ($run in $runs) {
  Out-Line ''
  Out-Line ('=================== ' + $run.Name)
  $metaPath = Join-Path $run.FullName 'meta.json'
  Out-Line '--- meta.json'
  if (Test-Path $metaPath) { Get-Content $metaPath -Encoding UTF8 | ForEach-Object { Out-Line $_ } } else { Out-Line '(meta.json がありません)' }

  $csvs = @(Get-ChildItem $run.FullName -Filter 'perf_*.csv' | Sort-Object Name)
  Out-Line '--- CSV'
  if ($csvs.Count -eq 0) { Out-Line '(CSV がありません)'; continue }
  foreach ($c in $csvs) {
    $lines = [System.IO.File]::ReadAllLines($c.FullName)
    Out-Line ('{0}: {1:N0} 行(データ)/ {2:N1} MB / 最終更新 {3}' -f $c.Name, [Math]::Max(0, $lines.Length - 1), ($c.Length / 1MB), $c.LastWriteTime)
  }
  $firstCsv = $csvs[0].FullName
  $lastCsv = $csvs[$csvs.Count - 1].FullName
  Out-Line ('--- ' + $csvs[0].Name + ' の先頭')
  Get-Content $firstCsv -TotalCount 4 -Encoding UTF8 | ForEach-Object { Out-Line $_ }
  Out-Line ('--- ' + $csvs[$csvs.Count - 1].Name + ' の末尾')
  Get-Content $lastCsv -Tail 3 -Encoding UTF8 | ForEach-Object { Out-Line $_ }
  Out-Line '--- 列ごとの -1(取得できなかった)と 0 の行数(minus1 が rows と同じ列は取得できていない)'
  (Get-ColumnTable ($csvs | ForEach-Object { $_.FullName })).TrimEnd() -split "`r?`n" | ForEach-Object { Out-Line $_ }
}

Out-Line ''
Out-Line '=================== Player.log'
if (Test-Path $LogPath) {
  $logLines = @(Get-Content $LogPath -Encoding UTF8)
  Out-Line ('{0}: {1:N0} 行 / {2:N1} MB' -f $LogPath, $logLines.Count, ((Get-Item $LogPath).Length / 1MB))
  Out-Line '--- PerfCsvRecorder の行'
  $perfLines = @($logLines | Where-Object { $_ -match 'PerfCsvRecorder' })
  if ($perfLines.Count -eq 0) { Out-Line '(なし。-perf 付きで起動していないか、別の起動の Player.log です)' } else { $perfLines | ForEach-Object { Out-Line $_ } }
  Out-Line ('--- Exception を含む行: ' + @($logLines | Where-Object { $_ -match 'Exception' }).Count + ' 行')
} else {
  Out-Line ('Player.log が見つかりません: ' + $LogPath)
}

if ($OutDir -eq '') { $OutDir = Split-Path $Root -Parent }
$outFile = Join-Path $OutDir ('perf_result_' + (Get-Date -Format 'yyyyMMdd_HHmmss') + '.txt')
[System.IO.File]::WriteAllLines($outFile, $report, (New-Object System.Text.UTF8Encoding($true)))
Write-Host ''
Write-Host ('保存しました: ' + $outFile)
