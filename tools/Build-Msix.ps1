<#
.SYNOPSIS  Publishes MDEditor self-contained and packs it into an .msix (optionally signed) plus a portable zip.
.EXAMPLE   pwsh tools/Build-Msix.ps1                       # unsigned package in artifacts/
.EXAMPLE   pwsh tools/Build-Msix.ps1 -CertPath dev.pfx -CertPassword (Read-Host -AsSecureString)
.NOTES
  Requires the Windows 10/11 SDK (makeappx.exe, signtool.exe) for the .msix; the zip needs nothing extra.
  The package Publisher must equal the signing certificate's Subject, otherwise installation fails.
  Unsigned packages cannot be installed.
#>
param(
    [string]$Version,                                   # defaults to <Version> in Directory.Build.props
    [ValidateSet('win-x64', 'win-arm64')][string]$Runtime = 'win-x64',
    [string]$Publisher = 'CN=MDEditor Dev',
    [string]$PublisherDisplayName = 'zhongwu',
    [string]$IdentityName = 'zhongwu.MDEditor',
    [string]$CertPath,
    [securestring]$CertPassword,
    [string]$OutDir
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not $OutDir) { $OutDir = Join-Path $root 'artifacts' }
if (-not $Version) {
    $Version = ([xml](Get-Content (Join-Path $root 'Directory.Build.props'))).Project.PropertyGroup.Version
}
$v = [version]($Version -replace '-.*$')
$msixVersion = '{0}.{1}.{2}.0' -f $v.Major, $v.Minor, [Math]::Max($v.Build, 0)   # MSIX needs four parts; the last must be 0 for the Store
$arch = if ($Runtime -eq 'win-arm64') { 'arm64' } else { 'x64' }

$publish = Join-Path $OutDir "publish-$Runtime"
$stage = Join-Path $OutDir "msix-$Runtime"
Remove-Item $publish, $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $OutDir | Out-Null

dotnet publish (Join-Path $root 'MDEditor/MDEditor.csproj') -c Release -r $Runtime --self-contained true `
    -p:DebugType=None -p:DebugSymbols=false -p:Version=$Version -o $publish
if ($LASTEXITCODE) { throw 'dotnet publish failed' }

# Portable zip
$zip = Join-Path $OutDir "MDEditor-$Version-$Runtime-portable.zip"
Remove-Item $zip -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zip
"Portable zip: $zip"

# MSIX layout
Copy-Item $publish $stage -Recurse
Copy-Item (Join-Path $root 'MDEditor/Packaging/Assets') (Join-Path $stage 'Assets') -Recurse
$manifest = Get-Content (Join-Path $root 'MDEditor/Packaging/AppxManifest.template.xml') -Raw
$tokens = @{ IDENTITY_NAME = $IdentityName; PUBLISHER = $Publisher; PUBLISHER_DISPLAY_NAME = $PublisherDisplayName; VERSION = $msixVersion; ARCH = $arch }
foreach ($kv in $tokens.GetEnumerator()) {
    $manifest = $manifest.Replace("{{$($kv.Key)}}", [Security.SecurityElement]::Escape($kv.Value))
}
Set-Content (Join-Path $stage 'AppxManifest.xml') $manifest -Encoding UTF8

function Find-SdkTool([string]$name) {
    $cmd = Get-Command $name -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $kits = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin'
    $hit = Get-ChildItem $kits -Recurse -Filter $name -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '[\\/]x64[\\/]' } | Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $hit) { throw "$name not found - install the Windows 10/11 SDK" }
    $hit.FullName
}

$msix = Join-Path $OutDir "MDEditor-$Version-$arch.msix"
Remove-Item $msix -ErrorAction SilentlyContinue
& (Find-SdkTool 'makeappx.exe') pack /d $stage /p $msix /o
if ($LASTEXITCODE) { throw 'makeappx failed' }

if ($CertPath) {
    $plain = [Net.NetworkCredential]::new('', $CertPassword).Password
    & (Find-SdkTool 'signtool.exe') sign /fd SHA256 /a /f $CertPath /p $plain /tr http://timestamp.digicert.com /td SHA256 $msix
    if ($LASTEXITCODE) { throw 'signtool failed' }
    "Signed MSIX: $msix"
} else {
    "Unsigned MSIX (cannot be installed until signed): $msix"
}
