param([string]$DataFolder, [string]$AiTimeline, [string]$BaselineTimeline)
# Draws the baseline-vs-AI comparison charts for the presentation from the pulled run data
# (Desktop\NOVA_부하데이터 by default; run Pull-LoadData first). Uses the latest finished run of each
# mode unless timeline files are given. Output: <DataFolder>\Charts
#   1_부하_시간그래프.png  load over time (both modes) + AI belt speed, 60 threshold, height-change marker
#   2_부위별_부하.png      per-region mean and peak load, side by side
#   3_지표비교.png / 지표비교.csv  mean load, peak, time above 60, cumulative load, completion time
#   4_신체부하맵.png       average load per body region drawn on a body outline for each mode
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Windows.Forms.DataVisualization
Add-Type -AssemblyName System.Drawing
if (-not $DataFolder) { $DataFolder = Join-Path ([Environment]::GetFolderPath('Desktop')) 'NOVA_부하데이터' }
if (-not (Test-Path -LiteralPath $DataFolder)) { throw "Data folder not found: $DataFolder (run Pull-LoadData first)" }
function Latest($pattern) { Get-ChildItem -LiteralPath $DataFolder -Filter $pattern | Sort-Object Name | Select-Object -Last 1 }
if (-not $AiTimeline) { $f = Latest 'Timeline_*_AI.csv'; if ($f) { $AiTimeline = $f.FullName } }
if (-not $BaselineTimeline) { $f = Latest 'Timeline_*_Baseline.csv'; if ($f) { $BaselineTimeline = $f.FullName } }
if (-not $AiTimeline -or -not $BaselineTimeline) { throw 'Need one finished AI run and one finished baseline run (Timeline_*_AI.csv / Timeline_*_Baseline.csv).' }
$out = Join-Path $DataFolder 'Charts'
New-Item -ItemType Directory -Force -Path $out | Out-Null

$regions = '왼어깨', '오른어깨', '왼팔', '오른팔', '왼손목', '오른손목', '허리'
$inv = [Globalization.CultureInfo]::InvariantCulture
function Num($v) { [double]::Parse($v, $inv) }
$baseColor = [Drawing.Color]::FromArgb(150, 150, 150)
$aiColor = [Drawing.Color]::FromArgb(30, 120, 230)
$font = New-Object Drawing.Font('Malgun Gothic', 11)
$titleFont = New-Object Drawing.Font('Malgun Gothic', 15, [Drawing.FontStyle]::Bold)

function Load-Run($timelinePath) {
    $rows = @(Import-Csv -LiteralPath $timelinePath -Encoding UTF8)
    $runPath = $timelinePath -replace '\\Timeline_', '\Run_'
    $boxes = if (Test-Path -LiteralPath $runPath) { @(Import-Csv -LiteralPath $runPath -Encoding UTF8) } else { @() }
    $active = @($rows | Where-Object { $_.'정지중' -ne '1' })
    $t = 0.0; $mean = 0.0; $high = 0.0; $peak = 0.0; $prev = $null
    foreach ($r in $active) {
        $time = Num $r.'시간(s)'
        $dt = if ($prev -eq $null) { 1 } else { [Math]::Min(2.0, [Math]::Max(0.0, $time - $prev)) }
        $prev = $time; $load = Num $r.'전체부하'
        $t += $dt; $mean += $load * $dt
        if ($load -ge 60) { $high += $dt }
        $peak = [Math]::Max($peak, (Num $r.'최대부위부하'))
    }
    $regionMean = @(); $regionPeak = @()
    foreach ($name in $regions) {
        $m = 0.0; $p = 0.0
        foreach ($b in $boxes) { $m += Num $b.($name + '평균'); $p = [Math]::Max($p, (Num $b.($name + '최대'))) }
        $regionMean += $(if ($boxes.Count) { $m / $boxes.Count } else { 0 }); $regionPeak += $p
    }
    $pause = $rows | Where-Object { $_.'정지중' -eq '1' } | Select-Object -First 1
    [pscustomobject]@{
        Rows = $active; Boxes = $boxes
        MeanLoad = $(if ($t) { $mean / $t } else { 0 }); PeakLoad = $peak
        HighShare = $(if ($t) { $high / $t * 100 } else { 0 }); Cumulative = $mean
        Duration = $(if ($rows.Count) { Num $rows[-1].'시간(s)' } else { 0 })
        Completed = $(if ($rows.Count) { $rows[-1].'완료개수' } else { 0 })
        RegionMean = $regionMean; RegionPeak = $regionPeak
        PauseAt = $(if ($pause) { Num $pause.'시간(s)' } else { $null })
        MeanSpeed = $(if ($boxes.Count) { ($boxes | ForEach-Object { Num $_.'벨트속도(m/s)' } | Measure-Object -Average).Average } else { 0 })
    }
}
$base = Load-Run $BaselineTimeline
$ai = Load-Run $AiTimeline

