# Manual test harness for ClaudeWatch. Shows raw output (with ANSI) and a
# color-stripped view so layout/width can be verified.
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot\..
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$exe = ".\dist\claudewatch.exe"

function Strip($s) { return ($s -replace "`e\[[0-9;]*m", "") }

function Transcript([long]$in, [long]$read, [long]$create, [string]$tool, [bool]$answered) {
    $lines = @(
        "{`"type`":`"assistant`",`"message`":{`"role`":`"assistant`",`"content`":[{`"type`":`"tool_use`",`"id`":`"tX`",`"name`":`"$tool`",`"input`":{}}],`"usage`":{`"input_tokens`":$in,`"cache_read_input_tokens`":$read,`"cache_creation_input_tokens`":$create}}}"
    )
    if ($answered) {
        $lines += "{`"type`":`"user`",`"message`":{`"role`":`"user`",`"content`":[{`"type`":`"tool_result`",`"tool_use_id`":`"tX`",`"content`":`"ok`"}]}}"
    }
    $f = Join-Path $env:TEMP ("cw-" + [guid]::NewGuid().ToString('N') + ".jsonl")
    Set-Content -Path $f -Value $lines -Encoding utf8
    return $f
}

function Run($json) {
    $out = $json | & $exe
    Write-Host ("  raw    : " + $out)
    Write-Host ("  plain  : " + (Strip $out) + "  [width=" + (Strip $out).Length + "]")
}

Write-Host "== Low (~30%, green), Opus 4.8 (1M context) -> strip suffix, label (200k?) ==" -ForegroundColor Cyan
$tp = (Transcript 50000 8000 2000 'Grep' $false).Replace('\','\\')
Run ('{"model":{"id":"claude-opus-4-8","display_name":"Opus 4.8 (1M context)"},"workspace":{"current_dir":"C:\\src\\CodeCompass"},"transcript_path":"' + $tp + '"}')

Write-Host "== Med (~65%, orange), 200k model, active Edit ==" -ForegroundColor Cyan
$tp = (Transcript 120000 8000 2000 'Edit' $false).Replace('\','\\')
Run ('{"model":{"id":"claude-sonnet-4-6","display_name":"Sonnet 4.5"},"workspace":{"current_dir":"/home/u/UtilityBelt"},"transcript_path":"' + $tp + '"}')

Write-Host "== High (~92%, red), tool answered -> Idle ==" -ForegroundColor Cyan
$tp = (Transcript 170000 12000 2000 'Bash' $true).Replace('\','\\')
Run ('{"model":{"id":"claude-opus-4-8[1m]","display_name":"Opus 4.8 (1M context)"},"workspace":{"current_dir":"C:\\x\\ClaudeWatch"},"transcript_path":"' + $tp + '"}')

Write-Host "== 1M model at ~92k -> label (1M), low green ==" -ForegroundColor Cyan
$tp = (Transcript 80000 10000 2000 'Read' $false).Replace('\','\\')
Run ('{"model":{"id":"claude-opus-4-8[1m]","display_name":"Opus 4.8 (1M context)"},"workspace":{"current_dir":"C:\\x\\Big"},"transcript_path":"' + $tp + '"}')

Write-Host "== Width tiers (Med case) ==" -ForegroundColor Cyan
$tp = (Transcript 120000 8000 2000 'Edit' $false).Replace('\','\\')
$json = '{"model":{"display_name":"Sonnet 4.5"},"workspace":{"current_dir":"C:\\x\\UtilityBelt"},"transcript_path":"' + $tp + '"}'
foreach ($w in 80,50,38,30) { $env:COLUMNS=$w; Write-Host ("  w=$w : " + (Strip ($json | & $exe))) }
Remove-Item Env:\COLUMNS

Write-Host "== NO_COLOR disables color ==" -ForegroundColor Cyan
$env:NO_COLOR='1'; $o = ($json | & $exe); Remove-Item Env:\NO_COLOR
Write-Host ("  has ESC: " + ($o.Contains([char]27)))
