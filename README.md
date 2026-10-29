<p align="center">
  <img src="https://github.com/kjagdeesh/TasklyDev/blob/main/src/TasklyDev.App/Assets/TasklyDevLogo.png?raw=true" alt="Taskly.Dev logo" width="112">
</p>

<h1 align="center">taskly<span style="color:#F5B942">.dev</span></h1>

<p align="center">
  A Windows desktop workbench for common developer and system-diagnostic tasks.
</p>

<p align="center">
  <strong>WPF · .NET 10 · Windows x64</strong>
</p>

Taskly.Dev is a Windows desktop workbench for common developer and system-diagnostic tasks. It is built with WPF and .NET 10. Utility operations run locally; the application does not send telemetry or upload tool inputs.

## Features

### Developer tools

- **Base64 Encoder & Decoder** — Encode text or decode Base64 using Standard, URL-safe, and MIME variants.
- **Number Base Converter** — Convert integers between bases 2 and 36, including values larger than built-in integer types.
- **Unix Timestamp Converter** — Convert between Unix seconds/milliseconds and ISO 8601 dates with UTC and local-time results.
- **GUID Generator** — Generate up to 500 GUIDs per batch in standard .NET formats.
- **Hash Calculator** — Calculate MD5, SHA-256, or SHA-512 digests for UTF-8 text or a local file.
- **Color Converter** — Convert HEX, RGB, and HSL values, preview transparency, and choose a color using the Windows color dialog.
- **Regex Tester** — Test .NET regular expressions against sample text with options and a finite match timeout.
- **URL Encoder & Decoder** — Encode and decode URL components.
- **JWT Decoder & Inspector** — Decode and inspect JWT header and payload data locally. Decoding a token does not verify its signature.
- **UUID / GUID Validator & Parser** — Validate and inspect UUID/GUID strings and their component information.

### System diagnostics

- **Process Lookup** — Search and sort a read-only snapshot of running processes.
- **Environment Variables** — Browse and filter process, user, or machine environment variables without editing them.
- **System Information** — Inspect key operating-system and machine information.
- **.NET SDK & Workload Inspector** — Review installed .NET SDK versions, paths, workloads, and SDK-selection information.
- **Certificate Inspector** — Inspect certificate details from supported certificate sources. Use care when viewing or sharing certificate information.
- **Clipboard History** — Optionally capture and review clipboard entries locally, with controls to enable or disable history, pause capture, search entries, pin or delete items, and configure retention. Clipboard history is opt-in and visible to the user.

### Productivity and application tools

- **Global Command Search** — Open the command palette with `Ctrl+K` and find tools or supported application actions. The palette uses a catalog of safe actions and does not execute arbitrary shell commands.
- **Startup App Viewer** — View supported Run/RunOnce registry entries and Startup-folder items in read-only mode. It does not change startup configuration.
- **Clipboard Snippets** — Save and reuse user-authored snippets stored in a local JSON file. The application does not monitor the clipboard in the background for this feature.
- **Clipboard History** — An opt-in, visible clipboard-history feature with controls to enable or disable capture, pause collection, search entries, pin or delete items, and manage retention. History is stored locally; enable it only if you are comfortable storing copied content on your device.

The **Overview** screen provides searchable tool cards. **Preferences** provides theme selection and application information. The global command palette offers another way to locate tools and supported actions.

## Screens and usage

The [Application Guide](docs/application-guide.md) describes the application shell, screens, visible controls, and common workflows. Update this guide as screens or behavior change.

## Requirements

- Windows; the application targets `net10.0-windows`.
- .NET SDK `10.0.403` or a later compatible patch version. `global.json` selects the SDK and allows latest-patch roll-forward.
- Visual Studio 2022 or Visual Studio Code with the .NET SDK for development (optional).

The released MSI is intended for Windows x64. The installer is built as a self-contained package; development and source builds require the .NET SDK.

## Run

From the repository root in PowerShell:

```powershell
dotnet run --project .\src\TasklyDev.App\TasklyDev.App.csproj
```

The window opens maximized with the standard Windows title bar. Use the sidebar to select a tool, search for a tool by name or description, or press `Ctrl+K` to open Global Command Search.

## Build and test

```powershell
dotnet build .\TasklyDev.slnx
dotnet test .\tests\TasklyDev.Tests\TasklyDev.Tests.csproj
```

The test project uses xUnit and covers core services, view models, and WPF shell behavior.

## Installer and releases

Taskly.Dev ships as a self-contained MSI installer built with `installer\Build-Installer.ps1` and published through GitHub Actions. See [Building the installer and publishing a release](docs/releasing.md).

For the v1.0.0 commit history, see the [Full Changelog](https://github.com/kjagdeesh/TasklyDev/commits/v1.0.0).

## Project layout

```text
src/
  TasklyDev.App/       WPF application, views, view models, shared themes, and UI services
  TasklyDev.Core/      Platform-independent utility logic and data types
  TasklyDev.Windows/   Windows-specific process and environment-variable services
tests/
  TasklyDev.Tests/     Automated tests
```

The application follows a presentation/service split: WPF views bind to view models; utility operations are implemented in `TasklyDev.Core`; Windows-only diagnostics are implemented in `TasklyDev.Windows`; clipboard, theme, file, and color-dialog interactions are provided by app services.

## Privacy and safety notes

- Utility input is handled locally. The app does not send telemetry or upload tool inputs.
- Environment-variable values and tool inputs are not persisted by the application unless a feature explicitly stores user-selected data, such as Clipboard Snippets or the opt-in Clipboard History feature.
- Environment values may contain credentials, tokens, or private paths; review them before copying or sharing.
- Process and environment-variable screens read snapshots. They do not modify processes or environment values, and the app does not request elevation.
- Startup App Viewer is read-only: it displays supported startup entries and does not add, remove, or modify them.
- Clipboard Snippets stores user-authored snippets in a local JSON file.
- Clipboard History is opt-in. Copied content may contain passwords, access tokens, personal data, or confidential text; enable history only if appropriate for your device and workflow. Use its pause, delete, and retention controls as needed.
- JWT decoding displays token contents but does not establish that a token is authentic, valid, or safe to use. Treat decoded claims and tokens as sensitive.
- Certificate details can include identifying information. Review them before sharing screenshots or copied output.
- Hashing a file reads the selected local file. MD5 is provided only for legacy checksum compatibility; do not use it for security-sensitive integrity checks or password storage.
- GUIDs/UUIDs are identifiers, not passwords or secrets.
- Theme selection is session-only and is not saved after closing the application.


<p align="center">
    <img src="https://github.com/kjagdeesh/TasklyDev/blob/main/Taskly.Dev%20Poster.png?raw=true" alt="Taskly.Dev Poster"  />
</p>


## License and third-party notices

See [LICENSE](LICENSE). Release distributions include applicable third-party license notices.
