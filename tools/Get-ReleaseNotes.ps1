<#
.SYNOPSIS  Prints the CHANGELOG.md section for a version (used as the GitHub release body).
.EXAMPLE   pwsh tools/Get-ReleaseNotes.ps1 -Version 0.3.0
#>
param([Parameter(Mandatory)][string]$Version)
$ErrorActionPreference = 'Stop'
$changelog = Join-Path (Split-Path $PSScriptRoot -Parent) 'CHANGELOG.md'
$lines = Get-Content $changelog
$start = [Array]::FindIndex($lines, [Predicate[string]] { param($l) $l -match "^## \[$([regex]::Escape($Version))\]" })
if ($start -lt 0) { throw "No '## [$Version]' section in CHANGELOG.md - add release notes before tagging." }
$body = @()
for ($i = $start + 1; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '^## \[' -or $lines[$i] -match '^\[[^\]]+\]:\s') { break }
    $body += $lines[$i]
}
($body -join "`n").Trim()
