# Building the installer and publishing a release

Taskly.Dev ships as a self-contained Windows installer (`.msi`). It includes the .NET runtime, so users do not need to install .NET.

## Prerequisites

- Windows 10/11 x64
- A Visual Studio version that supports .NET 10, with the **.NET desktop development** workload (or the .NET SDK version in `global.json`)
- Internet access for the first build (restores the WiX installer packages from NuGet)

## Build the installer locally

1. Open the solution in Visual Studio.
2. Open a terminal at the repository root (**View → Terminal**).
3. Run:

   ```powershell
   .\installer\Build-Installer.ps1 -Version 1.0.0
   ```

4. The results are written to `artifacts\`:
   - `TasklyDev-1.0.0-win-x64.msi` — the installer
   - `TasklyDev-1.0.0-win-x64.msi.sha256` — its SHA-256 checksum

The script publishes the app (self-contained, `win-x64`), then builds the WiX project in `installer\TasklyDev.Installer`. Every published file is included automatically. The installer is not part of `TasklyDev.slnx`, so normal solution builds are unaffected.

### Test the installer

Install it on a clean Windows machine or VM, then check that:

- the installer asks for administrator approval and installs to `C:\Program Files\Taskly.Dev`
- **Taskly.Dev** appears in the Start menu and starts
- installing a newer version upgrades in place
- **Settings → Apps → Installed apps → Taskly.Dev → Uninstall** removes it

Installers are unsigned, so Windows SmartScreen may show an "unknown publisher" warning. Signing requires a code-signing certificate.

## Publish a release on GitHub

Releases are built by `.github/workflows/release.yml` when you push a version tag.

1. Make sure `main` contains the code to release and the **Release** tests pass locally.
2. Create and push a tag. Use `v<major>.<minor>.<patch>` (a suffix such as `-beta.1` marks a pre-release):

   ```powershell
   git tag v1.0.0
   git push origin v1.0.0
   ```

   In Visual Studio you can also use **View → Git Repository → Tags → New Tag**, then push the tag.
3. Open the repository's **Actions** tab and watch the **Release** workflow. It runs the tests, builds the MSI, and creates the release.
4. Open **Releases**. The release is created as a **draft** with generated notes and the MSI and checksum attached. Review and edit the notes, then choose **Publish release**.

To publish immediately instead of creating a draft, set `RELEASE_AS_DRAFT` to `'false'` in the workflow.

### Dry run

To test the pipeline without releasing, open **Actions → Release → Run workflow**, enter a version, and run it. It builds the installer and uploads it as a workflow artifact only.

## Notes

- **Keep the `UpgradeCode` in `Package.wxs` unchanged.** It lets new versions upgrade older installs. A different value installs side by side.
- The app version comes from the tag. `<Version>` in `TasklyDev.App.csproj` only affects local builds, so keep it close to the latest release.
- The installer is built with WiX Toolset v6. WiX participates in the [Open Source Maintenance Fee](https://docs.firegiant.com/wix/osmf/); organizations above its revenue threshold are asked to sponsor the project.
