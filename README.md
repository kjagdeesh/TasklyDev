# Taskly.Dev

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

### System diagnostics

- **Process Lookup** — Search and sort a read-only snapshot of running processes.
- **Environment Variables** — Browse and filter process, user, or machine environment variables without editing them.

The **Overview** screen provides searchable tool cards. **Preferences** provides theme selection and application information.

## Screens and usage

The [Application Guide](docs/application-guide.md) describes the shell, every screen, its visible controls, and common workflows.

## Requirements

- Windows (the application targets `net10.0-windows`)
- .NET SDK `10.0.403` or a later compatible patch version; `global.json` selects the SDK and allows latest-patch roll-forward
- Visual Studio 2022 or Visual Studio Code with the .NET SDK for development (optional)

## Run

From the repository root in PowerShell:

```powershell
dotnet run --project .\src\TasklyDev.App\TasklyDev.App.csproj
```

The window opens maximized with the standard Windows title bar. Use the sidebar to select a tool, or search for a tool by name or description.

## Build and test

```powershell
dotnet build .\TasklyDev.slnx
dotnet test .\tests\TasklyDev.Tests\TasklyDev.Tests.csproj
```

The test project uses xUnit and covers core services, view models, and WPF shell behavior.

## Installer and releases

Taskly.Dev ships as a self-contained MSI installer built with `installer\Build-Installer.ps1` and published through GitHub Actions. See [Building the installer and publishing a release](docs/releasing.md).

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
- Environment-variable values and tool inputs are not persisted by the application. Environment values may contain credentials, tokens, or private paths; review them before copying or sharing.
- Process and environment-variable screens read snapshots. They do not modify processes or environment values, and the app does not request elevation.
- Hashing a file reads the selected local file. MD5 is provided only for legacy checksum compatibility; do not use it for security-sensitive integrity checks or password storage.
- GUIDs are identifiers, not passwords or secrets.
- Theme selection is session-only and is not saved after closing the application.

## License and third-party notices

See [LICENSE](LICENSE). Release distributions include applicable third-party license notices.
