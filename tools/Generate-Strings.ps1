<#
.SYNOPSIS
    Regenerates the localized .resx files from MDEditor.Core/Resources/strings.tsv.

.DESCRIPTION
    strings.tsv is the single source of truth for UI text: one row per key with tab-separated
    columns  key | English | Simplified Chinese (zh-Hans) | Traditional Chinese (zh-Hant).
    A literal "\n" in a value becomes a line break. Edit the TSV, then run:

        pwsh tools/Generate-Strings.ps1

    and commit the TSV together with the regenerated Strings*.resx files.
#>
$ErrorActionPreference = 'Stop'

$resources = Join-Path $PSScriptRoot '..\MDEditor.Core\Resources'
$tsv = Join-Path $resources 'strings.tsv'

$columns = @(
    @{ Index = 1; File = 'Strings.resx' },
    @{ Index = 2; File = 'Strings.zh-Hans.resx' },
    @{ Index = 3; File = 'Strings.zh-Hant.resx' }
)

$rows = @()
$seen = @{}
$lineNumber = 0
foreach ($line in [System.IO.File]::ReadAllLines($tsv, [System.Text.Encoding]::UTF8)) {
    $lineNumber++
    if ($line.Trim().Length -eq 0) { continue }
    $fields = $line.Split("`t")
    if ($fields.Count -ne 4) { throw "strings.tsv line ${lineNumber}: expected 4 tab-separated fields, found $($fields.Count)." }
    if ($seen.ContainsKey($fields[0])) { throw "strings.tsv line ${lineNumber}: duplicate key '$($fields[0])'." }
    $seen[$fields[0]] = $true
    $rows += , $fields
}

foreach ($column in $columns) {
    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Indent = $true
    $settings.Encoding = New-Object System.Text.UTF8Encoding($false)
    $path = Join-Path $resources $column.File
    $writer = [System.Xml.XmlWriter]::Create($path, $settings)
    try {
        $writer.WriteStartElement('root')
        foreach ($header in @(
                @('resmimetype', 'text/microsoft-resx'),
                @('version', '2.0'),
                @('reader', 'System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089'),
                @('writer', 'System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089'))) {
            $writer.WriteStartElement('resheader')
            $writer.WriteAttributeString('name', $header[0])
            $writer.WriteElementString('value', $header[1])
            $writer.WriteEndElement()
        }

        foreach ($row in $rows) {
            $writer.WriteStartElement('data')
            $writer.WriteAttributeString('name', $row[0])
            $writer.WriteAttributeString('xml', 'space', 'http://www.w3.org/XML/1998/namespace', 'preserve')
            $writer.WriteElementString('value', $row[$column.Index].Replace('\n', "`n"))
            $writer.WriteEndElement()
        }

        $writer.WriteEndElement()
    }
    finally {
        $writer.Close()
    }

    Write-Host "Wrote $($rows.Count) strings to $($column.File)"
}