function New-Chart($w, $h, $title) {
    $c = New-Object Windows.Forms.DataVisualization.Charting.Chart
    $c.Width = $w; $c.Height = $h; $c.BackColor = [Drawing.Color]::White
    [void]$c.Titles.Add($title); $c.Titles[0].Font = $titleFont
    $legend = New-Object Windows.Forms.DataVisualization.Charting.Legend; $legend.Font = $font; $legend.Docking = 'Top'; $c.Legends.Add($legend)
    $c
}
function New-Area($chart, $name, $yTitle, $xTitle) {
    $a = New-Object Windows.Forms.DataVisualization.Charting.ChartArea $name
    $a.AxisY.Title = $yTitle; $a.AxisX.Title = $xTitle
    foreach ($axis in $a.AxisX, $a.AxisY) { $axis.TitleFont = $font; $axis.LabelStyle.Font = $font; $axis.MajorGrid.LineColor = [Drawing.Color]::Gainsboro }
    $chart.ChartAreas.Add($a); $a
}
function Add-Line($chart, $area, $name, $color, $points, $width = 3, $dash = 'Solid') {
    $s = New-Object Windows.Forms.DataVisualization.Charting.Series $name
    $s.ChartType = 'Line'; $s.ChartArea = $area; $s.Color = $color; $s.BorderWidth = $width; $s.BorderDashStyle = $dash
    foreach ($p in $points) { [void]$s.Points.AddXY($p[0], $p[1]) }
    $chart.Series.Add($s)
}

# 1. Load over time + AI belt speed.
$chart = New-Chart 1600 1000 '시간에 따른 전체 부하 (기준 vs AI) · 같은 100개 / 200초'
$top = New-Area $chart 'load' '전체 부하 (0-100)' ''
$top.Position = New-Object Windows.Forms.DataVisualization.Charting.ElementPosition(0, 6, 100, 58)
$bottom = New-Area $chart 'speed' '벨트 속도 (m/s)' '시간 (초)'
$bottom.Position = New-Object Windows.Forms.DataVisualization.Charting.ElementPosition(0, 64, 100, 36)
$bottom.AlignWithChartArea = 'load'
$maxT = [Math]::Max($base.Duration, $ai.Duration)
foreach ($a in $top, $bottom) { $a.AxisX.Minimum = 0; $a.AxisX.Maximum = [Math]::Ceiling($maxT / 10) * 10 }
$top.AxisY.Minimum = 0; $top.AxisY.Maximum = 100
Add-Line $chart 'load' '기준 모드 부하' $baseColor ($base.Rows | ForEach-Object { , @((Num $_.'시간(s)'), (Num $_.'전체부하')) })
Add-Line $chart 'load' 'AI 모드 부하' $aiColor ($ai.Rows | ForEach-Object { , @((Num $_.'시간(s)'), (Num $_.'전체부하')) })
Add-Line $chart 'load' '피로 임계값 60' ([Drawing.Color]::Red) @(@(0, 60), @($top.AxisX.Maximum, 60)) 2 'Dash'
Add-Line $chart 'speed' '기준 벨트 속도' $baseColor ($base.Rows | ForEach-Object { , @((Num $_.'시간(s)'), (Num $_.'벨트속도(m/s)')) })
Add-Line $chart 'speed' 'AI 벨트 속도' $aiColor ($ai.Rows | ForEach-Object { , @((Num $_.'시간(s)'), (Num $_.'벨트속도(m/s)')) })
if ($ai.PauseAt -ne $null) {
    foreach ($area in $top, $bottom) {
        $line = New-Object Windows.Forms.DataVisualization.Charting.StripLine
        $line.IntervalOffset = $ai.PauseAt; $line.StripWidth = .6; $line.BackColor = [Drawing.Color]::DarkOrange
        if ($area -eq $top) { $line.Text = '작업대 높이 조정 (AI, 20개)'; $line.Font = $font; $line.TextAlignment = 'Far'; $line.ForeColor = [Drawing.Color]::DarkOrange }
        $area.AxisX.StripLines.Add($line)
    }
}
$chart.SaveImage((Join-Path $out '1_부하_시간그래프.png'), 'Png')

