<#
.SYNOPSIS  Creates a self-signed code-signing certificate for sideloading test MSIX builds.
.DESCRIPTION
  The certificate Subject must equal the manifest Publisher (Build-Msix.ps1 -Publisher). To install a
  package signed with it, import the .cer into "Trusted People" (LocalMachine) first. For public releases
  use a real code-signing certificate instead - a self-signed one is NOT trusted on other machines.
#>
param(
    [string]$Subject = 'CN=MDEditor Dev',
    [string]$OutDir = (Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/dev-cert'),
    [Parameter(Mandatory)][securestring]$Password
)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $OutDir | Out-Null
$cert = New-SelfSignedCertificate -Type Custom -Subject $Subject -KeyUsage DigitalSignature -FriendlyName 'MDEditor dev signing' `
    -CertStoreLocation 'Cert:\CurrentUser\My' -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
Export-PfxCertificate -Cert $cert -FilePath (Join-Path $OutDir 'MDEditorDev.pfx') -Password $Password | Out-Null
Export-Certificate -Cert $cert -FilePath (Join-Path $OutDir 'MDEditorDev.cer') | Out-Null
Remove-Item "Cert:\CurrentUser\My\$($cert.Thumbprint)"
"Wrote $OutDir\MDEditorDev.pfx and .cer (subject $Subject)"
