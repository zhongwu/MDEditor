# MDEditor — Build & Release Procedure

This document describes how to build, package, sign, and release MDEditor.

## Prerequisites

- Windows 10 1809+ / Windows 11
- .NET 10 SDK
- Visual Studio 2022 17.8+ with "Desktop development with C#" workload (optional, for IDE builds)
- Windows SDK (provides `signtool.exe`) — required for signing
- PowerShell 7+ (`pwsh`) for the packaging scripts

## Versioning

- The app version lives in `Directory.Build.props` (`<Version>`).
- Add a matching `## [x.y.z]` entry to `CHANGELOG.md` describing the release.
- Commit these changes before tagging.

## Local Build

Restore and build all projects:

```bash
dotnet restore
dotnet build MDEditor.slnx
```

Run the app directly:

```bash
dotnet run --project MDEditor/MDEditor.csproj
```

Or open `MDEditor.slnx` in Visual Studio and press F5.

Output (runnable app): `MDEditor\bin\Debug\net10.0-windows10.0.17763.0\MDEditor.exe`

## Packaging (MSIX + portable zip)

Generate installable artifacts with:

```powershell
pwsh tools/Build-Msix.ps1
```

Outputs are written to `artifacts\`:
- `artifacts\MDEditor-<version>-x64.msix` (and ARM64)
- `artifacts\MDEditor-<version>-portable.zip`

For local sideload testing with a self-signed certificate:

```powershell
pwsh tools/New-DevCertificate.ps1
pwsh tools/Build-Msix.ps1 -CertPath <path-to-dev.pfx>
```

The portable zip needs no signing.

## Code Signing

### Production

1. Obtain a code-signing certificate from a trusted CA (DigiCert, Sectigo, etc.). You receive a `.pfx` and a password.
2. Sign locally:
   ```powershell
   signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 `
     /f "cert.pfx" /p "<password>" MDEditor.exe
   ```
3. Verify:
   ```powershell
   signtool verify /pa /v MDEditor.exe
   ```

### GitHub Actions (CI signing)

Set these repository secrets (Settings → Secrets and variables → Actions):

| Secret | Value |
|--------|-------|
| `WINDOWS_CERT_PFX_BASE64` | Base64 of the `.pfx` (`[Convert]::ToBase64String([IO.File]::ReadAllBytes("cert.pfx"))`) |
| `WINDOWS_CERT_PASSWORD` | The `.pfx` password |
| `WINDOWS_CERT_SUBJECT` | The certificate subject (e.g. `CN=MDEditor`) |

Without these secrets the release still uploads packages, but the MSIX is unsigned (sideload-only). The portable zip is always unsigned by design.

### Security

- Never commit `.pfx` files or passwords. Keep `*.pfx` in `.gitignore`.
- Rotate the certificate/password immediately if leaked, then update the secrets.

## Releasing to GitHub

1. Ensure version + changelog changes are committed on `main`.
2. Create a tag:
   ```bash
   git tag vX.Y.Z
   ```
3. Push the tag (triggers `.github/workflows/release.yml`):
   ```bash
   git push origin vX.Y.Z
   ```
4. The workflow runs tests, builds x64 + ARM64 packages, and creates a GitHub Release with the matching `CHANGELOG.md` section as notes, attaching the MSIX and portable zip.

To push all local tags at once (only if intended): `git push origin --tags`.

Monitor progress under the repository's **Actions** tab; the release appears under **Releases** when complete.

## CI

- `.github/workflows/ci.yml` runs on push/PR: build + tests. No artifacts uploaded.
- `.github/workflows/release.yml` runs on `v*` tags: tests + multi-arch packaging + GitHub Release.

## Tests

```bash
dotnet test
```

## Related Files

- `Directory.Build.props` — app version
- `CHANGELOG.md` — release notes
- `tools/Build-Msix.ps1` — packaging script
- `tools/New-DevCertificate.ps1` — dev cert creation
- `tools/Generate-Icons.ps1` — icon generation
- `tools/Get-ReleaseNotes.ps1` — changelog extraction for releases
- `MDEditor/Packaging/` — MSIX manifest and assets