# 2. Per-region mean and peak load.
$chart = New-Chart 1600 900 '부위별 부하 (박스 들고 있는 동안)'
$areaMean = New-Area $chart 'mean' '평균 부하' ''; $areaPeak = New-Area $chart 'peak' '최대 부하' ''
foreach ($a in $areaMean, $areaPeak) { $a.AxisY.Minimum = 0; $a.AxisY.Maximum = 100; $a.AxisX.Interval = 1 }
foreach ($spec in @(@('mean', '평균'), @('peak', '최대'))) {
    foreach ($mode in @(@('기준', $base, $baseColor), @('AI', $ai, $aiColor))) {
        $s = New-Object Windows.Forms.DataVisualization.Charting.Series ("$($mode[0]) $($spec[1])")
        $s.ChartType = 'Column'; $s.ChartArea = $spec[0]; $s.Color = $mode[2]; $s.IsValueShownAsLabel = $true; $s.LabelFormat = '0'; $s.Font = $font
        $values = if ($spec[0] -eq 'mean') { $mode[1].RegionMean } else { $mode[1].RegionPeak }
        for ($i = 0; $i -lt $regions.Count; $i++) { [void]$s.Points.AddXY($regions[$i], $values[$i]) }
        $chart.Series.Add($s)
    }
}
$chart.SaveImage((Join-Path $out '2_부위별_부하.png'), 'Png')

