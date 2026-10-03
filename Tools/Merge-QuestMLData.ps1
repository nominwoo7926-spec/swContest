$ErrorActionPreference = 'Stop'
$root = Join-Path (Split-Path $PSScriptRoot -Parent) 'Recordings/Quest'
$destination = Join-Path $root 'FactoryMLCombined'
New-Item -ItemType Directory -Force -Path $destination | Out-Null
$combined = @{}
foreach ($fileName in @('manufacturing_trials_v3.csv', 'manufacturing_unlabeled_v3.csv', 'agent_events_v3.csv')) {
    $sources = @()
    $legacy = Join-Path (Join-Path $root 'FactoryML') $fileName
    if (Test-Path -LiteralPath $legacy) { $sources += Get-Item -LiteralPath $legacy }
    $snapshots = Join-Path $root 'FactoryMLSnapshots'
    if (Test-Path -LiteralPath $snapshots) {
        $sources += @(Get-ChildItem -LiteralPath $snapshots -Recurse -File -Filter $fileName | Sort-Object FullName)
    }
    if ($sources.Count -eq 0) { Write-Output "No source yet: $fileName"; continue }
    $seen = @{}
    $headers = $null
    foreach ($source in $sources) {
        $rows = @(Import-Csv -LiteralPath $source.FullName)
        if ($rows.Count -eq 0) { continue }
        $currentHeaders = @($rows[0].PSObject.Properties.Name)
        if ($null -eq $headers) { $headers = $currentHeaders }
        elseif (($headers -join ',') -ne ($currentHeaders -join ',')) { throw "CSV schema differs: $($source.FullName)" }
        foreach ($row in $rows) {
            if (-not $row.session -or -not $row.trial -or -not $row.time) { throw "Missing session/trial/time: $($source.FullName)" }
            $key = "$($row.session)|$($row.trial)|$($row.time)"
            $content = ($headers | ForEach-Object { $row.$_ }) -join "`0"
            if ($seen.ContainsKey($key) -and $seen[$key].Content -ne $content) { throw "Conflicting sample: $key" }
            $seen[$key] = [pscustomobject]@{ Content = $content; Row = $row }
        }
    }
    $unique = @($seen.Keys | Sort-Object | ForEach-Object { $seen[$_].Row })
    if ($unique.Count -gt 0) {
        $target = Join-Path $destination $fileName
        $temporary = "$target.tmp"
        $unique | Export-Csv -LiteralPath $temporary -NoTypeInformation -Encoding UTF8
        Move-Item -LiteralPath $temporary -Destination $target -Force
    }
    $combined[$fileName] = $unique
    Write-Output "$($fileName): $($unique.Count) unique rows"
}
$trials = @()
if ($combined.ContainsKey('manufacturing_trials_v3.csv')) { $trials = @($combined['manufacturing_trials_v3.csv']) }
if ($trials.Count -gt 0) {
    $summary = foreach ($block in ($trials | Group-Object { "$($_.session)|$($_.trial)" })) {
        $rows = @($block.Group | Sort-Object { [double]::Parse($_.time, [Globalization.CultureInfo]::InvariantCulture) })
        $first = $rows[0]
        $last = $rows[-1]
        if (@($rows | Select-Object -ExpandProperty profile_id -Unique).Count -ne 1 -or
            @($rows | Select-Object -ExpandProperty feedback -Unique).Count -ne 1) { throw "Mixed participant or feedback: $($block.Name)" }
        [pscustomobject][ordered]@{
            session = $first.session
            profile_id = $first.profile_id
            trial = $first.trial
            sensor_seconds = $rows.Count
            feedback_1_to_5 = $first.feedback
            height_cm = $last.height_cm
            reach_cm = $last.reach_cm
            part_mode = $last.part_mode
            belt_speed_start_m_s = $first.belt_speed
            belt_speed_end_m_s = $last.belt_speed
            table_height_start_m = $first.table_height
            table_height_end_m = $last.table_height
            completed_start = $first.completed
            completed_end = $last.completed
        }
    }
    $target = Join-Path $destination 'work_blocks_summary.csv'
    $temporary = "$target.tmp"
    $summary | Export-Csv -LiteralPath $temporary -NoTypeInformation -Encoding UTF8
    Move-Item -LiteralPath $temporary -Destination $target -Force
    Write-Output "Labeled work blocks: $(@($summary).Count)"
}