# 3. Metrics table.
function Change($b, $a, $lowerIsBetter = $true) {
    if ($b -eq 0) { return '' }
    $pct = ($a - $b) / $b * 100
    '{0}{1:0.0}%' -f $(if ($pct -ge 0) { '+' } else { '' }), $pct
}
$metrics = @(
    @('평균 부하 (시간 평균)', $base.MeanLoad, $ai.MeanLoad, '0.0'),
    @('최대 부하 (최대 부위)', $base.PeakLoad, $ai.PeakLoad, '0.0'),
    @('부하 60 이상 시간 비율 (%)', $base.HighShare, $ai.HighShare, '0.0'),
    @('누적 부하 (점·초)', $base.Cumulative, $ai.Cumulative, '0'),
    @('완료 시간 (초)', $base.Duration, $ai.Duration, '0.0'),
    @('평균 벨트 속도 (m/s)', $base.MeanSpeed, $ai.MeanSpeed, '0.000')
)
$csv = @('지표,기준 모드,AI 모드,변화율')
foreach ($m in $metrics) { $csv += '{0},{1},{2},{3}' -f $m[0], $m[1].ToString($m[3], $inv), $m[2].ToString($m[3], $inv), (Change $m[1] $m[2]) }
$csv += '완료 개수,{0},{1},' -f $base.Completed, $ai.Completed
[IO.File]::WriteAllLines((Join-Path $out '지표비교.csv'), $csv, (New-Object Text.UTF8Encoding($true)))
$bmp = New-Object Drawing.Bitmap(1400, (120 + 70 * ($metrics.Count + 2)))
$g = [Drawing.Graphics]::FromImage($bmp); $g.TextRenderingHint = 'AntiAliasGridFit'; $g.Clear([Drawing.Color]::White)
$big = New-Object Drawing.Font('Malgun Gothic', 20); $bold = New-Object Drawing.Font('Malgun Gothic', 20, [Drawing.FontStyle]::Bold)
$g.DrawString('같은 생산성(100개)에서의 부하 비교', $titleFont, [Drawing.Brushes]::Black, 40, 30)
$cols = 40, 600, 860, 1120; $y = 100
$heads = '지표', '기준 모드', 'AI 모드', '변화율'
for ($i = 0; $i -lt 4; $i++) { $g.DrawString($heads[$i], $bold, [Drawing.Brushes]::Black, $cols[$i], $y) }
$y += 70
foreach ($m in $metrics + , @('완료 개수', [double]$base.Completed, [double]$ai.Completed, '0')) {
    $g.DrawLine([Drawing.Pens]::Gainsboro, 30, $y - 10, 1370, $y - 10)
    $g.DrawString($m[0], $big, [Drawing.Brushes]::Black, $cols[0], $y)
    $g.DrawString($m[1].ToString($m[3], $inv), $big, (New-Object Drawing.SolidBrush $baseColor), $cols[1], $y)
    $g.DrawString($m[2].ToString($m[3], $inv), $big, (New-Object Drawing.SolidBrush $aiColor), $cols[2], $y)
    $ch = Change $m[1] $m[2]
    $better = $m[2] -lt $m[1] -and $m[0] -notlike '완료*' -and $m[0] -notlike '*속도*'
    $g.DrawString($ch, $bold, $(if ($better) { [Drawing.Brushes]::SeaGreen } else { [Drawing.Brushes]::DimGray }), $cols[3], $y)
    $y += 70
}
$bmp.Save((Join-Path $out '3_지표비교.png'), [Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose()

# 4. Body map of the average load (same colour scale as the in-app heatmap: green 0 -> yellow 30 -> red 60+).
function LoadColor($score) {
    $k = [Math]::Min(1.0, [Math]::Max(0.0, $score / 60.0))
    if ($k -lt .5) { [Drawing.Color]::FromArgb([int](60 + 390 * $k), 200, 70) } else { [Drawing.Color]::FromArgb(255, [int](200 - 340 * ($k - .5)), 50) }
}
$bmp = New-Object Drawing.Bitmap(1400, 1000)
$g = [Drawing.Graphics]::FromImage($bmp); $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'; $g.Clear([Drawing.Color]::White)
$g.DrawString('부위별 평균 부하 (신체 맵)', $titleFont, [Drawing.Brushes]::Black, 40, 25)
$small = New-Object Drawing.Font('Malgun Gothic', 13, [Drawing.FontStyle]::Bold)
$panels = @(@('기준 모드', $base, 150), @('AI 모드', $ai, 850))
foreach ($p in $panels) {
    $x = $p[2]; $v = $p[1].RegionMean
    $g.DrawString($p[0], $bold, (New-Object Drawing.SolidBrush $(if ($p[0] -eq 'AI 모드') { $aiColor } else { $baseColor })), $x + 110, 80)
    $gray = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(225, 225, 225))
    $g.FillEllipse($gray, $x + 140, 140, 120, 140)                     # head
    $g.FillRectangle($gray, $x + 150, 520, 40, 340); $g.FillRectangle($gray, $x + 210, 520, 40, 340)  # legs
    $shape = @(
        @(0, @(($x + 60), 300, 90, 90)), @(1, @(($x + 250), 300, 90, 90)),
        @(2, @(($x + 40), 390, 60, 170)), @(3, @(($x + 300), 390, 60, 170)),
        @(4, @(($x + 30), 565, 70, 70)), @(5, @(($x + 300), 565, 70, 70)),
        @(6, @(($x + 140), 300, 120, 220)))
    foreach ($s in $shape) {
        $r = $s[1]; $score = $v[$s[0]]
        $g.FillRectangle((New-Object Drawing.SolidBrush (LoadColor $score)), $r[0], $r[1], $r[2], $r[3])
        $g.DrawRectangle([Drawing.Pens]::White, $r[0], $r[1], $r[2], $r[3])
        $g.DrawString(('{0:0}' -f $score), $small, [Drawing.Brushes]::Black, $r[0] + 8, $r[1] + 8)
    }
    $g.DrawString(('평균 부하 {0:0.0} · 60 이상 {1:0}% 시간' -f $p[1].MeanLoad, $p[1].HighShare), $font, [Drawing.Brushes]::Black, $x + 40, 900)
}
$g.DrawString('색: 초록 0 → 노랑 30 → 빨강 60 이상 (피로 임계값)', $font, [Drawing.Brushes]::DimGray, 40, 950)
$bmp.Save((Join-Path $out '4_신체부하맵.png'), [Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose()

Write-Output "기준: $(Split-Path $BaselineTimeline -Leaf)"
Write-Output "AI  : $(Split-Path $AiTimeline -Leaf)"
Write-Output "차트 저장: $out"
